namespace PanguEngine.Graphics.Text;

/// <summary>
/// Describes all inputs to a CPU text layout operation.
/// </summary>
/// <param name="Text">The source UTF-16 text.</param>
/// <param name="Font">The preferred font.</param>
/// <param name="FontSize">The font size in logical pixels.</param>
/// <param name="MaximumWidth">The maximum automatic wrapping width.</param>
/// <param name="LineHeight">The natural line height multiplier.</param>
/// <param name="Wrapping">The wrapping mode.</param>
/// <param name="Alignment">The horizontal alignment.</param>
public readonly record struct TextLayoutRequest(
    string Text,
    Font Font,
    double FontSize,
    double MaximumWidth,
    double LineHeight,
    TextWrapping Wrapping,
    TextAlignment Alignment);
