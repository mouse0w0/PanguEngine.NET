namespace PanguEngine.UI.Drawing;

/// <summary>
/// Restores a UI drawing context state when disposed.
/// </summary>
public readonly ref struct DrawingScope
{
    private readonly DrawingContext? _context;
    private readonly int _token;

    internal DrawingScope(DrawingContext context, int token)
    {
        _context = context;
        _token = token;
    }

    /// <summary>
    /// Restores the drawing state that existed before this scope was created.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scope is disposed more than once, out of order, or after its context is inactive.
    /// </exception>
    public void Dispose() =>
        _context?.Pop(_token);
}
