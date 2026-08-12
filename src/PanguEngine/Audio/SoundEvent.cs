namespace PanguEngine.Audio;

/// <summary>
/// Identifies a registered sound event and its playback policy.
/// </summary>
public sealed class SoundEvent
{
    /// <summary>
    /// Creates a sound event definition.
    /// </summary>
    /// <param name="category">The category used for volume control.</param>
    public SoundEvent(SoundCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        Category = category;
    }

    /// <summary>The category used for volume control.</summary>
    public SoundCategory Category { get; }
}
