using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This class is how weathered a model is: how much grime has gathered in its crevices, how much dust has
/// settled on it, how far its edges are worn.  A group or an object says it with <c>weather { }</c>, and every
/// part of it is weathered alike, whatever it is made of.
/// <para>
/// **Each part's own material is what is weathered**, so a model weathers without its every material being
/// written again: each becomes a blend of itself and what weather makes of it (see <see cref="MaterialBlend"/>).
/// Grime gathers where the part is closed in (<see cref="CrevicesPattern"/>): the part's own color, darkened
/// most of the way to the grime's, and matte, its gloss and any coat gone.  Wear shows where it stands out
/// (<see cref="EdgesPattern"/>): the material it says lies beneath, bared, or else the part's own made paler and
/// smoother, as handling polishes paint.  Dust settles where it faces up (<see cref="FacingPattern"/>): sandy and
/// matte.  Each comes on more widely the more of it there is, and its edge is broken by noise, as real dirt's
/// is.  Snow lies where it faces up too, but whole: white over everything flat enough to hold it, broken only
/// along its edge, where a slope is getting too steep, and more snow holds on steeper slopes.  An amount of
/// nought adds nothing, so a model weathered by nothing is exactly as it was.
/// </para>
/// <para>
/// **A part's weather is its nearest**: what it says itself, or what the group round it says, and so on out --
/// through the instances a shared shape is met through, at the hit, so that one shape may stand clean in one
/// place and weathered in another.  Each part's material is weathered once and kept.
/// </para>
/// </summary>
public sealed class Weather
{
    /// <summary>
    /// This is how far grime and wear are looked for when a model says nothing.
    /// </summary>
    public const double DefaultRadius = 0.25;

    /// <summary>
    /// This property holds how much grime has gathered, from nought to one.
    /// </summary>
    public double Grime { get; set; }

    /// <summary>
    /// This property holds the grime's color.
    /// </summary>
    public Color GrimeColor { get; set; } = new (0.16, 0.12, 0.08);

    /// <summary>
    /// This property holds how much dust has settled, from nought to one.
    /// </summary>
    public double Dust { get; set; }

    /// <summary>
    /// This property holds the dust's color.
    /// </summary>
    public Color DustColor { get; set; } = new (0.66, 0.56, 0.42);

    /// <summary>
    /// This property holds how much snow has fallen, from nought to one: at one it lies on anything sloping less
    /// than about fifty-eight degrees, at a half on anything under about thirty-five.
    /// </summary>
    public double Snow { get; set; }

    /// <summary>
    /// This property holds the snow's color.
    /// </summary>
    public Color SnowColor { get; set; } = new (0.93, 0.94, 0.97);

    /// <summary>
    /// This property holds how far the edges are worn, from nought to one.
    /// </summary>
    public double Wear { get; set; }

    /// <summary>
    /// This property holds what wear bares, or <c>null</c> for the part's own material made paler and smoother.
    /// </summary>
    public Material WearTo { get; set; }

    /// <summary>
    /// This property holds how far about a place grime and wear are looked for, in the units of the space each
    /// part stands in.
    /// </summary>
    public double Radius { get; set; } = DefaultRadius;

    /// <summary>
    /// This property holds how high dirt has been kicked up from the ground, in the weathered thing's own units
    /// above its own y = 0, which is where a model stands; nought for none.
    /// </summary>
    public double Splash { get; set; }

    /// <summary>
    /// This property holds the kicked-up dirt's color.
    /// </summary>
    public Color SplashColor { get; set; } = new (0.52, 0.42, 0.30);

    /// <summary>
    /// This property holds the surface that says this weather, whose own space splash is measured in; set when
    /// the weather is settled.
    /// </summary>
    internal Surface Owner { get; set; }

    /// <summary>
    /// This property reports whether this weather does anything at all.
    /// </summary>
    public bool IsNothing => Grime <= 0 && Dust <= 0 && Wear <= 0 && Splash <= 0 && Snow <= 0;

    private readonly ConcurrentDictionary<Material, Material> _weathered = new ();

    /// <summary>
    /// This method returns a material weathered: itself, for weather that does nothing, and otherwise the blend
    /// of it and what this weather makes of it, made once and kept.
    /// </summary>
    /// <param name="material">The material to weather.</param>
    /// <returns>The material weathered.</returns>
    public Material Weathered(Material material) =>
        IsNothing || material is null ? material : _weathered.GetOrAdd(material, Build);

    // The layers, from the inside out: grime in the crevices, wear on the edges, dust on top, and snow over all.
    private Material Build(Material material)
    {
        Material layered = material;

        if (Grime > 0)
        {
            Material grimy = Grimy(material);
            double from = 0.6 - 0.5 * Math.Min(Grime, 1);
            Material broken = Blended(Noise(11), [0.42, 0.58], MaterialBlend.Mixed(material, grimy, 0.5), grimy);

            layered = Blended(new CrevicesPattern { Radius = Radius }, [from, from + 0.25], layered, broken);
        }

        if (Wear > 0)
        {
            Material worn = WearTo ?? Worn(material);
            double from = 0.35 - 0.3 * Math.Min(Wear, 1);
            Material chipped = Blended(Noise(13), [0.45, 0.52], layered, worn);

            layered = Blended(new EdgesPattern { Radius = Radius }, [from, from + 0.12], layered, chipped);
        }

        if (Splash > 0)
        {
            Material splashed = Splashed(material);
            Material broken = Blended(Noise(19), [0.4, 0.6], MaterialBlend.Mixed(material, splashed, 0.4), splashed);

            layered = Blended(new GroundPattern(this), [0, 0.7], layered, broken);
        }

        if (Dust > 0)
        {
            Material dusty = Dusty(material);
            double from = 1 - 0.35 * Math.Min(Dust, 1);
            Material broken = Blended(Noise(17), [0.4, 0.56], MaterialBlend.Mixed(material, dusty, 0.5), dusty);

            layered = Blended(new FacingPattern(), [from, Math.Min(from + 0.12, 0.999)], layered, broken);
        }

        if (Snow > 0)
        {
            // Whole where it lies, patchy along the edge it lies to: a band of slopes steep enough to lose some.
            Material snowy = Snowy(material);
            double from = 1 - 0.3 * Math.Min(Snow, 1);
            Material patchy = Blended(Noise(23), [0.42, 0.58], layered, snowy);
            Material lying = Blended(new FacingPattern(), [from + 0.05, from + 0.08], patchy, snowy);

            layered = Blended(new FacingPattern(), [from, from + 0.03], layered, lying);
        }

        return layered;
    }

    // Noise to break the weather's edge up, about as fine as the reach it is looked for over.
    private (Pattern Pattern, Matrix Transform) Noise(int seed)
    {
        BozoPattern bozo = new ();

        bozo.SetSeed(seed);

        return (bozo, Transforms.Scale(Radius * 1.5));
    }

    private static Material Blended((Pattern Pattern, Matrix Transform) noise, double[] breaks, Material from, Material to) =>
        Blended(noise.Pattern, breaks, from, to, noise.Transform);

    private static Material Blended(Pattern pattern, double[] breaks, Material from, Material to, Matrix transform = null)
    {
        Material blended = from.CopiedForBlending();

        blended.Blend = new MaterialBlend { Pattern = pattern, Transform = transform, Breaks = breaks, Materials = [from, to] };

        return blended;
    }

    // Grime: the part's color most of the way to the grime's, matte, its gloss and any coat or sheen gone.
    private Material Grimy(Material material)
    {
        Material grimy = Stripped(material);

        grimy.Pigment = new MixedPigment { From = material.Pigment, To = new SolidPigment(GrimeColor), Share = 0.8 };
        grimy.Specular = material.Specular * 0.15;
        grimy.Reflective = material.Reflective * 0.15;
        grimy.Roughness = grimy.Reflective > 0 ? Math.Max(material.Roughness, 0.75) : material.Roughness;
        grimy.Metallic = material.Metallic * 0.3;

        return grimy;
    }

    // Dirt kicked up from the ground: most of the way to its color, and matte.
    private Material Splashed(Material material)
    {
        Material splashed = Stripped(material);

        splashed.Pigment = new MixedPigment { From = material.Pigment, To = new SolidPigment(SplashColor), Share = 0.7 };
        splashed.Specular = material.Specular * 0.2;
        splashed.Reflective = material.Reflective * 0.2;
        splashed.Roughness = splashed.Reflective > 0 ? Math.Max(material.Roughness, 0.7) : material.Roughness;
        splashed.Metallic = material.Metallic * 0.3;

        return splashed;
    }

    /// <summary>
    /// This class is the pattern of how near the ground a place is, for kicked-up dirt: one at the weathered
    /// thing's own y = 0, falling to nought at the splash's height, read in the space of the thing that says
    /// the weather -- through whatever instances stand between -- so that its ground is its own.
    /// </summary>
    private sealed class GroundPattern(Weather weather) : Pattern
    {
        public override int DiscretePigmentsNeeded => 0;

        public override bool SeesTheSurroundings => true;

        public override double Evaluate(Point point)
        {
            Surroundings here = Surroundings.Current;

            if (here is null || weather.Owner is not { } owner)
                return 0;

            Point local = owner.WorldToSurface(
                here.Point, here.TimeIndex, Material.PortalTo(owner, here.Surface, here.Portals));

            return Math.Clamp(1 - local.Y / weather.Splash, 0, Math.BitDecrement(1.0));
        }

        public override double Evaluate(Point point, Footprint footprint) => Evaluate(point);
    }

    // Dust: sandy and matte, whatever lies beneath.
    private Material Dusty(Material material)
    {
        Material dusty = Stripped(material);

        dusty.Pigment = new MixedPigment { From = material.Pigment, To = new SolidPigment(DustColor), Share = 0.85 };
        dusty.Diffuse = Math.Max(material.Diffuse, 0.8);
        dusty.Specular = 0;
        dusty.Reflective = 0;
        dusty.Roughness = 0;
        dusty.Metallic = 0;
        dusty.Fresnel = false;

        return dusty;
    }

    // Snow: white and matte but for a little sheen, whatever lies beneath.
    private Material Snowy(Material material)
    {
        Material snowy = Stripped(material);

        snowy.Pigment = new SolidPigment(SnowColor);
        snowy.Diffuse = 0.9;
        snowy.Specular = 0.12;
        snowy.Shininess = 20;
        snowy.Reflective = 0;
        snowy.Roughness = 0;
        snowy.Metallic = 0;
        snowy.Fresnel = false;

        return snowy;
    }

    // Wear with nothing said to bare: the part's own, paler and smoother, as handling polishes paint.
    private static Material Worn(Material material)
    {
        Material worn = material.CopiedForBlending();

        worn.Pigment = new MixedPigment { From = material.Pigment, To = SolidPigment.White, Share = 0.35 };
        worn.Roughness = material.Roughness * 0.5;

        return worn;
    }

    // A copy with its skins gone: a coat or a sheen is under the dirt, not over it.
    private static Material Stripped(Material material)
    {
        Material stripped = material.CopiedForBlending();

        stripped.Coat = null;
        stripped.Sheen = null;
        stripped.Anisotropy = 0;

        return stripped;
    }
}
