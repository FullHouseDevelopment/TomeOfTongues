using TomeOfTongues.Core.Curriculum;
using TomeOfTongues.Core.LearningSessions;

namespace TomeOfTongues.Core.Exercises;

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
