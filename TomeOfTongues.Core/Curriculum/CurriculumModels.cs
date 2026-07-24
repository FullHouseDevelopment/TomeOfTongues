using System.Collections.ObjectModel;

namespace TomeOfTongues.Core.Curriculum;

public readonly record struct CourseId
{
    public CourseId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct UnitId
{
    public UnitId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct LessonId
{
    public LessonId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct StepId
{
    public StepId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ObjectiveId
{
    public ObjectiveId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public enum CurriculumStepKind
{
    Explanation,
    ContextualExposure,
    Exercise,
    Checkpoint,
    ExternalResource,
    OptionalSpeakingOpportunity
}

public enum CurriculumStepProgression
{
    Required,
    Optional,
    DeferredAllowed
}

public sealed class CurriculumObjective
{
    public CurriculumObjective(ObjectiveId id, int revision)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        DomainGuard.Revision(revision, nameof(revision));

        Id = id;
        Revision = revision;
    }

    public ObjectiveId Id { get; }

    public int Revision { get; }
}

public sealed class CurriculumStep
{
    public CurriculumStep(
        StepId id,
        int revision,
        CurriculumStepKind kind,
        CurriculumStepProgression progression)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        DomainGuard.Revision(revision, nameof(revision));

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(progression))
        {
            throw new ArgumentOutOfRangeException(nameof(progression));
        }

        if (kind == CurriculumStepKind.OptionalSpeakingOpportunity &&
            progression != CurriculumStepProgression.DeferredAllowed)
        {
            throw new ArgumentException(
                "Speaking opportunities must allow deferral and cannot gate curriculum progression.",
                nameof(progression));
        }

        Id = id;
        Revision = revision;
        Kind = kind;
        Progression = progression;
    }

    public StepId Id { get; }

    public int Revision { get; }

    public CurriculumStepKind Kind { get; }

    public CurriculumStepProgression Progression { get; }
}

public sealed class CurriculumLesson
{
    public CurriculumLesson(
        LessonId id,
        int revision,
        IEnumerable<CurriculumObjective> objectives,
        IEnumerable<CurriculumStep> steps,
        IEnumerable<LessonId>? prerequisiteLessonIds = null)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        DomainGuard.Revision(revision, nameof(revision));

        var objectiveSnapshot = DomainGuard.Snapshot(
            objectives,
            nameof(objectives),
            allowEmpty: false);
        var stepSnapshot = DomainGuard.Snapshot(steps, nameof(steps), allowEmpty: false);
        var prerequisiteSnapshot = DomainGuard.Snapshot(
            prerequisiteLessonIds ?? [],
            nameof(prerequisiteLessonIds),
            allowEmpty: true);

        DomainGuard.Unique(objectiveSnapshot, objective => objective.Id, nameof(objectives));
        DomainGuard.Unique(stepSnapshot, step => step.Id, nameof(steps));
        DomainGuard.Unique(prerequisiteSnapshot, prerequisite => prerequisite, nameof(prerequisiteLessonIds));

        foreach (var prerequisite in prerequisiteSnapshot)
        {
            DomainGuard.Identifier(prerequisite.Value, nameof(prerequisiteLessonIds));
            if (prerequisite == id)
            {
                throw new ArgumentException(
                    "A lesson cannot require itself.",
                    nameof(prerequisiteLessonIds));
            }
        }

        Id = id;
        Revision = revision;
        Objectives = objectiveSnapshot;
        Steps = stepSnapshot;
        PrerequisiteLessonIds = prerequisiteSnapshot;
    }

    public LessonId Id { get; }

    public int Revision { get; }

    public IReadOnlyList<CurriculumObjective> Objectives { get; }

    public IReadOnlyList<CurriculumStep> Steps { get; }

    public IReadOnlyList<LessonId> PrerequisiteLessonIds { get; }
}

public sealed class CurriculumUnit
{
    public CurriculumUnit(UnitId id, int revision, IEnumerable<CurriculumLesson> lessons)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        DomainGuard.Revision(revision, nameof(revision));

        var lessonSnapshot = DomainGuard.Snapshot(lessons, nameof(lessons), allowEmpty: false);
        DomainGuard.Unique(lessonSnapshot, lesson => lesson.Id, nameof(lessons));

        Id = id;
        Revision = revision;
        Lessons = lessonSnapshot;
    }

    public UnitId Id { get; }

    public int Revision { get; }

    public IReadOnlyList<CurriculumLesson> Lessons { get; }
}

public sealed class CurriculumCourse
{
    private readonly IReadOnlyDictionary<LessonId, CurriculumLesson> _lessons;

    public CurriculumCourse(
        CourseId id,
        int revision,
        string proficiencyBand,
        IEnumerable<CurriculumUnit> units)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        DomainGuard.Revision(revision, nameof(revision));
        DomainGuard.Text(proficiencyBand, nameof(proficiencyBand));

        var unitSnapshot = DomainGuard.Snapshot(units, nameof(units), allowEmpty: false);
        DomainGuard.Unique(unitSnapshot, unit => unit.Id, nameof(units));

        var lessons = unitSnapshot.SelectMany(unit => unit.Lessons).ToArray();
        DomainGuard.Unique(lessons, lesson => lesson.Id, nameof(units));
        ValidatePrerequisites(lessons);

        Id = id;
        Revision = revision;
        ProficiencyBand = proficiencyBand;
        Units = unitSnapshot;
        _lessons = new ReadOnlyDictionary<LessonId, CurriculumLesson>(
            lessons.ToDictionary(lesson => lesson.Id));
    }

    public CourseId Id { get; }

    public int Revision { get; }

    public string ProficiencyBand { get; }

    public IReadOnlyList<CurriculumUnit> Units { get; }

    public CurriculumLesson GetLesson(LessonId lessonId)
    {
        DomainGuard.Identifier(lessonId.Value, nameof(lessonId));

        return _lessons.TryGetValue(lessonId, out var lesson)
            ? lesson
            : throw new ArgumentException(
                $"Lesson '{lessonId}' does not belong to course '{Id}'.",
                nameof(lessonId));
    }

    private static void ValidatePrerequisites(IReadOnlyCollection<CurriculumLesson> lessons)
    {
        var lessonsById = lessons.ToDictionary(lesson => lesson.Id);

        foreach (var lesson in lessons)
        {
            foreach (var prerequisite in lesson.PrerequisiteLessonIds)
            {
                if (!lessonsById.ContainsKey(prerequisite))
                {
                    throw new ArgumentException(
                        $"Lesson '{lesson.Id}' references unknown prerequisite '{prerequisite}'.",
                        nameof(lessons));
                }
            }
        }

        var visitState = new Dictionary<LessonId, int>();
        foreach (var lesson in lessons)
        {
            Visit(lesson);
        }

        void Visit(CurriculumLesson lesson)
        {
            if (visitState.TryGetValue(lesson.Id, out var state))
            {
                if (state == 1)
                {
                    throw new ArgumentException(
                        "Lesson prerequisites must not contain a cycle.",
                        nameof(lessons));
                }

                return;
            }

            visitState[lesson.Id] = 1;
            foreach (var prerequisite in lesson.PrerequisiteLessonIds)
            {
                Visit(lessonsById[prerequisite]);
            }

            visitState[lesson.Id] = 2;
        }
    }
}

internal static class DomainGuard
{
    public static string Identifier(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Identifiers must be nonempty and have no surrounding whitespace.",
                parameterName);
        }

        return value;
    }

    public static string Text(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The value must be nonempty.", parameterName);
        }

        return value;
    }

    public static int Revision(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Revisions must be positive.");
        }

        return value;
    }

    public static IReadOnlyList<T> Snapshot<T>(
        IEnumerable<T> values,
        string parameterName,
        bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var snapshot = values.ToArray();
        if (!allowEmpty && snapshot.Length == 0)
        {
            throw new ArgumentException("The collection must not be empty.", parameterName);
        }

        if (snapshot.Any(value => value is null))
        {
            throw new ArgumentException("The collection must not contain null values.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }

    public static void Unique<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> keySelector,
        string parameterName)
        where TKey : notnull
    {
        var seen = new HashSet<TKey>();
        if (values.Any(value => !seen.Add(keySelector(value))))
        {
            throw new ArgumentException("The collection must not contain duplicate identifiers.", parameterName);
        }
    }
}
