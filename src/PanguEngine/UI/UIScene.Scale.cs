namespace PanguEngine.UI;

public partial class UIScene
{
    private double _scale;
    private bool _hasExplicitScale;
    private bool _useLayoutRounding = true;

    /// <summary>
    /// Gets or sets the uniform scale from scene logical coordinates to output coordinates.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not finite or is not greater than zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a different value is assigned during layout or drawing, or in Debug builds when
    /// an open scene is accessed from outside the host UI thread.
    /// </exception>
    public double Scale
    {
        get => _scale;
        set => SetScale(value);
    }

    /// <summary>
    /// Gets or sets whether layout values snap to the physical pixel grid defined by <see cref="Scale"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a different value is assigned during layout or drawing, or in Debug builds when
    /// an open scene is accessed from outside the host UI thread.
    /// </exception>
    public bool UseLayoutRounding
    {
        get => _useLayoutRounding;
        set => SetUseLayoutRounding(value);
    }

    private void SetScale(double value)
    {
        UIToolkit.ValidateScale(value, nameof(value));

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (_scale == value)
        {
            _hasExplicitScale = true;
            return;
        }

        if (_isOpen)
            VerifyOwnerThreadCore();
        if (IsUpdatingLayout)
            throw new InvalidOperationException("The UI scene scale cannot change during layout.");
        if (_isDrawing)
        {
            throw new InvalidOperationException(
                "The UI scene scale cannot change while drawing commands are generated.");
        }

        _scale = value;
        _hasExplicitScale = true;
        Root?.InvalidateMeasureSubtree();
    }

    private void SynchronizeDefaultScale()
    {
        if (_hasExplicitScale)
            return;

        var scale = UIToolkit.DefaultScale;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (_scale == scale)
            return;

        _scale = scale;
        Root?.InvalidateMeasureSubtree();
    }

    private void SetUseLayoutRounding(bool value)
    {
        if (_useLayoutRounding == value)
            return;
        if (_isOpen)
            VerifyOwnerThreadCore();
        if (IsUpdatingLayout)
            throw new InvalidOperationException("UI layout rounding cannot change during layout.");
        if (_isDrawing)
        {
            throw new InvalidOperationException(
                "UI layout rounding cannot change while drawing commands are generated.");
        }

        _useLayoutRounding = value;
        Root?.InvalidateMeasureSubtree();
    }

    private Point ToLogicalPoint(Point outputPoint)
    {
        var scale = Scale;

        return new Point(outputPoint.X / scale, outputPoint.Y / scale);
    }
}
