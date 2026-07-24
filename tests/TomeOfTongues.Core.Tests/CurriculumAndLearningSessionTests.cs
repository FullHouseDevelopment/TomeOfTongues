using NUnit.Framework;
using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.LearningSessions;

namespace TomeOfTongues.Core.Tests;

[TestFixture]
public sealed class CurriculumAndLearningSessionTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void Curriculum_preserves_authored_order_and_resolves_lessons()
    {
        var course = CreateCourse();

        var lesson = course.GetLesson(new LessonId("lesson-2"));

        Assert.Multiple(() =>
        {
            Assert.That(course.Units.Select(unit => unit.Id.Value), Is.EqualTo(["unit-1"]));
            Assert.That(
                course.Units.Single().Lessons.Select(item => item.Id.Value),
                Is.EqualTo(["lesson-1", "lesson-2"]));
            Assert.That(
                lesson.PrerequisiteLessonIds,
                Is.EqualTo([new LessonId("lesson-1")]));
            Assert.That(
                lesson.Steps.Select(step => step.Id.Value),
                Is.EqualTo(["required", "optional", "speaking"]));
        });
    }

    [Test]
    public void Curriculum_rejects_duplicate_steps_and_prerequisite_cycles()
    {
        var duplicateStep = CreateStep("same", CurriculumStepProgression.Required);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => CreateLesson("duplicates", [], [duplicateStep, duplicateStep]),
                Throws.ArgumentException.With.Message.Contains("duplicate"));

            Assert.That(
                () => new CurriculumCourse(
                    new CourseId("course"),
                    1,
                    "A1",
                    [
                        new CurriculumUnit(
                            new UnitId("unit"),
                            1,
                            [
                                CreateLesson("first", [new LessonId("second")]),
                                CreateLesson("second", [new LessonId("first")])
                            ])
                    ]),
                Throws.ArgumentException.With.Message.Contains("cycle"));
        });
    }

    [Test]
    public void Speaking_opportunities_must_be_deferrable()
    {
        Assert.That(
            () => new CurriculumStep(
                new StepId("speaking"),
                1,
                CurriculumStepKind.OptionalSpeakingOpportunity,
                CurriculumStepProgression.Required),
            Throws.ArgumentException.With.Message.Contains("allow deferral"));
    }

    [Test]
    public void Session_traverses_required_optional_and_deferred_steps()
    {
        var session = StartSession(CreateCourse());

        session.CompleteCurrentStep(StartedAt.AddMinutes(1));
        session.SkipCurrentStep(StartedAt.AddMinutes(2));
        session.DeferCurrentStep(StartedAt.AddMinutes(3));

        Assert.Multiple(() =>
        {
            Assert.That(session.Status, Is.EqualTo(LearningSessionStatus.Completed));
            Assert.That(session.CurrentStep, Is.Null);
            Assert.That(session.CompletedStepIds, Is.EqualTo([new StepId("required")]));
            Assert.That(session.SkippedStepIds, Is.EqualTo([new StepId("optional")]));
            Assert.That(session.DeferredStepIds, Is.EqualTo([new StepId("speaking")]));
            Assert.That(
                () => session.CompleteCurrentStep(StartedAt.AddMinutes(4)),
                Throws.InvalidOperationException);
        });
    }

    [Test]
    public void Session_restores_from_an_immutable_snapshot()
    {
        var course = CreateCourse();
        var session = StartSession(course);
        session.CompleteCurrentStep(StartedAt.AddMinutes(1));
        var snapshot = session.CreateSnapshot();

        var restored = LearningSession.Restore(course, snapshot);
        restored.SkipCurrentStep(StartedAt.AddMinutes(2));

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CurrentStepId, Is.EqualTo(new StepId("optional")));
            Assert.That(restored.CurrentStep!.Id, Is.EqualTo(new StepId("speaking")));
            Assert.That(restored.Mode.IsSilent, Is.True);
            Assert.That(restored.CompletedStepIds, Is.EqualTo([new StepId("required")]));
            Assert.That(restored.SkippedStepIds, Is.EqualTo([new StepId("optional")]));
        });
    }

    [Test]
    public void Restore_rejects_a_different_lesson_revision()
    {
        var session = StartSession(CreateCourse());
        var snapshot = session.CreateSnapshot();
        var revisedCourse = CreateCourse(lessonRevision: 2);

        Assert.That(
            () => LearningSession.Restore(revisedCourse, snapshot),
            Throws.ArgumentException.With.Message.Contains("different lesson revision"));
    }

    [Test]
    public void Restore_rejects_noncontiguous_or_overlapping_step_state()
    {
        var course = CreateCourse();
        var session = StartSession(course);
        var valid = session.CreateSnapshot();

        var noncontiguous = CopySnapshot(
            valid,
            currentStepId: new StepId("required"),
            completedStepIds: [new StepId("optional")]);
        var overlapping = CopySnapshot(
            valid,
            currentStepId: new StepId("optional"),
            completedStepIds: [new StepId("required")],
            skippedStepIds: [new StepId("required")]);

        Assert.Multiple(() =>
        {
            Assert.That(
                () => LearningSession.Restore(course, noncontiguous),
                Throws.ArgumentException.With.Message.Contains("contiguous"));
            Assert.That(
                () => LearningSession.Restore(course, overlapping),
                Throws.ArgumentException.With.Message.Contains("more than one"));
        });
    }

    [Test]
    public void Session_enforces_skip_defer_and_time_boundaries()
    {
        var session = StartSession(CreateCourse());

        Assert.Multiple(() =>
        {
            Assert.That(
                () => session.SkipCurrentStep(StartedAt.AddMinutes(1)),
                Throws.InvalidOperationException);
            Assert.That(
                () => session.DeferCurrentStep(StartedAt.AddMinutes(1)),
                Throws.InvalidOperationException);
            Assert.That(
                () => session.CompleteCurrentStep(StartedAt.AddSeconds(-1)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    private static LearningSession StartSession(CurriculumCourse course) =>
        LearningSession.Start(
            new LearningSessionId("session-1"),
            course,
            new LessonId("lesson-2"),
            new LearningSessionModeSnapshot(IsSilent: true),
            StartedAt);

    private static CurriculumCourse CreateCourse(int lessonRevision = 1)
    {
        var firstLesson = CreateLesson("lesson-1");
        var secondLesson = CreateLesson(
            "lesson-2",
            [firstLesson.Id],
            [
                CreateStep("required", CurriculumStepProgression.Required),
                CreateStep("optional", CurriculumStepProgression.Optional),
                CreateStep(
                    "speaking",
                    CurriculumStepProgression.DeferredAllowed,
                    CurriculumStepKind.OptionalSpeakingOpportunity)
            ],
            lessonRevision);

        return new CurriculumCourse(
            new CourseId("course-1"),
            1,
            "A1",
            [new CurriculumUnit(new UnitId("unit-1"), 1, [firstLesson, secondLesson])]);
    }

    private static CurriculumLesson CreateLesson(
        string id,
        IEnumerable<LessonId>? prerequisites = null,
        IEnumerable<CurriculumStep>? steps = null,
        int revision = 1) =>
        new(
            new LessonId(id),
            revision,
            [new CurriculumObjective(new ObjectiveId($"{id}-objective"), 1)],
            steps ?? [CreateStep($"{id}-step", CurriculumStepProgression.Required)],
            prerequisites);

    private static CurriculumStep CreateStep(
        string id,
        CurriculumStepProgression progression,
        CurriculumStepKind kind = CurriculumStepKind.Exercise) =>
        new(new StepId(id), 1, kind, progression);

    private static LearningSessionSnapshot CopySnapshot(
        LearningSessionSnapshot source,
        StepId? currentStepId,
        IEnumerable<StepId> completedStepIds,
        IEnumerable<StepId>? skippedStepIds = null) =>
        new(
            source.SessionId,
            source.CourseId,
            source.LessonId,
            source.LessonRevision,
            currentStepId,
            completedStepIds,
            skippedStepIds ?? [],
            source.DeferredStepIds,
            source.Mode,
            source.Status,
            source.StartedAt,
            source.UpdatedAt);
}
