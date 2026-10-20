namespace PanguEngine.World.Entities;

/// <summary>
/// Represents one object-oriented facade over entity archetype storage.
/// </summary>
public abstract class Entity
{
    protected internal Entity(EntityManager entities, EntityHandle handle)
    {
        Entities = entities;
        Handle = handle;
        Type = entities.GetEntityType(handle);
    }

    /// <summary>The stable entity handle.</summary>
    public EntityHandle Handle { get; }

    /// <summary>The registered entity type.</summary>
    public EntityType Type { get; }

    /// <summary>Whether this facade still identifies a live entity.</summary>
    public bool IsAlive => Entities.IsAlive(Handle);

    internal EntityManager Entities { get; }

    /// <summary>
    /// Destroys this entity.
    /// </summary>
    public void Destroy()
    {
        Entities.Destroy(Handle);
    }
}
