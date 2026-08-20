using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Rendering;
using PanguEngine.Graphics;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;
using PanguEngine.Windowing;

namespace PanguEngine.Client.Tests.UiBatch;

internal static class UiBatch
{
    private static void Main() =>
        ClientTestApp.Run(new UiBatchScene());
}

internal sealed class UiBatchScene : IClientTestScene
{
    private const double UiScale = 1.25;
    private Presenter _presenter = null!;
    private UiRenderer _renderer = null!;
    private UiManager _uiManager = null!;
    private UiBatchNode _root = null!;
    private UiScreen _screen = null!;
    private UiImage _firstImage = null!;
    private UiImage _secondImage = null!;
    private bool _buttonStatesInitialized;

    public string Name => "UI Batch";

    public void Initialize(Window window)
    {
        _presenter = window.Presenter;
        TextServices.Initialize();
        try
        {
            TextServices.FontManager.RegisterResources(Engine.ResourceManager);
            TextServices.FontManager.DefaultFont = new Font("Source Han Sans CN");
        }
        catch
        {
            TextServices.Dispose();
            throw;
        }

        _renderer = new UiRenderer(
            ClientTestApp.Current.Device,
            TextServices.FontManager,
            _presenter.ColorFormat,
            TextureFormat.Undefined,
            _presenter.MaxFramesInFlight);
        _firstImage = CreateCheckerImage(16, 16, new Color(240, 70, 80), new Color(40, 205, 160));
        _secondImage = CreateCheckerImage(12, 20, new Color(70, 125, 245), new Color(245, 215, 70));
        _root = new UiBatchNode(_firstImage, _secondImage);
        _screen = new UiScreen(_root) { Scale = UiScale };
        _uiManager = new UiManager();
        _uiManager.Open(_screen);
        window.Render += (_, _) => DrawFrame();
    }

    public void Destroy()
    {
        try
        {
            _uiManager.Destroy();
        }
        finally
        {
            try
            {
                _renderer.Destroy();
            }
            finally
            {
                TextServices.Dispose();
            }
        }
    }

    private void DrawFrame()
    {
        _renderer.ProcessFinalizedResources();
        if (!_presenter.TryBeginFrame(out var frame))
            return;

        try
        {
            var commandList = frame.CommandList;
            commandList.BeginRecording();
            if (frame.Width == 0 || frame.Height == 0)
            {
                commandList.PrepareForPresent(frame.ColorOutput);
                commandList.EndRecording();
                return;
            }

            _root.Dense = frame.FrameNumber >= _presenter.MaxFramesInFlight;
            _uiManager.Update(new Size(frame.Width, frame.Height));
            if (!_buttonStatesInitialized)
            {
                _root.EstablishButtonStates(_uiManager);
                _buttonStatesInitialized = true;
            }

            var drawCommands = _screen.CreateDrawCommandList();

            commandList.BeginRendering(new RenderingDescription
            {
                Width = frame.Width,
                Height = frame.Height,
                ColorAttachments =
                [
                    new ColorAttachmentDescription(
                        frame.ColorOutput,
                        new ClearColor(0.015f, 0.018f, 0.024f, 1))
                ]
            });
            _renderer.Draw(frame, drawCommands);
            commandList.EndRendering();
            commandList.PrepareForPresent(frame.ColorOutput);
            commandList.EndRecording();
        }
        finally
        {
            _presenter.EndFrame(frame);
        }
    }

    private static UiImage CreateCheckerImage(int width, int height, Color first, Color second)
    {
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                var color = border || (x / 4 + y / 4) % 2 == 0 ? first : second;
                var offset = (y * width + x) * 4;
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
                pixels[offset + 3] = border ? (byte)255 : (byte)180;
            }
        }

        return UiImage.FromRgba(pixels, width, height);
    }

    private static ImageView CreateImageView(UiImage source, ImageStretch stretch)
    {
        var imageView = new ImageView
        {
            Source = source,
            Stretch = stretch,
            SamplingMode = ImageSamplingMode.Nearest
        };
        return imageView;
    }

    private sealed class UiBatchNode : Panel
    {
        private const double DesignWidth = 640;
        private const double DesignHeight = 750;
        private readonly UiImage _firstImage;
        private readonly UiImage _secondImage;
        private readonly ImageView[] _imageViews;
        private readonly Text[] _textNodes;
        private readonly TextClipPanel _textClipPanel;
        private readonly DecorationPanel _decorationPanel;
        private readonly ButtonPanel _buttonPanel;

        internal UiBatchNode(UiImage firstImage, UiImage secondImage)
        {
            _firstImage = firstImage;
            _secondImage = secondImage;
            _imageViews =
            [
                CreateImageView(_firstImage, ImageStretch.None),
                CreateImageView(_firstImage, ImageStretch.Fill),
                CreateImageView(_secondImage, ImageStretch.Uniform),
                CreateImageView(_secondImage, ImageStretch.UniformToFill)
            ];
            _textNodes =
            [
                new Text
                {
                    Content = "Dynamic glyph atlas  Aa 0123",
                    FontSize = 24,
                    Color = new Color(245, 245, 245),
                    Wrapping = TextWrapping.NoWrap
                },
                new Text
                {
                    Content = "动态字形图集与简体中文换行",
                    Font = new Font("Missing UI Family"),
                    FontSize = 18,
                    Color = new Color(95, 225, 185),
                    Wrapping = TextWrapping.Wrap,
                    Alignment = TextAlignment.Center
                },
                new Text
                {
                    Content = "Right aligned\nshort line",
                    FontSize = 14,
                    Color = new Color(245, 210, 70),
                    Wrapping = TextWrapping.NoWrap,
                    Alignment = TextAlignment.Right,
                    Opacity = 0.65
                }
            ];
            _textClipPanel = new TextClipPanel(_textNodes[1])
            {
                ClipToBounds = true,
                Background = new SolidColorBrush(25, 60, 55, 180),
                Padding = new Thickness(4)
            };
            Children.Add(_imageViews[0]);
            Children.Add(_textNodes[0]);
            Children.Add(_imageViews[1]);
            Children.Add(_textClipPanel);
            Children.Add(_imageViews[2]);
            Children.Add(_textNodes[2]);
            Children.Add(_imageViews[3]);

            _decorationPanel = new DecorationPanel
            {
                Background = new SolidColorBrush(35, 150, 125, 145),
                BorderBrush = new SolidColorBrush(230, 75, 65, 185),
                BorderThickness = new Thickness(6, 10, 14, 8),
                Padding = new Thickness(8)
            };
            _decorationPanel.Children.Add(new DecorationContentNode());
            Children.Add(_decorationPanel);

            _buttonPanel = new ButtonPanel(_firstImage);
            Children.Add(_buttonPanel);
        }

        internal bool Dense { get; set; }

        internal void EstablishButtonStates(UiManager manager) =>
            _buttonPanel.EstablishStates(manager);

        protected override Size MeasureContent(Size availableSize)
        {
            foreach (var imageView in _imageViews)
                imageView.Measure(Size.Infinite);
            _textNodes[0].Measure(Size.Infinite);
            _textClipPanel.Measure(new Size(220, 52));
            _textNodes[2].Measure(Size.Infinite);
            _decorationPanel.Measure(Size.Infinite);
            _buttonPanel.Measure(new Size(560, 150));

            return Size.Zero;
        }

        protected override void ArrangeContent(Rect contentBounds)
        {
            var layoutScale = Math.Min(contentBounds.Width / DesignWidth, contentBounds.Height / DesignHeight);
            var offsetX = (contentBounds.Width - DesignWidth * layoutScale) / 2;
            var offsetY = (contentBounds.Height - DesignHeight * layoutScale) / 2;
            var imageBounds = new[]
            {
                new Rect(40, 350, 120, 90),
                new Rect(180, 350, 120, 90),
                new Rect(320, 350, 120, 90),
                new Rect(460, 350, 120, 90)
            };

            for (var index = 0; index < _imageViews.Length; index++)
                _imageViews[index].Arrange(Scale(imageBounds[index], layoutScale, offsetX, offsetY));

            _textNodes[0].Arrange(
                Scale(new Rect(50, 215, 350, 30), layoutScale, offsetX, offsetY));
            _textClipPanel.Arrange(
                Scale(new Rect(360, 188, 220, 54), layoutScale, offsetX, offsetY));
            _textNodes[2].Arrange(
                Scale(new Rect(250, 305, 350, 42), layoutScale, offsetX, offsetY));

            _decorationPanel.Arrange(
                Scale(new Rect(160, 460, 320, 70), layoutScale, offsetX, offsetY));
            _buttonPanel.Arrange(
                Scale(new Rect(40, 560, 560, 150), layoutScale, offsetX, offsetY));
        }

        protected override void DrawCore(UiDrawingContext context)
        {
            var layoutScale = Math.Min(LayoutBounds.Width / DesignWidth, LayoutBounds.Height / DesignHeight);
            var offsetX = (LayoutBounds.Width - DesignWidth * layoutScale) / 2;
            var offsetY = (LayoutBounds.Height - DesignHeight * layoutScale) / 2;

            using (context.PushClip(Scale(new Rect(40, 40, 300, 180), layoutScale, offsetX, offsetY)))
            {
                context.FillRectangle(Scale(new Rect(20, 20, 260, 150), layoutScale, offsetX, offsetY),
                    new Color(230, 64, 80, 180));
                context.FillRectangle(Scale(new Rect(120, 70, 260, 150), layoutScale, offsetX, offsetY),
                    new Color(40, 190, 150, 160));
                var count = Dense ? 128 : 4;
                for (var index = 0; index < count; index++)
                {
                    var x = 48 + index % 16 * 17;
                    var y = 48 + index / 16 * 17;
                    context.FillRectangle(Scale(new Rect(x, y, 12, 12), layoutScale, offsetX, offsetY),
                        new Color(245, 210, 70, 150));
                }
            }

            using (context.PushClip(Scale(new Rect(380, 80, 220, 220), layoutScale, offsetX, offsetY)))
            {
                context.FillRectangle(Scale(new Rect(340, 40, 300, 300), layoutScale, offsetX, offsetY),
                    new Color(70, 110, 240, 150));
                context.FillRectangle(Scale(new Rect(430, 130, 140, 140), layoutScale, offsetX, offsetY),
                    new Color(245, 245, 245, 120));
            }

            var imagePanels = new[]
            {
                new Rect(40, 250, 120, 90),
                new Rect(180, 250, 120, 90),
                new Rect(320, 250, 120, 90),
                new Rect(460, 250, 120, 90)
            };
            foreach (var panel in imagePanels)
                context.FillRectangle(Scale(panel, layoutScale, offsetX, offsetY), new Color(32, 36, 44));

            context.DrawImage(
                Scale(new Rect(40, 250, 120, 90), layoutScale, offsetX, offsetY),
                _firstImage,
                samplingMode: ImageSamplingMode.Linear);
            context.DrawImage(
                Scale(new Rect(180, 250, 120, 90), layoutScale, offsetX, offsetY),
                _firstImage,
                samplingMode: ImageSamplingMode.Nearest);
            context.DrawImage(
                Scale(new Rect(320, 250, 120, 90), layoutScale, offsetX, offsetY),
                _firstImage,
                new Rect(4, 4, 8, 8),
                ImageSamplingMode.Linear);

            using (context.PushClip(Scale(new Rect(480, 260, 80, 70), layoutScale, offsetX, offsetY)))
            using (context.PushOpacity(0.65))
            {
                context.DrawImage(
                    Scale(new Rect(450, 235, 140, 120), layoutScale, offsetX, offsetY),
                    _secondImage,
                    samplingMode: ImageSamplingMode.Nearest);
            }
        }

        private sealed class DecorationPanel : Panel
        {
        }

        private sealed class DecorationContentNode : UiNode
        {
            protected override void DrawCore(UiDrawingContext context) =>
                context.FillRectangle(
                    new Rect(0, 0, LayoutBounds.Width, LayoutBounds.Height),
                    new Color(245, 245, 245, 210));
        }

        private sealed class TextClipPanel : Panel
        {
            internal TextClipPanel(Text child) => Children.Add(child);
        }

        private sealed class ButtonPanel : Panel
        {
            private const int ColumnCount = 4;
            private readonly Button _hoverButton;
            private readonly Button _pressedButton;
            private readonly Button _focusedButton;

            internal ButtonPanel(UiImage image)
            {
                var buttons = new Button[]
                {
                    new() { Text = "Normal" },
                    new() { Text = "Hover" },
                    new() { Text = "Pressed" },
                    new() { Text = "Focused" },
                    new() { Text = "Disabled", IsEnabled = false },
                    new() { Text = "Mix", Icon = image },
                    new() { Icon = image },
                    new()
                };
                _hoverButton = buttons[1];
                _pressedButton = buttons[2];
                _focusedButton = buttons[3];
                foreach (var button in buttons)
                    Children.Add(button);
            }

            internal void EstablishStates(UiManager manager)
            {
                var pressedCenter = GetOutputCenter(_pressedButton);
                manager.ProcessPointerMoved(pressedCenter);
                manager.ProcessPointerPressed(
                    pressedCenter,
                    MouseButton.Left,
                    KeyModifiers.None);
                _ = _focusedButton.Focus();
                manager.ProcessPointerMoved(GetOutputCenter(_hoverButton));
            }

            protected override Size MeasureContent(Size availableSize)
            {
                var (cellSize, _, _) = GetGridMetrics(availableSize);
                foreach (var child in Children)
                    child.Measure(cellSize);
                return availableSize;
            }

            protected override void ArrangeContent(Rect contentBounds)
            {
                var (cellSize, columnSpacing, rowSpacing) = GetGridMetrics(
                    new Size(contentBounds.Width, contentBounds.Height));
                for (var index = 0; index < Children.Count; index++)
                {
                    var column = index % ColumnCount;
                    var row = index / ColumnCount;
                    Children[index].Arrange(new Rect(
                        contentBounds.X + column * (cellSize.Width + columnSpacing),
                        contentBounds.Y + row * (cellSize.Height + rowSpacing),
                        cellSize));
                }
            }

            private static (Size CellSize, double ColumnSpacing, double RowSpacing) GetGridMetrics(
                Size availableSize)
            {
                var columnSpacing = availableSize.Width / 56;
                var rowSpacing = availableSize.Height * 0.08;
                return (
                    new Size(
                        (availableSize.Width - (ColumnCount - 1) * columnSpacing) / ColumnCount,
                        (availableSize.Height - rowSpacing) / 2),
                    columnSpacing,
                    rowSpacing);
            }

            private static Point GetOutputCenter(Button button)
            {
                var logicalCenter = button.LocalToScreen(new Point(
                    button.LayoutBounds.Width / 2,
                    button.LayoutBounds.Height / 2));
                var scale = button.Screen!.Scale;
                return new Point(logicalCenter.X * scale, logicalCenter.Y * scale);
            }
        }

        private static Rect Scale(Rect rect, double layoutScale, double offsetX, double offsetY) =>
            new(
                offsetX + rect.X * layoutScale,
                offsetY + rect.Y * layoutScale,
                rect.Width * layoutScale,
                rect.Height * layoutScale);
    }
}