namespace PanguEngine.Tests.Client.UI;

internal static class UiSettingsCollection
{
    internal const string Name = "UI settings";
}

[CollectionDefinition(UiSettingsCollection.Name, DisableParallelization = true)]
public sealed class UiSettingsCollectionDefinition
{
}
