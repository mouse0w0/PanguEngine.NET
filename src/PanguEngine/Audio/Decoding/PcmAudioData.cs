namespace PanguEngine.Audio.Decoding;

internal sealed record PcmAudioData
{
    public PcmAudioData(short[] Samples, int Channels, int SampleRate)
    {
        ArgumentNullException.ThrowIfNull(Samples);
        if (Samples.Length == 0)
            throw new ArgumentException("Samples must not be empty.", nameof(Samples));
        if (Channels is not 1 and not 2)
            throw new ArgumentOutOfRangeException(nameof(Channels), Channels, "Channels must be 1 or 2.");
        if (SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(SampleRate), SampleRate, "Sample rate must be positive.");
        if (Samples.Length % Channels != 0)
            throw new ArgumentException("Samples length must be aligned to the channel count.", nameof(Samples));

        this.Samples = Samples;
        this.Channels = Channels;
        this.SampleRate = SampleRate;
    }

    internal short[] Samples { get; }
    internal int Channels { get; }
    internal int SampleRate { get; }
}
