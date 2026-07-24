using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TomeOfTongues.Content.Schema;

namespace TomeOfTongues.Content.Packaging;

public static class TotlangPackageArchive
{
    private const string ManifestPath = "manifest.json";
    private const string CourseCatalogPath = "courses.json";
    private const string LessonsPrefix = "lessons/";
    private const string AssetsPrefix = "assets/";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly DateTimeOffset StableEntryTime =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly HashSet<string> ExecutableExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".bat", ".class", ".cmd", ".com", ".dll", ".dylib", ".exe",
            ".jar", ".msi", ".ps1", ".scr", ".sh", ".so", ".wasm"
        };

    public static void Compile(string sourceDirectory, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var sourceRoot = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException(
                $"Language-pack source directory '{sourceRoot}' does not exist.");
        }

        var destination = Path.GetFullPath(destinationPath);
        if (!Path.GetExtension(destination).Equals(".totlang", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The destination must use the .totlang extension.",
                nameof(destinationPath));
        }

        if (IsWithinDirectory(sourceRoot, destination))
        {
            throw new ArgumentException(
                "The destination must be outside the language-pack source directory.",
                nameof(destinationPath));
        }

        var contents = ReadDirectory(sourceRoot);
        ValidateContents(contents);

        var destinationDirectory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var (path, bytes) in contents.OrderBy(entry => entry.Key, StringComparer.Ordinal))
                {
                    var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                    entry.LastWriteTime = StableEntryTime;
                    using var entryStream = entry.Open();
                    entryStream.Write(bytes);
                }
            }

            Validate(temporaryPath);
            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void Validate(string packagePath)
    {
        _ = ReadManifest(packagePath);
    }

    public static TotlangManifest ReadManifest(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var fullPath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The .totlang package does not exist.", fullPath);
        }

        var contents = ReadPackageContents(fullPath);
        return ValidateContents(contents);
    }

    private static Dictionary<string, byte[]> ReadPackageContents(string fullPath)
    {
        var contents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    throw new InvalidDataException(
                        $"Package entry '{entry.FullName}' must be a file.");
                }

                ValidateEntryPath(entry.FullName);
                if (!contents.TryAdd(entry.FullName, ReadEntry(entry)))
                {
                    throw new InvalidDataException(
                        $"Package entry '{entry.FullName}' is duplicated.");
                }
            }
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Could not read .totlang package '{fullPath}'.",
                exception);
        }

        return contents;
    }

    private static Dictionary<string, byte[]> ReadDirectory(string sourceRoot)
    {
        var contents = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };

        foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", options))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, filePath).Replace('\\', '/');
            ValidateEntryPath(relativePath);
            if (!contents.TryAdd(relativePath, File.ReadAllBytes(filePath)))
            {
                throw new InvalidDataException(
                    $"Source entry '{relativePath}' is duplicated.");
            }
        }

        return contents;
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static TotlangManifest ValidateContents(IReadOnlyDictionary<string, byte[]> contents)
    {
        var manifest = ReadRequiredDocument<TotlangManifest>(contents, ManifestPath);
        var catalog = ReadRequiredDocument<TotlangCourseCatalog>(contents, CourseCatalogPath);

        var lessons = contents
            .Where(entry =>
                entry.Key.StartsWith(LessonsPrefix, StringComparison.OrdinalIgnoreCase)
                && entry.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(entry => (
                Path: entry.Key,
                Lesson: ReadDocument<TotlangLesson>(entry.Value, entry.Key)))
            .ToArray();

        var allowedPaths = new HashSet<string>(
            [ManifestPath, CourseCatalogPath, .. lessons.Select(lesson => lesson.Path)],
            StringComparer.OrdinalIgnoreCase);

        ValidateManifest(manifest, contents, allowedPaths);
        ValidateCatalog(manifest, catalog, lessons);
        ValidateLessons(manifest, lessons);

        var undeclaredPath = contents.Keys.FirstOrDefault(path => !allowedPaths.Contains(path));
        if (undeclaredPath is not null)
        {
            throw new InvalidDataException(
                $"Package entry '{undeclaredPath}' is not declared by the package.");
        }

        return manifest;
    }

    private static void ValidateManifest(
        TotlangManifest manifest,
        IReadOnlyDictionary<string, byte[]> contents,
        ISet<string> allowedPaths)
    {
        RequireValue(manifest.PackId, "manifest pack ID");
        RequireValue(manifest.PackageVersion, "manifest package version");
        RequireValue(manifest.MinimumEngineVersion, "manifest minimum engine version");
        RequireValue(manifest.LanguageTag, "manifest language tag");

        var representationIds = RequireUnique(
            manifest.Representations,
            representation => representation.Id,
            "representation");
        var sourceIds = RequireUnique(manifest.Sources, source => source.Id, "source");
        var licenseIds = RequireUnique(manifest.Licenses, license => license.Id, "license");
        var assetIds = RequireUnique(manifest.Assets, asset => asset.Id, "asset");
        RequireUniqueValues(manifest.CourseIds, "manifest course");

        foreach (var representation in manifest.Representations)
        {
            RequireValue(representation.LanguageTag, $"representation '{representation.Id}' language tag");
            RequireValue(representation.ScriptTag, $"representation '{representation.Id}' script tag");
        }

        foreach (var source in manifest.Sources)
        {
            if (!licenseIds.Contains(source.LicenseId))
            {
                throw new InvalidDataException(
                    $"Source '{source.Id}' references unknown license '{source.LicenseId}'.");
            }

            if (!source.RedistributionAllowed)
            {
                throw new InvalidDataException(
                    $"Source '{source.Id}' does not permit redistribution.");
            }
        }

        foreach (var asset in manifest.Assets)
        {
            ValidateEntryPath(asset.Path);
            if (!asset.Path.StartsWith(AssetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Asset '{asset.Id}' path '{asset.Path}' must be below '{AssetsPrefix}'.");
            }

            if (asset.SourceId is not null && !sourceIds.Contains(asset.SourceId))
            {
                throw new InvalidDataException(
                    $"Asset '{asset.Id}' references unknown source '{asset.SourceId}'.");
            }

            if (!contents.TryGetValue(asset.Path, out var bytes))
            {
                throw new InvalidDataException(
                    $"Asset '{asset.Id}' is missing package entry '{asset.Path}'.");
            }

            var actualChecksum = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!actualChecksum.Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Asset '{asset.Id}' does not match its SHA-256 checksum.");
            }

            allowedPaths.Add(asset.Path);
        }

        _ = representationIds;
        _ = assetIds;
    }

    private static void ValidateCatalog(
        TotlangManifest manifest,
        TotlangCourseCatalog catalog,
        IReadOnlyList<(string Path, TotlangLesson Lesson)> lessons)
    {
        var courseIds = RequireUnique(catalog.Courses, course => course.Id, "course");
        EnsureSameIds(manifest.CourseIds, courseIds, "manifest and course catalog");

        var lessonIds = RequireUnique(lessons, lesson => lesson.Lesson.Id, "lesson");
        var declaredLessonIds = new HashSet<string>(StringComparer.Ordinal);
        var unitIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var course in catalog.Courses)
        {
            foreach (var unit in course.Units)
            {
                RequireValue(unit.Id, "unit ID");
                if (!unitIds.Add(unit.Id))
                {
                    throw new InvalidDataException($"Unit ID '{unit.Id}' is duplicated.");
                }

                foreach (var lessonId in unit.LessonIds)
                {
                    RequireValue(lessonId, $"lesson ID in unit '{unit.Id}'");
                    if (!declaredLessonIds.Add(lessonId))
                    {
                        throw new InvalidDataException(
                            $"Lesson ID '{lessonId}' is declared more than once.");
                    }

                    var lesson = lessons.SingleOrDefault(item =>
                        item.Lesson.Id.Equals(lessonId, StringComparison.Ordinal));
                    if (lesson.Lesson is null)
                    {
                        throw new InvalidDataException(
                            $"Unit '{unit.Id}' references missing lesson '{lessonId}'.");
                    }

                    if (!lesson.Lesson.CourseId.Equals(course.Id, StringComparison.Ordinal)
                        || !lesson.Lesson.UnitId.Equals(unit.Id, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"Lesson '{lessonId}' does not match course '{course.Id}' and unit '{unit.Id}'.");
                    }
                }
            }
        }

        EnsureSameIds(declaredLessonIds, lessonIds, "course catalog and lesson documents");

        foreach (var (path, lesson) in lessons)
        {
            var expectedPath = $"{LessonsPrefix}{lesson.Id}.json";
            if (!path.Equals(expectedPath, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Lesson '{lesson.Id}' must use package path '{expectedPath}'.");
            }
        }
    }

    private static void ValidateLessons(
        TotlangManifest manifest,
        IReadOnlyList<(string Path, TotlangLesson Lesson)> lessons)
    {
        var representationIds = manifest.Representations
            .Select(representation => representation.Id)
            .ToHashSet(StringComparer.Ordinal);
        var assetIds = manifest.Assets
            .Select(asset => asset.Id)
            .ToHashSet(StringComparer.Ordinal);
        var sourceIds = manifest.Sources
            .Select(source => source.Id)
            .ToHashSet(StringComparer.Ordinal);
        var allowedNormalizations = manifest.NormalizationOperations.ToHashSet();

        foreach (var (_, lesson) in lessons)
        {
            var objectiveIds = RequireUnique(
                lesson.Objectives,
                objective => objective.Id,
                $"objective in lesson '{lesson.Id}'");
            var expressionIds = RequireUnique(
                lesson.Expressions,
                expression => expression.Id,
                $"expression in lesson '{lesson.Id}'");
            RequireUnique(
                lesson.Steps,
                step => step.Id,
                $"step in lesson '{lesson.Id}'");

            foreach (var objective in lesson.Objectives)
            {
                ValidateOptionalReference(
                    objective.RepresentationId,
                    representationIds,
                    $"Objective '{objective.Id}' representation");
            }

            foreach (var expression in lesson.Expressions)
            {
                var textRepresentationIds = RequireUnique(
                    expression.Representations,
                    representation => representation.Id,
                    $"text representation in expression '{expression.Id}'");

                foreach (var representation in expression.Representations)
                {
                    ValidateReference(
                        representation.RepresentationId,
                        representationIds,
                        $"Text representation '{representation.Id}' representation");

                    foreach (var annotation in representation.Annotations)
                    {
                        ValidateOptionalReference(
                            annotation.RepresentationId,
                            representationIds,
                            $"Annotation '{annotation.Id}' representation");
                    }
                }

                foreach (var sourceId in expression.SourceIds)
                {
                    ValidateReference(
                        sourceId,
                        sourceIds,
                        $"Expression '{expression.Id}' source");
                }

                foreach (var audio in expression.Audio)
                {
                    ValidateReference(
                        audio.AssetId,
                        assetIds,
                        $"Expression '{expression.Id}' audio asset");
                    ValidateOptionalReference(
                        audio.TranscriptRepresentationId,
                        textRepresentationIds,
                        $"Expression '{expression.Id}' audio transcript");
                }
            }

            foreach (var step in lesson.Steps)
            {
                foreach (var expressionId in step.ExpressionIds)
                {
                    ValidateReference(
                        expressionId,
                        expressionIds,
                        $"Step '{step.Id}' expression");
                }

                if (step.Exercise is not { } exercise)
                {
                    continue;
                }

                foreach (var targetId in exercise.TargetIds)
                {
                    ValidateReference(targetId, expressionIds, $"Exercise '{exercise.Id}' target");
                }

                foreach (var answer in exercise.AcceptableAnswers)
                {
                    ValidateOptionalReference(
                        answer.RepresentationId,
                        representationIds,
                        $"Exercise '{exercise.Id}' answer representation");

                    foreach (var operation in answer.NormalizationOperations)
                    {
                        if (!allowedNormalizations.Contains(operation))
                        {
                            throw new InvalidDataException(
                                $"Exercise '{exercise.Id}' uses undeclared normalization '{operation}'.");
                        }
                    }
                }

                foreach (var assistance in exercise.Assistance)
                {
                    foreach (var representationId in assistance.RepresentationIds)
                    {
                        ValidateReference(
                            representationId,
                            representationIds,
                            $"Exercise '{exercise.Id}' assistance representation");
                    }
                }

                foreach (var evidence in exercise.Evidence)
                {
                    ValidateReference(
                        evidence.ObjectiveId,
                        objectiveIds,
                        $"Exercise '{exercise.Id}' evidence objective");
                    ValidateOptionalReference(
                        evidence.RepresentationId,
                        representationIds,
                        $"Exercise '{exercise.Id}' evidence representation");
                }
            }
        }
    }

    private static TDocument ReadRequiredDocument<TDocument>(
        IReadOnlyDictionary<string, byte[]> contents,
        string path)
        where TDocument : class, ITotlangDocument
    {
        if (!contents.TryGetValue(path, out var bytes))
        {
            throw new InvalidDataException($"Package is missing required entry '{path}'.");
        }

        return ReadDocument<TDocument>(bytes, path);
    }

    private static TDocument ReadDocument<TDocument>(byte[] bytes, string path)
        where TDocument : class, ITotlangDocument
    {
        try
        {
            return TotlangSchema.Deserialize<TDocument>(StrictUtf8.GetString(bytes));
        }
        catch (Exception exception) when (
            exception is DecoderFallbackException
                or ArgumentException
                or TotlangSchemaException)
        {
            throw new InvalidDataException(
                $"Package entry '{path}' is not a valid {typeof(TDocument).Name} document.",
                exception);
        }
    }

    private static HashSet<string> RequireUnique<T>(
        IEnumerable<T> items,
        Func<T, string> idSelector,
        string kind)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var id = idSelector(item);
            RequireValue(id, $"{kind} ID");
            if (!ids.Add(id))
            {
                throw new InvalidDataException($"{kind} ID '{id}' is duplicated.");
            }
        }

        return ids;
    }

    private static void RequireUniqueValues(IEnumerable<string> values, string kind)
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            RequireValue(value, $"{kind} ID");
            if (!unique.Add(value))
            {
                throw new InvalidDataException($"{kind} ID '{value}' is duplicated.");
            }
        }
    }

    private static void ValidateReference(
        string id,
        IReadOnlySet<string> knownIds,
        string description)
    {
        RequireValue(id, description);
        if (!knownIds.Contains(id))
        {
            throw new InvalidDataException($"{description} '{id}' does not exist.");
        }
    }

    private static void ValidateOptionalReference(
        string? id,
        IReadOnlySet<string> knownIds,
        string description)
    {
        if (id is not null)
        {
            ValidateReference(id, knownIds, description);
        }
    }

    private static void EnsureSameIds(
        IEnumerable<string> left,
        IReadOnlySet<string> right,
        string description)
    {
        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        if (!leftSet.SetEquals(right))
        {
            throw new InvalidDataException(
                $"The {description} IDs do not match.");
        }
    }

    private static void RequireValue(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{description} must not be empty.");
        }
    }

    private static void ValidateEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.StartsWith('/')
            || path.Contains('\\')
            || path.Contains(':'))
        {
            throw new InvalidDataException($"Package entry path '{path}' is not portable.");
        }

        var segments = path.Split('/');
        if (segments.Any(segment =>
                string.IsNullOrEmpty(segment)
                || segment.Equals(".", StringComparison.Ordinal)
                || segment.Equals("..", StringComparison.Ordinal)))
        {
            throw new InvalidDataException($"Package entry path '{path}' is unsafe.");
        }

        if (ExecutableExtensions.Contains(Path.GetExtension(path)))
        {
            throw new InvalidDataException(
                $"Executable package entry '{path}' is not allowed.");
        }
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal);
    }
}
