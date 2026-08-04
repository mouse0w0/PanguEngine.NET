namespace PanguEngine.Client.UI;

/// <summary>
/// Describes the UI work that a property change may invalidate.
/// </summary>
[Flags]
public enum UiPropertyInvalidation
{
    /// <summary>No known UI work is invalidated.</summary>
    None = 0,

    /// <summary>Measurement may need to be recalculated.</summary>
    Measure = 1 << 0,

    /// <summary>Arrangement may need to be recalculated.</summary>
    Arrange = 1 << 1,

    /// <summary>Rendering may need to be recalculated.</summary>
    Render = 1 << 2,

    /// <summary>Input eligibility or hit testing may need to be recalculated.</summary>
    Input = 1 << 3
}
