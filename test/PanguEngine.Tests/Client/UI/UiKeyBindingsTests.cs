using System.Reflection;
using PanguEngine.Client.UI;
using PanguEngine.Input;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiKeyBindingsTests
{
    [Fact]
    public void PublicSurfaceExposesBindingRegistrationAndDispatch()
    {
        var openType = typeof(UiKeyBindings<>);
        var genericParameter = Assert.Single(openType.GetGenericArguments());
        var methods = openType.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.True(openType.IsPublic);
        Assert.True(openType.IsSealed);
        Assert.Single(openType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal([typeof(UiNode)], genericParameter.GetGenericParameterConstraints());
        Assert.Equal(3, methods.Count(method => method.Name == nameof(UiKeyBindings<UiNode>.AddBinding)));
        Assert.Single(methods, method => method.Name == nameof(UiKeyBindings<UiNode>.TryHandle));
    }

    [Fact]
    public void PressBindingRunsForMatchingKeyAndStopsAncestorRoute()
    {
        var (manager, screen, node) = Open(new PressBindingNode());
        var ancestorCalls = 0;
        screen.Root!.KeyDown += (_, _) => ancestorCalls++;

        try
        {
            Assert.True(node.Focus());
            manager.ProcessKeyDown(Key.A, KeyModifiers.None);

            Assert.Equal(1, node.Calls);
            Assert.False(node.HandledDuringCallback);
            Assert.True(node.EventArgs!.Handled);
            Assert.Same(node, node.EventArgs.Source);
            Assert.Equal(0, ancestorCalls);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void ReleaseBindingDoesNotRunForPressEvent()
    {
        var (manager, _, node) = Open(new ReleaseBindingNode());

        try
        {
            Assert.True(node.Focus());
            manager.ProcessKeyDown(Key.A, KeyModifiers.None);
            Assert.Equal(0, node.Calls);

            manager.ProcessKeyUp(Key.A, KeyModifiers.None);
            Assert.Equal(1, node.Calls);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void BindingRequiresExactKeyModifiers()
    {
        var (manager, _, node) = Open(new ExactModifiersNode());

        try
        {
            Assert.True(node.Focus());
            manager.ProcessKeyDown(Key.A, KeyModifiers.Control);
            manager.ProcessKeyDown(Key.A, KeyModifiers.Control | KeyModifiers.Shift);
            manager.ProcessKeyDown(Key.A, KeyModifiers.Alt);
            manager.ProcessKeyDown(Key.A, KeyModifiers.Super);

            Assert.Equal(1, node.Calls);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void SeparateBindingCollectionsDoNotShareBindings()
    {
        var (firstManager, _, first) = Open(new FirstComponentNode());
        var (secondManager, _, second) = Open(new SecondComponentNode());

        try
        {
            Assert.True(first.Focus());
            firstManager.ProcessKeyDown(Key.A, KeyModifiers.None);
            Assert.Equal(1, first.Calls);
            Assert.Equal(0, second.Calls);

            Assert.True(second.Focus());
            secondManager.ProcessKeyDown(Key.A, KeyModifiers.None);
            Assert.Equal(1, second.Calls);
            Assert.Equal(1, first.Calls);
        }
        finally
        {
            firstManager.Close();
            secondManager.Close();
        }
    }

    [Fact]
    public void AddBindingOverloadsUseExpectedDefaultsAndReturnSameCollection()
    {
        var bindings = new UiKeyBindings<OverloadNode>();

        var defaultBinding = bindings.AddBinding(Key.A, static (_, _) => { });
        var modifierBinding = bindings.AddBinding(
            Key.B,
            KeyModifiers.Shift,
            static (_, _) => { });
        var fullBinding = bindings.AddBinding(
            Key.C,
            KeyModifiers.Control,
            KeyAction.Release,
            static (_, _) => { });

        Assert.Same(bindings, defaultBinding);
        Assert.Same(bindings, modifierBinding);
        Assert.Same(bindings, fullBinding);
        Assert.Throws<InvalidOperationException>(() =>
            bindings.AddBinding(
                Key.A,
                KeyModifiers.None,
                KeyAction.Press,
                static (_, _) => { }));
        Assert.Throws<InvalidOperationException>(() =>
            bindings.AddBinding(
                Key.B,
                KeyModifiers.Shift,
                KeyAction.Press,
                static (_, _) => { }));
    }

    [Fact]
    public void DuplicateBindingThrowsInvalidOperationException()
    {
        var calls = 0;
        var bindings = new UiKeyBindings<DuplicateBindingNode>()
            .AddBinding(Key.A, (_, _) => calls++);

        Assert.Throws<InvalidOperationException>(() =>
            bindings.AddBinding(Key.A, static (_, _) => { }));

        var node = new DuplicateBindingNode();
        var eventArgs = new UiKeyEventArgs(
            node,
            Key.A,
            KeyModifiers.None,
            isRepeat: false);
        Assert.True(bindings.TryHandle(node, eventArgs, KeyAction.Press));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void NullBindingHandlerThrowsArgumentNullException()
    {
        Action<NullHandlerNode, UiKeyEventArgs> handler = null!;

        var bindings = new UiKeyBindings<NullHandlerNode>();

        Assert.Throws<ArgumentNullException>(() =>
            bindings.AddBinding(Key.A, handler));
    }

    [Fact]
    public void BindingExceptionPropagatesWithoutMarkingEventHandled()
    {
        var (manager, _, node) = Open(new ExceptionBindingNode());
        var expected = new InvalidOperationException("binding");
        node.Expected = expected;

        try
        {
            Assert.True(node.Focus());
            var actual = Assert.Throws<InvalidOperationException>(() =>
                manager.ProcessKeyDown(Key.A, KeyModifiers.None));

            Assert.Same(expected, actual);
            Assert.False(node.EventArgs!.Handled);
        }
        finally
        {
            manager.Close();
        }
    }

    [Fact]
    public void CurrentNodeHandledFlagDoesNotSuppressItsBinding()
    {
        var (manager, screen, node) = Open(new HandledBindingNode());
        var ancestorCalls = 0;
        node.KeyDown += (_, args) => args.Handled = true;
        screen.Root!.KeyDown += (_, _) => ancestorCalls++;

        try
        {
            Assert.True(node.Focus());
            manager.ProcessKeyDown(Key.A, KeyModifiers.None);

            Assert.Equal(1, node.Calls);
            Assert.Equal(0, ancestorCalls);
        }
        finally
        {
            manager.Close();
        }
    }

    private static (UiManager Manager, UiScreen Screen, TNode Node) Open<TNode>(TNode node)
        where TNode : UiNode
    {
        node.Focusable = true;
        node.Width = 40;
        node.Height = 40;
        var root = new Canvas();
        root.Children.Add(node);
        var screen = new UiScreen(root);
        var manager = new UiManager();
        manager.Open(screen);
        manager.Update(new Size(100, 100));
        return (manager, screen, node);
    }

    private sealed class PressBindingNode : UiNode
    {
        private static readonly UiKeyBindings<PressBindingNode> KeyBindings =
            new UiKeyBindings<PressBindingNode>()
                .AddBinding(
                    Key.A,
                    static (node, args) =>
                    {
                        node.Calls++;
                        node.HandledDuringCallback = args.Handled;
                        node.EventArgs = args;
                    });

        internal int Calls { get; private set; }
        internal bool HandledDuringCallback { get; private set; }
        internal UiKeyEventArgs? EventArgs { get; private set; }

        protected override void OnKeyDown(UiKeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
                eventArgs.Handled = true;
        }
    }

    private sealed class ReleaseBindingNode : UiNode
    {
        private static readonly UiKeyBindings<ReleaseBindingNode> KeyBindings =
            new UiKeyBindings<ReleaseBindingNode>()
                .AddBinding(
                    Key.A,
                    KeyModifiers.None,
                    KeyAction.Release,
                    static (node, _) => node.Calls++);

        internal int Calls { get; private set; }

        protected override void OnKeyDown(UiKeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
                eventArgs.Handled = true;
        }

        protected override void OnKeyUp(UiKeyEventArgs eventArgs)
        {
            base.OnKeyUp(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Release))
                eventArgs.Handled = true;
        }
    }

    private sealed class ExactModifiersNode : UiNode
    {
        private static readonly UiKeyBindings<ExactModifiersNode> KeyBindings =
            new UiKeyBindings<ExactModifiersNode>()
                .AddBinding(
                    Key.A,
                    KeyModifiers.Control,
                    static (node, _) => node.Calls++);

        internal int Calls { get; private set; }

        protected override void OnKeyDown(UiKeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
                eventArgs.Handled = true;
        }
    }

    private sealed class FirstComponentNode : UiNode
    {
        private static readonly UiKeyBindings<FirstComponentNode> KeyBindings =
            new UiKeyBindings<FirstComponentNode>()
                .AddBinding(
                    Key.A,
                    static (node, _) => node.Calls++);

        internal int Calls { get; private set; }

        protected override void OnKeyDown(UiKeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
                eventArgs.Handled = true;
        }
    }

    private sealed class SecondComponentNode : UiNode
    {
        private static readonly UiKeyBindings<SecondComponentNode> KeyBindings =
            new UiKeyBindings<SecondComponentNode>()
                .AddBinding(
                    Key.A,
                    static (node, _) => node.Calls++);

        internal int Calls { get; private set; }

        protected override void OnKeyDown(UiKeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
                eventArgs.Handled = true;
        }
    }

    private sealed class DuplicateBindingNode : UiNode
    {
    }

    private sealed class OverloadNode : UiNode
    {
    }

    private sealed class NullHandlerNode : UiNode
    {
    }

    private sealed class ExceptionBindingNode : UiNode
    {
        private static readonly UiKeyBindings<ExceptionBindingNode> KeyBindings =
            new UiKeyBindings<ExceptionBindingNode>()
                .AddBinding(
                    Key.A,
                    static (node, args) =>
                    {
                        node.EventArgs = args;
                        throw node.Expected!;
                    });

        internal Exception? Expected { get; set; }
        internal UiKeyEventArgs? EventArgs { get; private set; }

        protected override void OnKeyDown(UiKeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
                eventArgs.Handled = true;
        }
    }

    private sealed class HandledBindingNode : UiNode
    {
        private static readonly UiKeyBindings<HandledBindingNode> KeyBindings =
            new UiKeyBindings<HandledBindingNode>()
                .AddBinding(
                    Key.A,
                    static (node, _) => node.Calls++);

        internal int Calls { get; private set; }

        protected override void OnKeyDown(UiKeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            if (KeyBindings.TryHandle(this, eventArgs, KeyAction.Press))
                eventArgs.Handled = true;
        }
    }
}
