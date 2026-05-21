namespace PanguEngine.Graphics;

/// <summary>
/// Describes the beginning of a color rendering operation.
/// </summary>
/// <param name="ClearColor">The clear color.</param>
/// <param name="LoadOperation">The color attachment load operation.</param>
/// <param name="StoreOperation">The color attachment store operation.</param>
public readonly record struct RenderingDescription(
    ClearColor ClearColor,
    LoadOperation LoadOperation = LoadOperation.Clear,
    StoreOperation StoreOperation = StoreOperation.Store);