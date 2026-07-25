using NUnit.Framework;
using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.Exercises;
using TomeOfTongues.Core.LearningSessions;
using TomeOfTongues.Core.Progress;

namespace TomeOfTongues.Core.Tests;

[TestFixture]
public sealed class LearnerProgressTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 25, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public void Enrollment_and_completion_unlock_authored_curriculum()
    {
        var course = CreateCourse();
        var progress = LearnerProgress.Enroll(course, StartedAt);

        Assert.Multiple(() =>
        {
            Assert.That(
                progress.UnlockedLessonIds,
                Is.EqualTo([new LessonId("lesson-1")]));
            Assert.That(progress.CompletedLessonIds, Is.Empty);
            Assert.That(progress.IsLessonUnlocked(new LessonId("lesson-2")), Is.False);
        });

        progress.RecordPosition(
            new LessonId("lesson-1"),
            new StepId("required"),
            StartedAt.AddMinutes(1));
        progress.CompleteLesson(
            new LessonId("lesson-1"),
            StartedAt.AddMinutes(2));

        Assert.Multiple(() =>
        {
            Assert.That(
                progress.CompletedLessonIds,
                Is.EqualTo([new LessonId("lesson-1")]));
            Assert.That(
                progress.UnlockedLessonIds,
                Is.EqualTo(
                    [
                        new LessonId("lesson-1"),
                        new LessonId("lesson-2")
                    ]));
            Assert.That(progress.LastLessonId, Is.EqualTo(new LessonId("lesson-1")));
            Assert.That(progress.LastStepId, Is.EqualTo(new StepId("required")));
        });
    }

    [Test]
    public void Progress_snapshot_restores_exact_course_state()
    {
        var course = CreateCourse();
        var original = LearnerProgress.Enroll(course, StartedAt);
        original.RecordPosition(
            new LessonId("lesson-1"),
            new StepId("speaking"),
            StartedAt.AddMinutes(1));
        original.CompleteLesson(
            new LessonId("lesson-1"),
            StartedAt.AddMinutes(2));

        var restored = LearnerProgress.Restore(course, original.CreateSnapshot());

        Assert.Multiple(() =>
        {
            Assert.That(restored.CourseId, Is.EqualTo(original.CourseId));
            Assert.That(restored.CourseRevision, Is.EqualTo(original.CourseRevision));
            Assert.That(restored.UnlockedLessonIds, Is.EqualTo(original.UnlockedLessonIds));
            Assert.That(restored.CompletedLessonIds, Is.EqualTo(original.CompletedLessonIds));
            Assert.That(restored.LastLessonId, Is.EqualTo(original.LastLessonId));
            Assert.That(restored.LastStepId, Is.EqualTo(original.LastStepId));
            Assert.That(restored.UpdatedAt, Is.EqualTo(original.UpdatedAt));
        });
    }

    [Test]
    public void Progress_rejects_locked_transitions_and_invalid_restart_state()
    {
        var course = CreateCourse();
        var progress = LearnerProgress.Enroll(course, StartedAt);
        var invalidSnapshot = new LearnerProgressSnapshot(
            course.Id,
            course.Revision,
            [new LessonId("lesson-1"), new LessonId("lesson-2")],
            [],
            lastLessonId: null,
            lastStepId: null,
            StartedAt,
            StartedAt);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => progress.CompleteLesson(
                    new LessonId("lesson-2"),
                    StartedAt.AddMinutes(1)),
                Throws.InvalidOperationException.With.Message.Contains("not unlocked"));
            Assert.That(
                () => LearnerProgress.Restore(course, invalidSnapshot),
                Throws.ArgumentException.With.Message.Contains("do not match"));
            Assert.That(
                () => progress.RecordPosition(
                    new LessonId("lesson-1"),
                    new StepId("unknown"),
                    StartedAt.AddMinutes(1)),
                Throws.ArgumentException.With.Message.Contains("does not belong"));
        });
    }

    [Test]
    public void Skill_state_records_matching_evidence_and_round_trips()
    {
        var first = CreateObservation(
            "attempt-1",
            new ObjectiveId("read-greeting"),
            SkillDimension.ReadingRecognition,
            new RepresentationId("ja-Jpan"),
            isCorrect: false,
            EvidenceMeasurement.Measured,
            StartedAt.AddMinutes(1));
        var second = CreateObservation(
            "attempt-2",
            new ObjectiveId("read-greeting"),
            SkillDimension.ReadingRecognition,
            new RepresentationId("ja-Jpan"),
            isCorrect: true,
            EvidenceMeasurement.Measured,
            StartedAt.AddMinutes(2));
        var state = LearnerSkillState.Create(first);

        state.RecordEvidence(second);
        var restored = LearnerSkillState.Restore(state.CreateSnapshot());

        Assert.Multiple(() =>
        {
            Assert.That(restored.ObjectiveId, Is.EqualTo(first.ObjectiveId));
            Assert.That(restored.SkillDimension, Is.EqualTo(first.SkillDimension));
            Assert.That(restored.RepresentationId, Is.EqualTo(first.RepresentationId));
            Assert.That(restored.Observations.Select(item => item.Id), Is.EqualTo([first.Id, second.Id]));
            Assert.That(restored.UpdatedAt, Is.EqualTo(second.ObservedAt));
        });
    }

    [Test]
    public void Skill_state_rejects_mismatched_duplicate_and_out_of_order_evidence()
    {
        var first = CreateObservation(
            "attempt-1",
            new ObjectiveId("greeting"),
            SkillDimension.Recognition,
            representationId: null,
            isCorrect: true,
            EvidenceMeasurement.Measured,
            StartedAt.AddMinutes(2));
        var state = LearnerSkillState.Create(first);
        var mismatched = CreateObservation(
            "attempt-2",
            new ObjectiveId("greeting"),
            SkillDimension.Recall,
            representationId: null,
            isCorrect: true,
            EvidenceMeasurement.Measured,
            StartedAt.AddMinutes(3));
        var older = CreateObservation(
            "attempt-3",
            new ObjectiveId("greeting"),
            SkillDimension.Recognition,
            representationId: null,
            isCorrect: false,
            EvidenceMeasurement.Measured,
            StartedAt.AddMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(
                () => state.RecordEvidence(mismatched),
                Throws.ArgumentException.With.Message.Contains("must match"));
            Assert.That(
                () => state.RecordEvidence(first),
                Throws.ArgumentException.With.Message.Contains("already recorded"));
            Assert.That(
                () => state.RecordEvidence(older),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void Speaking_and_script_evidence_never_gate_curriculum_progress()
    {
        var course = CreateCourse();
        var progress = LearnerProgress.Enroll(course, StartedAt);
        var scriptFailure = CreateObservation(
            "attempt-script",
            new ObjectiveId("read-greeting"),
            SkillDimension.ReadingRecognition,
            new RepresentationId("ja-Jpan"),
            isCorrect: false,
            EvidenceMeasurement.Measured,
            StartedAt.AddMinutes(1));
        var spokenReport = CreateObservation(
            "attempt-spoken",
            new ObjectiveId("say-greeting"),
            SkillDimension.SpokenProduction,
            representationId: null,
            isCorrect: false,
            EvidenceMeasurement.SelfReported,
            StartedAt.AddMinutes(2),
            ResponseModality.Spoken);

        var scriptState = LearnerSkillState.Create(scriptFailure);
        var spokenState = LearnerSkillState.Create(spokenReport);
        progress.CompleteLesson(
            new LessonId("lesson-1"),
            StartedAt.AddMinutes(3));

        Assert.Multiple(() =>
        {
            Assert.That(scriptState.Observations.Single().IsCorrect, Is.False);
            Assert.That(spokenState.Observations.Single().IsCorrect, Is.False);
            Assert.That(
                spokenState.Observations.Single().Measurement,
                Is.EqualTo(EvidenceMeasurement.SelfReported));
            Assert.That(progress.IsLessonUnlocked(new LessonId("lesson-2")), Is.True);
        });
    }

    private static EvidenceObservation CreateObservation(
        string attemptId,
        ObjectiveId objectiveId,
        SkillDimension skillDimension,
        RepresentationId? representationId,
        bool isCorrect,
        EvidenceMeasurement measurement,
        DateTimeOffset observedAt,
        ResponseModality responseModality = ResponseModality.Selection)
    {
        var exercise = new ExerciseDefinition(
            new ExerciseId($"exercise-{attemptId}"),
            revision: 1,
            PromptModality.Text,
            responseModality,
            [new EvidenceMapping(objectiveId, skillDimension, representationId)]);
        var attempt = ExerciseAttempt.Record(
            new ExerciseAttemptId(attemptId),
            new LearningSessionId("session"),
            exercise,
            packRevision: 1,
            wasSilent: false,
            response: null,
            assistance: [],
            latency: null,
            confidence: null,
            AttemptOutcome.Evaluated,
            isCorrect,
            measurement,
            observedAt.AddSeconds(-1),
            observedAt);

        return attempt.Evidence.Single();
    }

    private static CurriculumCourse CreateCourse()
    {
        var firstLesson = new CurriculumLesson(
            new LessonId("lesson-1"),
            revision: 3,
            [
                new CurriculumObjective(new ObjectiveId("read-greeting"), 1),
                new CurriculumObjective(new ObjectiveId("say-greeting"), 1)
            ],
            [
                new CurriculumStep(
                    new StepId("required"),
                    1,
                    CurriculumStepKind.Exercise,
                    CurriculumStepProgression.Required),
                new CurriculumStep(
                    new StepId("speaking"),
                    1,
                    CurriculumStepKind.OptionalSpeakingOpportunity,
                    CurriculumStepProgression.DeferredAllowed)
            ]);
        var secondLesson = new CurriculumLesson(
            new LessonId("lesson-2"),
            revision: 1,
            [new CurriculumObjective(new ObjectiveId("next-objective"), 1)],
            [
                new CurriculumStep(
                    new StepId("next-step"),
                    1,
                    CurriculumStepKind.Exercise,
                    CurriculumStepProgression.Required)
            ],
            [firstLesson.Id]);

        return new CurriculumCourse(
            new CourseId("course"),
            revision: 5,
            "A1",
            [new CurriculumUnit(new UnitId("unit"), 1, [firstLesson, secondLesson])]);
    }
}
