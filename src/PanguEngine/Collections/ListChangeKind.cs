namespace PanguEngine.Collections;

/// <summary>
/// Identifies the kind of change reported by an observable list.
/// </summary>
public enum ListChangeKind
{
    /// <summary>
    /// One or more items were added.
    /// </summary>
    Add,

    /// <summary>
    /// One or more items were removed.
    /// </summary>
    Remove,

    /// <summary>
    /// One or more items were replaced.
    /// </summary>
    Replace,

    /// <summary>
    /// Existing items changed positions.
    /// </summary>
    Reorder
}
