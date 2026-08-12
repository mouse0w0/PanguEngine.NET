namespace PanguEngine.Audio;

internal interface IAudioRandom
{
    long NextSeed();
}

internal sealed class AudioRandom(Random random) : IAudioRandom
{
    public long NextSeed() => random.NextInt64();
}
