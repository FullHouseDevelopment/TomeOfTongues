using TomeOfTongues.Core.Curriculum;

namespace TomeOfTongues.Core.Exercises;

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
