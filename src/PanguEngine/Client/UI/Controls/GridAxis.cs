namespace PanguEngine.Client.UI.Controls;

internal sealed class GridAxis
{
    private readonly GridDefinition[] _definitions;
    private readonly double[] _desired;
    private readonly double[] _sizes;
    private readonly double[] _starts;
    private readonly double[] _ends;
    private readonly double _spacing;
    private readonly double _totalSpacing;
    private bool _unbounded;

    internal GridAxis(GridDefinitions definitions, double spacing)
    {
        _definitions = definitions.Count == 0 ? [new GridDefinition(GridLength.Star)] : [.. definitions];
        _desired = new double[_definitions.Length];
        _sizes = new double[_definitions.Length];
        _starts = new double[_definitions.Length];
        _ends = new double[_definitions.Length];
        _spacing = spacing;
        _totalSpacing = Finite(spacing * (_definitions.Length - 1));
        for (var index = 0; index < Count; index++)
        {
            var definition = _definitions[index];
            _desired[index] = definition.Length.UnitType == GridUnitType.Pixel
                ? Math.Clamp(definition.Length.Value, definition.MinLength, Maximum(index))
                : definition.MinLength;
        }
    }

    internal int Count => _definitions.Length;

    internal double DesiredExtent => Sum(_desired, 0, Count);

    internal (int Index, int Span) GetRange(int index, int span)
    {
        index = Math.Min(index, Count - 1);
        return (index, Math.Min(span, Count - index));
    }

    internal void Allocate(double available, bool round, double scale)
    {
        AllocateSizes(available);
        var cumulative = 0d;
        var boundary = 0d;
        for (var index = 0; index < Count; index++)
        {
            var precedingSpacing = Finite(_spacing * index);
            _starts[index] = Finite(boundary + precedingSpacing);
            cumulative = Finite(cumulative + _sizes[index]);
            boundary = round ? UiLayoutHelper.RoundLayoutValue(cumulative, scale) : cumulative;
            _ends[index] = Finite(boundary + precedingSpacing);
        }
    }

    private void AllocateSizes(double available)
    {
        _unbounded = double.IsPositiveInfinity(available);
        if (_unbounded)
        {
            _desired.CopyTo(_sizes, 0);
            return;
        }

        var active = new bool[Count];
        var reserved = _totalSpacing;
        var minimumTotal = 0d;
        for (var index = 0; index < Count; index++)
        {
            var definition = _definitions[index];
            if (definition.Length.UnitType == GridUnitType.Star && definition.Length.Value > 0)
            {
                active[index] = true;
                _sizes[index] = definition.MinLength;
                minimumTotal = Finite(minimumTotal + definition.MinLength);
            }
            else
            {
                _sizes[index] = definition.Length.UnitType == GridUnitType.Star
                    ? definition.MinLength : _desired[index];
                reserved = Finite(reserved + _sizes[index]);
            }
        }

        var remaining = Math.Max(0, available - reserved);
        if (remaining <= minimumTotal)
            return;

        while (true)
        {
            var largestWeight = 0d;
            for (var index = 0; index < Count; index++)
                if (active[index])
                    largestWeight = Math.Max(largestWeight, _definitions[index].Length.Value);
            if (largestWeight == 0)
                return;

            var totalWeight = 0d;
            for (var index = 0; index < Count; index++)
                if (active[index])
                    totalWeight += _definitions[index].Length.Value / largestWeight;

            var violation = 0d;
            for (var index = 0; index < Count; index++)
            {
                if (!active[index])
                    continue;
                var ideal = Share(remaining, _definitions[index].Length.Value, largestWeight, totalWeight);
                _sizes[index] = Math.Clamp(ideal, _definitions[index].MinLength, Maximum(index));
                violation += _sizes[index] - ideal;
            }
            if (violation == 0)
                return;

            var freezeMinimum = violation > 0;
            for (var index = 0; index < Count; index++)
            {
                if (!active[index])
                    continue;
                var ideal = Share(remaining, _definitions[index].Length.Value, largestWeight, totalWeight);
                if (freezeMinimum ? ideal >= _definitions[index].MinLength : ideal <= Maximum(index))
                    continue;
                active[index] = false;
            }

            var allocated = reserved;
            for (var index = 0; index < Count; index++)
                if (!active[index] && _definitions[index].Length.UnitType == GridUnitType.Star &&
                    _definitions[index].Length.Value > 0)
                    allocated = Finite(allocated + _sizes[index]);
            remaining = Math.Max(0, available - allocated);
        }
    }

    internal double GetDiscoveryConstraint(int index, int span, bool discoverStars)
    {
        var discoversContent = false;
        for (var current = index; current < index + span; current++)
        {
            var unit = _definitions[current].Length.UnitType;
            discoversContent |= unit == GridUnitType.Auto ||
                (unit == GridUnitType.Star && (discoverStars || _unbounded));
        }
        if (!discoversContent)
            return GetExtent(index, span);

        var constraint = Finite(_spacing * (span - 1));
        for (var current = index; current < index + span; current++)
        {
            var contribution = _definitions[current].Length.UnitType == GridUnitType.Pixel
                ? _desired[current] : Maximum(current);
            if (double.IsPositiveInfinity(contribution))
                return double.PositiveInfinity;
            constraint = Finite(constraint + contribution);
        }
        return constraint;
    }

    internal double GetExtent(int index, int span) => _ends[index + span - 1] - _starts[index];

    internal void AddRequirement(int index, int span, double desired)
    {
        var remaining = Math.Max(0, desired - Sum(_desired, index, span));
        remaining = Grow(index, span, remaining, GridUnitType.Star, positiveWeights: true);
        remaining = Grow(index, span, remaining, GridUnitType.Auto, positiveWeights: false);
        _ = Grow(index, span, remaining, GridUnitType.Star, positiveWeights: false);
    }

    internal (double Origin, double Extent) GetSlot(int index, int span, double origin) =>
        (Finite(origin + _starts[index]), GetExtent(index, span));

    private double Grow(int index, int span, double remaining, GridUnitType unit, bool positiveWeights)
    {
        var active = new bool[span];
        for (var offset = 0; offset < span; offset++)
        {
            var current = index + offset;
            var length = _definitions[current].Length;
            active[offset] = length.UnitType == unit &&
                (unit != GridUnitType.Star || (length.Value > 0) == positiveWeights) &&
                _desired[current] < Maximum(current);
        }

        while (remaining > 0)
        {
            var largestWeight = 0d;
            for (var offset = 0; offset < span; offset++)
                if (active[offset])
                    largestWeight = Math.Max(largestWeight, positiveWeights ? _definitions[index + offset].Length.Value : 1);
            if (largestWeight == 0)
                break;

            var totalWeight = 0d;
            for (var offset = 0; offset < span; offset++)
                if (active[offset])
                    totalWeight += positiveWeights ? _definitions[index + offset].Length.Value / largestWeight : 1;

            var capped = false;
            var nextRemaining = remaining;
            for (var offset = 0; offset < span; offset++)
            {
                if (!active[offset])
                    continue;
                var current = index + offset;
                var weight = positiveWeights ? _definitions[current].Length.Value : 1;
                var addition = Share(remaining, weight, largestWeight, totalWeight);
                var capacity = Maximum(current) - _desired[current];
                if (addition <= capacity)
                    continue;
                _desired[current] = Maximum(current);
                active[offset] = false;
                nextRemaining = Math.Max(0, nextRemaining - capacity);
                capped = true;
            }
            if (capped)
            {
                remaining = nextRemaining;
                continue;
            }

            for (var offset = 0; offset < span; offset++)
            {
                if (!active[offset])
                    continue;
                var current = index + offset;
                var weight = positiveWeights ? _definitions[current].Length.Value : 1;
                _desired[current] = Math.Min(Maximum(current),
                    Finite(_desired[current] + Share(remaining, weight, largestWeight, totalWeight)));
            }
            return 0;
        }
        return remaining;
    }

    private double Maximum(int index) => Math.Max(_definitions[index].MinLength, _definitions[index].MaxLength);

    private static double Share(double amount, double weight, double largestWeight, double totalWeight)
    {
        if (amount == 0)
            return 0;
        var amountExponent = Math.ILogB(amount);
        var weightExponent = Math.ILogB(weight);
        var largestExponent = Math.ILogB(largestWeight);
        var mantissa = Math.ScaleB(amount, -amountExponent) * Math.ScaleB(weight, -weightExponent) /
            Math.ScaleB(largestWeight, -largestExponent) / totalWeight;
        return Math.Min(amount, Math.ScaleB(mantissa, amountExponent + weightExponent - largestExponent));
    }

    private double Sum(double[] values, int index, int count)
    {
        var result = Finite(_spacing * (count - 1));
        for (var current = index; current < index + count; current++)
            result = Finite(result + values[current]);
        return result;
    }

    private static double Finite(double value)
    {
        if (!double.IsFinite(value))
            throw new InvalidOperationException("Grid layout produced a non-finite track extent or position.");
        return value;
    }
}
