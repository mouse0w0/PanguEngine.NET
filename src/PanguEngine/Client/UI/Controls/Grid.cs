using System.Globalization;
using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI.Controls;

/// <summary>Arranges child nodes in explicitly assigned rows and columns.</summary>
/// <remarks>
/// Empty definitions represent one proportional track. Unassigned children share the first cell.
/// Measurement resolves columns before measuring row content at the resulting widths.
/// </remarks>
public sealed class Grid : Panel
{
    /// <summary>Identifies the <see cref="RowDefinitions"/> property.</summary>
    public static readonly Property<GridDefinitions> RowDefinitionsProperty =
        Property.Register<Grid, GridDefinitions>(nameof(RowDefinitions), GridDefinitions.Empty,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>Identifies the <see cref="ColumnDefinitions"/> property.</summary>
    public static readonly Property<GridDefinitions> ColumnDefinitionsProperty =
        Property.Register<Grid, GridDefinitions>(nameof(ColumnDefinitions), GridDefinitions.Empty,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>Identifies the <see cref="RowSpacing"/> property.</summary>
    public static readonly Property<double> RowSpacingProperty =
        Property.Register<Grid, double>(nameof(RowSpacing),
            onChanged: static (node, _, _) => node.InvalidateMeasure(), validate: IsFiniteNonNegative,
            validationMessage: "Row spacing must be finite and non-negative.");

    /// <summary>Identifies the <see cref="ColumnSpacing"/> property.</summary>
    public static readonly Property<double> ColumnSpacingProperty =
        Property.Register<Grid, double>(nameof(ColumnSpacing),
            onChanged: static (node, _, _) => node.InvalidateMeasure(), validate: IsFiniteNonNegative,
            validationMessage: "Column spacing must be finite and non-negative.");

    /// <summary>Identifies the attached zero-based row index property.</summary>
    public static readonly Property<int> RowProperty =
        Property.RegisterAttached<Grid, UiNode, int>("Row",
            onChanged: static (node, _, _) => node.InvalidateMeasure(), validate: static value => value >= 0,
            validationMessage: "A grid row index cannot be negative.");

    /// <summary>Identifies the attached zero-based column index property.</summary>
    public static readonly Property<int> ColumnProperty =
        Property.RegisterAttached<Grid, UiNode, int>("Column",
            onChanged: static (node, _, _) => node.InvalidateMeasure(), validate: static value => value >= 0,
            validationMessage: "A grid column index cannot be negative.");

    /// <summary>Identifies the attached row span property.</summary>
    public static readonly Property<int> RowSpanProperty =
        Property.RegisterAttached<Grid, UiNode, int>("RowSpan", 1,
            onChanged: static (node, _, _) => node.InvalidateMeasure(), validate: static value => value >= 1,
            validationMessage: "A grid row span must be at least one.");

    /// <summary>Identifies the attached column span property.</summary>
    public static readonly Property<int> ColumnSpanProperty =
        Property.RegisterAttached<Grid, UiNode, int>("ColumnSpan", 1,
            onChanged: static (node, _, _) => node.InvalidateMeasure(), validate: static value => value >= 1,
            validationMessage: "A grid column span must be at least one.");

    private GridAxis _columns = null!;
    private GridAxis _rows = null!;

    static Grid()
    {
        UiCssRegistry.RegisterElement<Grid>("Grid");
        UiCssRegistry.RegisterProperty<Grid, GridDefinitions>("row-definitions", RowDefinitionsProperty, GridDefinitions.Parse);
        UiCssRegistry.RegisterProperty<Grid, GridDefinitions>("column-definitions", ColumnDefinitionsProperty, GridDefinitions.Parse);
        UiCssRegistry.RegisterProperty<Grid, double>("row-spacing", RowSpacingProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<Grid, double>("column-spacing", ColumnSpacingProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, int>("grid-row", RowProperty, ParseIndex);
        UiCssRegistry.RegisterProperty<UiNode, int>("grid-column", ColumnProperty, ParseIndex);
        UiCssRegistry.RegisterProperty<UiNode, int>("grid-row-span", RowSpanProperty, ParseIndex);
        UiCssRegistry.RegisterProperty<UiNode, int>("grid-column-span", ColumnSpanProperty, ParseIndex);
    }

    /// <summary>Gets or sets the immutable row definitions; an empty sequence represents one star row.</summary>
    public GridDefinitions RowDefinitions
    {
        get => GetValue(RowDefinitionsProperty);
        set => SetValue(RowDefinitionsProperty, value);
    }

    /// <summary>Gets or sets the immutable column definitions; an empty sequence represents one star column.</summary>
    public GridDefinitions ColumnDefinitions
    {
        get => GetValue(ColumnDefinitionsProperty);
        set => SetValue(ColumnDefinitionsProperty, value);
    }

    /// <summary>Gets or sets the finite non-negative distance between rows in logical pixels.</summary>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>Gets or sets the finite non-negative distance between columns in logical pixels.</summary>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>Gets the attached zero-based row index.</summary>
    /// <param name="node">The child node.</param>
    /// <returns>The assigned row index.</returns>
    public static int GetRow(UiNode node) => node.GetValue(RowProperty);

    /// <summary>Sets the attached zero-based row index.</summary>
    /// <param name="node">The child node.</param>
    /// <param name="value">The non-negative row index.</param>
    public static void SetRow(UiNode node, int value) => node.SetValue(RowProperty, value);

    /// <summary>Gets the attached zero-based column index.</summary>
    /// <param name="node">The child node.</param>
    /// <returns>The assigned column index.</returns>
    public static int GetColumn(UiNode node) => node.GetValue(ColumnProperty);

    /// <summary>Sets the attached zero-based column index.</summary>
    /// <param name="node">The child node.</param>
    /// <param name="value">The non-negative column index.</param>
    public static void SetColumn(UiNode node, int value) => node.SetValue(ColumnProperty, value);

    /// <summary>Gets the number of rows spanned by a node.</summary>
    /// <param name="node">The child node.</param>
    /// <returns>The assigned row span.</returns>
    public static int GetRowSpan(UiNode node) => node.GetValue(RowSpanProperty);

    /// <summary>Sets the number of rows spanned by a node.</summary>
    /// <param name="node">The child node.</param>
    /// <param name="value">The row span, at least one.</param>
    public static void SetRowSpan(UiNode node, int value) => node.SetValue(RowSpanProperty, value);

    /// <summary>Gets the number of columns spanned by a node.</summary>
    /// <param name="node">The child node.</param>
    /// <returns>The assigned column span.</returns>
    public static int GetColumnSpan(UiNode node) => node.GetValue(ColumnSpanProperty);

    /// <summary>Sets the number of columns spanned by a node.</summary>
    /// <param name="node">The child node.</param>
    /// <param name="value">The column span, at least one.</param>
    public static void SetColumnSpan(UiNode node, int value) => node.SetValue(ColumnSpanProperty, value);

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var screen = Screen;
        var round = screen?.UseLayoutRounding ?? true;
        var scale = screen?.Scale ?? 1;
        var columnSpacing = round ? UiLayoutHelper.RoundLayoutValue(ColumnSpacing, scale) : ColumnSpacing;
        var rowSpacing = round ? UiLayoutHelper.RoundLayoutValue(RowSpacing, scale) : RowSpacing;
        var columns = new GridAxis(ColumnDefinitions, columnSpacing);
        var rows = new GridAxis(RowDefinitions, rowSpacing);
        var cells = GetCells(columns, rows);
        columns.Allocate(availableSize.Width, round, scale);
        rows.Allocate(availableSize.Height, round, scale);

        foreach (var cell in cells)
            cell.Node.Measure(new Size(columns.GetDiscoveryConstraint(cell.Column, cell.ColumnSpan, false),
                rows.GetDiscoveryConstraint(cell.Row, cell.RowSpan, true)));
        CollectRequirements(columns, cells, horizontal: true);
        columns.Allocate(availableSize.Width, round, scale);

        foreach (var cell in cells)
            cell.Node.Measure(new Size(columns.GetExtent(cell.Column, cell.ColumnSpan),
                rows.GetDiscoveryConstraint(cell.Row, cell.RowSpan, true)));
        CollectRequirements(rows, cells, horizontal: false);
        rows.Allocate(availableSize.Height, round, scale);

        foreach (var cell in cells)
            cell.Node.Measure(new Size(columns.GetExtent(cell.Column, cell.ColumnSpan),
                rows.GetExtent(cell.Row, cell.RowSpan)));

        var result = new Size(columns.DesiredExtent, rows.DesiredExtent);
        _columns = columns;
        _rows = rows;
        return result;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var screen = Screen;
        var round = screen?.UseLayoutRounding ?? true;
        var scale = screen?.Scale ?? 1;
        _columns.Allocate(contentBounds.Width, round, scale);
        _rows.Allocate(contentBounds.Height, round, scale);
        foreach (var cell in GetCells(_columns, _rows))
        {
            if (cell.Node.Visibility == Visibility.Collapsed)
            {
                cell.Node.Arrange(Rect.Zero);
                continue;
            }
            var (x, width) = _columns.GetSlot(cell.Column, cell.ColumnSpan, contentBounds.X);
            var (y, height) = _rows.GetSlot(cell.Row, cell.RowSpan, contentBounds.Y);
            cell.Node.Arrange(new Rect(x, y, width, height));
        }
    }

    private Cell[] GetCells(GridAxis columns, GridAxis rows) => Children.Select(node =>
    {
        var (column, columnSpan) = columns.GetRange(GetColumn(node), GetColumnSpan(node));
        var (row, rowSpan) = rows.GetRange(GetRow(node), GetRowSpan(node));
        return new Cell(node, column, columnSpan, row, rowSpan);
    }).ToArray();

    private static void CollectRequirements(GridAxis axis, Cell[] cells, bool horizontal)
    {
        foreach (var cell in cells.OrderBy(cell => horizontal ? cell.ColumnSpan : cell.RowSpan))
        {
            if (cell.Node.Visibility == Visibility.Collapsed)
                continue;
            axis.AddRequirement(horizontal ? cell.Column : cell.Row,
                horizontal ? cell.ColumnSpan : cell.RowSpan,
                horizontal ? cell.Node.DesiredSize.Width : cell.Node.DesiredSize.Height);
        }
    }

    private static int ParseIndex(string value)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
            throw new FormatException($"Grid index or span '{value}' must be a unitless integer.");
        return result;
    }

    private readonly record struct Cell(UiNode Node, int Column, int ColumnSpan, int Row, int RowSpan);
}
