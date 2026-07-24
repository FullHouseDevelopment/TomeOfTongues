namespace TomeOfTongues.Content.Schema;

public enum ProficiencyFacet
{
    Listening,
    Reading,
    SpokenInteraction,
    SpokenProduction,
    WrittenInteraction,
    WrittenProduction,
    Mediation
}

public enum ExternalAlignmentStatus
{
    Estimated,
    Reviewed,
    Validated
}

public sealed record ProficiencyStageDefinition
{
    public required string Id { get; init; }
    public required int Order { get; init; }
    public required IReadOnlyList<LocalizedText> DisplayNames { get; init; }
    public required IReadOnlyList<LocalizedText> CanDoDescriptors { get; init; }
}

public sealed record ProficiencyMilestoneDefinition
{
    public required string Id { get; init; }
    public required string AnchorStage { get; init; }
    public required int WithinStageOrder { get; init; }
    public required IReadOnlyList<LocalizedText> DisplayNames { get; init; }
    public required IReadOnlyList<LocalizedText> CanDoDescriptors { get; init; }
}

public sealed record ExternalFrameworkDefinition
{
    public required string Id { get; init; }
    public required IReadOnlyList<string> OrderedReferenceLevels { get; init; }
}

public sealed record ProficiencyFrameworkDefinition : ITotlangDocument
{
    public required int SchemaVersion { get; init; }
    public required string FrameworkId { get; init; }
    public required int FrameworkVersion { get; init; }
    public required IReadOnlyList<ProficiencyStageDefinition> Stages { get; init; }
    public required IReadOnlyList<ProficiencyMilestoneDefinition> GlobalMilestones { get; init; }
    public required IReadOnlyList<ExternalFrameworkDefinition> ExternalFrameworks { get; init; }
}

public sealed record TotlangProficiencyCatalog : ITotlangDocument
{
    public required int SchemaVersion { get; init; }
    public required IReadOnlyList<ProficiencyMilestoneDefinition> Milestones { get; init; }
}

public sealed record FacetProficiencyRange
{
    public required ProficiencyFacet Facet { get; init; }
    public required string EntryStage { get; init; }
    public required string ExitStage { get; init; }
}

public sealed record ExternalProficiencyAlignment
{
    public required string FrameworkId { get; init; }
    public required string LowerReferenceLevel { get; init; }
    public required string UpperReferenceLevel { get; init; }
    public required IReadOnlyList<ProficiencyFacet> Facets { get; init; }
    public required ExternalAlignmentStatus Status { get; init; }
    public required string AuthoritativeSourceUri { get; init; }
    public DateOnly? ReviewDate { get; init; }
}

public sealed record CourseProficiencyDefinition
{
    public required string OverallEntryStage { get; init; }
    public required string OverallExitStage { get; init; }
    public required IReadOnlyList<FacetProficiencyRange> Facets { get; init; }
    public required IReadOnlyList<string> MilestoneIds { get; init; }
    public required IReadOnlyList<ExternalProficiencyAlignment> ExternalAlignments { get; init; }
}

public static class TomeOfTonguesProficiencyFramework
{
    public const int FrameworkVersion = 1;

    public static IReadOnlyList<string> StageIds { get; } =
        Enumerable.Range(1, 18).Select(number => $"T{number:00}").ToArray();

    public static IReadOnlySet<string> GlobalMilestoneIds { get; } =
        new HashSet<string>(
            [
                "global:initial-repertoire",
                "global:independent-exchange",
                "global:complex-participation"
            ],
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ExternalFrameworkLevels { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["cefr"] = ["A1", "A2", "B1", "B2", "C1", "C2"],
            ["jlpt"] = ["N5", "N4", "N3", "N2", "N1"],
            ["jf-standard"] = ["A1", "A2", "B1", "B2", "C1", "C2"]
        };

    public static bool TryGetStageOrder(string stageId, out int order)
    {
        order = Array.IndexOf(StageIds.ToArray(), stageId);
        return order >= 0;
    }

    public static bool TryGetExternalReferenceOrder(
        string frameworkId,
        string referenceLevel,
        out int order)
    {
        order = -1;
        if (!ExternalFrameworkLevels.TryGetValue(frameworkId, out var levels))
        {
            return false;
        }

        order = levels.ToList().IndexOf(referenceLevel);
        return order >= 0;
    }
}
