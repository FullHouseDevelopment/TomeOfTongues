using System.Text.Json;
using System.Text.Json.Serialization;

namespace TomeOfTongues.Content.Schema;

public static class TotlangSchema
{
    public const int LegacyVersion = 1;
    public const int CurrentVersion = 2;

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

            if (version is not LegacyVersion and not CurrentVersion)
            {
                throw new TotlangSchemaException(
                    $"Unsupported .totlang schema version {version}. Expected {LegacyVersion} or {CurrentVersion}.");
            }
        }
        catch (JsonException exception)
        {
            throw new TotlangSchemaException("The document is not valid JSON.", exception);
        }
    }

    private static void ValidateIntrinsicContract(ITotlangDocument document)
    {
        if (document.SchemaVersion is not LegacyVersion and not CurrentVersion)
        {
            throw new TotlangSchemaException(
                $"Unsupported .totlang schema version {document.SchemaVersion}. Expected {LegacyVersion} or {CurrentVersion}.");
        }

        if (document is TotlangManifest manifest)
        {
            ValidateVersion(manifest.PackageVersion, "package version");
            ValidateVersion(manifest.MinimumEngineVersion, "minimum engine version");
            return;
        }

        if (document is TotlangCourseCatalog catalog)
        {
            ValidateCourseCatalog(catalog);
            return;
        }

        if (document is TotlangProficiencyCatalog proficiencyCatalog)
        {
            if (proficiencyCatalog.SchemaVersion != CurrentVersion)
            {
                throw new TotlangSchemaException(
                    "proficiency.json is supported only by .totlang schema version 2.");
            }

            ValidateMilestones(proficiencyCatalog.Milestones, requireGlobalNamespace: false);
            return;
        }

        if (document is ProficiencyFrameworkDefinition framework)
        {
            ValidateFramework(framework);
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

    private static void ValidateCourseCatalog(TotlangCourseCatalog catalog)
    {
        foreach (var course in catalog.Courses)
        {
            if (catalog.SchemaVersion == LegacyVersion)
            {
                if (string.IsNullOrWhiteSpace(course.ProficiencyBand))
                {
                    throw new TotlangSchemaException(
                        $"Schema v1 course '{course.Id}' must declare proficiencyBand.");
                }

                if (course.Proficiency is not null)
                {
                    throw new TotlangSchemaException(
                        $"Schema v1 course '{course.Id}' must not declare structured proficiency.");
                }

                continue;
            }

            if (course.ProficiencyBand is not null)
            {
                throw new TotlangSchemaException(
                    $"Schema v2 course '{course.Id}' must not declare legacy proficiencyBand.");
            }

            if (course.Proficiency is null)
            {
                throw new TotlangSchemaException(
                    $"Schema v2 course '{course.Id}' must declare structured proficiency.");
            }

            ValidateCourseProficiency(course.Id, course.Proficiency);
        }
    }

    private static void ValidateCourseProficiency(
        string courseId,
        CourseProficiencyDefinition proficiency)
    {
        ValidateStageRange(
            proficiency.OverallEntryStage,
            proficiency.OverallExitStage,
            $"Course '{courseId}' overall proficiency");

        var facets = new HashSet<ProficiencyFacet>();
        foreach (var facet in proficiency.Facets)
        {
            if (!Enum.IsDefined(facet.Facet) || !facets.Add(facet.Facet))
            {
                throw new TotlangSchemaException(
                    $"Course '{courseId}' has an invalid or duplicate proficiency facet '{facet.Facet}'.");
            }

            ValidateStageRange(
                facet.EntryStage,
                facet.ExitStage,
                $"Course '{courseId}' facet '{facet.Facet}'");
        }

        ValidateUniqueValues(proficiency.MilestoneIds, $"Course '{courseId}' milestone reference");
        foreach (var milestoneId in proficiency.MilestoneIds)
        {
            ValidateNamespacedId(milestoneId, $"Course '{courseId}' milestone reference");
        }

        foreach (var alignment in proficiency.ExternalAlignments)
        {
            ValidateAlignment(courseId, alignment);
        }
    }

    private static void ValidateAlignment(
        string courseId,
        ExternalProficiencyAlignment alignment)
    {
        if (!Enum.IsDefined(alignment.Status))
        {
            throw new TotlangSchemaException(
                $"Course '{courseId}' has invalid external alignment status '{alignment.Status}'.");
        }

        if (!TomeOfTonguesProficiencyFramework.TryGetExternalReferenceOrder(
                alignment.FrameworkId,
                alignment.LowerReferenceLevel,
                out var lowerOrder)
            || !TomeOfTonguesProficiencyFramework.TryGetExternalReferenceOrder(
                alignment.FrameworkId,
                alignment.UpperReferenceLevel,
                out var upperOrder))
        {
            throw new TotlangSchemaException(
                $"Course '{courseId}' has an unsupported external alignment range for framework '{alignment.FrameworkId}'.");
        }

        if (lowerOrder > upperOrder)
        {
            throw new TotlangSchemaException(
                $"Course '{courseId}' external alignment range is reversed.");
        }

        if (alignment.Facets.Count == 0
            || alignment.Facets.Any(facet => !Enum.IsDefined(facet))
            || alignment.Facets.Distinct().Count() != alignment.Facets.Count)
        {
            throw new TotlangSchemaException(
                $"Course '{courseId}' external alignment must explicitly cover unique valid facets.");
        }

        if (!Uri.TryCreate(alignment.AuthoritativeSourceUri, UriKind.Absolute, out var sourceUri)
            || sourceUri.Scheme is not ("http" or "https"))
        {
            throw new TotlangSchemaException(
                $"Course '{courseId}' external alignment requires an authoritative HTTP(S) source URI.");
        }

        if (alignment.Status is ExternalAlignmentStatus.Reviewed
                or ExternalAlignmentStatus.Validated
            && alignment.ReviewDate is null)
        {
            throw new TotlangSchemaException(
                $"Course '{courseId}' external alignment status '{alignment.Status}' requires a review date.");
        }
    }

    private static void ValidateFramework(ProficiencyFrameworkDefinition framework)
    {
        if (framework.SchemaVersion != CurrentVersion
            || !framework.FrameworkId.Equals("tomeoftongues", StringComparison.Ordinal)
            || framework.FrameworkVersion != TomeOfTonguesProficiencyFramework.FrameworkVersion)
        {
            throw new TotlangSchemaException(
                "The TomeOfTongues proficiency framework identity or version is invalid.");
        }

        if (framework.Stages.Count != TomeOfTonguesProficiencyFramework.StageIds.Count)
        {
            throw new TotlangSchemaException(
                "The TomeOfTongues proficiency framework must define exactly 18 stages.");
        }

        for (var index = 0; index < framework.Stages.Count; index++)
        {
            var stage = framework.Stages[index];
            if (!stage.Id.Equals(
                    TomeOfTonguesProficiencyFramework.StageIds[index],
                    StringComparison.Ordinal)
                || stage.Order != index + 1)
            {
                throw new TotlangSchemaException(
                    "The TomeOfTongues proficiency framework stages must remain ordered T01 through T18.");
            }

            ValidateLocalizedValues(stage.DisplayNames, $"Stage '{stage.Id}' display name");
            ValidateLocalizedValues(stage.CanDoDescriptors, $"Stage '{stage.Id}' can-do descriptor");
        }

        ValidateMilestones(framework.GlobalMilestones, requireGlobalNamespace: true);
        var globalIds = framework.GlobalMilestones
            .Select(milestone => milestone.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (!globalIds.SetEquals(TomeOfTonguesProficiencyFramework.GlobalMilestoneIds))
        {
            throw new TotlangSchemaException(
                "The global proficiency milestone set does not match framework version 1.");
        }

        var frameworkIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var externalFramework in framework.ExternalFrameworks)
        {
            if (string.IsNullOrWhiteSpace(externalFramework.Id)
                || !frameworkIds.Add(externalFramework.Id)
                || externalFramework.OrderedReferenceLevels.Count == 0)
            {
                throw new TotlangSchemaException(
                    "External framework definitions require unique IDs and ordered reference levels.");
            }

            ValidateUniqueValues(
                externalFramework.OrderedReferenceLevels,
                $"External framework '{externalFramework.Id}' reference level");

            foreach (var referenceLevel in externalFramework.OrderedReferenceLevels)
            {
                if (!TomeOfTonguesProficiencyFramework.TryGetExternalReferenceOrder(
                        externalFramework.Id,
                        referenceLevel,
                        out _))
                {
                    throw new TotlangSchemaException(
                        $"External framework definition '{externalFramework.Id}' is unsupported.");
                }
            }
        }
    }

    private static void ValidateMilestones(
        IReadOnlyList<ProficiencyMilestoneDefinition> milestones,
        bool requireGlobalNamespace)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var milestone in milestones)
        {
            ValidateNamespacedId(milestone.Id, "Milestone");
            if (!ids.Add(milestone.Id))
            {
                throw new TotlangSchemaException(
                    $"Milestone ID '{milestone.Id}' is duplicated.");
            }

            if (requireGlobalNamespace
                && !milestone.Id.StartsWith("global:", StringComparison.Ordinal))
            {
                throw new TotlangSchemaException(
                    $"Global milestone '{milestone.Id}' must use the 'global:' namespace.");
            }

            if (!TomeOfTonguesProficiencyFramework.TryGetStageOrder(
                    milestone.AnchorStage,
                    out _))
            {
                throw new TotlangSchemaException(
                    $"Milestone '{milestone.Id}' has unknown anchor stage '{milestone.AnchorStage}'.");
            }

            if (milestone.WithinStageOrder < 1)
            {
                throw new TotlangSchemaException(
                    $"Milestone '{milestone.Id}' must have a positive within-stage order.");
            }

            ValidateLocalizedValues(
                milestone.DisplayNames,
                $"Milestone '{milestone.Id}' display name");
            ValidateLocalizedValues(
                milestone.CanDoDescriptors,
                $"Milestone '{milestone.Id}' can-do descriptor");
        }
    }

    private static void ValidateStageRange(
        string entryStage,
        string exitStage,
        string description)
    {
        if (!TomeOfTonguesProficiencyFramework.TryGetStageOrder(entryStage, out var entryOrder)
            || !TomeOfTonguesProficiencyFramework.TryGetStageOrder(exitStage, out var exitOrder))
        {
            throw new TotlangSchemaException(
                $"{description} contains an unknown TomeOfTongues stage.");
        }

        if (entryOrder > exitOrder)
        {
            throw new TotlangSchemaException($"{description} range is reversed.");
        }
    }

    private static void ValidateNamespacedId(string id, string description)
    {
        if (string.IsNullOrWhiteSpace(id)
            || id.Count(character => character == ':') != 1
            || id.StartsWith(':')
            || id.EndsWith(':')
            || id.Any(character =>
                !(char.IsAsciiLetterOrDigit(character)
                    || character is '.' or '_' or '-' or ':')))
        {
            throw new TotlangSchemaException(
                $"{description} ID '{id}' must be a stable namespaced ID.");
        }
    }

    private static void ValidateLocalizedValues(
        IReadOnlyList<LocalizedText> values,
        string description)
    {
        if (values.Count == 0
            || values.Any(value =>
                string.IsNullOrWhiteSpace(value.LanguageTag)
                || string.IsNullOrWhiteSpace(value.Value)))
        {
            throw new TotlangSchemaException(
                $"{description} must contain localized nonempty text.");
        }
    }

    private static void ValidateUniqueValues(
        IReadOnlyList<string> values,
        string description)
    {
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new TotlangSchemaException(
                $"{description} values must be nonempty and unique.");
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
