using PanguEngine.Audio;
using PanguEngine.Registries;
using Silk.NET.Maths;

namespace PanguEngine.Tests.Audio;

public sealed class SoundEventTests
{
    [Fact]
    public void BuiltinSoundCategories_RegisterAllCategories()
    {
        var registry = new Registry<SoundCategory>(ResourceKey.Parse("test:sound_category"));

        BuiltinSoundCategories.Register(registry);

        Assert.Equal(5, registry.Count);
        Assert.Same(BuiltinSoundCategories.Music, registry.Get(ResourceKey.Parse("pangu:music")));
        Assert.Same(BuiltinSoundCategories.Ambient, registry.Get(ResourceKey.Parse("pangu:ambient")));
        Assert.Same(BuiltinSoundCategories.SoundEffects, registry.Get(ResourceKey.Parse("pangu:sound_effects")));
        Assert.Same(BuiltinSoundCategories.UserInterface, registry.Get(ResourceKey.Parse("pangu:user_interface")));
        Assert.Same(BuiltinSoundCategories.Voice, registry.Get(ResourceKey.Parse("pangu:voice")));
    }

    [Fact]
    public void SoundCategories_ConfigureUiPauseBehavior()
    {
        Assert.False(new SoundCategory().IgnoresUiPause);
        Assert.True(new SoundCategory(ignoresUiPause: true).IgnoresUiPause);
        Assert.True(BuiltinSoundCategories.Music.IgnoresUiPause);
        Assert.False(BuiltinSoundCategories.Ambient.IgnoresUiPause);
        Assert.False(BuiltinSoundCategories.SoundEffects.IgnoresUiPause);
        Assert.True(BuiltinSoundCategories.UserInterface.IgnoresUiPause);
        Assert.False(BuiltinSoundCategories.Voice.IgnoresUiPause);
    }

    [Fact]
    public void SoundEvent_StoresCategory()
    {
        var soundEvent = new SoundEvent(BuiltinSoundCategories.SoundEffects);

        Assert.Same(BuiltinSoundCategories.SoundEffects, soundEvent.Category);
    }

    [Fact]
    public void SoundEvent_RejectsNullCategory()
    {
        Assert.Throws<ArgumentNullException>(() => new SoundEvent(null!));
    }

    [Fact]
    public void Listener_RejectsDegenerateOrientation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AudioListenerState(Vector3D<double>.Zero, Vector3D<double>.Zero, Vector3D<double>.UnitY));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AudioListenerState(Vector3D<double>.Zero, Vector3D<double>.UnitY, Vector3D<double>.UnitY));
    }
}
