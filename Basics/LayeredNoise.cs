using System.Diagnostics.CodeAnalysis;

namespace RayTracer.Basics;

/// <summary>
/// This class sums layers of noise, each finer and fainter than the one before, which is the thing
/// both turbulence and mottling are built from.
/// <para>
/// It exists on its own because those two want genuinely different halves of it.  Mottling wants a
/// single number to dim a color by; turbulence wants a direction to push a point in, and how far
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

    /// <summary>
    /// This method reports how much of a layer is worth summing, given how much of the world one
    /// ray is answerable for.
    /// <para>
    /// **This is what stops noise from aliasing, and it is one method because every pattern with
    /// noise in it comes through here.**  Each layer is finer than the one before by
    /// <see cref="Finer"/>, so layer <c>i</c> has features about <c>1 / Finer^i</c> across.  Once
    /// those features are smaller than the patch a single ray covers, no amount of sampling can
    /// tell them apart -- they are past the point where the picture can hold them, and summing them
    /// only adds a value that leaps about between neighbouring pixels.  Which is aliasing.
    /// </para>
    /// <para>
    /// So a layer finer than the patch is left out.  It is faded rather than dropped sharply,
    /// because a layer that vanished the instant a surface passed some distance would show as a
    /// visible seam running across it -- the same reason a mip map blends between its levels rather
    /// than stepping.  Full weight while its features are twice the patch or bigger, nothing once
    /// they are smaller than the patch, and eased across in between.
    /// </para>
    /// <para>
    /// Note what this deliberately does *not* do: it does not fade the whole pattern toward its
    /// average.  The coarse layers -- the veining of marble, the swirl of agate -- are the shape of
    /// the thing and are still perfectly resolvable at any sane distance, so they are kept whole.
    /// Only the grain goes, which is exactly what happens to a real surface as it recedes.
    /// </para>
    /// </summary>
    /// <param name="layer">Which layer, counting from nought.</param>
    /// <param name="width">How wide a patch one ray covers, in the same space as the points.</param>
    /// <returns>What fraction of this layer to sum, from nought to one.</returns>
    private double WorthSumming(int layer, double width)
    {
        return WorthSummingAt(Math.Pow(Finer, -layer), width);
    }

    /// <summary>
    /// This method is the same judgement told the feature size outright, for the patterns that sum
    /// their own layers rather than coming through here -- granite, wrinkles and crackle each have
    /// a loop of their own.  They share this so that they all fade a layer out the same way.
    /// </summary>
    /// <param name="features">How big the things this layer draws are.</param>
    /// <param name="width">How wide a patch one ray covers, in the same units.</param>
    /// <returns>What fraction of the layer to sum, from nought to one.</returns>
    public static double WorthSummingAt(double features, double width)
    {
        if (width <= 0)
            return 1;

        if (features >= width * 2)
            return 1;

        if (features <= width)
            return 0;

        // Eased rather than straight, so neither end of the fade shows as an edge.
        double part = (features - width) / width;

        return part * part * (3 - 2 * part);
    }

    /// <summary>
    /// This method sums the layers as a vector, leaving out the ones finer than the given patch --
    /// see <see cref="WorthSumming"/>.
    /// </summary>
    /// <param name="point">The point to determine some noise for.</param>
    /// <param name="width">How wide a patch one ray covers there.</param>
    /// <returns>The summed noise at that point, as a vector.</returns>
    public Vector GenerateVector(Point point, double width)
    {
        if (width <= 0 || Octaves == 0)
            return GenerateVector(point);

        NoiseGenerator generator = NoiseGenerator.ForSeed(Seed);
        Vector result = new (0, 0, 0);
        double weight = 1.0;
        Point sample = point;

        for (int i = 0; i < Octaves; i++)
        {
            double worth = WorthSumming(i, width);

            // Breaking out is right *here* and wrong in the scalar form above, which is worth being
            // clear about.  This sums `DNoise`, which is a displacement and averages to nothing --
            // measured at about a thousandth on each axis over 64,000 samples -- so a layer left out
            // takes nothing with it.  The scalar form sums a value that averages to about a half.
            if (worth <= 0)
                break;

            result += generator.DNoise(sample) * (weight * worth);
            weight *= Fainter;
            sample = new Point(sample.X * Finer, sample.Y * Finer, sample.Z * Finer);
        }

        return result;
    }

    /// <summary>
    /// This method sums the layers, leaving out the ones finer than the given patch -- see
    /// <see cref="WorthSumming"/>.
    /// </summary>
    /// <param name="point">The point to determine some noise for.</param>
    /// <param name="width">How wide a patch one ray covers there.</param>
    /// <returns>The summed noise at that point.</returns>
    public double Generate(Point point, double width)
    {
        if (width <= 0 || Octaves == 0)
            return Generate(point);

        NoiseGenerator generator = NoiseGenerator.ForSeed(Seed);
        double noise = 0.0;
        double weight = 1.0;
        Point sample = point;

        for (int i = 0; i < Octaves; i++)
        {
            double worth = WorthSumming(i, width);

            // Faded toward the noise's own average rather than toward nought, and the loop runs to
            // the end rather than breaking out.  A layer too fine to see still contributes what it
            // contributes *on average*, and taking that away as well shifts the whole sum -- see
            // NoiseGenerator.AverageValue.  Only the call itself is skipped, which is the cost.
            noise += weight * (worth <= 0
                ? NoiseGenerator.AverageValue
                : worth * generator.Noise(sample) + (1 - worth) * NoiseGenerator.AverageValue);
            weight *= Fainter;
            sample = new Point(sample.X * Finer, sample.Y * Finer, sample.Z * Finer);
        }

        return noise;
    }

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
