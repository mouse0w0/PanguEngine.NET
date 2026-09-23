using System.ComponentModel;
using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.Screens;

internal static class UiShowcaseBindings
{
    internal const string OneWayAdvanceId = "showcase-bindings-oneway-advance";
    internal const string OneWayTextId = "showcase-bindings-oneway-text";
    internal const string OneWayCountId = "showcase-bindings-oneway-count";

    internal const string TwoWayEditorId = "showcase-bindings-twoway-editor";
    internal const string TwoWayPresetId = "showcase-bindings-twoway-preset";
    internal const string TwoWayMirrorId = "showcase-bindings-twoway-mirror";
    internal const string TwoWayStatusId = "showcase-bindings-twoway-status";

    internal const string ComputedIncrementId = "showcase-bindings-computed-increment";
    internal const string ComputedMessageId = "showcase-bindings-computed-message";
    internal const string ComputedSummaryId = "showcase-bindings-computed-summary";

    internal static UiShowcaseExample[] CreateExamples(Action<string> report)
    {
        return
        [
            CreateOneWayExample(report),
            CreateTwoWayExample(report),
            CreateComputedExample(report)
        ];
    }

    private static UiShowcaseExample CreateOneWayExample(Action<string> report)
    {
        var model = new ShowcaseBindingModel { Message = "初始文本" };
        var text = UiShowcaseWidgets.Label(string.Empty, OneWayTextId);
        var count = UiShowcaseWidgets.Label(string.Empty, OneWayCountId);
        text.Bind(Text.ContentProperty, model, source => source.Message);
        count.Bind(Text.ContentProperty, model, source => $"更新 {source.Updates} 次");

        var advanceButton = UiShowcaseWidgets.ActionButton(OneWayAdvanceId, "更新数据源", () =>
        {
            model.Updates++;
            model.Message = $"文本 {model.Updates}";
            report($"单向绑定：{model.Message}");
        });

        var content = UiShowcaseWidgets.Column(
            advanceButton,
            UiShowcaseWidgets.Row(UiShowcaseWidgets.Label("目标文本："), text),
            UiShowcaseWidgets.Row(UiShowcaseWidgets.Label("更新次数："), count));
        return new UiShowcaseExample(
            "单向绑定",
            "点击按钮更新数据源，目标 Text 立即同步，并显示更新次数。",
            content);
    }

    private static UiShowcaseExample CreateTwoWayExample(Action<string> report)
    {
        var model = new ShowcaseBindingModel { Message = "可编辑文本" };
        var editor = new TextBox
        {
            StyleId = TwoWayEditorId,
            Placeholder = "编辑文本",
            Width = 320
        };
        var mirror = UiShowcaseWidgets.Label(string.Empty, TwoWayMirrorId);
        var status = UiShowcaseWidgets.Label("编辑框与镜像共享同一数据源。", TwoWayStatusId);
        editor.BindTwoWay(TextBox.TextProperty, model, source => source.Message);
        mirror.Bind(Text.ContentProperty, model, source => source.Message);

        var presetButton = UiShowcaseWidgets.ActionButton(TwoWayPresetId, "写入预设值", () =>
        {
            model.Message = "预设值";
            report($"双向绑定：源已写入 {model.Message}");
        });

        var content = UiShowcaseWidgets.Column(
            editor,
            presetButton,
            UiShowcaseWidgets.Column(UiShowcaseWidgets.Label("镜像："), mirror),
            status);
        return new UiShowcaseExample(
            "双向绑定",
            "编辑文本框回写数据源，按钮写入预设值后编辑框与镜像同步。",
            content);
    }

    private static UiShowcaseExample CreateComputedExample(Action<string> report)
    {
        var model = new ShowcaseBindingModel { Message = "初始", Count = 0 };
        var summary = UiShowcaseWidgets.Label(string.Empty, ComputedSummaryId);
        summary.Bind(Text.ContentProperty, model, source => $"{source.Message} / {source.Count}");

        var incrementButton = UiShowcaseWidgets.ActionButton(ComputedIncrementId, "计数 +1", () =>
        {
            model.Count++;
            report($"计算绑定：{model.Message} / {model.Count}");
        });
        var messageButton = UiShowcaseWidgets.ActionButton(ComputedMessageId, "切换短文本", () =>
        {
            model.Message = model.Message == "初始" ? "已修改" : "初始";
            report($"计算绑定：{model.Message} / {model.Count}");
        });

        var content = UiShowcaseWidgets.Column(
            incrementButton,
            messageButton,
            UiShowcaseWidgets.Row(UiShowcaseWidgets.Label("组合说明："), summary));
        return new UiShowcaseExample(
            "计算绑定",
            "修改短文本或计数，组合说明自动重新计算。",
            content);
    }

    private sealed class ShowcaseBindingModel : INotifyPropertyChanged
    {
        private string _message = string.Empty;
        private int _count;
        private int _updates;

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

        public int Updates
        {
            get => _updates;
            set
            {
                if (_updates == value)
                    return;
                _updates = value;
                OnPropertyChanged(nameof(Updates));
            }
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
