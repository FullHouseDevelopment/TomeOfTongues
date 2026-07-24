using TomeOfTongues.Core.Curriculum;

namespace TomeOfTongues.Core.Exercises;

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
