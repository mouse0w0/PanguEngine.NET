using Microsoft.Extensions.Logging;
using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.Resources.Images;
using PanguEngine.World.Blocks;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed class BlockModelManager
{
    private const int AtlasGutter = 1;
    private static readonly ResourceKey MissingTextureKey = ResourceKey.Create("pangu", "missing");
    private static readonly ResourceKey MissingModelKey = ResourceKey.Create("pangu", "missing_model");
    private static readonly string[] FaceDirections = ["down", "up", "north", "south", "west", "east"];

    private static readonly UnbakedBlockModel EmptyModel = new(
        ResourceKey.Create("pangu", "empty"),
        null,
        new Dictionary<string, BlockTextureValue>(StringComparer.Ordinal),
        []);

    private static readonly Action<ILogger, ResourceKey, ResourceKey, Exception?> LogMissingBlockModel =
        LoggerMessage.Define<ResourceKey, ResourceKey>(
            LogLevel.Error,
            new EventId(1, nameof(LogMissingBlockModel)),
            "Falling back to missing block model for block '{BlockKey}' after model '{ModelKey}' failed");

    private static readonly Action<ILogger, ResourceKey, Exception?> LogMissingTexture =
        LoggerMessage.Define<ResourceKey>(
            LogLevel.Error,
            new EventId(2, nameof(LogMissingTexture)),
            "Falling back to missing texture for texture '{TextureKey}'");

    private readonly ResourceManager _resources;
    private readonly IRegistry<Block> _blocks;
    private readonly int _atlasMaxDimension;
    private readonly ILogger _logger;
    private Snapshot? _snapshot;

    internal BlockModelManager(
        ResourceManager resources,
        IRegistry<Block> blocks,
        uint maxTextureDimension2D,
        ILogger logger)
    {
        _resources = resources;
        _blocks = blocks;
        _atlasMaxDimension = checked((int)maxTextureDimension2D);
        _logger = logger;
    }

    internal TextureAtlas<ResourceKey> Atlas => GetSnapshot().Atlas;

    internal BakedBlockModel Get(BlockState state)
    {
        if (state.IsAir)
            return GetSnapshot().AirModel;

        var snapshot = GetSnapshot();
        return snapshot.Models.TryGetValue(state.Block, out var model)
            ? model
            : snapshot.MissingModel;
    }

    internal void Load()
    {
        var loader = new JsonBlockModelLoader(_resources);
        var unbakedModels = new Dictionary<Block, UnbakedBlockModel>();
        var layerCache = new Dictionary<ResourceKey, UnbakedBlockModel>();
        var missingModel = CreateMissingModel();

        foreach (var entry in _blocks.Entries)
        {
            if (entry.Value.IsAir)
            {
                unbakedModels.Add(entry.Value, EmptyModel);
                continue;
            }

            var modelKey = ResourceKey.Create(entry.Key.Namespace, $"block/{entry.Key.Path}");
            try
            {
                unbakedModels.Add(entry.Value, ResolveModel(loader, modelKey, [], layerCache));
            }
            catch (Exception exception) when (IsRecoverableModelException(exception))
            {
                LogMissingBlockModel(_logger, entry.Key, modelKey, exception);
                unbakedModels.Add(entry.Value, missingModel);
            }
        }

        var textureKeys = unbakedModels.Values
            .SelectMany(model => model.Elements!
                .SelectMany(element => element.Faces.Values)
                .Select(face => GetTextureKey(face.Texture, model.SourceKey)))
            .Append(MissingTextureKey)
            .Distinct()
            .ToArray();
        var failedTextures = new HashSet<ResourceKey>();
        var textures = new Dictionary<ResourceKey, RgbaImage>();
        foreach (var key in textureKeys)
        {
            if (key == MissingTextureKey)
            {
                textures.Add(key, new RgbaImage(16, 16, CreateMissingTexture()));
                continue;
            }

            try
            {
                using var stream = _resources.Open(
                    ResourceKey.Create(key.Namespace, $"textures/{key.Path}.png"));
                textures.Add(key, ImageDecoder.Decode(stream));
            }
            catch (Exception exception) when (IsRecoverableTextureException(exception))
            {
                if (failedTextures.Add(key))
                {
                    LogMissingTexture(_logger, key, exception);
                }
            }
        }

        var builder = new MaxRectsTextureAtlasBuilder<ResourceKey>(
            _atlasMaxDimension,
            _atlasMaxDimension,
            AtlasGutter);
        foreach (var (key, texture) in textures)
            builder.Add(key, texture.Width, texture.Height, texture.Pixels.Span);
        var atlas = builder.Build();

        var baker = new BlockModelBaker(atlas);
        var models = new Dictionary<Block, BakedBlockModel>();
        foreach (var (block, unbakedModel) in unbakedModels)
        {
            var model = ReplaceTextures(unbakedModel, failedTextures, MissingTextureKey);
            models.Add(block, baker.Bake(model));
        }

        _snapshot = new Snapshot(
            atlas,
            models,
            baker.Bake(EmptyModel),
            baker.Bake(missingModel));
    }

    private static byte[] CreateMissingTexture()
    {
        var pixels = new byte[16 * 16 * 4];
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var isLight = (x / 4 + y / 4) % 2 == 0;
                var offset = (y * 16 + x) * 4;
                pixels[offset] = isLight ? (byte)255 : (byte)0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = isLight ? (byte)255 : (byte)0;
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    private static UnbakedBlockModel CreateMissingModel()
    {
        var faces = FaceDirections
            .ToDictionary(
                direction => direction,
                direction => new UnbakedFace(
                    new BlockTextureValue.Resource(MissingTextureKey),
                    null,
                    0,
                    [direction]),
                StringComparer.Ordinal);
        return new UnbakedBlockModel(
            MissingModelKey,
            null,
            new Dictionary<string, BlockTextureValue>(StringComparer.Ordinal),
            [
                new UnbakedElement(
                    new Vector3D<float>(0, 0, 0),
                    new Vector3D<float>(16, 16, 16),
                    faces)
            ]);
    }

    private static UnbakedBlockModel ResolveModel(
        JsonBlockModelLoader loader,
        ResourceKey modelKey,
        IReadOnlyList<ResourceKey> parentChain,
        Dictionary<ResourceKey, UnbakedBlockModel> layerCache)
    {
        var layer = ResolveLayer(loader, modelKey, parentChain, layerCache);
        var textures = new Dictionary<string, BlockTextureValue>(layer.Textures, StringComparer.Ordinal);
        var elements = ResolveElementTextures(layer.Elements!, textures, modelKey);
        return layer with
        {
            ParentReference = null,
            Textures = textures,
            Elements = elements
        };
    }

    private static UnbakedBlockModel ResolveLayer(
        JsonBlockModelLoader loader,
        ResourceKey modelKey,
        IReadOnlyList<ResourceKey> parentChain,
        Dictionary<ResourceKey, UnbakedBlockModel> layerCache)
    {
        if (parentChain.Contains(modelKey))
        {
            var chain = parentChain.Append(modelKey);
            throw new InvalidDataException($"Block model parent cycle: {string.Join(" -> ", chain)}.");
        }

        if (layerCache.TryGetValue(modelKey, out var cachedLayer))
            return cachedLayer;

        var chainWithCurrent = parentChain.Append(modelKey).ToArray();
        var definition = LoadDefinition(loader, modelKey, chainWithCurrent);
        var parent = definition.ParentReference is null
            ? null
            : ResolveLayer(
                loader,
                ResolveModelReference(definition.ParentReference, modelKey),
                chainWithCurrent,
                layerCache);

        var textures = parent is null
            ? new Dictionary<string, BlockTextureValue>(StringComparer.Ordinal)
            : new Dictionary<string, BlockTextureValue>(parent.Textures, StringComparer.Ordinal);

        foreach (var texture in definition.Textures)
            textures[texture.Key] = texture.Value;

        var elements = definition.Elements ?? parent?.Elements ?? [];
        var layer = definition with
        {
            ParentReference = null,
            Textures = textures,
            Elements = elements
        };
        layerCache.Add(modelKey, layer);
        return layer;
    }

    private static UnbakedBlockModel LoadDefinition(
        JsonBlockModelLoader loader,
        ResourceKey modelKey,
        IReadOnlyList<ResourceKey> parentChain)
    {
        try
        {
            return loader.Load(modelKey);
        }
        catch (Exception exception) when (IsRecoverableModelException(exception))
        {
            throw new InvalidDataException(
                $"Failed to load block model '{modelKey}' while resolving parent chain '{string.Join(" -> ", parentChain)}'.",
                exception);
        }
    }

    private static UnbakedElement[] ResolveElementTextures(
        IReadOnlyList<UnbakedElement> elements,
        IReadOnlyDictionary<string, BlockTextureValue> textures,
        ResourceKey modelKey)
    {
        return elements
            .Select(element => element with
            {
                Faces = element.Faces.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value with
                    {
                        Texture = ResolveTexture(pair.Value.Texture, textures, modelKey)
                    },
                    StringComparer.Ordinal)
            })
            .ToArray();
    }

    private static UnbakedBlockModel ReplaceTextures(
        UnbakedBlockModel model,
        HashSet<ResourceKey> failedTextures,
        ResourceKey replacementTexture)
    {
        var elements = model.Elements!
            .Select(element => element with
            {
                Faces = element.Faces.ToDictionary(
                    pair => pair.Key,
                    pair =>
                    {
                        var texture = GetTextureKey(pair.Value.Texture, model.SourceKey);
                        return failedTextures.Contains(texture)
                            ? pair.Value with
                            {
                                Texture = new BlockTextureValue.Resource(replacementTexture)
                            }
                            : pair.Value;
                    },
                    StringComparer.Ordinal)
            })
            .ToArray();
        return model with { Elements = elements };
    }

    private static ResourceKey GetTextureKey(BlockTextureValue value, ResourceKey modelKey)
    {
        return value is BlockTextureValue.Resource resource
            ? resource.Key
            : throw new InvalidDataException($"Block model '{modelKey}' has an unresolved texture variable.");
    }

    private static ResourceKey ResolveModelReference(string value, ResourceKey declaringModel)
    {
        if (value.Contains(':'))
            return ResourceKey.Parse(value);
        return ResourceKey.Create(declaringModel.Namespace, value);
    }

    private static BlockTextureValue.Resource ResolveTexture(
        BlockTextureValue value,
        IReadOnlyDictionary<string, BlockTextureValue> textures,
        ResourceKey modelKey)
    {
        var chain = new HashSet<string>(StringComparer.Ordinal);
        while (value is BlockTextureValue.Variable variable)
        {
            if (!chain.Add(variable.Name)
                || !textures.TryGetValue(variable.Name, out var nextValue))
                throw new InvalidDataException(
                    $"Block model '{modelKey}' has an invalid texture variable '#{variable.Name}'.");
            value = nextValue;
        }

        return value as BlockTextureValue.Resource
               ?? throw new InvalidDataException($"Block model '{modelKey}' has an invalid texture value.");
    }

    private Snapshot GetSnapshot()
    {
        return _snapshot ?? throw new InvalidOperationException("Block models have not been loaded.");
    }

    private static bool IsRecoverableModelException(Exception exception)
    {
        return exception is IOException or InvalidDataException or FormatException or ArgumentException;
    }

    private static bool IsRecoverableTextureException(Exception exception)
    {
        return exception is IOException or InvalidDataException or InvalidOperationException or ArgumentException;
    }

    private sealed record Snapshot(
        TextureAtlas<ResourceKey> Atlas,
        IReadOnlyDictionary<Block, BakedBlockModel> Models,
        BakedBlockModel AirModel,
        BakedBlockModel MissingModel);
}