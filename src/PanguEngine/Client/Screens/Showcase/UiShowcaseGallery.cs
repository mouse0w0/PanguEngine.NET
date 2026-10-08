using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Arranges showcase cards in responsive columns with equal-height rows.
/// </summary>
/// <param name="minimumColumnWidth">The preferred minimum width of a column.</param>
/// <param name="maximumColumns">The maximum number of columns.</param>
internal sealed class UiShowcaseGallery(double minimumColumnWidth = 360, int maximumColumns = 2) : Panel
{
    private const double Gap = 16;
    private UiNode[] _items = [];
    private double[] _rowHeights = [];
    private int _columns = 1;

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        _items = Children.Where(child => child.Visibility != Visibility.Collapsed).ToArray();
        _columns = double.IsFinite(availableSize.Width)
            ? Math.Clamp((int)((availableSize.Width + Gap) / (minimumColumnWidth + Gap)), 1, maximumColumns)
            : 1;
        var columnWidth = double.IsFinite(availableSize.Width)
            ? Math.Max(0, (availableSize.Width - Gap * (_columns - 1)) / _columns)
            : double.PositiveInfinity;
        foreach (var child in Children.Where(child => child.Visibility == Visibility.Collapsed))
            child.Measure(Size.Zero);
        _rowHeights = new double[(_items.Length + _columns - 1) / _columns];
        var desiredWidth = 0d;
        for (var index = 0; index < _items.Length; index++)
        {
            var child = _items[index];
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            _rowHeights[index / _columns] = Math.Max(_rowHeights[index / _columns], child.DesiredSize.Height);
        }

        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : desiredWidth,
            _rowHeights.Sum() + Gap * Math.Max(0, _rowHeights.Length - 1));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        foreach (var child in Children.Where(child => child.Visibility == Visibility.Collapsed))
            child.Arrange(Rect.Zero);
        var columnWidth = Math.Max(0, (contentBounds.Width - Gap * (_columns - 1)) / _columns);
        var top = contentBounds.Y;
        for (var row = 0; row < _rowHeights.Length; row++)
        {
            for (var column = 0; column < _columns; column++)
            {
                var index = row * _columns + column;
                if (index >= _items.Length)
                    break;
                _items[index].Arrange(new Rect(
                    contentBounds.X + column * (columnWidth + Gap), top, columnWidth, _rowHeights[row]));
            }
            top += _rowHeights[row] + Gap;
        }
    }
}
