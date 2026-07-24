using NUnit.Framework;
using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.Exercises;
using TomeOfTongues.Core.LearningSessions;

namespace TomeOfTongues.Core.Tests;

[TestFixture]
public sealed class ExerciseAttemptTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Evaluated_attempt_preserves_conditions_and_emits_each_evidence_dimension()
    {
        var mappings = new List<EvidenceMapping>
        {
            new(
                new ObjectiveId("understand-greeting"),
                SkillDimension.ListeningComprehension),
            new(
                new ObjectiveId("read-greeting"),
                SkillDimension.ReadingRecognition,
                new RepresentationId("ja-Jpan"))
        };
        var assistance = new List<AssistanceUsage>
        {
            new("transliteration", AssistanceKind.RevealedOnRequest)
        };
        var exercise = CreateExercise(
            PromptModality.Audio,
            ResponseModality.Selection,
            mappings);

        var attempt = Record(
            exercise,
            assistance,
            response: "choice-2",
            latency: new ResponseLatency(TimeSpan.FromSeconds(3)),
            confidence: new ConfidenceRating(4),
            isCorrect: true,
            measurement: EvidenceMeasurement.Measured);
        mappings.Clear();
        assistance.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(attempt.ExerciseRevision, Is.EqualTo(2));
            Assert.That(attempt.PackRevision, Is.EqualTo(7));
            Assert.That(attempt.PromptModality, Is.EqualTo(PromptModality.Audio));
            Assert.That(attempt.ResponseModality, Is.EqualTo(ResponseModality.Selection));
            Assert.That(attempt.Response, Is.EqualTo("choice-2"));
            Assert.That(attempt.Assistance, Has.Count.EqualTo(1));
            Assert.That(attempt.Latency!.Value.Value, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(attempt.Confidence!.Value.Value, Is.EqualTo(4));
            Assert.That(attempt.Evidence, Has.Count.EqualTo(2));
            Assert.That(
                attempt.Evidence.Select(item => item.Id.Value),
                Is.EqualTo(["attempt-1:1", "attempt-1:2"]));
            Assert.That(
                attempt.Evidence.Select(item => item.SkillDimension),
                Is.EqualTo(
                    [
                        SkillDimension.ListeningComprehension,
                        SkillDimension.ReadingRecognition
                    ]));
            Assert.That(
                attempt.Evidence[1].RepresentationId,
                Is.EqualTo(new RepresentationId("ja-Jpan")));
            Assert.That(attempt.Evidence.All(item => item.IsCorrect), Is.True);
            Assert.That(
                attempt.Evidence.All(item => item.Measurement == EvidenceMeasurement.Measured),
                Is.True);
        });
    }

    [Test]
    public void Self_reported_spoken_attempt_is_labelled_and_does_not_advance_session()
    {
        var session = CreateSession();
        var exercise = CreateExercise(
            PromptModality.Text,
            ResponseModality.Spoken,
            [
                new EvidenceMapping(
                    new ObjectiveId("say-greeting"),
                    SkillDimension.SpokenProduction)
            ]);

        var attempt = Record(
            exercise,
            [],
            response: null,
            latency: null,
            confidence: new ConfidenceRating(3),
            isCorrect: true,
            measurement: EvidenceMeasurement.SelfReported);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Evidence.Single().Measurement, Is.EqualTo(EvidenceMeasurement.SelfReported));
            Assert.That(attempt.Evidence.Single().SkillDimension, Is.EqualTo(SkillDimension.SpokenProduction));
            Assert.That(session.CurrentStep!.Id, Is.EqualTo(new StepId("speaking")));
            Assert.That(session.Status, Is.EqualTo(LearningSessionStatus.InProgress));
        });
    }

    [Test]
    public void Deferred_attempt_emits_no_competence_evidence()
    {
        var exercise = CreateExercise(
            PromptModality.Text,
            ResponseModality.Spoken,
            [
                new EvidenceMapping(
                    new ObjectiveId("say-greeting"),
                    SkillDimension.SpokenProduction)
            ]);

        var attempt = ExerciseAttempt.Record(
            new ExerciseAttemptId("attempt-deferred"),
            new LearningSessionId("session-1"),
            exercise,
            packRevision: 7,
            wasSilent: true,
            response: null,
            assistance: [],
            latency: null,
            confidence: null,
            AttemptOutcome.Deferred,
            isCorrect: null,
            measurement: null,
            StartedAt,
            StartedAt.AddSeconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Outcome, Is.EqualTo(AttemptOutcome.Deferred));
            Assert.That(attempt.WasSilent, Is.True);
            Assert.That(attempt.Evidence, Is.Empty);
        });
    }

    [Test]
    public void Definition_rejects_duplicate_multidimensional_mappings()
    {
        var mapping = new EvidenceMapping(
            new ObjectiveId("objective"),
            SkillDimension.Recognition,
            new RepresentationId("representation"));

        Assert.That(
            () => CreateExercise(
                PromptModality.Text,
                ResponseModality.Selection,
                [mapping, mapping]),
            Throws.ArgumentException.With.Message.Contains("duplicate"));
    }

    [Test]
    public void Attempt_rejects_inconsistent_outcome_and_time_boundaries()
    {
        var exercise = CreateExercise(
            PromptModality.Text,
            ResponseModality.Selection,
            []);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => Record(
                    exercise,
                    [],
                    response: "choice",
                    latency: null,
                    confidence: null,
                    isCorrect: null,
                    measurement: EvidenceMeasurement.Measured),
                Throws.ArgumentException.With.Message.Contains("require correctness"));
            Assert.That(
                () => ExerciseAttempt.Record(
                    new ExerciseAttemptId("attempt"),
                    new LearningSessionId("session"),
                    exercise,
                    packRevision: 1,
                    wasSilent: false,
                    response: null,
                    assistance: [],
                    latency: new ResponseLatency(TimeSpan.FromSeconds(5)),
                    confidence: null,
                    AttemptOutcome.Deferred,
                    isCorrect: null,
                    measurement: null,
                    StartedAt,
                    StartedAt.AddSeconds(1)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => ExerciseAttempt.Record(
                    new ExerciseAttemptId("attempt"),
                    new LearningSessionId("session"),
                    exercise,
                    packRevision: 1,
                    wasSilent: false,
                    response: null,
                    assistance: [],
                    latency: null,
                    confidence: null,
                    AttemptOutcome.Deferred,
                    isCorrect: null,
                    measurement: null,
                    StartedAt,
                    StartedAt.AddSeconds(-1)),
                Throws.ArgumentException.With.Message.Contains("cannot precede"));
        });
    }

    [Test]
    public void Recreating_an_attempt_from_persisted_inputs_is_deterministic()
    {
        var exercise = CreateExercise(
            PromptModality.Image,
            ResponseModality.Typed,
            [
                new EvidenceMapping(
                    new ObjectiveId("recall-expression"),
                    SkillDimension.Recall,
                    new RepresentationId("latin"))
            ]);

        var first = Record(
            exercise,
            [new AssistanceUsage("hint", AssistanceKind.ShownAutomatically)],
            response: "answer",
            latency: new ResponseLatency(TimeSpan.FromSeconds(2)),
            confidence: new ConfidenceRating(5),
            isCorrect: false,
            measurement: EvidenceMeasurement.Measured);
        var restored = Record(
            exercise,
            first.Assistance,
            first.Response,
            first.Latency,
            first.Confidence,
            first.IsCorrect,
            first.Measurement);

        Assert.Multiple(() =>
        {
            Assert.That(restored.Id, Is.EqualTo(first.Id));
            Assert.That(restored.CompletedAt, Is.EqualTo(first.CompletedAt));
            Assert.That(restored.Evidence.Single().Id, Is.EqualTo(first.Evidence.Single().Id));
            Assert.That(restored.Evidence.Single().ObjectiveId, Is.EqualTo(first.Evidence.Single().ObjectiveId));
            Assert.That(restored.Evidence.Single().RepresentationId, Is.EqualTo(first.Evidence.Single().RepresentationId));
            Assert.That(restored.Evidence.Single().IsCorrect, Is.EqualTo(first.Evidence.Single().IsCorrect));
        });
    }

    private static ExerciseDefinition CreateExercise(
        PromptModality promptModality,
        ResponseModality responseModality,
        IEnumerable<EvidenceMapping> mappings) =>
        new(
            new ExerciseId("exercise-1"),
            revision: 2,
            promptModality,
            responseModality,
            mappings);

    private static ExerciseAttempt Record(
        ExerciseDefinition exercise,
        IEnumerable<AssistanceUsage> assistance,
        string? response,
        ResponseLatency? latency,
        ConfidenceRating? confidence,
        bool? isCorrect,
        EvidenceMeasurement? measurement) =>
        ExerciseAttempt.Record(
            new ExerciseAttemptId("attempt-1"),
            new LearningSessionId("session-1"),
            exercise,
            packRevision: 7,
            wasSilent: false,
            response,
            assistance,
            latency,
            confidence,
            AttemptOutcome.Evaluated,
            isCorrect,
            measurement,
            StartedAt,
            StartedAt.AddSeconds(10));

    private static LearningSession CreateSession()
    {
        var lesson = new CurriculumLesson(
            new LessonId("lesson"),
            1,
            [new CurriculumObjective(new ObjectiveId("objective"), 1)],
            [
                new CurriculumStep(
                    new StepId("speaking"),
                    1,
                    CurriculumStepKind.OptionalSpeakingOpportunity,
                    CurriculumStepProgression.DeferredAllowed)
            ]);
        var course = new CurriculumCourse(
            new CourseId("course"),
            1,
            "A1",
            [new CurriculumUnit(new UnitId("unit"), 1, [lesson])]);

        return LearningSession.Start(
            new LearningSessionId("session"),
            course,
            lesson.Id,
            new LearningSessionModeSnapshot(IsSilent: false),
            StartedAt);
    }
}
