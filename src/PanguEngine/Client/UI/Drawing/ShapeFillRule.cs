namespace PanguEngine.Client.UI.Drawing;

/// <summary>Determines which areas of a path are filled.</summary>
public enum ShapeFillRule
{
    /// <summary>Fills areas with a nonzero winding number.</summary>
    NonZero,
    /// <summary>Fills areas crossed an odd number of times.</summary>
    EvenOdd
}
