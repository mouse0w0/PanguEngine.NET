namespace PanguEngine.Client.UI.Controls;

/// <summary>Specifies how a grid row or column obtains its length.</summary>
public enum GridUnitType
{
    /// <summary>Uses a fixed length in logical pixels.</summary>
    Pixel,
    /// <summary>Uses the length required by the contained nodes.</summary>
    Auto,
    /// <summary>Uses a weighted share of the remaining available space.</summary>
    Star
}
