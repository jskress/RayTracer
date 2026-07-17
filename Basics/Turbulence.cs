using System.Diagnostics.CodeAnalysis;

namespace RayTracer.Basics;

/// <summary>
/// This class provides a generator for turbulence.
/// </summary>
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class Turbulence : INoiseConsumer
{
    /// <summary>
    /// This property holds the seed for the noise generator to use.
    /// If it is not specified, a default noise generator will be used.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// This property controls the depth of the turbulence we will apply.
    /// </summary>
    public int Depth { get; set; } = 1;

    /// <summary>
    /// This property controls whether we will apply phasing to the turbulence.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
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

    /// <summary>
    /// This method produces turbulence by accumulating multiple calls to the Perlin noise
    /// generator we were constructed with.
    /// </summary>
    /// <param name="point">The point to determine some noise for.</param>
    /// <returns></returns>
    public double Generate(Point point)
    {
        PerlinNoise generator = PerlinNoise.GetNoise(Seed);

        if (Depth == 0)
            return generator.Noise(point);

        double noise = 0.0;
        double weight = 1.0;
        Point sample = point;

        for (int i = 0; i < Depth; i++)
        {
            noise += weight * generator.Noise(sample);
            weight *= 0.5;
            sample = new Point(sample.X * 2, sample.Y * 2, sample.Z * 2);
        }

        // Note there is deliberately no Math.Abs here: POV-Ray's own scalar Turbulence() has
        // none, and since Noise() never returns a negative value, the accumulated sum can't be
        // negative either.  (An Abs call used to live here, compensating for noise that was
        // wrongly centered on zero -- see PerlinNoise.Noise.)

        if (Phased)
        {
            // The original point, not the octave-scaled one the loop left behind: phasing is
            // meant to ride along the surface's own Z, not Z multiplied by 2^Depth.
            noise = 1 + Math.Sin(Scale * point.Z + Tightness * noise);
        }

        return noise;
    }

    /// <summary>
    /// This method produces turbulence as a vector rather than a single number, by
    /// accumulating octaves of <see cref="PerlinNoise.DNoise"/>.  It is the counterpart to
    /// <see cref="Generate"/>, mirroring the way POV-Ray pairs its own <c>Turbulence()</c> with
    /// <c>DTurbulence()</c>, and exists for callers that need to displace more than one axis
    /// and so need a genuinely different amount for each.
    ///
    /// Phasing is deliberately not applied here: it has no counterpart in POV's
    /// <c>DTurbulence()</c>, and it folds its result into a single number, which is exactly
    /// what a caller asking for a vector doesn't want.
    /// </summary>
    /// <param name="point">The point to determine some turbulence for.</param>
    /// <returns>The turbulence at that point, as a vector.</returns>
    public Vector GenerateVector(Point point)
    {
        PerlinNoise generator = PerlinNoise.GetNoise(Seed);

        if (Depth == 0)
            return generator.DNoise(point);

        Vector result = new (0, 0, 0);
        double weight = 1.0;
        Point sample = point;

        for (int i = 0; i < Depth; i++)
        {
            result += generator.DNoise(sample) * weight;
            weight *= 0.5;
            sample = new Point(sample.X * 2, sample.Y * 2, sample.Z * 2);
        }

        return result;
    }
}
