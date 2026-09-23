using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.Screens;

/// <summary>
/// Provides the private layout and visual structure of the UI showcase page.
/// </summary>
internal sealed class UiShowcaseShell : Region
{
    internal const double CompactWidthThreshold = 620;
    internal const double CompactHeightThreshold = 460;
    internal const double FullWidthThreshold = 800;
    internal const double FullHeightThreshold = 600;

    private const double Gap = 8;
    private const double CompactNavMinWidth = 72;
    private const double NormalNavMinWidth = 140;

    private static readonly string[] CategoryLabels = ["基础控件", "样式", "绑定", "布局"];
    private static readonly string[] CompactCategoryLabels = ["控件", "样式", "绑定", "布局"];

    private readonly Text _heading;
    private readonly Text _escapeHint;
    private readonly StackPanel _header;
    private readonly StackPanel _nav;
    private readonly Button[] _categoryButtons;
    private readonly Panel _contentClip;
    private readonly Text _exampleTitle;
    private readonly Text _exampleInstructions;
    private readonly StackPanel _exampleSwitches;
    private readonly Panel _contentHost;
    private readonly Text _feedback;
    private readonly Text _warning;
    private readonly StackPanel _scaleRow;
    private readonly Text _scaleValue;
    private readonly Button _scaleDown;
    private readonly Button _scaleReset;
    private readonly Button _scaleUp;
    private readonly Button _restoreScale;
    private readonly Button _returnButton;

    private bool _isCompact;
    private bool _showWarning;

    internal UiShowcaseShell()
    {
        Classes.Add("showcase-shell");
        ClipToBounds = true;
        Background = new SolidColorBrush(12, 14, 18);

        _heading = CreateClassedText("UI Showcase", "showcase-heading");
        _escapeHint = CreateClassedText("Esc：回到游戏", "showcase-hint");
        _header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            StyleId = "showcase-header"
        };
        _header.Children.Add(_heading);
        _header.Children.Add(_escapeHint);

        _categoryButtons = new Button[CategoryLabels.Length];
        _nav = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = Gap,
            StyleId = "showcase-nav-list"
        };
        for (var index = 0; index < CategoryLabels.Length; index++)
        {
            var button = new Button
            {
                Text = CategoryLabels[index],
                MinWidth = NormalNavMinWidth,
                MinHeight = 36,
                StyleId = $"showcase-nav-{index}"
            };
            button.Classes.Add("showcase-nav");
            _categoryButtons[index] = button;
            _nav.Children.Add(button);
        }

        _exampleTitle = UiShowcaseWidgets.Label(string.Empty, "showcase-example-title");
        _exampleInstructions = UiShowcaseWidgets.Label(string.Empty, "showcase-example-instructions");
        _exampleSwitches = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gap,
            StyleId = "showcase-example-switches"
        };
        _contentHost = new Panel { StyleId = "showcase-slot" };
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = Gap,
            StyleId = "showcase-content-panel"
        };
        contentPanel.Children.Add(_exampleTitle);
        contentPanel.Children.Add(_exampleInstructions);
        contentPanel.Children.Add(_exampleSwitches);
        contentPanel.Children.Add(_contentHost);
        _contentClip = new Panel
        {
            ClipToBounds = true,
            StyleId = "showcase-content"
        };
        _contentClip.Children.Add(contentPanel);

        _feedback = CreateClassedText(string.Empty, "showcase-feedback");
        _warning = CreateClassedText("窗口较小，请放大窗口或恢复缩放", "showcase-warning");
        _warning.Visibility = Visibility.Collapsed;

        _scaleDown = CreateScaleButton("0.75x", "showcase-scale-down");
        _scaleReset = CreateScaleButton("1x", "showcase-scale-reset");
        _scaleUp = CreateScaleButton("1.25x", "showcase-scale-up");
        _restoreScale = CreateScaleButton("恢复", "showcase-scale-restore");
        _scaleValue = UiShowcaseWidgets.Label(string.Empty, "showcase-scale-value");
        _scaleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gap,
            StyleId = "showcase-scale"
        };
        _scaleRow.Children.Add(_scaleDown);
        _scaleRow.Children.Add(_scaleReset);
        _scaleRow.Children.Add(_scaleUp);
        _scaleRow.Children.Add(_restoreScale);

        _returnButton = new Button
        {
            Text = "返回暂停菜单",
            MinHeight = 36,
            StyleId = "showcase-return"
        };
        _returnButton.Classes.Add("showcase-return");

        AddChild(_header);
        AddChild(_nav);
        AddChild(_contentClip);
        AddChild(_feedback);
        AddChild(_warning);
        AddChild(_scaleRow);
        AddChild(_scaleValue);
        AddChild(_returnButton);
    }

    internal IReadOnlyList<Button> CategoryButtons => _categoryButtons;

    internal IReadOnlyList<Button> ScaleButtons => new[] { _scaleDown, _scaleReset, _scaleUp, _restoreScale };

    internal Button ScaleDownButton => _scaleDown;

    internal Button ScaleResetButton => _scaleReset;

    internal Button ScaleUpButton => _scaleUp;

    internal Button RestoreScaleButton => _restoreScale;

    internal Button ReturnButton => _returnButton;

    internal Panel ContentHost => _contentHost;

    internal Panel ExampleSwitches => _exampleSwitches;

    internal bool IsCompact => _isCompact;

    internal bool ShowWarning => _showWarning;

    internal void SetCategorySelected(int index)
    {
        for (var position = 0; position < _categoryButtons.Length; position++)
        {
            if (position == index)
                _categoryButtons[position].Classes.Add("selected");
            else
                _categoryButtons[position].Classes.Remove("selected");
        }
    }

    internal void SetFeedback(string message) => _feedback.Content = message;

    internal void SetExampleHeader(string title, string instructions)
    {
        _exampleTitle.Content = title;
        _exampleInstructions.Content = instructions;
    }

    internal void SetScaleValue(string text) => _scaleValue.Content = text;

    internal void ApplyViewportMode(Size viewport)
    {
        var compact = viewport.Width < CompactWidthThreshold || viewport.Height < CompactHeightThreshold;
        var warning = viewport.Width < FullWidthThreshold || viewport.Height < FullHeightThreshold;
        if (compact != _isCompact)
        {
            _isCompact = compact;
            _nav.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
            for (var index = 0; index < _categoryButtons.Length; index++)
            {
                _categoryButtons[index].Text = compact ? CompactCategoryLabels[index] : CategoryLabels[index];
                _categoryButtons[index].MinWidth = compact ? CompactNavMinWidth : NormalNavMinWidth;
            }

            InvalidateMeasure();
        }

        if (warning != _showWarning)
        {
            _showWarning = warning;
            _warning.Visibility = warning ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var width = availableSize.Width;
        _header.Measure(new Size(width, double.PositiveInfinity));
        if (_isCompact)
            _nav.Measure(new Size(width, double.PositiveInfinity));
        else
            _nav.Measure(new Size(double.PositiveInfinity, availableSize.Height));
        _feedback.Measure(new Size(width, double.PositiveInfinity));
        _warning.Measure(new Size(width, double.PositiveInfinity));
        _scaleRow.Measure(new Size(width, double.PositiveInfinity));
        _scaleValue.Measure(new Size(width, double.PositiveInfinity));
        _returnButton.Measure(new Size(_isCompact ? width : double.PositiveInfinity, double.PositiveInfinity));

        var navWidth = _isCompact ? 0 : _nav.DesiredSize.Width;
        var contentWidth = Math.Max(0, width - navWidth - (_isCompact ? 0 : Gap));
        _contentClip.Measure(new Size(contentWidth, double.PositiveInfinity));

        if (double.IsFinite(width) && double.IsFinite(availableSize.Height))
            return availableSize;

        var fallbackWidth = double.IsFinite(width)
            ? width
            : Math.Max(
                Math.Max(_header.DesiredSize.Width, _contentClip.DesiredSize.Width),
                Math.Max(_nav.DesiredSize.Width, _scaleRow.DesiredSize.Width));
        return new Size(
            fallbackWidth,
            _header.DesiredSize.Height +
            _nav.DesiredSize.Height +
            _contentClip.DesiredSize.Height +
            _feedback.DesiredSize.Height +
            _warning.DesiredSize.Height +
            _scaleRow.DesiredSize.Height +
            _scaleValue.DesiredSize.Height +
            _returnButton.DesiredSize.Height +
            Gap * 7);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var left = contentBounds.X;
        var top = contentBounds.Y;
        var width = contentBounds.Width;
        var bottom = top + contentBounds.Height;

        var headerHeight = _header.DesiredSize.Height;
        ArrangeNode(_header, left, top, width, headerHeight);
        var middleTop = top + headerHeight + Gap;

        var returnHeight = _returnButton.DesiredSize.Height;
        var scaleHeight = _scaleRow.DesiredSize.Height;
        var valueHeight = _scaleValue.DesiredSize.Height;
        var warningHeight = _showWarning ? _warning.DesiredSize.Height : 0;
        var feedbackHeight = _feedback.DesiredSize.Height;

        double middleBottom;
        if (_isCompact)
        {
            var returnTop = bottom - returnHeight;
            var scaleTop = returnTop - Gap - scaleHeight;
            var valueTop = scaleTop - Gap - valueHeight;
            var warningTop = valueTop - Gap - warningHeight;
            var feedbackTop = warningTop - Gap - feedbackHeight;
            ArrangeNode(_returnButton, left, returnTop, width, returnHeight);
            ArrangeNode(_scaleRow, left, scaleTop, width, scaleHeight);
            ArrangeNode(_scaleValue, left, valueTop, width, valueHeight);
            ArrangeNode(_warning, left, warningTop, width, warningHeight);
            ArrangeNode(_feedback, left, feedbackTop, width, feedbackHeight);
            middleBottom = feedbackTop - Gap;
        }
        else
        {
            var footerHeight = Math.Max(returnHeight, scaleHeight);
            var footerTop = bottom - footerHeight;
            var returnWidth = Math.Min(_returnButton.DesiredSize.Width, width);
            ArrangeNode(_scaleRow, left, footerTop, width, scaleHeight);
            ArrangeNode(_returnButton, left + Math.Max(0, width - returnWidth), footerTop, returnWidth, returnHeight);
            var valueTop = footerTop - Gap - valueHeight;
            var warningTop = valueTop - Gap - warningHeight;
            var feedbackTop = warningTop - Gap - feedbackHeight;
            ArrangeNode(_scaleValue, left, valueTop, width, valueHeight);
            ArrangeNode(_warning, left, warningTop, width, warningHeight);
            ArrangeNode(_feedback, left, feedbackTop, width, feedbackHeight);
            middleBottom = feedbackTop - Gap;
        }

        var middleHeight = Math.Max(0, middleBottom - middleTop);
        if (_isCompact)
        {
            var navHeight = _nav.DesiredSize.Height;
            ArrangeNode(_nav, left, middleTop, width, navHeight);
            var contentTop = middleTop + navHeight + Gap;
            ArrangeNode(_contentClip, left, contentTop, width, Math.Max(0, middleBottom - contentTop));
        }
        else
        {
            var navWidth = Math.Min(_nav.DesiredSize.Width, width);
            ArrangeNode(_nav, left, middleTop, navWidth, middleHeight);
            ArrangeNode(
                _contentClip,
                left + navWidth + Gap,
                middleTop,
                Math.Max(0, width - navWidth - Gap),
                middleHeight);
        }
    }

    private static void ArrangeNode(UiNode node, double x, double y, double width, double height) =>
        node.Arrange(new Rect(x, y, Math.Max(0, width), Math.Max(0, height)));

    private static Text CreateClassedText(string text, string cssClass)
    {
        var node = new Text { Content = text, Wrapping = TextWrapping.Wrap };
        node.Classes.Add(cssClass);
        return node;
    }

    private static Button CreateScaleButton(string text, string id) =>
        new()
        {
            Text = text,
            MinHeight = 32,
            StyleId = id
        };
}
