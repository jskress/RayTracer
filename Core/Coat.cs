using System.Collections.Concurrent;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a clear coat over a surface: lacquer on a car, varnish on a table, the glaze on a
/// pot -- a layer of clear stuff with a surface of its own, over whatever the material is beneath it.
/// <para>
/// **The coat mirrors, and what it does not mirror reaches the surface beneath.**  It mirrors by its own
/// index of refraction, a few percent square-on and nearly all at a graze, as glass does; so a red car
/// seen from above is red, and seen along its flank carries the sky.  The rest of the light goes through
/// it to the material beneath -- paint, metal flakes, wood -- and whatever that gives back crosses the
/// coat again on its way out.  So everything the material beneath shows is charged for both crossings:
/// at the angle the light came in and at the angle the eye looks from.  Light reaching it from all round
/// at once -- ambient, bounced light, caustics -- is charged at the share such light gets through on
/// average.
/// </para>
/// <para>
/// **A coat may be tinted**, as an amber varnish or a candy paint is: <see cref="Tint"/> is the color the
/// coat gives what lies beneath it seen square-on, and seen at a slant, where the light crosses more of
/// the coat, it is deeper.  Light crosses the coat bent by its index, so a slant inside it is never as
/// steep as the slant outside.
/// </para>
/// <para>
/// **A smooth coat mirrors in one direction**, as a mirror does.  But no lacquer is truly flat, and a
/// lamp no ray can meet -- a point, a spot, an area light -- would show in a truly flat one not at all:
/// so its highlights are given as by facets <see cref="LampRoughness"/> rough, a small bright glint.  A
/// rough coat (an eggshell varnish) is rough as any surface is, its highlight and blurred reflection
/// from the same facets.
/// </para>
/// <para>
/// What is left out: light turned back and forth between the coat's underside and the surface beneath,
/// which deepens a real lacquered color a little; and the bending of directions on their way through it,
/// which the surface beneath is shaded without.
/// </para>
/// </summary>
public sealed class Coat
{
    /// <summary>
    /// This is the index of refraction a coat is given when it names none: lacquer's and varnish's, which
    /// is very nearly glass's.
    /// </summary>
    public const double DefaultIndexOfRefraction = 1.5;

    /// <summary>
    /// This is how rough a smooth coat's highlights are taken to be, for the lamps that no ray can meet.
    /// </summary>
    public const double LampRoughness = 0.05;

    // Steps across the cosine of the light arriving from all round, each weighed by twice its cosine: taken
    // evenly in the cosine's square instead, sixty-four of them fell short at a graze, where Schlick's share
    // climbs steeply, by a twentieth of what a coat mirrors from all round.
    private const int AroundSteps = 256;

    /// <summary>
    /// This property holds how rough the coat's own surface is, from nothing, a mirror, to one.
    /// </summary>
    public double Roughness { get; set; }

    /// <summary>
    /// This property holds the coat's index of refraction.
    /// </summary>
    public double IndexOfRefraction { get; set; } = DefaultIndexOfRefraction;

    /// <summary>
    /// This property holds the color the coat gives what lies beneath it, seen square-on, or <c>null</c>
    /// for a coat that is clear.
    /// </summary>
    public Color Tint { get; set; }

    /// <summary>
    /// This property reports whether the coat's own surface is rough.
    /// </summary>
    public bool IsRough => Roughness > 0;

    /// <summary>
    /// This property returns how far the coat's facets lean: the square of its roughness.
    /// </summary>
    public double Alpha => Roughness * Roughness;

    /// <summary>
    /// This property returns how far the facets lean that the coat's highlights come from: its own, or, for
    /// a smooth coat, those of <see cref="LampRoughness"/>.
    /// </summary>
    public double HighlightAlpha => IsRough ? Alpha : LampRoughness * LampRoughness;

    /// <summary>
    /// This method returns how far the facets lean that the coat's highlight of one sample of a lamp comes
    /// from, where the sample stands for a patch of the lamp's face the given angle across: spread over that
    /// patch as any rough surface's highlight is, so that a window in lacquer is one soft patch rather than a
    /// grid of dots; see <see cref="Microfacets.WidenedBy"/>.
    /// </summary>
    /// <param name="spread">The angle across the patch of the lamp the sample stands for, in radians.</param>
    /// <returns>How far the facets lean.</returns>
    public double HighlightAlphaFor(double spread) => Microfacets.WidenedBy(HighlightAlpha, spread);

    /// <summary>
    /// This method returns how much the coat mirrors of light meeting it at a given angle.
    /// </summary>
    /// <param name="cos">The cosine of the angle between the light, or the eye, and the normal.</param>
    /// <param name="outside">The index of refraction of what lies outside the coat.</param>
    /// <returns>The share mirrored.</returns>
    public double ReflectanceAt(double cos, double outside) =>
        Microfacets.GlassReflectance(cos, outside, IndexOfRefraction);

    /// <summary>
    /// This method returns how much of the light reaching the surface beneath from one direction gets
    /// through the coat, in at that direction and out toward the eye.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="cosEye">The cosine of the angle between the eye and the normal.</param>
    /// <param name="cosLight">The cosine of the angle between the light and the normal.</param>
    /// <param name="outside">The index of refraction of what lies outside the coat.</param>
    /// <returns>The share of each band that gets through.</returns>
    public TS Through<TS>(double cosEye, double cosLight, double outside)
        where TS : struct, ISpectrum<TS>
    {
        double passing = (1 - ReflectanceAt(cosEye, outside)) * (1 - ReflectanceAt(cosLight, outside));

        return Tint is null
            ? TS.White * passing
            : Tinted<TS>((1 / Within(cosEye, outside) + 1 / Within(cosLight, outside)) / 2) * passing;
    }

    /// <summary>
    /// This method returns how much of the light reaching the surface beneath from all round at once gets
    /// through the coat, in and out toward the eye.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="cosEye">The cosine of the angle between the eye and the normal.</param>
    /// <param name="outside">The index of refraction of what lies outside the coat.</param>
    /// <returns>The share of each band that gets through.</returns>
    public TS ThroughFromAround<TS>(double cosEye, double outside)
        where TS : struct, ISpectrum<TS>
    {
        (double passing, double path) = AroundFor(outside);

        passing *= 1 - ReflectanceAt(cosEye, outside);

        return Tint is null
            ? TS.White * passing
            : Tinted<TS>((1 / Within(cosEye, outside) + path) / 2) * passing;
    }

    /// <summary>
    /// This method returns how much of the light made beneath the coat -- what the surface there gives off
    /// of its own -- gets out through it toward the eye.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="cosEye">The cosine of the angle between the eye and the normal.</param>
    /// <param name="outside">The index of refraction of what lies outside the coat.</param>
    /// <returns>The share of each band that gets out.</returns>
    public TS ThroughOut<TS>(double cosEye, double outside)
        where TS : struct, ISpectrum<TS>
    {
        double passing = 1 - ReflectanceAt(cosEye, outside);

        return Tint is null
            ? TS.White * passing
            : Tinted<TS>(1 / (2 * Within(cosEye, outside))) * passing;
    }

    /// <summary>
    /// This method returns how much the coat mirrors of light reaching it from all round at once, weighed by
    /// the cosine, as a matte surface beneath would take it.
    /// </summary>
    /// <param name="outside">The index of refraction of what lies outside the coat.</param>
    /// <returns>The share mirrored.</returns>
    public double AverageReflectance(double outside) => 1 - AroundFor(outside).Passing;

    /// <summary>
    /// This method returns this coat made as rough as the given roughness, for a ray a rough surface has
    /// already spread; see <see cref="Material.RoughAs"/>, which asks it only of a smooth coat.
    /// </summary>
    /// <param name="roughness">The roughness to make it.</param>
    /// <returns>The coat, made rough.</returns>
    public Coat RoughAs(double roughness) => new ()
    {
        Roughness = roughness, IndexOfRefraction = IndexOfRefraction, Tint = Tint
    };

    /// <summary>
    /// This method returns the cosine of the angle a direction makes with the normal inside the coat, having
    /// been bent going in.  It never falls to nothing: a coat of a lower index than what lies outside it
    /// would turn back light at a graze, and the little that is left is taken as crossing steeply.
    /// </summary>
    private double Within(double cos, double outside)
    {
        double ratio = outside / IndexOfRefraction;
        double sin2 = ratio * ratio * (1 - Math.Clamp(cos, 0, 1) * Math.Clamp(cos, 0, 1));

        return Math.Max(Math.Sqrt(Math.Max(0, 1 - sin2)), 0.05);
    }

    /// <summary>
    /// This method returns the tint raised to the given power, band by band: how much of each band is left
    /// after crossing the coat so many times its thickness, counting twice its thickness as once.
    /// </summary>
    private TS Tinted<TS>(double power)
        where TS : struct, ISpectrum<TS>
    {
        TS tint = TS.FromReflectance(Tint);

        for (int band = 0; band < TS.Count; band++)
            tint[band] = Math.Pow(Math.Max(tint[band], 0), power);

        return tint;
    }

    /// <summary>
    /// This method returns, for light reaching the coat from all round, the share of it that gets through
    /// and how many times the coat's thickness it crosses on average, worked out once for each index outside.
    /// </summary>
    private (double Passing, double Path) AroundFor(double outside)
    {
        return _around.GetOrAdd(outside, index =>
        {
            double passing = 0;
            double path = 0;

            for (int step = 0; step < AroundSteps; step++)
            {
                double cos = (step + 0.5) / AroundSteps;
                double weight = 2 * cos / AroundSteps;

                passing += weight * (1 - ReflectanceAt(cos, index));
                path += weight / Within(cos, index);
            }

            return (passing, path);
        });
    }

    private readonly ConcurrentDictionary<double, (double Passing, double Path)> _around = new ();
}
