namespace PanguEngine.Audio;

/// <summary>
/// Identifies a registered category used to control related sounds.
/// </summary>
/// <param name="ignoresUiPause">Whether sounds in this category continue during a user interface pause.</param>
public sealed class SoundCategory(bool ignoresUiPause = false)
{
    /// <summary>Whether this category ignores a user interface pause.</summary>
    public bool IgnoresUiPause { get; } = ignoresUiPause;
}
