using System.IO.Compression;
using System.Security.Cryptography;
using NUnit.Framework;
using TomeOfTongues.Content.Schema;
using TomeOfTongues.Content.Tool;

namespace TomeOfTongues.Content.Tool.Tests;

[TestFixture]
public sealed class TotlangPackageToolTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "TomeOfTongues.Content.Tool.Tests",
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
    public void Compile_creates_a_reopenable_declarative_package()
    {
        var sourceDirectory = CreateValidSource();
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");

        TotlangPackageTool.Compile(sourceDirectory, packagePath);
        TotlangPackageTool.Validate(packagePath);

        using var stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                entries,
                Is.EqualTo(
                [
                    "assets/audio/prompt.ogg",
                    "courses.json",
                    "lessons/lesson-1.json",
                    "manifest.json"
                ]));
            Assert.That(
                archive.Entries.Select(entry => entry.LastWriteTime.DateTime).Distinct().Single(),
                Is.EqualTo(new DateTime(1980, 1, 1, 0, 0, 0)));
            Assert.That(entries, Has.None.EndsWith(".dll"));
        });
    }

    [Test]
    public void Compile_rejects_checksum_mismatch_without_replacing_destination()
    {
        var sourceDirectory = CreateValidSource();
        File.WriteAllText(
            Path.Combine(sourceDirectory, "assets", "audio", "prompt.ogg"),
            "changed");
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");
        File.WriteAllText(packagePath, "existing package");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("SHA-256"));
        Assert.That(File.ReadAllText(packagePath), Is.EqualTo("existing package"));
    }

    [Test]
    public void Compile_rejects_content_without_redistribution_rights()
    {
        var sourceDirectory = CreateValidSource(redistributionAllowed: false);
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("does not permit redistribution"));
        Assert.That(File.Exists(packagePath), Is.False);
    }

    [Test]
    public void Compile_rejects_incomplete_source_provenance()
    {
        var sourceDirectory = CreateValidSource(origin: " ");
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("source 'source-1' origin"));
    }

    [Test]
    public void Compile_rejects_missing_attribution()
    {
        var sourceDirectory = CreateValidSource(attribution: "");
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("source 'source-1' attribution"));
    }

    [Test]
    public void Compile_rejects_incomplete_license_notice()
    {
        var sourceDirectory = CreateValidSource(licenseName: " ");
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("license 'cc-by-sa-4.0' name"));
    }

    [Test]
    public void Compile_rejects_malformed_checksum()
    {
        var sourceDirectory = CreateValidSource(assetSha256: new string('g', 64));
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("64-character hexadecimal"));
    }

    [Test]
    public void Validate_rejects_an_executable_package_entry()
    {
        var sourceDirectory = CreateValidSource();
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");
        TotlangPackageTool.Compile(sourceDirectory, packagePath);

        using (var stream = File.Open(packagePath, FileMode.Open, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            var entry = archive.CreateEntry("payload.dll");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not executable, but executable content is forbidden by path");
        }

        Assert.That(
            () => TotlangPackageTool.Validate(packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("Executable package entry"));
    }

    [Test]
    public void Compile_rejects_a_missing_lesson_reference()
    {
        var sourceDirectory = CreateValidSource();
        File.Delete(Path.Combine(sourceDirectory, "lessons", "lesson-1.json"));
        var packagePath = Path.Combine(_temporaryDirectory, "fixture.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("references missing lesson"));
    }

    [Test]
    public void Compile_creates_and_validates_a_v2_package()
    {
        var sourceDirectory = CreateValidSource(schemaVersion: TotlangSchema.CurrentVersion);
        var packagePath = Path.Combine(_temporaryDirectory, "fixture-v2.totlang");

        TotlangPackageTool.Compile(sourceDirectory, packagePath);
        TotlangPackageTool.Validate(packagePath);

        using var stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.That(
            archive.Entries.Select(entry => entry.FullName),
            Does.Contain("proficiency.json"));
    }

    [Test]
    public void Compile_rejects_a_missing_v2_milestone_reference()
    {
        var sourceDirectory = CreateValidSource(
            schemaVersion: TotlangSchema.CurrentVersion,
            milestoneIds: ["fixture.pack:missing"]);
        var packagePath = Path.Combine(_temporaryDirectory, "fixture-v2.totlang");

        Assert.That(
            () => TotlangPackageTool.Compile(sourceDirectory, packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("references missing proficiency milestone"));
    }

    private string CreateValidSource(
        bool redistributionAllowed = true,
        string origin = "Original fixture",
        string attribution = "Original fixture content",
        string licenseName = "CC BY-SA 4.0",
        string? assetSha256 = null,
        int schemaVersion = TotlangSchema.LegacyVersion,
        IReadOnlyList<string>? milestoneIds = null)
    {
        var sourceDirectory = Path.Combine(_temporaryDirectory, "source");
        var lessonDirectory = Path.Combine(sourceDirectory, "lessons");
        var assetDirectory = Path.Combine(sourceDirectory, "assets", "audio");
        Directory.CreateDirectory(lessonDirectory);
        Directory.CreateDirectory(assetDirectory);

        var assetBytes = "fixture audio"u8.ToArray();
        File.WriteAllBytes(Path.Combine(assetDirectory, "prompt.ogg"), assetBytes);

        var manifest = new TotlangManifest
        {
            SchemaVersion = schemaVersion,
            PackId = "fixture.pack",
            PackageVersion = "1.0.0",
            MinimumEngineVersion = "1.0.0",
            LanguageTag = "he",
            LocaleTags = ["he-IL"],
            DisplayNames =
            [
                new LocalizedText
                {
                    LanguageTag = "en",
                    Value = "Fixture pack"
                }
            ],
            Representations =
            [
                new RepresentationDefinition
                {
                    Id = "hebr",
                    LanguageTag = "he",
                    ScriptTag = "Hebr",
                    Direction = TextDirection.RightToLeft
                }
            ],
            NormalizationOperations = [NormalizationOperation.UnicodeNfc],
            CourseIds = ["course-1"],
            Assets =
            [
                new AssetDefinition
                {
                    Id = "audio-1",
                    Path = "assets/audio/prompt.ogg",
                    MediaType = "audio/ogg",
                    Sha256 = assetSha256 ?? Convert.ToHexStringLower(SHA256.HashData(assetBytes)),
                    SourceId = "source-1"
                }
            ],
            Sources =
            [
                new SourceDefinition
                {
                    Id = "source-1",
                    Origin = origin,
                    Author = "TomeOfTongues contributors",
                    Reviewer = "Fixture reviewer",
                    LicenseId = "cc-by-sa-4.0",
                    Attribution = attribution,
                    RedistributionAllowed = redistributionAllowed,
                    ModificationAllowed = true,
                    RecordedOn = new DateOnly(2026, 7, 24)
                }
            ],
            Licenses =
            [
                new LicenseDefinition
                {
                    Id = "cc-by-sa-4.0",
                    Name = licenseName,
                    Uri = "https://creativecommons.org/licenses/by-sa/4.0/"
                }
            ]
        };

        var catalog = new TotlangCourseCatalog
        {
            SchemaVersion = schemaVersion,
            Courses =
            [
                new CourseDefinition
                {
                    Id = "course-1",
                    Revision = 1,
                    DisplayNames =
                    [
                        new LocalizedText
                        {
                            LanguageTag = "en",
                            Value = "Fixture course"
                        }
                    ],
                    ProficiencyBand = schemaVersion == TotlangSchema.LegacyVersion
                        ? "beginner"
                        : null,
                    Proficiency = schemaVersion == TotlangSchema.CurrentVersion
                        ? CreateV2CourseProficiency(
                            milestoneIds ?? ["fixture.pack:survival-exchange"])
                        : null,
                    Units =
                    [
                        new UnitDefinition
                        {
                            Id = "unit-1",
                            Revision = 1,
                            DisplayNames =
                            [
                                new LocalizedText
                                {
                                    LanguageTag = "en",
                                    Value = "Fixture unit"
                                }
                            ],
                            LessonIds = ["lesson-1"]
                        }
                    ]
                }
            ]
        };

        var lesson = new TotlangLesson
        {
            SchemaVersion = schemaVersion,
            Id = "lesson-1",
            Revision = 1,
            CourseId = "course-1",
            UnitId = "unit-1",
            DisplayNames =
            [
                new LocalizedText
                {
                    LanguageTag = "en",
                    Value = "Fixture lesson"
                }
            ],
            Objectives = [],
            Expressions = [],
            Steps = []
        };

        File.WriteAllText(
            Path.Combine(sourceDirectory, "manifest.json"),
            TotlangSchema.Serialize(manifest));
        File.WriteAllText(
            Path.Combine(sourceDirectory, "courses.json"),
            TotlangSchema.Serialize(catalog));
        if (schemaVersion == TotlangSchema.CurrentVersion)
        {
            var proficiencyCatalog = new TotlangProficiencyCatalog
            {
                SchemaVersion = TotlangSchema.CurrentVersion,
                Milestones =
                [
                    new ProficiencyMilestoneDefinition
                    {
                        Id = "fixture.pack:survival-exchange",
                        AnchorStage = "T03",
                        WithinStageOrder = 1,
                        DisplayNames =
                        [
                            new LocalizedText
                            {
                                LanguageTag = "en",
                                Value = "Survival exchange"
                            }
                        ],
                        CanDoDescriptors =
                        [
                            new LocalizedText
                            {
                                LanguageTag = "en",
                                Value = "Can complete a supported practical exchange."
                            }
                        ]
                    }
                ]
            };
            File.WriteAllText(
                Path.Combine(sourceDirectory, "proficiency.json"),
                TotlangSchema.Serialize(proficiencyCatalog));
        }

        File.WriteAllText(
            Path.Combine(lessonDirectory, "lesson-1.json"),
            TotlangSchema.Serialize(lesson));

        return sourceDirectory;
    }

    private static CourseProficiencyDefinition CreateV2CourseProficiency(
        IReadOnlyList<string> milestoneIds) =>
        new()
        {
            OverallEntryStage = "T02",
            OverallExitStage = "T05",
            Facets =
            [
                new FacetProficiencyRange
                {
                    Facet = ProficiencyFacet.Listening,
                    EntryStage = "T01",
                    ExitStage = "T06"
                }
            ],
            MilestoneIds = milestoneIds,
            ExternalAlignments =
            [
                new ExternalProficiencyAlignment
                {
                    FrameworkId = "cefr",
                    LowerReferenceLevel = "A1",
                    UpperReferenceLevel = "A2",
                    Facets = [ProficiencyFacet.Listening],
                    Status = ExternalAlignmentStatus.Estimated,
                    AuthoritativeSourceUri = "https://example.org/authoritative-source"
                }
            ]
        };
}
