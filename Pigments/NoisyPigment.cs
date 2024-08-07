using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that applies noise to a wrapped pigment.
/// </summary>
public class NoisyPigment : Pigment
{
    private static readonly PerlinNoise PerlinNoise = new ();

    private readonly Pigment _wrappedPigment;

    /// <summary>
    /// This property controls the depth of the turbulence we will apply.
    /// </summary>
    public int Depth { get; set; } = 1;

    /// <summary>
    /// This property controls whether we will apply phasing to the turbulence.
    /// </summary>
    public bool Phased { get; set; }

    /// <summary>
    /// This property controls the tightness of the turbulence we will apply.  It applies
    /// only if <see cref="Phased"/> is <c>true</c>.
    /// </summary>
    public int Tightness { get; set; } = 10;

    /// <summary>
    /// This property controls the scale of the turbulence we will apply.  It applies only
    /// if <see cref="Phased"/> is <c>true</c>.
    /// </summary>
    public double Scale { get; set; } = 1;

    public NoisyPigment(Pigment wrappedPigment)
    {
        _wrappedPigment = wrappedPigment;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  We return the
    /// color our wrapped pigment returns, with noise applied.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        double noise = PerlinNoise.Turbulence(point, Depth);

        if (Phased)
            noise = 1 + Math.Sin(Scale * point.Z + Tightness * noise);

        return _wrappedPigment.GetColorFor(point) * noise;
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is NoisyPigment pigmentation &&
               _wrappedPigment.Matches(pigmentation._wrappedPigment);
    }
}
