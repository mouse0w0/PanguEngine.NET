using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.Input;
using PanguEngine.UI.Input;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

/// <summary>Provides fixed-size virtualization and selection for built-in item views.</summary>
/// <typeparam name="T">The source item type.</typeparam>
public abstract class ItemsView<T> : Control
{
    /// <summary>Identifies the observable source property.</summary>
    public static readonly DirectProperty<ItemsView<T>, IReadOnlyObservableList<T>?> ItemsSourceProperty =
        Property.RegisterDirect<ItemsView<T>, IReadOnlyObservableList<T>?>(nameof(ItemsSource),
            static view => view.ItemsSource, static (view, value) => view.ItemsSource = value,
            onChanged: static (view, _, value) => view.ChangeSource(value));

    /// <summary>Identifies the optional item template property.</summary>
    public static readonly DirectProperty<ItemsView<T>, IDataTemplate?> ItemTemplateProperty =
        Property.RegisterDirect<ItemsView<T>, IDataTemplate?>(nameof(ItemTemplate),
            static view => view.ItemTemplate, static (view, value) => view.ItemTemplate = value,
            onChanged: static (view, _, _) => view.ResetTemplate());

    /// <summary>Identifies the model's selection mode exposed by this view.</summary>
    public static readonly DirectProperty<ItemsView<T>, SelectionMode> SelectionModeProperty =
        Property.RegisterDirect<ItemsView<T>, SelectionMode>(nameof(SelectionMode),
            static view => view.SelectionMode, static (view, value) => view.SelectionMode = value,
            unsetValue: SelectionMode.Single);

    private static readonly DefaultTextTemplate DefaultTemplate = new();

    private readonly Dictionary<int, ItemContainer> _realized = [];
    private readonly Stack<ItemContainer> _containerPool = [];
    private readonly Stack<UIElement> _contentPool = [];
    private readonly ScrollViewport _scrollViewport;
    private readonly ScrollBar _scrollBar;
    private IReadOnlyObservableList<T>? _source;
    private IDataTemplate? _template;
    private SourceSubscription? _sourceSubscription;
    private ScrollLayoutSolution _scrollLayout;
    private Size? _arrangedSize;
    private Point _offset;
    private bool _hasResolvedScrollRange;
    private bool _isSynchronizingBar;
    private int _navigationIndex = -1;
    private int _anchorIndex = -1;
    private int? _pendingScrollIndex;
    private int _poolCapacity;

    private readonly record struct ScrollLayoutSolution(
        Orientation Axis, Size Viewport, Size Extent, bool ShowBar, double BarThickness);

    /// <summary>Creates an empty item view with single selection.</summary>
    private protected ItemsView()
    {
        Selection = new MultiSelectionModel<T> { SelectionMode = SelectionMode.Single };
        Selection.PropertyChanged += OnSelectionPropertyChanged;
        Selection.SelectedIndices.Changed += (_, _) => RefreshStates();
        Host = new VirtualizedItemsHost(this);
        _scrollViewport = new ScrollViewport();
        _scrollViewport.SetContent(Host);
        _scrollBar = new ScrollBar
        {
            Focusable = false,
            IsWheelScrollingEnabled = false,
            SmallChange = 16
        };
        Children.Add(_scrollViewport);
        Children.Add(_scrollBar);
        _scrollBar.PropertyChanged += OnScrollBarPropertyChanged;
        Focusable = true;
    }

    /// <summary>Gets the view-owned selection model; its source must not be changed externally.</summary>
    /// <remarks>Mutate the model on the view's scene owner thread and outside drawing callbacks.</remarks>
    public MultiSelectionModel<T> Selection { get; }
    private VirtualizedItemsHost Host { get; }
    private ItemsViewLayout Layout { get; set; }

    /// <summary>Gets or sets the live source retained without copying.</summary>
    public IReadOnlyObservableList<T>? ItemsSource
    {
        get => _source;
        set => SetField(ItemsSourceProperty, ref _source, value);
    }

    /// <summary>Gets or sets the explicit template, optionally supporting content recycling.</summary>
    public IDataTemplate? ItemTemplate
    {
        get => _template;
        set => SetField(ItemTemplateProperty, ref _template, value);
    }

    /// <summary>Gets or sets the permitted selection cardinality.</summary>
    public SelectionMode SelectionMode
    {
        get => Selection.SelectionMode;
        set => Selection.SelectionMode = value;
    }

    internal abstract ItemsViewLayout CreateLayout(Size viewport, int count);

    private void ChangeSource(IReadOnlyObservableList<T>? value)
    {
        _sourceSubscription?.Detach();
        Selection.Source = value;
        ResetItems();
        _navigationIndex = -1;
        _anchorIndex = -1;
        _pendingScrollIndex = null;
        _sourceSubscription = value is null ? null : new SourceSubscription(this, value);
        _sourceSubscription?.Attach();
        SetOffset(Point.Zero);
        Invalidate();
    }

    private void ResetTemplate()
    {
        ResetItems();
        Invalidate();
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size available)
    {
        if (!double.IsFinite(available.Width) || !double.IsFinite(available.Height))
            throw new InvalidOperationException("Virtualized item views require a finite viewport on both axes.");
        var solution = SolveScroll(available);
        ConfigureAxis(solution.Axis);
        if (_arrangedSize is { } actual)
        {
            var actualSolution = SolveScroll(actual);
            MeasureScrollContent(actualSolution);
            MeasureScrollBar(actualSolution);
        }
        else
        {
            MeasureScrollBar(solution);
        }
        var occupiedWidth = solution.Extent.Width +
            (solution.ShowBar && solution.Axis == Orientation.Vertical ? solution.BarThickness : 0);
        var occupiedHeight = solution.Extent.Height +
            (solution.ShowBar && solution.Axis == Orientation.Horizontal ? solution.BarThickness : 0);
        return new Size(Math.Min(occupiedWidth, available.Width), Math.Min(occupiedHeight, available.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect bounds)
    {
        _arrangedSize = bounds.Size;
        var solution = SolveScroll(bounds.Size);
        ConfigureAxis(solution.Axis);
        _scrollLayout = solution;
        _hasResolvedScrollRange = true;
        SetOffset(ResolveOffset(solution.Viewport, _offset));
        SynchronizeScrollBar(solution);
        MeasureScrollContent(solution);
        MeasureScrollBar(solution);
        if (!IsStyleSubtreeValid || !_scrollViewport.IsMeasureValid || !_scrollBar.IsMeasureValid)
            return;
        _scrollViewport.Arrange(new Rect(bounds.X, bounds.Y, solution.Viewport.Width, solution.Viewport.Height));
        var barBounds = !solution.ShowBar ? Rect.Zero : solution.Axis == Orientation.Vertical
            ? new Rect(bounds.X + solution.Viewport.Width, bounds.Y,
                bounds.Width - solution.Viewport.Width, solution.Viewport.Height)
            : new Rect(bounds.X, bounds.Y + solution.Viewport.Height,
                solution.Viewport.Width, bounds.Height - solution.Viewport.Height);
        _scrollBar.Arrange(barBounds);
    }

    private void MeasureScrollContent(ScrollLayoutSolution solution)
    {
        UpdateViewport(solution.Viewport, _offset);
        _scrollViewport.ContentMeasureConstraint = new Size(
            Math.Max(solution.Viewport.Width, solution.Extent.Width),
            Math.Max(solution.Viewport.Height, solution.Extent.Height));
        _scrollViewport.Measure(solution.Viewport);
    }

    private void MeasureScrollBar(ScrollLayoutSolution solution)
    {
        var size = !solution.ShowBar ? Size.Zero : solution.Axis == Orientation.Vertical
            ? new Size(solution.BarThickness, solution.Viewport.Height)
            : new Size(solution.Viewport.Width, solution.BarThickness);
        _scrollBar.Measure(size);
    }

    internal void LayoutChanged(bool resetOffset)
    {
        if (resetOffset)
        {
            _pendingScrollIndex = null;
            _scrollBar.CancelInteraction();
            _hasResolvedScrollRange = false;
            SetOffset(Point.Zero);
        }
        Invalidate();
    }

    private void ConfigureAxis(Orientation axis) => _scrollBar.Orientation = axis;

    private ScrollLayoutSolution SolveScroll(Size available)
    {
        var thickness = _scrollBar.BarThickness;
        if (Scene?.UseLayoutRounding ?? true)
            thickness = LayoutHelper.RoundLayoutValueUp(thickness, Scene?.Scale ?? 1);
        var layout = CreateLayout(available, _source?.Count ?? 0);
        var vertical = layout.Axis == Orientation.Vertical;
        var overflow = vertical
            ? layout.Extent.Height > available.Height
            : layout.Extent.Width > available.Width;
        var viewport = !overflow ? available : vertical
            ? new Size(Math.Max(0, available.Width - thickness), available.Height)
            : new Size(available.Width, Math.Max(0, available.Height - thickness));
        if (overflow)
            layout = CreateLayout(viewport, _source?.Count ?? 0);
        return new ScrollLayoutSolution(layout.Axis, viewport, layout.Extent, overflow, thickness);
    }

    private void SetOffset(Point requested)
    {
        var vertical = _scrollBar.Orientation == Orientation.Vertical;
        var maximum = !_hasResolvedScrollRange ? double.PositiveInfinity : vertical
            ? Math.Max(0, _scrollLayout.Extent.Height - _scrollLayout.Viewport.Height)
            : Math.Max(0, _scrollLayout.Extent.Width - _scrollLayout.Viewport.Width);
        var position = Math.Clamp(vertical ? requested.Y : requested.X, 0, maximum);
        var next = vertical ? new Point(0, position) : new Point(position, 0);
        if (_offset == next)
            return;
        _offset = next;
        _scrollViewport.Offset = next;
        _scrollViewport.InvalidateArrange();
        _isSynchronizingBar = true;
        try
        {
            _scrollBar.Value = position;
        }
        finally
        {
            _isSynchronizingBar = false;
        }
    }

    private void SynchronizeScrollBar(ScrollLayoutSolution solution)
    {
        var vertical = solution.Axis == Orientation.Vertical;
        var length = vertical ? solution.Viewport.Height : solution.Viewport.Width;
        var extent = vertical ? solution.Extent.Height : solution.Extent.Width;
        var position = vertical ? _offset.Y : _offset.X;
        _isSynchronizingBar = true;
        try
        {
            _scrollBar.LargeChange = length;
            _scrollBar.SynchronizeRange(0, Math.Max(0, extent - length), length, position);
        }
        finally
        {
            _isSynchronizingBar = false;
        }
        if (solution.ShowBar)
        {
            _scrollBar.ClearValue(UIElement.OpacityProperty);
            _scrollBar.IsHitTestVisible = true;
        }
        else
        {
            _scrollBar.CancelInteraction();
            _scrollBar.Opacity = 0;
            _scrollBar.IsHitTestVisible = false;
        }
    }

    private void OnScrollBarPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_isSynchronizingBar || !ReferenceEquals(args.Property, ScrollBar.ValueProperty))
            return;
        SetOffset(_scrollBar.Orientation == Orientation.Vertical
            ? new Point(0, _scrollBar.Value) : new Point(_scrollBar.Value, 0));
    }

    private void Invalidate()
    {
        Host.InvalidateMeasure();
        InvalidateMeasure();
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!ReferenceEquals(args.Property, MultiSelectionModel<T>.SelectionModeProperty))
            return;
        var change = (PropertyChangedEventArgs<SelectionMode>)args;
        RaisePropertyChanged(SelectionModeProperty, change.OldValue, change.NewValue);
    }

    private void RefreshStates()
    {
        foreach (var item in _realized.Values)
            item.SetStates(Selection.IsSelected(item.Index), item.Index == _navigationIndex);
    }

    /// <summary>Requests the minimum scrolling needed to show an item without changing selection or actively transferring focus.</summary>
    /// <param name="index">The zero-based source index; invalid indices are ignored.</param>
    /// <remarks>Recycling focused item content may clear focus.</remarks>
    public void ScrollIntoView(int index)
    {
        VerifyMutationAccess();
        if ((uint)index >= (uint)(_source?.Count ?? 0))
            return;
        _pendingScrollIndex = index;
        Invalidate();
    }

    private Point ResolveOffset(Size viewport, Point offset)
    {
        if (_pendingScrollIndex is not { } index)
            return offset;
        _pendingScrollIndex = null;
        var layout = CreateLayout(viewport, _source?.Count ?? 0);
        return (uint)index < (uint)layout.Count ? layout.BringIntoView(index, viewport, offset) : offset;
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(PointerWheelEventArgs args)
    {
        base.OnPointerWheel(args);
        if (args.Handled || !_hasResolvedScrollRange)
            return;
        var lines = UIToolkit.WheelScrollLines;
        if (lines == 0)
            return;
        var deltaX = args.DeltaX;
        var deltaY = args.DeltaY;
        if ((args.Modifiers & KeyModifiers.Shift) != 0)
        {
            deltaX += deltaY;
            deltaY = 0;
        }
        var vertical = _scrollBar.Orientation == Orientation.Vertical;
        var delta = vertical ? deltaY : deltaX;
        if (delta == 0)
            return;
        var length = vertical ? _scrollLayout.Viewport.Height : _scrollLayout.Viewport.Width;
        var step = UIToolkit.GetWheelScrollStep(lines, _scrollBar.SmallChange, length);
        var before = _offset;
        SetOffset(vertical
            ? new Point(0, before.Y - delta * step)
            : new Point(before.X - delta * step, 0));
        if (_offset != before)
            args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.Handled || !IsNavigationTarget(args.Source) || !IsNavigationTarget(Scene?.FocusedNode))
            return;
        if (args.Key == Key.A && args.Modifiers == KeyModifiers.Control)
        {
            if (Selection.SelectionMode == SelectionMode.Multiple)
            {
                Selection.SelectAll();
                args.Handled = true;
            }
            return;
        }
        if (args.Key == Key.Space && args.Modifiers == KeyModifiers.None)
        {
            if (!args.IsRepeat && _navigationIndex >= 0)
                Selection.Toggle(_navigationIndex);
            args.Handled = true;
            return;
        }
        if ((args.Modifiers & ~(KeyModifiers.Control | KeyModifiers.Shift)) != 0)
            return;
        var layout = CreateLayout(_scrollLayout.Viewport, _source?.Count ?? 0);
        if (layout.Navigate(_navigationIndex, args.Key) is not { } target)
            return;
        var previous = _navigationIndex;
        _navigationIndex = target;
        var control = (args.Modifiers & KeyModifiers.Control) != 0;
        var shift = (args.Modifiers & KeyModifiers.Shift) != 0;
        if (!control || shift)
            ApplySelection(target, previous, control && Selection.SelectionMode == SelectionMode.Multiple, shift);
        RefreshStates();
        ScrollIntoView(target);
        args.Handled = true;
    }

    private bool IsNavigationTarget(UIElement? node) =>
        ReferenceEquals(node, this) ||
        node is ItemContainer && ReferenceEquals(node.Parent, Host);

    private void OnItemClicked(ItemContainer item, PointerButtonEventArgs args)
    {
        if (args.Button != MouseButton.Left || item.Index < 0 ||
            (args.Modifiers & ~(KeyModifiers.Control | KeyModifiers.Shift)) != 0)
            return;
        var previous = _navigationIndex;
        _navigationIndex = item.Index;
        ApplySelection(item.Index, previous,
            (args.Modifiers & KeyModifiers.Control) != 0,
            (args.Modifiers & KeyModifiers.Shift) != 0);
        RefreshStates();
        args.Handled = true;
    }

    private void ApplySelection(int target, int previousNavigation, bool control, bool shift)
    {
        if (shift && Selection.SelectionMode == SelectionMode.Multiple)
        {
            if (_anchorIndex < 0)
                _anchorIndex = previousNavigation >= 0 ? previousNavigation : target;
            if (control)
                Selection.SelectRange(_anchorIndex, target);
            else
                Selection.SelectRangeOnly(_anchorIndex, target);
            return;
        }
        _anchorIndex = target;
        if (control)
            Selection.Toggle(target);
        else
            Selection.SelectOnly(target);
    }

    private void UpdateViewport(Size viewport, Point offset)
    {
        var layout = CreateLayout(viewport, _source?.Count ?? 0);
        if (Layout != layout)
        {
            Layout = layout;
            Host.InvalidateMeasure();
        }
        var visible = Layout.Window(viewport, offset, 0);
        var retained = Layout.Window(viewport, offset, 1);
        _poolCapacity = visible.End - visible.Start;
        TrimPools();
        var removals = new List<int>();
        foreach (var index in _realized.Keys)
        {
            if (index < retained.Start || index >= retained.End)
                removals.Add(index);
        }
        foreach (var index in removals)
        {
            var item = _realized[index];
            _realized.Remove(index);
            Release(item, true);
        }
        for (var index = retained.Start; index < retained.End; index++)
        {
            if (!_realized.ContainsKey(index))
                Realize(index);
        }
        RefreshStates();
    }

    private void Realize(int index)
    {
        var item = _containerPool.Count > 0 ? _containerPool.Pop() : new ItemContainer(OnItemClicked);
        item.Index = index;
        item.Data = _source![index];
        item.Template = _template ?? (item.Data is UIElement ? null : DefaultTemplate);
        item.SetRoot(CreateContent(item.Data, item.Template));
        var bounds = Layout.Bounds(index);
        item.Width = bounds.Width;
        item.Height = bounds.Height;
        _realized.Add(index, item);
        Host.Children.Add(item);
    }

    private UIElement? CreateContent(object? data, IDataTemplate? template)
    {
        if (data is null)
            return null;
        if (template is IRecyclingDataTemplate recycling && _contentPool.Count > 0)
            return recycling.Update(data, _contentPool.Pop());
        if (template is not null)
            return template.Build(data);
        return (UIElement)data;
    }

    private void Release(ItemContainer item, bool recycle)
    {
        var root = item.ContentRoot;
        var template = item.Template;
        if (root is not null && item.Data is { } data && template is IRecyclingDataTemplate recycling)
            recycling.Clear(data, root);
        item.SetRoot(null);
        Host.Children.Remove(item);
        item.Index = -1;
        item.Data = null;
        item.Template = null;
        item.SetStates(false, false);
        if (!recycle)
            return;
        if (root is not null && template is IRecyclingDataTemplate &&
            ReferenceEquals(template, _template ?? DefaultTemplate) && _contentPool.Count < _poolCapacity)
            _contentPool.Push(root);
        if (_containerPool.Count < _poolCapacity)
            _containerPool.Push(item);
    }

    private void ResetItems()
    {
        foreach (var item in _realized.Values)
            Release(item, false);
        _realized.Clear();
        _contentPool.Clear();
        _containerPool.Clear();
    }

    private void TrimPools()
    {
        while (_contentPool.Count > _poolCapacity)
            _contentPool.Pop();
        while (_containerPool.Count > _poolCapacity)
            _containerPool.Pop();
    }

    private void OnSourceChanged(ListChangedEventArgs<T> change)
    {
        VerifyMutationAccess();
        var moved = new Dictionary<int, ItemContainer>(_realized.Count);
        foreach (var (index, item) in _realized)
        {
            var next = MapIndex(index, change);
            if (next < 0)
                Release(item, true);
            else
            {
                item.Index = next;
                moved.Add(next, item);
            }
        }
        _realized.Clear();
        foreach (var (index, item) in moved)
            _realized.Add(index, item);
        _anchorIndex = _anchorIndex < 0 ? -1 : MapIndex(_anchorIndex, change);
        if (_navigationIndex >= 0)
        {
            var next = MapIndex(_navigationIndex, change);
            _navigationIndex = next >= 0 ? next :
                Math.Min(_navigationIndex, (_source?.Count ?? 0) - 1);
        }
        RefreshStates();
        Invalidate();
    }

    private static int MapIndex(int index, ListChangedEventArgs<T> change)
    {
        if (change.Kind == ListChangeKind.Reorder)
            return change.Permutation[index];
        if (index < change.Index)
            return index;
        var removed = change.OldItems.Count;
        if (index - change.Index < removed)
            return -1;
        return index + change.NewItems.Count - removed;
    }

    private sealed class VirtualizedItemsHost(ItemsView<T> view) : Container
    {
        protected override Size MeasureCore(Size availableSize)
        {
            foreach (var item in Children.Cast<ItemContainer>())
            {
                var bounds = view.Layout.Bounds(item.Index);
                item.Width = bounds.Width;
                item.Height = bounds.Height;
                item.Measure(bounds.Size);
            }
            return view.Layout.Extent;
        }

        protected override void ArrangeCore(Size finalSize)
        {
            foreach (var item in Children.Cast<ItemContainer>())
                item.Arrange(view.Layout.Bounds(item.Index));
        }
    }

    private sealed class DefaultTextTemplate : IRecyclingDataTemplate
    {
        public UIElement? Build(object data)
        {
            var value = data as string ?? data.ToString();
            return string.IsNullOrEmpty(value) ? null : new Text { Content = value, IsHitTestVisible = false };
        }

        public UIElement? Update(object data, UIElement root)
        {
            var value = data as string ?? data.ToString();
            if (string.IsNullOrEmpty(value))
                return null;
            ((Text)root).Content = value;
            return root;
        }

        public void Clear(object data, UIElement root) => ((Text)root).Content = string.Empty;
    }

    private sealed class SourceSubscription(ItemsView<T> owner, IReadOnlyObservableList<T> source)
    {
        private readonly WeakReference<ItemsView<T>> _owner = new(owner);

        internal void Attach() => source.Changed += OnChanged;
        internal void Detach() => source.Changed -= OnChanged;

        private void OnChanged(object? sender, ListChangedEventArgs<T> change)
        {
            if (!_owner.TryGetTarget(out var owner))
            {
                Detach();
                return;
            }
            if (ReferenceEquals(owner._sourceSubscription, this))
                owner.OnSourceChanged(change);
        }
    }
}

internal readonly record struct ItemsViewLayout(
    int Count, bool IsGrid, Orientation Axis, int CrossCount,
    double ItemMain, double ItemCross, double MainSpacing, double CrossSpacing, Size Extent)
{
    private double MainStep => ItemMain + MainSpacing;
    private double CrossStep => ItemCross + CrossSpacing;
    private int LineCount => Count == 0 ? 0 : (Count - 1) / CrossCount + 1;

    internal static ItemsViewLayout CreateList(int count, Size viewport, Orientation axis,
        double itemExtent, double spacing, UIScene? scene) => Create(count, false, viewport, axis, 1,
        ResolveDimension(itemExtent, scene),
        axis == Orientation.Vertical ? viewport.Width : viewport.Height, ResolveSpacing(spacing, scene), 0);

    internal static ItemsViewLayout CreateGrid(int count, Size viewport, Orientation flow,
        double width, double height, double rowSpacing, double columnSpacing, UIScene? scene)
    {
        width = ResolveDimension(width, scene);
        height = ResolveDimension(height, scene);
        rowSpacing = ResolveSpacing(rowSpacing, scene);
        columnSpacing = ResolveSpacing(columnSpacing, scene);
        var vertical = flow == Orientation.Horizontal;
        var available = vertical ? viewport.Width : viewport.Height;
        var crossSize = vertical ? width : height;
        var crossSpacing = vertical ? columnSpacing : rowSpacing;
        var step = crossSize + crossSpacing;
        if (!double.IsFinite(step))
            throw new InvalidOperationException("The grid step is not finite.");
        var ratio = available / step + crossSpacing / step;
        var crossCount = count == 0 ? 1 : (int)Math.Max(1, Math.Min(count, Math.Floor(ratio)));
        return Create(count, true, viewport, vertical ? Orientation.Vertical : Orientation.Horizontal,
            crossCount, vertical ? height : width, crossSize,
            vertical ? rowSpacing : columnSpacing, crossSpacing);
    }

    private static double ResolveDimension(double value, UIScene? scene) =>
        (scene?.UseLayoutRounding ?? true) ? LayoutHelper.RoundLayoutValueUp(value, scene?.Scale ?? 1) : value;

    private static double ResolveSpacing(double value, UIScene? scene) =>
        (scene?.UseLayoutRounding ?? true) ? LayoutHelper.RoundLayoutValue(value, scene?.Scale ?? 1) : value;

    private static ItemsViewLayout Create(int count, bool grid, Size viewport, Orientation axis,
        int crossCount, double itemMain, double itemCross, double mainSpacing, double crossSpacing)
    {
        var lines = count == 0 ? 0 : (count - 1) / crossCount + 1;
        var main = lines == 0 ? 0 : lines * itemMain + (lines - 1) * mainSpacing;
        var cross = count == 0 ? 0 : grid
            ? crossCount * itemCross + (crossCount - 1) * crossSpacing
            : axis == Orientation.Vertical ? viewport.Width : viewport.Height;
        if (!double.IsFinite(main) || !double.IsFinite(cross) ||
            !double.IsFinite(itemMain + mainSpacing) || !double.IsFinite(itemCross + crossSpacing))
            throw new InvalidOperationException("The items layout cannot represent a finite extent.");
        var extent = axis == Orientation.Vertical ? new Size(cross, main) : new Size(main, cross);
        return new ItemsViewLayout(count, grid, axis, crossCount,
            itemMain, itemCross, mainSpacing, crossSpacing, extent);
    }

    internal Rect Bounds(int index)
    {
        var main = index / CrossCount * MainStep;
        var cross = index % CrossCount * CrossStep;
        return Axis == Orientation.Vertical
            ? new Rect(cross, main, ItemCross, ItemMain)
            : new Rect(main, cross, ItemMain, ItemCross);
    }

    internal (int Start, int End) Window(Size viewport, Point offset, int bufferPages)
    {
        if (Count == 0 || viewport.Width <= 0 || viewport.Height <= 0)
            return (0, 0);
        var length = Axis == Orientation.Vertical ? viewport.Height : viewport.Width;
        var position = Axis == Orientation.Vertical ? offset.Y : offset.X;
        var firstPosition = Math.Max(0, position - length * bufferPages);
        var endPosition = position + length * (1 + bufferPages);
        var firstLine = (int)Math.Min(LineCount, Math.Floor(firstPosition / MainStep));
        if (firstLine < LineCount && firstLine * MainStep + ItemMain <= firstPosition)
            firstLine++;
        var endLine = (int)Math.Min(LineCount, Math.Ceiling(endPosition / MainStep));
        return ((int)Math.Min(Count, (long)firstLine * CrossCount),
            (int)Math.Min(Count, (long)endLine * CrossCount));
    }

    internal int? Navigate(int index, Key key)
    {
        var supported = key is Key.Home or Key.End ||
            (IsGrid && key is Key.Up or Key.Down or Key.Left or Key.Right) ||
            (Axis == Orientation.Vertical ? key is Key.Up or Key.Down : key is Key.Left or Key.Right);
        if (!supported || Count == 0)
            return null;
        if (key == Key.Home)
            return 0;
        if (key == Key.End)
            return Count - 1;
        if (index < 0)
            return key is Key.Up or Key.Left ? Count - 1 : 0;
        var line = index / CrossCount;
        var cross = index % CrossCount;
        var previousMain = Axis == Orientation.Vertical ? Key.Up : Key.Left;
        var nextMain = Axis == Orientation.Vertical ? Key.Down : Key.Right;
        var previousCross = Axis == Orientation.Vertical ? Key.Left : Key.Up;
        if (key == previousMain)
            return line > 0 ? index - CrossCount : index;
        if (key == nextMain)
            return line + 1 < LineCount ? (int)Math.Min(Count - 1, (long)index + CrossCount) : index;
        if (key == previousCross)
            return cross > 0 ? index - 1 : index;
        return cross + 1 < CrossCount && index < Count - 1 ? index + 1 : index;
    }

    internal Point BringIntoView(int index, Size viewport, Point offset)
    {
        var start = index / CrossCount * MainStep;
        var position = Axis == Orientation.Vertical ? offset.Y : offset.X;
        var length = Axis == Orientation.Vertical ? viewport.Height : viewport.Width;
        var next = ItemMain > length || start < position ? start :
            start + ItemMain > position + length ? start + ItemMain - length : position;
        return Axis == Orientation.Vertical ? new Point(0, next) : new Point(next, 0);
    }
}
