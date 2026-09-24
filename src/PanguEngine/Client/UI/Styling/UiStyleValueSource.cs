namespace PanguEngine.Client.UI.Styling;

/// <summary>
/// Describes the winning style declaration for a property component as immutable diagnostic data.
/// </summary>
public sealed class UiStyleValueSource
{
    internal UiStyleValueSource(
        UiStyleSelector selector,
        string? sheetSourceName,
        UiStyleOrigin origin,
        int sheetIndex,
        int ruleIndex,
        int declarationIndex,
        UiStyleSourceLocation? sourceLocation,
        UiStyleEdge? component,
        string? cssPropertyName,
        bool isMaskedByLocalValue)
    {
        Selector = selector;
        SheetSourceName = sheetSourceName;
        Origin = origin;
        SheetIndex = sheetIndex;
        RuleIndex = ruleIndex;
        DeclarationIndex = declarationIndex;
        SourceLocation = sourceLocation;
        Component = component;
        CssPropertyName = cssPropertyName;
        IsMaskedByLocalValue = isMaskedByLocalValue;
    }

    /// <summary>Gets the matching selector branch whose declaration won for this component.</summary>
    public UiStyleSelector Selector { get; }

    /// <summary>Gets the source name of the sheet that owns the winning declaration.</summary>
    public string? SheetSourceName { get; }

    /// <summary>Gets the source group of the winning declaration.</summary>
    public UiStyleOrigin Origin { get; }

    /// <summary>Gets the zero-based index of the owning sheet within its source group.</summary>
    public int SheetIndex { get; }

    /// <summary>Gets the zero-based index of the owning rule within its sheet.</summary>
    public int RuleIndex { get; }

    /// <summary>Gets the zero-based declaration index of the winning setter within its sheet.</summary>
    public int DeclarationIndex { get; }

    /// <summary>Gets the CSS property name span, or the optional rule location for C# declarations.</summary>
    public UiStyleSourceLocation? SourceLocation { get; }

    /// <summary>Gets the thickness edge, or null for a scalar property.</summary>
    public UiStyleEdge? Component { get; }

    /// <summary>Gets the original CSS declaration name, or null for a C# setter.</summary>
    public string? CssPropertyName { get; }

    /// <summary>Gets whether a local value or binding currently masks this style declaration.</summary>
    public bool IsMaskedByLocalValue { get; }

    /// <summary>Gets the reconstructed selector text, e.g. <c>Button.primary:hover</c>.</summary>
    public string SelectorText => Selector.SelectorText;

    internal UiStyleValueSource WithLocalValueMask(bool isMaskedByLocalValue) =>
        IsMaskedByLocalValue == isMaskedByLocalValue
            ? this
            : new UiStyleValueSource(
                Selector,
                SheetSourceName,
                Origin,
                SheetIndex,
                RuleIndex,
                DeclarationIndex,
                SourceLocation,
                Component,
                CssPropertyName,
                isMaskedByLocalValue);
}
