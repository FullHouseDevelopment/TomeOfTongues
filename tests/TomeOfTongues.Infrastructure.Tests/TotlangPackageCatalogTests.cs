using System.IO.Compression;
using NUnit.Framework;
using TomeOfTongues.Content.Schema;
using TomeOfTongues.Infrastructure.Packaging;

namespace TomeOfTongues.Infrastructure.Tests;

[TestFixture]
public sealed class TotlangPackageCatalogTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "TomeOfTongues.Infrastructure.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Install_is_discovered_by_a_new_catalog_instance()
    {
        var packagePath = CreatePackage("fixture.pack", "1.2.0", "1.0.0");
        var catalogRoot = Path.Combine(_temporaryDirectory, "installed");

        var installed = new TotlangPackageCatalog(catalogRoot, new Version(1, 1, 0))
            .Install(packagePath);
        var discovered = new TotlangPackageCatalog(catalogRoot, new Version(1, 1, 0))
            .Discover();

        Assert.Multiple(() =>
        {
            Assert.That(discovered, Has.Count.EqualTo(1));
            Assert.That(discovered[0].PackId, Is.EqualTo("fixture.pack"));
            Assert.That(discovered[0].PackageVersion, Is.EqualTo("1.2.0"));
            Assert.That(discovered[0].LanguageTag, Is.EqualTo("he"));
            Assert.That(discovered[0].PackagePath, Is.EqualTo(installed.PackagePath));
            Assert.That(File.Exists(discovered[0].PackagePath), Is.True);
            Assert.That(discovered[0].PackagePath, Is.Not.EqualTo(packagePath));
        });
    }

    [Test]
    public void Failed_reinstall_does_not_replace_the_installed_package()
    {
        var packagePath = CreatePackage("fixture.pack", "1.0.0", "1.0.0");
        var catalog = new TotlangPackageCatalog(
            Path.Combine(_temporaryDirectory, "installed"),
            new Version(1, 0, 0));
        var installed = catalog.Install(packagePath);
        var installedBytes = File.ReadAllBytes(installed.PackagePath);

        using (var stream = File.Open(packagePath, FileMode.Open, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            archive.CreateEntry("payload.dll");
        }

        Assert.That(
            () => catalog.Install(packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("Executable package entry"));
        Assert.That(File.ReadAllBytes(installed.PackagePath), Is.EqualTo(installedBytes));
        Assert.That(catalog.Discover(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Install_rejects_a_pack_that_requires_a_newer_engine()
    {
        var packagePath = CreatePackage("fixture.pack", "1.0.0", "2.0.0");
        var catalogRoot = Path.Combine(_temporaryDirectory, "installed");
        var catalog = new TotlangPackageCatalog(catalogRoot, new Version(1, 9, 0));

        Assert.That(
            () => catalog.Install(packagePath),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("requires engine version"));
        Assert.That(
            Directory.Exists(catalogRoot)
                ? Directory.EnumerateFiles(catalogRoot, "*.totlang").ToArray()
                : [],
            Is.Empty);
    }

    [Test]
    public void Install_accepts_a_pack_that_matches_the_engine_version()
    {
        var packagePath = CreatePackage("fixture.pack", "1.0.0", "1.2.3");
        var catalog = new TotlangPackageCatalog(
            Path.Combine(_temporaryDirectory, "installed"),
            new Version(1, 2, 3));

        var installed = catalog.Install(packagePath);

        Assert.That(installed.MinimumEngineVersion, Is.EqualTo("1.2.3"));
    }

    [Test]
    public void Discover_rechecks_compatibility_after_an_engine_downgrade()
    {
        var packagePath = CreatePackage("fixture.pack", "1.0.0", "2.0.0");
        var catalogRoot = Path.Combine(_temporaryDirectory, "installed");
        new TotlangPackageCatalog(catalogRoot, new Version(2, 0, 0))
            .Install(packagePath);

        Assert.That(
            () => new TotlangPackageCatalog(catalogRoot, new Version(1, 9, 0))
                .Discover(),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("requires engine version"));
    }

    [Test]
    public void Discover_orders_side_by_side_versions_numerically_after_restart()
    {
        var catalogRoot = Path.Combine(_temporaryDirectory, "installed");
        var catalog = new TotlangPackageCatalog(catalogRoot, new Version(1, 0, 0));
        catalog.Install(CreatePackage("fixture.pack", "1.10.0", "1.0.0"));
        catalog.Install(CreatePackage("fixture.pack", "1.2.0", "1.0.0"));

        var discovered = new TotlangPackageCatalog(catalogRoot, new Version(1, 0, 0))
            .Discover();

        Assert.That(
            discovered.Select(pack => pack.PackageVersion),
            Is.EqualTo(new[] { "1.2.0", "1.10.0" }));
    }

    [Test]
    public void Install_keeps_manifest_identity_out_of_the_filesystem_path()
    {
        var packagePath = CreatePackage("../outside", "1.0.0", "1.0.0");
        var catalogRoot = Path.Combine(_temporaryDirectory, "installed");

        var installed = new TotlangPackageCatalog(catalogRoot, new Version(1, 0, 0))
            .Install(packagePath);

        Assert.Multiple(() =>
        {
            Assert.That(Path.GetDirectoryName(installed.PackagePath), Is.EqualTo(catalogRoot));
            Assert.That(Path.GetFileName(installed.PackagePath), Does.Not.Contain("outside"));
            Assert.That(File.Exists(Path.Combine(_temporaryDirectory, "outside")), Is.False);
        });
    }

    private string CreatePackage(
        string packId,
        string packageVersion,
        string minimumEngineVersion)
    {
        var packagePath = Path.Combine(
            _temporaryDirectory,
            Guid.NewGuid().ToString("N") + ".totlang");
        var manifest = new TotlangManifest
        {
            SchemaVersion = TotlangSchema.LegacyVersion,
            PackId = packId,
            PackageVersion = packageVersion,
            MinimumEngineVersion = minimumEngineVersion,
            LanguageTag = "he",
            LocaleTags = ["he-IL"],
            DisplayNames =
            [
                new LocalizedText
                {
                    LanguageTag = "en",
                    Value = "Fixture pack"
                }
            ],
            Representations = [],
            NormalizationOperations = [],
            CourseIds = [],
            Assets = [],
            Sources = [],
            Licenses = []
        };
        var courses = new TotlangCourseCatalog
        {
            SchemaVersion = TotlangSchema.LegacyVersion,
            Courses = []
        };

        using var stream = File.Create(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        WriteDocument(archive, "manifest.json", TotlangSchema.Serialize(manifest));
        WriteDocument(archive, "courses.json", TotlangSchema.Serialize(courses));
        return packagePath;
    }

    private static void WriteDocument(ZipArchive archive, string path, string json)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(json);
    }
}
