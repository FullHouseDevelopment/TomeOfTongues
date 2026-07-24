using TomeOfTongues.Core.Curriculum;

namespace TomeOfTongues.Core.Exercises;

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
