using TomeOfTongues.Core.Curriculum;

namespace TomeOfTongues.Core.LearningSessions;

public readonly record struct LearningSessionId
{
    public LearningSessionId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record LearningSessionModeSnapshot(bool IsSilent);

public enum LearningSessionStatus
{
    InProgress,
    Completed
}

public sealed class LearningSessionSnapshot
{
    public LearningSessionSnapshot(
        LearningSessionId sessionId,
        CourseId courseId,
        LessonId lessonId,
        int lessonRevision,
        StepId? currentStepId,
        IEnumerable<StepId> completedStepIds,
        IEnumerable<StepId> skippedStepIds,
        IEnumerable<StepId> deferredStepIds,
        LearningSessionModeSnapshot mode,
        LearningSessionStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset updatedAt)
    {
        DomainGuard.Identifier(sessionId.Value, nameof(sessionId));
        DomainGuard.Identifier(courseId.Value, nameof(courseId));
        DomainGuard.Identifier(lessonId.Value, nameof(lessonId));
        DomainGuard.Revision(lessonRevision, nameof(lessonRevision));
        ArgumentNullException.ThrowIfNull(mode);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (updatedAt < startedAt)
        {
            throw new ArgumentException(
                "The update timestamp cannot precede the session start.",
                nameof(updatedAt));
        }

        SessionId = sessionId;
        CourseId = courseId;
        LessonId = lessonId;
        LessonRevision = lessonRevision;
        CurrentStepId = currentStepId;
        CompletedStepIds = DomainGuard.Snapshot(
            completedStepIds,
            nameof(completedStepIds),
            allowEmpty: true);
        SkippedStepIds = DomainGuard.Snapshot(
            skippedStepIds,
            nameof(skippedStepIds),
            allowEmpty: true);
        DeferredStepIds = DomainGuard.Snapshot(
            deferredStepIds,
            nameof(deferredStepIds),
            allowEmpty: true);
        Mode = mode;
        Status = status;
        StartedAt = startedAt;
        UpdatedAt = updatedAt;
    }

    public LearningSessionId SessionId { get; }

    public CourseId CourseId { get; }

    public LessonId LessonId { get; }

    public int LessonRevision { get; }

    public StepId? CurrentStepId { get; }

    public IReadOnlyList<StepId> CompletedStepIds { get; }

    public IReadOnlyList<StepId> SkippedStepIds { get; }

    public IReadOnlyList<StepId> DeferredStepIds { get; }

    public LearningSessionModeSnapshot Mode { get; }

    public LearningSessionStatus Status { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset UpdatedAt { get; }
}

public sealed class LearningSession
{
    private readonly CurriculumLesson _lesson;
    private readonly HashSet<StepId> _completedStepIds;
    private readonly HashSet<StepId> _skippedStepIds;
    private readonly HashSet<StepId> _deferredStepIds;
    private int _currentStepIndex;

    private LearningSession(
        LearningSessionId id,
        CourseId courseId,
        CurriculumLesson lesson,
        LearningSessionModeSnapshot mode,
        DateTimeOffset startedAt,
        DateTimeOffset updatedAt,
        int currentStepIndex,
        HashSet<StepId>? completedStepIds = null,
        HashSet<StepId>? skippedStepIds = null,
        HashSet<StepId>? deferredStepIds = null)
    {
        Id = id;
        CourseId = courseId;
        _lesson = lesson;
        Mode = mode;
        StartedAt = startedAt;
        UpdatedAt = updatedAt;
        _currentStepIndex = currentStepIndex;
        _completedStepIds = completedStepIds ?? [];
        _skippedStepIds = skippedStepIds ?? [];
        _deferredStepIds = deferredStepIds ?? [];
    }

    public LearningSessionId Id { get; }

    public CourseId CourseId { get; }

    public LessonId LessonId => _lesson.Id;

    public int LessonRevision => _lesson.Revision;

    public CurriculumStep? CurrentStep =>
        _currentStepIndex < _lesson.Steps.Count
            ? _lesson.Steps[_currentStepIndex]
            : null;

    public LearningSessionModeSnapshot Mode { get; }

    public LearningSessionStatus Status =>
        CurrentStep is null
            ? LearningSessionStatus.Completed
            : LearningSessionStatus.InProgress;

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<StepId> CompletedStepIds => Ordered(_completedStepIds);

    public IReadOnlyList<StepId> SkippedStepIds => Ordered(_skippedStepIds);

    public IReadOnlyList<StepId> DeferredStepIds => Ordered(_deferredStepIds);

    public static LearningSession Start(
        LearningSessionId id,
        CurriculumCourse course,
        LessonId lessonId,
        LearningSessionModeSnapshot mode,
        DateTimeOffset startedAt)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(mode);

        return new LearningSession(
            id,
            course.Id,
            course.GetLesson(lessonId),
            mode,
            startedAt,
            startedAt,
            currentStepIndex: 0);
    }

    public static LearningSession Restore(
        CurriculumCourse course,
        LearningSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.CourseId != course.Id)
        {
            throw new ArgumentException(
                "The session snapshot belongs to a different course.",
                nameof(snapshot));
        }

        var lesson = course.GetLesson(snapshot.LessonId);
        if (snapshot.LessonRevision != lesson.Revision)
        {
            throw new ArgumentException(
                "The session snapshot targets a different lesson revision.",
                nameof(snapshot));
        }

        var completed = new HashSet<StepId>();
        var skipped = new HashSet<StepId>();
        var deferred = new HashSet<StepId>();
        var dispositions = new Dictionary<StepId, string>();

        Register(snapshot.CompletedStepIds, completed, "completed");
        Register(snapshot.SkippedStepIds, skipped, "skipped");
        Register(snapshot.DeferredStepIds, deferred, "deferred");

        var stepsById = lesson.Steps.ToDictionary(step => step.Id);
        foreach (var stepId in dispositions.Keys)
        {
            if (!stepsById.ContainsKey(stepId))
            {
                throw InvalidSnapshot($"Snapshot references unknown step '{stepId}'.");
            }
        }

        foreach (var stepId in skipped)
        {
            if (stepsById[stepId].Progression != CurriculumStepProgression.Optional)
            {
                throw InvalidSnapshot($"Step '{stepId}' cannot be skipped.");
            }
        }

        foreach (var stepId in deferred)
        {
            if (stepsById[stepId].Progression != CurriculumStepProgression.DeferredAllowed)
            {
                throw InvalidSnapshot($"Step '{stepId}' cannot be deferred.");
            }
        }

        var currentStepIndex = dispositions.Count;
        for (var index = 0; index < lesson.Steps.Count; index++)
        {
            var hasDisposition = dispositions.ContainsKey(lesson.Steps[index].Id);
            if (hasDisposition != (index < currentStepIndex))
            {
                throw InvalidSnapshot("Terminal step state must be a contiguous lesson prefix.");
            }
        }

        var expectedCurrentStepId =
            currentStepIndex < lesson.Steps.Count
                ? lesson.Steps[currentStepIndex].Id
                : (StepId?)null;
        var expectedStatus =
            expectedCurrentStepId is null
                ? LearningSessionStatus.Completed
                : LearningSessionStatus.InProgress;

        if (snapshot.CurrentStepId != expectedCurrentStepId || snapshot.Status != expectedStatus)
        {
            throw InvalidSnapshot("Current step and status do not match the terminal step state.");
        }

        return new LearningSession(
            snapshot.SessionId,
            snapshot.CourseId,
            lesson,
            snapshot.Mode,
            snapshot.StartedAt,
            snapshot.UpdatedAt,
            currentStepIndex,
            completed,
            skipped,
            deferred);

        void Register(
            IEnumerable<StepId> stepIds,
            HashSet<StepId> target,
            string disposition)
        {
            foreach (var stepId in stepIds)
            {
                DomainGuard.Identifier(stepId.Value, nameof(snapshot));
                if (!dispositions.TryAdd(stepId, disposition))
                {
                    throw InvalidSnapshot(
                        $"Step '{stepId}' has more than one terminal disposition.");
                }

                target.Add(stepId);
            }
        }
    }

    public void CompleteCurrentStep(DateTimeOffset occurredAt)
    {
        Advance(_completedStepIds, occurredAt);
    }

    public void SkipCurrentStep(DateTimeOffset occurredAt)
    {
        var step = RequireCurrentStep();
        if (step.Progression != CurriculumStepProgression.Optional)
        {
            throw new InvalidOperationException($"Step '{step.Id}' cannot be skipped.");
        }

        Advance(_skippedStepIds, occurredAt);
    }

    public void DeferCurrentStep(DateTimeOffset occurredAt)
    {
        var step = RequireCurrentStep();
        if (step.Progression != CurriculumStepProgression.DeferredAllowed)
        {
            throw new InvalidOperationException($"Step '{step.Id}' cannot be deferred.");
        }

        Advance(_deferredStepIds, occurredAt);
    }

    public LearningSessionSnapshot CreateSnapshot() =>
        new(
            Id,
            CourseId,
            LessonId,
            LessonRevision,
            CurrentStep?.Id,
            CompletedStepIds,
            SkippedStepIds,
            DeferredStepIds,
            Mode,
            Status,
            StartedAt,
            UpdatedAt);

    private CurriculumStep RequireCurrentStep() =>
        CurrentStep ??
        throw new InvalidOperationException("The learning session is already complete.");

    private void Advance(HashSet<StepId> target, DateTimeOffset occurredAt)
    {
        var step = RequireCurrentStep();
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAt),
                "Session transitions cannot move backwards in time.");
        }

        target.Add(step.Id);
        _currentStepIndex++;
        UpdatedAt = occurredAt;
    }

    private IReadOnlyList<StepId> Ordered(IReadOnlySet<StepId> stepIds) =>
        Array.AsReadOnly(
            _lesson.Steps
                .Where(step => stepIds.Contains(step.Id))
                .Select(step => step.Id)
                .ToArray());

    private static ArgumentException InvalidSnapshot(string message) =>
        new(message, "snapshot");
}
