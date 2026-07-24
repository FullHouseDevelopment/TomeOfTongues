using System.Text.Json.Serialization;

namespace TomeOfTongues.Content.Schema;

public sealed record CourseDefinition
{
    public required string Id { get; init; }
    public required int Revision { get; init; }
    public required IReadOnlyList<LocalizedText> DisplayNames { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProficiencyBand { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CourseProficiencyDefinition? Proficiency { get; init; }
    public required IReadOnlyList<UnitDefinition> Units { get; init; }
}
