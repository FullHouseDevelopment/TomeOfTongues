using System.IO.Compression;
using NUnit.Framework;
using TomeOfTongues.Content.Schema;
using TomeOfTongues.Content.Tool;

namespace TomeOfTongues.Language.Japanese.Tests;

[TestFixture]
public sealed class JapanesePackTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "TomeOfTongues.Language.Japanese.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Built_artifact_is_valid_and_carries_the_original_content_rights_ledger()
    {
        var repositoryRoot = FindRepositoryRoot();
        var packagePath = Path.Combine(
            repositoryRoot,
            "artifacts",
            "language-packs",
            "tomeoftongues.japanese.totlang");

        TotlangPackageTool.Validate(packagePath);
        var manifest = TotlangPackageTool.ReadManifest(packagePath);

        Assert.Multiple(() =>
        {
            Assert.That(manifest.PackId, Is.EqualTo("tomeoftongues.japanese"));
            Assert.That(manifest.LanguageTag, Is.EqualTo("ja"));
            Assert.That(manifest.PackageVersion, Is.EqualTo("0.2.0"));
            Assert.That(manifest.CourseIds, Is.EqualTo(new[] { "japanese-starter" }));
            Assert.That(manifest.Assets, Is.Empty);
            Assert.That(manifest.Sources, Has.Count.EqualTo(1));
            Assert.That(manifest.Sources[0].RedistributionAllowed, Is.True);
            Assert.That(manifest.Sources[0].ModificationAllowed, Is.True);
            Assert.That(manifest.Sources[0].LicenseId, Is.EqualTo("cc-by-sa-4.0"));
            Assert.That(manifest.Licenses.Single().Id, Is.EqualTo("cc-by-sa-4.0"));
        });
    }

    [Test]
    public void Built_artifact_contains_six_ordered_practical_starter_lessons()
    {
        var packagePath = GetBuiltPackagePath();
        var catalog = ReadPackageDocument<TotlangCourseCatalog>(packagePath, "courses.json");
        var lessons = ReadLessons(packagePath).ToDictionary(lesson => lesson.Id);
        var expectedLessonIds = new[]
        {
            "starter-greetings",
            "starter-introductions",
            "starter-courtesy",
            "starter-requests",
            "starter-directions",
            "starter-transactions"
        };

        var course = catalog.Courses.Single();
        var unit = course.Units.Single();

        Assert.Multiple(() =>
        {
            Assert.That(course.Id, Is.EqualTo("japanese-starter"));
            Assert.That(course.ProficiencyBand, Is.EqualTo("starter"));
            Assert.That(unit.LessonIds, Is.EqualTo(expectedLessonIds));
            Assert.That(lessons.Keys, Is.EquivalentTo(expectedLessonIds));
        });

        foreach (var lessonId in expectedLessonIds)
        {
            var lesson = lessons[lessonId];
            Assert.Multiple(() =>
            {
                Assert.That(lesson.CourseId, Is.EqualTo(course.Id));
                Assert.That(lesson.UnitId, Is.EqualTo(unit.Id));
                Assert.That(lesson.Expressions, Has.Count.EqualTo(2));
                Assert.That(
                    lesson.Expressions.SelectMany(expression => expression.SourceIds),
                    Is.All.EqualTo("tomeoftongues-original-japanese"));
                Assert.That(
                    lesson.Expressions.SelectMany(expression => expression.Meanings),
                    Has.Some.Matches<MeaningDefinition>(
                        meaning => meaning.LanguageTag == "en"
                            && !string.IsNullOrWhiteSpace(meaning.Value)));
            });
        }
    }

    [Test]
    public void Starter_lessons_keep_speaking_and_script_production_non_gating()
    {
        var lessons = ReadLessons(GetBuiltPackagePath());

        Assert.That(lessons, Has.Count.EqualTo(6));
        foreach (var lesson in lessons)
        {
            var requiredExercises = lesson.Steps
                .Where(step => step.Progression == StepProgression.Required)
                .Select(step => step.Exercise)
                .OfType<ExerciseDefinition>()
                .ToArray();
            var spokenSteps = lesson.Steps
                .Where(step =>
                    step.Kind == StepKind.OptionalSpeakingOpportunity
                    || step.Exercise?.ResponseModality == ResponseModality.Spoken)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(
                    requiredExercises,
                    Is.All.Matches<ExerciseDefinition>(
                        exercise => exercise.ResponseModality != ResponseModality.Spoken
                            && exercise.ResponseModality != ResponseModality.Typed),
                    $"{lesson.Id} must not require speech or Japanese-script entry.");
                Assert.That(spokenSteps, Has.Length.EqualTo(1));
                Assert.That(
                    spokenSteps,
                    Is.All.Matches<StepDefinition>(
                        step => step.Progression == StepProgression.DeferredAllowed),
                    $"{lesson.Id} speaking practice must allow deferral.");
            });
        }
    }

    [Test]
    public void Source_with_denied_redistribution_rights_is_rejected()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceDirectory = Path.Combine(
            repositoryRoot,
            "TomeOfTongues.Language.Japanese",
            "Source");
        var copiedSource = Path.Combine(_temporaryDirectory, "Source");
        CopyDirectory(sourceDirectory, copiedSource);

        var manifestPath = Path.Combine(copiedSource, "manifest.json");
        var manifest = File
            .ReadAllText(manifestPath)
            .Replace(
                "\"redistributionAllowed\": true",
                "\"redistributionAllowed\": false",
                StringComparison.Ordinal);
        File.WriteAllText(manifestPath, manifest);

        var packagePath = Path.Combine(_temporaryDirectory, "japanese.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(copiedSource, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("does not permit redistribution"));
        Assert.That(File.Exists(packagePath), Is.False);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static string GetBuiltPackagePath() =>
        Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "language-packs",
            "tomeoftongues.japanese.totlang");

    private static IReadOnlyList<TotlangLesson> ReadLessons(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        return archive.Entries
            .Where(entry =>
                entry.FullName.StartsWith("lessons/", StringComparison.Ordinal)
                && entry.FullName.EndsWith(".json", StringComparison.Ordinal))
            .Select(entry => ReadPackageDocument<TotlangLesson>(entry))
            .OrderBy(lesson => lesson.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static TDocument ReadPackageDocument<TDocument>(
        string packagePath,
        string entryPath)
        where TDocument : class, ITotlangDocument
    {
        using var stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryPath)
            ?? throw new InvalidDataException($"Package entry '{entryPath}' is missing.");
        return ReadPackageDocument<TDocument>(entry);
    }

    private static TDocument ReadPackageDocument<TDocument>(ZipArchiveEntry entry)
        where TDocument : class, ITotlangDocument
    {
        using var reader = new StreamReader(entry.Open());
        return TotlangSchema.Deserialize<TDocument>(reader.ReadToEnd());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomeOfTongues.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing TomeOfTongues.slnx.");
    }
}
