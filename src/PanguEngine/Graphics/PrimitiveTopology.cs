namespace PanguEngine.Graphics;

/// <summary>
/// Identifies the primitive topology used by a graphics pipeline.
/// </summary>
public enum PrimitiveTopology
{
    /// <summary>
    /// Independent point list topology.
    /// </summary>
    PointList,

    /// <summary>
    /// Independent line list topology.
    /// </summary>
    LineList,

    /// <summary>
    /// Connected line strip topology.
    /// </summary>
    LineStrip,

    /// <summary>
    /// Independent triangle list topology.
    /// </summary>
    TriangleList,

    /// <summary>
    /// Connected triangle strip topology.
    /// </summary>
    TriangleStrip,

    /// <summary>
    /// Connected triangle fan topology.
    /// </summary>
    TriangleFan
}