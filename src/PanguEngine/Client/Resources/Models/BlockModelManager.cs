using Microsoft.Extensions.Logging;
using PanguEngine.Graphics;
using PanguEngine.Registries;
using PanguEngine.Resources;
using PanguEngine.Resources.Images;
using PanguEngine.World;
using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.Client.Resources.Models;

internal sealed class BlockModelManager
{
    private const int AtlasGutter = 1;
    private static readonly ResourceKey MissingTextureKey = ResourceKey.Create("pangu", "missing");
    private static readonly ResourceKey MissingModelKey = ResourceKey.Create("pangu", "missing_model");

    private static readonly Direction[] FaceDirections =
    [
        Direction.Down,
        Direction.Up,
        Direction.North,
        Direction.South,
        Direction.West,
        Direction.East
    ];

    private static readonly Action<ILogger, ResourceKey, ResourceKey, Exception?> LogMissingBlockAppearance =
        LoggerMessage.Define<ResourceKey, ResourceKey>(
            LogLevel.Error,
            new EventId(1, nameof(LogMissingBlockAppearance)),
            "Falling back to missing block model for block '{BlockKey}' after appearance '{AppearanceKey}' failed");

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

    internal BakedBlockModel Get(BlockState state, BlockPos position)
    {
        var snapshot = GetSnapshot();
        return snapshot.Appearances.TryGetValue(state.Block, out var appearance)
            ? appearance.Get(state, position)
            : snapshot.MissingModel;
    }

    internal void Load()
    {
        var appearanceLoader = new JsonBlockAppearanceLoader(_resources);
        var modelLoader = new JsonBlockModelLoader(_resources);
        var layerCache = new Dictionary<ResourceKey, UnresolvedBlockModel>();
        var resolvedModelCache = new Dictionary<ResourceKey, ResolvedBlockModel>();
        var failedModels = new Dictionary<ResourceKey, Exception>();
        var resolvedAppearances = new Dictionary<Block, ResolvedBlockAppearance>();
        var textureKeys = new HashSet<ResourceKey> { MissingTextureKey };
        var missingModel = CreateMissingModel();

        foreach (var entry in _blocks.Entries)
        {
            var appearanceKey = ResourceKey.Create(
                entry.Key.Namespace,
                $"appearances/block/{entry.Key.Path}");
            try
            {
                var definition = appearanceLoader.Load(entry.Key, entry.Value);
                var variants = new Dictionary<BlockState, IReadOnlyList<ResolvedBlockCandidate>>();
                var localTextureKeys = new HashSet<ResourceKey>();
                foreach (var (state, variantCandidates) in definition.Variants)
                {
                    var candidates = new List<ResolvedBlockCandidate>(variantCandidates.Count);
                    foreach (var candidate in variantCandidates)
                    {
                        if (failedModels.TryGetValue(candidate.ModelKey, out var previousFailure))
                        {
                            throw new InvalidDataException(
                                $"Block model '{candidate.ModelKey}' previously failed to resolve.",
                                previousFailure);
                        }

                        if (!resolvedModelCache.TryGetValue(candidate.ModelKey, out var model))
                        {
                            try
                            {
                                model = ResolveModel(modelLoader, candidate.ModelKey, [], layerCache);
                                resolvedModelCache.Add(candidate.ModelKey, model);
                            }
                            catch (Exception exception) when (IsRecoverableModelException(exception))
                            {
                                failedModels.Add(candidate.ModelKey, exception);
                                throw;
                            }
                        }

                        foreach (var textureKey in GetTextureKeys(model))
                            localTextureKeys.Add(textureKey);
                        candidates.Add(new ResolvedBlockCandidate(
                            candidate.ModelKey,
                            candidate.Rotation,
                            candidate.Weight,
                            model));
                    }

                    variants.Add(state, candidates);
                }

                textureKeys.UnionWith(localTextureKeys);
                resolvedAppearances.Add(
                    entry.Value,
                    new ResolvedBlockAppearance(entry.Key, appearanceKey, variants));
            }
            catch (Exception exception) when (IsRecoverableModelException(exception))
            {
                LogMissingBlockAppearance(_logger, entry.Key, appearanceKey, exception);
                resolvedAppearances.Add(
                    entry.Value,
                    CreateMissingAppearance(entry.Key, appearanceKey, entry.Value, missingModel));
            }
        }

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
        var bakedCache = new Dictionary<BakeKey, BakedBlockModel>();
        var failedBakes = new Dictionary<BakeKey, Exception>();
        var appearances = new Dictionary<Block, BakedBlockAppearance>();
        var bakedMissingModel = baker.Bake(missingModel);
        bakedCache.Add(new BakeKey(MissingModelKey, default), bakedMissingModel);
        foreach (var (block, resolved) in resolvedAppearances)
        {
            try
            {
                var variants = new Dictionary<BlockState, IReadOnlyList<BakedBlockAppearanceEntry>>();
                foreach (var (state, candidates) in resolved.Variants)
                {
                    var entries = new List<BakedBlockAppearanceEntry>(candidates.Count);
                    foreach (var candidate in candidates)
                    {
                        var key = new BakeKey(candidate.ModelKey, candidate.Rotation);
                        if (failedBakes.TryGetValue(key, out var previousFailure))
                        {
                            throw new InvalidDataException(
                                $"Block model '{candidate.ModelKey}' previously failed to bake.",
                                previousFailure);
                        }

                        if (!bakedCache.TryGetValue(key, out var bakedModel))
                        {
                            try
                            {
                                var model = ReplaceTextures(
                                    candidate.Model,
                                    failedTextures,
                                    MissingTextureKey);
                                bakedModel = baker.Bake(model, candidate.Rotation);
                                bakedCache.Add(key, bakedModel);
                            }
                            catch (Exception exception) when (IsRecoverableBakeException(exception))
                            {
                                failedBakes.Add(key, exception);
                                throw;
                            }
                        }

                        entries.Add(new BakedBlockAppearanceEntry(bakedModel, candidate.Weight));
                    }

                    variants.Add(state, entries);
                }

                appearances.Add(
                    block,
                    new BakedBlockAppearance(resolved.BlockKey, variants));
            }
            catch (Exception exception) when (IsRecoverableBakeException(exception))
            {
                LogMissingBlockAppearance(
                    _logger,
                    resolved.BlockKey,
                    resolved.SourceKey,
                    exception);
                appearances.Add(
                    block,
                    new BakedBlockAppearance(
                        resolved.BlockKey,
                        CreateMissingBakedVariants(block, bakedMissingModel)));
            }
        }

        _snapshot = new Snapshot(
            atlas,
            appearances,
            bakedMissingModel);
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

    private static ResolvedBlockModel CreateMissingModel()
    {
        var faces = FaceDirections
            .ToDictionary(
                direction => direction,
                direction => new ResolvedBlockFace(
                    MissingTextureKey,
                    [0, 0, 16, 16],
                    0,
                    direction.ToFlag()));
        return new ResolvedBlockModel(
            MissingModelKey,
            [
                new ResolvedBlockElement(
                    new Vector3D<float>(0, 0, 0),
                    new Vector3D<float>(16, 16, 16),
                    faces)
            ]);
    }

    private static IEnumerable<ResourceKey> GetTextureKeys(ResolvedBlockModel model)
    {
        return model.Elements
            .SelectMany(element => element.Faces.Values)
            .Select(face => face.Texture);
    }

    private static ResolvedBlockAppearance CreateMissingAppearance(
        ResourceKey blockKey,
        ResourceKey appearanceKey,
        Block block,
        ResolvedBlockModel missingModel)
    {
        IReadOnlyList<ResolvedBlockCandidate> candidates =
            [new(MissingModelKey, default, 1, missingModel)];
        return new ResolvedBlockAppearance(
            blockKey,
            appearanceKey,
            block.StateDefinition.States.ToDictionary(
                state => state,
                _ => candidates));
    }

    private static Dictionary<BlockState, IReadOnlyList<BakedBlockAppearanceEntry>>
        CreateMissingBakedVariants(Block block, BakedBlockModel missingModel)
    {
        IReadOnlyList<BakedBlockAppearanceEntry> entries =
            [new(missingModel, 1)];
        return block.StateDefinition.States.ToDictionary(
            state => state,
            _ => entries);
    }

    private static ResolvedBlockModel ResolveModel(
        JsonBlockModelLoader loader,
        ResourceKey modelKey,
        IReadOnlyList<ResourceKey> parentChain,
        Dictionary<ResourceKey, UnresolvedBlockModel> layerCache)
    {
        var layer = ResolveLayer(loader, modelKey, parentChain, layerCache);
        return new ResolvedBlockModel(
            layer.SourceKey,
            ResolveElements(layer.Elements!, layer.Textures, modelKey));
    }

    private static UnresolvedBlockModel ResolveLayer(
        JsonBlockModelLoader loader,
        ResourceKey modelKey,
        IReadOnlyList<ResourceKey> parentChain,
        Dictionary<ResourceKey, UnresolvedBlockModel> layerCache)
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

    private static UnresolvedBlockModel LoadDefinition(
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

    private static ResolvedBlockElement[] ResolveElements(
        IReadOnlyList<UnresolvedBlockElement> elements,
        IReadOnlyDictionary<string, BlockTextureValue> textures,
        ResourceKey modelKey)
    {
        var result = new ResolvedBlockElement[elements.Count];
        for (var elementIndex = 0; elementIndex < elements.Count; elementIndex++)
        {
            var element = elements[elementIndex];
            var faces = new Dictionary<Direction, ResolvedBlockFace>(element.Faces.Count);
            foreach (var (directionName, face) in element.Faces)
            {
                var direction = ParseDirection(directionName, modelKey);
                faces.Add(direction, new ResolvedBlockFace(
                    ResolveTexture(face.Texture, textures, modelKey).Key,
                    face.Uv ?? GetAutomaticUv(element.From, element.To, direction),
                    face.Rotation,
                    ResolveCull(face.Cull, modelKey)));
            }

            result[elementIndex] = new ResolvedBlockElement(
                element.From,
                element.To,
                faces);
        }

        return result;
    }

    private static float[] GetAutomaticUv(
        Vector3D<float> from,
        Vector3D<float> to,
        Direction direction)
    {
        return direction switch
        {
            Direction.Down => [from.X, 16 - to.Z, to.X, 16 - from.Z],
            Direction.Up => [from.X, from.Z, to.X, to.Z],
            Direction.North => [16 - to.X, 16 - to.Y, 16 - from.X, 16 - from.Y],
            Direction.South => [from.X, 16 - to.Y, to.X, 16 - from.Y],
            Direction.West => [from.Z, 16 - to.Y, to.Z, 16 - from.Y],
            Direction.East => [16 - to.Z, 16 - to.Y, 16 - from.Z, 16 - from.Y],
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    private static ResolvedBlockModel ReplaceTextures(
        ResolvedBlockModel model,
        HashSet<ResourceKey> failedTextures,
        ResourceKey replacementTexture)
    {
        var elements = model.Elements
            .Select(element => element with
            {
                Faces = element.Faces.ToDictionary(
                    pair => pair.Key,
                    pair => failedTextures.Contains(pair.Value.Texture)
                        ? pair.Value with { Texture = replacementTexture }
                        : pair.Value)
            })
            .ToArray();
        return model with { Elements = elements };
    }

    private static ResourceKey ResolveModelReference(string value, ResourceKey declaringModel)
    {
        if (value.Contains(':'))
            return ResourceKey.Parse(value);
        return ResourceKey.Create(declaringModel.Namespace, value);
    }

    private static DirectionFlags ResolveCull(
        IReadOnlyList<string> values,
        ResourceKey modelKey)
    {
        var result = DirectionFlags.None;
        foreach (var value in values)
            result |= ParseDirection(value, modelKey).ToFlag();
        return result;
    }

    private static Direction ParseDirection(string value, ResourceKey modelKey)
    {
        return value switch
        {
            "down" => Direction.Down,
            "up" => Direction.Up,
            "north" => Direction.North,
            "south" => Direction.South,
            "west" => Direction.West,
            "east" => Direction.East,
            _ => throw new InvalidDataException(
                $"Block model '{modelKey}' has unknown direction '{value}'.")
        };
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

    private static bool IsRecoverableBakeException(Exception exception)
    {
        return exception is InvalidDataException or ArgumentException or KeyNotFoundException;
    }

    private sealed record Snapshot(
        TextureAtlas<ResourceKey> Atlas,
        IReadOnlyDictionary<Block, BakedBlockAppearance> Appearances,
        BakedBlockModel MissingModel);

    private readonly record struct BakeKey(
        ResourceKey ModelKey,
        BlockModelRotation Rotation);
}