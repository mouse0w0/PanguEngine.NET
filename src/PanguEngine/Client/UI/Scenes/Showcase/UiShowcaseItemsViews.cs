using PanguEngine.Collections;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Templates;

namespace PanguEngine.Client.UI.Scenes.Showcase;

/// <summary>Builds independently selectable virtualized list and tile demonstrations.</summary>
internal static class UiShowcaseItemsViews
{
    /// <summary>Creates vertical and horizontal fixed-item list samples.</summary>
    internal static UiShowcaseExample CreateListExample() => new(
        "列表视图", "固定项目尺寸；滚动只创建视口附近的内容，两个实例的选择与缓存互不影响。",
        UiShowcaseWidgets.Column(CreateListSample(Orientation.Vertical), CreateListSample(Orientation.Horizontal)));

    /// <summary>Creates both fixed-tile filling directions.</summary>
    internal static UiShowcaseExample CreateGridExample() => new(
        "网格视图", "水平填充时纵向滚动，垂直填充时横向滚动；调整窗口可观察行列数量变化和尾部空位。",
        UiShowcaseWidgets.Column(CreateGridSample(Orientation.Horizontal), CreateGridSample(Orientation.Vertical)));

    private static StackPanel CreateListSample(Orientation orientation)
    {
        var source = CreateSource();
        var view = new ListView<DemoItem>
        {
            ItemsSource = source,
            ItemTemplate = new DemoTemplate(),
            Orientation = orientation,
            ItemExtent = orientation == Orientation.Vertical ? 44 : 144,
            Spacing = 4,
            Height = 220,
            SelectionMode = SelectionMode.Multiple
        };
        view.Classes.Add("showcase-items-view");
        return CreateSample($"list-{orientation}", $"{orientation} 列表", view, source, view.Selection, view.ScrollIntoView);
    }

    private static StackPanel CreateGridSample(Orientation flow)
    {
        var source = CreateSource();
        var view = new GridView<DemoItem>
        {
            ItemsSource = source,
            ItemTemplate = new DemoTemplate(),
            Flow = flow,
            ItemWidth = 144,
            ItemHeight = 64,
            RowSpacing = 4,
            ColumnSpacing = 4,
            Height = 220,
            SelectionMode = SelectionMode.Multiple
        };
        view.Classes.Add("showcase-items-view");
        return CreateSample($"grid-{flow}", $"{flow} 填充网格", view, source, view.Selection, view.ScrollIntoView);
    }

    private static StackPanel CreateSample(string id, string title, Control view, ObservableList<DemoItem> source,
        MultiSelectionModel<DemoItem> selection, Action<int> scrollIntoView)
    {
        var nextId = source.Count;
        var feedback = UiShowcaseWidgets.Caption(string.Empty);
        var position = new NumericUpDown<int> { Minimum = 0, Maximum = source.Count - 1, Value = 9000, Width = 120 };
        var all = UiShowcaseWidgets.ActionButton($"showcase-items-all-{id}", "全选", selection.SelectAll);
        void RefreshSelection()
        {
            feedback.Content = $"模式：{selection.SelectionMode}，选中 {selection.SelectedIndices.Count} 项；索引：" +
                string.Join(", ", selection.SelectedIndices.Take(12)) + (selection.SelectedIndices.Count > 12 ? " …" : "");
            all.IsEnabled = selection.SelectionMode == SelectionMode.Multiple;
        }
        selection.SelectedIndices.Changed += (_, _) => RefreshSelection();
        selection.Subscribe(MultiSelectionModel<DemoItem>.SelectionModeProperty, (_, _) => RefreshSelection());
        source.Changed += (_, _) => position.Maximum = Math.Max(0, source.Count - 1);
        RefreshSelection();
        var modes = UiShowcaseWidgets.Samples(
            UiShowcaseWidgets.ActionButton($"showcase-items-none-{id}", "None", () => selection.SelectionMode = SelectionMode.None),
            UiShowcaseWidgets.ActionButton($"showcase-items-single-{id}", "Single", () => selection.SelectionMode = SelectionMode.Single),
            UiShowcaseWidgets.ActionButton($"showcase-items-multiple-{id}", "Multiple", () => selection.SelectionMode = SelectionMode.Multiple),
            UiShowcaseWidgets.ActionButton($"showcase-items-clear-{id}", "清空选择", selection.Clear), all);
        var changes = UiShowcaseWidgets.Samples(
            UiShowcaseWidgets.ActionButton($"showcase-items-insert-{id}", "插入首项", () => source.Insert(0, new DemoItem(nextId++))),
            UiShowcaseWidgets.ActionButton($"showcase-items-remove-{id}", "删除目标项", () =>
            {
                if (position.Value < source.Count)
                    source.RemoveAt(position.Value);
            }),
            UiShowcaseWidgets.ActionButton($"showcase-items-reverse-{id}", "反转顺序", source.Reverse),
            position,
            UiShowcaseWidgets.ActionButton($"showcase-items-locate-{id}", "定位（不选择）", () => scrollIntoView(position.Value)));
        return UiShowcaseWidgets.Column(UiShowcaseWidgets.Caption(title), modes, changes, view, feedback,
            UiShowcaseWidgets.Caption("点击替换；Ctrl 点击切换；Shift 连续范围；Ctrl+Shift 追加。聚焦视图后方向键导航，Ctrl 仅移动，Space 切换。"));
    }

    private static ObservableList<DemoItem> CreateSource() => new(Enumerable.Range(0, 10003).Select(index => new DemoItem(index)));

    private sealed record DemoItem(int Id);

    private sealed class DemoTemplate : IRecyclingDataTemplate
    {
        public UIElement Build(object data)
        {
            var root = new StackPanel { Spacing = 2, IsHitTestVisible = false };
            root.Classes.Add("showcase-item-content");
            root.Children.Add(new Text());
            var bar = new Panel { Height = 4, HorizontalAlignment = HorizontalAlignment.Left };
            bar.Classes.Add("showcase-item-meter");
            root.Children.Add(bar);
            Apply((DemoItem)data, root);
            return root;
        }

        public UIElement Update(object data, UIElement root)
        {
            Apply((DemoItem)data, (StackPanel)root);
            return root;
        }

        public void Clear(object data, UIElement root)
        {
            var content = (StackPanel)root;
            ((Text)content.Children[0]).Content = string.Empty;
            content.Children[1].Width = 0;
        }

        private static void Apply(DemoItem data, StackPanel root)
        {
            ((Text)root.Children[0]).Content = $"项目 {data.Id:00000}";
            root.Children[1].Width = 20 + data.Id % 90;
        }
    }
}
