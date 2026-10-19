using PanguEngine.Collections;
using PanguEngine.ComponentModel;
using PanguEngine.Graphics.Text;
using PanguEngine.UI.Drawing;
using PanguEngine.UI.Styling;

namespace PanguEngine.UI;

public abstract partial class UIElement
{
    private IReadOnlyDictionary<Property, IReadOnlyList<StyleValueSource>>? _styleSources;
    private readonly ObservableSet<PseudoClass> _pseudoClasses = new();

    internal bool IsStyleValid { get; private set; }
    internal bool IsStyleSubtreeValid { get; private set; }
    internal bool IsUpdatingStyles { get; private set; }

    static UIElement()
    {
        CssRegistry.RegisterElement<UIElement>("UIElement");
        CssRegistry.RegisterProperty<UIElement, string>("font-family", FontFamilyProperty,
            CssFontConverters.ParseFamily);
        CssRegistry.RegisterProperty<UIElement, FontWeight>("font-weight", FontWeightProperty,
            CssFontConverters.ParseWeight);
        CssRegistry.RegisterProperty<UIElement, FontStyle>("font-style", FontStyleProperty,
            CssFontConverters.ParseStyle);
        CssRegistry.RegisterProperty<UIElement, double>("font-size", FontSizeProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<UIElement, double>("line-height", LineHeightProperty,
            CssFontConverters.ParseLineHeight);
        CssRegistry.RegisterProperty<UIElement, Color>("color", ColorProperty, CssValueConverters.ParseColor);
        CssRegistry.RegisterProperty<UIElement>("font", CssFontConverters.ParseFont);
        CssRegistry.RegisterProperty<UIElement, double>("width", WidthProperty, CssValueConverters.ParseDimension);
        CssRegistry.RegisterProperty<UIElement, double>("height", HeightProperty, CssValueConverters.ParseDimension);
        CssRegistry.RegisterProperty<UIElement, double>("min-width", MinWidthProperty, CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<UIElement, double>("min-height", MinHeightProperty,
            CssValueConverters.ParseLength);
        CssRegistry.RegisterProperty<UIElement, double>("max-width", MaxWidthProperty,
            CssValueConverters.ParseMaxDimension);
        CssRegistry.RegisterProperty<UIElement, double>("max-height", MaxHeightProperty,
            CssValueConverters.ParseMaxDimension);
        CssRegistry.RegisterProperty<UIElement, Thickness>("margin", MarginProperty, CssValueConverters.ParseThickness);
        CssRegistry.RegisterProperty<UIElement>("margin-top", value =>
            new[] { StyleSetter.CreateEdge(MarginProperty, StyleEdge.Top, CssValueConverters.ParseLength(value)) });
        CssRegistry.RegisterProperty<UIElement>("margin-right", value =>
            new[] { StyleSetter.CreateEdge(MarginProperty, StyleEdge.Right, CssValueConverters.ParseLength(value)) });
        CssRegistry.RegisterProperty<UIElement>("margin-bottom", value =>
            new[] { StyleSetter.CreateEdge(MarginProperty, StyleEdge.Bottom, CssValueConverters.ParseLength(value)) });
        CssRegistry.RegisterProperty<UIElement>("margin-left", value =>
            new[] { StyleSetter.CreateEdge(MarginProperty, StyleEdge.Left, CssValueConverters.ParseLength(value)) });
        CssRegistry.RegisterProperty<UIElement, double>("opacity", OpacityProperty, CssValueConverters.ParseNumber);
        CssRegistry.RegisterProperty<UIElement, HorizontalAlignment>(
            "horizontal-alignment",
            HorizontalAlignmentProperty,
            CssValueConverters.ParseHorizontalAlignment);
        CssRegistry.RegisterProperty<UIElement, VerticalAlignment>(
            "vertical-alignment",
            VerticalAlignmentProperty,
            CssValueConverters.ParseVerticalAlignment);
        CssRegistry.RegisterProperty<UIElement, Visibility>(
            "visibility",
            VisibilityProperty,
            CssValueConverters.ParseVisibility);
    }

    internal UIElement GetStyleRoot()
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
                StyleIdentifier.ThrowIfInvalid(value, nameof(StyleId));
            Scene?.VerifyTreeMutationAccess();
            field = value;
            InvalidateStyle();
        }
    }

    /// <summary>Gets the observable set of style classes applied to this element.</summary>
    /// <remarks>
    /// The collection reference is stable. Membership uses Ordinal comparison and names are not validated.
    /// Actual changes invalidate styles before subsequent collection listeners run.
    /// Styles are applied during scene layout.
    /// Callers are responsible for changing classes on the owning UI thread.
    /// </remarks>
    public ObservableSet<string> Classes { get; }

    /// <summary>Adds or removes an active built-in or custom state pseudo class used for style matching.</summary>
    /// <param name="pseudoClass">The shared pseudo-class identifier.</param>
    /// <param name="active">Whether the pseudo class should be active on this node.</param>
    /// <remarks>
    /// The collection only changes when the requested membership differs,
    /// and a no-op does not invalidate styles. A pseudo class mirrors real control state;
    /// its styles are applied during scene layout.
    /// Requests for built-in structural pseudo classes have no effect because they are matched from tree structure.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the owning scene does not allow style input changes.
    /// </exception>
    protected void SetPseudoClass(PseudoClass pseudoClass, bool active)
    {
        if (pseudoClass.IsStructural)
            return;

        var isActive = _pseudoClasses.Contains(pseudoClass);
        if (isActive == active)
            return;

        Scene?.VerifyTreeMutationAccess();
        if (active)
            _pseudoClasses.Add(pseudoClass);
        else
            _pseudoClasses.Remove(pseudoClass);

        InvalidateStyle();
    }

    /// <summary>Determines whether this element matches the supplied structural or state pseudo class.</summary>
    /// <param name="pseudoClass">The shared pseudo-class identifier.</param>
    /// <returns>true when the node matches the pseudo class; otherwise false.</returns>
    internal bool HasPseudoClass(PseudoClass pseudoClass)
    {
        if (pseudoClass == PseudoClass.Root)
            return Parent is null;
        if (pseudoClass == PseudoClass.Empty)
            return this is not Container parent || parent.ReadOnlyChildren.Count == 0;
        if (pseudoClass == PseudoClass.FirstChild)
            return Parent is null || ReferenceEquals(Parent.ReadOnlyChildren[0], this);
        if (pseudoClass == PseudoClass.LastChild)
            return Parent is null || ReferenceEquals(Parent.ReadOnlyChildren[^1], this);
        if (pseudoClass == PseudoClass.OnlyChild)
            return Parent is null || Parent.ReadOnlyChildren.Count == 1;
        return _pseudoClasses.Contains(pseudoClass);
    }

    /// <summary>Gets the element's one-based sibling position, treating a parentless element as a single sibling.</summary>
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
        IReadOnlyList<UIElement> roots)
    {
        foreach (var root in roots)
            root.InvalidateStyleSubtree();
    }

    internal void UpdateStyles() => UpdateStylesCore(this);

    private void UpdateStylesCore(UIElement root)
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
            if (this is Container parent)
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

    private StyleResolver GetStyleResolver() =>
        Scene?.StyleResolver ?? StyleResolver.Default;

    /// <summary>
    /// Gets the winning style declarations for a property's components, including local value or binding masks.
    /// </summary>
    /// <param name="property">The property whose style source to query.</param>
    /// <returns>The immutable sources in component order, or an empty list when no style declaration applies.</returns>
    /// <remarks>Returns only previously applied sources, even when styles are pending or have never been applied.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be stored on this node.</exception>
    public IReadOnlyList<StyleValueSource> GetStyleValueSources(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);
        property.VerifyOwner(this);
        if (property.IsDirect)
            return Array.Empty<StyleValueSource>();
        var sources = _styleSources is not null && _styleSources.TryGetValue(property, out var value)
            ? value
            : Array.Empty<StyleValueSource>();
        var isMasked = HasLocalValue(property);
        return isMasked
            ? Array.AsReadOnly(sources.Select(source => source.WithLocalValueMask(true)).ToArray())
            : sources;
    }

    private List<(Property Property, object? Old, object? New)> ComputeStyleChanges(StyleSnapshot newSnapshot)
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
                ? inherited
                : property.DefaultValue;
            var oldValue = GetCommittedStyleValue(property, fallback);
            var newValue = GetSnapshotValue(newSnapshot, property, fallback);
            if (!property.AreEqual(oldValue, newValue))
                changed.Add((property, oldValue, newValue));
        }

        return changed;
    }

    internal static void AddRelationshipRefreshEntry(
        List<UIElement> entries,
        UIElement? root,
        StyleResolver resolver)
    {
        if (root is not null && (resolver.HasRelationships || resolver.HasChildIndexedPseudoClasses ||
                                 resolver.HasEmptyPseudoClass))
            entries.Add(root);
    }

    internal static void AddSubtreeRefreshEntry(
        List<UIElement> entries,
        UIElement? node,
        StyleResolver previousResolver,
        StyleResolver currentResolver,
        bool sceneChanged = false)
    {
        if (node is not null && (sceneChanged || !ReferenceEquals(previousResolver, currentResolver) ||
                                 previousResolver.HasRelationships ||
                                 currentResolver.HasRelationships || previousResolver.HasVariables ||
                                 currentResolver.HasVariables || previousResolver.HasChildIndexedPseudoClasses ||
                                 currentResolver.HasChildIndexedPseudoClasses ||
                                 previousResolver.HasRootPseudoClass || currentResolver.HasRootPseudoClass))
        {
            entries.Add(node);
        }
    }

    private static object? GetSnapshotValue(StyleSnapshot snapshot, Property property, object? defaultValue) =>
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

    private static IEnumerable<UIElement> PreOrderTraversal(UIElement root)
    {
        yield return root;
        if (root is Container parent)
        {
            foreach (var child in parent.ReadOnlyChildren)
            {
                foreach (var descendant in PreOrderTraversal(child))
                    yield return descendant;
            }
        }
    }
}