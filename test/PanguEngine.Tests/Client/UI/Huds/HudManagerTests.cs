using System.Numerics;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Huds;
using PanguEngine.Input;
using PanguEngine.Registries;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Rendering;

namespace PanguEngine.Tests.Client.UI.Huds;

public sealed class HudManagerTests
{
    [Fact]
    public void PostedActionsCanChangeComponentChildren()
    {
        var manager = new UIManager();

        try
        {
            var root = new Panel();
            InitializeHud(manager, root);
            var panel = new Panel { Width = 10, Height = 10 };

            manager.Hud.Post(() => root.Children.Add(panel));

            Assert.Empty(root.Children);

            manager.UpdateFrame(new Size(200, 100), 0);

            Assert.Same(panel, Assert.Single(root.Children));
            Assert.Same(root, panel.Parent);
            Assert.Same(manager.Hud.Scene, panel.Scene);
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
        var manager = new UIManager();

        manager.Destroy();

        Assert.Throws<InvalidOperationException>(() => manager.Hud.Post(static () => { }));
    }

    [Fact]
    public void HudNodesDoNotReceiveManagerInputRouting()
    {
        var manager = new UIManager();
        var leaf = new FocusableTestNode { Width = 200, Height = 100 };

        try
        {
            InitializeHud(manager, leaf);
            manager.UpdateFrame(new Size(200, 100), 0);

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
    public void SharedCommandsDrawHudBeforeSceneWithIsolatedState()
    {
        var manager = new UIManager();
        try
        {
            manager.Hud.Scene.Scale = 2;
            InitializeHud(manager, new DrawingNode
            {
                DrawAction = context =>
                {
                    using var clip = context.PushClip(new Rect(0, 0, 5, 5));
                    using var opacity = context.PushOpacity(0.25);
                    context.FillRectangle(new Rect(1, 2, 4, 6), new Color(255, 0, 0));
                }
            });
            var scene = new GameUIScene(new DrawingNode
            {
                DrawAction = context =>
                    context.FillRectangle(new Rect(1, 2, 4, 6), new Color(0, 255, 0))
            }) { Scale = 0.5 };
            manager.Open(scene);
            manager.UpdateFrame(new Size(200, 100), 0);
            var commands = new DrawCommandList();
            var builder = new DrawBuilder();

            manager.AppendDrawCommands(commands);
            builder.Build(commands, 200, 100, false);

            Assert.Equal(2, builder.RectangleCount);
            Assert.Equal(new Vertex(2, 4, 1, 0, 0, 0.25f, drawDataIndex: builder.Vertices[0].DrawDataIndex),
                builder.Vertices[0]);
            Assert.Equal(new Vertex(10, 10, 1, 0, 0, 0.25f, drawDataIndex: builder.Vertices[0].DrawDataIndex),
                builder.Vertices[2]);
            Assert.Equal(new Vertex(0.5f, 1, 0, 1, 0, 1), builder.Vertices[4]);
            Assert.Equal(new Vertex(2.5f, 4, 0, 1, 0, 1), builder.Vertices[6]);
            Assert.Equal(
                [new Batch(new Scissor(0, 0, 200, 100), 0, 12)],
                builder.Batches.ToArray());
            var data = builder.DrawData[(int)builder.Vertices[0].DrawDataIndex];
            Assert.Equal(new Vector4(0, 0, 10, 10), builder.ClipData[(int)data.ClipIndex].Bounds);

            manager.Close();
            commands.Clear();
            manager.AppendDrawCommands(commands);
            builder.Build(commands, 200, 100, false);

            Assert.Single(commands.OfType<FillRectangleCommand>());
            Assert.Equal(1, builder.RectangleCount);
            Assert.Equal(new Vertex(2, 4, 1, 0, 0, 0.25f, drawDataIndex: builder.Vertices[0].DrawDataIndex),
                builder.Vertices[0]);
        }
        finally
        {
            manager.Destroy();
        }
    }

    [Fact]
    public void FailedSceneRecordingDiscardsHudCommandsAndAllowsReuse()
    {
        var manager = new UIManager();
        try
        {
            InitializeHud(manager, new DrawingNode
            {
                DrawAction = context =>
                    context.FillRectangle(new Rect(0, 0, 10, 10), new Color(255, 255, 255))
            });
            var expected = new InvalidOperationException("scene drawing failed");
            manager.Open(new GameUIScene(new DrawingNode
            {
                DrawAction = _ => throw expected
            }));
            manager.UpdateFrame(new Size(200, 100), 0);
            var commands = new DrawCommandList();

            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => manager.AppendDrawCommands(commands)));
            Assert.Empty(commands);

            manager.Close();
            manager.AppendDrawCommands(commands);
            var builder = new DrawBuilder();
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
        var manager = new UIManager();
        try
        {
            var commands = new DrawCommandList();
            var errors = new List<Exception?>();
            var postedActionExecuted = false;
            var node = new DrawingNode
            {
                DrawAction = context =>
                {
                    errors.Add(Record.Exception(manager.Close));
                    errors.Add(Record.Exception(manager.Destroy));
                    errors.Add(Record.Exception(() => manager.Open(new GameUIScene(new Panel()))));
                    errors.Add(Record.Exception(() => manager.AppendDrawCommands(commands)));
                    errors.Add(Record.Exception(() => manager.UpdateFrame(new Size(200, 100), 0)));
                    context.FillRectangle(new Rect(0, 0, 10, 10), new Color(255, 0, 0));
                }
            };
            if (drawInHud)
            {
                InitializeHud(manager, node);
            }
            else
            {
                manager.Open(new GameUIScene(node));
            }

            manager.UpdateFrame(new Size(200, 100), 0);
            manager.Hud.Post(() => postedActionExecuted = true);

            manager.AppendDrawCommands(commands);

            Assert.Equal(5, errors.Count);
            Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
            Assert.Single(commands.OfType<FillRectangleCommand>());
            Assert.False(postedActionExecuted);

            manager.UpdateFrame(new Size(200, 100), 0);

            Assert.True(postedActionExecuted);
        }
        finally
        {
            manager.Destroy();
        }
    }

    private static void InitializeHud(UIManager manager, UIElement root)
    {
        var definitions = new Registry<HudDefinition>(RegistryKeys.Hud);
        definitions.Register(ResourceKey.Create("test", "hud"), new HudDefinition(() => new TestHud(root)));
        definitions.Freeze();
        manager.InitializeHud(definitions);
    }

    private sealed class TestHud(UIElement root) : Hud(root)
    {
    }

    private sealed class DrawingNode : UIElement
    {
        public Action<DrawingContext>? DrawAction { get; init; }

        protected override void DrawCore(DrawingContext context) => DrawAction?.Invoke(context);
    }

    private sealed class FocusableTestNode : UIElement
    {
        public FocusableTestNode()
        {
            Focusable = true;
        }
    }
}