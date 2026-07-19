using System.Text.Json;
using System.Text.Json.Serialization;
using PanguEngine.Registries;
using PanguEngine.Resources;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed class JsonBlockModelLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly HashSet<string> Directions =
        new(StringComparer.Ordinal)
        {
            "down", "up", "north", "south", "west", "east"
        };

    private readonly ResourceManager _resources;

    internal JsonBlockModelLoader(ResourceManager resources)
    {
        _resources = resources;
    }

    internal UnbakedBlockModel Load(ResourceKey modelKey)
    {
        var resourceKey = ResourceKey.Create(modelKey.Namespace, $"models/{modelKey.Path}.json");
        JsonBlockModel definition;
        try
        {
            definition = JsonSerializer.Deserialize<JsonBlockModel>(
                             _resources.ReadAllText(resourceKey), JsonOptions)
                         ?? throw new InvalidDataException($"Block model '{modelKey}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Failed to parse block model '{modelKey}'.", exception);
        }

        var textures = ParseTextures(definition.Textures, modelKey);
        var elements = definition.Elements is null
            ? null
            : ParseElements(definition.Elements, modelKey);

        return new UnbakedBlockModel(modelKey, definition.Parent, textures, elements);
    }

    private static Dictionary<string, BlockTextureValue> ParseTextures(
        Dictionary<string, string?>? values,
        ResourceKey modelKey)
    {
        var textures = new Dictionary<string, BlockTextureValue>(StringComparer.Ordinal);
        if (values is null)
            return textures;

        foreach (var (name, value) in values)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                throw new InvalidDataException($"Block model '{modelKey}' contains an empty texture entry.");

            textures.Add(name, ParseTextureValue(value, modelKey));
        }

        return textures;
    }

    private static List<UnbakedElement> ParseElements(
        JsonBlockModelElement?[] values,
        ResourceKey modelKey)
    {
        var elements = new List<UnbakedElement>(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            var element = values[index]
                          ?? throw new InvalidDataException(
                              $"Block model '{modelKey}' element {index} is null.");
            var from = ParseVector(element.From, modelKey, index, "from");
            var to = ParseVector(element.To, modelKey, index, "to");
            var faces = ParseFaces(element.Faces, modelKey, index);
            elements.Add(new UnbakedElement(from, to, faces));
        }

        return elements;
    }

    private static Dictionary<string, UnbakedFace> ParseFaces(
        Dictionary<string, JsonBlockModelFace?>? values,
        ResourceKey modelKey,
        int elementIndex)
    {
        var faces = new Dictionary<string, UnbakedFace>(StringComparer.Ordinal);
        if (values is null)
            return faces;

        foreach (var (direction, face) in values)
        {
            if (!Directions.Contains(direction))
                throw new InvalidDataException(
                    $"Block model '{modelKey}' element {elementIndex} has unknown face direction '{direction}'.");
            if (face is null || string.IsNullOrEmpty(face.Texture))
                throw new InvalidDataException(
                    $"Block model '{modelKey}' element {elementIndex} face '{direction}' is missing texture.");

            var uv = ParseUv(face.Uv, modelKey, elementIndex, direction);
            var cull = face.Cull ?? [];
            foreach (var cullDirection in cull)
            {
                if (!Directions.Contains(cullDirection))
                    throw new InvalidDataException(
                        $"Block model '{modelKey}' element {elementIndex} face '{direction}' has unknown cull direction '{cullDirection}'.");
            }

            faces.Add(direction, new UnbakedFace(ParseTextureValue(face.Texture, modelKey), uv, cull));
        }

        return faces;
    }

    private static BlockTextureValue ParseTextureValue(string value, ResourceKey modelKey)
    {
        if (value.StartsWith('#'))
        {
            var name = value[1..];
            if (name.Length == 0)
                throw new InvalidDataException($"Block model '{modelKey}' contains an empty texture variable.");
            return new BlockTextureValue.Variable(name);
        }

        var key = value.Contains(':')
            ? ResourceKey.Parse(value)
            : ResourceKey.Create(modelKey.Namespace, value);
        return new BlockTextureValue.Resource(key);
    }

    private static Vector3D<float> ParseVector(
        float[]? values,
        ResourceKey modelKey,
        int elementIndex,
        string propertyName)
    {
        if (values is null || values.Length != 3 || values.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException(
                $"Block model '{modelKey}' element {elementIndex} property '{propertyName}' must contain three finite values.");

        return new Vector3D<float>(values[0], values[1], values[2]);
    }

    private static float[]? ParseUv(
        float[]? values,
        ResourceKey modelKey,
        int elementIndex,
        string direction)
    {
        if (values is null)
            return null;
        if (values.Length != 4 || values.Any(value => !float.IsFinite(value) || value is < 0 or > 16))
            throw new InvalidDataException(
                $"Block model '{modelKey}' element {elementIndex} face '{direction}' uv must contain four values from 0 to 16.");

        return values.ToArray();
    }
}