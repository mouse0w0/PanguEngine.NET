namespace PanguEngine.UI.Selection;

/// <summary>Specifies how many items a selection model can select.</summary>
public enum SelectionMode
{
    /// <summary>Disallows selection.</summary>
    None,
    /// <summary>Allows at most one selected item.</summary>
    Single,
    /// <summary>Allows multiple selected items.</summary>
    Multiple
}
