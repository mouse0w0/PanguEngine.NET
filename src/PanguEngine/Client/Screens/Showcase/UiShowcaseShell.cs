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

    private const double Gap = 10;

    private static readonly string[] CategoryLabels = ["基础控件", "样式", "绑定", "布局"];
    private static readonly string[] CompactCategoryLabels = ["控件", "样式", "绑定", "布局"];

    private readonly Text _heading;
    private readonly Text _escapeHint;
    private readonly StackPanel _header;
    private readonly TabView _categoryTabs;
    private Text[] _categoryHeaders = [];
    private readonly List<TabView> _exampleTabs = [];
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

        _categoryTabs = new TabView
        {
            TabStripPlacement = TabStripPlacement.Left,
            StyleId = "showcase-category-tabs",
            CanReorderTabs = false
        };

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

        Children.Add(_header);
        Children.Add(_categoryTabs);
        Children.Add(_feedback);
        Children.Add(_warning);
        Children.Add(_scaleRow);
        Children.Add(_scaleValue);
        Children.Add(_returnButton);
    }

    internal TabView CategoryTabs => _categoryTabs;

    internal IReadOnlyList<Button> ScaleButtons => new[] { _scaleDown, _scaleReset, _scaleUp, _restoreScale };

    internal Button ScaleDownButton => _scaleDown;

    internal Button ScaleResetButton => _scaleReset;

    internal Button ScaleUpButton => _scaleUp;

    internal Button RestoreScaleButton => _restoreScale;

    internal Button ReturnButton => _returnButton;

    internal IReadOnlyList<TabView> ExampleTabs => _exampleTabs;

    internal bool IsCompact => _isCompact;

    internal bool ShowWarning => _showWarning;

    internal void SetExamples(IReadOnlyList<UiShowcaseExample[]> categories)
    {
        _categoryTabs.Items.Clear();
        _exampleTabs.Clear();
        _categoryHeaders = new Text[categories.Count];

        for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            var categoryHeader = CreateTabHeader(CategoryLabels[categoryIndex], "showcase-nav-header");
            var exampleTabs = new TabView
            {
                TabStripPlacement = TabStripPlacement.Top,
                StyleId = "showcase-example-tabs",
                CanReorderTabs = false
            };
            foreach (var example in categories[categoryIndex])
            {
                var exampleHeader = CreateTabHeader(example.Title, "showcase-example-tab-header");
                var exampleItem = new TabItem
                {
                    Header = exampleHeader,
                    Content = CreateExamplePage(example)
                };
                exampleItem.Classes.Add("showcase-example-switch");
                exampleTabs.Items.Add(exampleItem);
            }

            var categoryItem = new TabItem
            {
                Header = categoryHeader,
                Content = exampleTabs
            };
            categoryItem.Classes.Add("showcase-nav");
            _categoryHeaders[categoryIndex] = categoryHeader;
            _exampleTabs.Add(exampleTabs);
            _categoryTabs.Items.Add(categoryItem);
        }
    }

    internal void SetFeedback(string message) => _feedback.Content = message;

    internal void SetCategoryLabels(bool compact)
    {
        for (var index = 0; index < _categoryHeaders.Length; index++)
            _categoryHeaders[index].Content = compact ? CompactCategoryLabels[index] : CategoryLabels[index];
    }

    internal void SetScaleValue(string text) => _scaleValue.Content = text;

    internal void ApplyViewportMode(Size viewport)
    {
        var compact = viewport.Width < CompactWidthThreshold || viewport.Height < CompactHeightThreshold;
        var warning = viewport.Width < FullWidthThreshold || viewport.Height < FullHeightThreshold;
        if (compact != _isCompact)
        {
            _isCompact = compact;
            SetCategoryLabels(compact);

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
        _feedback.Measure(new Size(width, double.PositiveInfinity));
        _warning.Measure(new Size(width, double.PositiveInfinity));
        _scaleRow.Measure(new Size(width, double.PositiveInfinity));
        _scaleValue.Measure(new Size(width, double.PositiveInfinity));
        _returnButton.Measure(new Size(_isCompact ? width : double.PositiveInfinity, double.PositiveInfinity));

        var footerHeight = _isCompact
            ? _returnButton.DesiredSize.Height + _scaleRow.DesiredSize.Height
            : Math.Max(_returnButton.DesiredSize.Height, _scaleRow.DesiredSize.Height);
        var reservedHeight = _header.DesiredSize.Height + footerHeight +
            _scaleValue.DesiredSize.Height + (_showWarning ? _warning.DesiredSize.Height : 0) +
            _feedback.DesiredSize.Height + Gap * (_isCompact ? 7 : 5);
        var categoryHeight = double.IsPositiveInfinity(availableSize.Height)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Height - reservedHeight);
        _categoryTabs.Measure(new Size(width, categoryHeight));

        if (double.IsFinite(width) && double.IsFinite(availableSize.Height))
            return availableSize;

        var fallbackWidth = double.IsFinite(width)
            ? width
            : Math.Max(
                _header.DesiredSize.Width,
                Math.Max(_categoryTabs.DesiredSize.Width, _scaleRow.DesiredSize.Width));
        return new Size(
            fallbackWidth,
            _header.DesiredSize.Height +
             _categoryTabs.DesiredSize.Height +
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
        ArrangeNode(_categoryTabs, left, middleTop, width, middleHeight);
    }

    private static void ArrangeNode(UiNode node, double x, double y, double width, double height) =>
        node.Arrange(new Rect(x, y, Math.Max(0, width), Math.Max(0, height)));

    private static Text CreateClassedText(string text, string cssClass)
    {
        var node = new Text { Content = text, Wrapping = TextWrapping.Wrap };
        node.Classes.Add(cssClass);
        return node;
    }

    private static Text CreateTabHeader(string text, string cssClass)
    {
        var header = new Text { Content = text, Wrapping = TextWrapping.NoWrap };
        header.Classes.Add(cssClass);
        return header;
    }

    private static StackPanel CreateExamplePage(UiShowcaseExample example)
    {
        var page = UiShowcaseWidgets.Column(
            UiShowcaseWidgets.Label(example.Title, "showcase-example-title"),
            UiShowcaseWidgets.Label(example.Instructions, "showcase-example-instructions"),
            example.Content);
        page.Classes.Add("showcase-example-page");
        page.Padding = new Thickness(12);
        return page;
    }

    private static Button CreateScaleButton(string text, string id) =>
        new()
        {
            Text = text,
            MinHeight = 32,
            StyleId = id
        };
}
