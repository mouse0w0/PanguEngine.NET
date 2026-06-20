namespace PanguEngine.Events;

/// <summary>
/// Defines event listener execution order.
/// </summary>
public enum Order
{
    /// <summary>
    /// Runs before early listeners.
    /// </summary>
    First,

    /// <summary>
    /// Runs before default listeners.
    /// </summary>
    Early,

    /// <summary>
    /// Runs at the default order.
    /// </summary>
    Default,

    /// <summary>
    /// Runs after default listeners.
    /// </summary>
    Late,

    /// <summary>
    /// Runs after late listeners.
    /// </summary>
    Last
}