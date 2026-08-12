using Silk.NET.Maths;

namespace PanguEngine.Audio.Backend;

internal readonly record struct AudioSourceSettings(
    bool IsRelative,
    bool IsLooping,
    Vector3D<float> Position,
    float Gain,
    float Pitch,
    float ReferenceDistance,
    float MaxDistance,
    float RolloffFactor);
