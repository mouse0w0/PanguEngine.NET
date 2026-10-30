using PanguEngine.ComponentModel;
using PanguEngine.UI.Styling;
using PanguEngine.UI.Templates;

namespace PanguEngine.UI.Controls;

/// <summary>Provides shared content properties and presentation for single-content controls.</summary>
public class ContentControl : Control
{
    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly DirectProperty<ContentControl, object?> ContentProperty =
        Property.RegisterDirect<ContentControl, object?>(nameof(Content),
            static control => control.Content, static (control, value) => control.Content = value);

    /// <summary>Identifies the <see cref="ContentTemplate"/> property.</summary>
    public static readonly DirectProperty<ContentControl, IDataTemplate?> ContentTemplateProperty =
        Property.RegisterDirect<ContentControl, IDataTemplate?>(nameof(ContentTemplate),
            static control => control.ContentTemplate, static (control, value) => control.ContentTemplate = value);

    static ContentControl()
    {
        CssRegistry.RegisterElement<ContentControl>("ContentControl");
    }

    /// <summary>Initializes an empty content control.</summary>
    public ContentControl()
    {
        ContentSlot = new ContentSlot(this, this, VerifyMutationAccess,
            (previous, value) => RaisePropertyChanged(ContentProperty, previous, value),
            (previous, value) => RaisePropertyChanged(ContentTemplateProperty, previous, value));
    }

    /// <summary>Gets or sets the data or element displayed by this control.</summary>
    /// <remarks>
    /// Moving or reusing the content root externally has undefined behavior; content state is not reconciled.
    /// Same-reference writes do not rebuild. Synchronous update and notification reentry is rejected,
    /// including two-way binding feedback with a different reference. Failures after commit do not roll back.
    /// </remarks>
    public object? Content
    {
        get => ContentSlot.Content;
        set => ContentSlot.Content = value;
    }

    /// <summary>Gets or sets the explicit template used to display non-null content.</summary>
    /// <remarks>
    /// Template updates are synchronous. Changes during this slot's update or notifications are
    /// rejected, including binding feedback with a different reference. Failures after commit do not roll back.
    /// </remarks>
    public IDataTemplate? ContentTemplate
    {
        get => ContentSlot.Template;
        set => ContentSlot.Template = value;
    }

    /// <summary>Gets the root managed by this control's content slot.</summary>
    protected UIElement? ContentRoot => ContentSlot.Root;

    internal ContentSlot ContentSlot { get; }
}
