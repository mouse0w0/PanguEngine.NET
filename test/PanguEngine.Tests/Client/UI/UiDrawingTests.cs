using System.ComponentModel;
using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiDrawingTests
{
    [Fact]
    public void OpacityPropertyUsesExpectedOwnerDefaultAndRenderInvalidation()
    {
        var node = new DrawingNode();

        Assert.Equal(typeof(UiNode), UiNode.OpacityProperty.OwnerType);
        Assert.Equal(typeof(UiNode), UiNode.OpacityProperty.TargetType);
        Assert.Equal(1, UiNode.OpacityProperty.DefaultValue);
        Assert.Equal(UiPropertyInvalidation.Render, UiNode.OpacityProperty.Invalidation);
        Assert.Equal(1, node.Opacity);
    }

    [Fact]
    public void EmptyAndNonDrawableRootsProduceNoCommands()
    {
        Assert.Empty(new UiScreen().CreateDrawCommandList());

        var unarranged = new DrawingNode
        {
            DrawAction = DrawUnitRectangle
        };
        Assert.Empty(new UiScreen(unarranged).CreateDrawCommandList());

        var hidden = new DrawingNode
        {
            Visibility = Visibility.Hidden,
            DrawAction = DrawUnitRectangle
        };
        var hiddenScreen = new UiScreen(hidden);
        Arrange(hidden, new Rect(0, 0, 10, 10));
        Assert.Empty(hiddenScreen.CreateDrawCommandList());

        var collapsed = new DrawingNode
        {
            Visibility = Visibility.Collapsed,
            DrawAction = DrawUnitRectangle
        };
        var collapsedScreen = new UiScreen(collapsed);
        Arrange(collapsed, new Rect(0, 0, 10, 10));
        Assert.Empty(collapsedScreen.CreateDrawCommandList());

        var transparent = new DrawingNode
        {
            Opacity = 0,
            DrawAction = DrawUnitRectangle
        };
        var transparentScreen = new UiScreen(transparent);
        Arrange(transparent, new Rect(0, 0, 10, 10));
        Assert.Empty(transparentScreen.CreateDrawCommandList());
    }

    [Fact]
    public void NestedNodesEmitScreenLogicalBoundsInDrawingOrder()
    {
        var root = new DrawingParent
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(1, 2, 3, 4), new Color(1, 0, 0))
        };
        var first = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(2, 3, 4, 5), new Color(2, 0, 0))
        };
        var second = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(3, 4, 5, 6), new Color(3, 0, 0))
        };
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(10, 20, 100, 100));
        Arrange(first, new Rect(5, 6, 20, 20));
        Arrange(second, new Rect(7, 8, 20, 20));

        var commands = screen.CreateDrawCommandList()
            .Cast<UiFillRectangleCommand>()
            .ToArray();

        Assert.Equal(
            [new Color(1, 0, 0), new Color(2, 0, 0), new Color(3, 0, 0)],
            commands.Select(command => command.Color));
        Assert.Equal(new Rect(11, 22, 3, 4), commands[0].Bounds);
        Assert.Equal(new Rect(17, 29, 4, 5), commands[1].Bounds);
        Assert.Equal(new Rect(20, 32, 5, 6), commands[2].Bounds);
        Assert.All(commands, command => Assert.Null(command.Clip));
        Assert.All(commands, command => Assert.Equal(1, command.Opacity));
    }

    [Fact]
    public void CommandListIsReadOnlyAndDoesNotTrackLaterNodeChanges()
    {
        var firstColor = new Color(1, 2, 3);
        var secondColor = new Color(4, 5, 6);
        var node = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(0, 0, 2, 3), firstColor)
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(5, 7, 10, 10));

        var firstSnapshot = screen.CreateDrawCommandList();
        node.DrawAction = context =>
            context.FillRectangle(new Rect(1, 1, 4, 5), secondColor);
        var secondSnapshot = screen.CreateDrawCommandList();

        Assert.False((object)firstSnapshot is IList<UiDrawCommand>);
        var firstCommand = Assert.IsType<UiFillRectangleCommand>(Assert.Single(firstSnapshot));
        Assert.Equal(new Rect(5, 7, 2, 3), firstCommand.Bounds);
        Assert.Equal(firstColor, firstCommand.Color);
        var secondCommand = Assert.IsType<UiFillRectangleCommand>(Assert.Single(secondSnapshot));
        Assert.Equal(new Rect(6, 8, 4, 5), secondCommand.Bounds);
        Assert.Equal(secondColor, secondCommand.Color);
    }

    [Fact]
    public void MoveCommandsChangeOnlySubsequentDrawingOrder()
    {
        var root = new DrawingParent();
        var first = CreateColorNode(1);
        var second = CreateColorNode(2);
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(first, new Rect(0, 0, 10, 10));
        Arrange(second, new Rect(0, 0, 10, 10));

        var original = screen.CreateDrawCommandList();
        first.MoveToFront();
        Arrange(root, new Rect(0, 0, 10, 10));
        var reordered = screen.CreateDrawCommandList();

        Assert.Equal(
            [new Color(1, 0, 0), new Color(2, 0, 0)],
            original.Cast<UiFillRectangleCommand>().Select(command => command.Color));
        Assert.Equal(
            [new Color(2, 0, 0), new Color(1, 0, 0)],
            reordered.Cast<UiFillRectangleCommand>().Select(command => command.Color));
    }

    [Fact]
    public void ParentAndContextClipsIntersectWithoutChangingOriginalBounds()
    {
        var root = new DrawingParent { ClipToBounds = true };
        var child = new DrawingNode
        {
            DrawAction = context =>
            {
                using var clip = context.PushClip(new Rect(5, 5, 20, 20));
                context.FillRectangle(
                    new Rect(0, 0, 40, 40),
                    new Color(10, 20, 30));
            }
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(10, 20, 30, 30));
        Arrange(child, new Rect(20, 20, 40, 40));

        var command = Assert.IsType<UiFillRectangleCommand>(
            Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(new Rect(30, 40, 40, 40), command.Bounds);
        Assert.Equal<Rect?>(new Rect(35, 45, 5, 5), command.Clip);
    }

    [Fact]
    public void ClipToBoundsOnlyClipsDescendants()
    {
        var root = new DrawingParent
        {
            ClipToBounds = true,
            DrawAction = context =>
                context.FillRectangle(new Rect(20, 0, 5, 5), new Color(1, 0, 0))
        };
        var child = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(20, 0, 5, 5), new Color(2, 0, 0))
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(child, new Rect(0, 0, 10, 10));

        var commands = screen.CreateDrawCommandList();

        var command = Assert.IsType<UiFillRectangleCommand>(Assert.Single(commands));
        Assert.Equal(new Color(1, 0, 0), command.Color);
        Assert.Null(command.Clip);
    }

    [Fact]
    public void UnclippedParentAllowsOverflowingDescendant()
    {
        var root = new DrawingParent();
        var child = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(new Rect(20, 0, 5, 5), new Color(1, 0, 0))
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(child, new Rect(0, 0, 10, 10));

        var command = Assert.IsType<UiFillRectangleCommand>(
            Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(new Rect(20, 0, 5, 5), command.Bounds);
        Assert.Null(command.Clip);
    }

    [Fact]
    public void InvisibleRectangleCallsDoNotProduceCommands()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
            {
                context.FillRectangle(Rect.Zero, new Color(1, 1, 1));
                context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1, 0));
                using (context.PushClip(Rect.Zero))
                    context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
                using (context.PushClip(new Rect(5, 5, 1, 1)))
                    context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
                using (context.PushOpacity(0))
                    context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        Assert.Empty(screen.CreateDrawCommandList());
    }

    [Fact]
    public void NodeAndContextOpacityMultiplyWithoutChangingColorAlpha()
    {
        var color = new Color(10, 20, 30, 128);
        var root = new DrawingParent { Opacity = 0.5 };
        var child = new DrawingNode
        {
            Opacity = 0.5,
            DrawAction = context =>
            {
                using var opacity = context.PushOpacity(0.25);
                context.FillRectangle(new Rect(0, 0, 10, 10), color);
            }
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 10, 10));
        Arrange(child, new Rect(0, 0, 10, 10));

        var command = Assert.IsType<UiFillRectangleCommand>(
            Assert.Single(screen.CreateDrawCommandList()));

        Assert.Equal(0.0625, command.Opacity);
        Assert.Equal(color, command.Color);
    }

    [Fact]
    public void InvalidNodeOpacityFailsBeforeItsDrawCore()
    {
        var drawCalls = 0;
        var propertyNode = new DrawingNode
        {
            Opacity = -1,
            DrawAction = _ => drawCalls++
        };
        var propertyScreen = new UiScreen(propertyNode);
        Arrange(propertyNode, new Rect(0, 0, 10, 10));

        Assert.Throws<InvalidOperationException>(() => propertyScreen.CreateDrawCommandList());

        var setValueNode = new DrawingNode
        {
            DrawAction = _ => drawCalls++
        };
        var setValueScreen = new UiScreen(setValueNode);
        Arrange(setValueNode, new Rect(0, 0, 10, 10));
        setValueNode.SetValue(UiNode.OpacityProperty, double.NaN);

        Assert.Throws<InvalidOperationException>(() => setValueScreen.CreateDrawCommandList());

        var bindingNode = new DrawingNode
        {
            DrawAction = _ => drawCalls++
        };
        var bindingScreen = new UiScreen(bindingNode);
        Arrange(bindingNode, new Rect(0, 0, 10, 10));
        var source = new OpacitySource { Opacity = double.PositiveInfinity };
        bindingNode.Bind(UiNode.OpacityProperty, source, item => item.Opacity);

        Assert.Throws<InvalidOperationException>(() => bindingScreen.CreateDrawCommandList());
        Assert.Equal(0, drawCalls);
    }

    [Fact]
    public void PushOpacityRejectsInvalidValuesWithoutLosingCurrentState()
    {
        var errors = new List<Exception?>();
        var node = new DrawingNode
        {
            DrawAction = context =>
            {
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(double.NaN); }));
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(double.PositiveInfinity); }));
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(-0.01); }));
                errors.Add(Record.Exception(() => { _ = context.PushOpacity(1.01); }));
                context.FillRectangle(new Rect(0, 0, 1, 1), new Color(1, 1, 1));
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        var commands = screen.CreateDrawCommandList();

        Assert.All(errors, error => Assert.IsType<ArgumentOutOfRangeException>(error));
        Assert.Equal(1, Assert.IsType<UiFillRectangleCommand>(Assert.Single(commands)).Opacity);
    }

    [Fact]
    public void ScopeRequiresLifoAndSingleDisposal()
    {
        Exception? outOfOrderError = null;
        Exception? duplicateError = null;
        var node = new DrawingNode
        {
            DrawAction = context =>
            {
                var first = context.PushClip(new Rect(0, 0, 5, 5));
                var second = context.PushOpacity(0.5);
                try
                {
                    first.Dispose();
                }
                catch (Exception exception)
                {
                    outOfOrderError = exception;
                }
                second.Dispose();
                first.Dispose();
                try
                {
                    first.Dispose();
                }
                catch (Exception exception)
                {
                    duplicateError = exception;
                }
            }
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        _ = screen.CreateDrawCommandList();
        default(UiDrawingScope).Dispose();

        Assert.IsType<InvalidOperationException>(outOfOrderError);
        Assert.IsType<InvalidOperationException>(duplicateError);
    }

    [Fact]
    public void MissingScopeStopsGenerationAndLaterGenerationCanRecover()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
                _ = context.PushClip(new Rect(0, 0, 1, 1))
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        Assert.Throws<InvalidOperationException>(() => screen.CreateDrawCommandList());

        node.DrawAction = DrawUnitRectangle;
        Assert.Single(screen.CreateDrawCommandList());
    }

    [Fact]
    public void ContextCannotBeUsedAfterDrawCoreReturns()
    {
        UiDrawingContext? captured = null;
        var node = new DrawingNode
        {
            DrawAction = context => captured = context
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        _ = screen.CreateDrawCommandList();

        Assert.Throws<InvalidOperationException>(() =>
            captured!.FillRectangle(
                new Rect(0, 0, 1, 1),
                new Color(1, 1, 1)));
    }

    [Fact]
    public void DrawCoreExceptionPreservesInstanceAndDrawingStateRecovers()
    {
        var expected = new InvalidOperationException("draw failed");
        var node = new DrawingNode
        {
            DrawAction = _ => throw expected
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        var actual = Assert.Throws<InvalidOperationException>(() => screen.CreateDrawCommandList());

        Assert.Same(expected, actual);
        node.DrawAction = DrawUnitRectangle;
        Assert.Single(screen.CreateDrawCommandList());
    }

    [Fact]
    public void DrawingRejectsRootTreeLayoutAndPropertyMutationsBeforeCommit()
    {
        var root = new DrawingParent();
        var first = new DrawingNode();
        var second = new DrawingNode();
        var added = new DrawingNode();
        var replacement = new DrawingNode();
        root.Add(first);
        root.Add(second);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(first, new Rect(0, 0, 10, 10));
        Arrange(second, new Rect(20, 0, 10, 10));
        var errors = new List<Exception?>();
        root.DrawAction = _ =>
        {
            errors.Add(Record.Exception(() => screen.Root = replacement));
            errors.Add(Record.Exception(() => root.Add(added)));
            errors.Add(Record.Exception(() => root.Remove(first)));
            errors.Add(Record.Exception(root.Clear));
            errors.Add(Record.Exception(first.MoveToFront));
            errors.Add(Record.Exception(() => root.Measure(new Size(100, 100))));
            errors.Add(Record.Exception(() => root.Arrange(new Rect(0, 0, 100, 100))));
            errors.Add(Record.Exception(root.InvalidateMeasure));
            errors.Add(Record.Exception(root.InvalidateArrange));
            errors.Add(Record.Exception(() => root.Opacity = 0.5));
            errors.Add(Record.Exception(() => root.Width = 50));
            errors.Add(Record.Exception(() => root.Focusable = true));
        };

        _ = screen.CreateDrawCommandList();

        Assert.Equal(12, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(root, screen.Root);
        Assert.Equal(new UiNode[] { first, second }, root.Children);
        Assert.Null(added.Parent);
        Assert.Null(replacement.Screen);
        Assert.Equal(1, root.Opacity);
        Assert.True(double.IsNaN(root.Width));
        Assert.False(root.Focusable);
        Assert.True(root.IsMeasureValid);
        Assert.True(root.IsArrangeValid);
    }

    [Fact]
    public void ExistingNoOpTreeOperationsRemainNoOpsWhileDrawing()
    {
        var root = new DrawingParent();
        var first = new DrawingNode();
        var emptyFront = new DrawingParent();
        var foreign = new DrawingNode();
        var independent = new DrawingNode();
        root.Add(first);
        root.Add(emptyFront);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(0, 0, 100, 100));
        Arrange(first, new Rect(0, 0, 10, 10));
        Arrange(emptyFront, new Rect(20, 0, 10, 10));
        var errors = new List<Exception?>();
        root.DrawAction = _ =>
        {
            errors.Add(Record.Exception(() => screen.Root = root));
            errors.Add(Record.Exception(independent.MoveToFront));
            errors.Add(Record.Exception(emptyFront.MoveToFront));
            errors.Add(Record.Exception(() => root.Remove(foreign)));
            errors.Add(Record.Exception(emptyFront.Clear));
        };

        _ = screen.CreateDrawCommandList();

        Assert.All(errors, error => Assert.Null(error));
        Assert.Equal(new UiNode[] { first, emptyFront }, root.Children);
        Assert.Null(foreign.Parent);
        Assert.Null(independent.Parent);
    }

    [Fact]
    public void DrawingRejectsManagerLifecycleOperationsBeforeManagerStateChanges()
    {
        var manager = new UiManager();
        var root = new DrawingNode();
        var screen = new UiScreen(root);
        manager.Open(screen);
        Arrange(root, new Rect(0, 0, 10, 10));
        var errors = new List<Exception?>();
        root.DrawAction = _ =>
        {
            errors.Add(Record.Exception(() => manager.Open(new UiScreen())));
            errors.Add(Record.Exception(manager.Close));
            errors.Add(Record.Exception(manager.Shutdown));
        };

        _ = screen.CreateDrawCommandList();

        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Same(screen, manager.CurrentScreen);
        manager.Close();
    }

    [Fact]
    public void ClosedDrawingScreenCannotOpenUntilGenerationReturns()
    {
        var manager = new UiManager();
        var node = new DrawingNode();
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));
        Exception? openError = null;
        node.DrawAction = _ =>
            openError = Record.Exception(() => manager.Open(screen));

        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(openError);
        Assert.Null(manager.CurrentScreen);
        manager.Open(screen);
        manager.Close();
    }

    [Fact]
    public void DrawingRejectsReentrantGeneration()
    {
        Exception? error = null;
        UiScreen screen = null!;
        var node = new DrawingNode
        {
            DrawAction = _ =>
                error = Record.Exception(() => screen.CreateDrawCommandList())
        };
        screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));

        _ = screen.CreateDrawCommandList();

        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void DrawingAllowsPostButDoesNotRunItSynchronously()
    {
        var calls = 0;
        UiScreen screen = null!;
        var node = new DrawingNode
        {
            DrawAction = _ => screen.Post(() => calls++)
        };
        screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));
        screen.Open();

        _ = screen.CreateDrawCommandList();
        Assert.Equal(0, calls);

        screen.Update(new Size(10, 10));
        Assert.Equal(1, calls);
        screen.Close();
    }

    [Fact]
    public void OpenScreenRequiresOwnerThreadAndClosedScreenAllowsBackgroundDrawing()
    {
        var node = new DrawingNode { DrawAction = DrawUnitRectangle };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(0, 0, 10, 10));
        screen.Open();

        var openError = RunOnBackgroundThread(() =>
            Record.Exception(() => screen.CreateDrawCommandList()));

        Assert.IsType<InvalidOperationException>(openError);
        screen.Close();

        var closedCount = RunOnBackgroundThread(() =>
            screen.CreateDrawCommandList().Count);
        Assert.Equal(1, closedCount);
    }

    [Fact]
    public void CoordinateOverflowFailsWithoutReturningACommandList()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
                context.FillRectangle(
                    new Rect(double.MaxValue, 0, 1, 1),
                    new Color(1, 1, 1))
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(double.MaxValue, 0, 1, 1));

        Assert.Throws<InvalidOperationException>(() => screen.CreateDrawCommandList());
    }

    [Fact]
    public void NestedLayoutOriginOverflowFailsWithoutDrawingTheChild()
    {
        var drawCalls = 0;
        var root = new DrawingParent();
        var child = new DrawingNode
        {
            DrawAction = _ => drawCalls++
        };
        root.Add(child);
        var screen = new UiScreen(root);
        Arrange(root, new Rect(double.MaxValue, 0, 1, 1));
        Arrange(child, new Rect(double.MaxValue, 0, 1, 1));

        Assert.Throws<InvalidOperationException>(() => screen.CreateDrawCommandList());
        Assert.Equal(0, drawCalls);
    }

    [Fact]
    public void PushClipCoordinateOverflowFailsWithoutReturningACommandList()
    {
        var node = new DrawingNode
        {
            DrawAction = context =>
                _ = context.PushClip(
                    new Rect(double.MaxValue, 0, 1, 1))
        };
        var screen = new UiScreen(node);
        Arrange(node, new Rect(double.MaxValue, 0, 1, 1));

        Assert.Throws<InvalidOperationException>(() => screen.CreateDrawCommandList());
    }

    private static DrawingNode CreateColorNode(byte red) =>
        new()
        {
            DrawAction = context =>
                context.FillRectangle(
                    new Rect(0, 0, 1, 1),
                    new Color(red, 0, 0))
        };

    private static void DrawUnitRectangle(UiDrawingContext context) =>
        context.FillRectangle(
            new Rect(0, 0, 1, 1),
            new Color(1, 1, 1));

    private static void Arrange(UiNode node, Rect bounds)
    {
        node.Measure(new Size(bounds.Width, bounds.Height));
        node.Arrange(bounds);
    }

    private static T RunOnBackgroundThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.Start();
        thread.Join();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();

        return result;
    }

    private sealed class DrawingNode : UiNode
    {
        internal Action<UiDrawingContext>? DrawAction { get; set; }

        protected override void DrawCore(UiDrawingContext context) =>
            DrawAction?.Invoke(context);
    }

    private sealed class DrawingParent : Parent
    {
        internal Action<UiDrawingContext>? DrawAction { get; set; }

        internal void Add(UiNode child) =>
            AddChild(child);

        internal bool Remove(UiNode child) =>
            RemoveChild(child);

        internal void Clear() =>
            ClearChildren();

        protected override void DrawCore(UiDrawingContext context) =>
            DrawAction?.Invoke(context);
    }

    private sealed class OpacitySource : INotifyPropertyChanged
    {
        private double _opacity;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double Opacity
        {
            get => _opacity;
            set
            {
                if (_opacity.Equals(value))
                    return;

                _opacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Opacity)));
            }
        }
    }
}
