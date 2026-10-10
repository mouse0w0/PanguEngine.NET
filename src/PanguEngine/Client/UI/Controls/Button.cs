using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Input;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Graphics.Text;
using PanguEngine.Input;

namespace PanguEngine.Client.UI.Controls;

/// <summary>
/// Provides a clickable control with optional text and image content.
/// </summary>
public class Button : Control
{
    /// <summary>
    /// Identifies the <see cref="Text"/> property.
    /// </summary>
    public static readonly Property<string> TextProperty =
        Property.Register<Button, string>(
            nameof(Text),
            string.Empty,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => value is not null,
            validationMessage: "Text cannot be null.");

    /// <summary>
    /// Identifies the <see cref="Font"/> property.
    /// </summary>
    public static readonly Property<Font> FontProperty =
        Property.Register<Button, Font>(
            nameof(Font),
            new Font(string.Empty),
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: static value => value is not null,
            validationMessage: "Font cannot be null.");

    /// <summary>
    /// Identifies the <see cref="FontSize"/> property.
    /// </summary>
    public static readonly Property<double> FontSizeProperty =
        Property.Register<Button, double>(
            nameof(FontSize),
            16d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFinitePositive,
            validationMessage: "FontSize must be positive and finite.");

    /// <summary>
    /// Identifies the <see cref="Foreground"/> property.
    /// </summary>
    public static readonly Property<Color> ForegroundProperty =
        Property.Register<Button, Color>(
            nameof(Foreground),
            new Color(242, 244, 247));

    /// <summary>
    /// Identifies the <see cref="Icon"/> property.
    /// </summary>
    public static readonly Property<UiImage?> IconProperty =
        Property.Register<Button, UiImage?>(
            nameof(Icon),
            defaultValue: null,
            onChanged: static (node, _, _) => node.InvalidateMeasure());

    /// <summary>
    /// Identifies the <see cref="IconSize"/> property.
    /// </summary>
    public static readonly Property<double> IconSizeProperty =
        Property.Register<Button, double>(
            nameof(IconSize),
            16d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "IconSize must be a finite non-negative value.");

    /// <summary>
    /// Identifies the <see cref="Spacing"/> property.
    /// </summary>
    public static readonly Property<double> SpacingProperty =
        Property.Register<Button, double>(
            nameof(Spacing),
            6d,
            onChanged: static (node, _, _) => node.InvalidateMeasure(),
            validate: IsFiniteNonNegative,
            validationMessage: "Spacing must be a finite non-negative value.");

    static Button()
    {
        UiCssRegistry.RegisterElement<Button>("Button");
        UiCssRegistry.RegisterProperty<Button, double>("font-size", FontSizeProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<Button, Color>("foreground", ForegroundProperty, UiCssValueConverters.ParseColor);
        UiCssRegistry.RegisterProperty<Button, double>("icon-size", IconSizeProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<Button, double>("spacing", SpacingProperty, UiCssValueConverters.ParseLength);
    }

    private ImageView? _imageNode;
    private Text? _textNode;
    private bool _enterKeyDown;
    private bool _spaceKeyDown;

    /// <summary>
    /// Initializes a button with keyboard focus enabled.
    /// </summary>
    public Button()
    {
        Focusable = true;
    }

    /// <summary>
    /// Gets or sets the plain text displayed by this button.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null.</exception>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Gets or sets the preferred font request for the button text.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is null.</exception>
    public Font Font
    {
        get => GetValue(FontProperty);
        set => SetValue(FontProperty, value);
    }

    /// <summary>
    /// Gets or sets the button text size in logical pixels.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and positive.</exception>
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
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the spacing between the icon and text in logical pixels.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is not finite and non-negative.</exception>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Occurs when pointer or keyboard input activates this button.
    /// </summary>
    public event EventHandler? Click;

    /// <summary>
    /// Raises the <see cref="Click"/> event when this button is activated.
    /// </summary>
    protected virtual void OnClick() => Click?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
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
            OnClick();
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
                    OnClick();
                return;
            }
            case Key.Space:
                SetSpaceKeyDown(true);
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
                SetSpaceKeyDown(false);
                base.OnKeyUp(eventArgs);
                eventArgs.Handled = true;
                if (activate)
                    OnClick();
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
        SetSpaceKeyDown(false);
        base.OnLostFocus(eventArgs);
    }

    /// <inheritdoc />
    protected override bool IsPressedPseudoClassActive => IsPressed || _spaceKeyDown;

    private void SynchronizeText()
    {
        var content = Text;
        if (content.Length == 0)
        {
            if (_textNode is null)
                return;

            _ = Children.Remove(_textNode);
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
        Children.Add(text);
        _textNode = text;
    }

    private void SynchronizeIcon()
    {
        var source = Icon;
        if (source is null)
        {
            if (_imageNode is null)
                return;

            _ = Children.Remove(_imageNode);
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
        Children.Insert(0, image);
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
        var spacing = Spacing;

        var screen = Screen;
        if (screen?.UseLayoutRounding ?? true)
            spacing = UiLayoutHelper.RoundLayoutValue(spacing, screen?.Scale ?? 1);
        return spacing;
    }

    private void SetSpaceKeyDown(bool value)
    {
        if (_spaceKeyDown == value)
            return;
        _spaceKeyDown = value;
        RefreshPressedPseudoClass();
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
