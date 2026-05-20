namespace PanguEngine.Graphics;

/// <summary>
/// Identifies how vertex input advances between shader invocations.
/// </summary>
public enum VertexInputRate
{
    /// <summary>
    /// Vertex input advances per vertex.
    /// </summary>
    Vertex,

    /// <summary>
    /// Vertex input advances per instance.
    /// </summary>
    Instance
}