using HarfBuzzSharp;

namespace PanguEngine.Graphics.Text;

internal readonly record struct TextElementRange(
    int Start,
    int Length,
    Script Script,
    bool IsUnsupported,
    bool IsBreakWhitespace);
