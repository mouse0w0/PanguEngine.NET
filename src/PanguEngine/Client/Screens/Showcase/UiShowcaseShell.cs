using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Provides component navigation and a scrollable page with persistent controls.
/// </summary>
internal sealed class UiShowcaseShell : Region
{
    internal const double CompactWidthThreshold = 620;
    internal const double CompactHeightThreshold = 460;
    internal const double FullWidthThreshold = 800;
    internal const double FullHeightThreshold = 600;
    private const double Gap = 10;

    private readonly Text _escapeHint;
    private readonly StackPanel _header;
    private readonly TabView _pageTabs;
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
        _escapeHint = CreateClassedText("选择组件，直接体验控件 · Esc 回到游戏", "showcase-hint");
        _header = UiShowcaseWidgets.Column(CreateClassedText("UI 控件展示", "showcase-heading"), _escapeHint);
        _header.Spacing = 4;
        _pageTabs = new TabView
        {
            TabStripPlacement = TabStripPlacement.Left,
            StyleId = "showcase-component-tabs",
            CanReorderTabs = false
        };
        _feedback = CreateClassedText("悬停、点击、输入或滚动，体验真实交互。", "showcase-feedback");
        _warning = CreateClassedText("窗口较小，可缩小 UI 或放大窗口", "showcase-warning");
        _warning.Visibility = Visibility.Collapsed;
        _scaleDown = CreateScaleButton("0.75x", "showcase-scale-down");
        _scaleReset = CreateScaleButton("1x", "showcase-scale-reset");
        _scaleUp = CreateScaleButton("1.25x", "showcase-scale-up");
        _restoreScale = CreateScaleButton("恢复", "showcase-scale-restore");
        _scaleValue = UiShowcaseWidgets.Label(string.Empty, "showcase-scale-value");
        _scaleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap };
        _scaleRow.Children.Add(_scaleDown);
        _scaleRow.Children.Add(_scaleReset);
        _scaleRow.Children.Add(_scaleUp);
        _scaleRow.Children.Add(_restoreScale);
        _returnButton = new Button { Text = "返回暂停菜单", MinHeight = 36, StyleId = "showcase-return" };
        Children.Add(_header);
        Children.Add(_pageTabs);
        Children.Add(_feedback);
        Children.Add(_warning);
        Children.Add(_scaleRow);
        Children.Add(_scaleValue);
        Children.Add(_returnButton);
    }

    internal TabView PageTabs => _pageTabs;
    internal IReadOnlyList<Button> ScaleButtons => new[] { _scaleDown, _scaleReset, _scaleUp, _restoreScale };
    internal Button ScaleDownButton => _scaleDown;
    internal Button ScaleResetButton => _scaleReset;
    internal Button ScaleUpButton => _scaleUp;
    internal Button RestoreScaleButton => _restoreScale;
    internal Button ReturnButton => _returnButton;
    internal bool IsCompact => _isCompact;
    internal bool ShowWarning => _showWarning;

    internal void SetPages(IReadOnlyList<UiShowcaseExample> pages)
    {
        _pageTabs.Items.Clear();
        foreach (var page in pages)
        {
            var header = new Text { Content = page.Title, Wrapping = TextWrapping.NoWrap };
            header.Classes.Add("showcase-nav-header");
            var view = new ScrollView
            {
                Content = UiShowcaseWidgets.ExampleCard(page),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                ShowArrows = false
            };
            view.Classes.Add("showcase-gallery-view");
            var item = new TabItem { Header = header, Content = view };
            item.Classes.Add("showcase-nav");
            _pageTabs.Items.Add(item);
        }
    }

    internal void SetFeedback(string message) => _feedback.Content = message;
    internal void SetScaleValue(string text) => _scaleValue.Content = text;

    internal void ApplyViewportMode(Size viewport)
    {
        var compact = viewport.Width < CompactWidthThreshold || viewport.Height < CompactHeightThreshold;
        var warning = viewport.Width < FullWidthThreshold || viewport.Height < FullHeightThreshold;
        if (compact != _isCompact)
        {
            _isCompact = compact;
            var supplementalVisibility = compact ? Visibility.Collapsed : Visibility.Visible;
            _escapeHint.Visibility = supplementalVisibility;
            _feedback.Visibility = supplementalVisibility;
            _scaleValue.Visibility = supplementalVisibility;
            _restoreScale.Visibility = supplementalVisibility;
            if (compact)
            {
                Classes.Add("showcase-compact");
                _pageTabs.VerticalTabStripWidth = 76;
            }
            else
            {
                Classes.Remove("showcase-compact");
                _pageTabs.ClearValue(TabView.VerticalTabStripWidthProperty);
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
        foreach (var node in new UiNode[] { _header, _feedback, _warning, _scaleRow, _scaleValue, _returnButton })
            node.Measure(new Size(width, double.PositiveInfinity));
        var footerHeight = _isCompact
            ? _returnButton.DesiredSize.Height + Gap + _scaleRow.DesiredSize.Height
            : Math.Max(_returnButton.DesiredSize.Height, _scaleRow.DesiredSize.Height);
        var informationHeight = _showWarning ? _warning.DesiredSize.Height + Gap : 0;
        if (!_isCompact)
            informationHeight += _feedback.DesiredSize.Height + _scaleValue.DesiredSize.Height + Gap * 2;
        var reservedHeight = _header.DesiredSize.Height + footerHeight + informationHeight + Gap * 2;
        var pageHeight = double.IsPositiveInfinity(availableSize.Height)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Height - reservedHeight);
        _pageTabs.Measure(new Size(width, pageHeight));
        return new Size(
            double.IsFinite(width) ? width : Math.Max(_header.DesiredSize.Width, Math.Max(_pageTabs.DesiredSize.Width, _scaleRow.DesiredSize.Width)),
            double.IsFinite(availableSize.Height) ? availableSize.Height : _pageTabs.DesiredSize.Height + reservedHeight);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var left = contentBounds.X;
        var top = contentBounds.Y;
        var width = contentBounds.Width;
        var bottom = top + contentBounds.Height;
        ArrangeNode(_header, left, top, width, _header.DesiredSize.Height);
        var middleTop = top + _header.DesiredSize.Height + Gap;
        double middleBottom;
        if (_isCompact)
        {
            var returnTop = bottom - _returnButton.DesiredSize.Height;
            var scaleTop = returnTop - Gap - _scaleRow.DesiredSize.Height;
            ArrangeNode(_returnButton, left, returnTop, width, _returnButton.DesiredSize.Height);
            ArrangeNode(_scaleRow, left, scaleTop, width, _scaleRow.DesiredSize.Height);
            _feedback.Arrange(Rect.Zero);
            _scaleValue.Arrange(Rect.Zero);
            middleBottom = scaleTop - Gap;
        }
        else
        {
            var footerTop = bottom - Math.Max(_returnButton.DesiredSize.Height, _scaleRow.DesiredSize.Height);
            var returnWidth = Math.Min(_returnButton.DesiredSize.Width, width);
            ArrangeNode(_scaleRow, left, footerTop, width, _scaleRow.DesiredSize.Height);
            ArrangeNode(_returnButton, left + width - returnWidth, footerTop, returnWidth, _returnButton.DesiredSize.Height);
            var valueTop = footerTop - Gap - _scaleValue.DesiredSize.Height;
            ArrangeNode(_scaleValue, left, valueTop, width, _scaleValue.DesiredSize.Height);
            middleBottom = valueTop - Gap;
        }
        if (_showWarning)
        {
            middleBottom -= _warning.DesiredSize.Height;
            ArrangeNode(_warning, left, middleBottom, width, _warning.DesiredSize.Height);
            middleBottom -= Gap;
        }
        else
            _warning.Arrange(Rect.Zero);
        if (!_isCompact)
        {
            middleBottom -= _feedback.DesiredSize.Height;
            ArrangeNode(_feedback, left, middleBottom, width, _feedback.DesiredSize.Height);
            middleBottom -= Gap;
        }
        ArrangeNode(_pageTabs, left, middleTop, width, Math.Max(0, middleBottom - middleTop));
    }

    private static void ArrangeNode(UiNode node, double x, double y, double width, double height) =>
        node.Arrange(new Rect(x, y, Math.Max(0, width), Math.Max(0, height)));

    private static Text CreateClassedText(string text, string cssClass)
    {
        var node = new Text { Content = text, Wrapping = TextWrapping.Wrap };
        node.Classes.Add(cssClass);
        return node;
    }

    private static Button CreateScaleButton(string text, string id) => new() { Text = text, MinHeight = 32, StyleId = id };
}
