namespace PanguEngine.Graphics;

/// <summary>
/// Describes a color attachment used by a rendering operation.
/// </summary>
/// <param name="Attachment">The color attachment texture.</param>
/// <param name="ClearColor">The clear color.</param>
/// <param name="LoadOperation">The color attachment load operation.</param>
/// <param name="StoreOperation">The color attachment store operation.</param>
public readonly record struct ColorAttachmentDescription(
    Texture Attachment,
    ClearColor ClearColor,
    LoadOperation LoadOperation = LoadOperation.Clear,
    StoreOperation StoreOperation = StoreOperation.Store);