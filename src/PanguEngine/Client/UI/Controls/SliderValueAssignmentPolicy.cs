namespace PanguEngine.Client.UI.Controls;

/// <summary>Specifies how a numeric slider model handles programmatic assignments.</summary>
public enum SliderValueAssignmentPolicy
{
    /// <summary>Restricts the value to the range without applying the step.</summary>
    Clamp,

    /// <summary>Restricts the value to the range and selects the nearest step.</summary>
    Snap,

    /// <summary>Rejects values outside the range or between steps.</summary>
    Reject
}
