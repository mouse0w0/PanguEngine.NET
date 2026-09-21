using System.Runtime.ExceptionServices;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private UiStyleSnapshot? _styleSnapshot;
    private bool _isStyleRecomputing;
    private bool _hasPendingStyleRecompute;
    private bool _isResolvingStyle;

    /// <summary>Gets or sets the optional ASCII identifier used by style selectors, or null for no id.</summary>
    /// <remarks>The value must be a valid ASCII identifier; selectors match by Ordinal equality. Assigning the same value is a no-op.</remarks>
    public string? StyleId
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
                return;
            if (value is not null)
                UiStyleIdentifier.ThrowIfInvalid(value, nameof(StyleId));
            VerifyStyleInputAccess();
            var previous = field;
            ChangeStyleInput(
                () => field = value,
                () => field = previous);
        }
    }

    /// <summary>Gets the read-only collection of style classes applied to this node.</summary>
    public UiStyleClassCollection Classes { get; }

    /// <summary>Reports the currently active framework pseudo states used for style matching.</summary>
    /// <returns>The composable pseudo states active on this node.</returns>
    /// <remarks>Custom controls may override this to expose additional built-in states while preserving framework semantics.</remarks>
    protected virtual UiPseudoStates GetStylePseudoStates()
    {
        var states = UiPseudoStates.None;
        if (IsHovered) states |= UiPseudoStates.Hovered;
        if (IsFocused) states |= UiPseudoStates.Focused;
        if (!IsEnabled) states |= UiPseudoStates.Disabled;
        return states;
    }

    /// <summary>Resolves the pseudo states for selector matching without exposing the protected extension point.</summary>
    /// <returns>The composable pseudo states active on this node.</returns>
    internal UiPseudoStates GetStylePseudoStatesForMatching() => GetStylePseudoStates();

    internal void ChangeStyleInput(Action apply, Action rollback)
    {
        apply();
        PreparedStyle prepared;
        try
        {
            prepared = PrepareStyle(GetStyleResolver());
        }
        catch
        {
            rollback();
            throw;
        }

        RecomputeStyle(prepared);
    }

    /// <summary>
    /// Refreshes style matching after a custom control changes pseudo-state information returned by
    /// <see cref="GetStylePseudoStates"/>.
    /// </summary>
    protected void RefreshStylePseudoStates()
    {
        VerifyStyleInputAccess();
        RecomputeStyle();
    }

    private UiStyleResolver GetStyleResolver() =>
        Screen?.StyleResolver ?? UiStyleResolver.Default;

    /// <summary>
    /// Gets the winning style declarations for a property's components, including local value or binding masks.
    /// </summary>
    /// <param name="property">The property whose style source to query.</param>
    /// <returns>The immutable sources in component order, or an empty list when no style declaration applies.</returns>
    /// <remarks>During style resolution, returns the last committed source without starting another resolution.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be stored on this node.</exception>
    public IReadOnlyList<UiStyleValueSource> GetStyleValueSources(UiProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        EnsureStyleSnapshot();
        var sources = _styleSnapshot?.GetSources(property) ?? Array.Empty<UiStyleValueSource>();
        var isMasked = _localValues is not null && _localValues.ContainsKey(property);
        return isMasked
            ? Array.AsReadOnly(sources.Select(source => source.WithLocalValueMask(true)).ToArray())
            : sources;
    }

    private void EnsureStyleSnapshot()
    {
        if (_styleSnapshot is null && !_isResolvingStyle)
            _styleSnapshot = ComputeStyleSnapshot();
    }

    private UiStyleSnapshot ComputeStyleSnapshot() =>
        ComputeStyleSnapshot(GetStyleResolver());

    internal UiStyleSnapshot ComputeStyleSnapshot(UiStyleResolver resolver)
    {
        var wasResolvingStyle = _isResolvingStyle;
        _isResolvingStyle = true;
        try
        {
            return resolver.Resolve(this);
        }
        finally
        {
            _isResolvingStyle = wasResolvingStyle;
        }
    }

    internal void CommitStyleSnapshot(UiStyleSnapshot snapshot) =>
        _styleSnapshot = snapshot;

    private List<(UiProperty Property, object? Old, object? New)> ComputeStyleChanges(
        UiStyleSnapshot? oldSnapshot,
        UiStyleSnapshot newSnapshot)
    {
        var changed = new List<(UiProperty, object?, object?)>();
        var keys = new HashSet<UiProperty>();
        if (oldSnapshot is not null)
        {
            foreach (var property in oldSnapshot.StyledProperties)
                keys.Add(property);
        }
        foreach (var property in newSnapshot.StyledProperties)
            keys.Add(property);

        foreach (var property in keys)
        {
            if (_localValues is not null && _localValues.ContainsKey(property))
                continue;

            var oldValue = oldSnapshot is null
                ? property.DefaultValue
                : GetSnapshotValue(oldSnapshot, property, property.DefaultValue);
            var newValue = GetSnapshotValue(newSnapshot, property, property.DefaultValue);
            if (!property.AreEqual(oldValue, newValue))
                changed.Add((property, oldValue, newValue));
        }

        return changed;
    }

    internal void VerifyStyleInputAccess()
    {
        if (_isResolvingStyle)
            throw new InvalidOperationException("A style selector input cannot change while the node resolves its styles.");
        var screen = Screen;
        if (screen is null)
            return;
        screen.VerifyTreeMutationAccess();
        if (screen.IsUpdatingLayout)
            throw new InvalidOperationException("A style selector input cannot change while the UI screen updates layout.");
        if (screen.IsApplyingStyleSheets)
            throw new InvalidOperationException("A style selector input cannot change while the UI screen applies style sheets.");
    }

    private void RecomputeStyle(PreparedStyle? firstPrepared = null)
    {
        if (_isStyleRecomputing)
        {
            _hasPendingStyleRecompute = true;
            return;
        }

        _isStyleRecomputing = true;
        var errors = new List<Exception>();
        var prepared = firstPrepared;
        try
        {
            do
            {
                _hasPendingStyleRecompute = false;
                try
                {
                    var currentPrepared = prepared;
                    prepared = null;
                    RecomputeStyleCore(currentPrepared);
                }
                catch (Exception exception)
                {
                    AddErrors(errors, exception);
                }
            }
            while (_hasPendingStyleRecompute);
        }
        finally
        {
            _isStyleRecomputing = false;
        }

        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private void RecomputeStyleCore(PreparedStyle? prepared)
    {
        PreparedStyle entry;
        try
        {
            entry = prepared ?? PrepareStyle(GetStyleResolver());
        }
        catch
        {
            _styleSnapshot = null;
            throw;
        }

        _styleSnapshot = entry.Snapshot;

        if (entry.Changes.Count == 0)
            return;

        var errors = new List<Exception>();
        foreach (var (property, oldValue, newValue) in entry.Changes)
        {
            if (!IsPreparedValueCurrent(property, newValue))
                continue;
            try
            {
                property.RaiseEffectiveValueChanged(this, oldValue, newValue);
            }
            catch (Exception exception)
            {
                AddErrors(errors, exception);
            }
        }

        if (errors.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException(errors);
    }

    private PreparedStyle PrepareStyle(UiStyleResolver resolver)
    {
        var wasResolvingStyle = _isResolvingStyle;
        _isResolvingStyle = true;
        try
        {
            var newSnapshot = ComputeStyleSnapshot(resolver);
            var changes = ComputeStyleChanges(_styleSnapshot, newSnapshot);
            changes.Sort((a, b) => a.Property.RegistrationOrder.CompareTo(b.Property.RegistrationOrder));
            return new PreparedStyle(this, newSnapshot, changes);
        }
        finally
        {
            _isResolvingStyle = wasResolvingStyle;
        }
    }

    private static object? GetSnapshotValue(UiStyleSnapshot snapshot, UiProperty property, object? defaultValue) =>
        snapshot.TryGetBoxedValue(property, out var value) ? value : defaultValue;

    private bool IsPreparedValueCurrent(UiProperty property, object? expectedValue)
    {
        if (_localValues is not null && _localValues.ContainsKey(property))
            return false;
        var currentValue = _styleSnapshot is null
            ? property.DefaultValue
            : GetSnapshotValue(_styleSnapshot, property, property.DefaultValue);
        return property.AreEqual(currentValue, expectedValue);
    }

    private static bool IsPseudoStateProperty(UiProperty property) =>
        ReferenceEquals(property, IsHoveredProperty) ||
        ReferenceEquals(property, IsFocusedProperty) ||
        ReferenceEquals(property, IsEnabledProperty) ||
        ReferenceEquals(property, Control.IsPressedProperty);

    internal void RaiseStyleEffectiveValueChanged<T>(UiProperty<T> property, object? oldValue, object? newValue) =>
        OnPropertyChanged(new UiPropertyChangedEventArgs<T>(
            property,
            oldValue is null ? default! : (T)oldValue,
            newValue is null ? default! : (T)newValue));

    internal static void RecomputeStyleSubtreeBatch(
        IReadOnlyList<(UiNode? Root, UiStyleResolver Resolver)> entries,
        List<Exception> errors)
    {
        var prepared = PrepareStyleSubtreeBatch(entries);
        prepared.Commit();
        prepared.Notify(errors);
    }

    internal static PreparedStyleBatch PrepareStyleSubtreeBatch(
        IReadOnlyList<(UiNode? Root, UiStyleResolver Resolver)> entries)
    {
        var nodes = new List<(UiNode Node, UiStyleResolver Resolver, bool WasResolvingStyle)>();
        foreach (var (root, resolver) in entries)
        {
            if (root is null)
                continue;

            foreach (var node in PreOrderTraversal(root))
                nodes.Add((node, resolver, node._isResolvingStyle));
        }

        foreach (var (node, _, _) in nodes)
            node._isResolvingStyle = true;

        try
        {
            var prepared = new List<PreparedStyle>(nodes.Count);
            foreach (var (node, resolver, _) in nodes)
                prepared.Add(node.PrepareStyle(resolver));
            return new PreparedStyleBatch(prepared);
        }
        finally
        {
            foreach (var (node, _, wasResolvingStyle) in nodes)
                node._isResolvingStyle = wasResolvingStyle;
        }
    }

    internal static void ClearStyleSubtreeBatch(
        IReadOnlyList<(UiNode? Root, UiStyleResolver Resolver)> entries)
    {
        foreach (var (root, _) in entries)
        {
            if (root is null)
                continue;
            foreach (var node in PreOrderTraversal(root))
                node._styleSnapshot = null;
        }
    }

    private static IEnumerable<UiNode> PreOrderTraversal(UiNode root)
    {
        yield return root;
        if (root is Parent parent)
        {
            foreach (var child in parent.Children)
            {
                foreach (var descendant in PreOrderTraversal(child))
                    yield return descendant;
            }
        }
    }

    internal sealed class PreparedStyleBatch(IReadOnlyList<PreparedStyle> entries)
    {
        internal void Commit()
        {
            foreach (var entry in entries)
                entry.Node.CommitStyleSnapshot(entry.Snapshot);
        }

        internal void Notify(List<Exception> errors)
        {
            foreach (var entry in entries)
            {
                foreach (var (property, oldValue, newValue) in entry.Changes)
                {
                    if (!entry.Node.IsPreparedValueCurrent(property, newValue))
                        continue;
                    try
                    {
                        property.RaiseEffectiveValueChanged(entry.Node, oldValue, newValue);
                    }
                    catch (Exception exception)
                    {
                        AddErrors(errors, exception);
                    }
                }
            }
        }
    }

    internal readonly record struct PreparedStyle(
        UiNode Node,
        UiStyleSnapshot Snapshot,
        List<(UiProperty Property, object? Old, object? New)> Changes);
}
