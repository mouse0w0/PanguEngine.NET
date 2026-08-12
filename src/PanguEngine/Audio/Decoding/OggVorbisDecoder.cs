using NVorbis;

namespace PanguEngine.Audio.Decoding;

internal sealed class OggVorbisDecoder : IAudioDecoder
{
    private const int BufferSize = 4096;

    public PcmAudioData Decode(Stream stream)
    {
        using var reader = new VorbisReader(stream, closeOnDispose: false);
        var channels = reader.Channels;
        var sampleRate = reader.SampleRate;
        if (channels is not 1 and not 2)
            throw new InvalidDataException($"Unsupported Vorbis channel count: {channels}.");
        if (sampleRate <= 0)
            throw new InvalidDataException($"Invalid Vorbis sample rate: {sampleRate}.");

        var buffer = new float[BufferSize - BufferSize % channels];
        var samples = new List<short>();

        int count;
        while ((count = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < count; i++)
                samples.Add(ConvertSample(buffer[i]));
        }

        return new PcmAudioData(samples.ToArray(), channels, sampleRate);
    }

    internal static short ConvertSample(float sample)
    {
        if (sample <= -1f)
            return short.MinValue;
        if (sample >= 1f)
            return short.MaxValue;
        return (short)MathF.Round(sample * short.MaxValue, MidpointRounding.AwayFromZero);
    }
}
