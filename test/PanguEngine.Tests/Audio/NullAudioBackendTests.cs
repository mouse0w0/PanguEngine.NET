using PanguEngine.Audio;
using PanguEngine.Audio.Backend;
using PanguEngine.Audio.Decoding;

namespace PanguEngine.Tests.Audio;

public sealed class NullAudioBackendTests
{
    [Fact]
    public void Backend_IsUnavailableAndHasNoSources()
    {
        var backend = new NullAudioBackend();

        Assert.False(backend.IsAvailable);
        Assert.Equal(0, backend.SourceCapacity);
        Assert.False(backend.TryRentSource(out var source));
        Assert.Equal(default, source);
    }

    [Fact]
    public void CreateBuffer_IsNotSupported()
    {
        var backend = new NullAudioBackend();
        var data = new PcmAudioData([0], 1, 44100);

        Assert.Throws<InvalidOperationException>(() => backend.CreateBuffer(data));
    }

    [Fact]
    public void PauseResume_AreNoOp()
    {
        var backend = new NullAudioBackend();

        backend.Pause(default);
        backend.Resume(default);
    }

    [Fact]
    public void Destroy_IsIdempotent()
    {
        var backend = new NullAudioBackend();

        backend.Destroy();
        backend.Destroy();
    }
}
