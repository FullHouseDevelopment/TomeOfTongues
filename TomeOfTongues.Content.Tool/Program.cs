using TomeOfTongues.Content.Tool;

return Run(args);

static int Run(string[] arguments)
{
    try
    {
        if (arguments is ["compile", var sourceDirectory, var destinationPath])
        {
            TotlangPackageTool.Compile(sourceDirectory, destinationPath);
            Console.WriteLine($"Compiled {Path.GetFullPath(destinationPath)}");
            return 0;
        }

        if (arguments is ["validate", var path])
        {
            Validate(path);
            return 0;
        }

        Console.Error.WriteLine(
            "Usage: TomeOfTongues.Content.Tool compile <source-directory> <destination.totlang>");
        Console.Error.WriteLine(
            "       TomeOfTongues.Content.Tool validate <package-or-directory>");
        return 2;
    }
    catch (Exception exception) when (
        exception is ArgumentException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

static void Validate(string path)
{
    var fullPath = Path.GetFullPath(path);
    if (File.Exists(fullPath))
    {
        TotlangPackageTool.Validate(fullPath);
        Console.WriteLine($"Valid: {fullPath}");
        return;
    }

    if (!Directory.Exists(fullPath))
    {
        throw new FileNotFoundException(
            "The package or artifact directory does not exist.",
            fullPath);
    }

    var packages = Directory
        .EnumerateFiles(fullPath, "*.totlang", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .ToArray();
    if (packages.Length == 0)
    {
        throw new InvalidDataException(
            $"Artifact directory '{fullPath}' contains no .totlang packages.");
    }

    foreach (var package in packages)
    {
        TotlangPackageTool.Validate(package);
        Console.WriteLine($"Valid: {package}");
    }
}
