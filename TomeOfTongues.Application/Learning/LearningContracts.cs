using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.Exercises;
using TomeOfTongues.Core.LearningSessions;
using TomeOfTongues.Core.Progress;

namespace TomeOfTongues.Application.Learning;

public sealed record StartOrResumeLessonCommand(
    LearningSessionId SessionId,
    CourseId CourseId,
    LessonId LessonId,
    bool IsSilent,
    DateTimeOffset OccurredAt);

public sealed record StartOrResumeLessonResult(
    LearningSessionSnapshot Session,
    LearnerProgressSnapshot Progress,
    bool WasResumed);

public sealed record SubmitLearningStepCommand(
    LearningSessionId SessionId,
    ExerciseAttemptId AttemptId,
    int PackRevision,
    string? Response,
    IReadOnlyList<AssistanceUsage> Assistance,
    ResponseLatency? Latency,
    ConfidenceRating? Confidence,
    AttemptOutcome Outcome,
    bool? IsCorrect,
    EvidenceMeasurement? Measurement,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

public sealed record SubmitLearningStepResult(
    ExerciseAttempt Attempt,
    LearningSessionSnapshot Session,
    LearnerProgressSnapshot Progress,
    IReadOnlyList<LearnerSkillStateSnapshot> SkillStates);

public interface ICurriculumRepository
{
    Task<CurriculumCourse?> GetCourseAsync(
        CourseId courseId,
        CancellationToken cancellationToken);

    Task<ExerciseDefinition?> GetExerciseAsync(
        CourseId courseId,
        LessonId lessonId,
        StepId stepId,
        CancellationToken cancellationToken);
}

public interface ILearningSessionRepository
{
    Task<LearningSession?> GetActiveAsync(
        CourseId courseId,
        LessonId lessonId,
        CancellationToken cancellationToken);

    Task<LearningSession?> GetAsync(
        LearningSessionId sessionId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        LearningSession session,
        CancellationToken cancellationToken);
}

public interface ILearnerProgressRepository
{
    Task<LearnerProgress?> GetAsync(
        CourseId courseId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        LearnerProgress progress,
        CancellationToken cancellationToken);
}

public interface IExerciseAttemptRepository
{
    Task AddAsync(
        ExerciseAttempt attempt,
        CancellationToken cancellationToken);
}

public interface ILearnerSkillStateRepository
{
    Task<LearnerSkillState?> GetAsync(
        ObjectiveId objectiveId,
        SkillDimension skillDimension,
        RepresentationId? representationId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        LearnerSkillState state,
        CancellationToken cancellationToken);
}

public interface ILearningTransaction
{
    ICurriculumRepository Curriculum { get; }

    ILearningSessionRepository Sessions { get; }

    ILearnerProgressRepository Progress { get; }

    IExerciseAttemptRepository Attempts { get; }

    ILearnerSkillStateRepository SkillStates { get; }
}

public interface ILearningUnitOfWork
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<ILearningTransaction, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
