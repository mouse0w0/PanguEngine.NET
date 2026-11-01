namespace PanguEngine.UI.Templates;

/// <summary>Creates content and optionally reuses cleared roots for different data items.</summary>
public interface IRecyclingDataTemplate : IDataTemplate
{
    /// <summary>Displays non-null data using a cleared root, a replacement root, or no root.</summary>
    /// <param name="data">The new non-null data item.</param>
    /// <param name="root">A cleared, detached root previously created by this template.</param>
    /// <returns>The reused root, a new independent root, or null for empty presentation.</returns>
    UIElement? Update(object data, UIElement root);

    /// <summary>Ends a root's association with its current data before removal or reuse.</summary>
    /// <param name="data">The non-null data currently represented by the root.</param>
    /// <param name="root">The root whose bindings and item-specific state should be cleared.</param>
    void Clear(object data, UIElement root);
}
