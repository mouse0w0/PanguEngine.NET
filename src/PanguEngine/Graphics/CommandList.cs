namespace PanguEngine.Graphics;

/// <summary>
/// Represents a backend-independent graphics command recording object.
/// </summary>
public abstract class CommandList
{
    /// <summary>
    /// Begins command recording.
    /// </summary>
    public abstract void BeginRecording();

    /// <summary>
    /// Begins rendering for the described attachments.
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
    /// Binds a vertex buffer to the given input slot.
    /// </summary>
    /// <param name="slot">The vertex input slot.</param>
    /// <param name="buffer">The vertex buffer.</param>
    /// <param name="offset">The byte offset within the vertex buffer.</param>
    public abstract void SetVertexBuffer(uint slot, Buffer buffer, ulong offset = 0);

    /// <summary>
    /// Binds an index buffer for indexed drawing.
    /// </summary>
    /// <param name="buffer">The index buffer.</param>
    /// <param name="format">The index element format.</param>
    /// <param name="offset">The byte offset within the index buffer.</param>
    public abstract void SetIndexBuffer(Buffer buffer, IndexFormat format, ulong offset = 0);

    /// <summary>
    /// Binds a shader-visible descriptor set to the given slot.
    /// </summary>
    /// <param name="slot">The descriptor set slot.</param>
    /// <param name="descriptorSet">The descriptor set.</param>
    public abstract void SetDescriptorSet(uint slot, DescriptorSet descriptorSet);

    /// <summary>
    /// Records a non-indexed draw command.
    /// </summary>
    /// <param name="vertexCount">The number of vertices to draw.</param>
    /// <param name="instanceCount">The number of instances to draw.</param>
    /// <param name="firstVertex">The first vertex index.</param>
    /// <param name="firstInstance">The first instance index.</param>
    public abstract void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);

    /// <summary>
    /// Records an indexed draw command.
    /// </summary>
    /// <param name="indexCount">The number of indices to draw.</param>
    /// <param name="instanceCount">The number of instances to draw.</param>
    /// <param name="firstIndex">The first index to draw.</param>
    /// <param name="vertexOffset">The value added to the vertex index before vertex fetching.</param>
    /// <param name="firstInstance">The first instance index.</param>
    public abstract void DrawIndexed(
        uint indexCount,
        uint instanceCount = 1,
        uint firstIndex = 0,
        int vertexOffset = 0,
        uint firstInstance = 0);

    /// <summary>
    /// Ends the active rendering operation.
    /// </summary>
    public abstract void EndRendering();

    /// <summary>
    /// Prepares a color output for presentation.
    /// </summary>
    /// <param name="colorOutput">The color output view to present.</param>
    public abstract void PrepareForPresent(TextureView colorOutput);

    /// <summary>
    /// Ends command recording.
    /// </summary>
    public abstract void EndRecording();
}