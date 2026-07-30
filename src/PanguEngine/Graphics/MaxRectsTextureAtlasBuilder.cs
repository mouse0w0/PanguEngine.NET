namespace PanguEngine.Graphics;

/// <summary>
/// Builds a deterministic single-page texture atlas using the MaxRects layout algorithm.
/// </summary>
/// <typeparam name="TKey">The type used to identify source images.</typeparam>
public sealed class MaxRectsTextureAtlasBuilder<TKey> where TKey : notnull
{
    private const float MipUvInset = 0.01f;

    private readonly int _maxWidth;
    private readonly int _maxHeight;
    private readonly int _gutter;
    private readonly int _mipLevels;
    private readonly int _alignment;
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<TKey, Entry> _entriesByKey = [];
    private bool _built;

    /// <summary>
    /// Creates a MaxRects texture atlas builder.
    /// </summary>
    /// <param name="maxWidth">The maximum atlas width in pixels.</param>
    /// <param name="maxHeight">The maximum atlas height in pixels.</param>
    /// <param name="gutter">The number of edge pixels reserved around each image.</param>
    public MaxRectsTextureAtlasBuilder(
        int maxWidth,
        int maxHeight,
        int gutter = 0)
        : this(maxWidth, maxHeight, gutter, 1)
    {
    }

    /// <summary>
    /// Creates a MaxRects texture atlas builder that generates mip levels.
    /// </summary>
    /// <param name="maxWidth">The maximum atlas width in pixels.</param>
    /// <param name="maxHeight">The maximum atlas height in pixels.</param>
    /// <param name="gutter">The number of edge pixels reserved around each image.</param>
    /// <param name="mipLevels">The number of mip levels generated for the atlas.</param>
    public MaxRectsTextureAtlasBuilder(
        int maxWidth,
        int maxHeight,
        int gutter,
        int mipLevels)
    {
        if (maxWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Maximum atlas width must be positive.");
        if (maxHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "Maximum atlas height must be positive.");
        if (gutter < 0)
            throw new ArgumentOutOfRangeException(nameof(gutter), "Gutter must not be negative.");
        if (mipLevels <= 0)
            throw new ArgumentOutOfRangeException(nameof(mipLevels), "Mip level count must be positive.");
        if (mipLevels > 31)
            throw new ArgumentOutOfRangeException(nameof(mipLevels), "Mip level alignment exceeds integer capacity.");

        var alignment = 1L << (mipLevels - 1);
        if (alignment > maxWidth || alignment > maxHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mipLevels),
                "Mip level alignment must fit within the maximum atlas dimensions.");
        }

        if (mipLevels > 1 && gutter > 0)
        {
            throw new ArgumentException(
                "Gutter is not supported when generating multiple mip levels.",
                nameof(gutter));
        }

        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
        _gutter = gutter;
        _mipLevels = mipLevels;
        _alignment = checked((int)alignment);
    }

    /// <summary>
    /// Adds an RGBA image to the atlas.
    /// </summary>
    /// <param name="key">The key used to identify the image.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="rgbaPixels">The row-major RGBA image pixels.</param>
    /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the key or pixel length is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the image dimensions are invalid.</exception>
    public void Add(
        TKey key,
        int width,
        int height,
        ReadOnlySpan<byte> rgbaPixels)
    {
        EnsureCollecting();

        if (key is null)
            throw new ArgumentNullException(nameof(key), "Texture atlas key must not be null.");
        if (_entriesByKey.ContainsKey(key))
            throw new ArgumentException($"Texture atlas key '{key}' is already present.", nameof(key));
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Image height must be positive.");

        var pixelLength = (long)width * height * 4;
        if (rgbaPixels.Length != pixelLength)
            throw new ArgumentException(
                $"RGBA pixel data length must be {pixelLength}, but was {rgbaPixels.Length}.",
                nameof(rgbaPixels));

        var cellWidth = _mipLevels == 1
            ? width + 2L * _gutter
            : AlignUp(width, _alignment);
        var cellHeight = _mipLevels == 1
            ? height + 2L * _gutter
            : AlignUp(height, _alignment);
        if (cellWidth > _maxWidth || cellHeight > _maxHeight)
        {
            var parameterName = cellWidth > _maxWidth ? nameof(width) : nameof(height);
            var paddingDescription = _mipLevels == 1
                ? $"gutter {_gutter}"
                : $"mip alignment {_alignment} producing aligned cell {cellWidth}x{cellHeight}";
            throw new ArgumentException(
                $"Image '{key}' with size {width}x{height} and {paddingDescription} exceeds the maximum atlas size {_maxWidth}x{_maxHeight}.",
                parameterName);
        }

        var entry = new Entry(
            key,
            width,
            height,
            [.. rgbaPixels],
            checked((int)cellWidth),
            checked((int)cellHeight));
        _entries.Add(entry);
        _entriesByKey.Add(key, entry);
    }

    /// <summary>
    /// Builds the texture atlas from the added images.
    /// </summary>
    /// <returns>The completed texture atlas.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the images cannot fit in the configured capacity.</exception>
    public TextureAtlas<TKey> Build()
    {
        EnsureCollecting();
        if (_entries.Count == 0)
        {
            if (_mipLevels > 1)
                throw new InvalidOperationException("A mipmapped texture atlas requires at least one image.");

            _built = true;
            return new TextureAtlas<TKey>(
                0,
                0,
                [Array.Empty<byte>()],
                new Dictionary<TKey, TextureAtlasRegion>());
        }

        var totalArea = _entries.Aggregate(0L, (sum, entry) =>
            checked(sum + (long)entry.CellWidth * entry.CellHeight));
        var maxCellWidth = _entries.Max(entry => entry.CellWidth);
        var maxCellHeight = _entries.Max(entry => entry.CellHeight);
        var side = Math.Max(CeilSqrt(totalArea), Math.Max(maxCellWidth, maxCellHeight));
        var candidateWidth = (int)Math.Min(_maxWidth, side);
        var candidateHeight = (int)Math.Min(_maxHeight, side);
        var layoutEntries = _entries
            .OrderByDescending(entry => Math.Max(entry.CellWidth, entry.CellHeight))
            .ThenByDescending(entry => (long)entry.CellWidth * entry.CellHeight)
            .ToArray();
        while (true)
        {
            if (TryPack(
                    layoutEntries,
                    candidateWidth,
                    candidateHeight,
                    out var placements,
                    out var failedEntry))
            {
                var atlas = CreateAtlas(placements);
                _built = true;
                return atlas;
            }

            if (!TryGrow(ref candidateWidth, ref candidateHeight))
                throw CreatePackingException(failedEntry!);
        }
    }

    private TextureAtlas<TKey> CreateAtlas(IReadOnlyList<Placement> placements)
    {
        var usedWidth = placements.Max(placement => placement.Cell.X + placement.Entry.CellWidth);
        var usedHeight = placements.Max(placement => placement.Cell.Y + placement.Entry.CellHeight);
        var mipPixels = CreateMipPixelBuffers(usedWidth, usedHeight);
        var regions = new Dictionary<TKey, TextureAtlasRegion>(_entries.Count);

        foreach (var placement in placements)
        {
            if (_mipLevels == 1)
                CopyImageAndGutter(mipPixels[0], usedWidth, placement);
            else
                CopyMipmappedCell(mipPixels, usedWidth, placement);

            var x = placement.Cell.X + _gutter;
            var y = placement.Cell.Y + _gutter;
            var entry = placement.Entry;
            var inset = _mipLevels > 1 ? MipUvInset : 0;
            regions.Add(entry.Key, new TextureAtlasRegion(
                x,
                y,
                entry.Width,
                entry.Height,
                (x + inset) / usedWidth,
                (y + inset) / usedHeight,
                (x + entry.Width - inset) / usedWidth,
                (y + entry.Height - inset) / usedHeight));
        }

        return new TextureAtlas<TKey>(usedWidth, usedHeight, mipPixels, regions);
    }

    private byte[][] CreateMipPixelBuffers(int width, int height)
    {
        var result = new byte[_mipLevels][];
        for (var level = 0; level < _mipLevels; level++)
        {
            var mipWidth = width >> level;
            var mipHeight = height >> level;
            var pixelLength = checked((int)((long)mipWidth * mipHeight * 4));
            result[level] = new byte[pixelLength];
        }

        return result;
    }

    private void CopyMipmappedCell(
        byte[][] destinationMipPixels,
        int destinationWidth,
        Placement placement)
    {
        var entry = placement.Entry;
        var cellPixels = CreateEdgeExtendedCell(entry);
        var cellWidth = entry.CellWidth;
        var cellHeight = entry.CellHeight;

        for (var level = 0; level < _mipLevels; level++)
        {
            CopyCell(
                destinationMipPixels[level],
                destinationWidth >> level,
                placement.Cell.X >> level,
                placement.Cell.Y >> level,
                cellPixels,
                cellWidth,
                cellHeight);

            if (level == _mipLevels - 1)
                continue;

            cellPixels = Downsample2X2(cellPixels, cellWidth, cellHeight);
            cellWidth >>= 1;
            cellHeight >>= 1;
        }
    }

    private static byte[] CreateEdgeExtendedCell(Entry entry)
    {
        var result = new byte[checked(entry.CellWidth * entry.CellHeight * 4)];
        for (var y = 0; y < entry.CellHeight; y++)
        {
            var sourceY = Math.Min(y, entry.Height - 1);
            for (var x = 0; x < entry.CellWidth; x++)
            {
                var sourceX = Math.Min(x, entry.Width - 1);
                var sourceOffset = ToArrayIndex(((long)sourceY * entry.Width + sourceX) * 4);
                var destinationOffset = ToArrayIndex(((long)y * entry.CellWidth + x) * 4);
                entry.Pixels.AsSpan(sourceOffset, 4)
                    .CopyTo(result.AsSpan(destinationOffset, 4));
            }
        }

        return result;
    }

    private static void CopyCell(
        byte[] destination,
        int destinationWidth,
        int destinationX,
        int destinationY,
        byte[] source,
        int sourceWidth,
        int sourceHeight)
    {
        var rowLength = checked(sourceWidth * 4);
        for (var y = 0; y < sourceHeight; y++)
        {
            var sourceOffset = checked(y * rowLength);
            var destinationOffset = ToArrayIndex(
                ((long)(destinationY + y) * destinationWidth + destinationX) * 4);
            source.AsSpan(sourceOffset, rowLength)
                .CopyTo(destination.AsSpan(destinationOffset, rowLength));
        }
    }

    private static byte[] Downsample2X2(byte[] source, int sourceWidth, int sourceHeight)
    {
        var destinationWidth = sourceWidth / 2;
        var destinationHeight = sourceHeight / 2;
        var destination = new byte[checked(destinationWidth * destinationHeight * 4)];
        for (var y = 0; y < destinationHeight; y++)
        {
            for (var x = 0; x < destinationWidth; x++)
            {
                var destinationOffset = (y * destinationWidth + x) * 4;
                Filter2X2(source, sourceWidth, x * 2, y * 2, destination, destinationOffset);
            }
        }

        return destination;
    }

    private static void Filter2X2(
        byte[] source,
        int sourceWidth,
        int sourceX,
        int sourceY,
        byte[] destination,
        int destinationOffset)
    {
        var alphaSum = 0f;
        var redSum = 0f;
        var greenSum = 0f;
        var blueSum = 0f;
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                var sourceOffset = ((sourceY + y) * sourceWidth + sourceX + x) * 4;
                var alpha = source[sourceOffset + 3] / 255f;
                alphaSum += alpha;
                redSum += SrgbToLinear(source[sourceOffset]) * alpha;
                greenSum += SrgbToLinear(source[sourceOffset + 1]) * alpha;
                blueSum += SrgbToLinear(source[sourceOffset + 2]) * alpha;
            }
        }

        if (alphaSum == 0)
        {
            destination.AsSpan(destinationOffset, 4).Clear();
            return;
        }

        destination[destinationOffset] = LinearToSrgb(redSum / alphaSum);
        destination[destinationOffset + 1] = LinearToSrgb(greenSum / alphaSum);
        destination[destinationOffset + 2] = LinearToSrgb(blueSum / alphaSum);
        destination[destinationOffset + 3] = ToByte(alphaSum / 4);
    }

    private static float SrgbToLinear(byte value)
    {
        var channel = value / 255f;
        return channel <= 0.04045f
            ? channel / 12.92f
            : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    private static byte LinearToSrgb(float value)
    {
        value = Math.Clamp(value, 0, 1);
        var channel = value <= 0.0031308f
            ? value * 12.92f
            : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
        return ToByte(channel);
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }

    private void CopyImageAndGutter(
        byte[] destination,
        int destinationWidth,
        Placement placement)
    {
        var entry = placement.Entry;
        var contentX = placement.Cell.X + _gutter;
        var contentY = placement.Cell.Y + _gutter;

        for (var y = 0; y < entry.Height; y++)
        {
            var rowLength = checked(entry.Width * 4);
            var sourceOffset = ToArrayIndex((long)y * rowLength);
            var destinationOffset = ToArrayIndex(
                ((long)(contentY + y) * destinationWidth + contentX) * 4);
            entry.Pixels.AsSpan(sourceOffset, rowLength)
                .CopyTo(destination.AsSpan(destinationOffset, rowLength));
        }

        for (var cellY = placement.Cell.Y; cellY < placement.Cell.Y + entry.CellHeight; cellY++)
        {
            for (var cellX = placement.Cell.X; cellX < placement.Cell.X + entry.CellWidth; cellX++)
            {
                if (cellX >= contentX
                    && cellX < contentX + entry.Width
                    && cellY >= contentY
                    && cellY < contentY + entry.Height)
                    continue;

                var sourceX = Math.Clamp(cellX - contentX, 0, entry.Width - 1);
                var sourceY = Math.Clamp(cellY - contentY, 0, entry.Height - 1);
                var sourceOffset = ToArrayIndex(((long)sourceY * entry.Width + sourceX) * 4);
                var destinationOffset = ToArrayIndex(
                    ((long)cellY * destinationWidth + cellX) * 4);
                entry.Pixels.AsSpan(sourceOffset, 4)
                    .CopyTo(destination.AsSpan(destinationOffset, 4));
            }
        }
    }

    private static bool TryPack(
        IReadOnlyList<Entry> entries,
        int width,
        int height,
        out List<Placement> placements,
        out Entry? failedEntry)
    {
        List<FreeRect> freeRects = [new(0, 0, width, height)];
        placements = new List<Placement>(entries.Count);
        failedEntry = null;

        foreach (var entry in entries)
        {
            var bestIndex = -1;
            var bestShortSideFit = long.MaxValue;
            var bestAreaFit = long.MaxValue;
            var bestX = int.MaxValue;
            var bestY = int.MaxValue;

            for (var index = 0; index < freeRects.Count; index++)
            {
                var freeRect = freeRects[index];
                if (freeRect.Width < entry.CellWidth || freeRect.Height < entry.CellHeight)
                    continue;

                var shortSideFit = Math.Min(
                    (long)freeRect.Width - entry.CellWidth,
                    (long)freeRect.Height - entry.CellHeight);
                var areaFit = (long)freeRect.Width * freeRect.Height
                              - (long)entry.CellWidth * entry.CellHeight;
                var x = freeRect.X;
                var y = freeRect.Y;
                if (!IsBetterPlacement(
                        shortSideFit,
                        areaFit,
                        x,
                        y,
                        index,
                        bestShortSideFit,
                        bestAreaFit,
                        bestX,
                        bestY,
                        bestIndex))
                    continue;

                bestIndex = index;
                bestShortSideFit = shortSideFit;
                bestAreaFit = areaFit;
                bestX = x;
                bestY = y;
            }

            if (bestIndex < 0)
            {
                failedEntry = entry;
                return false;
            }

            var placement = new Placement(entry, new FreeRect(
                bestX,
                bestY,
                entry.CellWidth,
                entry.CellHeight));
            placements.Add(placement);
            SplitFreeRects(freeRects, placement.Cell);
        }

        return true;
    }

    private static bool IsBetterPlacement(
        long shortSideFit,
        long areaFit,
        int x,
        int y,
        int index,
        long bestShortSideFit,
        long bestAreaFit,
        int bestX,
        int bestY,
        int bestIndex)
    {
        if (shortSideFit != bestShortSideFit)
            return shortSideFit < bestShortSideFit;
        if (areaFit != bestAreaFit)
            return areaFit < bestAreaFit;
        if (y != bestY)
            return y < bestY;
        if (x != bestX)
            return x < bestX;
        return index < bestIndex;
    }

    private static void SplitFreeRects(List<FreeRect> freeRects, FreeRect placed)
    {
        var originalRects = freeRects.ToArray();
        var retainedRects = new List<FreeRect>(originalRects.Length);
        List<FreeRect> generatedRects = [];

        foreach (var freeRect in originalRects)
        {
            if (!Intersects(freeRect, placed))
            {
                retainedRects.Add(freeRect);
                continue;
            }

            AddIfPositive(generatedRects, freeRect with
            {
                Height = placed.Y - freeRect.Y
            });
            AddIfPositive(generatedRects, freeRect with
            {
                Width = placed.X - freeRect.X
            });
            AddIfPositive(generatedRects, freeRect with
            {
                Y = placed.Y + placed.Height,
                Height = freeRect.Y + freeRect.Height - (placed.Y + placed.Height)
            });
            AddIfPositive(generatedRects, freeRect with
            {
                X = placed.X + placed.Width,
                Width = freeRect.X + freeRect.Width - (placed.X + placed.Width)
            });
        }

        freeRects.Clear();
        freeRects.AddRange(retainedRects);
        freeRects.AddRange(generatedRects);
        RemoveContainedRects(freeRects);
    }

    private static void RemoveContainedRects(List<FreeRect> freeRects)
    {
        var removed = new bool[freeRects.Count];
        for (var i = 0; i < freeRects.Count; i++)
        {
            if (removed[i])
                continue;

            for (var j = i + 1; j < freeRects.Count; j++)
            {
                if (removed[j])
                    continue;

                if (Contains(freeRects[i], freeRects[j]))
                {
                    removed[j] = true;
                }
                else if (Contains(freeRects[j], freeRects[i]))
                {
                    removed[i] = true;
                    break;
                }
            }
        }

        for (var index = freeRects.Count - 1; index >= 0; index--)
        {
            if (removed[index])
                freeRects.RemoveAt(index);
        }
    }

    private static bool Intersects(FreeRect first, FreeRect second)
    {
        return first.X < second.X + (long)second.Width
               && second.X < first.X + (long)first.Width
               && first.Y < second.Y + (long)second.Height
               && second.Y < first.Y + (long)first.Height;
    }

    private static bool Contains(FreeRect outer, FreeRect inner)
    {
        return outer.X <= inner.X
               && outer.Y <= inner.Y
               && outer.X + (long)outer.Width >= inner.X + (long)inner.Width
               && outer.Y + (long)outer.Height >= inner.Y + (long)inner.Height;
    }

    private static void AddIfPositive(List<FreeRect> freeRects, FreeRect freeRect)
    {
        if (freeRect.Width > 0 && freeRect.Height > 0)
            freeRects.Add(freeRect);
    }

    private static int ToArrayIndex(long value) => checked((int)value);

    private static int AlignUp(int value, int alignment)
    {
        return checked((int)(((long)value + alignment - 1) / alignment * alignment));
    }

    private bool TryGrow(ref int width, ref int height)
    {
        if (width <= height)
        {
            if (width < _maxWidth)
            {
                width = Grow(width, _maxWidth);
                return true;
            }

            if (height < _maxHeight)
            {
                height = Grow(height, _maxHeight);
                return true;
            }
        }
        else
        {
            if (height < _maxHeight)
            {
                height = Grow(height, _maxHeight);
                return true;
            }

            if (width < _maxWidth)
            {
                width = Grow(width, _maxWidth);
                return true;
            }
        }

        return false;
    }

    private static int Grow(int current, int maximum)
    {
        return (int)Math.Min(maximum, (long)current * 2);
    }

    private InvalidOperationException CreatePackingException(Entry failedEntry)
    {
        var paddingDescription = _mipLevels == 1
            ? $"gutter {_gutter}"
            : $"mip levels {_mipLevels} and alignment {_alignment}";
        return new InvalidOperationException(
            $"Texture atlas cannot fit key '{failedEntry.Key}' with image size "
            + $"{failedEntry.Width}x{failedEntry.Height} and cell size "
            + $"{failedEntry.CellWidth}x{failedEntry.CellHeight}; maximum atlas size is "
            + $"{_maxWidth}x{_maxHeight} with {paddingDescription}.");
    }

    private void EnsureCollecting()
    {
        if (_built)
            throw new InvalidOperationException("Texture atlas builder has already built an atlas.");
    }

    private static long CeilSqrt(long value)
    {
        var low = 1L;
        var high = 3_037_000_500L;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var squareIsLargeEnough = middle > value / middle
                                      || middle == value / middle && value % middle == 0;
            if (squareIsLargeEnough)
                high = middle;
            else
                low = middle + 1;
        }

        return low;
    }

    private sealed class Entry(
        TKey key,
        int width,
        int height,
        byte[] pixels,
        int cellWidth,
        int cellHeight)
    {
        public TKey Key { get; } = key;
        public int Width { get; } = width;
        public int Height { get; } = height;
        public byte[] Pixels { get; } = pixels;
        public int CellWidth { get; } = cellWidth;
        public int CellHeight { get; } = cellHeight;
    }

    private readonly record struct FreeRect(int X, int Y, int Width, int Height);

    private readonly record struct Placement(Entry Entry, FreeRect Cell);
}