using NUnit.Framework;
using TomeOfTongues.Content.Schema;

namespace TomeOfTongues.Content.Tests;

[TestFixture]
public sealed class TotlangSchemaTests
{
    [Test]
    public void Manifest_round_trips_deterministically()
    {
        var manifest = CreateManifest();

        var firstJson = TotlangSchema.Serialize(manifest);
        var restored = TotlangSchema.Deserialize<TotlangManifest>(firstJson);
        var secondJson = TotlangSchema.Serialize(restored);

        Assert.Multiple(() =>
        {
            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(restored.SchemaVersion, Is.EqualTo(TotlangSchema.CurrentVersion));
            Assert.That(restored.Representations[0].Direction, Is.EqualTo(TextDirection.RightToLeft));
            Assert.That(restored.NormalizationOperations, Does.Contain(NormalizationOperation.UnicodeNfc));
            Assert.That(firstJson, Does.Contain("\"rightToLeft\""));
            Assert.That(firstJson, Does.Not.Contain("TomeOfTongues." + "Language."));
        });
    }

    [Test]
    public void Lesson_preserves_ranges_and_non_gating_speaking()
    {
        var lesson = CreateLesson(StepProgression.DeferredAllowed);

        var json = TotlangSchema.Serialize(lesson);
        var restored = TotlangSchema.Deserialize<TotlangLesson>(json);
        var representation = restored.Expressions.Single().Representations.Single();
        var speakingStep = restored.Steps.Single();

        Assert.Multiple(() =>
        {
            Assert.That(representation.Value, Is.EqualTo("שלום"));
            Assert.That(representation.Annotations.Single().Start, Is.Zero);
            Assert.That(representation.Annotations.Single().Length, Is.EqualTo(2));
            Assert.That(speakingStep.Progression, Is.EqualTo(StepProgression.DeferredAllowed));
            Assert.That(speakingStep.Exercise!.ResponseModality, Is.EqualTo(ResponseModality.Spoken));
            Assert.That(speakingStep.Exercise.Scoring.Mode, Is.EqualTo(ScoringMode.SelfReported));
        });
    }

    [TestCase("""{"schemaVersion":3,"packId":"fixture"}""")]
    [TestCase("""{"packId":"fixture"}""")]
    [TestCase("""{"schemaVersion":"1","packId":"fixture"}""")]
    [TestCase("""not json""")]
    public void Invalid_or_unsupported_schema_documents_are_rejected(string json)
    {
        Assert.That(
            () => TotlangSchema.Deserialize<TotlangManifest>(json),
            Throws.TypeOf<TotlangSchemaException>());
    }

    [Test]
    public void Unknown_contract_members_are_rejected()
    {
        var json = TotlangSchema.Serialize(CreateManifest())
            .Replace(
                "\"packId\": \"fixture.pack\"",
                "\"packId\": \"fixture.pack\", \"runtimeAssembly\": \"Fixture.dll\"",
                StringComparison.Ordinal);

        Assert.That(
            () => TotlangSchema.Deserialize<TotlangManifest>(json),
            Throws.TypeOf<TotlangSchemaException>());
    }

    [TestCase("packageVersion", "not-a-version", "package version")]
    [TestCase("minimumEngineVersion", "preview", "minimum engine version")]
    public void Manifest_rejects_invalid_versions(
        string propertyName,
        string value,
        string expectedDescription)
    {
        var json = TotlangSchema.Serialize(CreateManifest())
            .Replace(
                $"\"{propertyName}\": \"1.0.0\"",
                $"\"{propertyName}\": \"{value}\"",
                StringComparison.Ordinal);

        Assert.That(
            () => TotlangSchema.Deserialize<TotlangManifest>(json),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains(expectedDescription));
    }

    [Test]
    public void Gating_spoken_work_is_rejected()
    {
        var lesson = CreateLesson(StepProgression.Required);

        Assert.That(
            () => TotlangSchema.Serialize(lesson),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains("must allow deferral"));
    }

    [Test]
    public void Out_of_range_annotations_are_rejected()
    {
        var validLesson = CreateLesson(StepProgression.DeferredAllowed);
        var representation = validLesson.Expressions.Single().Representations.Single();
        var invalidRepresentation = representation with
        {
            Annotations =
            [
                representation.Annotations.Single() with
                {
                    Start = representation.Value.Length,
                    Length = 1
                }
            ]
        };
        var invalidLesson = validLesson with
        {
            Expressions =
            [
                validLesson.Expressions.Single() with
                {
                    Representations = [invalidRepresentation]
                }
            ]
        };

        Assert.That(
            () => TotlangSchema.Serialize(invalidLesson),
            Throws.TypeOf<TotlangSchemaException>()
                .With.Message.Contains("outside representation"));
    }

    private static TotlangManifest CreateManifest() =>
        new()
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
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
                },
                new RepresentationDefinition
                {
                    Id = "latn",
                    LanguageTag = "he",
                    ScriptTag = "Latn",
                    Direction = TextDirection.LeftToRight,
                    TransliterationScheme = "authored-fixture",
                    AssistanceGroupId = "pronunciation"
                }
            ],
            NormalizationOperations =
            [
                NormalizationOperation.UnicodeNfc,
                NormalizationOperation.Trim
            ],
            CourseIds = ["course-1"],
            Assets =
            [
                new AssetDefinition
                {
                    Id = "audio-1",
                    Path = "assets/audio/prompt.ogg",
                    MediaType = "audio/ogg",
                    Sha256 = new string('a', 64),
                    SourceId = "source-1"
                }
            ],
            Sources =
            [
                new SourceDefinition
                {
                    Id = "source-1",
                    Origin = "Original fixture",
                    Author = "TomeOfTongues contributors",
                    Reviewer = "Fixture reviewer",
                    LicenseId = "cc-by-sa-4.0",
                    Attribution = "Original fixture content",
                    RedistributionAllowed = true,
                    ModificationAllowed = true,
                    RecordedOn = new DateOnly(2026, 7, 24)
                }
            ],
            Licenses =
            [
                new LicenseDefinition
                {
                    Id = "cc-by-sa-4.0",
                    Name = "CC BY-SA 4.0",
                    Uri = "https://creativecommons.org/licenses/by-sa/4.0/"
                }
            ]
        };

    private static TotlangLesson CreateLesson(StepProgression speakingProgression) =>
        new()
        {
            SchemaVersion = TotlangSchema.CurrentVersion,
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
            Objectives =
            [
                new ObjectiveDefinition
                {
                    Id = "objective-1",
                    Revision = 1,
                    SkillDimension = SkillDimension.SpokenProduction,
                    RepresentationId = "hebr"
                }
            ],
            Expressions =
            [
                new ExpressionDefinition
                {
                    Id = "expression-1",
                    Revision = 1,
                    Representations =
                    [
                        new TextRepresentation
                        {
                            Id = "expression-1-hebr",
                            RepresentationId = "hebr",
                            Value = "שלום",
                            Annotations =
                            [
                                new RangeAnnotation
                                {
                                    Id = "annotation-1",
                                    Start = 0,
                                    Length = 2,
                                    Kind = AnnotationKind.Pronunciation,
                                    Value = "sha",
                                    RepresentationId = "latn"
                                }
                            ]
                        }
                    ],
                    Meanings =
                    [
                        new MeaningDefinition
                        {
                            Kind = MeaningKind.Natural,
                            LanguageTag = "en",
                            Value = "Hello"
                        }
                    ],
                    Audio =
                    [
                        new AudioReference
                        {
                            AssetId = "audio-1",
                            TranscriptRepresentationId = "expression-1-hebr"
                        }
                    ],
                    SourceIds = ["source-1"]
                }
            ],
            Steps =
            [
                new StepDefinition
                {
                    Id = "step-1",
                    Revision = 1,
                    Kind = StepKind.OptionalSpeakingOpportunity,
                    Progression = speakingProgression,
                    ExpressionIds = ["expression-1"],
                    Exercise = new ExerciseDefinition
                    {
                        Id = "exercise-1",
                        Revision = 1,
                        Type = ExerciseType.SelfAssessment,
                        PromptModality = PromptModality.Audio,
                        ResponseModality = ResponseModality.Spoken,
                        TargetIds = ["expression-1"],
                        AcceptableAnswers = [],
                        Assistance =
                        [
                            new AssistanceDefinition
                            {
                                GroupId = "pronunciation",
                                DefaultMode = AssistanceMode.TapToReveal,
                                RepresentationIds = ["latn"]
                            }
                        ],
                        Scoring = new ScoringDefinition
                        {
                            Mode = ScoringMode.SelfReported,
                            MaximumScore = 1
                        },
                        Evidence =
                        [
                            new EvidenceMapping
                            {
                                ObjectiveId = "objective-1",
                                SkillDimension = SkillDimension.SpokenProduction,
                                RepresentationId = "hebr"
                            }
                        ]
                    }
                }
            ]
        };
}
