using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.LearningSessions;

namespace TomeOfTongues.Core.Exercises;

public readonly record struct ExerciseId
{
    public ExerciseId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ExerciseAttemptId
{
    public ExerciseAttemptId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct EvidenceObservationId
{
    public EvidenceObservationId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct RepresentationId
{
    public RepresentationId(string value)
    {
        Value = DomainGuard.Identifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public enum PromptModality
{
    Text,
    Audio,
    Image
}

public enum ResponseModality
{
    Selection,
    Typed,
    Spoken,
    SelfReported
}

public enum SkillDimension
{
    Recognition,
    Recall,
    ListeningComprehension,
    ReadingRecognition,
    SilentProduction,
    SpokenProduction
}

public enum AssistanceKind
{
    ShownAutomatically,
    RevealedOnRequest
}

public enum AttemptOutcome
{
    Evaluated,
    Skipped,
    Deferred
}

public enum EvidenceMeasurement
{
    Measured,
    SelfReported,
    Estimated
}

public readonly record struct ResponseLatency
{
    public ResponseLatency(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Response latency cannot be negative.");
        }

        Value = value;
    }

    public TimeSpan Value { get; }
}

public readonly record struct ConfidenceRating
{
    public ConfidenceRating(int value)
    {
        if (value is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Confidence must be between 1 and 5.");
        }

        Value = value;
    }

    public int Value { get; }
}

public sealed record AssistanceUsage
{
    public AssistanceUsage(string groupId, AssistanceKind kind)
    {
        GroupId = DomainGuard.Identifier(groupId, nameof(groupId));

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Kind = kind;
    }

    public string GroupId { get; }

    public AssistanceKind Kind { get; }
}

public sealed record EvidenceMapping
{
    public EvidenceMapping(
        ObjectiveId objectiveId,
        SkillDimension skillDimension,
        RepresentationId? representationId = null)
    {
        DomainGuard.Identifier(objectiveId.Value, nameof(objectiveId));

        if (!Enum.IsDefined(skillDimension))
        {
            throw new ArgumentOutOfRangeException(nameof(skillDimension));
        }

        if (representationId is { } value)
        {
            DomainGuard.Identifier(value.Value, nameof(representationId));
        }

        ObjectiveId = objectiveId;
        SkillDimension = skillDimension;
        RepresentationId = representationId;
    }

    public ObjectiveId ObjectiveId { get; }

    public SkillDimension SkillDimension { get; }

    public RepresentationId? RepresentationId { get; }
}

public sealed class ExerciseDefinition
{
    public ExerciseDefinition(
        ExerciseId id,
        int revision,
        PromptModality promptModality,
        ResponseModality responseModality,
        IEnumerable<EvidenceMapping> evidenceMappings)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        DomainGuard.Revision(revision, nameof(revision));

        if (!Enum.IsDefined(promptModality))
        {
            throw new ArgumentOutOfRangeException(nameof(promptModality));
        }

        if (!Enum.IsDefined(responseModality))
        {
            throw new ArgumentOutOfRangeException(nameof(responseModality));
        }

        var mappingSnapshot = DomainGuard.Snapshot(
            evidenceMappings,
            nameof(evidenceMappings),
            allowEmpty: true);
        var mappingKeys = new HashSet<(ObjectiveId, SkillDimension, RepresentationId?)>();
        if (mappingSnapshot.Any(
                mapping => !mappingKeys.Add(
                    (mapping.ObjectiveId, mapping.SkillDimension, mapping.RepresentationId))))
        {
            throw new ArgumentException(
                "Evidence mappings must not contain duplicate objective, skill, and representation keys.",
                nameof(evidenceMappings));
        }

        Id = id;
        Revision = revision;
        PromptModality = promptModality;
        ResponseModality = responseModality;
        EvidenceMappings = mappingSnapshot;
    }

    public ExerciseId Id { get; }

    public int Revision { get; }

    public PromptModality PromptModality { get; }

    public ResponseModality ResponseModality { get; }

    public IReadOnlyList<EvidenceMapping> EvidenceMappings { get; }
}

public sealed class EvidenceObservation
{
    internal EvidenceObservation(
        EvidenceObservationId id,
        ExerciseAttemptId attemptId,
        EvidenceMapping mapping,
        PromptModality promptModality,
        ResponseModality responseModality,
        IReadOnlyList<AssistanceUsage> assistance,
        bool isCorrect,
        EvidenceMeasurement measurement,
        ResponseLatency? latency,
        ConfidenceRating? confidence,
        DateTimeOffset observedAt,
        int packRevision,
        int exerciseRevision)
    {
        Id = id;
        AttemptId = attemptId;
        ObjectiveId = mapping.ObjectiveId;
        SkillDimension = mapping.SkillDimension;
        RepresentationId = mapping.RepresentationId;
        PromptModality = promptModality;
        ResponseModality = responseModality;
        Assistance = assistance;
        IsCorrect = isCorrect;
        Measurement = measurement;
        Latency = latency;
        Confidence = confidence;
        ObservedAt = observedAt;
        PackRevision = packRevision;
        ExerciseRevision = exerciseRevision;
    }

    public EvidenceObservationId Id { get; }

    public ExerciseAttemptId AttemptId { get; }

    public ObjectiveId ObjectiveId { get; }

    public SkillDimension SkillDimension { get; }

    public RepresentationId? RepresentationId { get; }

    public PromptModality PromptModality { get; }

    public ResponseModality ResponseModality { get; }

    public IReadOnlyList<AssistanceUsage> Assistance { get; }

    public bool IsCorrect { get; }

    public EvidenceMeasurement Measurement { get; }

    public ResponseLatency? Latency { get; }

    public ConfidenceRating? Confidence { get; }

    public DateTimeOffset ObservedAt { get; }

    public int PackRevision { get; }

    public int ExerciseRevision { get; }
}

public sealed class ExerciseAttempt
{
    private ExerciseAttempt(
        ExerciseAttemptId id,
        LearningSessionId sessionId,
        ExerciseDefinition exercise,
        int packRevision,
        bool wasSilent,
        string? response,
        IReadOnlyList<AssistanceUsage> assistance,
        ResponseLatency? latency,
        ConfidenceRating? confidence,
        AttemptOutcome outcome,
        bool? isCorrect,
        EvidenceMeasurement? measurement,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<EvidenceObservation> evidence)
    {
        Id = id;
        SessionId = sessionId;
        ExerciseId = exercise.Id;
        ExerciseRevision = exercise.Revision;
        PackRevision = packRevision;
        PromptModality = exercise.PromptModality;
        ResponseModality = exercise.ResponseModality;
        WasSilent = wasSilent;
        Response = response;
        Assistance = assistance;
        Latency = latency;
        Confidence = confidence;
        Outcome = outcome;
        IsCorrect = isCorrect;
        Measurement = measurement;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Evidence = evidence;
    }

    public ExerciseAttemptId Id { get; }

    public LearningSessionId SessionId { get; }

    public ExerciseId ExerciseId { get; }

    public int ExerciseRevision { get; }

    public int PackRevision { get; }

    public PromptModality PromptModality { get; }

    public ResponseModality ResponseModality { get; }

    public bool WasSilent { get; }

    public string? Response { get; }

    public IReadOnlyList<AssistanceUsage> Assistance { get; }

    public ResponseLatency? Latency { get; }

    public ConfidenceRating? Confidence { get; }

    public AttemptOutcome Outcome { get; }

    public bool? IsCorrect { get; }

    public EvidenceMeasurement? Measurement { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset CompletedAt { get; }

    public IReadOnlyList<EvidenceObservation> Evidence { get; }

    public static ExerciseAttempt Record(
        ExerciseAttemptId id,
        LearningSessionId sessionId,
        ExerciseDefinition exercise,
        int packRevision,
        bool wasSilent,
        string? response,
        IEnumerable<AssistanceUsage> assistance,
        ResponseLatency? latency,
        ConfidenceRating? confidence,
        AttemptOutcome outcome,
        bool? isCorrect,
        EvidenceMeasurement? measurement,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        DomainGuard.Identifier(id.Value, nameof(id));
        DomainGuard.Identifier(sessionId.Value, nameof(sessionId));
        ArgumentNullException.ThrowIfNull(exercise);
        DomainGuard.Revision(packRevision, nameof(packRevision));

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (completedAt < startedAt)
        {
            throw new ArgumentException(
                "The completion timestamp cannot precede the attempt start.",
                nameof(completedAt));
        }

        if (latency is { } responseLatency)
        {
            if (responseLatency.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(latency));
            }

            if (responseLatency.Value > completedAt - startedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(latency),
                    "Response latency cannot exceed the attempt duration.");
            }
        }

        if (confidence is { } confidenceRating &&
            confidenceRating.Value is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        if (measurement is { } evidenceMeasurement && !Enum.IsDefined(evidenceMeasurement))
        {
            throw new ArgumentOutOfRangeException(nameof(measurement));
        }

        var wasEvaluated = outcome == AttemptOutcome.Evaluated;
        if (wasEvaluated != isCorrect.HasValue || wasEvaluated != measurement.HasValue)
        {
            throw new ArgumentException(
                "Evaluated attempts require correctness and measurement; skipped or deferred attempts must not provide them.",
                nameof(outcome));
        }

        if (wasEvaluated &&
            exercise.ResponseModality == ResponseModality.SelfReported &&
            measurement != EvidenceMeasurement.SelfReported)
        {
            throw new ArgumentException(
                "Self-reported responses must be labelled as self-reported evidence.",
                nameof(measurement));
        }

        var assistanceSnapshot = DomainGuard.Snapshot(
            assistance,
            nameof(assistance),
            allowEmpty: true);
        DomainGuard.Unique(assistanceSnapshot, item => item.GroupId, nameof(assistance));

        var observations = wasEvaluated
            ? exercise.EvidenceMappings
                .Select(
                    (mapping, index) => new EvidenceObservation(
                        new EvidenceObservationId($"{id.Value}:{index + 1}"),
                        id,
                        mapping,
                        exercise.PromptModality,
                        exercise.ResponseModality,
                        assistanceSnapshot,
                        isCorrect!.Value,
                        measurement!.Value,
                        latency,
                        confidence,
                        completedAt,
                        packRevision,
                        exercise.Revision))
                .ToArray()
            : [];

        return new ExerciseAttempt(
            id,
            sessionId,
            exercise,
            packRevision,
            wasSilent,
            response,
            assistanceSnapshot,
            latency,
            confidence,
            outcome,
            isCorrect,
            measurement,
            startedAt,
            completedAt,
            Array.AsReadOnly(observations));
    }
}
