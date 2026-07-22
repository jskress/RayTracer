using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class defines some well-known indices of refraction.
/// <para>
/// A name added here becomes a name a scene may use, since every constant below is found by
/// reflection and set as a variable before anything is parsed.  Sharing a name with a color is no
/// trouble: a variable holds a value per type rather than one value, so <c>Turquoise</c> is both a
/// color and an index of refraction, and which one a scene means is settled by where it is used.
/// </para>
/// <para>
/// The gemstones and optical glasses come from POV-Ray's <c>ior.inc</c>.  They are kept here
/// rather than converted into a library because a library would have to be imported to be used,
/// where these are simply in scope, and because a second set of the same numbers under slightly
/// different names would be a trap rather than a convenience.
/// </para>
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class IndicesOfRefraction
{
    public const double Vacuum = 1;
    public const double Air = 1.000293;
    public const double CarbonDioxide = 1.00045;
    public const double Helium = 1.000036;
    public const double Hydrogen = 1.000132;
    public const double Water = 1.333;
    public const double WaterIce = 1.31;
    public const double Kerosene = 1.39;
    public const double Glass = 1.52;
    public const double Amber = 1.55;
    public const double Diamond = 2.417;

    // The optical glasses, which differ enough from one another to be worth telling apart: a lens
    // made of flint bends light half again as far from true as one made of crown.
    public const double CrownGlass = 1.51673;
    public const double CrownGlassBaK1 = 1.57241;
    public const double WindowGlass = 1.51673;
    public const double FlintGlass = 1.78446;
    public const double FlintGlassF2 = 1.61989;
    public const double FlintGlassLaSFN9 = 1.85002;

    // The gemstones.  Several share a number -- amethyst, citrine and onyx are all quartz, and a
    // ruby and a sapphire are both corundum -- and they are named separately anyway, since a scene
    // wanting an emerald should not have to know that it is a beryl.  Five of them are also named
    // colors, which costs nothing: a scene asking for the color of aquamarine and a scene asking
    // for how far it bends light are asking different questions of the same name.
    public const double Agate = 1.544;
    public const double Alexandrite = 1.746;
    public const double Amazonite = 1.53;
    public const double Amethyst = 1.544;
    public const double Andalusite = 1.64;
    public const double Andesine = 1.53;
    public const double Apatite = 1.63;
    public const double Aquamarine = 1.58;
    public const double Aventurine = 1.544;
    public const double Beryl = 1.58;
    public const double Chalcedony = 1.544;
    public const double ChromeDiopside = 1.69;
    public const double Chrysoberyl = 1.746;
    public const double Citrine = 1.544;
    public const double Coral = 1.486;
    public const double Corundum = 1.766;
    public const double CubicZirconia = 2.16;
    public const double Emerald = 1.58;
    public const double Fluorite = 1.434;
    public const double Iolite = 1.55;
    public const double Ivory = 1.54;
    public const double Jadeite = 1.67;
    public const double Jasper = 1.54;
    public const double Kunzite = 1.67;
    public const double Kyanite = 1.73;
    public const double Labradorite = 1.56;
    public const double LapisLazuli = 1.5;
    public const double Malachite = 1.655;
    public const double Moissanite = 2.67;
    public const double Moonstone = 1.52;
    public const double Morganite = 1.58;
    public const double NephriteJade = 1.62;
    public const double Onyx = 1.544;
    public const double Opal = 1.45;
    public const double Orthoclase = 1.52;
    public const double Pearl = 1.53;
    public const double Peridot = 1.654;
    public const double Prehnite = 1.64;
    public const double Quartz = 1.544;
    public const double RoseQuartz = 1.544;
    public const double Ruby = 1.766;
    public const double Sapphire = 1.766;
    public const double SmokyQuartz = 1.544;
    public const double Sphene = 1.7;
    public const double Spinel = 1.712;
    public const double Spodumene = 1.67;
    public const double Tanzanite = 1.7;
    public const double TigersEye = 1.544;
    public const double Topaz = 1.62;
    public const double Tourmaline = 1.624;
    public const double Turquoise = 1.61;
    public const double Zircon = 1.95;
    public const double Zoisite = 1.7;

    // The garnets, which are a family rather than a stone and range more widely than most.
    public const double AlmandineGarnet = 1.79;
    public const double AndraditeGarnet = 1.888;
    public const double DemantoidGarnet = 1.885;
    public const double GrossulariteGarnet = 1.74;
    public const double PyropeGarnet = 1.74;
    public const double RhodoliteGarnet = 1.76;
    public const double SpessartiteGarnet = 1.81;
    public const double TsavoriteGarnet = 1.74;

    /// <summary>
    /// This field holds our cache of known, named colors.
    /// </summary>
    private static readonly Lazy<Dictionary<string, double>> LazyNamedColors = new (GetNamedIoRs);

    /// <summary>
    /// This is a helper method for building a dictionary of all known indices of refraction,
    /// keyed by their names.
    /// </summary>
    /// <returns>The dictionary of known indices of refraction.</returns>
    private static Dictionary<string, double> GetNamedIoRs()
    {
        return typeof(IndicesOfRefraction)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(fieldInfo => fieldInfo.IsLiteral && !fieldInfo.IsInitOnly &&
                                fieldInfo.FieldType == typeof(double))
            .ToDictionary(info => info.Name, info => (double) info.GetValue(null)!);
    }

    /// <summary>
    /// This method is used to register all our known indices of refraction as variables
    /// in the given variable pool.
    /// </summary>
    /// <param name="variables">The variable pool to add the colors to.</param>
    public static void AddToVariables(Variables variables)
    {
        foreach (KeyValuePair<string, double> pair in LazyNamedColors.Value)
            variables.SetValue(pair.Key, pair.Value);
    }
}
