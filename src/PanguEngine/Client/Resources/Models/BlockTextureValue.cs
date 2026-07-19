using PanguEngine.Registries;

namespace PanguEngine.Client.Resources.Models;

internal abstract record BlockTextureValue
{
    internal sealed record Variable(string Name) : BlockTextureValue;

    internal sealed record Resource(ResourceKey Key) : BlockTextureValue;
}