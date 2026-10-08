using System.ComponentModel;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.Screens.Showcase;

internal static class UiShowcaseBindings
{
    internal const string OneWayEditorId = "showcase-bindings-oneway-editor";
    internal const string OneWayTextId = "showcase-bindings-oneway-text";
    internal const string OneWayCountId = "showcase-bindings-oneway-count";

    internal const string TwoWayEditorId = "showcase-bindings-twoway-editor";
    internal const string TwoWayPeerId = "showcase-bindings-twoway-peer";
    internal const string TwoWayMirrorId = "showcase-bindings-twoway-mirror";
    internal const string TwoWayStatusId = "showcase-bindings-twoway-status";

    internal const string ComputedIncrementId = "showcase-bindings-computed-increment";
    internal const string ComputedEditorId = "showcase-bindings-computed-editor";
    internal const string ComputedSummaryId = "showcase-bindings-computed-summary";

    internal static UiShowcaseExample[] CreateExamples(Action<string> report)
    {
        return
        [
            CreateOneWayExample(),
            CreateTwoWayExample(),
            CreateComputedExample(report)
        ];
    }

    private static UiShowcaseExample CreateOneWayExample()
    {
        var editor = new TextBox { StyleId = OneWayEditorId, Text = "初始文本" };
        var text = UiShowcaseWidgets.Label(string.Empty, OneWayTextId);
        var count = UiShowcaseWidgets.Label(string.Empty, OneWayCountId);
        text.Bind(Text.ContentProperty, editor, TextBox.TextProperty);
        count.Bind(Text.ContentProperty, editor, TextBox.TextProperty, static value => $"字符数：{value.Length}");

        var content = UiShowcaseWidgets.Column(
            UiShowcaseWidgets.Sample("源文本", editor),
            UiShowcaseWidgets.Preview(UiShowcaseWidgets.Caption("实时显示"), text, count));
        return new UiShowcaseExample(
            "单向绑定",
            "直接编辑源文本，预览与字符数立即同步。",
            content);
    }

    private static UiShowcaseExample CreateTwoWayExample()
    {
        var model = new ShowcaseBindingModel { Message = "可编辑文本" };
        var editor = new TextBox
        {
            StyleId = TwoWayEditorId,
            Placeholder = "编辑文本"
        };
        var peer = new TextBox { StyleId = TwoWayPeerId, Placeholder = "也可在此编辑" };
        var mirror = UiShowcaseWidgets.Label(string.Empty, TwoWayMirrorId);
        var status = UiShowcaseWidgets.Label("编辑框与镜像共享同一数据源。", TwoWayStatusId);
        editor.BindTwoWay(TextBox.TextProperty, model, source => source.Message);
        peer.BindTwoWay(TextBox.TextProperty, model, source => source.Message);
        mirror.Bind(Text.ContentProperty, model, source => source.Message);

        var content = UiShowcaseWidgets.Column(
            UiShowcaseWidgets.Samples(
                UiShowcaseWidgets.Sample("输入 A", editor),
                UiShowcaseWidgets.Sample("输入 B", peer)),
            UiShowcaseWidgets.Preview(UiShowcaseWidgets.Caption("共享内容"), mirror),
            status);
        return new UiShowcaseExample(
            "双向绑定",
            "任一输入框的编辑都会同步到另一输入框与预览。",
            content);
    }

    private static UiShowcaseExample CreateComputedExample(Action<string> report)
    {
        var model = new ShowcaseBindingModel { Message = "初始", Count = 0 };
        var editor = new TextBox { StyleId = ComputedEditorId };
        editor.BindTwoWay(TextBox.TextProperty, model, source => source.Message);
        var summary = UiShowcaseWidgets.Label(string.Empty, ComputedSummaryId);
        summary.Bind(Text.ContentProperty, model, source => $"{source.Message} / {source.Count}");

        var incrementButton = UiShowcaseWidgets.ActionButton(ComputedIncrementId, "计数 +1", () =>
        {
            model.Count++;
            report($"计算绑定：计数 {model.Count}");
        });
        var content = UiShowcaseWidgets.Column(
            UiShowcaseWidgets.Sample("文本", editor),
            incrementButton,
            UiShowcaseWidgets.Preview(UiShowcaseWidgets.Caption("组合结果"), summary));
        return new UiShowcaseExample(
            "计算绑定",
            "编辑文本或增加计数，组合结果自动重新计算。",
            content);
    }

    private sealed class ShowcaseBindingModel : INotifyPropertyChanged
    {
        private string _message = string.Empty;
        private int _count;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Message
        {
            get => _message;
            set
            {
                if (string.Equals(_message, value, StringComparison.Ordinal))
                    return;
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public int Count
        {
            get => _count;
            set
            {
                if (_count == value)
                    return;
                _count = value;
                OnPropertyChanged(nameof(Count));
            }
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
