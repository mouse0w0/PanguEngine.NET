namespace PanguEngine.Audio;

internal static class AudioSeedDeriver
{
    private const ulong VariantSalt = 0x9E3779B97F4A7C15;
    private const ulong VolumeSalt = 0xD1B54A32D192ED03;
    private const ulong PitchSalt = 0x94D049BB133111EB;
    private const float UnitSingleScale = 1f / 16777216f;

    internal static long DeriveVariant(long seed, long exclusiveMaximum)
    {
        var bound = (ulong)exclusiveMaximum;
        var threshold = unchecked(0UL - bound) % bound;
        var bits = Mix(unchecked((ulong)seed + VariantSalt));
        while (bits < threshold)
            bits = Mix(unchecked(bits + VariantSalt));
        return (long)(bits % bound);
    }

    internal static float DeriveVolume(long seed) =>
        ToUnitSingle(Mix(unchecked((ulong)seed + VolumeSalt)));

    internal static float DerivePitch(long seed) =>
        ToUnitSingle(Mix(unchecked((ulong)seed + PitchSalt)));

    internal static ulong Mix(ulong value)
    {
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EB);
        return value ^ (value >> 31);
    }

    internal static float ToUnitSingle(ulong bits) =>
        (bits >> 40) * UnitSingleScale;
}
