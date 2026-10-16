using System.Collections.Frozen;

namespace PanguEngine.Client.UI.Drawing;

public readonly partial record struct Color
{
    /// <summary>Gets the standard aliceblue color.</summary>
    public static Color AliceBlue => new(0xF0, 0xF8, 0xFF);

    /// <summary>Gets the standard antiquewhite color.</summary>
    public static Color AntiqueWhite => new(0xFA, 0xEB, 0xD7);

    /// <summary>Gets the standard aqua color.</summary>
    public static Color Aqua => new(0x00, 0xFF, 0xFF);

    /// <summary>Gets the standard aquamarine color.</summary>
    public static Color Aquamarine => new(0x7F, 0xFF, 0xD4);

    /// <summary>Gets the standard azure color.</summary>
    public static Color Azure => new(0xF0, 0xFF, 0xFF);

    /// <summary>Gets the standard beige color.</summary>
    public static Color Beige => new(0xF5, 0xF5, 0xDC);

    /// <summary>Gets the standard bisque color.</summary>
    public static Color Bisque => new(0xFF, 0xE4, 0xC4);

    /// <summary>Gets the standard black color.</summary>
    public static Color Black => new(0x00, 0x00, 0x00);

    /// <summary>Gets the standard blanchedalmond color.</summary>
    public static Color BlanchedAlmond => new(0xFF, 0xEB, 0xCD);

    /// <summary>Gets the standard blue color.</summary>
    public static Color Blue => new(0x00, 0x00, 0xFF);

    /// <summary>Gets the standard blueviolet color.</summary>
    public static Color BlueViolet => new(0x8A, 0x2B, 0xE2);

    /// <summary>Gets the standard brown color.</summary>
    public static Color Brown => new(0xA5, 0x2A, 0x2A);

    /// <summary>Gets the standard burlywood color.</summary>
    public static Color BurlyWood => new(0xDE, 0xB8, 0x87);

    /// <summary>Gets the standard cadetblue color.</summary>
    public static Color CadetBlue => new(0x5F, 0x9E, 0xA0);

    /// <summary>Gets the standard chartreuse color.</summary>
    public static Color Chartreuse => new(0x7F, 0xFF, 0x00);

    /// <summary>Gets the standard chocolate color.</summary>
    public static Color Chocolate => new(0xD2, 0x69, 0x1E);

    /// <summary>Gets the standard coral color.</summary>
    public static Color Coral => new(0xFF, 0x7F, 0x50);

    /// <summary>Gets the standard cornflowerblue color.</summary>
    public static Color CornflowerBlue => new(0x64, 0x95, 0xED);

    /// <summary>Gets the standard cornsilk color.</summary>
    public static Color Cornsilk => new(0xFF, 0xF8, 0xDC);

    /// <summary>Gets the standard crimson color.</summary>
    public static Color Crimson => new(0xDC, 0x14, 0x3C);

    /// <summary>Gets the standard cyan color, an alias of aqua.</summary>
    public static Color Cyan => Aqua;

    /// <summary>Gets the standard darkblue color.</summary>
    public static Color DarkBlue => new(0x00, 0x00, 0x8B);

    /// <summary>Gets the standard darkcyan color.</summary>
    public static Color DarkCyan => new(0x00, 0x8B, 0x8B);

    /// <summary>Gets the standard darkgoldenrod color.</summary>
    public static Color DarkGoldenrod => new(0xB8, 0x86, 0x0B);

    /// <summary>Gets the standard darkgray color.</summary>
    public static Color DarkGray => new(0xA9, 0xA9, 0xA9);

    /// <summary>Gets the standard darkgreen color.</summary>
    public static Color DarkGreen => new(0x00, 0x64, 0x00);

    /// <summary>Gets the standard darkgrey color, an alias of darkgray.</summary>
    public static Color DarkGrey => DarkGray;

    /// <summary>Gets the standard darkkhaki color.</summary>
    public static Color DarkKhaki => new(0xBD, 0xB7, 0x6B);

    /// <summary>Gets the standard darkmagenta color.</summary>
    public static Color DarkMagenta => new(0x8B, 0x00, 0x8B);

    /// <summary>Gets the standard darkolivegreen color.</summary>
    public static Color DarkOliveGreen => new(0x55, 0x6B, 0x2F);

    /// <summary>Gets the standard darkorange color.</summary>
    public static Color DarkOrange => new(0xFF, 0x8C, 0x00);

    /// <summary>Gets the standard darkorchid color.</summary>
    public static Color DarkOrchid => new(0x99, 0x32, 0xCC);

    /// <summary>Gets the standard darkred color.</summary>
    public static Color DarkRed => new(0x8B, 0x00, 0x00);

    /// <summary>Gets the standard darksalmon color.</summary>
    public static Color DarkSalmon => new(0xE9, 0x96, 0x7A);

    /// <summary>Gets the standard darkseagreen color.</summary>
    public static Color DarkSeaGreen => new(0x8F, 0xBC, 0x8F);

    /// <summary>Gets the standard darkslateblue color.</summary>
    public static Color DarkSlateBlue => new(0x48, 0x3D, 0x8B);

    /// <summary>Gets the standard darkslategray color.</summary>
    public static Color DarkSlateGray => new(0x2F, 0x4F, 0x4F);

    /// <summary>Gets the standard darkslategrey color, an alias of darkslategray.</summary>
    public static Color DarkSlateGrey => DarkSlateGray;

    /// <summary>Gets the standard darkturquoise color.</summary>
    public static Color DarkTurquoise => new(0x00, 0xCE, 0xD1);

    /// <summary>Gets the standard darkviolet color.</summary>
    public static Color DarkViolet => new(0x94, 0x00, 0xD3);

    /// <summary>Gets the standard deeppink color.</summary>
    public static Color DeepPink => new(0xFF, 0x14, 0x93);

    /// <summary>Gets the standard deepskyblue color.</summary>
    public static Color DeepSkyBlue => new(0x00, 0xBF, 0xFF);

    /// <summary>Gets the standard dimgray color.</summary>
    public static Color DimGray => new(0x69, 0x69, 0x69);

    /// <summary>Gets the standard dimgrey color, an alias of dimgray.</summary>
    public static Color DimGrey => DimGray;

    /// <summary>Gets the standard dodgerblue color.</summary>
    public static Color DodgerBlue => new(0x1E, 0x90, 0xFF);

    /// <summary>Gets the standard firebrick color.</summary>
    public static Color Firebrick => new(0xB2, 0x22, 0x22);

    /// <summary>Gets the standard floralwhite color.</summary>
    public static Color FloralWhite => new(0xFF, 0xFA, 0xF0);

    /// <summary>Gets the standard forestgreen color.</summary>
    public static Color ForestGreen => new(0x22, 0x8B, 0x22);

    /// <summary>Gets the standard fuchsia color.</summary>
    public static Color Fuchsia => new(0xFF, 0x00, 0xFF);

    /// <summary>Gets the standard gainsboro color.</summary>
    public static Color Gainsboro => new(0xDC, 0xDC, 0xDC);

    /// <summary>Gets the standard ghostwhite color.</summary>
    public static Color GhostWhite => new(0xF8, 0xF8, 0xFF);

    /// <summary>Gets the standard gold color.</summary>
    public static Color Gold => new(0xFF, 0xD7, 0x00);

    /// <summary>Gets the standard goldenrod color.</summary>
    public static Color Goldenrod => new(0xDA, 0xA5, 0x20);

    /// <summary>Gets the standard gray color.</summary>
    public static Color Gray => new(0x80, 0x80, 0x80);

    /// <summary>Gets the standard green color.</summary>
    public static Color Green => new(0x00, 0x80, 0x00);

    /// <summary>Gets the standard greenyellow color.</summary>
    public static Color GreenYellow => new(0xAD, 0xFF, 0x2F);

    /// <summary>Gets the standard grey color, an alias of gray.</summary>
    public static Color Grey => Gray;

    /// <summary>Gets the standard honeydew color.</summary>
    public static Color Honeydew => new(0xF0, 0xFF, 0xF0);

    /// <summary>Gets the standard hotpink color.</summary>
    public static Color HotPink => new(0xFF, 0x69, 0xB4);

    /// <summary>Gets the standard indianred color.</summary>
    public static Color IndianRed => new(0xCD, 0x5C, 0x5C);

    /// <summary>Gets the standard indigo color.</summary>
    public static Color Indigo => new(0x4B, 0x00, 0x82);

    /// <summary>Gets the standard ivory color.</summary>
    public static Color Ivory => new(0xFF, 0xFF, 0xF0);

    /// <summary>Gets the standard khaki color.</summary>
    public static Color Khaki => new(0xF0, 0xE6, 0x8C);

    /// <summary>Gets the standard lavender color.</summary>
    public static Color Lavender => new(0xE6, 0xE6, 0xFA);

    /// <summary>Gets the standard lavenderblush color.</summary>
    public static Color LavenderBlush => new(0xFF, 0xF0, 0xF5);

    /// <summary>Gets the standard lawngreen color.</summary>
    public static Color LawnGreen => new(0x7C, 0xFC, 0x00);

    /// <summary>Gets the standard lemonchiffon color.</summary>
    public static Color LemonChiffon => new(0xFF, 0xFA, 0xCD);

    /// <summary>Gets the standard lightblue color.</summary>
    public static Color LightBlue => new(0xAD, 0xD8, 0xE6);

    /// <summary>Gets the standard lightcoral color.</summary>
    public static Color LightCoral => new(0xF0, 0x80, 0x80);

    /// <summary>Gets the standard lightcyan color.</summary>
    public static Color LightCyan => new(0xE0, 0xFF, 0xFF);

    /// <summary>Gets the standard lightgoldenrodyellow color.</summary>
    public static Color LightGoldenrodYellow => new(0xFA, 0xFA, 0xD2);

    /// <summary>Gets the standard lightgray color.</summary>
    public static Color LightGray => new(0xD3, 0xD3, 0xD3);

    /// <summary>Gets the standard lightgreen color.</summary>
    public static Color LightGreen => new(0x90, 0xEE, 0x90);

    /// <summary>Gets the standard lightgrey color, an alias of lightgray.</summary>
    public static Color LightGrey => LightGray;

    /// <summary>Gets the standard lightpink color.</summary>
    public static Color LightPink => new(0xFF, 0xB6, 0xC1);

    /// <summary>Gets the standard lightsalmon color.</summary>
    public static Color LightSalmon => new(0xFF, 0xA0, 0x7A);

    /// <summary>Gets the standard lightseagreen color.</summary>
    public static Color LightSeaGreen => new(0x20, 0xB2, 0xAA);

    /// <summary>Gets the standard lightskyblue color.</summary>
    public static Color LightSkyBlue => new(0x87, 0xCE, 0xFA);

    /// <summary>Gets the standard lightslategray color.</summary>
    public static Color LightSlateGray => new(0x77, 0x88, 0x99);

    /// <summary>Gets the standard lightslategrey color, an alias of lightslategray.</summary>
    public static Color LightSlateGrey => LightSlateGray;

    /// <summary>Gets the standard lightsteelblue color.</summary>
    public static Color LightSteelBlue => new(0xB0, 0xC4, 0xDE);

    /// <summary>Gets the standard lightyellow color.</summary>
    public static Color LightYellow => new(0xFF, 0xFF, 0xE0);

    /// <summary>Gets the standard lime color.</summary>
    public static Color Lime => new(0x00, 0xFF, 0x00);

    /// <summary>Gets the standard limegreen color.</summary>
    public static Color LimeGreen => new(0x32, 0xCD, 0x32);

    /// <summary>Gets the standard linen color.</summary>
    public static Color Linen => new(0xFA, 0xF0, 0xE6);

    /// <summary>Gets the standard magenta color, an alias of fuchsia.</summary>
    public static Color Magenta => Fuchsia;

    /// <summary>Gets the standard maroon color.</summary>
    public static Color Maroon => new(0x80, 0x00, 0x00);

    /// <summary>Gets the standard mediumaquamarine color.</summary>
    public static Color MediumAquamarine => new(0x66, 0xCD, 0xAA);

    /// <summary>Gets the standard mediumblue color.</summary>
    public static Color MediumBlue => new(0x00, 0x00, 0xCD);

    /// <summary>Gets the standard mediumorchid color.</summary>
    public static Color MediumOrchid => new(0xBA, 0x55, 0xD3);

    /// <summary>Gets the standard mediumpurple color.</summary>
    public static Color MediumPurple => new(0x93, 0x70, 0xDB);

    /// <summary>Gets the standard mediumseagreen color.</summary>
    public static Color MediumSeaGreen => new(0x3C, 0xB3, 0x71);

    /// <summary>Gets the standard mediumslateblue color.</summary>
    public static Color MediumSlateBlue => new(0x7B, 0x68, 0xEE);

    /// <summary>Gets the standard mediumspringgreen color.</summary>
    public static Color MediumSpringGreen => new(0x00, 0xFA, 0x9A);

    /// <summary>Gets the standard mediumturquoise color.</summary>
    public static Color MediumTurquoise => new(0x48, 0xD1, 0xCC);

    /// <summary>Gets the standard mediumvioletred color.</summary>
    public static Color MediumVioletRed => new(0xC7, 0x15, 0x85);

    /// <summary>Gets the standard midnightblue color.</summary>
    public static Color MidnightBlue => new(0x19, 0x19, 0x70);

    /// <summary>Gets the standard mintcream color.</summary>
    public static Color MintCream => new(0xF5, 0xFF, 0xFA);

    /// <summary>Gets the standard mistyrose color.</summary>
    public static Color MistyRose => new(0xFF, 0xE4, 0xE1);

    /// <summary>Gets the standard moccasin color.</summary>
    public static Color Moccasin => new(0xFF, 0xE4, 0xB5);

    /// <summary>Gets the standard navajowhite color.</summary>
    public static Color NavajoWhite => new(0xFF, 0xDE, 0xAD);

    /// <summary>Gets the standard navy color.</summary>
    public static Color Navy => new(0x00, 0x00, 0x80);

    /// <summary>Gets the standard oldlace color.</summary>
    public static Color OldLace => new(0xFD, 0xF5, 0xE6);

    /// <summary>Gets the standard olive color.</summary>
    public static Color Olive => new(0x80, 0x80, 0x00);

    /// <summary>Gets the standard olivedrab color.</summary>
    public static Color OliveDrab => new(0x6B, 0x8E, 0x23);

    /// <summary>Gets the standard orange color.</summary>
    public static Color Orange => new(0xFF, 0xA5, 0x00);

    /// <summary>Gets the standard orangered color.</summary>
    public static Color OrangeRed => new(0xFF, 0x45, 0x00);

    /// <summary>Gets the standard orchid color.</summary>
    public static Color Orchid => new(0xDA, 0x70, 0xD6);

    /// <summary>Gets the standard palegoldenrod color.</summary>
    public static Color PaleGoldenrod => new(0xEE, 0xE8, 0xAA);

    /// <summary>Gets the standard palegreen color.</summary>
    public static Color PaleGreen => new(0x98, 0xFB, 0x98);

    /// <summary>Gets the standard paleturquoise color.</summary>
    public static Color PaleTurquoise => new(0xAF, 0xEE, 0xEE);

    /// <summary>Gets the standard palevioletred color.</summary>
    public static Color PaleVioletRed => new(0xDB, 0x70, 0x93);

    /// <summary>Gets the standard papayawhip color.</summary>
    public static Color PapayaWhip => new(0xFF, 0xEF, 0xD5);

    /// <summary>Gets the standard peachpuff color.</summary>
    public static Color PeachPuff => new(0xFF, 0xDA, 0xB9);

    /// <summary>Gets the standard peru color.</summary>
    public static Color Peru => new(0xCD, 0x85, 0x3F);

    /// <summary>Gets the standard pink color.</summary>
    public static Color Pink => new(0xFF, 0xC0, 0xCB);

    /// <summary>Gets the standard plum color.</summary>
    public static Color Plum => new(0xDD, 0xA0, 0xDD);

    /// <summary>Gets the standard powderblue color.</summary>
    public static Color PowderBlue => new(0xB0, 0xE0, 0xE6);

    /// <summary>Gets the standard purple color.</summary>
    public static Color Purple => new(0x80, 0x00, 0x80);

    /// <summary>Gets the standard rebeccapurple color.</summary>
    public static Color RebeccaPurple => new(0x66, 0x33, 0x99);

    /// <summary>Gets the standard red color.</summary>
    public static Color Red => new(0xFF, 0x00, 0x00);

    /// <summary>Gets the standard rosybrown color.</summary>
    public static Color RosyBrown => new(0xBC, 0x8F, 0x8F);

    /// <summary>Gets the standard royalblue color.</summary>
    public static Color RoyalBlue => new(0x41, 0x69, 0xE1);

    /// <summary>Gets the standard saddlebrown color.</summary>
    public static Color SaddleBrown => new(0x8B, 0x45, 0x13);

    /// <summary>Gets the standard salmon color.</summary>
    public static Color Salmon => new(0xFA, 0x80, 0x72);

    /// <summary>Gets the standard sandybrown color.</summary>
    public static Color SandyBrown => new(0xF4, 0xA4, 0x60);

    /// <summary>Gets the standard seagreen color.</summary>
    public static Color SeaGreen => new(0x2E, 0x8B, 0x57);

    /// <summary>Gets the standard seashell color.</summary>
    public static Color SeaShell => new(0xFF, 0xF5, 0xEE);

    /// <summary>Gets the standard sienna color.</summary>
    public static Color Sienna => new(0xA0, 0x52, 0x2D);

    /// <summary>Gets the standard silver color.</summary>
    public static Color Silver => new(0xC0, 0xC0, 0xC0);

    /// <summary>Gets the standard skyblue color.</summary>
    public static Color SkyBlue => new(0x87, 0xCE, 0xEB);

    /// <summary>Gets the standard slateblue color.</summary>
    public static Color SlateBlue => new(0x6A, 0x5A, 0xCD);

    /// <summary>Gets the standard slategray color.</summary>
    public static Color SlateGray => new(0x70, 0x80, 0x90);

    /// <summary>Gets the standard slategrey color, an alias of slategray.</summary>
    public static Color SlateGrey => SlateGray;

    /// <summary>Gets the standard snow color.</summary>
    public static Color Snow => new(0xFF, 0xFA, 0xFA);

    /// <summary>Gets the standard springgreen color.</summary>
    public static Color SpringGreen => new(0x00, 0xFF, 0x7F);

    /// <summary>Gets the standard steelblue color.</summary>
    public static Color SteelBlue => new(0x46, 0x82, 0xB4);

    /// <summary>Gets the standard tan color.</summary>
    public static Color Tan => new(0xD2, 0xB4, 0x8C);

    /// <summary>Gets the standard teal color.</summary>
    public static Color Teal => new(0x00, 0x80, 0x80);

    /// <summary>Gets the standard thistle color.</summary>
    public static Color Thistle => new(0xD8, 0xBF, 0xD8);

    /// <summary>Gets the standard tomato color.</summary>
    public static Color Tomato => new(0xFF, 0x63, 0x47);

    /// <summary>Gets fully transparent black.</summary>
    public static Color Transparent => new(0, 0, 0, 0);

    /// <summary>Gets the standard turquoise color.</summary>
    public static Color Turquoise => new(0x40, 0xE0, 0xD0);

    /// <summary>Gets the standard violet color.</summary>
    public static Color Violet => new(0xEE, 0x82, 0xEE);

    /// <summary>Gets the standard wheat color.</summary>
    public static Color Wheat => new(0xF5, 0xDE, 0xB3);

    /// <summary>Gets the standard white color.</summary>
    public static Color White => new(0xFF, 0xFF, 0xFF);

    /// <summary>Gets the standard whitesmoke color.</summary>
    public static Color WhiteSmoke => new(0xF5, 0xF5, 0xF5);

    /// <summary>Gets the standard yellow color.</summary>
    public static Color Yellow => new(0xFF, 0xFF, 0x00);

    /// <summary>Gets the standard yellowgreen color.</summary>
    public static Color YellowGreen => new(0x9A, 0xCD, 0x32);

    private static readonly FrozenDictionary<string, Color> NamedColors = new Dictionary<string, Color>
    {
        [nameof(AliceBlue)] = AliceBlue,
        [nameof(AntiqueWhite)] = AntiqueWhite,
        [nameof(Aqua)] = Aqua,
        [nameof(Aquamarine)] = Aquamarine,
        [nameof(Azure)] = Azure,
        [nameof(Beige)] = Beige,
        [nameof(Bisque)] = Bisque,
        [nameof(Black)] = Black,
        [nameof(BlanchedAlmond)] = BlanchedAlmond,
        [nameof(Blue)] = Blue,
        [nameof(BlueViolet)] = BlueViolet,
        [nameof(Brown)] = Brown,
        [nameof(BurlyWood)] = BurlyWood,
        [nameof(CadetBlue)] = CadetBlue,
        [nameof(Chartreuse)] = Chartreuse,
        [nameof(Chocolate)] = Chocolate,
        [nameof(Coral)] = Coral,
        [nameof(CornflowerBlue)] = CornflowerBlue,
        [nameof(Cornsilk)] = Cornsilk,
        [nameof(Crimson)] = Crimson,
        [nameof(Cyan)] = Cyan,
        [nameof(DarkBlue)] = DarkBlue,
        [nameof(DarkCyan)] = DarkCyan,
        [nameof(DarkGoldenrod)] = DarkGoldenrod,
        [nameof(DarkGray)] = DarkGray,
        [nameof(DarkGreen)] = DarkGreen,
        [nameof(DarkGrey)] = DarkGrey,
        [nameof(DarkKhaki)] = DarkKhaki,
        [nameof(DarkMagenta)] = DarkMagenta,
        [nameof(DarkOliveGreen)] = DarkOliveGreen,
        [nameof(DarkOrange)] = DarkOrange,
        [nameof(DarkOrchid)] = DarkOrchid,
        [nameof(DarkRed)] = DarkRed,
        [nameof(DarkSalmon)] = DarkSalmon,
        [nameof(DarkSeaGreen)] = DarkSeaGreen,
        [nameof(DarkSlateBlue)] = DarkSlateBlue,
        [nameof(DarkSlateGray)] = DarkSlateGray,
        [nameof(DarkSlateGrey)] = DarkSlateGrey,
        [nameof(DarkTurquoise)] = DarkTurquoise,
        [nameof(DarkViolet)] = DarkViolet,
        [nameof(DeepPink)] = DeepPink,
        [nameof(DeepSkyBlue)] = DeepSkyBlue,
        [nameof(DimGray)] = DimGray,
        [nameof(DimGrey)] = DimGrey,
        [nameof(DodgerBlue)] = DodgerBlue,
        [nameof(Firebrick)] = Firebrick,
        [nameof(FloralWhite)] = FloralWhite,
        [nameof(ForestGreen)] = ForestGreen,
        [nameof(Fuchsia)] = Fuchsia,
        [nameof(Gainsboro)] = Gainsboro,
        [nameof(GhostWhite)] = GhostWhite,
        [nameof(Gold)] = Gold,
        [nameof(Goldenrod)] = Goldenrod,
        [nameof(Gray)] = Gray,
        [nameof(Green)] = Green,
        [nameof(GreenYellow)] = GreenYellow,
        [nameof(Grey)] = Grey,
        [nameof(Honeydew)] = Honeydew,
        [nameof(HotPink)] = HotPink,
        [nameof(IndianRed)] = IndianRed,
        [nameof(Indigo)] = Indigo,
        [nameof(Ivory)] = Ivory,
        [nameof(Khaki)] = Khaki,
        [nameof(Lavender)] = Lavender,
        [nameof(LavenderBlush)] = LavenderBlush,
        [nameof(LawnGreen)] = LawnGreen,
        [nameof(LemonChiffon)] = LemonChiffon,
        [nameof(LightBlue)] = LightBlue,
        [nameof(LightCoral)] = LightCoral,
        [nameof(LightCyan)] = LightCyan,
        [nameof(LightGoldenrodYellow)] = LightGoldenrodYellow,
        [nameof(LightGray)] = LightGray,
        [nameof(LightGreen)] = LightGreen,
        [nameof(LightGrey)] = LightGrey,
        [nameof(LightPink)] = LightPink,
        [nameof(LightSalmon)] = LightSalmon,
        [nameof(LightSeaGreen)] = LightSeaGreen,
        [nameof(LightSkyBlue)] = LightSkyBlue,
        [nameof(LightSlateGray)] = LightSlateGray,
        [nameof(LightSlateGrey)] = LightSlateGrey,
        [nameof(LightSteelBlue)] = LightSteelBlue,
        [nameof(LightYellow)] = LightYellow,
        [nameof(Lime)] = Lime,
        [nameof(LimeGreen)] = LimeGreen,
        [nameof(Linen)] = Linen,
        [nameof(Magenta)] = Magenta,
        [nameof(Maroon)] = Maroon,
        [nameof(MediumAquamarine)] = MediumAquamarine,
        [nameof(MediumBlue)] = MediumBlue,
        [nameof(MediumOrchid)] = MediumOrchid,
        [nameof(MediumPurple)] = MediumPurple,
        [nameof(MediumSeaGreen)] = MediumSeaGreen,
        [nameof(MediumSlateBlue)] = MediumSlateBlue,
        [nameof(MediumSpringGreen)] = MediumSpringGreen,
        [nameof(MediumTurquoise)] = MediumTurquoise,
        [nameof(MediumVioletRed)] = MediumVioletRed,
        [nameof(MidnightBlue)] = MidnightBlue,
        [nameof(MintCream)] = MintCream,
        [nameof(MistyRose)] = MistyRose,
        [nameof(Moccasin)] = Moccasin,
        [nameof(NavajoWhite)] = NavajoWhite,
        [nameof(Navy)] = Navy,
        [nameof(OldLace)] = OldLace,
        [nameof(Olive)] = Olive,
        [nameof(OliveDrab)] = OliveDrab,
        [nameof(Orange)] = Orange,
        [nameof(OrangeRed)] = OrangeRed,
        [nameof(Orchid)] = Orchid,
        [nameof(PaleGoldenrod)] = PaleGoldenrod,
        [nameof(PaleGreen)] = PaleGreen,
        [nameof(PaleTurquoise)] = PaleTurquoise,
        [nameof(PaleVioletRed)] = PaleVioletRed,
        [nameof(PapayaWhip)] = PapayaWhip,
        [nameof(PeachPuff)] = PeachPuff,
        [nameof(Peru)] = Peru,
        [nameof(Pink)] = Pink,
        [nameof(Plum)] = Plum,
        [nameof(PowderBlue)] = PowderBlue,
        [nameof(Purple)] = Purple,
        [nameof(RebeccaPurple)] = RebeccaPurple,
        [nameof(Red)] = Red,
        [nameof(RosyBrown)] = RosyBrown,
        [nameof(RoyalBlue)] = RoyalBlue,
        [nameof(SaddleBrown)] = SaddleBrown,
        [nameof(Salmon)] = Salmon,
        [nameof(SandyBrown)] = SandyBrown,
        [nameof(SeaGreen)] = SeaGreen,
        [nameof(SeaShell)] = SeaShell,
        [nameof(Sienna)] = Sienna,
        [nameof(Silver)] = Silver,
        [nameof(SkyBlue)] = SkyBlue,
        [nameof(SlateBlue)] = SlateBlue,
        [nameof(SlateGray)] = SlateGray,
        [nameof(SlateGrey)] = SlateGrey,
        [nameof(Snow)] = Snow,
        [nameof(SpringGreen)] = SpringGreen,
        [nameof(SteelBlue)] = SteelBlue,
        [nameof(Tan)] = Tan,
        [nameof(Teal)] = Teal,
        [nameof(Thistle)] = Thistle,
        [nameof(Tomato)] = Tomato,
        [nameof(Transparent)] = Transparent,
        [nameof(Turquoise)] = Turquoise,
        [nameof(Violet)] = Violet,
        [nameof(Wheat)] = Wheat,
        [nameof(White)] = White,
        [nameof(WhiteSmoke)] = WhiteSmoke,
        [nameof(Yellow)] = Yellow,
        [nameof(YellowGreen)] = YellowGreen
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Looks up a complete standard Web color name or transparent, ignoring case.</summary>
    /// <param name="name">The color name without surrounding whitespace, quotes, or additional content.</param>
    /// <param name="color">The named color, or the default color when the name is unknown.</param>
    /// <returns>True when the name identifies a standard color; otherwise, false.</returns>
    public static bool TryFromName(string name, out Color color) => NamedColors.TryGetValue(name, out color);
}
