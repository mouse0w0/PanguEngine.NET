using PanguEngine.UI.Styling;

namespace PanguEngine.UI;

public partial class Screen
{
    /// <summary>
    /// Gets the immutable snapshot of base style sheets for this screen.
    /// </summary>
    /// <remarks>
    /// New screens include the engine default sheet. Use <see cref="SetBaseStyleSheets"/> to replace
    /// this source group, including replacing it with an empty collection.
    /// </remarks>
    public IReadOnlyList<StyleSheet> BaseStyleSheets
    {
        get
        {
            lock (_stateSync)
                return _styleResolver.BaseStyleSheets;
        }
    }

    /// <summary>Gets the immutable snapshot of application style sheets for this screen.</summary>
    /// <remarks>Application styles take precedence over base styles. This collection is empty on new screens.</remarks>
    public IReadOnlyList<StyleSheet> StyleSheets
    {
        get
        {
            lock (_stateSync)
                return _styleResolver.StyleSheets;
        }
    }

    /// <summary>Replaces the base style sheets while preserving the application style sheets.</summary>
    /// <param name="styleSheets">The ordered style sheets to copy into the new base snapshot.</param>
    /// <remarks>Replaces the configuration immediately. Node styles are applied during screen layout.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="styleSheets"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown on the wrong owner thread or during a conflicting screen operation.</exception>
    public void SetBaseStyleSheets(IEnumerable<StyleSheet> styleSheets) =>
        SetStyleSheetsCore(styleSheets, StyleOrigin.Base);

    /// <summary>Replaces the application style sheets while preserving the base style sheets.</summary>
    /// <param name="styleSheets">The ordered style sheets to copy into the new author snapshot.</param>
    /// <remarks>Replaces the configuration immediately. Node styles are applied during screen layout.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="styleSheets"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown on the wrong owner thread or during a conflicting screen operation.</exception>
    public void SetStyleSheets(IEnumerable<StyleSheet> styleSheets) =>
        SetStyleSheetsCore(styleSheets, StyleOrigin.Author);

    internal StyleResolver StyleResolver
    {
        get
        {
            lock (_stateSync)
                return _styleResolver;
        }
    }

    private void SetStyleSheetsCore(IEnumerable<StyleSheet> styleSheets, StyleOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(styleSheets);
        StyleResolver previous;
        IReadOnlyList<StyleSheet> currentSheets;
        Node? root;
        lock (_stateSync)
        {
            previous = _styleResolver;
            currentSheets = origin == StyleOrigin.Base
                ? previous.BaseStyleSheets
                : previous.StyleSheets;
            if (ReferenceEquals(currentSheets, styleSheets))
                return;

            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
            if (_isTransitioning || IsUpdatingLayout || _isDrawing)
                throw new InvalidOperationException(
                    "The UI screen style sheets cannot change while it is transitioning, updating layout, applying styles, or drawing.");

            VerifyNotPreparingStyleSheets();

            root = _root;
            _isPreparingStyleSheets = true;
        }

        try
        {
            var snapshot = Array.AsReadOnly(styleSheets.ToArray());
            if (currentSheets.SequenceEqual(snapshot))
                return;

            var resolver = origin == StyleOrigin.Base
                ? new StyleResolver(snapshot, previous.StyleSheets)
                : new StyleResolver(previous.BaseStyleSheets, snapshot);
            lock (_stateSync)
                _styleResolver = resolver;
            root?.InvalidateStyleSubtree();
        }
        finally
        {
            lock (_stateSync)
            {
                _isPreparingStyleSheets = false;
            }
        }
    }

    private void VerifyNotPreparingStyleSheets()
    {
        if (_isPreparingStyleSheets)
            throw new InvalidOperationException(
                "The UI screen tree and lifecycle cannot change while style sheets are being prepared.");
    }
}
