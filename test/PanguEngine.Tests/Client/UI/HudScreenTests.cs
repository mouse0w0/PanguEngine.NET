using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Input;
using PanguEngine.Registries;

namespace PanguEngine.Tests.Client.UI;

public sealed class HudScreenTests
{
    [Fact]
    public void PostedActionsCanChangeComponentChildren()
    {
        var manager = new UiManager();

        try
        {
            var root = new Panel();
            InitializeHud(manager, root);
            var panel = new Panel { Width = 10, Height = 10 };

            manager.Hud.Post(() => root.Children.Add(panel));

            Assert.Empty(root.Children);

            manager.PrepareFrame(new Size(200, 100), 0);

            Assert.Same(panel, Assert.Single(root.Children));
            Assert.Same(root, panel.Parent);
            Assert.Same(manager.Hud.Screen, panel.Screen);
            Assert.True(panel.IsArrangeValid);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void DestroyClosesHudPostQueue()
    {
        var manager = new UiManager();

        manager.Destroy();

        Assert.Throws<InvalidOperationException>(() => manager.Hud.Post(static () => { }));
    }

    [Fact]
    public void HudNodesDoNotReceiveManagerInputRouting()
    {
        var manager = new UiManager();
        var leaf = new FocusableTestNode { Width = 200, Height = 100 };

        try
        {
            InitializeHud(manager, leaf);
            manager.PrepareFrame(new Size(200, 100), 0);

            manager.ProcessPointerMoved(new Point(5, 5));
            manager.ProcessPointerPressed(new Point(5, 5), MouseButton.Left, KeyModifiers.None);
            manager.ProcessKeyDown(Key.A, KeyModifiers.None);
            manager.ProcessTextInput("a");

            Assert.False(leaf.IsHovered);
            Assert.False(leaf.IsFocused);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void SharedCommandsDrawHudBeforeScreenWithIsolatedState()
    {
        var manager = new UiManager();
        try
        {
            manager.Hud.Screen.Scale = 2;
            InitializeHud(manager, new DrawingNode
            {
                DrawAction = context =>
                {
                    using var clip = context.PushClip(new Rect(0, 0, 5, 5));
                    using var opacity = context.PushOpacity(0.25);
                    context.FillRectangle(new Rect(1, 2, 4, 6), new Color(255, 0, 0));
                }
            });
            var screen = new UiScreen(new DrawingNode
            {
                DrawAction = context =>
                    context.FillRectangle(new Rect(1, 2, 4, 6), new Color(0, 255, 0))
            }) { Scale = 0.5 };
            manager.Open(screen);
            manager.PrepareFrame(new Size(200, 100), 0);
            var commands = new UiDrawCommandList();
            var builder = new UiDrawBuilder();

            manager.AppendDrawCommands(commands);
            builder.Build(commands, 200, 100, false);

            Assert.Equal(2, builder.RectangleCount);
            Assert.Equal(new UiVertex(2, 4, 1, 0, 0, 0.25f), builder.Vertices[0]);
            Assert.Equal(new UiVertex(10, 16, 1, 0, 0, 0.25f), builder.Vertices[2]);
            Assert.Equal(new UiVertex(0.5f, 1, 0, 1, 0, 1), builder.Vertices[4]);
            Assert.Equal(new UiVertex(2.5f, 4, 0, 1, 0, 1), builder.Vertices[6]);
            Assert.Equal(
                [new UiBatch(new UiScissor(0, 0, 10, 10), 0, 6), new UiBatch(new UiScissor(0, 0, 200, 100), 6, 6)],
                builder.Batches.ToArray());

            manager.Close();
            commands.Clear();
            manager.AppendDrawCommands(commands);
            builder.Build(commands, 200, 100, false);

            Assert.Single(commands.OfType<UiFillRectangleCommand>());
            Assert.Equal(1, builder.RectangleCount);
            Assert.Equal(new UiVertex(2, 4, 1, 0, 0, 0.25f), builder.Vertices[0]);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void FailedScreenRecordingDiscardsHudCommandsAndAllowsReuse()
    {
        var manager = new UiManager();
        try
        {
            InitializeHud(manager, new DrawingNode
            {
                DrawAction = context =>
                    context.FillRectangle(new Rect(0, 0, 10, 10), new Color(255, 255, 255))
            });
            var expected = new InvalidOperationException("screen drawing failed");
            manager.Open(new UiScreen(new DrawingNode
            {
                DrawAction = _ => throw expected
            }));
            manager.PrepareFrame(new Size(200, 100), 0);
            var commands = new UiDrawCommandList();

            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => manager.AppendDrawCommands(commands)));
            Assert.Empty(commands);

            manager.Close();
            manager.AppendDrawCommands(commands);
            var builder = new UiDrawBuilder();
            builder.Build(commands, 200, 100, false);

            Assert.Equal(1, builder.RectangleCount);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SharedRecordingRejectsNestedManagerOperations(bool drawInHud)
    {
        var manager = new UiManager();
        try
        {
            var commands = new UiDrawCommandList();
            var errors = new List<Exception?>();
            var postedActionExecuted = false;
            var node = new DrawingNode
            {
                DrawAction = context =>
                {
                    errors.Add(Record.Exception(manager.Close));
                    errors.Add(Record.Exception(manager.Destroy));
                    errors.Add(Record.Exception(() => manager.Open(new UiScreen(new Panel()))));
                    errors.Add(Record.Exception(() => manager.AppendDrawCommands(commands)));
                    errors.Add(Record.Exception(() => manager.PrepareFrame(new Size(200, 100), 0)));
                    context.FillRectangle(new Rect(0, 0, 10, 10), new Color(255, 0, 0));
                }
            };
            if (drawInHud)
            {
                InitializeHud(manager, node);
            }
            else
            {
                manager.Open(new UiScreen(node));
            }
            manager.PrepareFrame(new Size(200, 100), 0);
            manager.Hud.Post(() => postedActionExecuted = true);

            manager.AppendDrawCommands(commands);

            Assert.Equal(5, errors.Count);
            Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
            Assert.Single(commands.OfType<UiFillRectangleCommand>());
            Assert.False(postedActionExecuted);

            manager.PrepareFrame(new Size(200, 100), 0);

            Assert.True(postedActionExecuted);
        }
        finally
        {
            manager.Destroy();
        }
    }

    private static void InitializeHud(UiManager manager, UiNode root)
    {
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "hud"), new HudDefinition(() => new TestHud(root)));
        definitions.Freeze();
        manager.InitializeHud(definitions);
    }

    private sealed class TestHud(UiNode root) : Hud(root)
    {
    }

    private sealed class DrawingNode : UiNode
    {
        public Action<UiDrawingContext>? DrawAction { get; init; }

        protected override void DrawCore(UiDrawingContext context) => DrawAction?.Invoke(context);
    }

    private sealed class FocusableTestNode : UiNode
    {
        public FocusableTestNode()
        {
            Focusable = true;
        }
    }
}
