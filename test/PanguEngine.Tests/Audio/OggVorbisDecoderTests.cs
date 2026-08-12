using PanguEngine.Audio.Decoding;

namespace PanguEngine.Tests.Audio;

public sealed class OggVorbisDecoderTests
{
    private static string GetAssetPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", fileName);

    [Theory]
    [InlineData(-1f, short.MinValue)]
    [InlineData(0f, (short)0)]
    [InlineData(1f, short.MaxValue)]
    public void ConvertSample_MapsPcm16Endpoints(float input, short expected)
    {
        Assert.Equal(expected, OggVorbisDecoder.ConvertSample(input));
    }

    [Theory]
    [InlineData(-2f, short.MinValue)]
    [InlineData(2f, short.MaxValue)]
    public void ConvertSample_ClampsOutOfRangeValues(float input, short expected)
    {
        Assert.Equal(expected, OggVorbisDecoder.ConvertSample(input));
    }

    [Theory]
    [InlineData(0.5f, (short)16384)]
    [InlineData(-0.5f, (short)(-16384))]
    public void ConvertSample_RoundsAwayFromZero(float input, short expected)
    {
        Assert.Equal(expected, OggVorbisDecoder.ConvertSample(input));
    }

    [Fact]
    public void Decode_NonOggStream_ThrowsAndLeavesStreamOpen()
    {
        using var stream = new MemoryStream("not an ogg container"u8.ToArray());

        Assert.Throws<ArgumentException>(() => new OggVorbisDecoder().Decode(stream));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Decode_EmptyStream_Throws()
    {
        using var stream = new MemoryStream();

        Assert.Throws<ArgumentException>(() => new OggVorbisDecoder().Decode(stream));
    }

    [Fact]
    public void Decode_Ogg_ReturnsChannelAlignedPcm()
    {
        using var stream = File.OpenRead(GetAssetPath("break_1.ogg"));

        var data = new OggVorbisDecoder().Decode(stream);

        Assert.InRange(data.Channels, 1, 2);
        Assert.True(data.SampleRate > 0);
        Assert.NotEmpty(data.Samples);
        Assert.Equal(0, data.Samples.Length % data.Channels);
    }

    [Fact]
    public void Decode_LeavesInputStreamOpen()
    {
        using var stream = File.OpenRead(GetAssetPath("break_1.ogg"));

        _ = new OggVorbisDecoder().Decode(stream);

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void PcmAudioData_RejectsInvalidShapes()
    {
        Assert.Throws<ArgumentException>(() => new PcmAudioData(Array.Empty<short>(), 1, 44100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmAudioData(new short[] { 0 }, 0, 44100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmAudioData(new short[] { 0 }, 3, 44100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PcmAudioData(new short[] { 0 }, 1, 0));
        Assert.Throws<ArgumentException>(() => new PcmAudioData(new short[] { 0, 0, 0 }, 2, 44100));
    }
}
