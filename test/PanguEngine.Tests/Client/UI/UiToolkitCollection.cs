namespace PanguEngine.Tests.Client.UI;

internal static class UiToolkitCollection
{
    internal const string Name = "UI toolkit";
}

[CollectionDefinition(UiToolkitCollection.Name, DisableParallelization = true)]
public sealed class UiToolkitCollectionDefinition
{
}
