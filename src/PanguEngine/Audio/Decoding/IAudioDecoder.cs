namespace PanguEngine.Audio.Decoding;

internal interface IAudioDecoder
{
    PcmAudioData Decode(Stream stream);
}
