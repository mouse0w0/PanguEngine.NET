namespace PanguEngine.Client.UI;

public partial class UiScreen
{
    private double _scale = 1;
    private bool _useLayoutRounding = true;

    /// <summary>
    /// Gets or sets the uniform scale from screen logical coordinates to output coordinates.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not finite or is not greater than zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a different value is assigned while an open screen is accessed from the wrong
    /// thread, is updating layout, or is generating drawing commands.
    /// </exception>
    public double Scale
    {
        get
        {
            lock (_stateSync)
                return _scale;
        }
        set => SetScale(value);
    }

    /// <summary>
    /// Gets or sets whether layout values snap to the physical pixel grid defined by <see cref="Scale"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a different value is assigned while an open screen is accessed from the wrong
    /// thread, is updating layout, or is generating drawing commands.
    /// </exception>
    public bool UseLayoutRounding
    {
        get
        {
            lock (_stateSync)
                return _useLayoutRounding;
        }
        set => SetUseLayoutRounding(value);
    }

    private void SetScale(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "UI scale must be finite and greater than zero.");
        }

        lock (_stateSync)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_scale == value)
                return;
            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
            if (IsUpdatingLayout)
                throw new InvalidOperationException("The UI screen scale cannot change during layout.");
            if (_isDrawing)
            {
                throw new InvalidOperationException(
                    "The UI screen scale cannot change while drawing commands are generated.");
            }

            _scale = value;
        }

        Root?.InvalidateMeasureSubtree();
    }

    private void SetUseLayoutRounding(bool value)
    {
        lock (_stateSync)
        {
            if (_useLayoutRounding == value)
                return;
            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
            if (IsUpdatingLayout)
                throw new InvalidOperationException("UI layout rounding cannot change during layout.");
            if (_isDrawing)
            {
                throw new InvalidOperationException(
                    "UI layout rounding cannot change while drawing commands are generated.");
            }

            _useLayoutRounding = value;
        }

        Root?.InvalidateMeasureSubtree();
    }

    private Point ToLogicalPoint(Point outputPoint)
    {
        var scale = Scale;

        return new Point(outputPoint.X / scale, outputPoint.Y / scale);
    }
}
