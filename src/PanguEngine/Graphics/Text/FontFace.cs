namespace PanguEngine.Graphics.Text;

/// <summary>
/// Represents a stable reference to a loaded physical font face.
/// </summary>
public sealed class FontFace
{
    internal FontFace(FontManager owner, ulong id, Font font, NativeFontFace nativeFace)
    {
        Owner = owner;
        Id = id;
        Font = font;
        NativeFace = nativeFace;
    }

    /// <summary>
    /// Gets the exact font metadata exposed by this face.
    /// </summary>
    public Font Font { get; }

    internal FontManager Owner { get; }
    internal ulong Id { get; }
    internal NativeFontFace NativeFace { get; }
}
