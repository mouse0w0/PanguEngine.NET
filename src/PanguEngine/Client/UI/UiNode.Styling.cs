using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Collections;

namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private UiStyleSnapshot? _styleSnapshot;
    private readonly ObservableSet<UiPseudoClass> _pseudoClasses = new();

    internal bool IsStyleValid { get; private set; }
    internal bool IsStyleSubtreeValid { get; private set; }
    internal bool IsUpdatingStyles { get; private set; }

    internal UiNode GetStyleRoot()
    {
        var root = this;
        while (root.Parent is { } parent)
            root = parent;
        return root;
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
            Screen?.VerifyTreeMutationAccess();
            field = value;
            InvalidateStyle();
        }
    }

    /// <summary>Gets the observable set of style classes applied to this node.</summary>
    /// <remarks>
    /// The collection reference is stable. Membership uses Ordinal comparison and names are not validated.
    /// Actual changes invalidate styles before subsequent collection listeners run.
    /// Styles are applied during screen layout.
    /// Callers are responsible for changing classes on the owning UI thread.
    /// </remarks>
    public ObservableSet<string> Classes { get; }

    /// <summary>Adds or removes an active built-in or custom state pseudo class used for style matching.</summary>
    /// <param name="pseudoClass">The shared pseudo-class identifier.</param>
    /// <param name="active">Whether the pseudo class should be active on this node.</param>
    /// <remarks>
    /// The collection only changes when the requested membership differs,
    /// and a no-op does not invalidate styles. A pseudo class mirrors real control state;
    /// its styles are applied during screen layout.
    /// Requests for <see cref="UiPseudoClass.Root"/> have no effect because root matching depends on tree structure.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the owning screen does not allow style input changes.
    /// </exception>
    protected void SetPseudoClass(UiPseudoClass pseudoClass, bool active)
    {
        if (pseudoClass == UiPseudoClass.Root)
            return;

        var isActive = _pseudoClasses.Contains(pseudoClass);
        if (isActive == active)
            return;

        Screen?.VerifyTreeMutationAccess();
        if (active)
            _pseudoClasses.Add(pseudoClass);
        else
            _pseudoClasses.Remove(pseudoClass);

        InvalidateStyle();
    }

    /// <summary>Determines whether this node matches the supplied structural or state pseudo class.</summary>
    /// <param name="pseudoClass">The shared pseudo-class identifier.</param>
    /// <returns>true when the node matches the pseudo class; otherwise false.</returns>
    internal bool HasPseudoClass(UiPseudoClass pseudoClass) =>
        pseudoClass == UiPseudoClass.Root ? Parent is null : _pseudoClasses.Contains(pseudoClass);

    internal void InvalidateStyle()
    {
        var resolver = GetStyleResolver();
        if (!resolver.HasRelationships && !resolver.HasVariables)
        {
            InvalidateStyleState();
            return;
        }

        var root = resolver.HasSiblingRelationships ? Parent ?? this : this;
        root.InvalidateStyleSubtree();
    }

    private void InvalidateStyleState()
    {
        IsStyleValid = false;
        for (var node = this; node is not null; node = node.Parent)
            node.IsStyleSubtreeValid = false;
    }

    internal void InvalidateStyleSubtree()
    {
        foreach (var node in PreOrderTraversal(this))
            node.InvalidateStyleState();
    }

    internal static void InvalidateStyleSubtreeBatch(
        IReadOnlyList<UiNode> roots)
    {
        foreach (var root in roots)
            root.InvalidateStyleSubtree();
    }

    internal void UpdateStyles() => UpdateStylesCore(this);

    private void UpdateStylesCore(UiNode root)
    {
        if (IsUpdatingStyles)
            throw new InvalidOperationException("Styles are already being updated for this node.");
        if (IsStyleSubtreeValid || !ReferenceEquals(GetStyleRoot(), root))
            return;
        IsUpdatingStyles = true;
        IsStyleSubtreeValid = true;
        try
        {
            if (!IsStyleValid)
            {
                IsStyleValid = true;
                List<(Property Property, object? Old, object? New)> changes;
                try
                {
                    var snapshot = GetStyleResolver().Resolve(this);
                    changes = ComputeStyleChanges(_styleSnapshot, snapshot);
                    changes.Sort((a, b) => a.Property.RegistrationOrder.CompareTo(b.Property.RegistrationOrder));
                    _styleSnapshot = snapshot;
                }
                catch
                {
                    InvalidateStyleState();
                    throw;
                }

                foreach (var (property, oldValue, newValue) in changes)
                {
                    if (!ReferenceEquals(GetStyleRoot(), root))
                        break;
                    if (!IsStyleValueCurrent(property, newValue))
                        continue;
                    property.RaiseEffectiveValueChanged(this, oldValue, newValue);
                }
            }

            if (!ReferenceEquals(GetStyleRoot(), root))
                return;
            if (this is Parent parent)
            {
                foreach (var child in parent.ReadOnlyChildren.ToArray())
                {
                    if (ReferenceEquals(child.Parent, this))
                        child.UpdateStylesCore(root);
                }
            }
        }
        catch
        {
            IsStyleSubtreeValid = false;
            throw;
        }
        finally
        {
            IsUpdatingStyles = false;
        }
    }

    private UiStyleResolver GetStyleResolver() =>
        Screen?.StyleResolver ?? UiStyleResolver.Default;

    /// <summary>
    /// Gets the winning style declarations for a property's components, including local value or binding masks.
    /// </summary>
    /// <param name="property">The property whose style source to query.</param>
    /// <returns>The immutable sources in component order, or an empty list when no style declaration applies.</returns>
    /// <remarks>Returns only previously applied sources, even when styles are pending or have never been applied.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be stored on this node.</exception>
    public IReadOnlyList<UiStyleValueSource> GetStyleValueSources(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        if (property.IsDirect)
            return Array.Empty<UiStyleValueSource>();
        var sources = _styleSnapshot?.GetSources(property) ?? Array.Empty<UiStyleValueSource>();
        var isMasked = HasLocalValue(property);
        return isMasked
            ? Array.AsReadOnly(sources.Select(source => source.WithLocalValueMask(true)).ToArray())
            : sources;
    }

    private List<(Property Property, object? Old, object? New)> ComputeStyleChanges(
        UiStyleSnapshot? oldSnapshot,
        UiStyleSnapshot newSnapshot)
    {
        var changed = new List<(Property, object?, object?)>();
        var keys = new HashSet<Property>();
        if (oldSnapshot is not null)
        {
            foreach (var property in oldSnapshot.StyledProperties)
                keys.Add(property);
        }

        foreach (var property in newSnapshot.StyledProperties)
            keys.Add(property);

        foreach (var property in keys)
        {
            if (HasLocalValue(property))
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

    internal static void AddRelationshipRefreshEntry(
        List<UiNode> entries,
        UiNode? root,
        UiStyleResolver resolver)
    {
        if (root is not null && resolver.HasRelationships)
            entries.Add(root);
    }

    internal static void AddSubtreeRefreshEntry(
        List<UiNode> entries,
        UiNode? node,
        UiStyleResolver previousResolver,
        UiStyleResolver currentResolver,
        bool screenChanged = false)
    {
        if (node is not null && (screenChanged || !ReferenceEquals(previousResolver, currentResolver) ||
                                 previousResolver.HasRelationships ||
                                 currentResolver.HasRelationships || previousResolver.HasVariables ||
                                 currentResolver.HasVariables ||
                                 previousResolver.HasRootPseudoClass || currentResolver.HasRootPseudoClass))
        {
            entries.Add(node);
        }
    }

    private static object? GetSnapshotValue(UiStyleSnapshot snapshot, Property property, object? defaultValue) =>
        snapshot.TryGetBoxedValue(property, out var value) ? value : defaultValue;

    private bool IsStyleValueCurrent(Property property, object? expectedValue)
    {
        if (HasLocalValue(property))
            return false;
        var currentValue = _styleSnapshot is null
            ? property.DefaultValue
            : GetSnapshotValue(_styleSnapshot, property, property.DefaultValue);
        return property.AreEqual(currentValue, expectedValue);
    }

    private static IEnumerable<UiNode> PreOrderTraversal(UiNode root)
    {
        yield return root;
        if (root is Parent parent)
        {
            foreach (var child in parent.ReadOnlyChildren)
            {
                foreach (var descendant in PreOrderTraversal(child))
                    yield return descendant;
            }
        }
    }
}
