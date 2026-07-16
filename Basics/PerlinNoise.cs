using System.Collections.Concurrent;
using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class provides the ability to introduce noise into things using Perlin noise.
/// </summary>
public class PerlinNoise
{
    private const int TableSize = 256;

    /// <summary>
    /// The seed used when a scene doesn't name one of its own.  It must be a fixed value, not
    /// a random one: the noise tables are built from it, so seeding them from a shared,
    /// arbitrarily-seeded generator would make every render of the same scene produce
    /// different noise.
    /// </summary>
    private const int DefaultSeed = 0;

    // These scale and bias the raw gradient noise below into the [0, 1] interval, with a mean
    // of about 0.5.  Every pattern that consumes noise here was ported from POV-Ray and so
    // assumes POV's own contract for Noise(): 0 to 1, never negative, mean ~0.49.  Raw gradient
    // noise is instead centered on 0 and runs about -0.63 to 0.66, so handing it to those
    // patterns unadjusted puts their carefully-chosen bias points (e.g. granite's "0.5 -
    // noise") at the tail of the distribution rather than the middle of it.  These are POV's
    // own constants, from Noise() in source/backend/texture/texture.cpp; they map a raw range
    // of [-0.6195, 0.6384] onto [0, 1], which fits this generator's measured range closely
    // enough that the resulting mean lands on ~0.491 against POV's documented ~0.49.
    private const double NoiseScale = 1.59;
    private const double NoiseBias = 0.985;

    private static readonly ConcurrentDictionary<int, PerlinNoise> NoiseGenerators = new ();

    /// <summary>
    /// This method returns an appropriate noise generator.
    /// If a specific random number generator seed is not provided, a default shared
    /// instance is returned.
    /// The same seed will always yield the same generator.
    /// </summary>
    /// <param name="seed">The seed to the random number generator to use.</param>
    /// <returns>The appropriate noise generator.</returns>
    public static PerlinNoise GetNoise(int? seed = null)
    {
        return NoiseGenerators.GetOrAdd(
            seed ?? DefaultSeed, value => new PerlinNoise(ThreadSafeRandom.GetGenerator(value)));
    }

    private readonly ThreadSafeRandom _rng;
    private readonly Vector[] _numbers;
    private readonly int[] _x;
    private readonly int[] _y;
    private readonly int[] _z;

    private PerlinNoise(ThreadSafeRandom rng)
    {
        _rng = rng;
        _numbers = new Vector[TableSize];
        _x = new int[TableSize];
        _y = new int[TableSize];
        _z = new int[TableSize];

        for (int index = 0; index < TableSize; index++)
            _numbers[index] = RandomVector();

        GenerateAxis(_x);
        GenerateAxis(_y);
        GenerateAxis(_z);
    }

    /// <summary>
    /// This method is used to generate the contents of one of our three axis tables.
    /// </summary>
    /// <param name="data">the axis table to populate.</param>
    private void GenerateAxis(int[] data)
    {
        for (int index = 0; index < data.Length; index++)
            data[index] = index;

        _rng.Shuffle(data);
    }

    /// <summary>
    /// This method generates a noise factor for the given point.  The value returned lies in
    /// the [0, 1] interval, with a mean of about 0.5, matching the contract POV-Ray's own
    /// <c>Noise()</c> provides -- see <see cref="NoiseScale"/> for why that matters.
    /// </summary>
    /// <param name="point">The point to generate noise for.</param>
    /// <returns>A noise value for the point, in the [0, 1] interval.</returns>
    public double Noise(Point point)
    {
        double value = 0.5 * (NoiseScale * RawNoise(point) + NoiseBias);

        return value switch
        {
            < 0 => 0,
            > 1 => 1,
            _ => value
        };
    }

    /// <summary>
    /// This method generates a noise vector for the given point, the way POV-Ray's own
    /// <c>DNoise()</c> does.  Each component is an independent noise value, so callers that
    /// need to displace more than one axis (the wood pattern, say) get a genuinely different
    /// amount per axis rather than the same one repeated.
    ///
    /// Unlike <see cref="Noise"/>, the components are left as raw, zero-centered noise rather
    /// than biased into [0, 1] -- callers want a signed displacement here, and POV leaves its
    /// own <c>DNoise()</c> unbiased for the same reason.  See <see cref="RawNoise"/> for the
    /// magnitude to expect.
    /// </summary>
    /// <param name="point">The point to generate a noise vector for.</param>
    /// <returns>A noise vector for the point.</returns>
    public Vector DNoise(Point point)
    {
        // Sampling the one scalar field at three points far apart from each other gives three
        // effectively independent values.  The offsets are arbitrary, but need to be large
        // enough, and share no common factor, so that the samples can't correlate.
        return new Vector(
            RawNoise(point),
            RawNoise(new Point(point.X + 137.31, point.Y - 71.53, point.Z + 29.17)),
            RawNoise(new Point(point.X - 43.79, point.Y + 113.61, point.Z - 89.23)));
    }

    /// <summary>
    /// This method generates the underlying gradient noise for the given point.  The value is
    /// centered on zero; sampling ~48 million points put its extent at about -0.724 to 0.694.
    /// Callers wanting POV-Ray's [0, 1] contract should use <see cref="Noise"/> instead.
    /// </summary>
    /// <param name="point">The point to generate noise for.</param>
    /// <returns>A raw, zero-centered noise value for the point.</returns>
    private double RawNoise(Point point)
    {
        double u = point.X.Fraction();
        double v = point.Y.Fraction();
        double w = point.Z.Fraction();

        int i = Convert.ToInt32(Math.Floor(point.X));
        int j = Convert.ToInt32(Math.Floor(point.Y));
        int k = Convert.ToInt32(Math.Floor(point.Z));
        Vector[,,] buffer = new Vector[2, 2, 2];

        for (int di=0; di < 2; di++)
        {
            for (int dj = 0; dj < 2; dj++)
            {
                for (int dk = 0; dk < 2; dk++)
                {
                    buffer[di, dj, dk] = _numbers[
                        _x[(i + di) & 255] ^
                        _y[(j + dj) & 255] ^
                        _z[(k + dk) & 255]
                    ];
                }
            }
        }

        return PerlinInterpolation(buffer, u, v, w);
    }

    /// <summary>
    /// This method is used to calculate the Perlin interpolation over the array of vectors
    /// provided.
    /// </summary>
    /// <param name="buffer">The buffer of vectors to interpolate over.</param>
    /// <param name="u">The fractional part of the current point's X coordinate.</param>
    /// <param name="v">The fractional part of the current point's Y coordinate.</param>
    /// <param name="w">The fractional part of the current point's Z coordinate.</param>
    /// <returns>The resulting interpolated value.</returns>
    private static double PerlinInterpolation(Vector[,,] buffer, double u, double v, double w)
    {
        double uu = u * u * (3 - 2 * u);
        double vv = v * v * (3 - 2 * v);
        double ww = w * w * (3 - 2 * w);

        double u1 = 1 - uu;
        double v1 = 1 - vv;
        double w1 = 1 - ww;

        double accumulator = 0;
        
        for (int i=0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                for (int k = 0; k < 2; k++)
                {
                    Vector weight = new (u-i, v-j, w-k);

                    accumulator += (i * uu + (1 - i) * u1) *
                                   (j * vv + (1 - j) * v1) *
                                   (k * ww + (1 - k) * w1) *
                                   buffer[i, j, k].Dot(weight);
                }
            }
        }
        
        return accumulator;
    }

    /// <summary>
    /// This is a helper method for generating a random unit vector in a given cube.  By
    /// default, the cube is the [-1, -1, -1]/[1, 1, 1] space.
    /// </summary>
    /// <param name="min">The minimum of the interval.</param>
    /// <param name="max">The maximum of the interval.</param>
    /// <returns>A random vector in the space.</returns>
    private Vector RandomVector(double min = -1, double max = 1)
    {
        return new Vector(
            _rng.NextDouble(min, max),
            _rng.NextDouble(min, max),
            _rng.NextDouble(min, max)).Unit;
    }
}
