using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.World.Blocks;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockAppearanceLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly ResourceManager _resources;

    internal JsonBlockAppearanceLoader(ResourceManager resources)
    {
        _resources = resources;
    }

    internal UnbakedBlockAppearance Load(ResourceKey blockKey, Block block)
    {
        var appearanceKey = ResourceKey.Create(
            blockKey.Namespace,
            $"appearances/block/{blockKey.Path}");
        var resourceKey = ResourceKey.Create(
            appearanceKey.Namespace,
            $"{appearanceKey.Path}.json");
        JsonBlockAppearance definition;
        try
        {
            definition = JsonSerializer.Deserialize<JsonBlockAppearance>(
                             _resources.ReadAllText(resourceKey), JsonOptions)
                         ?? throw new InvalidDataException(
                             $"Block appearance '{appearanceKey}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Failed to parse block appearance '{appearanceKey}'.", exception);
        }

        if (definition.Variants.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variants must be an object.");

        var stateLookup = CreateStateLookup(block, appearanceKey);
        var exactVariants = new Dictionary<BlockState, IReadOnlyList<UnbakedBlockAppearanceEntry>>();
        IReadOnlyList<UnbakedBlockAppearanceEntry>? fallback = null;
        var variantKeys = new HashSet<string>(StringComparer.Ordinal);
        var hasVariants = false;
        foreach (var variant in definition.Variants.EnumerateObject())
        {
            hasVariants = true;
            if (!variantKeys.Add(variant.Name))
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' contains duplicate variant key '{variant.Name}'.");

            var candidates = ParseCandidates(variant.Value, appearanceKey, variant.Name);
            if (variant.Name.Length == 0 && block.StateDefinition.Properties.Count > 0)
            {
                fallback = candidates;
                continue;
            }

            if (!stateLookup.TryGetValue(variant.Name, out var state))
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant key '{variant.Name}' is not a complete canonical state key.");
            exactVariants.Add(state, candidates);
        }

        if (!hasVariants)
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variants must contain at least one entry.");

        var variants = new Dictionary<BlockState, IReadOnlyList<UnbakedBlockAppearanceEntry>>();
        var states = block.StateDefinition.States;
        for (var stateIndex = 0; stateIndex < states.Count; stateIndex++)
        {
            var state = states[stateIndex];
            if (exactVariants.TryGetValue(state, out var candidates))
            {
                variants.Add(state, candidates);
                continue;
            }

            if (fallback is null)
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' does not cover state '{GetStateKey(block.StateDefinition, stateIndex)}'.");
            variants.Add(state, fallback);
        }

        return new UnbakedBlockAppearance(appearanceKey, variants);
    }

    private static Dictionary<string, BlockState> CreateStateLookup(
        Block block,
        ResourceKey appearanceKey)
    {
        var definition = block.StateDefinition;
        var result = new Dictionary<string, BlockState>(StringComparer.Ordinal);
        for (var stateIndex = 0; stateIndex < definition.States.Count; stateIndex++)
        {
            var key = GetStateKey(definition, stateIndex);
            if (!result.TryAdd(key, definition.States[stateIndex]))
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' cannot distinguish duplicate canonical state key '{key}'.");
        }

        return result;
    }

    private static string GetStateKey(BlockStateDefinition definition, int stateIndex)
    {
        var properties = new string[definition.Properties.Count];
        for (var propertyIndex = 0; propertyIndex < definition.Properties.Count; propertyIndex++)
        {
            var property = definition.Properties[propertyIndex];
            var valueIndex = stateIndex / definition.Strides[propertyIndex] % property.ValueCount;
            properties[propertyIndex] = $"{property.Name}={property.GetValueString(valueIndex)}";
        }

        return string.Join(",", properties);
    }

    private static List<UnbakedBlockAppearanceEntry> ParseCandidates(
        JsonElement value,
        ResourceKey appearanceKey,
        string variantKey)
    {
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' must be a model object or an array.");

        JsonBlockAppearanceCandidate?[] candidates;
        try
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                candidates =
                [
                    JsonSerializer.Deserialize<JsonBlockAppearanceCandidate>(
                        value.GetRawText(), JsonOptions)
                    ?? throw new InvalidDataException(
                        $"Block appearance '{appearanceKey}' variant '{variantKey}' is empty.")
                ];
            }
            else
            {
                candidates = JsonSerializer.Deserialize<JsonBlockAppearanceCandidate?[]>(
                                 value.GetRawText(), JsonOptions)
                             ?? throw new InvalidDataException(
                                 $"Block appearance '{appearanceKey}' variant '{variantKey}' is empty.");
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Failed to parse block appearance '{appearanceKey}' variant '{variantKey}'.", exception);
        }

        if (candidates.Length == 0)
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' must contain at least one model.");

        var models = new List<UnbakedBlockAppearanceEntry>(candidates.Length);
        long totalWeight = 0;
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index]
                            ?? throw new InvalidDataException(
                                $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} is null.");
            var modelReference = ParseModel(candidate.Model, appearanceKey, variantKey, index);
            var weight = ParseInteger(candidate.Weight, 1, appearanceKey, variantKey, index, "weight");
            if (weight <= 0)
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} weight must be positive.");
            totalWeight += weight;
            if (totalWeight > int.MaxValue)
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant '{variantKey}' total weight exceeds Int32.MaxValue.");

            models.Add(new UnbakedBlockAppearanceEntry(
                ResolveModelReference(modelReference, appearanceKey),
                weight,
                ParseRotation(candidate.Rotation, appearanceKey, variantKey, index)));
        }

        return models;
    }

    private static string ParseModel(
        JsonElement value,
        ResourceKey appearanceKey,
        string variantKey,
        int index)
    {
        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrEmpty(value.GetString()))
        {
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} reference must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static BlockModelRotation ParseRotation(
        JsonElement value,
        ResourceKey appearanceKey,
        string variantKey,
        int index)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
            return default;
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} rotation must be an object.");

        JsonBlockAppearanceRotation rotation;
        try
        {
            rotation = JsonSerializer.Deserialize<JsonBlockAppearanceRotation>(
                           value.GetRawText(), JsonOptions)
                       ?? throw new InvalidDataException(
                           $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} rotation is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Failed to parse block appearance '{appearanceKey}' variant '{variantKey}' model {index} rotation.",
                exception);
        }

        return new BlockModelRotation(
            ParseAngle(rotation.X, appearanceKey, variantKey, index, "x"),
            ParseAngle(rotation.Y, appearanceKey, variantKey, index, "y"),
            ParseAngle(rotation.Z, appearanceKey, variantKey, index, "z"));
    }

    private static int ParseAngle(
        JsonElement value,
        ResourceKey appearanceKey,
        string variantKey,
        int index,
        string axis)
    {
        var angle = ParseInteger(value, 0, appearanceKey, variantKey, index, $"rotation.{axis}");
        if (angle is not (0 or 90 or 180 or 270))
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} rotation.{axis} must be 0, 90, 180, or 270.");
        return angle;
    }

    private static int ParseInteger(
        JsonElement value,
        int defaultValue,
        ResourceKey appearanceKey,
        string variantKey,
        int index,
        string propertyName)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
            return defaultValue;
        if (value.ValueKind != JsonValueKind.Number ||
            !int.TryParse(
                value.GetRawText(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var result))
        {
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} {propertyName} must be an integer.");
        }

        return result;
    }

    private static ResourceKey ResolveModelReference(
        string value,
        ResourceKey appearanceKey)
    {
        return value.Contains(':')
            ? ResourceKey.Parse(value)
            : ResourceKey.Create(appearanceKey.Namespace, value);
    }
}