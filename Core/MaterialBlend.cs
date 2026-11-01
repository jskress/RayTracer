using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This class is a blend of whole materials by a pattern: clean paint where a surface is open and grime in its
/// crevices, bare metal on its worn edges, rust where it faces the weather.  It is what a material written as
/// <c>material blend &lt;pattern&gt; { [v, A, v, B, ...] }</c> holds.
/// <para>
/// **The finish changes with the color**, which is the point of it: grime is matte where the paint was glossy,
/// rust rough, and worn paint bares a shiny metal.  So where the pattern falls between two of its materials,
/// what the surface is shaded with there is a material part way between them -- every number a finish has,
/// and its color, mixed in proportion -- and where it falls beyond either end, that end's material whole.
/// What cannot be mixed, a coat, a sheen, a film, an interior, a skin of decals, is the nearer material's.  A
/// pattern that picks among a fixed number of things, a checker's two squares, picks a material outright.
/// </para>
/// <para>
/// **It is settled where a ray meets the surface**, where every hit's material is settled (see
/// <c>Scene.RegularizedFor</c>), and everything after shades with the material it settled on.  The mixtures are
/// kept, a few hundred steps between any two materials, so that no hit makes one of its own.  What asks a
/// surface for its material without a hit to settle it -- a shadow ray, a photon -- is given the first.
/// </para>
/// </summary>
public sealed class MaterialBlend
{
    // How finely the way between two materials is cut, so that the mixtures can be kept: fine enough that a
    // step is under a level of any color, which is far below what the eye takes for banding.
    internal const int Steps = 256;

    /// <summary>
    /// This property holds the pattern the materials are blended by.
    /// </summary>
    public Pattern Pattern { get; init; }

    /// <summary>
    /// This property holds where the pattern stands in the surface's space; <c>null</c> for where it is.
    /// </summary>
    public Matrix Transform { get; init; }

    /// <summary>
    /// This property holds the pattern's values at which each material stands whole, rising; empty for a pattern
    /// that picks among a fixed number of things.
    /// </summary>
    public double[] Breaks { get; init; } = [];

    /// <summary>
    /// This property holds the materials, in the order of the values they stand at.
    /// </summary>
    public Material[] Materials { get; init; } = [];

    private Matrix _inverse;

    private readonly ConcurrentDictionary<(Material, Material, int), Material> _mixtures = new ();

    /// <summary>
    /// This property returns every material the blend holds, its blends' too, for whatever must see to each of
    /// them before rendering starts.
    /// </summary>
    public IEnumerable<Material> AllMaterials =>
        Materials.SelectMany(material => material.Blend is { } inner ? inner.AllMaterials.Prepend(material) : [material]);

    /// <summary>
    /// This method returns the material a hit is shaded with: the pattern read where the ray met the surface,
    /// and the materials there blended by it.
    /// </summary>
    /// <param name="intersection">The hit, prepared.</param>
    /// <param name="scene">The scene being shaded, for a pattern that looks about the place.</param>
    /// <returns>The material to shade it with.</returns>
    public Material At(Intersection intersection, Scene scene)
    {
        double value = ValueAt(intersection, scene);

        if (Breaks.Length == 0)
        {
            int index = (int) Math.Floor(value);

            return Settled(Materials[((index % Materials.Length) + Materials.Length) % Materials.Length], intersection, scene);
        }

        if (value <= Breaks[0])
            return Settled(Materials[0], intersection, scene);

        for (int index = 1; index < Breaks.Length; index++)
        {
            if (value > Breaks[index])
                continue;

            double span = Breaks[index] - Breaks[index - 1];
            int step = span <= 0 ? Steps : (int) Math.Round((value - Breaks[index - 1]) / span * Steps);

            if (step <= 0)
                return Settled(Materials[index - 1], intersection, scene);

            if (step >= Steps)
                return Settled(Materials[index], intersection, scene);

            Material from = Settled(Materials[index - 1], intersection, scene);
            Material to = Settled(Materials[index], intersection, scene);

            return _mixtures.GetOrAdd((from, to, step), key => Mixed(key.Item1, key.Item2, (double) key.Item3 / Steps));
        }

        return Settled(Materials[^1], intersection, scene);
    }

    // A material that is itself a blend is settled at the hit first, and one whose numbers run by patterns of its own
    // is read there.
    private static Material Settled(Material material, Intersection intersection, Scene scene) =>
        (material.Blend is { } inner ? inner.At(intersection, scene) : material).MappedAt(intersection, scene);

    // The pattern's value where the ray met the surface; see PatternValueAt.
    private double ValueAt(Intersection intersection, Scene scene)
    {
        if (Transform is not null)
            _inverse ??= Transform.Invert();

        return PatternValueAt(Pattern, _inverse, intersection, scene);
    }

    /// <summary>
    /// This method returns a pattern's value where a ray met a surface, read in the surface's own space as a pigment
    /// is, and shown the surroundings if it looks about the place.  A picture is read over the patch the ray covers,
    /// as an image pigment is; every other pattern at the point.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="inverse">What carries the surface's space into the pattern's, or <c>null</c> for nothing.</param>
    /// <param name="intersection">The hit.</param>
    /// <param name="scene">The scene being shaded.</param>
    /// <returns>The pattern's value there.</returns>
    internal static double PatternValueAt(Pattern pattern, Matrix inverse, Intersection intersection, Scene scene)
    {
        // Noise in the pattern given no seed of its own draws on the surface's, as a pigment's does; see Pigment.Wearer.
        using Pigment.WornScope _ = Pigment.WornBy(intersection.Surface);

        Surface surface = intersection.Surface;
        Point point = surface.WorldToSurface(intersection.Point, intersection.TimeIndex, intersection.Portals);

        if (inverse is not null)
            point = inverse * point;

        if (pattern is ImagePattern)
        {
            Footprint patch = intersection.Footprint is { IsEmpty: false } footprint
                ? surface.WorldToSurface(footprint, intersection.TimeIndex, intersection.Portals)
                : null;

            return pattern.ValueFor(point, inverse is null ? patch : patch?.TransformedBy(inverse));
        }

        if (!pattern.SeesTheSurroundings)
            return pattern.ValueFor(point);

        Surroundings was = Surroundings.Current;

        Surroundings.Current = new Surroundings(
            scene, intersection.Surface, intersection.Point, intersection.Normal.Unit, intersection.Portals,
            intersection.TimeIndex);

        try
        {
            return pattern.ValueFor(point);
        }
        finally
        {
            Surroundings.Current = was;
        }
    }

    /// <summary>
    /// This method returns a material part way between two: every number of their finishes mixed in proportion,
    /// their colors mixed, and whatever cannot be mixed taken from the nearer.
    /// </summary>
    /// <param name="from">The material at nought.</param>
    /// <param name="to">The material at one.</param>
    /// <param name="share">How far between them, from nought to one.</param>
    /// <returns>The material between.</returns>
    public static Material Mixed(Material from, Material to, double share)
    {
        Material mixed = (share < 0.5 ? from : to).CopiedForBlending();

        // Between a metal and something else, the something else is what the metal is not: so how much of each
        // is bare metal mixes, and what is not bare metal -- its color, its own light, what it mirrors -- mixes
        // by how much of each is not.  A metal's own such numbers count for nothing where it is all metal.
        double fromMetal = from.Metal is null ? 0 : Math.Min(from.Metallic, 1);
        double toMetal = to.Metal is null ? 0 : Math.Min(to.Metallic, 1);
        double rest = share;

        if (from.Metal is not null || to.Metal is not null)
        {
            double fromRest = (1 - share) * (1 - fromMetal);
            double toRest = share * (1 - toMetal);
            double metal = (1 - share) * fromMetal + share * toMetal;

            rest = fromRest + toRest > 0 ? toRest / (fromRest + toRest) : share;
            mixed.Metal = from.Metal is null ? to.Metal
                : to.Metal is null ? from.Metal
                : Metal.Mixed(from.Metal, to.Metal, metal > 0 ? share * toMetal / metal : share);
        }

        mixed.Pigment = new MixedPigment { From = from.Pigment, To = to.Pigment, Share = rest };
        mixed.Ambient = Between(from.EffectiveAmbient, to.EffectiveAmbient, rest);
        mixed.Emission = from.Emission is null && to.Emission is null
            ? null
            : (from.Emission ?? Colors.Black) * (1 - rest) + (to.Emission ?? Colors.Black) * rest;
        mixed.Diffuse = Between(from.Diffuse, to.Diffuse, rest);
        mixed.Specular = Between(from.Specular, to.Specular, share);
        mixed.Shininess = Between(from.Shininess, to.Shininess, share);
        mixed.Reflective = Between(from.Reflective, to.Reflective, rest);
        mixed.Roughness = Between(from.Roughness, to.Roughness, share);
        mixed.Anisotropy = Between(from.Anisotropy, to.Anisotropy, share);
        mixed.Metallic = mixed.Metal is null
            ? Between(from.Metallic, to.Metallic, share)
            : Between(fromMetal, toMetal, share);
        mixed.Brilliance = Between(from.Brilliance, to.Brilliance, share);
        mixed.Grain = Between(from.Grain, to.Grain, share);
        mixed.Transparency = Between(from.Transparency, to.Transparency, share);
        mixed.ReflectionSamples = Math.Max(from.ReflectionSamples, to.ReflectionSamples);

        // Between a smooth material and a rough one the two are shaded each as itself and mixed; see
        // Scene.GetHitColor.  What asks of the mixture without a hit -- a shadow, a photon -- still has the numbers.
        if (from.IsRough != to.IsRough)
            mixed.Split = (from, to, share);

        return mixed;
    }

    private static double Between(double from, double to, double share) => from + (to - from) * share;
}
