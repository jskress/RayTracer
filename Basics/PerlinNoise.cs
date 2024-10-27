using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class provides the ability to introduce noise into things using Perlin noise.
/// </summary>
public class PerlinNoise
{
    private const int TableSize = 256;
    
    private static readonly Dictionary<int, PerlinNoise> NoiseGenerators = new ();

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
        PerlinNoise noise = DefaultInstance;

        if (seed.HasValue)
        {
            int value = seed.Value;

            if (!NoiseGenerators.TryGetValue(value, out noise))
            {
                NoiseGenerators[value] = noise = new PerlinNoise(
                    ThreadSafeRandom.GetGenerator(value));
            }
        }

        return noise; 
    }

    private static readonly PerlinNoise DefaultInstance = new (ThreadSafeRandom.GetGenerator());

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
    /// This method generates a noise factor for the given point.
    /// </summary>
    /// <param name="point">The point to generate noise for.</param>
    /// <returns>A noise value for the point.</returns>
    public double Noise(Point point)
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
