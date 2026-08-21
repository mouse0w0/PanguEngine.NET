namespace PanguEngine.Graphics;

/// <summary>
/// A shader-visible descriptor set.
/// </summary>
public abstract class DescriptorSet : GraphicsResource
{
    /// <summary>
    /// Updates one or more descriptor elements in this set.
    /// </summary>
    /// <param name="bindings">The descriptor elements to update.</param>
    /// <remarks>
    /// The update must be invoked on the owning graphics or render thread and only when no command
    /// list that may still be submitted or executed references this set.
    /// </remarks>
    public abstract void Update(DescriptorSetBinding[] bindings);
}
