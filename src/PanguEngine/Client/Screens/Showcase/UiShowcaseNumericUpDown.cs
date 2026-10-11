using System.Globalization;
using PanguEngine.Client.UI.Controls;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>Builds numeric editing samples with value feedback and editable state comparisons.</summary>
internal static class UiShowcaseNumericUpDown
{
    /// <summary>Creates the numeric control showcase card.</summary>
    /// <returns>The interactive numeric editing example.</returns>
    internal static UiShowcaseExample CreateExample()
    {
        var integer = new NumericUpDown<int>
        {
            StyleId = "showcase-controls-numeric-integer",
            Minimum = -20,
            Maximum = 100,
            Value = 10
        };
        var feedback = UiShowcaseWidgets.Label(string.Empty, "showcase-controls-numeric-feedback");
        feedback.Bind(Text.ContentProperty, integer, NumericUpDown<int>.ValueProperty,
            static value => $"当前数值：{value}");
        var assign = UiShowcaseWidgets.ActionButton("showcase-controls-numeric-assign", "外部设置为 25",
            () => integer.Value = 25);
        var fraction = new NumericUpDown<decimal>
        {
            StyleId = "showcase-controls-numeric-decimal",
            Minimum = -10,
            Maximum = 10,
            Increment = 0.125m,
            Value = 1.234m,
            FormatString = "F2",
            FormatProvider = CultureInfo.InvariantCulture
        };
        var culture = new NumericUpDown<decimal>
        {
            StyleId = "showcase-controls-numeric-culture",
            Value = 12.5m,
            Increment = 0.5m,
            FormatString = "F2",
            FormatProvider = CultureInfo.GetCultureInfo("de-DE")
        };
        var readOnly = new NumericUpDown<int>
        {
            StyleId = "showcase-controls-numeric-readonly",
            Value = 25,
            IsReadOnly = true
        };
        var disabled = new NumericUpDown<int>
        {
            StyleId = "showcase-controls-numeric-disabled",
            Value = 25,
            IsEnabled = false
        };
        return new UiShowcaseExample(
            "数值输入",
            "输入有效数值会实时更新。Enter 或离开控件确认；Escape 恢复当前数值。上下键、长按箭头和聚焦后的滚轮可调值。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("整数：-20 至 100", integer),
                    UiShowcaseWidgets.Sample("小数：步长 0.125，显示两位", fraction),
                    UiShowcaseWidgets.Sample("德语格式：逗号小数点", culture)),
                UiShowcaseWidgets.Preview(feedback, assign),
                UiShowcaseWidgets.Caption("小数示例只调整显示精度。输入越界数值后按 Enter 或点击箭头观察夹取；外部赋值按钮将整数设置为 25。"),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("只读：可选择和复制", readOnly),
                    UiShowcaseWidgets.Sample("禁用", disabled))));
    }
}
