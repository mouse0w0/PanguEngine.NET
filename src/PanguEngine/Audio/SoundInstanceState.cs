namespace PanguEngine.Audio;

/// <summary>
/// Identifies the lifecycle state of a tracked sound instance.
/// </summary>
public enum SoundInstanceState
{
    /// <summary>The sound is actively playing.</summary>
    Playing,

    /// <summary>The sound is paused and retains its playback source.</summary>
    Paused,

    /// <summary>The sound reached its natural end.</summary>
    Completed,

    /// <summary>The sound was explicitly stopped.</summary>
    Stopped,

    /// <summary>The sound source was reassigned to a more audible request.</summary>
    Stolen,

    /// <summary>The sound could not be started.</summary>
    Rejected
}
