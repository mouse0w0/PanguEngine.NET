using PanguEngine.Client.UI.Styling;
using PanguEngine.ComponentModel;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Arranges child nodes in uniformly sized cells in row order.
/// </summary>
/// <remarks>
/// When both dimensions are fixed, children beyond the specified capacity continue into additional rows.
/// </remarks>
public sealed class UniformGrid : Panel
{
    /// <summary>
    /// Identifies the <see cref="Rows"/> property.
    /// </summary>
    public static readonly Property<int> RowsProperty =
        Property.Register<UniformGrid, int>(
            nameof(Rows),
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => value >= 0,
            validationMessage: "Rows must be non-negative.");

    /// <summary>
    /// Identifies the <see cref="Columns"/> property.
    /// </summary>
    public static readonly Property<int> ColumnsProperty =
        Property.Register<UniformGrid, int>(
            nameof(Columns),
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => value >= 0,
            validationMessage: "Columns must be non-negative.");

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> property.
    /// </summary>
    public static readonly Property<double> RowSpacingProperty =
        Property.Register<UniformGrid, double>(
            nameof(RowSpacing),
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "RowSpacing must be a finite non-negative value.");

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> property.
    /// </summary>
    public static readonly Property<double> ColumnSpacingProperty =
        Property.Register<UniformGrid, double>(
            nameof(ColumnSpacing),
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "ColumnSpacing must be a finite non-negative value.");

    private int _rows;
    private int _columns;
    private bool _hasParticipants;

    static UniformGrid()
    {
        UiCssRegistry.RegisterElement<UniformGrid>("UniformGrid");
        UiCssRegistry.RegisterProperty<UniformGrid, int>("rows", RowsProperty, UiCssValueConverters.ParseInteger);
        UiCssRegistry.RegisterProperty<UniformGrid, int>("columns", ColumnsProperty, UiCssValueConverters.ParseInteger);
        UiCssRegistry.RegisterProperty<UniformGrid, double>("row-spacing", RowSpacingProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UniformGrid, double>("column-spacing", ColumnSpacingProperty, UiCssValueConverters.ParseLength);
    }

    /// <summary>
    /// Gets or sets the row count, or zero to calculate it automatically.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is negative.</exception>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>
    /// Gets or sets the column count, or zero to calculate it automatically.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is negative.</exception>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative spacing between rows.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>
    /// Gets or sets the finite non-negative spacing between columns.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var participantCount = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed)
                participantCount++;
        }

        (_rows, _columns) = ResolveDimensions(participantCount);
        _hasParticipants = participantCount != 0;
        if (!_hasParticipants)
        {
            foreach (var child in Children)
                child.Measure(Size.Zero);
            return Size.Zero;
        }

        var (rowSpacing, columnSpacing) = GetLayoutSpacing();
        var totalRowSpacing = GetTotalSpacing(_rows, rowSpacing);
        var totalColumnSpacing = GetTotalSpacing(_columns, columnSpacing);
        var childConstraint = new Size(
            GetCellExtent(availableSize.Width, totalColumnSpacing, _columns),
            GetCellExtent(availableSize.Height, totalRowSpacing, _rows));
        var desiredWidth = 0d;
        var desiredHeight = 0d;

        foreach (var child in Children)
        {
            child.Measure(childConstraint);
            if (child.Visibility == Visibility.Collapsed)
                continue;

            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
        }

        return new Size(
            desiredWidth * _columns + totalColumnSpacing,
            desiredHeight * _rows + totalRowSpacing);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        if (!_hasParticipants)
        {
            foreach (var child in Children)
                child.Arrange(Rect.Zero);
            return;
        }

        var screen = Screen;
        var useLayoutRounding = screen?.UseLayoutRounding ?? true;
        var scale = screen?.Scale ?? 1;
        var (rowSpacing, columnSpacing) = GetLayoutSpacing();
        var cellWidth = GetCellExtent(contentBounds.Width, GetTotalSpacing(_columns, columnSpacing), _columns);
        var cellHeight = GetCellExtent(contentBounds.Height, GetTotalSpacing(_rows, rowSpacing), _rows);
        var index = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            var (x, width) = GetCellBounds(contentBounds.X, index % _columns, cellWidth, columnSpacing, useLayoutRounding, scale);
            var (y, height) = GetCellBounds(contentBounds.Y, index / _columns, cellHeight, rowSpacing, useLayoutRounding, scale);
            child.Arrange(new Rect(x, y, width, height));
            index++;
        }
    }

    private (int Rows, int Columns) ResolveDimensions(int participantCount)
    {
        var count = Math.Max(1, participantCount);
        var rows = Rows;
        var columns = Columns;
        if (rows == 0 && columns == 0)
            rows = columns = (int)Math.Ceiling(Math.Sqrt(count));
        else if (rows == 0)
            rows = 1 + (count - 1) / columns;
        else if (columns == 0)
            columns = 1 + (count - 1) / rows;

        return (rows, columns);
    }

    private (double RowSpacing, double ColumnSpacing) GetLayoutSpacing()
    {
        var rowSpacing = RowSpacing;
        var columnSpacing = ColumnSpacing;
        var screen = Screen;
        if (screen?.UseLayoutRounding ?? true)
        {
            var scale = screen?.Scale ?? 1;
            rowSpacing = UiLayoutHelper.RoundLayoutValue(rowSpacing, scale);
            columnSpacing = UiLayoutHelper.RoundLayoutValue(columnSpacing, scale);
        }

        return (rowSpacing, columnSpacing);
    }

    private static double GetTotalSpacing(int count, double spacing)
    {
        var result = (count - 1) * spacing;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("UniformGrid layout produced non-finite total spacing.");

        return result;
    }

    private static double GetCellExtent(double available, double totalSpacing, int count) =>
        Math.Max(0, available - totalSpacing) / count;

    private static (double Origin, double Extent) GetCellBounds(
        double origin, int index, double cellExtent, double spacing, bool useLayoutRounding, double scale)
    {
        var start = origin + index * cellExtent;
        var end = origin + (index + 1d) * cellExtent;
        if (useLayoutRounding)
        {
            start = UiLayoutHelper.RoundLayoutValue(start, scale);
            end = UiLayoutHelper.RoundLayoutValue(end, scale);
        }

        return (start + index * spacing, end - start);
    }
}
