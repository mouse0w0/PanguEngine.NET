using PanguEngine.World.Blocks;
using PanguEngine.World.Chunking;
using Silk.NET.Maths;

namespace PanguEngine.World.Interaction;

/// <summary>
/// Provides block coordinate traversal and selection shape raycasts.
/// </summary>
public static class BlockRaycaster
{
    /// <summary>
    /// Enumerates block coordinates intersected by a world-space ray.
    /// </summary>
    /// <param name="ray">The world-space ray.</param>
    /// <param name="maxDistance">The maximum ray distance in world units.</param>
    /// <returns>The intersected block coordinates in traversal order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The ray is not finite, its direction is zero, or the maximum distance is not finite or is negative.
    /// </exception>
    public static IEnumerable<BlockPos> Traverse(
        in Ray3D<double> ray,
        double maxDistance)
    {
        var normalizedDirection = ValidateAndNormalize(ray, maxDistance);
        return TraverseCore(ray.Origin, normalizedDirection, maxDistance);
    }

    /// <summary>
    /// Finds the first block selection shape intersected by a ray.
    /// </summary>
    /// <param name="blockAccessor">The block state accessor.</param>
    /// <param name="ray">The world-space ray.</param>
    /// <param name="maxDistance">The maximum ray distance in world units.</param>
    /// <param name="hit">The first block hit when the method returns <see langword="true"/>.</param>
    /// <returns>Whether the ray intersects a block shape.</returns>
    public static bool TryRaycast(
        IReadOnlyBlockAccessor blockAccessor,
        in Ray3D<double> ray,
        double maxDistance,
        out BlockHit hit)
    {
        ArgumentNullException.ThrowIfNull(blockAccessor);
        var normalizedDirection = ValidateAndNormalize(ray, maxDistance);
        var found = false;
        var nearestHit = default(BlockShapeHit);
        var nearestPosition = default(BlockPos);
        BlockState? nearestState = null;

        foreach (var candidate in TraverseCore(ray.Origin, normalizedDirection, maxDistance))
        {
            var state = blockAccessor.GetBlock(candidate);
            var shape = state.GetSelectionShape(blockAccessor, candidate);
            var offset = new Vector3D<double>(
                candidate.X,
                candidate.Y,
                candidate.Z);
            var localRay = new Ray3D<double>(ray.Origin - offset, normalizedDirection);
            if (!shape.TryRaycast(localRay, maxDistance, out var shapeHit)
                || shapeHit.Distance < 0
                || shapeHit.Distance > maxDistance
                || found && !IsNearer(shapeHit, candidate, nearestHit, nearestPosition))
            {
                continue;
            }

            found = true;
            nearestHit = shapeHit;
            nearestPosition = candidate;
            nearestState = state;
        }

        if (!found)
        {
            hit = default;
            return false;
        }

        var worldPoint = nearestHit.Point + new Vector3D<double>(
            nearestPosition.X,
            nearestPosition.Y,
            nearestPosition.Z);
        hit = new BlockHit(
            nearestPosition,
            nearestState!,
            worldPoint,
            nearestHit.Face,
            nearestHit.Distance,
            nearestHit.IsInside);
        return true;
    }

    private static bool IsNearer(
        BlockShapeHit candidateHit,
        BlockPos candidatePosition,
        BlockShapeHit nearestHit,
        BlockPos nearestPosition)
    {
        if (candidateHit.Distance != nearestHit.Distance)
            return candidateHit.Distance < nearestHit.Distance;
        if (candidatePosition.X != nearestPosition.X)
            return candidatePosition.X < nearestPosition.X;
        if (candidatePosition.Y != nearestPosition.Y)
            return candidatePosition.Y < nearestPosition.Y;
        return candidatePosition.Z < nearestPosition.Z;
    }

    private static IEnumerable<BlockPos> TraverseCore(
        Vector3D<double> origin,
        Vector3D<double> direction,
        double maxDistance)
    {
        var axisX = CreateAxis(origin.X, direction.X);
        var axisY = CreateAxis(origin.Y, direction.Y);
        var axisZ = CreateAxis(origin.Z, direction.Z);
        if (!axisX.IsValid || !axisY.IsValid || !axisZ.IsValid)
            yield break;

        var entryDistance = 0d;
        while (entryDistance <= maxDistance)
        {
            var xCoordinates = GetCoordinates(origin.X, direction.X, axisX.Coordinate);
            var yCoordinates = GetCoordinates(origin.Y, direction.Y, axisY.Coordinate);
            var zCoordinates = GetCoordinates(origin.Z, direction.Z, axisZ.Coordinate);
            for (var xIndex = 0; xIndex < xCoordinates.Count; xIndex++)
            for (var yIndex = 0; yIndex < yCoordinates.Count; yIndex++)
            for (var zIndex = 0; zIndex < zCoordinates.Count; zIndex++)
            {
                yield return new BlockPos(
                    xCoordinates.GetValue(xIndex),
                    yCoordinates.GetValue(yIndex),
                    zCoordinates.GetValue(zIndex));
            }

            var nextDistance = Math.Min(axisX.NextDistance, Math.Min(axisY.NextDistance, axisZ.NextDistance));
            if (nextDistance > maxDistance)
                yield break;

            var moveX = axisX.NextDistance == nextDistance;
            var moveY = axisY.NextDistance == nextDistance;
            var moveZ = axisZ.NextDistance == nextDistance;
            if (moveX && !axisX.CanAdvance
                || moveY && !axisY.CanAdvance
                || moveZ && !axisZ.CanAdvance)
            {
                yield break;
            }

            if (moveX)
                axisX = axisX.Advance();
            if (moveY)
                axisY = axisY.Advance();
            if (moveZ)
                axisZ = axisZ.Advance();
            entryDistance = nextDistance;
        }
    }

    private static AxisCoordinates GetCoordinates(double origin, double direction, int coordinate)
    {
        if (direction != 0 || origin != Math.Floor(origin))
            return new AxisCoordinates(coordinate, 0, 1);

        var lower = origin - 1;
        var hasLower = IsBlockCoordinate(lower);
        var hasUpper = IsBlockCoordinate(origin);
        if (hasLower && hasUpper)
            return new AxisCoordinates((int)lower, (int)origin, 2);
        return new AxisCoordinates((int)(hasLower ? lower : origin), 0, 1);
    }

    private static AxisTraversal CreateAxis(double origin, double direction)
    {
        var floor = Math.Floor(origin);
        if (direction == 0)
        {
            var stationaryCoordinate = IsBlockCoordinate(floor) ? (int)floor : 0;
            var valid = IsBlockCoordinate(floor)
                        || origin == floor && IsBlockCoordinate(origin - 1);
            return new AxisTraversal(stationaryCoordinate, 0, double.PositiveInfinity, double.PositiveInfinity, valid);
        }

        var coordinateValue = direction < 0 && origin == floor ? floor - 1 : floor;
        if (!IsBlockCoordinate(coordinateValue))
            return default;

        var coordinate = (int)coordinateValue;
        var step = direction > 0 ? 1 : -1;
        var nextBoundary = direction > 0 ? (double)coordinate + 1 : coordinate;
        var nextDistance = (nextBoundary - origin) / direction;
        return new AxisTraversal(
            coordinate,
            step,
            nextDistance,
            1 / Math.Abs(direction),
            true);
    }

    private static bool IsBlockCoordinate(double coordinate)
    {
        return coordinate is >= int.MinValue and <= int.MaxValue;
    }

    private static Vector3D<double> ValidateAndNormalize(
        in Ray3D<double> ray,
        double maxDistance)
    {
        if (!IsFinite(ray.Origin))
            throw new ArgumentOutOfRangeException(nameof(ray));
        if (!double.IsFinite(maxDistance) || maxDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDistance));
        return NormalizeDirection(ray.Direction, nameof(ray));
    }

    private static Vector3D<double> NormalizeDirection(Vector3D<double> direction, string parameterName)
    {
        if (!IsFinite(direction))
            throw new ArgumentOutOfRangeException(parameterName);

        var absolute = Vector3D.Abs(direction);
        var scale = Math.Max(absolute.X, Math.Max(absolute.Y, absolute.Z));
        if (scale == 0)
            throw new ArgumentOutOfRangeException(parameterName);

        var normalized = Vector3D.Normalize(direction / scale);
        if (!IsFinite(normalized))
            throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static bool IsFinite(Vector3D<double> value)
    {
        return double.IsFinite(value.X)
               && double.IsFinite(value.Y)
               && double.IsFinite(value.Z);
    }

    private readonly record struct AxisTraversal(
        int Coordinate,
        int Step,
        double NextDistance,
        double DistanceIncrement,
        bool IsValid)
    {
        internal bool CanAdvance => Step > 0 ? Coordinate < int.MaxValue : Coordinate > int.MinValue;

        internal AxisTraversal Advance()
        {
            return this with
            {
                Coordinate = Coordinate + Step,
                NextDistance = NextDistance + DistanceIncrement
            };
        }
    }

    private readonly record struct AxisCoordinates(int First, int Second, int Count)
    {
        internal int GetValue(int index)
        {
            return index == 0 ? First : Second;
        }
    }
}