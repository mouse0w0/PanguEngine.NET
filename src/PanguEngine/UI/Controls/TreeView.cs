using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

/// <summary>Displays a virtualized hierarchy with synchronously accessed live child collections.</summary>
/// <typeparam name="T">The reference-identified business node type.</typeparam>
/// <remarks>Nodes must form an acyclic tree and each instance may occur only once in this view.</remarks>
public sealed class TreeView<T> : Control where T : class
{
    /// <summary>Identifies the live root collection.</summary>
    public static readonly DirectProperty<TreeView<T>, IReadOnlyObservableList<T>?> ItemsSourceProperty =
        Property.RegisterDirect<TreeView<T>, IReadOnlyObservableList<T>?>(nameof(ItemsSource),
            static view => view.ItemsSource, static (view, value) => view.ItemsSource = value,
            onChanged: static (view, _, _) => view.ChangeSource());
    /// <summary>Identifies the optional business content template.</summary>
    public static readonly DirectProperty<TreeView<T>, IDataTemplate?> ItemTemplateProperty =
        Property.RegisterDirect<TreeView<T>, IDataTemplate?>(nameof(ItemTemplate),
            static view => view.ItemTemplate, static (view, value) => view.ItemTemplate = value,
            onChanged: static (view, _, _) => view.ResetTemplate());
    /// <summary>Identifies the view's selection mode.</summary>
    public static readonly DirectProperty<TreeView<T>, SelectionMode> SelectionModeProperty =
        Property.RegisterDirect<TreeView<T>, SelectionMode>(nameof(SelectionMode),
            static view => view.SelectionMode, static (view, value) => view.SelectionMode = value,
            unsetValue: SelectionMode.Single);
    /// <summary>Identifies the fixed row height.</summary>
    public static readonly Property<double> ItemExtentProperty =
        Property.Register<TreeView<T>, double>(nameof(ItemExtent), 32,
            onChanged: static (view, _, _) => view.InvalidateMeasure(),
            validate: static value => double.IsFinite(value) && value > 0,
            validationMessage: "ItemExtent must be finite and positive.");
    /// <summary>Identifies the indentation per level.</summary>
    public static readonly Property<double> IndentProperty =
        Property.Register<TreeView<T>, double>(nameof(Indent), 16,
            onChanged: static (view, _, _) => view.InvalidateMeasure(),
            validate: static value => double.IsFinite(value) && value >= 0,
            validationMessage: "Indent must be finite and non-negative.");
    /// <summary>Identifies the space between rows.</summary>
    public static readonly Property<double> SpacingProperty =
        Property.Register<TreeView<T>, double>(nameof(Spacing),
            onChanged: static (view, _, _) => view.InvalidateMeasure(),
            validate: static value => double.IsFinite(value) && value >= 0,
            validationMessage: "Spacing must be finite and non-negative.");

    private static readonly DefaultTreeTemplate DefaultTemplate = new();
    private readonly Dictionary<T, NodeState> _nodes = new(ReferenceEqualityComparer.Instance);
    private readonly List<NodeState> _visible = [];
    private IReadOnlyObservableList<T>? _source;
    private IDataTemplate? _template;
    private SourceSubscription? _rootSubscription;
    private NodeState? _current;
    private NodeState? _anchor;
    private NodeState? _pendingScroll;
    private readonly TreeItemsHost _host;
    private readonly ScrollViewport _viewport;
    private readonly ScrollBar _horizontalBar;
    private readonly ScrollBar _verticalBar;
    private readonly Dictionary<NodeState, TreeItemContainer> _realized = [];
    private readonly Stack<TreeItemContainer> _rowPool = [];
    private readonly Stack<UIElement> _contentPool = [];
    private WidthConfiguration? _widthConfiguration;
    private Size? _arrangedSize;
    private ScrollSolution _scroll;
    private Point _offset;
    private double _estimatedWidth;
    private double _rowExtent;
    private double _spacing;
    private double _indent;
    private bool _synchronizingBars;
    private int _poolCapacity;

    static TreeView()
    {
        CssRegistry.RegisterElement<TreeView<T>>("TreeView");
        CssRegistry.RegisterProperty<TreeView<T>, double>("item-extent", ItemExtentProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<TreeView<T>, double>("indent", IndentProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<TreeView<T>, double>("spacing", SpacingProperty, CssValueConverters.ParseLength);
    }

    /// <summary>Creates a tree with child hints and a synchronous live child collection accessor.</summary>
    /// <param name="mayHaveChildren">Provides a child hint without obtaining the child collection.</param>
    /// <param name="childrenSelector">Returns the original live child collection on first expansion.</param>
    public TreeView(Func<T, bool> mayHaveChildren, Func<T, IReadOnlyObservableList<T>> childrenSelector)
    {
        MayHaveChildren = mayHaveChildren;
        ChildrenSelector = childrenSelector;
        Selection = new TreeSelectionModel<T>(_nodes);
        Selection.SelectedItems.Changed += (_, _) => RefreshRowStates();
        Selection.Subscribe(TreeSelectionModel<T>.SelectionModeProperty, (_, args) =>
            RaisePropertyChanged(SelectionModeProperty, args.OldValue, args.NewValue));
        _host = new TreeItemsHost(this);
        _viewport = new ScrollViewport();
        _viewport.SetContent(_host);
        _horizontalBar = CreateBar(Orientation.Horizontal);
        _verticalBar = CreateBar(Orientation.Vertical);
        Children.Add(_viewport);
        Children.Add(_horizontalBar);
        Children.Add(_verticalBar);
        Focusable = true;
    }

    /// <summary>Gets the child hint accessor for nodes whose child collection has not been obtained.</summary>
    public Func<T, bool> MayHaveChildren { get; }
    /// <summary>Gets the synchronous accessor whose original child collections are retained.</summary>
    public Func<T, IReadOnlyObservableList<T>> ChildrenSelector { get; }
    /// <summary>Gets or sets the original observable root collection.</summary>
    public IReadOnlyObservableList<T>? ItemsSource
    {
        get => _source;
        set => SetField(ItemsSourceProperty, ref _source, value);
    }
    /// <summary>Gets or sets the business content template, optionally supporting recycling.</summary>
    public IDataTemplate? ItemTemplate
    {
        get => _template;
        set => SetField(ItemTemplateProperty, ref _template, value);
    }
    /// <summary>Gets the stable view-owned selection model.</summary>
    public TreeSelectionModel<T> Selection { get; }
    /// <summary>Gets or sets the permitted selection cardinality.</summary>
    public SelectionMode SelectionMode
    {
        get => Selection.SelectionMode;
        set => Selection.SelectionMode = value;
    }
    /// <summary>Gets or sets the fixed row height in logical pixels.</summary>
    public double ItemExtent { get => GetValue(ItemExtentProperty); set => SetValue(ItemExtentProperty, value); }
    /// <summary>Gets or sets the indentation per hierarchy level.</summary>
    public double Indent { get => GetValue(IndentProperty); set => SetValue(IndentProperty, value); }
    /// <summary>Gets or sets the spacing between adjacent rows.</summary>
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    /// <summary>Occurs after a node's expansion changes.</summary>
    public event EventHandler<TreeNodeExpansionChangedEventArgs<T>>? ExpansionChanged;

    /// <summary>Determines whether a known node is expanded.</summary>
    public bool IsExpanded(T node) => _nodes.TryGetValue(node, out var state) && state.Expanded;

    /// <summary>Expands a known node, synchronously obtaining its original child collection on first use.</summary>
    public void Expand(T node)
    {
        VerifyMutationAccess();
        if (!_nodes.TryGetValue(node, out var state) || state.Expanded || !CanExpand(state))
            return;
        if (state.Children is null)
        {
            var children = ChildrenSelector(node);
            state.Children = children;
            AddNodes(children, state);
            state.Subscription = new SourceSubscription(this, children, state);
            state.Subscription.Attach();
        }
        if (state.Children.Count == 0)
        {
            RefreshRowStates();
            InvalidatePresentation();
            return;
        }
        state.Expanded = true;
        RebuildProjection();
        NotifyExpansion(state);
    }

    /// <summary>Collapses a node while retaining its child collection and hidden selections.</summary>
    public void Collapse(T node)
    {
        VerifyMutationAccess();
        if (!_nodes.TryGetValue(node, out var state) || !state.Expanded)
            return;
        state.Expanded = false;
        RebuildProjection();
        NotifyExpansion(state);
    }

    /// <summary>Toggles a known node's expansion.</summary>
    public void ToggleExpanded(T node)
    {
        if (IsExpanded(node))
            Collapse(node);
        else
            Expand(node);
    }

    /// <summary>Reevaluates a known node's child hint without obtaining or replacing its child collection.</summary>
    public void RefreshNode(T node)
    {
        VerifyMutationAccess();
        if (!_nodes.TryGetValue(node, out var state) || state.Children is not null)
            return;
        state.ChildHint = MayHaveChildren(node);
        RefreshRowStates();
        InvalidatePresentation();
    }

    /// <summary>Requests minimal scrolling for a currently visible node without selecting or expanding it.</summary>
    /// <remarks>The request is processed by normal layout; recycling focused content may clear focus.</remarks>
    public void ScrollIntoView(T node)
    {
        VerifyMutationAccess();
        if (!_nodes.TryGetValue(node, out var state) || state.VisibleIndex < 0)
            return;
        _pendingScroll = state;
        InvalidatePresentation();
    }

    private void NotifyExpansion(NodeState state)
    {
        RefreshRowStates();
        ExpansionChanged?.Invoke(this, new(state.Item, state.Expanded));
    }

    private bool CanExpand(NodeState state) => state.Children is { } children
        ? children.Count > 0 : (state.ChildHint ??= MayHaveChildren(state.Item));

    private void ChangeSource()
    {
        _rootSubscription?.Detach();
        foreach (var state in _nodes.Values)
            state.Subscription?.Detach();
        _nodes.Clear();
        _visible.Clear();
        _current = null;
        _anchor = null;
        _pendingScroll = null;
        ResetRows();
        ResetWidthEstimate();
        _offset = Point.Zero;
        if (_source is not null)
        {
            AddNodes(_source, null);
            _rootSubscription = new SourceSubscription(this, _source, null);
            _rootSubscription.Attach();
        }
        else
            _rootSubscription = null;
        RebuildProjection();
        Selection.Clear();
    }

    private void AddNodes(IReadOnlyList<T> items, NodeState? parent)
    {
        foreach (var item in items)
            _nodes.Add(item, new NodeState(item, parent));
    }

    private void RemoveNode(NodeState removed)
    {
        var stack = new Stack<NodeState>();
        stack.Push(removed);
        while (stack.TryPop(out var state))
        {
            if (state.Children is { } children)
                foreach (var child in children)
                    stack.Push(_nodes[child]);
            state.Subscription?.Detach();
            _nodes.Remove(state.Item);
        }
    }

    private void SourceChanged(SourceSubscription subscription, ListChangedEventArgs<T> change)
    {
        VerifyMutationAccess();
        T[]? previous = null;
        if (change.Kind != ListChangeKind.Reorder)
        {
            foreach (var old in change.OldItems)
                RemoveNode(_nodes[old]);
            previous = Selection.Prune();
            AddNodes(change.NewItems, subscription.Parent);
        }
        var parent = subscription.Parent;
        var collapsed = parent is { Expanded: true, Children.Count: 0 };
        if (collapsed)
            parent!.Expanded = false;
        RebuildProjection();
        RefreshRowStates();
        Selection.Publish(previous);
        if (collapsed)
            NotifyExpansion(parent!);
    }

    private void RebuildProjection()
    {
        var oldIndex = _current?.VisibleIndex ?? -1;
        foreach (var old in _visible)
            old.VisibleIndex = -1;
        _visible.Clear();
        var stack = new Stack<NodeState>();
        if (_source is not null)
            for (var index = _source.Count - 1; index >= 0; index--)
                stack.Push(_nodes[_source[index]]);
        while (stack.TryPop(out var state))
        {
            state.VisibleIndex = _visible.Count;
            _visible.Add(state);
            if (state.Expanded && state.Children is { } children)
                for (var index = children.Count - 1; index >= 0; index--)
                    stack.Push(_nodes[children[index]]);
        }
        if (_current is not null && !_visible.Contains(_current))
        {
            var parent = _current.Parent;
            while (parent is not null && !_visible.Contains(parent))
                parent = parent.Parent;
            _current = parent ?? (_visible.Count == 0 ? null : _visible[Math.Clamp(oldIndex, 0, _visible.Count - 1)]);
        }
        if (_anchor is not null && !_visible.Contains(_anchor))
        {
            if (!_nodes.TryGetValue(_anchor.Item, out var known) || !ReferenceEquals(known, _anchor))
                _anchor = null;
            else
            {
                do { _anchor = _anchor.Parent; }
                while (_anchor is not null && !_visible.Contains(_anchor));
            }
        }
        if (_pendingScroll is not null && !_visible.Contains(_pendingScroll))
            _pendingScroll = null;
        ReleaseInvisibleRows();
        RefreshRowStates();
        InvalidatePresentation();
    }

    private static double BringAxis(double start, double length, double offset, double viewport)
    {
        if (length > viewport || start < offset)
            return start;
        return start + length > offset + viewport ? start + length - viewport : offset;
    }

    private void OnRowClicked(TreeItemContainer row, PointerButtonEventArgs args)
    {
        if (args.Button != MouseButton.Left ||
            (args.Modifiers & ~(KeyModifiers.Control | KeyModifiers.Shift)) != 0)
            return;
        var target = _nodes[(T)row.Data!];
        var previous = _current;
        _current = target;
        ApplySelection(target, previous, (args.Modifiers & KeyModifiers.Control) != 0,
            (args.Modifiers & KeyModifiers.Shift) != 0);
        RefreshRowStates();
        args.Handled = true;
    }

    private void ApplySelection(NodeState target, NodeState? previous, bool control, bool shift)
    {
        if (shift && SelectionMode == SelectionMode.Multiple)
        {
            _anchor ??= previous ?? target;
            var first = Math.Min(_anchor.VisibleIndex, target.VisibleIndex);
            var last = Math.Max(_anchor.VisibleIndex, target.VisibleIndex);
            var candidates = new List<T>(last - first + 1);
            for (var index = first; index <= last; index++)
                candidates.Add(_visible[index].Item);
            Selection.Choose(candidates, control);
        }
        else
        {
            _anchor = target;
            if (control && !shift)
                Selection.Toggle(target.Item);
            else
                Selection.SelectOnly(target.Item);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Handled || !IsTreeNavigationTarget(eventArgs.Source) || !IsTreeNavigationTarget(Scene?.FocusedNode))
            return;
        if (eventArgs.Key == Key.A && eventArgs.Modifiers == KeyModifiers.Control)
        {
            if (SelectionMode == SelectionMode.Multiple)
            {
                Selection.Choose(_visible.Select(state => state.Item).ToArray(), true);
                eventArgs.Handled = true;
            }
            return;
        }
        if (eventArgs.Modifiers == KeyModifiers.None && eventArgs.Key == Key.Space)
        {
            if (_current is null)
                return;
            if (!eventArgs.IsRepeat)
            {
                _anchor = _current;
                Selection.Toggle(_current.Item);
            }
            eventArgs.Handled = true;
            return;
        }
        if (eventArgs.Modifiers == KeyModifiers.None && eventArgs.Key is Key.Left or Key.Right)
        {
            if (_current is null)
                return;
            if (eventArgs.Key == Key.Right)
            {
                if (!_current.Expanded)
                    Expand(_current.Item);
                else if (_current.Children is { Count: > 0 })
                    Navigate(_nodes[_current.Children[0]], false, false);
            }
            else if (_current.Expanded)
                Collapse(_current.Item);
            else if (_current.Parent is { } parent)
                Navigate(parent, false, false);
            eventArgs.Handled = true;
            return;
        }
        if ((eventArgs.Modifiers & ~(KeyModifiers.Control | KeyModifiers.Shift)) != 0 || _visible.Count == 0)
            return;
        var index = _current?.VisibleIndex ?? -1;
        var page = (int)Math.Max(1, Math.Min(_visible.Count,
            Math.Floor(_scroll.Viewport.Height / (_rowExtent + _spacing))));
        var target = eventArgs.Key switch
        {
            Key.Home => 0,
            Key.End => _visible.Count - 1,
            Key.Up => index < 0 ? 0 : Math.Max(0, index - 1),
            Key.Down => index < 0 ? 0 : Math.Min(_visible.Count - 1, index + 1),
            Key.PageUp => index < 0 ? 0 : Math.Max(0, index - page),
            Key.PageDown => index < 0 ? 0 : (int)Math.Min(_visible.Count - 1, (long)index + page),
            _ => -1
        };
        if (target < 0)
            return;
        Navigate(_visible[target], (eventArgs.Modifiers & KeyModifiers.Control) != 0,
            (eventArgs.Modifiers & KeyModifiers.Shift) != 0);
        eventArgs.Handled = true;
    }

    private bool IsTreeNavigationTarget(UIElement? node) => ReferenceEquals(node, this) ||
        node is TreeItemContainer && ReferenceEquals(node.Parent, _host);

    private void Navigate(NodeState target, bool control, bool shift)
    {
        var previous = _current;
        _current = target;
        if (!control || shift)
            ApplySelection(target, previous, control && SelectionMode == SelectionMode.Multiple, shift);
        RefreshRowStates();
        ScrollIntoView(target.Item);
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(PointerWheelEventArgs eventArgs)
    {
        base.OnPointerWheel(eventArgs);
        var lines = UIToolkit.WheelScrollLines;
        if (eventArgs.Handled || lines == 0)
            return;
        var deltaX = eventArgs.DeltaX;
        var deltaY = eventArgs.DeltaY;
        if ((eventArgs.Modifiers & KeyModifiers.Shift) != 0)
        {
            deltaX += deltaY;
            deltaY = 0;
        }
        var previous = _offset;
        SetOffset(new Point(
            previous.X - deltaX * UIToolkit.GetWheelScrollStep(lines, _horizontalBar.SmallChange, _scroll.Viewport.Width),
            previous.Y - deltaY * UIToolkit.GetWheelScrollStep(lines, _verticalBar.SmallChange, _scroll.Viewport.Height)), _scroll);
        if (_offset != previous)
            eventArgs.Handled = true;
    }

    private ScrollBar CreateBar(Orientation orientation)
    {
        var bar = new ScrollBar { Orientation = orientation, Focusable = false, IsWheelScrollingEnabled = false };
        bar.PropertyChanged += OnBarChanged;
        return bar;
    }

    private void OnBarChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_synchronizingBars || !ReferenceEquals(args.Property, ScrollBar.ValueProperty))
            return;
        SetOffset(ReferenceEquals(sender, _horizontalBar)
            ? new Point(_horizontalBar.Value, _offset.Y)
            : new Point(_offset.X, _verticalBar.Value), _scroll);
    }

    private void InvalidatePresentation()
    {
        _host.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void ResetWidthEstimate()
    {
        _estimatedWidth = 0;
        InvalidatePresentation();
    }

    private void ResetTemplate()
    {
        ResetRows();
        ResetWidthEstimate();
    }

    private void UpdateMetrics()
    {
        var configuration = new WidthConfiguration(FontFamily, FontWeight, FontStyle, FontSize,
            ItemExtent, Indent, Spacing, Scene?.Scale ?? 1, Scene?.UseLayoutRounding ?? true);
        if (_widthConfiguration == configuration)
            return;
        _widthConfiguration = configuration;
        _estimatedWidth = 0;
        _rowExtent = ResolveLength(ItemExtent);
        _spacing = ResolveLength(Spacing);
        _indent = ResolveLength(Indent);
        _host.InvalidateMeasure();
    }

    private double ResolveLength(double value) => Scene?.UseLayoutRounding ?? true
        ? LayoutHelper.RoundLayoutValueUp(value, Scene?.Scale ?? 1) : value;

    private double ContentHeight
    {
        get
        {
            var height = _visible.Count * _rowExtent + Math.Max(0, _visible.Count - 1) * _spacing;
            if (!double.IsFinite(height) || !double.IsFinite(_rowExtent + _spacing))
                throw new InvalidOperationException("The tree cannot represent a finite row extent.");
            return height;
        }
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        if (!double.IsFinite(availableSize.Width) || !double.IsFinite(availableSize.Height))
            throw new InvalidOperationException("Virtualized trees require a finite viewport on both axes.");
        UpdateMetrics();
        var desired = SolveScroll(availableSize);
        if (_arrangedSize is { } actual)
            MeasureWindow(SolveScroll(actual));
        else
            MeasureBars(desired);
        return new Size(availableSize.Width,
            Math.Min(availableSize.Height, ContentHeight + (desired.Horizontal ? desired.HorizontalThickness : 0)));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        _arrangedSize = contentBounds.Size;
        var solution = SolveScroll(contentBounds.Size);
        _scroll = solution;
        var requested = _offset;
        if (_pendingScroll is { VisibleIndex: >= 0 } target)
            requested = new Point(requested.X, BringAxis(target.VisibleIndex * (_rowExtent + _spacing),
                _rowExtent, requested.Y, solution.Viewport.Height));
        SetOffset(requested, solution);
        MeasureWindow(solution);
        if (!IsStyleSubtreeValid || !_viewport.IsMeasureValid || !_horizontalBar.IsMeasureValid || !_verticalBar.IsMeasureValid)
            return;
        if (_estimatedWidth != solution.Extent.Width)
        {
            InvalidateMeasure();
            return;
        }
        if (_pendingScroll is { } pending && _realized.TryGetValue(pending, out var row))
        {
            var start = Math.Max(0, row.DisclosureStart);
            SetOffset(new Point(BringAxis(start, row.ContentStart - start + 1, _offset.X, solution.Viewport.Width),
                _offset.Y), solution);
            _pendingScroll = null;
        }
        SynchronizeBars(solution);
        _viewport.Arrange(new Rect(contentBounds.X, contentBounds.Y, solution.Viewport));
        _horizontalBar.Arrange(solution.Horizontal
            ? new Rect(contentBounds.X, contentBounds.Y + solution.Viewport.Height, solution.Viewport.Width, solution.HorizontalThickness)
            : Rect.Zero);
        _verticalBar.Arrange(solution.Vertical
            ? new Rect(contentBounds.X + solution.Viewport.Width, contentBounds.Y, solution.VerticalThickness, solution.Viewport.Height)
            : Rect.Zero);
    }

    private ScrollSolution SolveScroll(Size available)
    {
        var horizontalThickness = ResolveLength(_horizontalBar.BarThickness);
        var verticalThickness = ResolveLength(_verticalBar.BarThickness);
        var horizontal = false;
        var vertical = false;
        var extent = new Size(_estimatedWidth, ContentHeight);
        var viewport = available;
        for (var pass = 0; pass < 3; pass++)
        {
            viewport = new Size(Math.Max(0, available.Width - (vertical ? verticalThickness : 0)),
                Math.Max(0, available.Height - (horizontal ? horizontalThickness : 0)));
            var nextHorizontal = horizontal || extent.Width > viewport.Width;
            var nextVertical = vertical || extent.Height > viewport.Height;
            if (nextHorizontal == horizontal && nextVertical == vertical)
                break;
            horizontal = nextHorizontal;
            vertical = nextVertical;
        }
        return new ScrollSolution(viewport, extent, horizontal, vertical,
            available.Height - viewport.Height, available.Width - viewport.Width);
    }

    private void MeasureWindow(ScrollSolution solution)
    {
        UpdateWindow(solution.Viewport);
        _viewport.ContentMeasureConstraint = new Size(Math.Max(solution.Viewport.Width, solution.Extent.Width),
            Math.Max(solution.Viewport.Height, solution.Extent.Height));
        _viewport.Measure(solution.Viewport);
        MeasureBars(solution);
    }

    private void MeasureBars(ScrollSolution solution)
    {
        ConfigureBar(_horizontalBar, solution.Horizontal);
        ConfigureBar(_verticalBar, solution.Vertical);
        _horizontalBar.Measure(solution.Horizontal ? new Size(solution.Viewport.Width, solution.HorizontalThickness) : Size.Zero);
        _verticalBar.Measure(solution.Vertical ? new Size(solution.VerticalThickness, solution.Viewport.Height) : Size.Zero);
    }

    private static void ConfigureBar(ScrollBar bar, bool visible)
    {
        bar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        bar.IsHitTestVisible = visible;
    }

    private void SynchronizeBars(ScrollSolution solution)
    {
        _synchronizingBars = true;
        try
        {
            _horizontalBar.LargeChange = solution.Viewport.Width;
            _verticalBar.LargeChange = solution.Viewport.Height;
            _horizontalBar.SynchronizeRange(0, Math.Max(0, solution.Extent.Width - solution.Viewport.Width),
                solution.Viewport.Width, _offset.X);
            _verticalBar.SynchronizeRange(0, Math.Max(0, solution.Extent.Height - solution.Viewport.Height),
                solution.Viewport.Height, _offset.Y);
        }
        finally { _synchronizingBars = false; }
    }

    private void SetOffset(Point requested, ScrollSolution solution)
    {
        var next = new Point(Math.Clamp(requested.X, 0, Math.Max(0, solution.Extent.Width - solution.Viewport.Width)),
            Math.Clamp(requested.Y, 0, Math.Max(0, solution.Extent.Height - solution.Viewport.Height)));
        if (_offset == next && _viewport.Offset == next)
            return;
        _offset = next;
        _viewport.Offset = next;
        _viewport.InvalidateArrange();
        InvalidateArrange();
    }

    private (int Start, int End) Window(Size viewport, int pages)
    {
        if (_visible.Count == 0 || viewport.Width <= 0 || viewport.Height <= 0)
            return (0, 0);
        var step = _rowExtent + _spacing;
        var startPosition = Math.Max(0, _offset.Y - viewport.Height * pages);
        var endPosition = _offset.Y + viewport.Height * (pages + 1);
        var start = (int)Math.Min(_visible.Count, Math.Floor(startPosition / step));
        if (start < _visible.Count && start * step + _rowExtent <= startPosition)
            start++;
        return (start, (int)Math.Min(_visible.Count, Math.Ceiling(endPosition / step)));
    }

    private void UpdateWindow(Size viewport)
    {
        var visible = Window(viewport, 0);
        var retained = Window(viewport, 1);
        _poolCapacity = visible.End - visible.Start;
        TrimPools();
        foreach (var pair in _realized.ToArray())
            if (pair.Key.VisibleIndex < retained.Start || pair.Key.VisibleIndex >= retained.End)
            {
                _realized.Remove(pair.Key);
                ReleaseRow(pair.Value, true);
            }
        for (var index = retained.Start; index < retained.End; index++)
        {
            var state = _visible[index];
            if (_realized.ContainsKey(state))
                continue;
            var row = _rowPool.TryPop(out var pooled) ? pooled : new TreeItemContainer(OnRowClicked,
                item => { Focus(); ToggleExpanded((T)item.Data!); });
            row.Data = state.Item;
            row.Template = _template ?? (state.Item is UIElement ? null : DefaultTemplate);
            row.SetRoot(CreateContent(state.Item, row.Template));
            _realized.Add(state, row);
            _host.Children.Add(row);
        }
        RefreshRowStates();
    }

    private UIElement? CreateContent(T item, IDataTemplate? template)
    {
        if (template is IRecyclingDataTemplate recycling && _contentPool.TryPop(out var root))
            return recycling.Update(item, root);
        return template is null ? (UIElement)(object)item : template.Build(item);
    }

    private void RefreshRowStates()
    {
        foreach (var (state, row) in _realized)
            row.SetStates(Selection.IsSelected(state.Item), ReferenceEquals(_current, state), state.Expanded,
                CanExpand(state));
    }

    private void ReleaseInvisibleRows()
    {
        foreach (var pair in _realized.ToArray())
            if (pair.Key.VisibleIndex < 0)
            {
                _realized.Remove(pair.Key);
                ReleaseRow(pair.Value, true);
            }
    }

    private void ReleaseRow(TreeItemContainer row, bool recycle)
    {
        var root = row.ContentRoot;
        var template = row.Template;
        if (root is not null && template is IRecyclingDataTemplate recycling)
            recycling.Clear(row.Data!, root);
        row.SetRoot(null);
        _host.Children.Remove(row);
        row.Data = null;
        row.Template = null;
        row.SetStates(false, false, false, false);
        if (!recycle)
            return;
        if (root is not null && template is IRecyclingDataTemplate &&
            ReferenceEquals(template, _template ?? DefaultTemplate) && _contentPool.Count < _poolCapacity)
            _contentPool.Push(root);
        if (_rowPool.Count < _poolCapacity)
            _rowPool.Push(row);
    }

    private void ResetRows()
    {
        foreach (var row in _realized.Values)
            ReleaseRow(row, false);
        _realized.Clear();
        _rowPool.Clear();
        _contentPool.Clear();
        _host.InvalidateMeasure();
    }

    private void TrimPools()
    {
        while (_rowPool.Count > _poolCapacity)
            _rowPool.Pop();
        while (_contentPool.Count > _poolCapacity)
            _contentPool.Pop();
    }

    internal sealed class NodeState(T item, NodeState? parent)
    {
        internal T Item { get; } = item;
        internal NodeState? Parent { get; } = parent;
        internal int Depth { get; } = parent is null ? 0 : checked(parent.Depth + 1);
        internal int VisibleIndex { get; set; } = -1;
        internal bool Expanded { get; set; }
        internal bool? ChildHint { get; set; }
        internal IReadOnlyObservableList<T>? Children { get; set; }
        internal SourceSubscription? Subscription { get; set; }
    }

    internal sealed class SourceSubscription(TreeView<T> owner, IReadOnlyObservableList<T> source, NodeState? parent)
    {
        private readonly WeakReference<TreeView<T>> _owner = new(owner);
        internal NodeState? Parent { get; } = parent;
        internal void Attach() => source.Changed += Changed;
        internal void Detach() => source.Changed -= Changed;

        private void Changed(object? sender, ListChangedEventArgs<T> change)
        {
            if (!_owner.TryGetTarget(out var tree))
            {
                Detach();
                return;
            }
            tree.SourceChanged(this, change);
        }
    }

    private readonly record struct WidthConfiguration(string Family, FontWeight Weight, FontStyle Style,
        double FontSize, double ItemExtent, double Indent, double Spacing, double Scale, bool Rounding);

    private readonly record struct ScrollSolution(Size Viewport, Size Extent, bool Horizontal, bool Vertical,
        double HorizontalThickness, double VerticalThickness);

    private sealed class TreeItemsHost(TreeView<T> view) : Container
    {
        protected override Size MeasureCore(Size availableSize)
        {
            foreach (var (state, row) in view._realized)
            {
                row.Height = view._rowExtent;
                row.Indentation = state.Depth * view._indent;
                row.Measure(new Size(double.PositiveInfinity, view._rowExtent));
                var width = row.DesiredSize.Width;
                if (!double.IsFinite(width))
                    throw new InvalidOperationException("The tree row cannot represent a finite natural width.");
                if (width > view._estimatedWidth)
                {
                    view._estimatedWidth = width;
                    view.InvalidateMeasure();
                }
            }
            return new Size(view._estimatedWidth, view.ContentHeight);
        }

        protected override void ArrangeCore(Size finalSize)
        {
            foreach (var (state, row) in view._realized)
                row.Arrange(new Rect(0, state.VisibleIndex * (view._rowExtent + view._spacing),
                    Math.Max(finalSize.Width, view._estimatedWidth), view._rowExtent));
        }
    }

    private sealed class DefaultTreeTemplate : IRecyclingDataTemplate
    {
        public UIElement Build(object data) => new Text { Content = data.ToString() ?? string.Empty, IsHitTestVisible = false };
        public UIElement Update(object data, UIElement root)
        {
            ((Text)root).Content = data.ToString() ?? string.Empty;
            return root;
        }
        public void Clear(object data, UIElement root) => ((Text)root).Content = string.Empty;
    }
}
