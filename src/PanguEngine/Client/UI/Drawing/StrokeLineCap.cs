namespace PanguEngine.Client.UI.Drawing;

/// <summary>Specifies the shape of an open stroke endpoint.</summary>
public enum StrokeLineCap
{
    /// <summary>Ends the stroke at its endpoint.</summary>
    Butt,
    /// <summary>Extends the stroke by half its width.</summary>
    Square,
    /// <summary>Ends the stroke with a semicircle.</summary>
    Round
}
