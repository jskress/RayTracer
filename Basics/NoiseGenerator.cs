using System.Collections.Concurrent;
using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class is the source of noise the patterns draw on.  The field underneath is lattice
/// gradient noise -- a grid of random gradients, smoothly interpolated between -- of the kind
/// often loosely called "Perlin", though it is not Ken Perlin's own algorithm and is not named
/// for it here.  What actually defines this class is the shape of what it hands back rather than
/// how it makes it: <see cref="Noise"/> honors POV-Ray's contract for its own <c>Noise()</c>,
/// since every pattern here was ported assuming exactly that (see <see cref="NoiseScale"/>).
/// </summary>
public class NoiseGenerator
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

    private static readonly ConcurrentDictionary<int, NoiseGenerator> NoiseGenerators = new ();

    /// <summary>
    /// This method returns the noise generator for the given seed, building it on first use and
    /// caching it thereafter, so the same seed always yields the very same generator.  With no
    /// seed given, the shared default one is returned.
    /// </summary>
    /// <param name="seed">The seed whose generator is wanted, or nothing for the default.</param>
    /// <returns>The generator for that seed.</returns>
    public static NoiseGenerator ForSeed(int? seed = null)
    {
        return NoiseGenerators.GetOrAdd(seed ?? DefaultSeed, value => new NoiseGenerator(value));
    }

    private readonly Vector[] _numbers;
    private readonly int[] _x;
    private readonly int[] _y;
    private readonly int[] _z;

    private NoiseGenerator(int seed)
    {
        // The generator here is deliberately private to this constructor, rather than one of
        // the shared cached ones: those carry their position in their own sequence, so what a
        // caller draws from one depends on who else has drawn from it and when.  Scanners run
        // this constructor from several threads at once (GetOrAdd is free to invoke its factory
        // more than once, and only keep one result), and threads sharing a generator would
        // interleave their draws and build different tables from the same seed -- leaving the
        // noise, and so the whole render, at the mercy of which thread happened to win.
        Random rng = new (seed);

        _numbers = new Vector[TableSize];
        _x = new int[TableSize];
        _y = new int[TableSize];
        _z = new int[TableSize];

        for (int index = 0; index < TableSize; index++)
            _numbers[index] = RandomVector(rng);

        GenerateAxis(rng, _x);
        GenerateAxis(rng, _y);
        GenerateAxis(rng, _z);
    }

    /// <summary>
    /// This method is used to generate the contents of one of our three axis tables.
    /// </summary>
    /// <param name="rng">The random number generator to draw from.</param>
    /// <param name="data">the axis table to populate.</param>
    private static void GenerateAxis(Random rng, int[] data)
    {
        for (int index = 0; index < data.Length; index++)
            data[index] = index;

        rng.Shuffle(data);
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
        return Noise(point.X, point.Y, point.Z);
    }

    /// <summary>
    /// This method generates a noise factor for the given coordinates, in the [0, 1] interval, exactly
    /// as <see cref="Noise(Point)"/> does.  It takes three numbers so that a compiled field function can
    /// ask for noise without building a point to ask with, which matters when it asks millions of times.
    /// </summary>
    /// <param name="x">The X of the point to generate noise for.</param>
    /// <param name="y">Its Y.</param>
    /// <param name="z">Its Z.</param>
    /// <returns>A noise value for those coordinates, in the [0, 1] interval.</returns>
    public double Noise(double x, double y, double z)
    {
        double value = 0.5 * (NoiseScale * RawNoise(x, y, z) + NoiseBias);

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
        return RawNoise(point.X, point.Y, point.Z);
    }

    /// <summary>
    /// This method generates the underlying gradient noise for the given coordinates.  The value is
    /// centered on zero; sampling ~48 million points put its extent at about -0.724 to 0.694.
    /// Callers wanting POV-Ray's [0, 1] contract should use <see cref="Noise(double,double,double)"/>
    /// instead.
    /// <para>
    /// This takes three numbers rather than a point, and holds the eight corner gradients in locals
    /// rather than in an array, because allocating nothing here is worth having: a field function may
    /// ask for noise at every step of every ray, and an array per sample would be millions of them a
    /// frame.  The arithmetic is deliberately in the same order, and grouped the same way, as the
    /// array-and-loops version it replaces, so every noise value it gives is the same to the last bit
    /// and nothing drawn with it moves.
    /// </para>
    /// </summary>
    /// <param name="x">The X of the point to generate noise for.</param>
    /// <param name="y">Its Y.</param>
    /// <param name="z">Its Z.</param>
    /// <returns>A raw, zero-centered noise value for those coordinates.</returns>
    private double RawNoise(double x, double y, double z)
    {
        double u = x.Fraction();
        double v = y.Fraction();
        double w = z.Fraction();

        int i = Convert.ToInt32(Math.Floor(x));
        int j = Convert.ToInt32(Math.Floor(y));
        int k = Convert.ToInt32(Math.Floor(z));

        int x0 = _x[i & 255], x1 = _x[(i + 1) & 255];
        int y0 = _y[j & 255], y1 = _y[(j + 1) & 255];
        int z0 = _z[k & 255], z1 = _z[(k + 1) & 255];

        // The smoothstep weights, and their complements.
        double uu = u * u * (3 - 2 * u);
        double vv = v * v * (3 - 2 * v);
        double ww = w * w * (3 - 2 * w);
        double uc = 1 - uu;
        double vc = 1 - vv;
        double wc = 1 - ww;

        // The eight corners, summed in the order the nested loops visited them.
        double accumulator = Corner(x0 ^ y0 ^ z0, uc, vc, wc, u, v, w);

        accumulator += Corner(x0 ^ y0 ^ z1, uc, vc, ww, u, v, w - 1);
        accumulator += Corner(x0 ^ y1 ^ z0, uc, vv, wc, u, v - 1, w);
        accumulator += Corner(x0 ^ y1 ^ z1, uc, vv, ww, u, v - 1, w - 1);
        accumulator += Corner(x1 ^ y0 ^ z0, uu, vc, wc, u - 1, v, w);
        accumulator += Corner(x1 ^ y0 ^ z1, uu, vc, ww, u - 1, v, w - 1);
        accumulator += Corner(x1 ^ y1 ^ z0, uu, vv, wc, u - 1, v - 1, w);
        accumulator += Corner(x1 ^ y1 ^ z1, uu, vv, ww, u - 1, v - 1, w - 1);

        return accumulator;
    }

    /// <summary>
    /// This method returns one corner's contribution to a noise value: the three weights multiplied
    /// together and then by how far the gradient there points along the way to the point.
    /// </summary>
    /// <param name="index">Which of the gradients this corner uses.</param>
    /// <param name="uWeight">The corner's weight along X.</param>
    /// <param name="vWeight">Its weight along Y.</param>
    /// <param name="wWeight">Its weight along Z.</param>
    /// <param name="dx">How far the point is from the corner along X.</param>
    /// <param name="dy">How far along Y.</param>
    /// <param name="dz">How far along Z.</param>
    /// <returns>The corner's contribution.</returns>
    private double Corner(
        int index, double uWeight, double vWeight, double wWeight, double dx, double dy, double dz)
    {
        Vector gradient = _numbers[index];

        return uWeight * vWeight * wWeight *
               (gradient.X * dx + gradient.Y * dy + gradient.Z * dz);
    }

    /// <summary>
    /// This is a helper method for generating a random unit vector in a given cube.  By
    /// default, the cube is the [-1, -1, -1]/[1, 1, 1] space.
    /// </summary>
    /// <param name="min">The minimum of the interval.</param>
    /// <param name="max">The maximum of the interval.</param>
    /// <returns>A random vector in the space.</returns>
    private static Vector RandomVector(Random rng, double min = -1, double max = 1)
    {
        return new Vector(
            min + rng.NextDouble() * (max - min),
            min + rng.NextDouble() * (max - min),
            min + rng.NextDouble() * (max - min)).Unit;
    }
}
