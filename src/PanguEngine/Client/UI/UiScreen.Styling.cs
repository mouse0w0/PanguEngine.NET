using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI;

public partial class UiScreen
{
    /// <summary>
    /// Gets the immutable snapshot of base style sheets for this screen.
    /// </summary>
    /// <remarks>
    /// New screens include the engine default sheet. Use <see cref="SetBaseStyleSheets"/> to replace
    /// this source group, including replacing it with an empty collection.
    /// </remarks>
    public IReadOnlyList<UiStyleSheet> BaseStyleSheets
    {
        get
        {
            lock (_stateSync)
                return _styleResolver.BaseStyleSheets;
        }
    }

    /// <summary>Gets the immutable snapshot of application style sheets for this screen.</summary>
    /// <remarks>Application styles take precedence over base styles. This collection is empty on new screens.</remarks>
    public IReadOnlyList<UiStyleSheet> StyleSheets
    {
        get
        {
            lock (_stateSync)
                return _styleResolver.StyleSheets;
        }
    }

    /// <summary>Replaces the base style sheets while preserving the application style sheets.</summary>
    /// <param name="styleSheets">The ordered style sheets to copy into the new base snapshot.</param>
    /// <remarks>Preparation failure preserves the previous style sheets and node styles.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="styleSheets"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown on the wrong owner thread or during a conflicting screen operation.</exception>
    public void SetBaseStyleSheets(IEnumerable<UiStyleSheet> styleSheets) =>
        SetStyleSheetsCore(styleSheets, UiStyleOrigin.Base);

    /// <summary>Replaces the application style sheets while preserving the base style sheets.</summary>
    /// <param name="styleSheets">The ordered style sheets to copy into the new author snapshot.</param>
    /// <remarks>Preparation failure preserves the previous style sheets and node styles.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="styleSheets"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown on the wrong owner thread or during a conflicting screen operation.</exception>
    public void SetStyleSheets(IEnumerable<UiStyleSheet> styleSheets) =>
        SetStyleSheetsCore(styleSheets, UiStyleOrigin.Author);

    internal UiStyleResolver StyleResolver
    {
        get
        {
            lock (_stateSync)
                return _styleResolver;
        }
    }

    private void SetStyleSheetsCore(IEnumerable<UiStyleSheet> styleSheets, UiStyleOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(styleSheets);
        UiStyleResolver previous;
        IReadOnlyList<UiStyleSheet> currentSheets;
        UiNode? root;
        lock (_stateSync)
        {
            previous = _styleResolver;
            currentSheets = origin == UiStyleOrigin.Base
                ? previous.BaseStyleSheets
                : previous.StyleSheets;
            if (ReferenceEquals(currentSheets, styleSheets))
                return;

            if (_ownerThreadId is not null)
                VerifyOwnerThreadCore();
            if (_isTransitioning || IsUpdatingLayout || _isDrawing)
                throw new InvalidOperationException(
                    "The UI screen style sheets cannot change while it is transitioning, updating layout, or drawing.");

            if (_isApplyingStyleSheets)
                throw new InvalidOperationException("The UI screen style sheets are already being applied.");

            root = _root;
            _isApplyingStyleSheets = true;
            _isPreparingStyleSheets = true;
        }

        var errors = new List<Exception>();
        try
        {
            var snapshot = Array.AsReadOnly(styleSheets.ToArray());
            if (currentSheets.SequenceEqual(snapshot))
                return;

            var resolver = origin == UiStyleOrigin.Base
                ? new UiStyleResolver(snapshot, previous.StyleSheets)
                : new UiStyleResolver(previous.BaseStyleSheets, snapshot);
            var prepared = UiNode.PrepareStyleSubtreeBatch(
                new (UiNode? Root, UiStyleResolver Resolver)[] { (root, resolver) });
            lock (_stateSync)
                _styleResolver = resolver;
            prepared.Commit();
            lock (_stateSync)
                _isPreparingStyleSheets = false;
            prepared.Notify(errors);
        }
        finally
        {
            lock (_stateSync)
            {
                _isPreparingStyleSheets = false;
                _isApplyingStyleSheets = false;
            }
        }

        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private void VerifyNotPreparingStyleSheets()
    {
        if (_isPreparingStyleSheets)
            throw new InvalidOperationException("The UI screen tree and lifecycle cannot change while style sheets are being prepared.");
    }
}
