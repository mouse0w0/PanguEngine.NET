namespace PanguEngine.UI.Styling;

/// <summary>Identifies structural changes that can affect selector conditions.</summary>
[Flags]
public enum StyleSelectorDependencies
{
    /// <summary>Indicates that no additional structural dependency is required.</summary>
    None = 0,

    /// <summary>Indicates a dependency on whether the element has a parent.</summary>
    Root = 1,

    /// <summary>Indicates a dependency on sibling positions or counts.</summary>
    ChildPosition = 2,

    /// <summary>Indicates a dependency on the presence of child elements.</summary>
    Children = 4
}
