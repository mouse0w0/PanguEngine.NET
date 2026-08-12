using PanguEngine.Audio.Decoding;

namespace PanguEngine.Audio.Backend;

internal interface IAudioBackend
{
    bool IsAvailable { get; }
    int SourceCapacity { get; }
    AudioBufferHandle CreateBuffer(PcmAudioData data);
    void DestroyBuffer(AudioBufferHandle buffer);
    bool TryRentSource(out AudioSourceHandle source);
    void Play(AudioSourceHandle source, AudioBufferHandle buffer, AudioSourceSettings settings);
    void Pause(AudioSourceHandle source);
    void Resume(AudioSourceHandle source);
    void Stop(AudioSourceHandle source);
    void Update(AudioSourceHandle source, AudioSourceSettings settings);
    AudioBackendSourceState GetState(AudioSourceHandle source);
    void ReturnSource(AudioSourceHandle source);
    void SetListener(AudioListenerState listener);
    void Destroy();
}
