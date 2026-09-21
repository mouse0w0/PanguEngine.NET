using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Registries;

namespace PanguEngine.Tests.Client.UI;

public sealed class HudRegistrationTests
{
    [Fact]
    public void EmptyRegistryInitializesWithEmptyHudRoot()
    {
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Freeze();
        var manager = new UiManager();

        try
        {
            manager.InitializeHud(definitions);

            Assert.Empty(HudRoot(manager).Children);
            Assert.Throws<KeyNotFoundException>(
                () => manager.Hud.Get(ResourceKey.Create("pangu", "missing")));
            Assert.False(manager.Hud.TryGet(ResourceKey.Create("pangu", "missing"), out var hud));
            Assert.Null(hud);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void FactoryCannotReenterManagerLifecycleOperations()
    {
        var manager = new UiManager();
        var screen = new UiScreen(new Panel());
        manager.Open(screen);
        var key = ResourceKey.Create("test", "hud");
        var calls = 0;
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(key, new HudDefinition(() =>
        {
            calls++;
            Assert.Throws<InvalidOperationException>(() => manager.InitializeHud(definitions));
            Assert.Throws<InvalidOperationException>(() => manager.Open(new UiScreen()));
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(manager.Destroy);
            Assert.Throws<InvalidOperationException>(manager.Update);
            Assert.Throws<InvalidOperationException>(() => manager.PrepareFrame(new Size(10, 10), 0));
            Assert.Throws<InvalidOperationException>(() => manager.AppendDrawCommands(new UiDrawCommandList()));
            return new RecordingHud();
        }));
        definitions.Freeze();

        try
        {
            manager.InitializeHud(definitions);

            Assert.Equal(1, calls);
            Assert.Same(screen, manager.CurrentScreen);
            Assert.IsType<RecordingHud>(manager.Hud.Get(key));
            manager.Update();
            manager.PrepareFrame(new Size(10, 10), 0);
            Assert.True(screen.Root!.IsArrangeValid);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void QueriesAfterCloseThrowInvalidOperation()
    {
        var key = ResourceKey.Create("test", "hud");
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(key, new HudDefinition(static () => new RecordingHud()));
        definitions.Freeze();
        var manager = new UiManager();
        manager.InitializeHud(definitions);

        manager.Destroy();

        Assert.Throws<InvalidOperationException>(() => manager.Hud.Get(key));
        Assert.Throws<InvalidOperationException>(() => manager.Hud.TryGet(key, out _));
    }

    [Fact]
    public void UnfrozenRegistryIsRejectedWithoutBreakingInitialization()
    {
        var key = ResourceKey.Create("test", "hud");
        var component = new RecordingHud();
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(key, new HudDefinition(() => component));
        var manager = new UiManager();

        try
        {
            Assert.Throws<InvalidOperationException>(() => manager.InitializeHud(definitions));
            Assert.Empty(HudRoot(manager).Children);

            definitions.Freeze();
            manager.InitializeHud(definitions);

            Assert.Same(component, manager.Hud.Get(key));
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void EachInitializationCreatesIndependentInstances()
    {
        var calls = 0;
        var key = ResourceKey.Create("test", "hud");
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(key, new HudDefinition(() =>
        {
            calls++;
            return new RecordingHud();
        }));
        definitions.Freeze();
        var first = new UiManager();
        var second = new UiManager();

        try
        {
            first.InitializeHud(definitions);
            second.InitializeHud(definitions);

            Assert.Equal(2, calls);
            var firstHud = first.Hud.Get(key);
            var secondHud = second.Hud.Get(key);
            Assert.NotSame(firstHud, secondHud);
            Assert.NotSame(firstHud.Root, secondHud.Root);
            Assert.True(first.Hud.TryGet(key, out var found));
            Assert.Same(firstHud, found);
            Assert.Same(firstHud.Root, Assert.Single(HudRoot(first).Children));
            Assert.Same(secondHud.Root, Assert.Single(HudRoot(second).Children));
            Assert.Same(first.Hud.Screen, firstHud.Root.Screen);
        }
        finally
        {
            first.Destroy();
            second.Destroy();
        }
    }

    [Fact]
    public void NullFactoryResultFailsInitializationWithEntryKey()
    {
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "null"), new HudDefinition(() => null!));
        definitions.Freeze();
        var manager = new UiManager();

        var error = Assert.Throws<InvalidOperationException>(() => manager.InitializeHud(definitions));

        Assert.Contains("test:null", error.Message);
        Assert.Empty(HudRoot(manager).Children);
    }

    [Fact]
    public void DuplicateFactoryInstanceFailsInitialization()
    {
        var shared = new RecordingHud();
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "first"), new HudDefinition(() => shared));
        definitions.Register(ResourceKey.Create("test", "second"), new HudDefinition(() => shared));
        definitions.Freeze();
        var manager = new UiManager();

        Assert.Throws<InvalidOperationException>(() => manager.InitializeHud(definitions));

        Assert.Equal(0, shared.DestroyCalls);
        Assert.Same(shared.Root, Assert.Single(HudRoot(manager).Children));
    }

    [Fact]
    public void AlreadyMountedRootIsRejectedWithoutStealingIt()
    {
        var parent = new Panel();
        var mounted = new Panel();
        parent.Children.Add(mounted);
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(
            ResourceKey.Create("test", "mounted"),
            new HudDefinition(() => new RecordingHud(root: mounted)));
        definitions.Freeze();
        var manager = new UiManager();

        Assert.Throws<InvalidOperationException>(() => manager.InitializeHud(definitions));

        Assert.Same(parent, mounted.Parent);
        Assert.Same(mounted, Assert.Single(parent.Children));
        Assert.Empty(HudRoot(manager).Children);
    }

    [Fact]
    public void FailedInitializationPropagatesWithoutRollbackOrCallingLaterFactories()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("factory failure");
        var first = new RecordingHud(events: events, name: "first");
        var second = new RecordingHud(events: events, name: "second");
        var laterFactoryCalled = false;
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "first"), new HudDefinition(() => first));
        definitions.Register(ResourceKey.Create("test", "second"), new HudDefinition(() => second));
        definitions.Register(ResourceKey.Create("test", "third"), new HudDefinition(() => throw expected));
        definitions.Register(ResourceKey.Create("test", "fourth"), new HudDefinition(() =>
        {
            laterFactoryCalled = true;
            return new RecordingHud();
        }));
        definitions.Freeze();
        var manager = new UiManager();

        var actual = Assert.Throws<InvalidOperationException>(() => manager.InitializeHud(definitions));

        Assert.Same(expected, actual);
        Assert.False(laterFactoryCalled);
        Assert.Empty(events);
        Assert.Equal(0, first.DestroyCalls);
        Assert.Equal(0, second.DestroyCalls);
        Assert.Equal([first.Root, second.Root], HudRoot(manager).Children);
    }

    [Fact]
    public void ComponentsUpdateInRegistrationOrder()
    {
        var events = new List<string>();
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "first"), new HudDefinition(
            () => new RecordingHud(events: events, name: "first")));
        definitions.Register(ResourceKey.Create("test", "second"), new HudDefinition(
            () => new RecordingHud(events: events, name: "second")));
        definitions.Freeze();
        var manager = new UiManager();

        try
        {
            manager.InitializeHud(definitions);

            manager.Update();
            manager.PrepareFrame(new Size(200, 100), 0.5);

            Assert.Equal(
                ["first:fixed", "second:fixed", "first:frame:0.5", "second:frame:0.5"],
                events);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void HiddenAndDetachedRootsStillReceiveUpdates()
    {
        var events = new List<string>();
        var key = ResourceKey.Create("test", "hud");
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        RecordingHud? hud = null;
        definitions.Register(key, new HudDefinition(() => hud = new RecordingHud(events: events, name: "hud")));
        definitions.Freeze();
        var manager = new UiManager();

        try
        {
            manager.InitializeHud(definitions);
            var component = Assert.IsType<RecordingHud>(hud);
            component.Root.Visibility = Visibility.Hidden;

            manager.Update();
            manager.PrepareFrame(new Size(200, 100), 0);

            Assert.Contains("hud:fixed", events);
            Assert.Contains("hud:frame:0", events);

            var container = new Panel();
            container.Children.Add(component.Root);
            container.Children.Clear();
            events.Clear();

            manager.Update();
            manager.PrepareFrame(new Size(200, 100), 0);

            Assert.Contains("hud:fixed", events);
            Assert.Contains("hud:frame:0", events);
            Assert.Same(component, manager.Hud.Get(key));
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void DestroyInvokesComponentsInReverseOrderOnce()
    {
        var events = new List<string>();
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "first"), new HudDefinition(
            () => new RecordingHud(events: events, name: "first")));
        definitions.Register(ResourceKey.Create("test", "second"), new HudDefinition(
            () => new RecordingHud(events: events, name: "second")));
        definitions.Register(ResourceKey.Create("test", "third"), new HudDefinition(
            () => new RecordingHud(events: events, name: "third")));
        definitions.Freeze();
        var manager = new UiManager();
        manager.InitializeHud(definitions);

        manager.Destroy();
        manager.Destroy();

        Assert.Equal(["third:destroy", "second:destroy", "first:destroy"], events);
    }

    [Fact]
    public void DestroyStopsAtFirstComponentFailure()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("destroy failure");
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "first"), new HudDefinition(
            () => new RecordingHud(events: events, name: "first")));
        definitions.Register(ResourceKey.Create("test", "second"), new HudDefinition(
            () => new RecordingHud(events: events, name: "second")
            {
                DestroyAction = () => throw expected
            }));
        definitions.Register(ResourceKey.Create("test", "third"), new HudDefinition(
            () => new RecordingHud(events: events, name: "third")));
        definitions.Freeze();
        var manager = new UiManager();
        manager.InitializeHud(definitions);

        var actual = Assert.Throws<InvalidOperationException>(manager.Destroy);

        Assert.Same(expected, actual);
        Assert.Equal(["third:destroy", "second:destroy"], events);
        Assert.True(manager.Hud.Screen.IsOpen());
        Assert.Equal(3, HudRoot(manager).Children.Count);
        Assert.Throws<InvalidOperationException>(() => manager.Hud.Post(static () => { }));
    }

    [Fact]
    public void ClosingSealsPostAndDiscardsPendingActions()
    {
        var events = new List<string>();
        var postError = default(Exception?);
        var backgroundError = default(Exception?);
        var manager = new UiManager();
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "hud"), new HudDefinition(() =>
            new RecordingHud(events: events, name: "hud")
            {
                DestroyAction = () =>
                {
                    postError = Record.Exception(() => manager.Hud.Post(() => events.Add("posted")));
                    var thread = new Thread(() =>
                        backgroundError = Record.Exception(() => manager.Hud.Post(static () => { })));
                    thread.Start();
                    thread.Join();
                }
            }));
        definitions.Freeze();

        try
        {
            manager.InitializeHud(definitions);
            var posted = false;
            manager.Hud.Post(() => posted = true);

            manager.Destroy();

            Assert.False(posted);
            Assert.IsType<InvalidOperationException>(postError);
            Assert.IsType<InvalidOperationException>(backgroundError);
            Assert.DoesNotContain("posted", events);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComponentUpdatesRejectSynchronousScreenChangesAndDestruction(bool fixedUpdate)
    {
        var events = new List<string>();
        var manager = new UiManager();
        var screen = new UiScreen(new Panel());
        void Check()
        {
            Assert.Throws<InvalidOperationException>(() => manager.Open(new UiScreen()));
            Assert.Throws<InvalidOperationException>(manager.Close);
            Assert.Throws<InvalidOperationException>(manager.Destroy);
        }
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "first"), new HudDefinition(() =>
            new RecordingHud(events: events, name: "first")
            {
                FixedAction = Check,
                FrameAction = _ => Check()
            }));
        definitions.Register(ResourceKey.Create("test", "second"), new HudDefinition(
            () => new RecordingHud(events: events, name: "second")));
        definitions.Freeze();

        try
        {
            manager.InitializeHud(definitions);
            manager.Open(screen);

            if (fixedUpdate)
                manager.Update();
            else
                manager.PrepareFrame(new Size(100, 100), 0);

            Assert.Same(screen, manager.CurrentScreen);
            string[] expected = fixedUpdate
                ? ["first:fixed", "second:fixed"]
                : ["first:frame:0", "second:frame:0"];
            Assert.Equal(expected, events);
        }
        finally
        {
            manager.Destroy();
        }
    }

    private static Panel HudRoot(UiManager manager) =>
        Assert.IsType<Panel>(manager.Hud.Screen.Root);

    private sealed class RecordingHud(UiNode? root = null, List<string>? events = null, string name = "hud")
        : Hud(root ?? new Panel())
    {
        internal string Name { get; } = name;

        internal int DestroyCalls { get; private set; }

        internal Action? DestroyAction { get; init; }

        internal Action? FixedAction { get; init; }

        internal Action<double>? FrameAction { get; init; }

        protected override void OnFixedUpdate()
        {
            events?.Add($"{Name}:fixed");
            FixedAction?.Invoke();
        }

        protected override void OnFrameUpdate(double alpha)
        {
            events?.Add(FormattableString.Invariant($"{Name}:frame:{alpha}"));
            FrameAction?.Invoke(alpha);
        }

        protected override void OnDestroy()
        {
            DestroyCalls++;
            events?.Add($"{Name}:destroy");
            DestroyAction?.Invoke();
        }
    }
}
