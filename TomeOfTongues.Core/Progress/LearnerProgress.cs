using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.Exercises;

namespace TomeOfTongues.Core.Progress;

public sealed class LearnerProgressSnapshot
{
    public LearnerProgressSnapshot(
        CourseId courseId,
        int courseRevision,
        IEnumerable<LessonId> unlockedLessonIds,
        IEnumerable<LessonId> completedLessonIds,
        LessonId? lastLessonId,
        StepId? lastStepId,
        DateTimeOffset enrolledAt,
        DateTimeOffset updatedAt)
    {
        DomainGuard.Identifier(courseId.Value, nameof(courseId));
        DomainGuard.Revision(courseRevision, nameof(courseRevision));

        if (lastLessonId.HasValue != lastStepId.HasValue)
        {
            throw new ArgumentException(
                "The last curriculum position requires both a lesson and a step.");
        }

        if (lastLessonId is { } lessonId)
        {
            DomainGuard.Identifier(lessonId.Value, nameof(lastLessonId));
        }

        if (lastStepId is { } stepId)
        {
            DomainGuard.Identifier(stepId.Value, nameof(lastStepId));
        }

        if (updatedAt < enrolledAt)
        {
            throw new ArgumentException(
                "The update timestamp cannot precede enrollment.",
                nameof(updatedAt));
        }

        var unlocked = DomainGuard.Snapshot(
            unlockedLessonIds,
            nameof(unlockedLessonIds),
            allowEmpty: false);
        var completed = DomainGuard.Snapshot(
            completedLessonIds,
            nameof(completedLessonIds),
            allowEmpty: true);
        DomainGuard.Unique(unlocked, lesson => lesson, nameof(unlockedLessonIds));
        DomainGuard.Unique(completed, lesson => lesson, nameof(completedLessonIds));

        CourseId = courseId;
        CourseRevision = courseRevision;
        UnlockedLessonIds = unlocked;
        CompletedLessonIds = completed;
        LastLessonId = lastLessonId;
        LastStepId = lastStepId;
        EnrolledAt = enrolledAt;
        UpdatedAt = updatedAt;
    }

    public CourseId CourseId { get; }

    public int CourseRevision { get; }

    public IReadOnlyList<LessonId> UnlockedLessonIds { get; }

    public IReadOnlyList<LessonId> CompletedLessonIds { get; }

    public LessonId? LastLessonId { get; }

    public StepId? LastStepId { get; }

    public DateTimeOffset EnrolledAt { get; }

    public DateTimeOffset UpdatedAt { get; }
}

public sealed class LearnerProgress
{
    private readonly CurriculumCourse _course;
    private readonly HashSet<LessonId> _unlockedLessonIds;
    private readonly HashSet<LessonId> _completedLessonIds;

    private LearnerProgress(
        CurriculumCourse course,
        HashSet<LessonId> unlockedLessonIds,
        HashSet<LessonId> completedLessonIds,
        LessonId? lastLessonId,
        StepId? lastStepId,
        DateTimeOffset enrolledAt,
        DateTimeOffset updatedAt)
    {
        _course = course;
        _unlockedLessonIds = unlockedLessonIds;
        _completedLessonIds = completedLessonIds;
        LastLessonId = lastLessonId;
        LastStepId = lastStepId;
        EnrolledAt = enrolledAt;
        UpdatedAt = updatedAt;
    }

    public CourseId CourseId => _course.Id;

    public int CourseRevision => _course.Revision;

    public IReadOnlyList<LessonId> UnlockedLessonIds => Ordered(_unlockedLessonIds);

    public IReadOnlyList<LessonId> CompletedLessonIds => Ordered(_completedLessonIds);

    public LessonId? LastLessonId { get; private set; }

    public StepId? LastStepId { get; private set; }

    public DateTimeOffset EnrolledAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static LearnerProgress Enroll(
        CurriculumCourse course,
        DateTimeOffset enrolledAt)
    {
        ArgumentNullException.ThrowIfNull(course);

        return new LearnerProgress(
            course,
            CalculateUnlockedLessons(course, []),
            [],
            lastLessonId: null,
            lastStepId: null,
            enrolledAt,
            enrolledAt);
    }

    public static LearnerProgress Restore(
        CurriculumCourse course,
        LearnerProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.CourseId != course.Id || snapshot.CourseRevision != course.Revision)
        {
            throw InvalidSnapshot(
                "The progress snapshot targets a different course or revision.");
        }

        var lessons = Lessons(course);
        var lessonsById = lessons.ToDictionary(lesson => lesson.Id);
        var completed = new HashSet<LessonId>(snapshot.CompletedLessonIds);

        foreach (var completedLessonId in completed)
        {
            if (!lessonsById.TryGetValue(completedLessonId, out var completedLesson))
            {
                throw InvalidSnapshot(
                    $"Snapshot references unknown completed lesson '{completedLessonId}'.");
            }

            if (completedLesson.PrerequisiteLessonIds.Any(
                    prerequisite => !completed.Contains(prerequisite)))
            {
                throw InvalidSnapshot(
                    $"Completed lesson '{completedLessonId}' is missing a prerequisite.");
            }
        }

        var expectedUnlocked = CalculateUnlockedLessons(course, completed);
        var unlocked = new HashSet<LessonId>(snapshot.UnlockedLessonIds);
        if (!unlocked.SetEquals(expectedUnlocked))
        {
            throw InvalidSnapshot(
                "Unlocked lessons do not match the completed prerequisite state.");
        }

        if (snapshot.LastLessonId is { } lastLessonId)
        {
            if (!unlocked.Contains(lastLessonId))
            {
                throw InvalidSnapshot(
                    $"The last lesson '{lastLessonId}' is not unlocked.");
            }

            var lesson = lessonsById[lastLessonId];
            if (!lesson.Steps.Any(step => step.Id == snapshot.LastStepId))
            {
                throw InvalidSnapshot(
                    $"The last step '{snapshot.LastStepId}' does not belong to lesson '{lastLessonId}'.");
            }
        }

        return new LearnerProgress(
            course,
            unlocked,
            completed,
            snapshot.LastLessonId,
            snapshot.LastStepId,
            snapshot.EnrolledAt,
            snapshot.UpdatedAt);
    }

    public bool IsLessonUnlocked(LessonId lessonId)
    {
        DomainGuard.Identifier(lessonId.Value, nameof(lessonId));
        return _unlockedLessonIds.Contains(lessonId);
    }

    public void RecordPosition(
        LessonId lessonId,
        StepId stepId,
        DateTimeOffset occurredAt)
    {
        var lesson = RequireUnlockedLesson(lessonId);
        if (!lesson.Steps.Any(step => step.Id == stepId))
        {
            throw new ArgumentException(
                $"Step '{stepId}' does not belong to lesson '{lessonId}'.",
                nameof(stepId));
        }

        RequireCurrentTimestamp(occurredAt);
        LastLessonId = lessonId;
        LastStepId = stepId;
        UpdatedAt = occurredAt;
    }

    public void CompleteLesson(
        LessonId lessonId,
        DateTimeOffset occurredAt)
    {
        RequireUnlockedLesson(lessonId);
        if (_completedLessonIds.Contains(lessonId))
        {
            throw new InvalidOperationException(
                $"Lesson '{lessonId}' is already complete.");
        }

        RequireCurrentTimestamp(occurredAt);
        _completedLessonIds.Add(lessonId);

        foreach (var unlockedLessonId in CalculateUnlockedLessons(
                     _course,
                     _completedLessonIds))
        {
            _unlockedLessonIds.Add(unlockedLessonId);
        }

        UpdatedAt = occurredAt;
    }

    public LearnerProgressSnapshot CreateSnapshot() =>
        new(
            CourseId,
            CourseRevision,
            UnlockedLessonIds,
            CompletedLessonIds,
            LastLessonId,
            LastStepId,
            EnrolledAt,
            UpdatedAt);

    private CurriculumLesson RequireUnlockedLesson(LessonId lessonId)
    {
        DomainGuard.Identifier(lessonId.Value, nameof(lessonId));
        if (!_unlockedLessonIds.Contains(lessonId))
        {
            throw new InvalidOperationException(
                $"Lesson '{lessonId}' is not unlocked.");
        }

        return _course.GetLesson(lessonId);
    }

    private void RequireCurrentTimestamp(DateTimeOffset occurredAt)
    {
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurredAt),
                "Progress transitions cannot move backwards in time.");
        }
    }

    private IReadOnlyList<LessonId> Ordered(IReadOnlySet<LessonId> lessonIds) =>
        Array.AsReadOnly(
            Lessons(_course)
                .Where(lesson => lessonIds.Contains(lesson.Id))
                .Select(lesson => lesson.Id)
                .ToArray());

    private static CurriculumLesson[] Lessons(CurriculumCourse course) =>
        course.Units.SelectMany(unit => unit.Lessons).ToArray();

    private static HashSet<LessonId> CalculateUnlockedLessons(
        CurriculumCourse course,
        IReadOnlySet<LessonId> completedLessonIds) =>
        Lessons(course)
            .Where(
                lesson =>
                    completedLessonIds.Contains(lesson.Id) ||
                    lesson.PrerequisiteLessonIds.All(completedLessonIds.Contains))
            .Select(lesson => lesson.Id)
            .ToHashSet();

    private static ArgumentException InvalidSnapshot(string message) =>
        new(message, "snapshot");
}

public sealed class LearnerSkillStateSnapshot
{
    public LearnerSkillStateSnapshot(
        ObjectiveId objectiveId,
        SkillDimension skillDimension,
        RepresentationId? representationId,
        IEnumerable<EvidenceObservation> observations,
        DateTimeOffset updatedAt)
    {
        DomainGuard.Identifier(objectiveId.Value, nameof(objectiveId));
        if (!Enum.IsDefined(skillDimension))
        {
            throw new ArgumentOutOfRangeException(nameof(skillDimension));
        }

        if (representationId is { } representation)
        {
            DomainGuard.Identifier(representation.Value, nameof(representationId));
        }

        var observationSnapshot = DomainGuard.Snapshot(
            observations,
            nameof(observations),
            allowEmpty: false);
        DomainGuard.Unique(
            observationSnapshot,
            observation => observation.Id,
            nameof(observations));

        ObjectiveId = objectiveId;
        SkillDimension = skillDimension;
        RepresentationId = representationId;
        Observations = observationSnapshot;
        UpdatedAt = updatedAt;
    }

    public ObjectiveId ObjectiveId { get; }

    public SkillDimension SkillDimension { get; }

    public RepresentationId? RepresentationId { get; }

    public IReadOnlyList<EvidenceObservation> Observations { get; }

    public DateTimeOffset UpdatedAt { get; }
}

public sealed class LearnerSkillState
{
    private readonly List<EvidenceObservation> _observations;
    private readonly HashSet<EvidenceObservationId> _observationIds;

    private LearnerSkillState(
        ObjectiveId objectiveId,
        SkillDimension skillDimension,
        RepresentationId? representationId,
        IEnumerable<EvidenceObservation> observations,
        DateTimeOffset updatedAt)
    {
        ObjectiveId = objectiveId;
        SkillDimension = skillDimension;
        RepresentationId = representationId;
        _observations = [.. observations];
        _observationIds = _observations.Select(observation => observation.Id).ToHashSet();
        UpdatedAt = updatedAt;
    }

    public ObjectiveId ObjectiveId { get; }

    public SkillDimension SkillDimension { get; }

    public RepresentationId? RepresentationId { get; }

    public IReadOnlyList<EvidenceObservation> Observations =>
        _observations.AsReadOnly();

    public DateTimeOffset UpdatedAt { get; private set; }

    public static LearnerSkillState Create(EvidenceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        return new LearnerSkillState(
            observation.ObjectiveId,
            observation.SkillDimension,
            observation.RepresentationId,
            [observation],
            observation.ObservedAt);
    }

    public static LearnerSkillState Restore(LearnerSkillStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        DateTimeOffset? previousObservedAt = null;
        foreach (var observation in snapshot.Observations)
        {
            RequireMatchingIdentity(
                snapshot.ObjectiveId,
                snapshot.SkillDimension,
                snapshot.RepresentationId,
                observation,
                "snapshot");

            if (previousObservedAt is { } previous &&
                observation.ObservedAt < previous)
            {
                throw InvalidSkillSnapshot(
                    "Evidence observations must be in chronological order.");
            }

            previousObservedAt = observation.ObservedAt;
        }

        if (snapshot.UpdatedAt != snapshot.Observations[^1].ObservedAt)
        {
            throw InvalidSkillSnapshot(
                "The update timestamp must match the latest evidence observation.");
        }

        return new LearnerSkillState(
            snapshot.ObjectiveId,
            snapshot.SkillDimension,
            snapshot.RepresentationId,
            snapshot.Observations,
            snapshot.UpdatedAt);
    }

    public void RecordEvidence(EvidenceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        RequireMatchingIdentity(
            ObjectiveId,
            SkillDimension,
            RepresentationId,
            observation,
            nameof(observation));

        if (!_observationIds.Add(observation.Id))
        {
            throw new ArgumentException(
                $"Evidence observation '{observation.Id}' is already recorded.",
                nameof(observation));
        }

        if (observation.ObservedAt < UpdatedAt)
        {
            _observationIds.Remove(observation.Id);
            throw new ArgumentOutOfRangeException(
                nameof(observation),
                "Evidence observations cannot move backwards in time.");
        }

        _observations.Add(observation);
        UpdatedAt = observation.ObservedAt;
    }

    public LearnerSkillStateSnapshot CreateSnapshot() =>
        new(
            ObjectiveId,
            SkillDimension,
            RepresentationId,
            Observations,
            UpdatedAt);

    private static void RequireMatchingIdentity(
        ObjectiveId objectiveId,
        SkillDimension skillDimension,
        RepresentationId? representationId,
        EvidenceObservation observation,
        string parameterName)
    {
        if (observation.ObjectiveId != objectiveId ||
            observation.SkillDimension != skillDimension ||
            observation.RepresentationId != representationId)
        {
            throw new ArgumentException(
                "Evidence must match the skill state's objective, dimension, and representation.",
                parameterName);
        }
    }

    private static ArgumentException InvalidSkillSnapshot(string message) =>
        new(message, "snapshot");
}
