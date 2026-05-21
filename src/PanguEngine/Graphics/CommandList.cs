namespace PanguEngine.Graphics;

/// <summary>
/// Represents a backend-independent graphics command recording object for the active frame.
/// </summary>
public abstract class CommandList
{
    /// <summary>
    /// Begins command recording.
    /// </summary>
    public abstract void Begin();

    /// <summary>
    /// Begins color rendering for the active frame target.
    /// </summary>
    /// <param name="description">The rendering description.</param>
    public abstract void BeginRendering(in RenderingDescription description);

    /// <summary>
    /// Sets the active viewport.
    /// </summary>
    /// <param name="x">The viewport x coordinate.</param>
    /// <param name="y">The viewport y coordinate.</param>
    /// <param name="width">The viewport width.</param>
    /// <param name="height">The viewport height.</param>
    public abstract void SetViewport(float x, float y, float width, float height);

    /// <summary>
    /// Sets the active scissor rectangle.
    /// </summary>
    /// <param name="x">The scissor x coordinate.</param>
    /// <param name="y">The scissor y coordinate.</param>
    /// <param name="width">The scissor width.</param>
    /// <param name="height">The scissor height.</param>
    public abstract void SetScissor(int x, int y, uint width, uint height);

    /// <summary>
    /// Sets the active graphics pipeline.
    /// </summary>
    /// <param name="pipeline">The graphics pipeline.</param>
    public abstract void SetGraphicsPipeline(GraphicsPipeline pipeline);

    /// <summary>
    /// Records a non-indexed draw command.
    /// </summary>
    /// <param name="vertexCount">The number of vertices to draw.</param>
    /// <param name="instanceCount">The number of instances to draw.</param>
    /// <param name="firstVertex">The first vertex index.</param>
    /// <param name="firstInstance">The first instance index.</param>
    public abstract void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);

    /// <summary>
    /// Ends the active rendering operation.
    /// </summary>
    public abstract void EndRendering();

    /// <summary>
    /// Ends command recording.
    /// </summary>
    public abstract void End();
}