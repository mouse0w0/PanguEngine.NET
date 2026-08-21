namespace PanguEngine.Client.UI.Rendering;

internal readonly record struct UiImageAtlasRegion(
    uint X,
    uint Y,
    uint Width,
    uint Height);

internal sealed class UiImageAtlasAllocator(uint width, uint height)
{
    private readonly List<UiImageAtlasRegion> _freeRegions =
        [new(0, 0, width, height)];

    internal bool TryAllocate(uint width, uint height, out UiImageAtlasRegion region)
    {
        var bestIndex = -1;
        var bestRemainingArea = ulong.MaxValue;
        var bestShortSide = uint.MaxValue;
        var bestY = uint.MaxValue;
        var bestX = uint.MaxValue;

        for (var index = 0; index < _freeRegions.Count; index++)
        {
            var free = _freeRegions[index];
            if (width > free.Width || height > free.Height)
                continue;

            var remainingWidth = free.Width - width;
            var remainingHeight = free.Height - height;
            var remainingArea = (ulong)free.Width * free.Height - (ulong)width * height;
            var shortSide = Math.Min(remainingWidth, remainingHeight);
            if (remainingArea > bestRemainingArea ||
                remainingArea == bestRemainingArea && shortSide > bestShortSide ||
                remainingArea == bestRemainingArea && shortSide == bestShortSide && free.Y > bestY ||
                remainingArea == bestRemainingArea && shortSide == bestShortSide && free.Y == bestY && free.X >= bestX)
            {
                continue;
            }

            bestIndex = index;
            bestRemainingArea = remainingArea;
            bestShortSide = shortSide;
            bestY = free.Y;
            bestX = free.X;
        }

        if (bestIndex < 0)
        {
            region = default;
            return false;
        }

        var selected = _freeRegions[bestIndex];
        _freeRegions.RemoveAt(bestIndex);
        region = selected with { Width = width, Height = height };
        Split(selected, width, height);
        return true;
    }

    internal void Free(UiImageAtlasRegion region)
    {
        _freeRegions.Add(region);
        while (TryMergeFirstPair())
        {
        }
    }

    private void Split(UiImageAtlasRegion free, uint width, uint height)
    {
        var remainingWidth = free.Width - width;
        var remainingHeight = free.Height - height;
        if (remainingWidth > remainingHeight)
        {
            AddFreeRegion(free.X + width, free.Y, remainingWidth, free.Height);
            AddFreeRegion(free.X, free.Y + height, width, remainingHeight);
        }
        else
        {
            AddFreeRegion(free.X + width, free.Y, remainingWidth, height);
            AddFreeRegion(free.X, free.Y + height, free.Width, remainingHeight);
        }
    }

    private void AddFreeRegion(uint x, uint y, uint width, uint height)
    {
        if (width != 0 && height != 0)
            _freeRegions.Add(new(x, y, width, height));
    }

    private bool TryMergeFirstPair()
    {
        _freeRegions.Sort(CompareRegions);
        for (var firstIndex = 0; firstIndex < _freeRegions.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < _freeRegions.Count; secondIndex++)
            {
                if (!TryMerge(_freeRegions[firstIndex], _freeRegions[secondIndex], out var merged))
                    continue;

                _freeRegions.RemoveAt(secondIndex);
                _freeRegions.RemoveAt(firstIndex);
                _freeRegions.Add(merged);
                return true;
            }
        }

        return false;
    }

    private static bool TryMerge(
        UiImageAtlasRegion first,
        UiImageAtlasRegion second,
        out UiImageAtlasRegion merged)
    {
        if (first.X == second.X && first.Width == second.Width)
        {
            if (first.Y + first.Height == second.Y)
            {
                merged = first with { Height = first.Height + second.Height };
                return true;
            }

            if (second.Y + second.Height == first.Y)
            {
                merged = second with { Height = second.Height + first.Height };
                return true;
            }
        }

        if (first.Y == second.Y && first.Height == second.Height)
        {
            if (first.X + first.Width == second.X)
            {
                merged = first with { Width = first.Width + second.Width };
                return true;
            }

            if (second.X + second.Width == first.X)
            {
                merged = second with { Width = second.Width + first.Width };
                return true;
            }
        }

        merged = default;
        return false;
    }

    private static int CompareRegions(UiImageAtlasRegion first, UiImageAtlasRegion second)
    {
        var result = first.Y.CompareTo(second.Y);
        if (result != 0)
            return result;
        result = first.X.CompareTo(second.X);
        if (result != 0)
            return result;
        result = first.Height.CompareTo(second.Height);
        return result != 0 ? result : first.Width.CompareTo(second.Width);
    }
}
