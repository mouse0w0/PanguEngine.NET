namespace PanguEngine.Graphics;

/// <summary>
/// Represents a logical graphics device.
/// </summary>
public abstract class GraphicsDevice
{
    /// <summary>
    /// Creates a buffer with the given description.
    /// </summary>
    /// <param name="description">The buffer description.</param>
    /// <returns>The created buffer.</returns>
    public abstract Buffer CreateBuffer(in BufferDescription description);

    /// <summary>
    /// Queues data for upload into a buffer.
    /// </summary>
    /// <typeparam name="T">The unmanaged type of the data elements.</typeparam>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="data">The data to upload.</param>
    /// <param name="destinationOffset">The destination byte offset within the buffer.</param>
    /// <returns>A handle that represents the queued upload completion state.</returns>
    public abstract UploadHandle UploadBuffer<T>(
        Buffer destination,
        ReadOnlySpan<T> data,
        ulong destinationOffset = 0) where T : unmanaged;

    /// <summary>
    /// Creates a texture with the given description.
    /// </summary>
    /// <param name="description">The texture description.</param>
    /// <returns>The created texture.</returns>
    public abstract Texture CreateTexture(in TextureDescription description);

    /// <summary>
    /// Queues data for upload into a texture.
    /// </summary>
    /// <param name="destination">The destination texture.</param>
    /// <param name="data">The texture data to upload.</param>
    /// <returns>A handle that represents the queued upload completion state.</returns>
    public abstract UploadHandle UploadTexture(
        Texture destination,
        ReadOnlySpan<byte> data);

    /// <summary>
    /// Queues data for upload into a texture region.
    /// </summary>
    /// <param name="destination">The destination texture.</param>
    /// <param name="data">The texture data to upload.</param>
    /// <param name="region">The destination texture region.</param>
    /// <returns>A handle that represents the queued upload completion state.</returns>
    public abstract UploadHandle UploadTexture(
        Texture destination,
        ReadOnlySpan<byte> data,
        in TextureUploadRegion region);

    /// <summary>
    /// Creates a sampler with the given description.
    /// </summary>
    /// <param name="description">The sampler description.</param>
    /// <returns>The created sampler.</returns>
    public abstract Sampler CreateSampler(in SamplerDescription description);

    /// <summary>
    /// Creates a shader with the given description.
    /// </summary>
    /// <param name="description">The shader description.</param>
    /// <returns>The created shader.</returns>
    public abstract Shader CreateShader(in ShaderDescription description);

    /// <summary>
    /// Creates a descriptor set layout with the given description.
    /// </summary>
    /// <param name="description">The descriptor set layout description.</param>
    /// <returns>The created descriptor set layout.</returns>
    public abstract DescriptorSetLayout CreateDescriptorSetLayout(in DescriptorSetLayoutDescription description);

    /// <summary>
    /// Creates a descriptor set with the given description.
    /// </summary>
    /// <param name="description">The descriptor set description.</param>
    /// <returns>The created descriptor set.</returns>
    public abstract DescriptorSet CreateDescriptorSet(in DescriptorSetDescription description);

    /// <summary>
    /// Calculates a uniform buffer binding size that satisfies the backend alignment requirement.
    /// </summary>
    /// <param name="rawSize">The unaligned size in bytes.</param>
    /// <returns>The aligned size in bytes.</returns>
    public abstract ulong GetAlignedUniformSize(ulong rawSize);

    /// <summary>
    /// Creates a graphics pipeline with the given description.
    /// </summary>
    /// <param name="description">The graphics pipeline description.</param>
    /// <returns>The created graphics pipeline.</returns>
    public abstract GraphicsPipeline CreateGraphicsPipeline(in GraphicsPipelineDescription description);
}