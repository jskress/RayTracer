using System.Diagnostics.CodeAnalysis;

namespace RayTracer.Basics;

/// <summary>
/// This class sums layers of noise, each finer and fainter than the one before, which is the thing
/// both turbulence and mottling are built from.
/// <para>
/// It exists on its own because those two want genuinely different halves of it.  Mottling wants a
/// single number to dim a colour by; turbulence wants a direction to push a point in, and how far
/// to push is its own business rather than this one's.  Keeping the summing here and the pushing in
/// <see cref="Turbulence"/> is what stops an amplitude from turning up where it can have no
/// meaning.
/// </para>
/// </summary>
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class LayeredNoise : INoiseConsumer
{
    /// <summary>
    /// This property holds the seed for the noise generator to use.  If it is not specified, a
    /// default noise generator will be used.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// This property controls how many layers of noise are summed.  Each is sampled at
    /// <see cref="Finer"/> times the rate of the one before it and contributes <see cref="Fainter"/>
    /// times as much, so the early layers give the shape and the later ones the detail.
    /// </summary>
    public int Octaves { get; set; } = 1;

    /// <summary>
    /// This property holds how much finer each layer of noise is than the one before it.  Two is
    /// the usual value, and the one POV-Ray defaults to under the name "lambda": each layer is
    /// sampled at twice the rate, so its features are half the size.
    /// </summary>
    public double Finer { get; set; } = 2;

    /// <summary>
    /// This property holds how much fainter each layer of noise is than the one before it.  A half
    /// is the usual value, and POV-Ray's default under the name "omega": it is what keeps a sum of
    /// many layers finite, and what stops the fine detail from drowning out the shape.
    /// </summary>
    public double Fainter { get; set; } = 0.5;

    /// <summary>
    /// This method sums the layers at the given point and returns the single number they come to.
    /// </summary>
    /// <param name="point">The point to determine some noise for.</param>
    /// <returns>The summed noise at that point.</returns>
    public double Generate(Point point)
    {
        NoiseGenerator generator = NoiseGenerator.ForSeed(Seed);

        if (Octaves == 0)
            return generator.Noise(point);

        double noise = 0.0;
        double weight = 1.0;
        Point sample = point;

        for (int i = 0; i < Octaves; i++)
        {
            noise += weight * generator.Noise(sample);
            weight *= Fainter;
            sample = new Point(sample.X * Finer, sample.Y * Finer, sample.Z * Finer);
        }

        // Note there is deliberately no Math.Abs here: POV-Ray's own scalar Turbulence() has
        // none, and since Noise() never returns a negative value, the accumulated sum can't be
        // negative either.  (An Abs call used to live here, compensating for noise that was
        // wrongly centered on zero -- see NoiseGenerator.Noise.)
        return noise;
    }

    /// <summary>
    /// This method sums the layers as a vector rather than a single number, so that a caller with
    /// more than one axis to move gets a genuinely different amount for each.  It is the
    /// counterpart to <see cref="Generate"/>, mirroring the way POV-Ray pairs its own
    /// <c>Turbulence()</c> with <c>DTurbulence()</c>.
    /// </summary>
    /// <param name="point">The point to determine some noise for.</param>
    /// <returns>The summed noise at that point, as a vector.</returns>
    public Vector GenerateVector(Point point)
    {
        NoiseGenerator generator = NoiseGenerator.ForSeed(Seed);

        if (Octaves == 0)
            return generator.DNoise(point);

        Vector result = new (0, 0, 0);
        double weight = 1.0;
        Point sample = point;

        for (int i = 0; i < Octaves; i++)
        {
            result += generator.DNoise(sample) * weight;
            weight *= Fainter;
            sample = new Point(sample.X * Finer, sample.Y * Finer, sample.Z * Finer);
        }

        return result;
    }
}
