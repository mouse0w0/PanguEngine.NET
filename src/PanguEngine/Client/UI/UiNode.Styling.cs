using PanguEngine.ComponentModel;
using PanguEngine.Client.UI.Drawing;
using PanguEngine.Client.UI.Styling;
using PanguEngine.Collections;
using PanguEngine.Graphics.Text;

namespace PanguEngine.Client.UI;

public abstract partial class UiNode
{
    private IReadOnlyDictionary<Property, IReadOnlyList<UiStyleValueSource>>? _styleSources;
    private readonly ObservableSet<UiPseudoClass> _pseudoClasses = new();

    internal bool IsStyleValid { get; private set; }
    internal bool IsStyleSubtreeValid { get; private set; }
    internal bool IsUpdatingStyles { get; private set; }

    static UiNode()
    {
        UiCssRegistry.RegisterElement<UiNode>("UiNode");
        UiCssRegistry.RegisterProperty<UiNode, string>("font-family", FontFamilyProperty, UiCssFontConverters.ParseFamily);
        UiCssRegistry.RegisterProperty<UiNode, FontWeight>("font-weight", FontWeightProperty, UiCssFontConverters.ParseWeight);
        UiCssRegistry.RegisterProperty<UiNode, FontStyle>("font-style", FontStyleProperty, UiCssFontConverters.ParseStyle);
        UiCssRegistry.RegisterProperty<UiNode, double>("font-size", FontSizeProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("line-height", LineHeightProperty, UiCssFontConverters.ParseLineHeight);
        UiCssRegistry.RegisterProperty<UiNode, Color>("color", ColorProperty, UiCssValueConverters.ParseColor);
        UiCssRegistry.RegisterProperty<UiNode>("font", UiCssFontConverters.ParseFont);
        UiCssRegistry.RegisterProperty<UiNode, double>("width", WidthProperty, UiCssValueConverters.ParseDimension);
        UiCssRegistry.RegisterProperty<UiNode, double>("height", HeightProperty, UiCssValueConverters.ParseDimension);
        UiCssRegistry.RegisterProperty<UiNode, double>("min-width", MinWidthProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("min-height", MinHeightProperty, UiCssValueConverters.ParseLength);
        UiCssRegistry.RegisterProperty<UiNode, double>("max-width", MaxWidthProperty, UiCssValueConverters.ParseMaxDimension);
        UiCssRegistry.RegisterProperty<UiNode, double>("max-height", MaxHeightProperty, UiCssValueConverters.ParseMaxDimension);
        UiCssRegistry.RegisterProperty<UiNode, Thickness>("margin", MarginProperty, UiCssValueConverters.ParseThickness);
        UiCssRegistry.RegisterProperty<UiNode>("margin-top", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Top, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode>("margin-right", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Right, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode>("margin-bottom", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Bottom, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode>("margin-left", value =>
            new[] { UiStyleSetter.CreateEdge(MarginProperty, UiStyleEdge.Left, UiCssValueConverters.ParseLength(value)) });
        UiCssRegistry.RegisterProperty<UiNode, double>("opacity", OpacityProperty, UiCssValueConverters.ParseNumber);
        UiCssRegistry.RegisterProperty<UiNode, HorizontalAlignment>(
            "horizontal-alignment",
            HorizontalAlignmentProperty,
            UiCssValueConverters.ParseHorizontalAlignment);
        UiCssRegistry.RegisterProperty<UiNode, VerticalAlignment>(
            "vertical-alignment",
            VerticalAlignmentProperty,
            UiCssValueConverters.ParseVerticalAlignment);
        UiCssRegistry.RegisterProperty<UiNode, Visibility>(
            "visibility",
            VisibilityProperty,
            UiCssValueConverters.ParseVisibility);
    }

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
    /// Requests for built-in structural pseudo classes have no effect because they are matched from tree structure.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the owning screen does not allow style input changes.
    /// </exception>
    protected void SetPseudoClass(UiPseudoClass pseudoClass, bool active)
    {
        if (pseudoClass.IsStructural)
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
    internal bool HasPseudoClass(UiPseudoClass pseudoClass)
    {
        if (pseudoClass == UiPseudoClass.Root)
            return Parent is null;
        if (pseudoClass == UiPseudoClass.Empty)
            return this is not Parent parent || parent.ReadOnlyChildren.Count == 0;
        if (pseudoClass == UiPseudoClass.FirstChild)
            return Parent is null || ReferenceEquals(Parent.ReadOnlyChildren[0], this);
        if (pseudoClass == UiPseudoClass.LastChild)
            return Parent is null || ReferenceEquals(Parent.ReadOnlyChildren[^1], this);
        if (pseudoClass == UiPseudoClass.OnlyChild)
            return Parent is null || Parent.ReadOnlyChildren.Count == 1;
        return _pseudoClasses.Contains(pseudoClass);
    }

    /// <summary>Gets the node's one-based sibling position, treating a parentless node as a single sibling.</summary>
    internal int GetChildPosition(bool fromEnd)
    {
        if (Parent is null)
            return 1;
        var index = Parent.Children.IndexOf(this);
        return fromEnd ? Parent.ReadOnlyChildren.Count - index : index + 1;
    }

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
                    snapshot.ValidateValues();

                    changes = ComputeStyleChanges(snapshot);
                    CommitStyleValues(snapshot.Values, _styleSources?.Keys ?? []);
                    _styleSources = snapshot.Sources;
                }
                catch
                {
                    InvalidateStyleState();
                    throw;
                }

                var notifyNonInherited = true;
                foreach (var (property, oldValue, newValue) in changes)
                {
                    notifyNonInherited &= ReferenceEquals(GetStyleRoot(), root);
                    if (!property.Inherits &&
                        (!notifyNonInherited || !IsStyleValueCurrent(property, newValue)))
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
        var sources = _styleSources is not null && _styleSources.TryGetValue(property, out var value)
            ? value : Array.Empty<UiStyleValueSource>();
        var isMasked = HasLocalValue(property);
        return isMasked
            ? Array.AsReadOnly(sources.Select(source => source.WithLocalValueMask(true)).ToArray())
            : sources;
    }

    private List<(Property Property, object? Old, object? New)> ComputeStyleChanges(UiStyleSnapshot newSnapshot)
    {
        var changed = new List<(Property, object?, object?)>();
        var keys = new HashSet<Property>();
        if (_styleSources is not null)
        {
            foreach (var property in _styleSources.Keys)
                keys.Add(property);
        }

        foreach (var property in newSnapshot.StyledProperties)
            keys.Add(property);

        foreach (var property in keys)
        {
            if (HasLocalValue(property))
                continue;

            var fallback = property.Inherits &&
                TryGetInheritedValue(property, InheritanceParent, out var inherited)
                    ? inherited : property.DefaultValue;
            var oldValue = GetCommittedStyleValue(property, fallback);
            var newValue = GetSnapshotValue(newSnapshot, property, fallback);
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
        if (root is not null && (resolver.HasRelationships || resolver.HasChildIndexedPseudoClasses ||
                                 resolver.HasEmptyPseudoClass))
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
                                 currentResolver.HasVariables || previousResolver.HasChildIndexedPseudoClasses ||
                                 currentResolver.HasChildIndexedPseudoClasses ||
                                 previousResolver.HasRootPseudoClass || currentResolver.HasRootPseudoClass))
        {
            entries.Add(node);
        }
    }

    private static object? GetSnapshotValue(UiStyleSnapshot snapshot, Property property, object? defaultValue) =>
        snapshot.TryGetBoxedValue(property, out var value) ? value : defaultValue;

    private object? GetCommittedStyleValue(Property property, object? fallback) =>
        TryGetStyleValue(property, out var value) ? value : fallback;

    private bool IsStyleValueCurrent(Property property, object? expectedValue)
    {
        if (HasLocalValue(property))
            return false;
        var currentValue = GetCommittedStyleValue(property, property.DefaultValue);
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
