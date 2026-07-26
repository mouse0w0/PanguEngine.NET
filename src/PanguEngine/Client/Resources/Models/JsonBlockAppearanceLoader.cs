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

    internal UnresolvedBlockAppearance Load(ResourceKey appearanceKey)
    {
        var resourceKey = ResourceKey.Create(
            appearanceKey.Namespace,
            $"appearances/{appearanceKey.Path}.json");
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

        if (definition.Parent is not null && definition.Parent.Length == 0)
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' parent reference must be a non-empty string.");

        var variants = definition.Variants.ValueKind switch
        {
            JsonValueKind.Undefined => null,
            JsonValueKind.Object => ParseVariants(definition.Variants, appearanceKey),
            JsonValueKind.Null => throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variants cannot be null."),
            _ => throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variants must be an object.")
        };

        return new UnresolvedBlockAppearance(
            appearanceKey,
            definition.Parent,
            ParseModels(definition.Models, appearanceKey),
            variants);
    }

    internal IReadOnlyDictionary<BlockState, IReadOnlyList<UnresolvedBlockAppearanceEntry>>
        ExpandVariants(
            ResourceKey blockKey,
            Block block,
            ResourceKey sourceKey,
            IReadOnlyDictionary<
                string,
                IReadOnlyList<UnresolvedBlockAppearanceEntry>> variantDefinitions)
    {
        var stateDefinition = block.StateDefinition;
        var exactVariants = new Dictionary<
            BlockState,
            IReadOnlyList<UnresolvedBlockAppearanceEntry>>();
        var variantSources = new Dictionary<BlockState, string>();
        IReadOnlyList<UnresolvedBlockAppearanceEntry>? fallback = null;
        foreach (var variant in variantDefinitions)
        {
            if (variant.Key.Length == 0)
            {
                fallback = variant.Value;
                continue;
            }

            var conditions = ParseStateConditions(
                stateDefinition,
                sourceKey,
                blockKey,
                variant.Key);
            for (var stateIndex = 0; stateIndex < stateDefinition.States.Count; stateIndex++)
            {
                if (!MatchesState(stateDefinition, stateIndex, conditions))
                    continue;

                var state = stateDefinition.States[stateIndex];
                if (!exactVariants.TryAdd(state, variant.Value))
                {
                    throw new InvalidDataException(
                        $"Block appearance '{sourceKey}' variant key '{variant.Key}' for block '{blockKey}' overlaps variant key '{variantSources[state]}' at state '{GetStateKey(stateDefinition, stateIndex)}'.");
                }

                variantSources.Add(state, variant.Key);
            }
        }

        var expandedVariants = new Dictionary<
            BlockState,
            IReadOnlyList<UnresolvedBlockAppearanceEntry>>();
        var states = stateDefinition.States;
        for (var stateIndex = 0; stateIndex < states.Count; stateIndex++)
        {
            var state = states[stateIndex];
            if (exactVariants.TryGetValue(state, out var candidates))
            {
                expandedVariants.Add(state, candidates);
                continue;
            }

            if (fallback is null)
                throw new InvalidDataException(
                    $"Block appearance '{sourceKey}' for block '{blockKey}' does not cover state '{GetStateKey(stateDefinition, stateIndex)}'.");
            expandedVariants.Add(state, fallback);
        }

        return expandedVariants;
    }

    private static IReadOnlyDictionary<
        string,
        IReadOnlyList<UnresolvedBlockAppearanceEntry>> ParseVariants(
        JsonElement value,
        ResourceKey appearanceKey)
    {
        var variants = new Dictionary<
            string,
            IReadOnlyList<UnresolvedBlockAppearanceEntry>>(StringComparer.Ordinal);
        var variantKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in value.EnumerateObject())
        {
            if (!variantKeys.Add(variant.Name))
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' contains duplicate variant key '{variant.Name}'.");
            variants.Add(
                variant.Name,
                ParseEntries(variant.Value, appearanceKey, variant.Name));
        }

        if (variants.Count == 0)
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variants must contain at least one entry.");

        return variants;
    }

    private static Dictionary<string, BlockModelValue> ParseModels(
        Dictionary<string, string?>? values,
        ResourceKey appearanceKey)
    {
        var models = new Dictionary<string, BlockModelValue>(StringComparer.Ordinal);
        if (values is null)
            return models;

        foreach (var (name, value) in values)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' contains an empty model alias entry.");
            models.Add(name, ParseModelValue(value, appearanceKey));
        }

        return models;
    }

    private static (int PropertyIndex, int ValueIndex)[] ParseStateConditions(
        BlockStateDefinition definition,
        ResourceKey appearanceKey,
        ResourceKey blockKey,
        string variantKey)
    {
        var parts = variantKey.Split(',');
        var conditions = new (int PropertyIndex, int ValueIndex)[parts.Length];
        var propertyIndexes = new HashSet<int>();
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0 ||
                separatorIndex != part.LastIndexOf('=') ||
                separatorIndex == part.Length - 1)
            {
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant key '{variantKey}' for block '{blockKey}' contains invalid condition '{part}'.");
            }

            var propertyName = part[..separatorIndex];
            var value = part[(separatorIndex + 1)..];
            var propertyIndex = definition.GetPropertyIndex(propertyName);
            if (propertyIndex < 0)
            {
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant key '{variantKey}' for block '{blockKey}' contains unknown property '{propertyName}'.");
            }

            if (!propertyIndexes.Add(propertyIndex))
            {
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant key '{variantKey}' for block '{blockKey}' repeats property '{propertyName}'.");
            }

            var property = definition.Properties[propertyIndex];
            var valueIndex = property.GetValueIndex(value);
            if (valueIndex < 0)
            {
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant key '{variantKey}' for block '{blockKey}' contains unknown value '{value}' for property '{propertyName}'.");
            }

            conditions[index] = (propertyIndex, valueIndex);
        }

        return conditions;
    }

    private static bool MatchesState(
        BlockStateDefinition definition,
        int stateIndex,
        IReadOnlyList<(int PropertyIndex, int ValueIndex)> conditions)
    {
        foreach (var condition in conditions)
        {
            var property = definition.Properties[condition.PropertyIndex];
            var valueIndex = stateIndex / definition.Strides[condition.PropertyIndex] % property.ValueCount;
            if (valueIndex != condition.ValueIndex)
                return false;
        }

        return true;
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

    private static List<UnresolvedBlockAppearanceEntry> ParseEntries(
        JsonElement value,
        ResourceKey appearanceKey,
        string variantKey)
    {
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' must be a model object or an array.");

        JsonBlockAppearanceEntry?[] entries;
        try
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                entries =
                [
                    JsonSerializer.Deserialize<JsonBlockAppearanceEntry>(
                        value.GetRawText(), JsonOptions)
                    ?? throw new InvalidDataException(
                        $"Block appearance '{appearanceKey}' variant '{variantKey}' is empty.")
                ];
            }
            else
            {
                entries = JsonSerializer.Deserialize<JsonBlockAppearanceEntry?[]>(
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

        if (entries.Length == 0)
            throw new InvalidDataException(
                $"Block appearance '{appearanceKey}' variant '{variantKey}' must contain at least one model.");

        var result = new List<UnresolvedBlockAppearanceEntry>(entries.Length);
        long totalWeight = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index]
                        ?? throw new InvalidDataException(
                            $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} is null.");
            var modelValue = ParseModel(entry.Model, appearanceKey, variantKey, index);
            var weight = ParseInteger(entry.Weight, 1, appearanceKey, variantKey, index, "weight");
            if (weight <= 0)
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant '{variantKey}' model {index} weight must be positive.");
            totalWeight += weight;
            if (totalWeight > int.MaxValue)
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' variant '{variantKey}' total weight exceeds Int32.MaxValue.");

            result.Add(new UnresolvedBlockAppearanceEntry(
                ParseModelValue(modelValue, appearanceKey),
                weight,
                ParseRotation(entry.Rotation, appearanceKey, variantKey, index)));
        }

        return result;
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

    private static BlockModelValue ParseModelValue(
        string value,
        ResourceKey appearanceKey)
    {
        if (value.StartsWith('#'))
        {
            var name = value[1..];
            if (name.Length == 0)
                throw new InvalidDataException(
                    $"Block appearance '{appearanceKey}' contains an empty model alias reference.");
            return new BlockModelValue.Variable(name);
        }

        var key = value.Contains(':')
            ? ResourceKey.Parse(value)
            : ResourceKey.Create(appearanceKey.Namespace, value);
        return new BlockModelValue.Resource(key);
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
}