using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.Screens.Showcase;

internal static class UiShowcaseTabs
{
    internal const string ViewId = "showcase-tabs-view";
    internal const string AddId = "showcase-tabs-add";
    internal const string ResetId = "showcase-tabs-reset";
    internal const string SelectedId = "showcase-tabs-selected";
    internal const string CountId = "showcase-tabs-count";
    internal const string OrderId = "showcase-tabs-order";

    internal static UiShowcaseExample CreateExample()
    {
        var view = new TabView { StyleId = ViewId, CanReorderTabs = true };
        view.Classes.Add("showcase-tabs-view");
        var selected = CreateText(string.Empty, "showcase-tabs-diagnostic", SelectedId);
        var count = CreateText(string.Empty, "showcase-tabs-diagnostic", CountId);
        var order = CreateText(string.Empty, "showcase-tabs-diagnostic", OrderId);
        var nextNumber = 0;

        void Refresh()
        {
            selected.Content = $"选中：{(view.Selection.SelectedItem is { } item ? Title(item) : "无")}";
            count.Content = $"总数：{view.Items.Count}";
            order.Content = "顺序：" + (view.Items.Count == 0 ? "无" : string.Join(" > ", view.Items.Select(Title)));
        }

        TabItem CreateTab()
        {
            var number = ++nextNumber;
            var title = $"标签 {number}";
            var content = CreateStack("showcase-tabs-content",
                CreateText($"{title} · 编号 {number}", "showcase-tabs-title"),
                new TextBox { Text = $"编辑 {title}，切换或拖拽后检查内容保留。" });
            return new TabItem
            {
                Header = CreateText(title, "showcase-tabs-header"),
                Content = content,
                IsClosable = true
            };
        }

        void Reset()
        {
            view.Items.Clear();
            nextNumber = 0;
            for (var index = 0; index < 12; index++)
                view.Items.Add(CreateTab());
            view.Selection.SelectedIndex = 0;
            Refresh();
        }

        view.SelectionChanged += (_, _) => Refresh();
        view.TabReordered += (_, _) => Refresh();
        view.TabCloseRequested += (_, args) =>
        {
            view.Items.Remove(args.Item);
            Refresh();
        };

        var add = UiShowcaseWidgets.ActionButton(AddId, "新增标签页", () =>
        {
            var item = CreateTab();
            view.Items.Add(item);
            view.Selection.SelectedItem = item;
            Refresh();
        });
        var reset = UiShowcaseWidgets.ActionButton(ResetId, "重置标签页", Reset);
        Reset();

        return new UiShowcaseExample(
            "标签页",
            "拖拽标签排序；× 关闭；标签栏滚轮或箭头滚动。聚焦本视图后：Ctrl+Tab / Ctrl+Shift+Tab 切换，" +
            "方向键及 Home / End 移动焦点，Enter / Space 选中，Ctrl+F4 关闭。",
            CreateStack("showcase-tabs-demo",
                CreateStack("showcase-tabs-actions", add, reset), view, selected, count, order));
    }

    private static string Title(TabItem item) => ((Text)item.Header!).Content;

    private static Text CreateText(string content, string cssClass, string? id = null)
    {
        var text = new Text { Content = content, StyleId = id };
        text.Classes.Add(cssClass);
        return text;
    }

    private static StackPanel CreateStack(string cssClass, params UiNode[] children)
    {
        var stack = new StackPanel();
        stack.Classes.Add(cssClass);
        foreach (var child in children)
            stack.Children.Add(child);
        return stack;
    }
}
