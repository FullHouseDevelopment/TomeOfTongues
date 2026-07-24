using System.Security.Cryptography;
using System.Text;
using TomeOfTongues.Content.Packaging;
using TomeOfTongues.Content.Schema;

namespace TomeOfTongues.Infrastructure.Packaging;

public sealed class TotlangPackageCatalog
{
    private readonly string _catalogRoot;
    private readonly Version _engineVersion;

    public TotlangPackageCatalog(string catalogRoot, Version engineVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogRoot);
        ArgumentNullException.ThrowIfNull(engineVersion);

        _catalogRoot = Path.GetFullPath(catalogRoot);
        _engineVersion = engineVersion;
    }

    public InstalledLanguagePack Install(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var sourcePath = Path.GetFullPath(packagePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The .totlang package does not exist.", sourcePath);
        }

        Directory.CreateDirectory(_catalogRoot);
        var temporaryPath = Path.Combine(
            _catalogRoot,
            $".install-{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(sourcePath, temporaryPath);
            var manifest = TotlangPackageArchive.ReadManifest(temporaryPath);
            EnsureCompatible(manifest);

            var destinationPath = Path.Combine(_catalogRoot, GetInstalledFileName(manifest));
            File.Move(temporaryPath, destinationPath, overwrite: true);
            return CreateDescriptor(manifest, destinationPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public IReadOnlyList<InstalledLanguagePack> Discover()
    {
        if (!Directory.Exists(_catalogRoot))
        {
            return [];
        }

        var discovered = new Dictionary<string, InstalledLanguagePack>(StringComparer.Ordinal);
        foreach (var packagePath in Directory.EnumerateFiles(
                     _catalogRoot,
                     "*.totlang",
                     SearchOption.TopDirectoryOnly))
        {
            var manifest = TotlangPackageArchive.ReadManifest(packagePath);
            EnsureCompatible(manifest);

            var key = manifest.PackId + "\n" + manifest.PackageVersion;
            if (!discovered.TryAdd(key, CreateDescriptor(manifest, packagePath)))
            {
                throw new InvalidDataException(
                    $"Language pack '{manifest.PackId}' version '{manifest.PackageVersion}' is installed more than once.");
            }
        }

        return discovered.Values
            .OrderBy(pack => pack.PackId, StringComparer.Ordinal)
            .ThenBy(pack => pack.PackageVersion, StringComparer.Ordinal)
            .ToArray();
    }

    private void EnsureCompatible(TotlangManifest manifest)
    {
        if (!Version.TryParse(manifest.MinimumEngineVersion, out var minimumEngineVersion))
        {
            throw new InvalidDataException(
                $"Language pack '{manifest.PackId}' declares invalid minimum engine version '{manifest.MinimumEngineVersion}'.");
        }

        if (minimumEngineVersion > _engineVersion)
        {
            throw new InvalidDataException(
                $"Language pack '{manifest.PackId}' requires engine version {minimumEngineVersion} or later; the current version is {_engineVersion}.");
        }
    }

    private static string GetInstalledFileName(TotlangManifest manifest)
    {
        var identity = Encoding.UTF8.GetBytes(
            manifest.PackId + "\n" + manifest.PackageVersion);
        return Convert.ToHexStringLower(SHA256.HashData(identity)) + ".totlang";
    }

    private static InstalledLanguagePack CreateDescriptor(
        TotlangManifest manifest,
        string packagePath) =>
        new()
        {
            PackId = manifest.PackId,
            PackageVersion = manifest.PackageVersion,
            MinimumEngineVersion = manifest.MinimumEngineVersion,
            LanguageTag = manifest.LanguageTag,
            LocaleTags = manifest.LocaleTags,
            DisplayNames = manifest.DisplayNames,
            PackagePath = Path.GetFullPath(packagePath)
        };
}
