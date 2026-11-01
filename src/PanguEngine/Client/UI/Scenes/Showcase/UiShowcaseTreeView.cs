using PanguEngine.Collections;
using PanguEngine.UI;
using PanguEngine.UI.Controls;
using PanguEngine.UI.Selection;
using PanguEngine.UI.Templates;

namespace PanguEngine.Client.UI.Scenes.Showcase;

/// <summary>Builds synchronous hierarchy and explicit business collection update demonstrations.</summary>
internal static class UiShowcaseTreeView
{
    /// <summary>Creates independently selectable tree demonstrations with live business data.</summary>
    internal static UiShowcaseExample CreateExample() => new("树视图",
        "固定行高虚拟化；首次展开同步获取原始子项集合；折叠保留选择和集合引用。",
        UiShowcaseWidgets.Column(CreateHierarchy(), CreateExplicitUpdates(),
            UiShowcaseWidgets.Caption("点击选择；Ctrl 切换；Shift 可见范围；左右键展开或进入父子节点。Shift+滚轮横向滚动。")));

    private static StackPanel CreateHierarchy()
    {
        var large = new DemoNode("大量项目（首次展开生成 2000 项）", 2000);
        var deep = new DemoNode("深层级与长名称", depth: 12);
        var tree = CreateTree([large, deep, new DemoNode("独立叶节点")],
            node => node.CreateChildren());
        tree.StyleId = "showcase-tree-hierarchy";
        var feedback = UiShowcaseWidgets.Caption(string.Empty);
        var nextId = 2000;
        var insert = UiShowcaseWidgets.ActionButton("showcase-tree-insert", "向已访问分支插入", () =>
            large.Children.Insert(0, new DemoNode($"新项目 {nextId++}")));
        var remove = UiShowcaseWidgets.ActionButton("showcase-tree-remove", "删除分支首项", () =>
        {
            if (large.Children.Count > 0)
                large.Children.RemoveAt(0);
        });
        var reorder = UiShowcaseWidgets.ActionButton("showcase-tree-reorder", "反转子项", large.Children.Reverse);
        var locate = UiShowcaseWidgets.ActionButton("showcase-tree-locate", "定位末项（不选择、不展开）", () =>
        {
            if (large.Children.Count > 0)
                tree.ScrollIntoView(large.Children[^1]);
        });
        void Refresh()
        {
            var prepared = large.IsPrepared;
            insert.IsEnabled = prepared;
            remove.IsEnabled = prepared && large.Children.Count > 0;
            reorder.IsEnabled = prepared;
            locate.IsEnabled = prepared && tree.IsExpanded(large) && large.Children.Count > 0;
            feedback.Content = $"模式：{tree.SelectionMode}，选中 {tree.Selection.SelectedItems.Count} 项；" +
                string.Join("、", tree.Selection.SelectedItems.Take(5).Select(node => node.Label));
        }
        tree.Selection.SelectedItems.Changed += (_, _) => Refresh();
        tree.Selection.Subscribe(TreeSelectionModel<DemoNode>.SelectionModeProperty, (_, _) => Refresh());
        tree.ExpansionChanged += (_, _) => Refresh();
        large.Children.Changed += (_, _) => Refresh();
        Refresh();
        var modes = UiShowcaseWidgets.Samples(
            UiShowcaseWidgets.ActionButton("showcase-tree-none", "None", () => tree.SelectionMode = SelectionMode.None),
            UiShowcaseWidgets.ActionButton("showcase-tree-single", "Single", () => tree.SelectionMode = SelectionMode.Single),
            UiShowcaseWidgets.ActionButton("showcase-tree-multiple", "Multiple", () => tree.SelectionMode = SelectionMode.Multiple),
            UiShowcaseWidgets.ActionButton("showcase-tree-clear", "清空选择", tree.Selection.Clear));
        return UiShowcaseWidgets.Column(UiShowcaseWidgets.Caption("层级、虚拟化与动态集合"), modes,
            UiShowcaseWidgets.Samples(insert, remove, reorder, locate), tree, feedback,
            UiShowcaseWidgets.Caption("先展开大量项目分支，再选择后代并折叠：选择会保留，删除才清理。定位仅针对已展开的可见节点，不会自动展开祖先。宽度随已显示内容向上估算，滚回短内容不会立即缩小。"));
    }

    private static StackPanel CreateExplicitUpdates()
    {
        var root = new DemoNode("业务结果（原集合初始为空）", mayHaveChildren: true);
        var tree = CreateTree([root], node => node.CreateChildren());
        tree.StyleId = "showcase-tree-explicit-updates";
        tree.Expand(root);
        DemoNode[] prepared = [];
        var generation = 0;
        var feedback = UiShowcaseWidgets.Caption(string.Empty);
        var apply = UiShowcaseWidgets.ActionButton("showcase-tree-apply", "应用到原集合", () =>
            root.Children.ReplaceAll(prepared));
        var prepare = UiShowcaseWidgets.ActionButton("showcase-tree-prepare", "准备一批业务数据", () =>
        {
            generation++;
            prepared =
            [
                new DemoNode($"第 {generation} 批：业务结果 A"),
                new DemoNode($"第 {generation} 批：业务结果 B"),
                new DemoNode($"第 {generation} 批：业务结果 C")
            ];
            Refresh();
        });
        void Refresh()
        {
            apply.IsEnabled = prepared.Length > 0;
            feedback.Content = $"业务已准备 {prepared.Length} 项，原集合当前 {root.Children.Count} 项；" +
                (tree.IsExpanded(root) ? "当前展开" : "当前折叠");
        }
        root.Children.Changed += (_, _) => Refresh();
        tree.ExpansionChanged += (_, _) => Refresh();
        Refresh();
        var actions = UiShowcaseWidgets.Samples(prepare, apply,
            UiShowcaseWidgets.ActionButton("showcase-tree-expand-result", "展开结果", () => tree.Expand(root)),
            UiShowcaseWidgets.ActionButton("showcase-tree-clear-result", "清空原集合", root.Children.Clear),
            UiShowcaseWidgets.ActionButton("showcase-tree-append-result", "向原集合追加", () =>
                root.Children.Add(new DemoNode($"追加结果 {++generation}"))));
        return UiShowcaseWidgets.Column(UiShowcaseWidgets.Caption("业务准备数据后，在 UI 线程显式应用"),
            tree, actions, feedback,
            UiShowcaseWidgets.Caption("准备数据不会改变树；应用只修改原可观察集合，空集合新增后不会自动展开。真实后台收集和切回 UI 线程由业务负责，树不保存异步任务。"));
    }

    private static TreeView<DemoNode> CreateTree(IEnumerable<DemoNode> roots,
        Func<DemoNode, IReadOnlyObservableList<DemoNode>> childrenSelector) => new(
        node => node.MayHaveChildren || node.ChildCount > 0 || node.Depth > 0 || node.Children.Count > 0, childrenSelector)
    {
        ItemsSource = new ObservableList<DemoNode>(roots), ItemTemplate = new DemoTemplate(),
        ItemExtent = 36, Height = 240, SelectionMode = SelectionMode.Multiple
    };

    private sealed class DemoNode(string label, int childCount = 0, int depth = 0, bool mayHaveChildren = false)
    {
        internal string Label { get; } = label;
        internal int ChildCount { get; } = childCount;
        internal int Depth { get; } = depth;
        internal bool MayHaveChildren { get; } = mayHaveChildren;
        internal bool IsPrepared => _generated;
        internal ObservableList<DemoNode> Children { get; } = new();
        private bool _generated;

        internal IReadOnlyObservableList<DemoNode> CreateChildren()
        {
            if (_generated)
                return Children;
            _generated = true;
            for (var index = 0; index < ChildCount; index++)
                Children.Add(new DemoNode(index % 17 == 0
                    ? $"项目 {index:0000}：这是一条较长的业务名称，用于观察虚拟行自然宽度与横向滚动范围的逐步扩展"
                    : $"项目 {index:0000}"));
            if (Depth > 0)
                Children.Add(new DemoNode($"层级 {13 - Depth}：展开查看下一层", depth: Depth - 1));
            return Children;
        }

        public override string ToString() => Label;
    }

    private sealed class DemoTemplate : IRecyclingDataTemplate
    {
        public UIElement Build(object data) => new Text { Content = ((DemoNode)data).Label, IsHitTestVisible = false };
        public UIElement Update(object data, UIElement root)
        {
            ((Text)root).Content = ((DemoNode)data).Label;
            return root;
        }
        public void Clear(object data, UIElement root) => ((Text)root).Content = string.Empty;
    }
}
