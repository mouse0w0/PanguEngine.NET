using PanguEngine.UI.Styling;

namespace PanguEngine.UI;

public partial class UIScene
{
    /// <summary>
    /// Gets the immutable snapshot of base style sheets for this scene.
    /// </summary>
    /// <remarks>
    /// New scenes include the engine default sheet. Use <see cref="SetBaseStyleSheets"/> to replace
    /// this source group, including replacing it with an empty collection.
    /// </remarks>
    public IReadOnlyList<StyleSheet> BaseStyleSheets => _styleResolver.BaseStyleSheets;

    /// <summary>Gets the immutable snapshot of application style sheets for this scene.</summary>
    /// <remarks>Application styles take precedence over base styles. This collection is empty on new scenes.</remarks>
    public IReadOnlyList<StyleSheet> StyleSheets => _styleResolver.StyleSheets;

    /// <summary>Replaces the base style sheets while preserving the application style sheets.</summary>
    /// <param name="styleSheets">The ordered style sheets to copy into the new base snapshot.</param>
    /// <remarks>Replaces the configuration immediately. Element styles are applied during scene layout.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="styleSheets"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown during a conflicting scene operation, or in Debug builds when called from outside the host UI thread.</exception>
    public void SetBaseStyleSheets(IEnumerable<StyleSheet> styleSheets) =>
        SetStyleSheetsCore(styleSheets, StyleOrigin.Base);

    /// <summary>Replaces the application style sheets while preserving the base style sheets.</summary>
    /// <param name="styleSheets">The ordered style sheets to copy into the new author snapshot.</param>
    /// <remarks>Replaces the configuration immediately. Element styles are applied during scene layout.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="styleSheets"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown during a conflicting scene operation, or in Debug builds when called from outside the host UI thread.</exception>
    public void SetStyleSheets(IEnumerable<StyleSheet> styleSheets) =>
        SetStyleSheetsCore(styleSheets, StyleOrigin.Author);

    internal StyleResolver StyleResolver => _styleResolver;

    private void SetStyleSheetsCore(IEnumerable<StyleSheet> styleSheets, StyleOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(styleSheets);
        var previous = _styleResolver;
        var currentSheets = origin == StyleOrigin.Base
            ? previous.BaseStyleSheets
            : previous.StyleSheets;
        if (ReferenceEquals(currentSheets, styleSheets))
            return;

        if (IsOpen || _isClosing)
            VerifyOwnerThreadCore();
        if (_isTransitioning || IsUpdatingLayout || IsDrawing)
            throw new InvalidOperationException(
                "The UI scene style sheets cannot change while it is transitioning, updating layout, applying styles, or drawing.");

        VerifyNotPreparingStyleSheets();

        var root = _root;
        _isPreparingStyleSheets = true;

        try
        {
            var snapshot = Array.AsReadOnly(styleSheets.ToArray());
            if (currentSheets.SequenceEqual(snapshot))
                return;

            var resolver = origin == StyleOrigin.Base
                ? new StyleResolver(snapshot, previous.StyleSheets)
                : new StyleResolver(previous.BaseStyleSheets, snapshot);
            _styleResolver = resolver;
            root?.InvalidateStyleSubtree();
        }
        finally
        {
            _isPreparingStyleSheets = false;
        }
    }

    private void VerifyNotPreparingStyleSheets()
    {
        if (_isPreparingStyleSheets)
            throw new InvalidOperationException(
                "The UI scene tree and lifecycle cannot change while style sheets are being prepared.");
    }
}
