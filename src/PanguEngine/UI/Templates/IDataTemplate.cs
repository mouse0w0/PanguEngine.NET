namespace PanguEngine.UI.Templates;

/// <summary>Creates a visual subtree for a non-null content value.</summary>
public interface IDataTemplate
{
    /// <summary>Creates an independent, unparented root for the supplied content.</summary>
    /// <param name="data">The non-null content to display.</param>
    /// <returns>A new visual root, or null for empty presentation.</returns>
    /// <remarks>Returning a root already in use or reusing a previous root has undefined behavior.</remarks>
    UIElement? Build(object data);
}
