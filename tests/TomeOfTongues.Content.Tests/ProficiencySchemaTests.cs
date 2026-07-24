using NUnit.Framework;
using TomeOfTongues.Content.Schema;

namespace TomeOfTongues.Content.Tests;

[TestFixture]
public sealed class ProficiencySchemaTests
{
    [Test]
    public void Versioned_framework_defines_the_fixed_stage_backbone()
    {
        var path = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "proficiency",
            "tomeoftongues-framework-v1.json");
        var json = File.ReadAllText(path);

        var framework = TotlangSchema.Deserialize<ProficiencyFrameworkDefinition>(json);
        var restored = TotlangSchema.Deserialize<ProficiencyFrameworkDefinition>(
            TotlangSchema.Serialize(framework));

        Assert.Multiple(() =>
        {
            Assert.That(restored.FrameworkVersion, Is.EqualTo(1));
            Assert.That(
                restored.Stages.Select(stage => stage.Id),
                Is.EqualTo(Enumerable.Range(1, 18).Select(number => $"T{number:00}")));
            Assert.That(
                restored.Stages.All(stage => stage.CanDoDescriptors.Count > 0),
                Is.True);
            Assert.That(
                restored.GlobalMilestones.Select(milestone => milestone.Id),
                Is.EquivalentTo(TomeOfTonguesProficiencyFramework.GlobalMilestoneIds));
        });
    }

    [Test]
    public void V1_course_preserves_opaque_band_without_inference()
    {
        var catalog = new TotlangCourseCatalog
        {
            SchemaVersion = TotlangSchema.LegacyVersion,
            Courses = [CreateCourse(proficiencyBand: "starter / not-a-mapping")]
        };

        var restored = TotlangSchema.Deserialize<TotlangCourseCatalog>(
            TotlangSchema.Serialize(catalog));
        var course = restored.Courses.Single();

        Assert.Multiple(() =>
        {
            Assert.That(course.ProficiencyBand, Is.EqualTo("starter / not-a-mapping"));
            Assert.That(course.Proficiency, Is.Null);
            Assert.That(TotlangSchema.Serialize(restored), Does.Not.Contain("\"proficiency\":"));
        });
    }

    [Test]
    public void Complete_v2_proficiency_documents_round_trip()
    {
        var languageMilestone = CreateMilestone("fixture.pack:survival-exchange");
        var proficiencyCatalog = new TotlangProficiencyCatalog
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
            Milestones = [languageMilestone]
        };
        var catalog = new TotlangCourseCatalog
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
            Courses =
            [
                CreateCourse(
                    proficiency: CreateProficiency(
                        milestoneIds:
                        [
                            "global:initial-repertoire",
                            languageMilestone.Id
                        ]))
            ]
        };

        var restoredMilestones = TotlangSchema.Deserialize<TotlangProficiencyCatalog>(
            TotlangSchema.Serialize(proficiencyCatalog));
        var restoredCatalog = TotlangSchema.Deserialize<TotlangCourseCatalog>(
            TotlangSchema.Serialize(catalog));
        var restored = restoredCatalog.Courses.Single().Proficiency!;

        Assert.Multiple(() =>
        {
            Assert.That(
                restoredMilestones.Milestones.Single().Id,
                Is.EqualTo(languageMilestone.Id));
            Assert.That(restored.OverallEntryStage, Is.EqualTo("T02"));
            Assert.That(restored.OverallExitStage, Is.EqualTo("T08"));
            Assert.That(restored.Facets.Single().Facet, Is.EqualTo(ProficiencyFacet.Reading));
            Assert.That(restored.MilestoneIds, Has.Count.EqualTo(2));
            Assert.That(
                restored.ExternalAlignments.Single().Status,
                Is.EqualTo(ExternalAlignmentStatus.Reviewed));
        });
    }

    [TestCase("T19", "T08", "unknown")]
    [TestCase("T08", "T02", "reversed")]
    public void Invalid_overall_stage_ranges_are_rejected(
        string entryStage,
        string exitStage,
        string expectedMessage)
    {
        var proficiency = CreateProficiency() with
        {
            OverallEntryStage = entryStage,
            OverallExitStage = exitStage
        };

        Assert.That(
            () => TotlangSchema.Serialize(CreateV2Catalog(proficiency)),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains(expectedMessage));
    }

    [Test]
    public void Reversed_facet_range_is_rejected()
    {
        var proficiency = CreateProficiency() with
        {
            Facets =
            [
                new FacetProficiencyRange
                {
                    Facet = ProficiencyFacet.Reading,
                    EntryStage = "T09",
                    ExitStage = "T03"
                }
            ]
        };

        Assert.That(
            () => TotlangSchema.Serialize(CreateV2Catalog(proficiency)),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains("reversed"));
    }

    [TestCase("unknown-framework", "A1", "A2", "unsupported")]
    [TestCase("cefr", "B2", "A2", "reversed")]
    public void Invalid_external_alignment_ranges_are_rejected(
        string frameworkId,
        string lowerLevel,
        string upperLevel,
        string expectedMessage)
    {
        var alignment = CreateAlignment() with
        {
            FrameworkId = frameworkId,
            LowerReferenceLevel = lowerLevel,
            UpperReferenceLevel = upperLevel
        };
        var proficiency = CreateProficiency() with { ExternalAlignments = [alignment] };

        Assert.That(
            () => TotlangSchema.Serialize(CreateV2Catalog(proficiency)),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains(expectedMessage));
    }

    [Test]
    public void Invalid_external_alignment_status_is_rejected()
    {
        var alignment = CreateAlignment() with { Status = (ExternalAlignmentStatus)99 };
        var proficiency = CreateProficiency() with { ExternalAlignments = [alignment] };

        Assert.That(
            () => TotlangSchema.Serialize(CreateV2Catalog(proficiency)),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains("status"));
    }

    [TestCase(ExternalAlignmentStatus.Reviewed)]
    [TestCase(ExternalAlignmentStatus.Validated)]
    public void Reviewed_external_alignments_require_a_review_date(
        ExternalAlignmentStatus status)
    {
        var alignment = CreateAlignment() with
        {
            Status = status,
            ReviewDate = null
        };
        var proficiency = CreateProficiency() with { ExternalAlignments = [alignment] };

        Assert.That(
            () => TotlangSchema.Serialize(CreateV2Catalog(proficiency)),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains("review date"));
    }

    [Test]
    public void External_alignments_require_facets_and_authoritative_uri()
    {
        var missingFacets = CreateAlignment() with { Facets = [] };
        var invalidUri = CreateAlignment() with { AuthoritativeSourceUri = "notes/local" };

        Assert.Multiple(() =>
        {
            Assert.That(
                () => TotlangSchema.Serialize(
                    CreateV2Catalog(
                        CreateProficiency() with { ExternalAlignments = [missingFacets] })),
                Throws.TypeOf<TotlangSchemaException>()
                    .With.Message.Contains("facets"));
            Assert.That(
                () => TotlangSchema.Serialize(
                    CreateV2Catalog(
                        CreateProficiency() with { ExternalAlignments = [invalidUri] })),
                Throws.TypeOf<TotlangSchemaException>()
                    .With.Message.Contains("HTTP(S)"));
        });
    }

    [Test]
    public void Duplicate_and_invalid_milestones_are_rejected()
    {
        var milestone = CreateMilestone("fixture.pack:survival-exchange");
        var duplicates = new TotlangProficiencyCatalog
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
            Milestones = [milestone, milestone]
        };
        var invalidAnchor = new TotlangProficiencyCatalog
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
            Milestones = [milestone with { AnchorStage = "T00" }]
        };
        var invalidNamespace = new TotlangProficiencyCatalog
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
            Milestones = [milestone with { Id = "not-namespaced" }]
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                () => TotlangSchema.Serialize(duplicates),
                Throws.TypeOf<TotlangSchemaException>()
                    .With.Message.Contains("duplicated"));
            Assert.That(
                () => TotlangSchema.Serialize(invalidAnchor),
                Throws.TypeOf<TotlangSchemaException>()
                    .With.Message.Contains("unknown anchor stage"));
            Assert.That(
                () => TotlangSchema.Serialize(invalidNamespace),
                Throws.TypeOf<TotlangSchemaException>()
                    .With.Message.Contains("namespaced"));
        });
    }

    [Test]
    public void Additional_milestones_do_not_change_stage_order()
    {
        var before = TomeOfTonguesProficiencyFramework.StageIds.ToArray();
        var catalog = new TotlangProficiencyCatalog
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
            Milestones =
            [
                CreateMilestone("fixture.pack:first"),
                CreateMilestone("fixture.pack:second") with { WithinStageOrder = 2 }
            ]
        };

        _ = TotlangSchema.Serialize(catalog);

        Assert.That(TomeOfTonguesProficiencyFramework.StageIds, Is.EqualTo(before));
    }

    private static TotlangCourseCatalog CreateV2Catalog(
        CourseProficiencyDefinition proficiency) =>
        new()
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
            Courses = [CreateCourse(proficiency: proficiency)]
        };

    private static CourseDefinition CreateCourse(
        string? proficiencyBand = null,
        CourseProficiencyDefinition? proficiency = null) =>
        new()
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
            ProficiencyBand = proficiencyBand,
            Proficiency = proficiency,
            Units = []
        };

    private static CourseProficiencyDefinition CreateProficiency(
        IReadOnlyList<string>? milestoneIds = null) =>
        new()
        {
            OverallEntryStage = "T02",
            OverallExitStage = "T08",
            Facets =
            [
                new FacetProficiencyRange
                {
                    Facet = ProficiencyFacet.Reading,
                    EntryStage = "T01",
                    ExitStage = "T09"
                }
            ],
            MilestoneIds = milestoneIds ?? ["global:initial-repertoire"],
            ExternalAlignments = [CreateAlignment()]
        };

    private static ExternalProficiencyAlignment CreateAlignment() =>
        new()
        {
            FrameworkId = "cefr",
            LowerReferenceLevel = "A1",
            UpperReferenceLevel = "B1",
            Facets = [ProficiencyFacet.Listening, ProficiencyFacet.Reading],
            Status = ExternalAlignmentStatus.Reviewed,
            AuthoritativeSourceUri = "https://example.org/authoritative-framework-review",
            ReviewDate = new DateOnly(2026, 7, 25)
        };

    private static ProficiencyMilestoneDefinition CreateMilestone(string id) =>
        new()
        {
            Id = id,
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
        };
}
