using TomeOfTongues.Content.Packaging;
using TomeOfTongues.Content.Schema;

namespace TomeOfTongues.Content.Tool;

public static class TotlangPackageTool
{
    public static void Compile(string sourceDirectory, string destinationPath) =>
        TotlangPackageArchive.Compile(sourceDirectory, destinationPath);

    public static void Validate(string packagePath) =>
        TotlangPackageArchive.Validate(packagePath);

    public static TotlangManifest ReadManifest(string packagePath) =>
        TotlangPackageArchive.ReadManifest(packagePath);
}
