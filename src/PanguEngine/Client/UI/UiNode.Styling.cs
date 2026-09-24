using PanguEngine.Client.UI.Styling;

namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private UiStyleSnapshot? _styleSnapshot;
    private List<UiPseudoClass>? _pseudoClasses;
    private bool _isStyleRecomputing;
    private bool _hasPendingStyleRecompute;
    private bool _isResolvingStyle;

    /// <summary>
    /// Throws when this node or an ancestor is preparing styles.
    /// </summary>
    internal void VerifyStylePreparationIdle()
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            if (node._isResolvingStyle)
                throw new InvalidOperationException("The node cannot change while its styles are being prepared.");
        }
    }

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

    /// <summary>Adds or removes an active built-in or custom pseudo class used for style matching.</summary>
    /// <param name="pseudoClass">The shared pseudo-class identifier.</param>
    /// <param name="active">Whether the pseudo class should be active on this node.</param>
    /// <remarks>
    /// The collection only changes when the requested membership differs,
    /// and a no-op does not request a style recompute. A pseudo class
    /// mirrors real control state, so a style preparation failure does not roll back the updated collection.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the node resolves its styles or the owning screen does not allow style input changes.
    /// </exception>
    protected void SetPseudoClass(UiPseudoClass pseudoClass, bool active)
    {
        if (active)
        {
            if (_pseudoClasses is not null && _pseudoClasses.Contains(pseudoClass))
                return;
        }
        else if (_pseudoClasses is null || !_pseudoClasses.Contains(pseudoClass))
        {
            return;
        }

        VerifyStyleInputAccess();
        if (active)
            (_pseudoClasses ??= []).Add(pseudoClass);
        else
            _pseudoClasses!.Remove(pseudoClass);

        RecomputeStyle();
    }

    /// <summary>Determines whether the supplied pseudo class is active on this node.</summary>
    /// <param name="pseudoClass">The shared pseudo-class identifier.</param>
    /// <returns>true when the pseudo class is active; otherwise false.</returns>
    internal bool HasPseudoClass(UiPseudoClass pseudoClass) => _pseudoClasses?.Contains(pseudoClass) == true;

    internal void ChangeStyleInput(Action apply, Action rollback)
    {
        apply();
        PreparedStyleBatch prepared;
        try
        {
            prepared = PrepareStyleChange();
        }
        catch
        {
            rollback();
            throw;
        }

        RecomputeStyle(prepared);
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

    internal UiStyleSnapshot ComputeStyleSnapshot(UiStyleResolver resolver) =>
        PrepareStyle(resolver, trackChanges: false).Snapshot;

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
        VerifyStylePreparationIdle();
        var screen = Screen;
        if (screen is null)
            return;
        screen.VerifyTreeMutationAccess();
        if (screen.IsUpdatingLayout)
            throw new InvalidOperationException(
                "A style selector input cannot change while the UI screen updates layout.");
        if (screen.IsApplyingStyleSheets)
            throw new InvalidOperationException(
                "A style selector input cannot change while the UI screen applies style sheets.");
    }

    private void RecomputeStyle(PreparedStyleBatch? firstPrepared = null)
    {
        if (_isStyleRecomputing)
        {
            _hasPendingStyleRecompute = true;
            return;
        }

        _isStyleRecomputing = true;
        var prepared = firstPrepared;
        try
        {
            do
            {
                _hasPendingStyleRecompute = false;
                var currentPrepared = prepared;
                prepared = null;
                RecomputeStyleCore(currentPrepared);
            } while (_hasPendingStyleRecompute);
        }
        finally
        {
            _isStyleRecomputing = false;
        }
    }

    private void RecomputeStyleCore(PreparedStyleBatch? prepared)
    {
        var entry = prepared ?? PrepareStyleChange();
        entry.Commit();
        entry.Notify();
    }

    private PreparedStyleBatch PrepareStyleChange()
    {
        var resolver = GetStyleResolver();
        if (!resolver.HasRelationships && !resolver.HasVariables)
            return new PreparedStyleBatch([PrepareStyle(resolver)]);

        var root = resolver.HasSiblingRelationships ? Parent ?? this : this;
        return PrepareStyleSubtreeBatch([(root, resolver)]);
    }

    internal static void AddRelationshipRefreshEntry(
        List<(UiNode? Root, UiStyleResolver Resolver)> entries,
        UiNode? root,
        UiStyleResolver resolver)
    {
        if (root is not null && resolver.HasRelationships)
            entries.Add((root, resolver));
    }

    internal static void AddSubtreeRefreshEntry(
        List<(UiNode? Root, UiStyleResolver Resolver)> entries,
        UiNode? node,
        UiStyleResolver previousResolver,
        UiStyleResolver currentResolver,
        bool screenChanged = false)
    {
        if (node is not null && (screenChanged || !ReferenceEquals(previousResolver, currentResolver) ||
            previousResolver.HasRelationships ||
            currentResolver.HasRelationships || previousResolver.HasVariables || currentResolver.HasVariables))
        {
            entries.Add((node, currentResolver));
        }
    }

    private PreparedStyle PrepareStyle(UiStyleResolver resolver, bool trackChanges = true,
        UiStyleResolver.VariableContext? variableContext = null)
    {
        var scope = this;
        if (resolver.HasRelationships || resolver.HasVariables)
        {
            while (scope.Parent is { } parent)
                scope = parent;
        }

        var wasResolvingStyle = _isResolvingStyle;
        var wasScopeResolvingStyle = scope._isResolvingStyle;
        _isResolvingStyle = true;
        scope._isResolvingStyle = true;
        try
        {
            var newSnapshot = resolver.Resolve(this, variableContext);
            var changes = trackChanges ? ComputeStyleChanges(_styleSnapshot, newSnapshot) : [];
            changes.Sort((a, b) => a.Property.RegistrationOrder.CompareTo(b.Property.RegistrationOrder));
            return new PreparedStyle(this, newSnapshot, changes);
        }
        finally
        {
            _isResolvingStyle = wasResolvingStyle;
            scope._isResolvingStyle = wasScopeResolvingStyle;
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

    internal void RaiseStyleEffectiveValueChanged<T>(UiProperty<T> property, object? oldValue, object? newValue) =>
        RaisePropertyChanged(
            property,
            oldValue is null ? default! : (T)oldValue,
            newValue is null ? default! : (T)newValue);

    internal static void RecomputeStyleSubtreeBatch(
        IReadOnlyList<(UiNode? Root, UiStyleResolver Resolver)> entries)
    {
        var prepared = PrepareStyleSubtreeBatch(entries);
        prepared.Commit();
        prepared.Notify();
    }

    internal static PreparedStyleBatch PrepareStyleSubtreeBatch(
        IReadOnlyList<(UiNode? Root, UiStyleResolver Resolver)> entries)
    {
        var nodes = new List<(UiNode Node, UiStyleResolver Resolver, bool WasResolvingStyle)>();
        var seen = new HashSet<UiNode>(ReferenceEqualityComparer.Instance);
        foreach (var (root, resolver) in entries)
        {
            if (root is null)
                continue;

            foreach (var node in PreOrderTraversal(root))
            {
                if (!seen.Add(node))
                    continue;
                nodes.Add((node, resolver, node._isResolvingStyle));
            }
        }

        foreach (var (node, _, _) in nodes)
            node._isResolvingStyle = true;

        try
        {
            var prepared = new List<PreparedStyle>(nodes.Count);
            var variableContext = new UiStyleResolver.VariableContext();
            foreach (var (node, resolver, _) in nodes)
                prepared.Add(node.PrepareStyle(resolver, variableContext: variableContext));
            return new PreparedStyleBatch(prepared);
        }
        finally
        {
            foreach (var (node, _, wasResolvingStyle) in nodes)
                node._isResolvingStyle = wasResolvingStyle;
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

        internal void Notify()
        {
            foreach (var entry in entries)
            {
                foreach (var (property, oldValue, newValue) in entry.Changes)
                {
                    if (!entry.Node.IsPreparedValueCurrent(property, newValue))
                        continue;
                    property.RaiseEffectiveValueChanged(entry.Node, oldValue, newValue);
                }
            }
        }
    }

    internal readonly record struct PreparedStyle(
        UiNode Node,
        UiStyleSnapshot Snapshot,
        List<(UiProperty Property, object? Old, object? New)> Changes);
}
