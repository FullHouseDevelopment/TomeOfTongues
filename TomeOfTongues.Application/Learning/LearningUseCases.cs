using TomeOfTongues.Core.Exercises;
using TomeOfTongues.Core.LearningSessions;
using TomeOfTongues.Core.Progress;

namespace TomeOfTongues.Application.Learning;

public sealed class StartOrResumeLessonUseCase
{
    private readonly ILearningUnitOfWork _unitOfWork;

    public StartOrResumeLessonUseCase(ILearningUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public Task<StartOrResumeLessonResult> ExecuteAsync(
        StartOrResumeLessonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _unitOfWork.ExecuteAsync(
            async (transaction, token) =>
            {
                var course = await transaction.Curriculum
                    .GetCourseAsync(command.CourseId, token)
                    .ConfigureAwait(false)
                    ?? throw new KeyNotFoundException(
                        $"Course '{command.CourseId}' was not found.");

                _ = course.GetLesson(command.LessonId);

                var progress = await transaction.Progress
                    .GetAsync(command.CourseId, token)
                    .ConfigureAwait(false)
                    ?? LearnerProgress.Enroll(course, command.OccurredAt);

                if (!progress.IsLessonUnlocked(command.LessonId))
                {
                    throw new InvalidOperationException(
                        $"Lesson '{command.LessonId}' is not unlocked.");
                }

                var session = await transaction.Sessions
                    .GetActiveAsync(command.CourseId, command.LessonId, token)
                    .ConfigureAwait(false);
                var wasResumed = session is not null;

                if (session is null)
                {
                    session = LearningSession.Start(
                        command.SessionId,
                        course,
                        command.LessonId,
                        new LearningSessionModeSnapshot(command.IsSilent),
                        command.OccurredAt);

                    if (session.CurrentStep is { } currentStep)
                    {
                        progress.RecordPosition(
                            command.LessonId,
                            currentStep.Id,
                            command.OccurredAt);
                    }

                    await transaction.Sessions
                        .SaveAsync(session, token)
                        .ConfigureAwait(false);
                    await transaction.Progress
                        .SaveAsync(progress, token)
                        .ConfigureAwait(false);
                }

                return new StartOrResumeLessonResult(
                    session.CreateSnapshot(),
                    progress.CreateSnapshot(),
                    wasResumed);
            },
            cancellationToken);
    }
}

public sealed class SubmitLearningStepUseCase
{
    private readonly ILearningUnitOfWork _unitOfWork;

    public SubmitLearningStepUseCase(ILearningUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public Task<SubmitLearningStepResult> ExecuteAsync(
        SubmitLearningStepCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _unitOfWork.ExecuteAsync(
            async (transaction, token) =>
            {
                var session = await transaction.Sessions
                    .GetAsync(command.SessionId, token)
                    .ConfigureAwait(false)
                    ?? throw new KeyNotFoundException(
                        $"Learning session '{command.SessionId}' was not found.");
                var currentStep = session.CurrentStep
                    ?? throw new InvalidOperationException(
                        "The learning session is already complete.");
                var exercise = await transaction.Curriculum
                    .GetExerciseAsync(
                        session.CourseId,
                        session.LessonId,
                        currentStep.Id,
                        token)
                    .ConfigureAwait(false)
                    ?? throw new KeyNotFoundException(
                        $"Exercise for step '{currentStep.Id}' was not found.");
                var progress = await transaction.Progress
                    .GetAsync(session.CourseId, token)
                    .ConfigureAwait(false)
                    ?? throw new InvalidOperationException(
                        $"Progress for course '{session.CourseId}' was not found.");

                var attempt = ExerciseAttempt.Record(
                    command.AttemptId,
                    session.Id,
                    exercise,
                    command.PackRevision,
                    session.Mode.IsSilent,
                    command.Response,
                    command.Assistance,
                    command.Latency,
                    command.Confidence,
                    command.Outcome,
                    command.IsCorrect,
                    command.Measurement,
                    command.StartedAt,
                    command.CompletedAt);

                var skillStates = await RecordEvidenceAsync(
                        transaction.SkillStates,
                        attempt,
                        token)
                    .ConfigureAwait(false);

                AdvanceSession(session, command.Outcome, command.CompletedAt);
                if (session.Status == LearningSessionStatus.Completed)
                {
                    progress.CompleteLesson(session.LessonId, command.CompletedAt);
                }
                else
                {
                    progress.RecordPosition(
                        session.LessonId,
                        session.CurrentStep!.Id,
                        command.CompletedAt);
                }

                await transaction.Attempts
                    .AddAsync(attempt, token)
                    .ConfigureAwait(false);
                foreach (var skillState in skillStates)
                {
                    await transaction.SkillStates
                        .SaveAsync(skillState, token)
                        .ConfigureAwait(false);
                }

                await transaction.Sessions
                    .SaveAsync(session, token)
                    .ConfigureAwait(false);
                await transaction.Progress
                    .SaveAsync(progress, token)
                    .ConfigureAwait(false);

                return new SubmitLearningStepResult(
                    attempt,
                    session.CreateSnapshot(),
                    progress.CreateSnapshot(),
                    skillStates.Select(state => state.CreateSnapshot()).ToArray());
            },
            cancellationToken);
    }

    private static async Task<IReadOnlyList<LearnerSkillState>> RecordEvidenceAsync(
        ILearnerSkillStateRepository repository,
        ExerciseAttempt attempt,
        CancellationToken cancellationToken)
    {
        var states = new List<LearnerSkillState>();

        foreach (var observations in attempt.Evidence.GroupBy(
                     observation => new SkillStateKey(
                         observation.ObjectiveId,
                         observation.SkillDimension,
                         observation.RepresentationId)))
        {
            var state = await repository
                .GetAsync(
                    observations.Key.ObjectiveId,
                    observations.Key.SkillDimension,
                    observations.Key.RepresentationId,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var observation in observations)
            {
                if (state is null)
                {
                    state = LearnerSkillState.Create(observation);
                }
                else
                {
                    state.RecordEvidence(observation);
                }
            }

            states.Add(state);
        }

        return states;
    }

    private static void AdvanceSession(
        LearningSession session,
        AttemptOutcome outcome,
        DateTimeOffset occurredAt)
    {
        switch (outcome)
        {
            case AttemptOutcome.Evaluated:
                session.CompleteCurrentStep(occurredAt);
                break;
            case AttemptOutcome.Skipped:
                session.SkipCurrentStep(occurredAt);
                break;
            case AttemptOutcome.Deferred:
                session.DeferCurrentStep(occurredAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome));
        }
    }

    private sealed record SkillStateKey(
        TomeOfTongues.Core.Curriculum.ObjectiveId ObjectiveId,
        SkillDimension SkillDimension,
        RepresentationId? RepresentationId);
}
