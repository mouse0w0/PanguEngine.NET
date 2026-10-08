using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;

namespace PanguEngine.Client.Screens.Showcase;

/// <summary>
/// Builds comparable style, cascade and image brush samples.
/// </summary>
internal static class UiShowcaseStyles
{
    /// <summary>
    /// Creates the ordered style gallery examples.
    /// </summary>
    /// <returns>The style examples.</returns>
    internal static UiShowcaseExample[] CreateExamples() =>
    [
        CreateAuthorLocalExample(),
        CreateSelectorExample(),
        CreatePaletteExample(),
        CreateNineSliceExample()
    ];

    private static UiShowcaseExample CreateAuthorLocalExample()
    {
        var basis = AuthorButton("showcase-author-target", "基础样式");
        var overridden = AuthorButton("showcase-author-override", "覆盖样式");
        overridden.Classes.Add("showcase-author-override");
        var local = AuthorButton("showcase-author-local", "本地值");
        local.SetValue(Region.BackgroundProperty, new SolidColorBrush(0x3F, 0x8F, 0x4F));
        return new UiShowcaseExample(
            "Author 覆盖与本地值",
            "基础规则、额外样式表与控件本地值的结果同时展示。",
            UiShowcaseWidgets.Samples(
                UiShowcaseWidgets.Sample("基础规则", basis),
                UiShowcaseWidgets.Sample("额外规则覆盖", overridden),
                UiShowcaseWidgets.Sample("本地值优先", local)));
    }

    private static Button AuthorButton(string id, string text)
    {
        var button = new Button { StyleId = id, Text = text };
        button.Classes.Add("showcase-author-target");
        return button;
    }

    private static UiShowcaseExample CreateSelectorExample()
    {
        var plain = new Button { StyleId = "showcase-selector-plain", Text = "默认" };
        var typed = new Button { StyleId = "showcase-selector-typed", Text = "类型" };
        typed.Classes.Add("showcase-typed");
        var classOnly = new Button { StyleId = "showcase-selector-classonly", Text = "类" };
        classOnly.Classes.Add("showcase-classonly");
        var idTarget = new Button { StyleId = "showcase-id-target", Text = "id" };
        var wildcard = new Button { StyleId = "showcase-selector-wildcard", Text = "通配" };
        wildcard.Classes.Add("showcase-wildcard");
        return new UiShowcaseExample(
            "class、id 与通配选择器",
            "不同规则作用于独立样本，可直接比较匹配结果。",
            UiShowcaseWidgets.Samples(
                UiShowcaseWidgets.Sample("默认类型", plain),
                UiShowcaseWidgets.Sample("类型 + class", typed),
                UiShowcaseWidgets.Sample("class", classOnly),
                UiShowcaseWidgets.Sample("id", idTarget),
                UiShowcaseWidgets.Sample("通配 + class", wildcard)));
    }

    internal static UiShowcaseExample CreateComboStateExample()
    {
        var button = new Button { StyleId = "showcase-combo-target", Text = "悬停并点击聚焦" };
        button.Classes.Add("showcase-combo");
        return new UiShowcaseExample(
            "状态组合伪类",
            "同时悬停与聚焦时显示强调色，移开鼠标后恢复。",
            UiShowcaseWidgets.Preview(button));
    }

    private static UiShowcaseExample CreatePaletteExample()
    {
        var palette = new[]
        {
            ("强调蓝", new Color(79, 141, 245)),
            ("成功绿", new Color(63, 143, 79)),
            ("提示黄", new Color(255, 180, 84)),
            ("危险红", new Color(157, 73, 77)),
            ("卡片背景", new Color(26, 32, 43)),
            ("边框", new Color(47, 57, 73))
        };
        return new UiShowcaseExample(
            "配色与表面",
            "统一强调色、语义色与深色表面层次。",
            UiShowcaseWidgets.Samples(palette.Select(entry => UiShowcaseWidgets.Sample(entry.Item1,
                UiShowcaseWidgets.Column(
                    new Panel { Height = 36, Background = new SolidColorBrush(entry.Item2) },
                    UiShowcaseWidgets.Caption($"#{entry.Item2.R:X2}{entry.Item2.G:X2}{entry.Item2.B:X2}")))).ToArray()));
    }

    internal static UiShowcaseExample CreateTypographyExample()
    {
        var heading = UiShowcaseWidgets.Label("页面标题 · Heading");
        heading.Classes.Add("showcase-type-heading");
        var section = UiShowcaseWidgets.Label("分组标题 · Section");
        section.Classes.Add("showcase-type-section");
        var body = UiShowcaseWidgets.Label("正文用于说明内容和引导操作，保持清晰的阅读层次。");
        body.Classes.Add("showcase-type-body");
        return new UiShowcaseExample(
            "文本",
            "标题、正文和辅助信息使用不同字号与颜色。",
            UiShowcaseWidgets.Preview(heading, section, body, UiShowcaseWidgets.Caption("辅助说明 · Caption · 0123456789")));
    }

    private static UiShowcaseExample CreateNineSliceExample()
    {
        var source = CreateFrameImage();
        var brush = new NineSliceImageBrush(source, new ImageSlice(4));
        var small = new Panel { StyleId = "showcase-nine-slice-small", Width = 72, Height = 48, Background = brush };
        var wide = new Panel { StyleId = "showcase-nine-slice-wide", MaxWidth = 180, Height = 48, Background = brush };
        var tall = new Panel { StyleId = "showcase-nine-slice-tall", Width = 80, Height = 100, Background = brush };
        return new UiShowcaseExample(
            "九宫格图像画刷",
            "蓝色角部固定，绿色横边与紫色竖边随尺寸拉伸，中心填充剩余空间。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Sample("原图（放大）", new ImageView { Source = source, Stretch = ImageStretch.Fill, Width = 64, Height = 64 }),
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("小尺寸", small),
                    UiShowcaseWidgets.Sample("横向拉伸", wide),
                    UiShowcaseWidgets.Sample("纵向拉伸", tall))));
    }

    private static UiImage CreateFrameImage()
    {
        const int size = 16;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var horizontalEdge = y < 4 || y >= 12;
            var verticalEdge = x < 4 || x >= 12;
            var color = (horizontalEdge, verticalEdge) switch
            {
                (true, true) => new Color(79, 141, 245),
                (true, false) => new Color(63, 143, 79),
                (false, true) => new Color(139, 107, 191),
                _ => new Color(26, 32, 43)
            };
            var offset = (y * size + x) * 4;
            pixels[offset] = color.R;
            pixels[offset + 1] = color.G;
            pixels[offset + 2] = color.B;
            pixels[offset + 3] = color.A;
        }
        return UiImage.FromRgba(pixels, size, size);
    }
}
