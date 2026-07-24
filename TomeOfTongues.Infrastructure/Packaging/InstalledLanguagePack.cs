using TomeOfTongues.Content.Schema;

namespace TomeOfTongues.Infrastructure.Packaging;

public sealed record InstalledLanguagePack
{
    public required string PackId { get; init; }
    public required string PackageVersion { get; init; }
    public required string MinimumEngineVersion { get; init; }
    public required string LanguageTag { get; init; }
    public required IReadOnlyList<string> LocaleTags { get; init; }
    public required IReadOnlyList<LocalizedText> DisplayNames { get; init; }
    public required string PackagePath { get; init; }
}
