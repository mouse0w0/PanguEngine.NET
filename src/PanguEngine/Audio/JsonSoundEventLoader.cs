using System.Text.Json;
using System.Text.Json.Serialization;
using PanguEngine.Registries;
using PanguEngine.Resources;

namespace PanguEngine.Audio;

internal sealed class JsonSoundEventLoader(ResourceManager resources)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal IReadOnlyList<SoundVariant> Load(ResourceKey eventKey)
    {
        var resourceKey = GetDefinitionKey(eventKey);
        JsonSoundEvent definition;
        try
        {
            definition = JsonSerializer.Deserialize<JsonSoundEvent>(
                             resources.ReadAllText(resourceKey), JsonOptions)
                         ?? throw InvalidDefinition(eventKey, resourceKey, "The definition is empty.");
        }
        catch (JsonException exception)
        {
            throw InvalidDefinition(eventKey, resourceKey, "The JSON could not be parsed.", exception);
        }

        if (definition.Variants.ValueKind != JsonValueKind.Array)
            throw InvalidDefinition(eventKey, resourceKey, "Property 'variants' must be an array.");

        var variants = new List<SoundVariant>();
        var index = 0;
        foreach (var value in definition.Variants.EnumerateArray())
        {
            variants.Add(ParseVariant(value, eventKey, resourceKey, index));
            index++;
        }

        if (variants.Count == 0)
            throw InvalidDefinition(eventKey, resourceKey, "Property 'variants' must not be empty.");
        return variants;
    }

    internal static ResourceKey GetDefinitionKey(ResourceKey eventKey) =>
        ResourceKey.Create(eventKey.Namespace, $"sound_events/{eventKey.Path}.json");

    private static SoundVariant ParseVariant(
        JsonElement value,
        ResourceKey eventKey,
        ResourceKey definitionKey,
        int index)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var resource = ParseResource(value.GetString(), eventKey, definitionKey, index);
            return new SoundVariant(resource, 1, 1, 1, 1, 1);
        }

        if (value.ValueKind != JsonValueKind.Object)
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} must be a string or object.");

        JsonSoundVariant definition;
        try
        {
            definition = value.Deserialize<JsonSoundVariant>(JsonOptions)
                         ?? throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} is empty.");
        }
        catch (JsonException exception)
        {
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} could not be parsed.", exception);
        }

        var resourceKey = ParseResource(definition.Resource, eventKey, definitionKey, index);
        if (definition.Weight <= 0)
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} weight must be positive.");
        var volume = ParseRange(definition.Volume, allowZero: true, eventKey, definitionKey, index, "volume");
        var pitch = ParseRange(definition.Pitch, allowZero: false, eventKey, definitionKey, index, "pitch");
        return new SoundVariant(
            resourceKey,
            definition.Weight,
            volume.Minimum,
            volume.Maximum,
            pitch.Minimum,
            pitch.Maximum);
    }

    private static ResourceKey ParseResource(
        string? value,
        ResourceKey eventKey,
        ResourceKey definitionKey,
        int index)
    {
        if (string.IsNullOrEmpty(value))
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} resource must be a non-empty string.");

        ResourceKey resourceKey;
        try
        {
            resourceKey = value.Contains(':')
                ? ResourceKey.Parse(value)
                : ResourceKey.Create(eventKey.Namespace, value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} resource '{value}' is invalid.", exception);
        }

        if (!resourceKey.Path.EndsWith(".ogg", StringComparison.Ordinal))
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} resource must use the lowercase .ogg extension.");
        return resourceKey;
    }

    private static (float Minimum, float Maximum) ParseRange(
        JsonElement value,
        bool allowZero,
        ResourceKey eventKey,
        ResourceKey definitionKey,
        int index,
        string propertyName)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
            return (1, 1);

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var fixedValue))
        {
            ValidateRangeValue(fixedValue, allowZero, eventKey, definitionKey, index, propertyName);
            return (fixedValue, fixedValue);
        }

        if (value.ValueKind != JsonValueKind.Array)
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} {propertyName} must be a number or two-element array.");

        var values = value.EnumerateArray().ToArray();
        if (values.Length != 2
            || values[0].ValueKind != JsonValueKind.Number
            || values[1].ValueKind != JsonValueKind.Number
            || !values[0].TryGetSingle(out var minimum)
            || !values[1].TryGetSingle(out var maximum))
        {
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} {propertyName} must contain two numbers.");
        }

        ValidateRangeValue(minimum, allowZero, eventKey, definitionKey, index, propertyName);
        ValidateRangeValue(maximum, allowZero, eventKey, definitionKey, index, propertyName);
        if (minimum > maximum)
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} {propertyName} range must be ordered.");
        return (minimum, maximum);
    }

    private static void ValidateRangeValue(
        float value,
        bool allowZero,
        ResourceKey eventKey,
        ResourceKey definitionKey,
        int index,
        string propertyName)
    {
        if (!float.IsFinite(value) || (allowZero ? value < 0 : value <= 0))
            throw InvalidDefinition(eventKey, definitionKey, $"Variant {index} {propertyName} contains an invalid value.");
    }

    private static InvalidDataException InvalidDefinition(
        ResourceKey eventKey,
        ResourceKey definitionKey,
        string detail,
        Exception? innerException = null) =>
        new($"Sound event '{eventKey}' definition '{definitionKey}' is invalid. {detail}", innerException);
}
