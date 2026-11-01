using System.Globalization;
using PanguEngine.Graphics.Text;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Drawing;

namespace PanguEngine.Client.UI.Scenes.Showcase;

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
    private readonly NumericUpDown<double> _scaleEditor;
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
        _scaleEditor = new NumericUpDown<double>
        {
            StyleId = "showcase-scale-editor",
            Minimum = 0.5,
            Maximum = 3,
            Increment = 0.05,
            Value = 1,
            FormatString = "0.##",
            FormatProvider = CultureInfo.InvariantCulture,
            Width = 120,
            MinHeight = 32
        };
        _restoreScale = CreateScaleButton("恢复", "showcase-scale-restore");
        _scaleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gap };
        _scaleRow.Children.Add(new Text { Content = "缩放", VerticalAlignment = VerticalAlignment.Center });
        _scaleRow.Children.Add(_scaleEditor);
        _scaleRow.Children.Add(_restoreScale);
        _returnButton = new Button { Content = "返回暂停菜单", MinHeight = 36, StyleId = "showcase-return" };
        Children.Add(_header);
        Children.Add(_pageTabs);
        Children.Add(_feedback);
        Children.Add(_warning);
        Children.Add(_scaleRow);
        Children.Add(_returnButton);
    }

    internal TabView PageTabs => _pageTabs;
    internal NumericUpDown<double> ScaleEditor => _scaleEditor;
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
        foreach (var node in new UIElement[] { _header, _feedback, _warning, _scaleRow, _returnButton })
            node.Measure(new Size(width, double.PositiveInfinity));
        var footerHeight = _isCompact
            ? _returnButton.DesiredSize.Height + Gap + _scaleRow.DesiredSize.Height
            : Math.Max(_returnButton.DesiredSize.Height, _scaleRow.DesiredSize.Height);
        var informationHeight = _showWarning ? _warning.DesiredSize.Height + Gap : 0;
        if (!_isCompact)
            informationHeight += _feedback.DesiredSize.Height + Gap;
        var reservedHeight = _header.DesiredSize.Height + footerHeight + informationHeight + Gap * 2;
        var pageHeight = double.IsPositiveInfinity(availableSize.Height)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Height - reservedHeight);
        _pageTabs.Measure(new Size(width, pageHeight));
        return new Size(
            double.IsFinite(width)
                ? width
                : Math.Max(_header.DesiredSize.Width,
                    Math.Max(_pageTabs.DesiredSize.Width, _scaleRow.DesiredSize.Width)),
            double.IsFinite(availableSize.Height)
                ? availableSize.Height
                : _pageTabs.DesiredSize.Height + reservedHeight);
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
            middleBottom = scaleTop - Gap;
        }
        else
        {
            var footerTop = bottom - Math.Max(_returnButton.DesiredSize.Height, _scaleRow.DesiredSize.Height);
            var returnWidth = Math.Min(_returnButton.DesiredSize.Width, width);
            ArrangeNode(_scaleRow, left, footerTop, width, _scaleRow.DesiredSize.Height);
            ArrangeNode(_returnButton, left + width - returnWidth, footerTop, returnWidth,
                _returnButton.DesiredSize.Height);
            middleBottom = footerTop - Gap;
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

    private static void ArrangeNode(UIElement node, double x, double y, double width, double height) =>
        node.Arrange(new Rect(x, y, Math.Max(0, width), Math.Max(0, height)));

    private static Text CreateClassedText(string text, string cssClass)
    {
        var node = new Text { Content = text, Wrapping = TextWrapping.Wrap };
        node.Classes.Add(cssClass);
        return node;
    }

    private static Button CreateScaleButton(string text, string id) =>
        new() { Content = text, MinHeight = 32, StyleId = id };
}