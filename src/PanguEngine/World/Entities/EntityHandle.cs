namespace PanguEngine.World.Entities;

/// <summary>
/// Identifies an entity slot and generation within one entity manager.
/// </summary>
/// <param name="Index">The entity slot index.</param>
/// <param name="Generation">The slot generation.</param>
public readonly record struct EntityHandle(int Index, uint Generation);
