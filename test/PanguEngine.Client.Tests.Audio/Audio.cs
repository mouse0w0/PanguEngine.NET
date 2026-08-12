using PanguEngine.Audio;
using PanguEngine.Client.Tests;
using PanguEngine.Graphics;
using PanguEngine.Input;
using PanguEngine.Registries;
using PanguEngine.Windowing;
using Silk.NET.Maths;
using System.Diagnostics;

namespace PanguEngine.Client.Tests.Audio;

internal static class Audio
{
    private static void Main()
    {
        ClientTestApp.Run(new AudioScene());
    }
}

internal sealed class AudioScene : IClientTestScene
{
    private static readonly SoundEvent TestEvent = new(BuiltinSoundCategories.SoundEffects);

    private Presenter _presenter = null!;
    private SoundInstance? _loopingInstance;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public string Name => "Audio Test";
    public bool RequiresAudio => true;

    public void ConfigureBeforeEngineInitialize()
    {
        BuiltinRegistries.SoundEvent.Register(ResourceKey.Parse("audiotest:test"), TestEvent);
    }

    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        window.KeyDown += OnKeyDown;
        window.Render += (_, _) => DrawFrame();
    }

    public void Destroy()
    {
        _loopingInstance?.Stop();
    }

    private void OnKeyDown(Window window, KeyEventArgs args)
    {
        if (args.Action != KeyAction.Press)
            return;

        var audio = ClientTestApp.Current.Audio;
        switch (args.Key)
        {
            case Key.Number1:
                audio.Play(TestEvent);
                break;
            case Key.Number2:
                audio.PlayAt(TestEvent, new Vector3D<double>(0, 0, -4));
                break;
            case Key.Number3:
                audio.PlayAt(TestEvent, new Vector3D<double>(0, 0, 4));
                break;
            case Key.Number4:
                audio.PlayAt(TestEvent, new Vector3D<double>(-4, 0, 0));
                break;
            case Key.Number5:
                audio.PlayAt(TestEvent, new Vector3D<double>(4, 0, 0));
                break;
            case Key.L:
                if (_loopingInstance is { } existing && (existing.IsPlaying || existing.IsPaused))
                {
                    existing.Stop();
                    _loopingInstance = null;
                }
                else
                {
                    _loopingInstance = audio.PlayTrackedAt(
                        TestEvent,
                        new Vector3D<double>(0, 0, -4),
                        looping: true);
                }
                break;
            case Key.P:
                if (_loopingInstance is { } looping && looping.IsPlaying)
                    looping.Pause();
                else if (_loopingInstance is { } paused && paused.IsPaused)
                    paused.Resume();
                break;
            case Key.C:
                if (audio.IsCategoryPaused(BuiltinSoundCategories.SoundEffects))
                    audio.ResumeCategories(BuiltinSoundCategories.SoundEffects);
                else
                    audio.PauseCategories(BuiltinSoundCategories.SoundEffects);
                break;
            case Key.U:
                audio.IsUiPaused = !audio.IsUiPaused;
                break;
            case Key.B:
                if (audio.IsCategoryPaused(BuiltinSoundCategories.SoundEffects)
                    || audio.IsCategoryPaused(BuiltinSoundCategories.Ambient))
                {
                    audio.ResumeCategories(BuiltinSoundCategories.SoundEffects, BuiltinSoundCategories.Ambient);
                }
                else
                {
                    audio.PauseCategories(BuiltinSoundCategories.SoundEffects, BuiltinSoundCategories.Ambient);
                }
                break;
            case Key.Up:
                audio.SetCategoryVolume(
                    BuiltinSoundCategories.SoundEffects,
                    Math.Min(1, audio.GetCategoryVolume(BuiltinSoundCategories.SoundEffects) + 0.1f));
                break;
            case Key.Down:
                audio.SetCategoryVolume(
                    BuiltinSoundCategories.SoundEffects,
                    Math.Max(0, audio.GetCategoryVolume(BuiltinSoundCategories.SoundEffects) - 0.1f));
                break;
            case Key.Space:
                for (var i = 0; i < 80; i++)
                    audio.PlayTracked(TestEvent, looping: i % 2 == 0);
                break;
        }
    }

    private void DrawFrame()
    {
        if (_loopingInstance is { IsPlaying: true } looping)
        {
            var angle = _stopwatch.Elapsed.TotalSeconds;
            looping.Position = new Vector3D<double>(Math.Cos(angle) * 4, 0, Math.Sin(angle) * 4);
        }

        if (!_presenter.TryBeginFrame(out var frame))
            return;

        try
        {
            var commands = frame.CommandList;
            commands.BeginRecording();
            commands.BeginRendering(new RenderingDescription
            {
                Width = frame.Width,
                Height = frame.Height,
                ColorAttachments =
                [
                    new ColorAttachmentDescription(frame.ColorOutput, new ClearColor(0.03f, 0.03f, 0.03f, 1))
                ]
            });
            commands.EndRendering();
            commands.PrepareForPresent(frame.ColorOutput);
            commands.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }
}
