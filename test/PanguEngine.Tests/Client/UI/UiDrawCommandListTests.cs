using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Rendering;

namespace PanguEngine.Tests.Client.UI;

public sealed class UiDrawCommandListTests
{
    [Fact]
    public void SetTranslatePreservesScaleAndRestoresOriginalPosition()
    {
        var screen = CreateScreen(context =>
        {
            using (context.SetTranslate(new Point(20, 30)))
            {
                DrawRectangle(context);
                using (context.SetTranslate(Point.Zero))
                    DrawRectangle(context);
                DrawRectangle(context);
            }
            DrawRectangle(context);
        }, 2, new Point(3, 4));
        var builder = new UiDrawBuilder();

        builder.Build(screen.CreateDrawCommandList(), 100, 100, false);

        Assert.Equal(4, builder.RectangleCount);
        Assert.Equal(new UiVertex(20, 30, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(24, 34, 1, 1, 1, 1), builder.Vertices[2]);
        Assert.Equal(new UiVertex(0, 0, 1, 1, 1, 1), builder.Vertices[4]);
        Assert.Equal(new UiVertex(4, 4, 1, 1, 1, 1), builder.Vertices[6]);
        Assert.Equal(new UiVertex(20, 30, 1, 1, 1, 1), builder.Vertices[8]);
        Assert.Equal(new UiVertex(6, 8, 1, 1, 1, 1), builder.Vertices[12]);
        Assert.Equal(new UiVertex(10, 12, 1, 1, 1, 1), builder.Vertices[14]);
        Assert.Equal(24u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void SetScalePreservesOriginAndRestoresAccumulatedScale()
    {
        var screen = CreateScreen(context =>
        {
            using var transform = context.PushTransform(new Point(1, 2), 3);
            using (context.SetScale(0.5))
            {
                DrawRectangle(context);
                using (context.SetScale(1))
                    DrawRectangle(context);
                using (context.SetTranslate(new Point(20, 30)))
                    DrawRectangle(context);
                DrawRectangle(context);
            }
            DrawRectangle(context);
        }, 2, new Point(3, 4));
        var builder = new UiDrawBuilder();

        builder.Build(screen.CreateDrawCommandList(), 100, 100, false);

        Assert.Equal(5, builder.RectangleCount);
        Assert.Equal(new UiVertex(8, 12, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(9, 13, 1, 1, 1, 1), builder.Vertices[2]);
        Assert.Equal(new UiVertex(8, 12, 1, 1, 1, 1), builder.Vertices[4]);
        Assert.Equal(new UiVertex(10, 14, 1, 1, 1, 1), builder.Vertices[6]);
        Assert.Equal(new UiVertex(20, 30, 1, 1, 1, 1), builder.Vertices[8]);
        Assert.Equal(new UiVertex(21, 31, 1, 1, 1, 1), builder.Vertices[10]);
        Assert.Equal(new UiVertex(8, 12, 1, 1, 1, 1), builder.Vertices[12]);
        Assert.Equal(new UiVertex(9, 13, 1, 1, 1, 1), builder.Vertices[14]);
        Assert.Equal(new UiVertex(8, 12, 1, 1, 1, 1), builder.Vertices[16]);
        Assert.Equal(new UiVertex(20, 24, 1, 1, 1, 1), builder.Vertices[18]);
        Assert.Equal(30u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void SetTransformReplacesInheritedTransformAndRestoresItAfterNestedScopes()
    {
        var screen = CreateScreen(context =>
        {
            using (context.SetTransform(new Point(20, 30), 3))
            {
                using (context.PushTransform(new Point(1, 2), 2))
                    DrawRectangle(context);
                DrawRectangle(context);
            }
            DrawRectangle(context);
        }, 2, new Point(3, 4));
        var builder = new UiDrawBuilder();

        builder.Build(screen.CreateDrawCommandList(), 100, 100, false);

        Assert.Equal(3, builder.RectangleCount);
        Assert.Equal(new UiVertex(23, 36, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(35, 48, 1, 1, 1, 1), builder.Vertices[2]);
        Assert.Equal(new UiVertex(20, 30, 1, 1, 1, 1), builder.Vertices[4]);
        Assert.Equal(new UiVertex(26, 36, 1, 1, 1, 1), builder.Vertices[6]);
        Assert.Equal(new UiVertex(6, 8, 1, 1, 1, 1), builder.Vertices[8]);
        Assert.Equal(new UiVertex(10, 12, 1, 1, 1, 1), builder.Vertices[10]);
        Assert.Equal(18u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void SetIdentityTransformResetsOuterStateAndKeepsScreenBoundariesIsolated()
    {
        var screen = CreateScreen(context =>
        {
            using (context.SetTransform(new Point(20, 30), 3))
            {
                using (context.SetTransform(Point.Zero))
                    DrawRectangle(context);
                DrawRectangle(context);
            }
            DrawRectangle(context);
        }, 2, new Point(3, 4));
        var commands = screen.CreateDrawCommandList();
        commands.Append(CreateScreen(DrawRectangle, 0.5));
        var builder = new UiDrawBuilder();

        builder.Build(commands, 100, 100, false);

        Assert.Equal(4, builder.RectangleCount);
        Assert.Equal(new UiVertex(0, 0, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(2, 2, 1, 1, 1, 1), builder.Vertices[2]);
        Assert.Equal(new UiVertex(20, 30, 1, 1, 1, 1), builder.Vertices[4]);
        Assert.Equal(new UiVertex(26, 36, 1, 1, 1, 1), builder.Vertices[6]);
        Assert.Equal(new UiVertex(6, 8, 1, 1, 1, 1), builder.Vertices[8]);
        Assert.Equal(new UiVertex(0, 0, 1, 1, 1, 1), builder.Vertices[12]);
        Assert.Equal(new UiVertex(1, 1, 1, 1, 1, 1), builder.Vertices[14]);
        Assert.Equal(24u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsoluteTransformsPreserveEstablishedClipAndOpacity(bool separateComponents)
    {
        var screen = CreateScreen(context =>
        {
            using (context.PushClip(new Rect(0, 0, 10, 10)))
            using (context.PushOpacity(0.25))
            {
                using (context.SetTransform(Point.Zero))
                    DrawRectangle(context);
                using (separateComponents
                           ? context.SetTranslate(new Point(20, 20))
                           : context.SetTransform(new Point(20, 20)))
                using (separateComponents ? context.SetScale(1) : default(UiDrawingScope))
                {
                    DrawRectangle(context);
                    using (context.PushClip(new Rect(1, 1, 1, 1)))
                        DrawRectangle(context);
                    DrawRectangle(context);
                }
                DrawRectangle(context);
            }
            DrawRectangle(context);
        }, 2, new Point(5, 5));
        var builder = new UiDrawBuilder();

        builder.Build(screen.CreateDrawCommandList(), 100, 100, false);

        Assert.Equal(5, builder.RectangleCount);
        Assert.Equal(new UiVertex(20, 20, 1, 1, 1, 0.25f), builder.Vertices[0]);
        Assert.Equal(new UiVertex(20, 20, 1, 1, 1, 0.25f), builder.Vertices[4]);
        Assert.Equal(new UiVertex(20, 20, 1, 1, 1, 0.25f), builder.Vertices[8]);
        Assert.Equal(new UiVertex(10, 10, 1, 1, 1, 0.25f), builder.Vertices[12]);
        Assert.Equal(new UiVertex(10, 10, 1, 1, 1, 1), builder.Vertices[16]);
        Assert.Equal(
            [new UiBatch(new UiScissor(10, 10, 20, 20), 0, 6),
             new UiBatch(new UiScissor(21, 21, 1, 1), 6, 6),
             new UiBatch(new UiScissor(10, 10, 20, 20), 12, 12),
             new UiBatch(new UiScissor(0, 0, 100, 100), 24, 6)],
            builder.Batches.ToArray());
    }

    [Fact]
    public void ScreensWithDifferentScalesHaveIsolatedTransformsClipsAndOpacity()
    {
        var first = CreateScreen(context =>
        {
            using var clip = context.PushClip(new Rect(0, 0, 5, 5));
            using var opacity = context.PushOpacity(0.25);
            context.FillRectangle(new Rect(1, 2, 4, 6), new Color(255, 0, 0));
        }, 2, new Point(3, 4));
        var second = CreateScreen(context =>
            context.FillRectangle(new Rect(1, 2, 4, 6), new Color(0, 255, 0)), 0.5, new Point(20, 30));
        var commands = new UiDrawCommandList();

        commands.Append(first);
        commands.Append(second);
        var builder = new UiDrawBuilder();
        builder.Build(commands, 100, 100, false);

        Assert.Equal(2, builder.RectangleCount);
        Assert.Equal(new UiVertex(8, 12, 1, 0, 0, 0.25f), builder.Vertices[0]);
        Assert.Equal(new UiVertex(16, 24, 1, 0, 0, 0.25f), builder.Vertices[2]);
        Assert.Equal(new UiVertex(10.5f, 16, 0, 1, 0, 1), builder.Vertices[4]);
        Assert.Equal(new UiVertex(12.5f, 19, 0, 1, 0, 1), builder.Vertices[6]);
        Assert.Equal(
            [new UiBatch(new UiScissor(6, 8, 10, 10), 0, 6), new UiBatch(new UiScissor(0, 0, 100, 100), 6, 6)],
            builder.Batches.ToArray());
    }

    [Fact]
    public void ScreenBoundariesDoNotSplitCompatibleBatchesOrReorderDraws()
    {
        var commands = new UiDrawCommandList();
        commands.Append(CreateScreen(context =>
            context.FillRectangle(new Rect(1, 1, 2, 2), new Color(255, 0, 0)), 2));
        commands.Append(CreateScreen(context =>
            context.FillRectangle(new Rect(1, 1, 2, 2), new Color(0, 255, 0)), 0.5));
        commands.Append(CreateScreen(context =>
            context.FillRectangle(new Rect(1, 1, 2, 2), new Color(0, 0, 255))));

        var builder = new UiDrawBuilder();
        builder.Build(commands, 100, 100, false);

        Assert.Equal(18u, Assert.Single(builder.Batches.ToArray()).IndexCount);
        Assert.Equal(new UiVertex(2, 2, 1, 0, 0, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(0.5f, 0.5f, 0, 1, 0, 1), builder.Vertices[4]);
        Assert.Equal(new UiVertex(1, 1, 0, 0, 1, 1), builder.Vertices[8]);
    }

    [Fact]
    public void RecordedCommandsKeepScaleTreeAndDrawingValuesUntilExplicitlyChanged()
    {
        var translation = new Point(2, 3);
        var screen = CreateScreen(context =>
        {
            using var transform = context.PushTransform(translation, 2);
            DrawRectangle(context);
        }, 1.5);
        var before = screen.CreateDrawCommandList();
        var savedCommands = before.ToArray();
        var builder = new UiDrawBuilder();
        builder.Build(before, 100, 100, false);
        var vertices = builder.Vertices.ToArray();

        translation = new Point(10, 20);
        screen.Scale = 3;
        screen.Root!.Opacity = 0.5;
        Arrange(screen.Root, Point.Zero);
        var after = screen.CreateDrawCommandList();

        builder.Build(after, 100, 100, false);
        Assert.Equal(new UiVertex(30, 60, 1, 1, 1, 0.5f), builder.Vertices[0]);
        builder.Build(before, 100, 100, false);
        Assert.Equal(savedCommands, before.ToArray());
        Assert.Equal(vertices, builder.Vertices.ToArray());
        Assert.Equal(new UiVertex(3, 4.5f, 1, 1, 1, 1), vertices[0]);
    }

    [Fact]
    public void ListCanBeAppendedClearedAndReusedAfterConsumption()
    {
        var commands = new UiDrawCommandList();
        var sameList = commands;
        var firstScreen = CreateScreen(DrawRectangle, 2);
        var secondScreen = CreateScreen(DrawRectangle, 0.5);
        var builder = new UiDrawBuilder();
        commands.Append(firstScreen);
        var firstCommand = Assert.Single(commands.OfType<UiFillRectangleCommand>());
        builder.Build(commands, 100, 100, false);
        Assert.Equal(4f, builder.Vertices[2].X);

        commands.Append(secondScreen);
        builder.Build(commands, 100, 100, false);
        Assert.Equal(2, builder.RectangleCount);
        Assert.Equal(4f, builder.Vertices[2].X);
        Assert.Equal(1f, builder.Vertices[6].X);

        commands.Clear();
        Assert.Empty(sameList);
        Assert.Equal(new Rect(0, 0, 2, 2), firstCommand.Bounds);
        commands.Append(secondScreen);
        builder.Build(sameList, 100, 100, false);
        Assert.Equal(1, builder.RectangleCount);
        Assert.Equal(1f, builder.Vertices[2].X);
    }

    [Fact]
    public void FailedAppendDiscardsWholeCollectionAndAllowsFreshRecording()
    {
        var commands = new UiDrawCommandList();
        commands.Append(CreateScreen(DrawRectangle));
        var expected = new InvalidOperationException("recording failed");
        UiDrawingContext? captured = null;
        var failing = CreateScreen(context =>
        {
            captured = context;
            _ = context.PushTransform(new Point(5, 6), 2);
            _ = context.SetTransform(new Point(1, 2));
            _ = context.SetTranslate(new Point(3, 4));
            _ = context.SetScale(0.5);
            DrawRectangle(context);
            throw expected;
        }, 3);

        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => commands.Append(failing)));
        Assert.Empty(commands);
        Assert.Throws<InvalidOperationException>(() => DrawRectangle(captured!));
        Assert.False(failing.IsDrawing);

        Assert.IsType<DrawingNode>(failing.Root).DrawAction = DrawRectangle;
        commands.Append(failing);
        var builder = new UiDrawBuilder();
        builder.Build(commands, 100, 100, false);
        Assert.Equal(1, builder.RectangleCount);
        Assert.Equal(new UiVertex(6, 6, 1, 1, 1, 1), builder.Vertices[2]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingScopeDiscardsCollectionAndInvalidatesContext(bool absoluteTransform)
    {
        UiDrawingContext? captured = null;
        var screen = CreateScreen(context =>
        {
            captured = context;
            if (absoluteTransform)
                _ = context.SetTransform(Point.Zero);
            else
                _ = context.PushTransform(Point.Zero);
            DrawRectangle(context);
        });
        var commands = new UiDrawCommandList();
        commands.Append(CreateScreen(DrawRectangle));

        Assert.Throws<InvalidOperationException>(() => commands.Append(screen));
        Assert.Empty(commands);
        Assert.Throws<InvalidOperationException>(() => { _ = captured!.PushTransform(Point.Zero); });
        Assert.Throws<InvalidOperationException>(() => { _ = captured!.SetTransform(Point.Zero); });
        Assert.Throws<InvalidOperationException>(() => { _ = captured!.SetTranslate(Point.Zero); });
        Assert.Throws<InvalidOperationException>(() => { _ = captured!.SetScale(1); });
        Assert.Throws<InvalidOperationException>(() => { _ = captured!.PushTranslate(Point.Zero); });
        Assert.Throws<InvalidOperationException>(() => { _ = captured!.PushScale(1); });

        Assert.IsType<DrawingNode>(screen.Root).DrawAction = DrawRectangle;
        commands.Append(screen);
        Assert.Single(commands);
    }

    [Fact]
    public void SharedListRejectsCrossScreenReentryClearAndPartialReads()
    {
        var commands = new UiDrawCommandList();
        var errors = new List<Exception?>();
        var otherCalls = 0;
        var other = CreateScreen(context =>
        {
            otherCalls++;
            DrawRectangle(context);
        });
        UiScreen? screen = null;
        screen = CreateScreen(context =>
        {
            using var transform = context.PushTransform(new Point(5, 6), 2);
            errors.Add(Record.Exception(() => commands.Append(other)));
            errors.Add(Record.Exception(commands.Clear));
            errors.Add(Record.Exception(() => { _ = commands.Count; }));
            errors.Add(Record.Exception(() => { _ = commands[0]; }));
            errors.Add(Record.Exception(() => { using var enumerator = commands.GetEnumerator(); }));
            errors.Add(Record.Exception(() => { _ = ((System.Collections.IEnumerable)commands).GetEnumerator(); }));
            errors.Add(Record.Exception(screen!.CreateDrawCommandList));
            DrawRectangle(context);
        });

        commands.Append(screen);
        Assert.Equal(7, errors.Count);
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Equal(0, otherCalls);
        commands.Append(other);

        var builder = new UiDrawBuilder();
        builder.Build(commands, 100, 100, false);
        Assert.Equal(1, otherCalls);
        Assert.Equal(new UiVertex(5, 6, 1, 1, 1, 1), builder.Vertices[0]);
        Assert.Equal(new UiVertex(0, 0, 1, 1, 1, 1), builder.Vertices[4]);
        Assert.Equal(12u, Assert.Single(builder.Batches.ToArray()).IndexCount);
    }

    [Fact]
    public void ListRejectsWrongThreadMutationWithoutChangingCompletedCommands()
    {
        var commands = new UiDrawCommandList();
        var screen = CreateScreen(DrawRectangle);
        commands.Append(screen);
        var errors = new List<Exception?>();
        var thread = new Thread(() =>
        {
            errors.Add(Record.Exception(() => commands.Append(screen)));
            errors.Add(Record.Exception(commands.Clear));
        });

        thread.Start();
        thread.Join();

        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Single(commands);
    }

    [Fact]
    public void ActiveContextRejectsWrongThreadAndExpiresAfterRecording()
    {
        UiDrawingContext? captured = null;
        Exception? error = null;
        var screen = CreateScreen(context =>
        {
            captured = context;
            var thread = new Thread(() => error = Record.Exception(() => DrawRectangle(context)));
            thread.Start();
            thread.Join();
            DrawRectangle(context);
        });

        Assert.Single(screen.CreateDrawCommandList());
        Assert.IsType<InvalidOperationException>(error);
        Assert.Throws<InvalidOperationException>(() => DrawRectangle(captured!));
    }

    [Fact]
    public void EmptyAndDefaultScopesDoNotLeaveCommandsButStillEnforceLifo()
    {
        Exception? orderError = null;
        var screen = CreateScreen(context =>
        {
            var transform = context.PushTranslate(Point.Zero);
            var scale = context.PushScale(1);
            var opacity = context.PushOpacity(1);
            try
            {
                transform.Dispose();
            }
            catch (Exception exception)
            {
                orderError = exception;
            }
            opacity.Dispose();
            scale.Dispose();
            transform.Dispose();
            using (context.PushTransform(new Point(1, 2), 3))
            using (context.SetTransform(Point.Zero))
            using (context.SetTranslate(Point.Zero))
            using (context.SetScale(1))
            using (context.PushClip(new Rect(0, 0, 1, 1)))
            using (context.PushOpacity(0.5))
            {
            }
            DrawRectangle(context);
        });

        Assert.IsType<UiFillRectangleCommand>(Assert.Single(screen.CreateDrawCommandList()));
        Assert.IsType<InvalidOperationException>(orderError);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidTransformDoesNotAlterRecordedState(double scale)
    {
        var screen = CreateScreen(context =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = context.PushTransform(Point.Zero, scale); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = context.PushScale(scale); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = context.SetTransform(Point.Zero, scale); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = context.SetScale(scale); });
            DrawRectangle(context);
        });

        Assert.IsType<UiFillRectangleCommand>(Assert.Single(screen.CreateDrawCommandList()));
    }

    [Fact]
    public void RecordedMixedScopesRestoreAllStateBeforeFollowingDraws()
    {
        var screen = CreateScreen(context =>
        {
            using (context.PushTransform(new Point(10, 20), 2))
            {
                using (context.PushClip(new Rect(0, 0, 5, 5)))
                using (context.PushOpacity(0.5))
                {
                    using (context.PushTranslate(new Point(1, 1)))
                    using (context.PushScale(3))
                        DrawRectangle(context);
                    DrawRectangle(context);
                }
                DrawRectangle(context);
            }
            DrawRectangle(context);
        });
        var builder = new UiDrawBuilder();
        builder.Build(screen.CreateDrawCommandList(), 100, 100, false);

        Assert.Equal(new UiVertex(12, 22, 1, 1, 1, 0.5f), builder.Vertices[0]);
        Assert.Equal(new UiVertex(24, 34, 1, 1, 1, 0.5f), builder.Vertices[2]);
        Assert.Equal(new UiVertex(10, 20, 1, 1, 1, 0.5f), builder.Vertices[4]);
        Assert.Equal(new UiVertex(14, 24, 1, 1, 1, 0.5f), builder.Vertices[6]);
        Assert.Equal(new UiVertex(10, 20, 1, 1, 1, 1), builder.Vertices[8]);
        Assert.Equal(new UiVertex(0, 0, 1, 1, 1, 1), builder.Vertices[12]);
        Assert.Equal(
            [new UiBatch(new UiScissor(10, 20, 10, 10), 0, 12), new UiBatch(new UiScissor(0, 0, 100, 100), 12, 12)],
            builder.Batches.ToArray());
    }

    private static UiScreen CreateScreen(Action<UiDrawingContext> draw, double scale = 1, Point origin = default)
    {
        var node = new DrawingNode { DrawAction = draw };
        var screen = new UiScreen(node) { Scale = scale, UseLayoutRounding = false };
        Arrange(node, origin);
        return screen;
    }

    private static void Arrange(UiNode node, Point origin)
    {
        node.Measure(new Size(40, 40));
        node.Arrange(new Rect(origin.X, origin.Y, 40, 40));
    }

    private static void DrawRectangle(UiDrawingContext context) =>
        context.FillRectangle(new Rect(0, 0, 2, 2), new Color(255, 255, 255));

    private sealed class DrawingNode : UiNode
    {
        internal Action<UiDrawingContext>? DrawAction { get; set; }

        protected override void DrawCore(UiDrawingContext context) => DrawAction?.Invoke(context);
    }
}
