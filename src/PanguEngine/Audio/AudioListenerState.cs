using Silk.NET.Maths;

namespace PanguEngine.Audio;

/// <summary>
/// Describes the world-space position and orientation of the audio listener.
/// </summary>
public readonly record struct AudioListenerState
{
    /// <summary>
    /// Creates an audio listener state.
    /// </summary>
    /// <param name="position">The world-space listener position.</param>
    /// <param name="forward">The non-zero forward direction.</param>
    /// <param name="up">The non-zero up direction.</param>
    public AudioListenerState(
        Vector3D<double> position,
        Vector3D<double> forward,
        Vector3D<double> up)
    {
        if (!IsFinite(position))
            throw new ArgumentOutOfRangeException(nameof(position));
        ValidateDirection(forward, nameof(forward));
        ValidateDirection(up, nameof(up));

        var cross = Vector3D.Cross(ScaleDirection(forward), ScaleDirection(up));
        if (!IsFinite(cross) || LengthSquared(cross) <= 0)
            throw new ArgumentOutOfRangeException(nameof(up), "Listener directions must not be collinear.");

        Position = position;
        Forward = forward;
        Up = up;
    }

    /// <summary>The world-space listener position.</summary>
    public Vector3D<double> Position { get; }

    /// <summary>The listener forward direction.</summary>
    public Vector3D<double> Forward { get; }

    /// <summary>The listener up direction.</summary>
    public Vector3D<double> Up { get; }

    private static void ValidateDirection(Vector3D<double> direction, string parameterName)
    {
        if (!IsFinite(direction) || LengthSquared(direction) <= 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }

    private static Vector3D<double> ScaleDirection(Vector3D<double> direction)
    {
        var absolute = Vector3D.Abs(direction);
        var scale = Math.Max(absolute.X, Math.Max(absolute.Y, absolute.Z));
        return direction / scale;
    }

    private static double LengthSquared(Vector3D<double> value) =>
        value.X * value.X + value.Y * value.Y + value.Z * value.Z;

    private static bool IsFinite(Vector3D<double> value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
}
