using System.Text.Json;
using System.Text.Json.Serialization;

namespace TomeOfTongues.Content.Schema;

public static class TotlangSchema
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static string Serialize<TDocument>(TDocument document)
        where TDocument : class, ITotlangDocument
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateIntrinsicContract(document);
        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    public static TDocument Deserialize<TDocument>(string json)
        where TDocument : class, ITotlangDocument
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        EnsureSupportedSchemaVersion(json);

        try
        {
            var document = JsonSerializer.Deserialize<TDocument>(json, SerializerOptions)
                ?? throw new TotlangSchemaException("The document must contain a JSON object.");
            ValidateIntrinsicContract(document);
            return document;
        }
        catch (JsonException exception)
        {
            throw new TotlangSchemaException("The document does not match the .totlang schema.", exception);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        return options;
    }

    private static void EnsureSupportedSchemaVersion(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object
                || !document.RootElement.TryGetProperty("schemaVersion", out var versionElement)
                || versionElement.ValueKind is not JsonValueKind.Number
                || !versionElement.TryGetInt32(out var version))
            {
                throw new TotlangSchemaException(
                    "The document must declare an integer schemaVersion.");
            }

            if (version != CurrentVersion)
            {
                throw new TotlangSchemaException(
                    $"Unsupported .totlang schema version {version}. Expected {CurrentVersion}.");
            }
        }
        catch (JsonException exception)
        {
            throw new TotlangSchemaException("The document is not valid JSON.", exception);
        }
    }

    private static void ValidateIntrinsicContract(ITotlangDocument document)
    {
        if (document.SchemaVersion != CurrentVersion)
        {
            throw new TotlangSchemaException(
                $"Unsupported .totlang schema version {document.SchemaVersion}. Expected {CurrentVersion}.");
        }

        if (document is TotlangManifest manifest)
        {
            ValidateVersion(manifest.PackageVersion, "package version");
            ValidateVersion(manifest.MinimumEngineVersion, "minimum engine version");
            return;
        }

        if (document is not TotlangLesson lesson)
        {
            return;
        }

        foreach (var expression in lesson.Expressions)
        {
            foreach (var representation in expression.Representations)
            {
                foreach (var annotation in representation.Annotations)
                {
                    if (annotation.Start < 0
                        || annotation.Length <= 0
                        || annotation.Start > representation.Value.Length - annotation.Length)
                    {
                        throw new TotlangSchemaException(
                            $"Annotation '{annotation.Id}' is outside representation '{representation.Id}'.");
                    }
                }
            }
        }

        foreach (var step in lesson.Steps)
        {
            if (step.Kind is StepKind.OptionalSpeakingOpportunity
                && step.Progression is not StepProgression.DeferredAllowed)
            {
                throw new TotlangSchemaException(
                    $"Speaking step '{step.Id}' must allow deferral.");
            }

            if (step.Exercise?.ResponseModality is ResponseModality.Spoken
                && step.Progression is not StepProgression.DeferredAllowed)
            {
                throw new TotlangSchemaException(
                    $"Spoken exercise '{step.Exercise.Id}' must allow deferral.");
            }
        }
    }

    private static void ValidateVersion(string value, string description)
    {
        if (!Version.TryParse(value, out _))
        {
            throw new TotlangSchemaException(
                $"The manifest {description} '{value}' is not a valid numeric dotted version.");
        }
    }
}
