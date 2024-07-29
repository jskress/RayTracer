using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using RayTracer.General;

namespace RayTracer.Graphics;

/// <summary>
/// This class holds some common color definitions.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class Colors
{
	public static readonly Color AliceBlue = new (0.941176, 0.972549, 1);
	public static readonly Color AntiqueWhite = new (0.980392, 0.921569, 0.843137);
	public static readonly Color Aqua = new (0, 1, 1);
	public static readonly Color Aquamarine = new (0.498039, 1, 0.831373);
	public static readonly Color Azure = new (0.941176, 1, 1);
	public static readonly Color BakersChocolate = new (0.36, 0.20, 0.09);
	public static readonly Color Beige = new (0.960784, 0.960784, 0.862745);
	public static readonly Color Bisque = new (1, 0.894118, 0.768627);
	public static readonly Color Black = new (0, 0, 0);
	public static readonly Color BlanchedAlmond = new (1, 0.921569, 0.803922);
	public static readonly Color Blue = new (0, 0, 1);
	public static readonly Color BlueViolet = new (0.541176, 0.168627, 0.886275);
	public static readonly Color Brass = new (0.71, 0.65, 0.26);
	public static readonly Color BrightGold = new (0.85, 0.85, 0.10);
	public static readonly Color Bronze = new (0.55, 0.47, 0.14);
	public static readonly Color Bronze2 = new (0.65, 0.49, 0.24);
	public static readonly Color Brown = new (0.647059, 0.164706, 0.164706);
	public static readonly Color BurlyWood = new (0.870588, 0.721569, 0.529412);
	public static readonly Color CadetBlue = new (0.372549, 0.623529, 0.623529);
	public static readonly Color Chartreuse = new (0.498039, 1, 0);
	public static readonly Color Chocolate = new (0.823529, 0.411765, 0.117647);
	public static readonly Color CoolCopper = new (0.85, 0.53, 0.10);
	public static readonly Color Copper = new (0.72, 0.45, 0.20);
	public static readonly Color Coral = new (1, 0.498039, 0.313725);
	public static readonly Color CornflowerBlue = new (0.392157, 0.584314, 0.929412);
	public static readonly Color Cornsilk = new (1, 0.972549, 0.862745);
	public static readonly Color Crimson = new (0.862745, 0.078431, 0.235294);
	public static readonly Color Cyan = new (0, 1, 1);
	public static readonly Color DarkBlue = new (0, 0, 0.545098);
	public static readonly Color DarkBrown = new (0.36, 0.25, 0.20);
	public static readonly Color DarkCyan = new (0, 0.545098, 0.545098);
	public static readonly Color DarkGoldenrod = new (0.721569, 0.52549, 0.043137);
	public static readonly Color DarkGray = new (0.662745, 0.662745, 0.662745);
	public static readonly Color DarkGreen = new (0.184314, 0.184314, 0.184314);
	public static readonly Color DarkGreenCopper = new (0.29, 0.46, 0.43);
	public static readonly Color DarkGrey = DarkGray;
	public static readonly Color DarkKhaki = new (0.741176, 0.717647, 0.419608);
	public static readonly Color DarkMagenta = new (0.545098, 0, 0.545098);
	public static readonly Color DarkOliveGreen = new (0.309804, 0.309804, 0.184314);
	public static readonly Color DarkOrange = new (1, 0.54902, 0);
	public static readonly Color DarkOrchid = new (0.6, 0.196078, 0.8);
	public static readonly Color DarkPurple = new (0.53, 0.12, 0.47);
	public static readonly Color DarkRed = new (0.545098, 0, 0);
	public static readonly Color DarkSalmon = new (0.913725, 0.588235, 0.478431);
	public static readonly Color DarkSeaGreen = new (0.560784, 0.737255, 0.560784);
	public static readonly Color DarkSlateBlue = new (0.119608, 0.137255, 0.556863);
	public static readonly Color DarkSlateGray = new (0.184314, 0.309804, 0.309804);
	public static readonly Color DarkSlateGrey = DarkSlateGray;
	public static readonly Color DarkTan = new (0.59, 0.41, 0.31);
	public static readonly Color DarkTurquoise = new (0.439216, 0.576471, 0.858824);
	public static readonly Color DarkViolet = new (0.580392, 0, 0.827451);
	public static readonly Color DarkWood = new (0.52, 0.37, 0.26);
	public static readonly Color DeepPink = new (1, 0.078431, 0.576471);
	public static readonly Color DeepSkyBlue = new (0, 0.74902, 1);
	public static readonly Color DimGray = new (0.4117645, 0.4117645, 0.4117645);
	public static readonly Color DimGrey = DimGray;
	public static readonly Color DodgerBlue = new (0.117647, 0.564706, 1);
	public static readonly Color DustyRose = new (0.52, 0.39, 0.39);
	public static readonly Color Feldspar = new (0.82, 0.57, 0.46);
	public static readonly Color Firebrick = new (0.556863, 0.137255, 0.137255);
	public static readonly Color Flesh = new (0.96, 0.80, 0.69);
	public static readonly Color FloralWhite = new (1, 0.980392, 0.941176);
	public static readonly Color ForestGreen = new (0.137255, 0.556863, 0.137255);
	public static readonly Color Fuchsia = new (1, 0, 1);
	public static readonly Color Gainsboro = new (0.862745, 0.862745, 0.862745);
	public static readonly Color GhostWhite = new (0.972549, 0.972549, 1);
	public static readonly Color Gold = new (0.8, 0.498039, 0.196078);
	public static readonly Color Goldenrod = new (0.858824, 0.858824, 0.439216);
	public static readonly Color Gray = new (0.5, 0.5, 0.5);
	public static readonly Color Green = new (0, 0.501961, 0);
	public static readonly Color GreenCopper = new (0.32, 0.49, 0.46);
	public static readonly Color GreenYellow = new (0.576471, 0.858824, 0.439216);
	public static readonly Color Grey = Gray;
	public static readonly Color Honeydew = new (0.941176, 1, 0.941176);
	public static readonly Color HotPink = new (1, 0.411765, 0.705882);
	public static readonly Color HunterGreen = new (0.13, 0.37, 0.31);
	public static readonly Color IndianRed = new (0.309804, 0.184314, 0.184314);
	public static readonly Color Indigo = new (0.294118, 0, 0.509804);
	public static readonly Color Ivory = new (1, 1, 0.941176);
	public static readonly Color Khaki = new (0.623529, 0.623529, 0.372549);
	public static readonly Color Lavender = new (0.901961, 0.901961, 0.980392);
	public static readonly Color LavenderBlush = new (1, 0.941176, 0.960784);
	public static readonly Color LawnGreen = new (0.486275, 0.988235, 0);
	public static readonly Color LemonChiffon = new (1, 0.980392, 0.803922);
	public static readonly Color LightBlue = new (0.74902, 0.847059, 0.847059);
	public static readonly Color LightCoral = new (0.941176, 0.501961, 0.501961);
	public static readonly Color LightCyan = new (0.878431, 1, 1);
	public static readonly Color LightGoldenrodYellow = new (0.980392, 0.980392, 0.823529);
	public static readonly Color LightGray = new (0.658824, 0.658824, 0.658824);
	public static readonly Color LightGreen = new (0.564706, 0.933333, 0.564706);
	public static readonly Color LightGrey = LightGray;
	public static readonly Color LightPink = new (1, 0.713725, 0.756863);
	public static readonly Color LightPurple = new (0.87, 0.58, 0.98);
	public static readonly Color LightSalmon = new (1, 0.627451, 0.478431);
	public static readonly Color LightSeaGreen = new (0.12549, 0.698039, 0.666667);
	public static readonly Color LightSkyBlue = new (0.529412, 0.807843, 0.980392);
	public static readonly Color LightSlateGray = new (0.466667, 0.533333, 0.6);
	public static readonly Color LightSlateGrey = LightSlateGray;
	public static readonly Color LightSteelBlue = new (0.560784, 0.560784, 0.737255);
	public static readonly Color LightWood = new (0.91, 0.76, 0.65);
	public static readonly Color LightYellow = new (1, 1, 0.878431);
	public static readonly Color Lime = new (0, 1, 0);
	public static readonly Color LimeGreen = new (0.196078, 0.8, 0.196078);
	public static readonly Color Linen = new (0.980392, 0.941176, 0.901961);
	public static readonly Color Magenta = new (1, 0, 1);
	public static readonly Color MandarinOrange = new (0.89, 0.47, 0.20);
	public static readonly Color Maroon = new (0.556863, 0.137255, 0.419608);
	public static readonly Color MediumAquamarine = new (0.196078, 0.8, 0.6);
	public static readonly Color MediumBlue = new (0.196078, 0.196078, 0.8);
	public static readonly Color MediumForestGreen = new (0.419608, 0.556863, 0.137255);
	public static readonly Color MediumGoldenrod = new (0.917647, 0.917647, 0.678431);
	public static readonly Color MediumOrchid = new (0.576471, 0.439216, 0.858824);
	public static readonly Color MediumPurple = new (0.73, 0.16, 0.96);
	public static readonly Color MediumSeaGreen = new (0.258824, 0.435294, 0.258824);
	public static readonly Color MediumSlateBlue = new (0.498039, 0, 1);
	public static readonly Color MediumSpringGreen = new (0.498039, 1, 0);
	public static readonly Color MediumTurquoise = new (0.439216, 0.858824, 0.858824);
	public static readonly Color MediumVioletRed = new (0.858824, 0.439216, 0.576471);
	public static readonly Color MediumWood = new (0.65, 0.50, 0.39);
	public static readonly Color Mica = Black;
	public static readonly Color MidnightBlue = new (0.184314, 0.184314, 0.309804);
	public static readonly Color MintCream = new(0.960784, 1, 0.980392);
	public static readonly Color MistyRose = new (1, 0.894118, 0.882353);
	public static readonly Color Moccasin = new (1, 0.894118, 0.709804);
	public static readonly Color NavajoWhite = new (1, 0.870588, 0.678431);
	public static readonly Color Navy = new (0.137255, 0.137255, 0.556863);
	public static readonly Color NavyBlue = Navy;
	public static readonly Color NeonBlue = new (0.30, 0.30, 1);
	public static readonly Color NeonPink = new (1, 0.43, 0.78);
	public static readonly Color NewMidnightBlue = new (0, 0, 0.61);
	public static readonly Color NewTan = new (0.92, 0.78, 0.62);
	public static readonly Color OldGold = new (0.81, 0.71, 0.23);
	public static readonly Color OldLace = new (0.992157, 0.960784, 0.901961);
	public static readonly Color Olive = new (0.501961, 0.501961, 0);
	public static readonly Color OliveDrab = new (0.419608, 0.556863, 0.137255);
	public static readonly Color Orange = new (1, 0.5, 0);
	public static readonly Color OrangeRed = new (1, 0.25, 0);
	public static readonly Color Orchid = new (0.858824, 0.439216, 0.858824);
	public static readonly Color PaleGoldenrod = new (0.933333, 0.909804, 0.666667);
	public static readonly Color PaleGreen = new (0.560784, 0.737255, 0.560784);
	public static readonly Color PaleTurquoise = new (0.686275, 0.933333, 0.933333);
	public static readonly Color PaleVioletRed = new (0.847059, 0.439216, 0.576471);
	public static readonly Color PapayaWhip = new (1, 0.937255, 0.835294);
	public static readonly Color PeachPuff = new (1, 0.854902, 0.72549);
	public static readonly Color Peru = new (0.803922, 0.521569, 0.247059);
	public static readonly Color Pink = new (0.737255, 0.560784, 0.560784);
	public static readonly Color Plum = new (0.917647, 0.678431, 0.917647);
	public static readonly Color PowderBlue = new (0.690196, 0.878431, 0.901961);
	public static readonly Color Purple = new (0.501961, 0, 0.501961);
	public static readonly Color Quartz = new (0.85, 0.85, 0.95);
	public static readonly Color Red = new (1, 0, 0);
	public static readonly Color RichBlue = new (0.35, 0.35, 0.67);
	public static readonly Color RosyBrown = new (0.737255, 0.560784, 0.560784);
	public static readonly Color RoyalBlue = new (0.254902, 0.411765, 0.882353);
	public static readonly Color SaddleBrown = new (0.545098, 0.270588, 0.07451);
	public static readonly Color Salmon = new (0.435294, 0.258824, 0.258824);
	public static readonly Color SandyBrown = new (0.956863, 0.643137, 0.376471);
	public static readonly Color Scarlet = new (0.55, 0.09, 0.09);
	public static readonly Color SeaGreen = new (0.137255, 0.556863, 0.419608);
	public static readonly Color SeaShell = new (1, 0.960784, 0.933333);
	public static readonly Color SemiSweetChocolate = new (0.42, 0.26, 0.15);
	public static readonly Color Sienna = new (0.556863, 0.419608, 0.137255);
	public static readonly Color Silver = new (0.90, 0.91, 0.98);
	public static readonly Color SkyBlue = new (0.196078, 0.6, 0.8);
	public static readonly Color SlateBlue = new (0, 0.498039, 1);
	public static readonly Color SlateGray = new (0.439216, 0.501961, 0.564706);
	public static readonly Color SlateGrey = SlateGray;
	public static readonly Color Snow = new (1, 0.980392, 0.980392);
	public static readonly Color SpicyPink = new (1, 0.11, 0.68);
	public static readonly Color SpringGreen = new (0, 1, 0.498039);
	public static readonly Color SteelBlue = new (0.137255, 0.419608, 0.556863);
	public static readonly Color SummerSky = new (0.22, 0.69, 0.87);
	public static readonly Color Tan = new (0.858824, 0.576471, 0.439216);
	public static readonly Color Teal = new (0, 0.501961, 0.501961);
	public static readonly Color Thistle = new (0.847059, 0.74902, 0.847059);
	public static readonly Color Tomato = new (1, 0.388235, 0.278431);
	public static readonly Color Transparent = new (0, 0, 0, 0);
	public static readonly Color Turquoise = new (0.678431, 0.917647, 0.917647);
	public static readonly Color VeryDarkBrown = new (0.35, 0.16, 0.14);
	public static readonly Color VeryLightGray = new (0.8, 0.8, 0.8);
	public static readonly Color VeryLightGrey = VeryLightGray;
	public static readonly Color VeryLightPurple = new (0.94, 0.81, 0.99);
	public static readonly Color Violet = new (0.309804, 0.184314, 0.309804);
	public static readonly Color VioletRed = new (0.8, 0.196078, 0.6);
	public static readonly Color Wheat = new (0.847059, 0.847059, 0.74902);
	public static readonly Color White = new (1, 1, 1);
	public static readonly Color WhiteSmoke = new (0.960784, 0.960784, 0.960784);
	public static readonly Color Yellow = new (1, 1, 0);
	public static readonly Color YellowGreen = new (0.6, 0.8, 0.196078);

	// 19 shades of gray and grey
	public static readonly Color Gray05 = White * 0.05;
	public static readonly Color Gray10 = White * 0.10;
	public static readonly Color Gray15 = White * 0.15;
	public static readonly Color Gray20 = White * 0.20;
	public static readonly Color Gray25 = White * 0.25;
	public static readonly Color Gray30 = White * 0.30;
	public static readonly Color Gray35 = White * 0.35;
	public static readonly Color Gray40 = White * 0.40;
	public static readonly Color Gray45 = White * 0.45;
	public static readonly Color Gray50 = White * 0.50;
	public static readonly Color Gray55 = White * 0.55;
	public static readonly Color Gray60 = White * 0.60;
	public static readonly Color Gray65 = White * 0.65;
	public static readonly Color Gray70 = White * 0.70;
	public static readonly Color Gray75 = White * 0.75;
	public static readonly Color Gray80 = White * 0.80;
	public static readonly Color Gray85 = White * 0.85;
	public static readonly Color Gray90 = White * 0.90;
	public static readonly Color Gray95 = White * 0.95;
	public static readonly Color Grey05 = White * 0.05;
	public static readonly Color Grey10 = White * 0.10;
	public static readonly Color Grey15 = White * 0.15;
	public static readonly Color Grey20 = White * 0.20;
	public static readonly Color Grey25 = White * 0.25;
	public static readonly Color Grey30 = White * 0.30;
	public static readonly Color Grey35 = White * 0.35;
	public static readonly Color Grey40 = White * 0.40;
	public static readonly Color Grey45 = White * 0.45;
	public static readonly Color Grey50 = White * 0.50;
	public static readonly Color Grey55 = White * 0.55;
	public static readonly Color Grey60 = White * 0.60;
	public static readonly Color Grey65 = White * 0.65;
	public static readonly Color Grey70 = White * 0.70;
	public static readonly Color Grey75 = White * 0.75;
	public static readonly Color Grey80 = White * 0.80;
	public static readonly Color Grey85 = White * 0.85;
	public static readonly Color Grey90 = White * 0.90;
	public static readonly Color Grey95 = White * 0.95;

	/// <summary>
	/// This field holds our cache of known, named colors.
	/// </summary>
	private static readonly Lazy<Dictionary<string, Color>> LazyNamedColors = new (GetNamedColors);

	/// <summary>
	/// This is a helper method for building a dictionary of all known colors, keyed by
	/// their names.
	/// </summary>
	/// <returns>The dictionary of known colors.</returns>
	private static Dictionary<string, Color> GetNamedColors()
	{
		return typeof(Colors)
			.GetFields(BindingFlags.Static | BindingFlags.Public)
			.Where(fieldInfo => fieldInfo.FieldType == typeof(Color))
			.ToDictionary(info => info.Name, info => info.GetValue(null) as Color);
	}

	/// <summary>
	/// This method is used to register all our known colors as variables in the given
	/// variable pool.
	/// </summary>
	/// <param name="variables">The variable pool to add the colors to.</param>
	public static void AddToVariables(Variables variables)
	{
		foreach (KeyValuePair<string, Color> pair in LazyNamedColors.Value)
			variables.SetValue(pair.Key, pair.Value);
	}
}
