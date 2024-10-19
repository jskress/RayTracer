using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that applies noise to a wrapped pigment.
/// </summary>
public class NoisyPigment : Pigment
{
    /// <summary>
    /// This property holds the pigment to apply noise to.
    /// </summary>
    public Pigment Pigment { get; init; }

    /// <summary>
    /// This property controls the turbulence we will apply.
    /// </summary>
    public Turbulence Turbulence { get; init; }

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        Pigment.SetSeed(seed);
        Turbulence.Seed ??= seed;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  We return the
    /// color that our wrapped pigment returns, with noise applied.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        return Pigment.GetColorFor(point) * Turbulence.Generate(point);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is NoisyPigment pigmentation &&
               Pigment.Matches(pigmentation.Pigment);
    }
}
