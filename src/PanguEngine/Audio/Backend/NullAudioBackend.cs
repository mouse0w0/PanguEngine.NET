using PanguEngine.Audio.Decoding;

namespace PanguEngine.Audio.Backend;

internal sealed class NullAudioBackend : IAudioBackend
{
    public bool IsAvailable => false;
    public int SourceCapacity => 0;

    public AudioBufferHandle CreateBuffer(PcmAudioData data) =>
        throw new InvalidOperationException("The null audio backend does not create buffers.");

    public void DestroyBuffer(AudioBufferHandle buffer)
    {
    }

    public bool TryRentSource(out AudioSourceHandle source)
    {
        source = default;
        return false;
    }

    public void Play(AudioSourceHandle source, AudioBufferHandle buffer, AudioSourceSettings settings)
    {
    }

    public void Pause(AudioSourceHandle source)
    {
    }

    public void Resume(AudioSourceHandle source)
    {
    }

    public void Stop(AudioSourceHandle source)
    {
    }

    public void Update(AudioSourceHandle source, AudioSourceSettings settings)
    {
    }

    public AudioBackendSourceState GetState(AudioSourceHandle source) => AudioBackendSourceState.Stopped;

    public void ReturnSource(AudioSourceHandle source)
    {
    }

    public void SetListener(AudioListenerState listener)
    {
    }

    public void Destroy()
    {
    }
}
