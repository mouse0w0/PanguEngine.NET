namespace PanguEngine.Client.UI.Drawing;

/// <summary>Specifies how adjacent stroke segments join.</summary>
public enum StrokeLineJoin
{
    /// <summary>Extends the outside edges to their intersection within the miter limit.</summary>
    Miter,
    /// <summary>Connects the outside edges with a straight bevel.</summary>
    Bevel,
    /// <summary>Connects the outside edges with a circular arc.</summary>
    Round
}
