using PanguEngine.Client.UI.Selection;
using PanguEngine.Collections;
using PanguEngine.ComponentModel;

namespace PanguEngine.Tests.Client.UI.Selection;

public sealed class SingleSelectionModelBindingTests
{
    [Fact]
    public void OneWayBindingAllowsLocalSelectionUntilSourceChanges()
    {
        var source = new Host { Index = 0 };
        var model = new SingleSelectionModel<string>(new ObservableList<string>(["a", "b"]).AsReadOnly());
        model.Bind(SingleSelectionModel<string>.SelectedIndexProperty, source, Host.IndexProperty);
        Assert.Equal(0, model.SelectedIndex);

        model.Select(1);
        Assert.Equal(0, source.Index);
        Assert.Equal(1, model.SelectedIndex);
        Assert.True(model.IsBound(SingleSelectionModel<string>.SelectedIndexProperty));

        source.Index = -1;
        Assert.Equal(-1, model.SelectedIndex);
        source.Index = 0;
        Assert.Equal("a", model.SelectedItem);
    }

    [Fact]
    public void TwoWayInitializationAndSourceUpdatesDoNotWriteNormalizedIndexBack()
    {
        var source = new Host { Index = 9 };
        var model = new SingleSelectionModel<string>(new ObservableList<string>(["a", "b"]).AsReadOnly());

        model.BindTwoWay(SingleSelectionModel<string>.SelectedIndexProperty, source, Host.IndexProperty);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(9, source.Index);
        source.Index = 1;
        Assert.Equal("b", model.SelectedItem);
        source.Index = 8;
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(8, source.Index);
    }

    [Fact]
    public void TwoWayIndexBindingWritesLocalAndCollectionChangesToSource()
    {
        var items = new ObservableList<string>(["a", "b"]);
        var model = new SingleSelectionModel<string>(items.AsReadOnly());
        var source = new Host { Index = -1 };
        model.BindTwoWay(SingleSelectionModel<string>.SelectedIndexProperty, source, Host.IndexProperty);

        model.Select(1);
        Assert.Equal(1, source.Index);
        items.Insert(0, "x");
        Assert.Equal(2, source.Index);
        Assert.Equal("b", model.SelectedItem);
        items.RemoveAt(2);
        Assert.Equal(-1, source.Index);
        Assert.Equal(-1, model.SelectedIndex);
    }

    [Fact]
    public void TwoWayItemEchoPreservesSecondOccurrenceOfSameReference()
    {
        var item = new object();
        var source = new Host();
        var model = new SingleSelectionModel<object>(new ObservableList<object>([item, item]).AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<object>.SelectedItemProperty, source, Host.ItemProperty);

        model.SelectedIndex = 1;

        Assert.Same(item, source.Item);
        Assert.Same(item, model.SelectedItem);
        Assert.Equal(1, model.SelectedIndex);
    }

    [Fact]
    public void UnbindKeepsSelectionAndStopsSynchronizationInBothDirections()
    {
        var source = new Host { Index = 1 };
        var model = new SingleSelectionModel<int>(new ObservableList<int>([10, 20]).AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<int>.SelectedIndexProperty, source, Host.IndexProperty);

        model.Unbind(SingleSelectionModel<int>.SelectedIndexProperty);

        Assert.Equal(1, model.SelectedIndex);
        Assert.False(model.IsBound(SingleSelectionModel<int>.SelectedIndexProperty));
        source.Index = 0;
        Assert.Equal(1, model.SelectedIndex);
        model.Clear();
        Assert.Equal(0, source.Index);
    }

    [Fact]
    public void ClearIndexValueUnbindsBeforeClearingSelection()
    {
        var source = new Host { Index = 1 };
        var model = new SingleSelectionModel<int>(new ObservableList<int>([10, 20]).AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<int>.SelectedIndexProperty, source, Host.IndexProperty);

        model.ClearValue(SingleSelectionModel<int>.SelectedIndexProperty);

        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(1, source.Index);
        Assert.False(model.IsBound(SingleSelectionModel<int>.SelectedIndexProperty));
    }

    [Fact]
    public void ClearItemValueUnbindsAndSelectsDefaultItemWhenPresent()
    {
        var item = new object();
        var source = new Host { Item = item };
        var model = new SingleSelectionModel<object?>(new ObservableList<object?>([item, null]).AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<object?>.SelectedItemProperty, source, Host.ItemProperty);
        Assert.Equal(0, model.SelectedIndex);

        model.ClearValue(SingleSelectionModel<object?>.SelectedItemProperty);

        Assert.Equal(1, model.SelectedIndex);
        Assert.Null(model.SelectedItem);
        Assert.Same(item, source.Item);
        Assert.False(model.IsBound(SingleSelectionModel<object?>.SelectedItemProperty));
    }

    [Fact]
    public void SourceBindingSwitchesListsAndClearValueDisconnectsWithoutWriteback()
    {
        var first = new ObservableList<string>(["a"]).AsReadOnly();
        var second = new ObservableList<string>(["b"]).AsReadOnly();
        var source = new Host { Items = first };
        var model = new SingleSelectionModel<string>();
        model.BindTwoWay(SingleSelectionModel<string>.SourceProperty, source, Host.ItemsProperty);
        model.Select(0);

        source.Items = second;
        Assert.Same(second, model.Source);
        Assert.Equal(-1, model.SelectedIndex);
        model.Source = first;
        Assert.Same(first, source.Items);
        model.Select(0);

        model.ClearValue(SingleSelectionModel<string>.SourceProperty);
        Assert.Null(model.Source);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Same(first, source.Items);
        Assert.False(model.IsBound(SingleSelectionModel<string>.SourceProperty));
    }

    [Fact]
    public void DifferentSelectionReturnedBySourceNormalizationIsRejectedDuringNotification()
    {
        var source = new NormalizingHost();
        var model = new SingleSelectionModel<string>(new ObservableList<string>(["a", "b"]).AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<string>.SelectedIndexProperty, source, NormalizingHost.IndexProperty);

        Assert.Throws<InvalidOperationException>(() => model.SelectedIndex = 1);

        Assert.Equal(0, source.Index);
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal("b", model.SelectedItem);
        model.SelectedIndex = 0;
        Assert.Equal(0, model.SelectedIndex);
    }

    [Fact]
    public void RemovingSelectedItemWritesNullBackWithoutSelectingNullOccurrence()
    {
        var item = new object();
        var items = new ObservableList<object?>([null, item]);
        var source = new Host { Item = item };
        var model = new SingleSelectionModel<object?>(items.AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<object?>.SelectedItemProperty, source, Host.ItemProperty);
        Assert.Equal(1, model.SelectedIndex);
        var notifications = 0;
        model.PropertyChanged += (_, _) =>
        {
            model.Clear();
            Assert.Equal(-1, model.SelectedIndex);
            notifications++;
        };

        items.RemoveAt(1);

        Assert.Null(source.Item);
        Assert.Null(model.SelectedItem);
        Assert.Equal(-1, model.SelectedIndex);
        Assert.Equal(2, notifications);
        Assert.Null(Assert.Single(items));
    }

    [Fact]
    public void ClearValueTypeItemUnbindsBeforeSelectingZero()
    {
        var source = new Host { Index = 5 };
        var model = new SingleSelectionModel<int>(new ObservableList<int>([5, 0, 0]).AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<int>.SelectedItemProperty, source, Host.IndexProperty);
        Assert.Equal(0, model.SelectedIndex);

        model.ClearValue(SingleSelectionModel<int>.SelectedItemProperty);

        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(0, model.SelectedItem);
        Assert.Equal(5, source.Index);
        Assert.False(model.IsBound(SingleSelectionModel<int>.SelectedItemProperty));
    }

    [Fact]
    public void ValueTypeItemEchoPreservesSecondEqualOccurrence()
    {
        var source = new Host { Index = -1 };
        var model = new SingleSelectionModel<int>(new ObservableList<int>([5, 5]).AsReadOnly());
        model.BindTwoWay(SingleSelectionModel<int>.SelectedItemProperty, source, Host.IndexProperty);

        model.Select(1);

        Assert.Equal(5, source.Index);
        Assert.Equal(1, model.SelectedIndex);
        Assert.Equal(5, model.SelectedItem);
    }

    private sealed class Host : ObservableObject
    {
        internal static readonly Property<int> IndexProperty = Property.Register<Host, int>(nameof(Index), -1);
        internal static readonly Property<object?> ItemProperty = Property.Register<Host, object?>(nameof(Item));
        internal static readonly Property<IReadOnlyObservableList<string>?> ItemsProperty =
            Property.Register<Host, IReadOnlyObservableList<string>?>(nameof(Items));

        internal int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }
        internal object? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }
        internal IReadOnlyObservableList<string>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    }

    private sealed class NormalizingHost : ObservableObject
    {
        internal static readonly DirectProperty<NormalizingHost, int> IndexProperty =
            Property.RegisterDirect<NormalizingHost, int>(nameof(Index), owner => owner.Index,
                (owner, value) => owner.SetIndex(value));

        internal int Index { get; private set; } = -1;

        private void SetIndex(int value)
        {
            var old = Index;
            Index = Math.Min(value, 0);
            if (old != Index)
                RaisePropertyChanged(IndexProperty, old, Index);
        }
    }
}
