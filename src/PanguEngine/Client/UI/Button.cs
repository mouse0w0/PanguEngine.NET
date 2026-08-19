using PanguEngine.Graphics.Text;
using PanguEngine.Input;

namespace PanguEngine.Client.UI;

/// <summary>
/// Provides a clickable control with optional text and image content.
/// </summary>
public sealed class Button : Control
{
    /// <summary>
    /// Identifies the <see cref="Text"/> property.
    /// </summary>
    public static readonly UiProperty<string> TextProperty =
        UiProperty.Register<Button, string>(
            nameof(Text),
            string.Empty,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Font"/> property.
    /// </summary>
    public static readonly UiProperty<Font> FontProperty =
        UiProperty.Register<Button, Font>(
            nameof(Font),
            new Font(string.Empty),
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="FontSize"/> property.
    /// </summary>
    public static readonly UiProperty<double> FontSizeProperty =
        UiProperty.Register<Button, double>(
            nameof(FontSize),
            16d,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Foreground"/> property.
    /// </summary>
    public static readonly UiProperty<Color> ForegroundProperty =
        UiProperty.Register<Button, Color>(
            nameof(Foreground),
            new Color(242, 244, 247),
            UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Icon"/> property.
    /// </summary>
    public static readonly UiProperty<UiImage?> IconProperty =
        UiProperty.Register<Button, UiImage?>(
            nameof(Icon),
            defaultValue: null,
            invalidation: UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="IconSize"/> property.
    /// </summary>
    public static readonly UiProperty<double> IconSizeProperty =
        UiProperty.Register<Button, double>(
            nameof(IconSize),
            16d,
            UiPropertyInvalidation.Measure | UiPropertyInvalidation.Render);

    /// <summary>
    /// Identifies the <see cref="Spacing"/> property.
    /// </summary>
    public static readonly UiProperty<double> SpacingProperty =
        UiProperty.Register<Button, double>(
            nameof(Spacing),
            6d,
            UiPropertyInvalidation.Measure);

    private ImageView? _imageNode;
    private Text? _textNode;
    private bool _enterKeyDown;
    private bool _spaceKeyDown;

    /// <summary>
    /// Initializes a button with its default focus and decoration values.
    /// </summary>
    public Button()
    {
        Focusable = true;
        Padding = new Thickness(12, 7);
        Background = new SolidColorBrush(new Color(48, 54, 62));
        BorderBrush = new SolidColorBrush(new Color(92, 103, 116));
        BorderThickness = new Thickness(1);
    }

    /// <summary>
    /// Gets or sets the plain text displayed by this button.
    /// </summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the preferred font request for the button text.
    /// </summary>
    public Font Font
    {
        get => GetValue(FontProperty);
        set => SetValue(FontProperty, value);
    }

    /// <summary>
    /// Gets or sets the button text size in logical pixels.
    /// </summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the non-premultiplied button text color.
    /// </summary>
    public Color Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the shared image displayed before the button text.
    /// </summary>
    public UiImage? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the square icon slot size in logical pixels.
    /// </summary>
    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the spacing between the icon and text in logical pixels.
    /// </summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Occurs when pointer or keyboard input activates this button.
    /// </summary>
    public event EventHandler? Click;

    /// <inheritdoc />
    protected override void OnPropertyChanged(UiPropertyChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Property, TextProperty))
            SynchronizeText();
        else if (ReferenceEquals(eventArgs.Property, IconProperty))
            SynchronizeIcon();
        else if (_textNode is not null && ReferenceEquals(eventArgs.Property, FontProperty))
            _textNode.Font = Font;
        else if (_textNode is not null && ReferenceEquals(eventArgs.Property, FontSizeProperty))
            _textNode.FontSize = FontSize;
        else if (_textNode is not null && ReferenceEquals(eventArgs.Property, ForegroundProperty))
            _textNode.Color = Foreground;
        else if (_imageNode is not null && ReferenceEquals(eventArgs.Property, IconSizeProperty))
            SetIconSize(_imageNode);

        base.OnPropertyChanged(eventArgs);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(Size availableSize)
    {
        var spacing = GetLayoutSpacing();
        var image = _imageNode;
        var text = _textNode;
        if (image is null)
        {
            if (text is null)
                return Size.Zero;

            text.Measure(availableSize);
            return text.DesiredSize;
        }

        image.Measure(availableSize);
        if (text is null)
            return image.DesiredSize;

        var textAvailableWidth = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, availableSize.Width - image.DesiredSize.Width - spacing);
        text.Measure(new Size(textAvailableWidth, availableSize.Height));
        return new Size(
            AddFinite(image.DesiredSize.Width, spacing, text.DesiredSize.Width),
            Math.Max(image.DesiredSize.Height, text.DesiredSize.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Rect contentBounds)
    {
        var spacing = GetLayoutSpacing();
        var image = _imageNode;
        var text = _textNode;
        if (image is null)
        {
            if (text is not null)
                ArrangeCentered(text, contentBounds);
            return;
        }

        if (text is null)
        {
            ArrangeCentered(image, contentBounds);
            return;
        }

        var rowWidth = AddFinite(image.DesiredSize.Width, spacing, text.DesiredSize.Width);
        var rowHeight = Math.Max(image.DesiredSize.Height, text.DesiredSize.Height);
        var x = contentBounds.X + (contentBounds.Width - rowWidth) / 2;
        var rowY = contentBounds.Y + (contentBounds.Height - rowHeight) / 2;
        image.Arrange(new Rect(
            x,
            rowY + (rowHeight - image.DesiredSize.Height) / 2,
            image.DesiredSize));
        text.Arrange(new Rect(
            x + image.DesiredSize.Width + spacing,
            rowY + (rowHeight - text.DesiredSize.Height) / 2,
            text.DesiredSize));
    }

    /// <inheritdoc />
    protected override void DrawCore(UiDrawingContext context)
    {
        base.DrawCore(context);
        if (!IsEnabled)
            context.FillRectangle(DecorationBounds, new Color(0, 0, 0, 112));
        else if (IsPressed || _spaceKeyDown)
            context.FillRectangle(DecorationBounds, new Color(0, 0, 0, 56));
        else if (IsHovered)
            context.FillRectangle(DecorationBounds, new Color(255, 255, 255, 24));

        if (IsEnabled && IsFocused)
            DrawFocusFrame(context);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Button == MouseButton.Left)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(UiPointerButtonEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (eventArgs.Button == MouseButton.Left)
            eventArgs.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerClicked(UiPointerButtonEventArgs eventArgs)
    {
        var activate = eventArgs.Button == MouseButton.Left && IsEnabled;
        base.OnPointerClicked(eventArgs);
        if (eventArgs.Button != MouseButton.Left)
            return;

        eventArgs.Handled = true;
        if (activate)
            Click?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(UiKeyEventArgs eventArgs)
    {
        switch (eventArgs.Key)
        {
            case Key.Enter:
            {
                var activate = !_enterKeyDown && IsEnabled && IsFocused;
                _enterKeyDown = true;
                base.OnKeyDown(eventArgs);
                eventArgs.Handled = true;
                if (activate)
                    Click?.Invoke(this, EventArgs.Empty);
                return;
            }
            case Key.Space:
                _spaceKeyDown = true;
                base.OnKeyDown(eventArgs);
                eventArgs.Handled = true;
                return;
            default:
                base.OnKeyDown(eventArgs);
                return;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(UiKeyEventArgs eventArgs)
    {
        switch (eventArgs.Key)
        {
            case Key.Enter:
                _enterKeyDown = false;
                base.OnKeyUp(eventArgs);
                eventArgs.Handled = true;
                return;
            case Key.Space:
            {
                var activate = _spaceKeyDown && IsEnabled && IsFocused;
                _spaceKeyDown = false;
                base.OnKeyUp(eventArgs);
                eventArgs.Handled = true;
                if (activate)
                    Click?.Invoke(this, EventArgs.Empty);
                return;
            }
            default:
                base.OnKeyUp(eventArgs);
                return;
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(UiFocusChangedEventArgs eventArgs)
    {
        _enterKeyDown = false;
        _spaceKeyDown = false;
        base.OnLostFocus(eventArgs);
    }

    private void SynchronizeText()
    {
        var content = Text;
        if (content.Length == 0)
        {
            if (_textNode is null)
                return;

            _ = RemoveChild(_textNode);
            _textNode = null;
            return;
        }

        if (_textNode is not null)
        {
            _textNode.Content = content;
            return;
        }

        var text = new Text
        {
            Content = content,
            Font = Font,
            FontSize = FontSize,
            Color = Foreground,
            Wrapping = TextWrapping.NoWrap,
            IsHitTestVisible = false
        };
        AddChild(text);
        _textNode = text;
    }

    private void SynchronizeIcon()
    {
        var source = Icon;
        if (source is null)
        {
            if (_imageNode is null)
                return;

            _ = RemoveChild(_imageNode);
            _imageNode = null;
            return;
        }

        if (_imageNode is not null)
        {
            _imageNode.Source = source;
            return;
        }

        var image = new ImageView
        {
            Source = source,
            Stretch = ImageStretch.Uniform,
            IsHitTestVisible = false
        };
        SetIconSize(image);
        InsertChild(0, image);
        _imageNode = image;
    }

    private void SetIconSize(ImageView image)
    {
        var size = IconSize;
        image.Width = size;
        image.Height = size;
    }

    private double GetLayoutSpacing()
    {
        var iconSize = IconSize;
        var spacing = Spacing;
        if (!double.IsFinite(iconSize) || iconSize < 0)
            throw new InvalidOperationException("IconSize must be a finite non-negative value.");
        if (!double.IsFinite(spacing) || spacing < 0)
            throw new InvalidOperationException("Spacing must be a finite non-negative value.");

        var screen = Screen;
        if (screen?.UseLayoutRounding ?? true)
            spacing = UiLayoutHelper.RoundLayoutValue(spacing, screen?.Scale ?? 1);
        return spacing;
    }

    private void DrawFocusFrame(UiDrawingContext context)
    {
        var screen = Screen;
        var thickness = screen?.UseLayoutRounding ?? true
            ? UiLayoutHelper.RoundLayoutValue(1d, screen?.Scale ?? 1)
            : 1d;
        if (thickness == 0)
            return;

        var bounds = DecorationBounds;
        var innerX = bounds.X + Math.Min(thickness, bounds.Width);
        var innerY = bounds.Y + Math.Min(thickness, bounds.Height);
        var innerWidth = Math.Max(0, bounds.Width - thickness - thickness);
        var innerHeight = Math.Max(0, bounds.Height - thickness - thickness);
        var color = new Color(84, 169, 255);
        context.FillRectangle(
            new Rect(bounds.X, bounds.Y, bounds.Width, innerY - bounds.Y),
            color);
        context.FillRectangle(
            new Rect(
                innerX + innerWidth,
                innerY,
                bounds.X + bounds.Width - (innerX + innerWidth),
                innerHeight),
            color);
        context.FillRectangle(
            new Rect(
                bounds.X,
                innerY + innerHeight,
                bounds.Width,
                bounds.Y + bounds.Height - (innerY + innerHeight)),
            color);
        context.FillRectangle(
            new Rect(bounds.X, innerY, innerX - bounds.X, innerHeight),
            color);
    }

    private static void ArrangeCentered(UiNode child, Rect contentBounds)
    {
        var desiredSize = child.DesiredSize;
        child.Arrange(new Rect(
            contentBounds.X + (contentBounds.Width - desiredSize.Width) / 2,
            contentBounds.Y + (contentBounds.Height - desiredSize.Height) / 2,
            desiredSize));
    }

    private static double AddFinite(double first, double second, double third)
    {
        var result = first + second + third;
        if (!double.IsFinite(result))
            throw new InvalidOperationException("Button layout produced a non-finite content width.");
        return result;
    }
}
