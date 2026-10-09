using PanguEngine.Client.UI;
using PanguEngine.Client.UI.Controls;
using PanguEngine.Client.UI.Drawing;
using Rectangle = PanguEngine.Client.UI.Controls.Rectangle;

namespace PanguEngine.Client.Screens.Showcase;

internal static class UiShowcaseRoundedRectangles
{
    internal static UiShowcaseExample[] CreateExamples(Action<string> report)
    {
        var image = CreatePatternImage();
        return [CreateCornersExample(), CreateBrushesExample(image), CreateShapeExample(), CreateClipExample(image, report)];
    }

    private static UiShowcaseExample CreateCornersExample()
    {
        var square = Surface(CornerRadius.Zero);
        var uniform = Surface(new CornerRadius(24));
        var asymmetric = Surface(CornerRadius.Zero);
        asymmetric.Classes.Add("showcase-rounded-asymmetric");
        asymmetric.ClearValue(Region.CornerRadiusProperty);
        var unequal = Surface(new CornerRadius(28));
        unequal.BorderThickness = new Thickness(4, 12, 18, 6);
        var oversized = Surface(new CornerRadius(100));
        var translucent = Surface(new CornerRadius(24));
        translucent.Background = null;
        translucent.BorderBrush = new SolidColorBrush(79, 141, 245, 128);
        translucent.BorderThickness = new Thickness(12);
        return new UiShowcaseExample(
            "圆角背景与边框",
            "比较直角、统一半径、四角独立半径、不等边厚和半透明边框。过大半径按比例缩小。",
            UiShowcaseWidgets.Samples(
                UiShowcaseWidgets.Sample("直角", square),
                UiShowcaseWidgets.Sample("统一半径 24", uniform),
                UiShowcaseWidgets.Sample("CSS 四角：4、28、12、40", asymmetric),
                UiShowcaseWidgets.Sample("不等边厚", unequal),
                UiShowcaseWidgets.Sample("半径 100", oversized),
                UiShowcaseWidgets.Sample("透明背景 · 半透明边框", translucent)));
    }

    private static UiShowcaseExample CreateBrushesExample(UiImage image)
    {
        var imageBackground = Surface(new CornerRadius(28));
        imageBackground.Background = new ImageBrush(image, ImageStretch.UniformToFill);
        var imageBorder = Surface(new CornerRadius(28));
        imageBorder.BorderBrush = new ImageBrush(image);
        imageBorder.BorderThickness = new Thickness(12);
        var nineSlice = new NineSliceImageBrush(image, new ImageSlice(8));
        var nineBackground = Surface(new CornerRadius(28));
        nineBackground.Background = nineSlice;
        var nineBorder = Surface(new CornerRadius(28));
        nineBorder.BorderBrush = nineSlice;
        nineBorder.BorderThickness = new Thickness(12);
        return new UiShowcaseExample(
            "图像画刷",
            "同一张棋盘渐变图用于普通图片和九宫格。观察外角裁切、边框内孔以及缩放后的接缝。",
            UiShowcaseWidgets.Samples(
                UiShowcaseWidgets.Sample("图片背景", imageBackground),
                UiShowcaseWidgets.Sample("图片边框", imageBorder),
                UiShowcaseWidgets.Sample("九宫格背景", nineBackground),
                UiShowcaseWidgets.Sample("九宫格边框", nineBorder)));
    }

    private static UiShowcaseExample CreateShapeExample()
    {
        var filled = Shape(28, 12);
        var outlined = Shape(28, 12);
        outlined.Fill = null;
        var radiusValue = UiShowcaseWidgets.Caption("横半径 28 · 纵半径 12");
        var presets = new (double X, double Y)[] { (28, 12), (12, 28), (0, 0), (48, 48) };
        var index = 0;
        var change = UiShowcaseWidgets.ActionButton("showcase-rounded-shape-cycle", "切换形状半径", () =>
        {
            index = (index + 1) % presets.Length;
            var (x, y) = presets[index];
            filled.RadiusX = outlined.RadiusX = x;
            filled.RadiusY = outlined.RadiusY = y;
            radiusValue.Content = $"横半径 {x} · 纵半径 {y}";
        });
        return new UiShowcaseExample(
            "圆角矩形形状",
            "横纵半径可分别设置。切换半径比较椭圆角、直角与胶囊形，以及填充和描边的变化。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("填充与描边", filled),
                    UiShowcaseWidgets.Sample("仅描边", outlined)),
                radiusValue, change));
    }

    private static UiShowcaseExample CreateClipExample(UiImage image, Action<string> report)
    {
        var clicks = 0;
        var feedback = UiShowcaseWidgets.Caption("角块点击次数：0");
        var open = ClipSurface(false, image, () => Click("左侧"));
        var clipped = ClipSurface(true, image, () => Click("右侧"));
        var state = UiShowcaseWidgets.Caption("右侧裁剪：开启");
        var toggle = UiShowcaseWidgets.ActionButton("showcase-rounded-clip-toggle", "切换右侧裁剪", () =>
        {
            clipped.ClipToBounds = !clipped.ClipToBounds;
            state.Content = $"右侧裁剪：{(clipped.ClipToBounds ? "开启" : "关闭")}";
        });
        return new UiShowcaseExample(
            "后代内容裁剪",
            "左侧关闭裁剪，右侧默认开启。点击黄色角块，再切换右侧裁剪，比较圆角外内容的显示和点击范围。",
            UiShowcaseWidgets.Column(
                UiShowcaseWidgets.Samples(
                    UiShowcaseWidgets.Sample("关闭裁剪", open),
                    UiShowcaseWidgets.Sample("可切换裁剪", clipped)),
                state, toggle, feedback));

        void Click(string side)
        {
            feedback.Content = $"角块点击次数：{++clicks}（{side}）";
            report($"圆角示例：{side}角块收到点击");
        }
    }

    private static Panel Surface(CornerRadius radius) => new()
    {
        Width = 160,
        Height = 96,
        CornerRadius = radius,
        Background = new SolidColorBrush(32, 53, 83),
        BorderBrush = new SolidColorBrush(79, 141, 245),
        BorderThickness = new Thickness(6)
    };

    private static Rectangle Shape(double x, double y) => new()
    {
        Width = 160,
        Height = 96,
        RadiusX = x,
        RadiusY = y,
        Fill = new SolidColorBrush(32, 53, 83),
        Stroke = new SolidColorBrush(79, 141, 245),
        StrokeThickness = 4
    };

    private static Canvas ClipSurface(bool clip, UiImage image, Action click)
    {
        var host = new Canvas
        {
            Width = 160,
            Height = 96,
            CornerRadius = new CornerRadius(44),
            ClipToBounds = clip
        };
        host.Children.Add(new ImageView { Source = image, Stretch = ImageStretch.Fill, Width = 160, Height = 96 });
        foreach (var (x, y) in new[] { (0d, 0d), (136d, 0d), (0d, 72d), (136d, 72d) })
        {
            var corner = new Button
            {
                Text = "+",
                Width = 24,
                Height = 24,
                MinHeight = 0,
                Padding = Thickness.Zero,
                BorderThickness = Thickness.Zero,
                Background = new SolidColorBrush(255, 180, 84),
                Foreground = new Color(26, 32, 43)
            };
            corner.Click += (_, _) => click();
            Canvas.SetLeft(corner, x);
            Canvas.SetTop(corner, y);
            host.Children.Add(corner);
        }
        return host;
    }

    private static UiImage CreatePatternImage()
    {
        const int size = 48;
        var pixels = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var offset = (y * size + x) * 4;
            pixels[offset] = (byte)(40 + x * 180 / (size - 1));
            pixels[offset + 1] = (byte)(40 + y * 180 / (size - 1));
            pixels[offset + 2] = (byte)((x / 8 + y / 8) % 2 == 0 ? 220 : 90);
            pixels[offset + 3] = 255;
        }
        return UiImage.FromRgba(pixels, size, size);
    }
}
