namespace RayTracer.Basics;

/// <summary>
/// This class provides a thread-safe random number generator.
/// </summary>
public class ThreadSafeRandom
{
    private static readonly Dictionary<int, ThreadSafeRandom> Generators = new ();
    private static readonly ThreadSafeRandom Shared = new ();

    /// <summary>
    /// This method returns an appropriate random number generator.
    /// If a specific random number generator seed is not provided, a default shared
    /// instance is returned.
    /// The same seed will always yield the same generator.
    /// </summary>
    /// <param name="seed">The seed to the random number generator to use.</param>
    /// <returns>The appropriate random number generator.</returns>
    public static ThreadSafeRandom GetGenerator(int? seed = null)
    {
        ThreadSafeRandom rng = Shared;

        if (seed.HasValue)
        {
            int value = seed.Value;

            if (!Generators.TryGetValue(value, out rng))
                Generators[value] = rng = new ThreadSafeRandom(value);
        }

        return rng; 
    }

    private readonly Random _random;
    private readonly bool _isAlreadyThreadSafe;

    private ThreadSafeRandom(int? seed = null)
    {
        if (seed.HasValue)
        {
            _random = new Random(seed.Value);
            _isAlreadyThreadSafe = false;
        }
        else
        {
            _random = Random.Shared;
            _isAlreadyThreadSafe = true;
        }
    }

    /// <summary>
    /// This is a helper method to give us a random double within a range.
    /// </summary>
    /// <param name="min">The minimum of the range.</param>
    /// <param name="max">The maximum of the range.</param>
    /// <returns>A random number in the given range.</returns>
    public int Next(int min, int max)
    {
        if (_isAlreadyThreadSafe)
            // ReSharper disable once InconsistentlySynchronizedField
            return _random.Next(min, max);

        lock (_random)
        {
            return _random.Next(min, max);
        }
    }

    /// <summary>
    /// This is a helper method to give us a random double within a range.
    /// </summary>
    /// <param name="min">The minimum of the range.</param>
    /// <param name="max">The maximum of the range.</param>
    /// <returns>A random number in the given range.</returns>
    public double NextDouble(double min, double max)
    {
        return min + NextDouble() * (max - min);
    }

    /// <summary>
    /// This method returns the next double from the random number generator.
    /// </summary>
    /// <returns>The next double value.</returns>
    public double NextDouble()
    {
        if (_isAlreadyThreadSafe)
            // ReSharper disable once InconsistentlySynchronizedField
            return _random.NextDouble();

        lock (_random)
        {
            return _random.NextDouble();
        }
    }

    /// <summary>
    /// This method performs an in-place shuffle of an array.
    /// </summary>
    /// <param name="values">The array to shuffle.</param>
    /// <typeparam name="T">The type of array.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="values" /> is <see langword="null" />.</exception>
    public void Shuffle<T>(T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Shuffle(values.AsSpan());
    }

    /// <summary>
    /// This method performs an in-place shuffle of a span.
    /// </summary>
    /// <param name="values">The span to shuffle.</param>
    /// <typeparam name="T">The type of span.</typeparam>
    private void Shuffle<T>(Span<T> values)
    {
        int n = values.Length;

        for (int i = 0; i < n - 1; i++)
        {
            int j = Next(i, n);

            if (j != i)
                (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
