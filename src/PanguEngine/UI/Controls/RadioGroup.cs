using PanguEngine.ComponentModel;

namespace PanguEngine.UI.Controls;

/// <summary>Provides selection access shared by strongly typed radio groups.</summary>
public abstract class RadioGroup : ObservableObject
{
    /// <summary>Identifies the read-only <see cref="SelectedButton"/> property.</summary>
    public static readonly DirectProperty<RadioGroup, RadioButton?> SelectedButtonProperty =
        Property.RegisterDirect<RadioGroup, RadioButton?>(nameof(SelectedButton),
            static group => group.SelectedButton);

    /// <summary>Gets the selected member, or null for no selection.</summary>
    public abstract RadioButton? SelectedButton { get; }

    /// <summary>Clears the selection while preserving existing property bindings.</summary>
    public abstract void Clear();
}

/// <summary>Coordinates a single selected button and its business value independently of the UI tree.</summary>
/// <typeparam name="T">A reference type or nullable value type whose null value represents no selection.</typeparam>
public sealed class RadioGroup<T> : RadioGroup
{
    /// <summary>Identifies the <see cref="SelectedValue"/> property.</summary>
    public static readonly DirectProperty<RadioGroup<T>, T?> SelectedValueProperty =
        Property.RegisterDirect<RadioGroup<T>, T?>(nameof(SelectedValue),
            static group => group.SelectedValue,
            static (group, value) => group.SelectedValue = value,
            comparer: EqualityComparer<T?>.Default);

    private readonly List<RadioButton<T>> _members = [];
    private RadioButton<T>? _selectedButton;
    private T? _selectedValue;
    private bool _notifying;

    /// <summary>Initializes an empty group with no selection.</summary>
    /// <exception cref="NotSupportedException">Thrown when T is a non-nullable value type.</exception>
    public RadioGroup() => RadioValueType<T>.VerifySupported();

    /// <summary>Gets or sets the selected business value, or null for no selection.</summary>
    /// <remarks>
    /// A non-null assignment selects the first matching member in registration order. An unmatched
    /// value clears the selection. Equal-value confirmation during notifications preserves button identity.
    /// </remarks>
    public T? SelectedValue
    {
        get => _selectedValue;
        set
        {
            VerifyMutationAccess();
            if (_notifying && SelectedValueProperty.ValuesEqual(_selectedValue, value))
                return;
            var button = value is null ? null : _members.Find(member =>
                member.Value is not null && SelectedValueProperty.ValuesEqual(member.Value, value));
            ChangeSelection(button);
        }
    }

    /// <inheritdoc />
    public override RadioButton<T>? SelectedButton => _selectedButton;

    /// <summary>Selects the specified member when it has a non-null value.</summary>
    /// <param name="button">The exact member to select, including when members share the same value.</param>
    /// <exception cref="ArgumentException">Thrown when the button does not belong to this group.</exception>
    /// <exception cref="InvalidOperationException">Thrown when access or notification reentry rejects the change.</exception>
    public void Select(RadioButton<T> button)
    {
        VerifyMutationAccess();
        if (!ReferenceEquals(button.Group, this))
            throw new ArgumentException("The button does not belong to this radio group.", nameof(button));
        if (button.Value is not null)
            ChangeSelection(button);
    }

    /// <inheritdoc />
    public override void Clear()
    {
        VerifyMutationAccess();
        ChangeSelection(null);
    }

    /// <inheritdoc />
    protected override void VerifyMutationAccess()
    {
        foreach (var member in _members)
            member.VerifyGroupAccess();
    }

    internal RadioButton<T>[] GetMembersSnapshot() => _members.ToArray();

    internal void BeginMutation()
    {
        VerifyMutationAccess();
        VerifyNotNotifying();
        _notifying = true;
    }

    internal void EndMutation() => _notifying = false;

    internal static void SynchronizeMembership(RadioButton<T> button, RadioGroup<T>? oldGroup, RadioGroup<T>? newGroup)
    {
        oldGroup?._members.Remove(button);
        newGroup?._members.Add(button);
        if (oldGroup is not null && ReferenceEquals(oldGroup.SelectedButton, button))
            oldGroup.PublishSelection(oldGroup.CommitSelection(null));
    }

    internal void SynchronizeMemberValue(RadioButton<T> button, T? value)
    {
        if (ReferenceEquals(SelectedButton, button))
            PublishSelection(CommitSelection(value is null ? null : button));
    }

    private void ChangeSelection(RadioButton<T>? button)
    {
        if (ReferenceEquals(SelectedButton, button))
            return;
        VerifyNotNotifying();
        _notifying = true;
        try
        {
            PublishSelection(CommitSelection(button));
        }
        finally
        {
            _notifying = false;
        }
    }

    private SelectionChange CommitSelection(RadioButton<T>? button)
    {
        var value = button is null ? default : button.Value;
        var change = new SelectionChange(SelectedButton, _selectedValue, button, value);
        _selectedButton = button;
        _selectedValue = value;
        if (!ReferenceEquals(change.OldButton, button))
        {
            change.OldButton?.CommitChecked(false);
            button?.CommitChecked(true);
        }
        return change;
    }

    private void PublishSelection(SelectionChange change)
    {
        if (!ReferenceEquals(change.OldButton, change.NewButton))
        {
            change.OldButton?.RefreshCheckedPseudoClass();
            change.NewButton?.RefreshCheckedPseudoClass();
            change.OldButton?.PublishCheckedChanged(true, false);
            change.NewButton?.PublishCheckedChanged(false, true);
            RaisePropertyChanged(SelectedButtonProperty, change.OldButton, change.NewButton);
        }
        if (!SelectedValueProperty.ValuesEqual(change.OldValue, change.NewValue))
            RaisePropertyChanged(SelectedValueProperty, change.OldValue, change.NewValue);
    }

    private void VerifyNotNotifying()
    {
        if (_notifying)
            throw new InvalidOperationException("A radio group cannot change during its notifications.");
    }

    private readonly record struct SelectionChange(
        RadioButton<T>? OldButton, T? OldValue, RadioButton<T>? NewButton, T? NewValue);
}
