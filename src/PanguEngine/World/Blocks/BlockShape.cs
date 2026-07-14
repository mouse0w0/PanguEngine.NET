using Silk.NET.Maths;

namespace PanguEngine.World.Blocks;

/// <summary>
/// Represents a block-local shape composed of axis-aligned boxes.
/// </summary>
public sealed class BlockShape : IBlockShape
{
    /// <summary>An empty block shape.</summary>
    public static BlockShape Empty { get; } = new();

    /// <summary>A shape that fills one block.</summary>
    public static BlockShape FullBlock { get; } = new(new Box3D<double>(0, 0, 0, 1, 1, 1));

    private readonly IReadOnlyList<Box3D<double>> _boxes;

    /// <summary>
    /// Creates a block shape from block-local axis-aligned boxes.
    /// </summary>
    /// <param name="boxes">The boxes that compose the shape.</param>
    public BlockShape(params Box3D<double>[] boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        var copy = boxes.ToArray();
        foreach (var box in copy)
            ValidateBox(box);

        _boxes = Array.AsReadOnly(copy);
    }

    /// <inheritdoc/>
    public bool TryRaycast(in Ray3D<double> ray, double maxDistance, out BlockShapeHit hit)
    {
        var found = false;
        var nearest = default(BlockShapeHit);
        foreach (var box in _boxes)
        {
            if (!TryIntersectBox(box, ray, maxDistance, out var candidate)
                || found && candidate.Distance >= nearest.Distance)
            {
                continue;
            }

            found = true;
            nearest = candidate;
        }

        hit = nearest;
        return found;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Box3D<double>> GetSelectionBoxes()
    {
        return _boxes;
    }

    private static bool TryIntersectBox(
        Box3D<double> box,
        in Ray3D<double> ray,
        double maxDistance,
        out BlockShapeHit hit)
    {
        var startsInside = ContainsStrictly(box, ray.Origin);
        var enter = double.NegativeInfinity;
        var exit = double.PositiveInfinity;
        var enterFace = default(Direction);

        if (!IntersectAxis(ray.Origin.X, ray.Direction.X, box.Min.X, box.Max.X, Direction.West, Direction.East,
                ref enter, ref exit, ref enterFace)
            || !IntersectAxis(ray.Origin.Y, ray.Direction.Y, box.Min.Y, box.Max.Y, Direction.Down, Direction.Up,
                ref enter, ref exit, ref enterFace)
            || !IntersectAxis(ray.Origin.Z, ray.Direction.Z, box.Min.Z, box.Max.Z, Direction.North, Direction.South,
                ref enter, ref exit, ref enterFace))
        {
            hit = default;
            return false;
        }

        var distance = startsInside ? 0 : Math.Max(enter, 0);
        if (exit <= distance || distance > maxDistance)
        {
            hit = default;
            return false;
        }

        var face = startsInside ? GetOppositeDominantFace(ray.Direction) : enterFace;
        hit = new BlockShapeHit(
            ray.GetPoint(distance),
            face,
            distance,
            startsInside);
        return true;
    }

    private static bool IntersectAxis(
        double origin,
        double direction,
        double min,
        double max,
        Direction negativeFace,
        Direction positiveFace,
        ref double enter,
        ref double exit,
        ref Direction enterFace)
    {
        if (direction == 0)
            return origin >= min && origin <= max;

        var first = (min - origin) / direction;
        var second = (max - origin) / direction;
        var near = Math.Min(first, second);
        var far = Math.Max(first, second);
        if (near > enter)
        {
            enter = near;
            enterFace = direction > 0 ? negativeFace : positiveFace;
        }

        exit = Math.Min(exit, far);
        return enter <= exit;
    }

    private static bool ContainsStrictly(Box3D<double> box, Vector3D<double> point)
    {
        return point.X > box.Min.X && point.X < box.Max.X
                                   && point.Y > box.Min.Y && point.Y < box.Max.Y
                                   && point.Z > box.Min.Z && point.Z < box.Max.Z;
    }

    private static Direction GetOppositeDominantFace(Vector3D<double> direction)
    {
        var absolute = Vector3D.Abs(direction);
        var x = absolute.X;
        var y = absolute.Y;
        var z = absolute.Z;
        if (x >= y && x >= z)
            return direction.X >= 0 ? Direction.West : Direction.East;
        if (y >= z)
            return direction.Y >= 0 ? Direction.Down : Direction.Up;
        return direction.Z >= 0 ? Direction.North : Direction.South;
    }

    private static void ValidateBox(Box3D<double> box)
    {
        if (!IsFinite(box.Min) || !IsFinite(box.Max)
                               || !IsUnitCoordinate(box.Min) || !IsUnitCoordinate(box.Max)
                               || box.Min.X >= box.Max.X
                               || box.Min.Y >= box.Max.Y
                               || box.Min.Z >= box.Max.Z)
        {
            throw new ArgumentOutOfRangeException(nameof(box));
        }
    }

    private static bool IsFinite(Vector3D<double> value)
    {
        return double.IsFinite(value.X)
               && double.IsFinite(value.Y)
               && double.IsFinite(value.Z);
    }

    private static bool IsUnitCoordinate(Vector3D<double> value)
    {
        return value.X is >= 0 and <= 1
               && value.Y is >= 0 and <= 1
               && value.Z is >= 0 and <= 1;
    }
}