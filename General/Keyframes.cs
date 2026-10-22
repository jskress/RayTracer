using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.General;

/// <summary>
/// This class holds a value that changes over time, given at moments called keys: a number, a point or a color
/// at each, and how it goes from one to the next.  It is asked by calling it, as a function, with the time, and
/// answers with the value then: before the first key it holds the first, after the last the last -- or, if it
/// loops, it begins again.
/// </summary>
public sealed class Keyframes
{
    /// <summary>
    /// This enumeration names how a value goes from one key to the next.
    /// </summary>
    public enum Way
    {
        /// <summary>At an even pace.</summary>
        Linear,
        /// <summary>Slowly at first.</summary>
        EaseIn,
        /// <summary>Slowing as it arrives.</summary>
        EaseOut,
        /// <summary>Slowly at both ends.</summary>
        EaseInOut,
        /// <summary>Not at all until the key, and then all at once.</summary>
        Hold
    }

    /// <summary>
    /// This property holds the keys, in order of time: each a time, the value then, and the way the value goes
    /// from the key before to this one.
    /// </summary>
    public List<(double Time, object Value, Way Way)> Keys { get; init; } = [];

    /// <summary>
    /// This property notes whether the value passes smoothly through every key, its pace carried on from one to
    /// the next, rather than going straight from each to the next.
    /// </summary>
    public bool Smooth { get; init; }

    /// <summary>
    /// This property notes whether the keys begin again once the last is reached.
    /// </summary>
    public bool Loops { get; init; }

    /// <summary>
    /// This method returns the value at a time.
    /// </summary>
    public object At(double time)
    {
        double first = Keys[0].Time;
        double last = Keys[^1].Time;

        if (Loops && last > first)
            time = first + Mod(time - first, last - first);

        if (time <= first)
            return Keys[0].Value;

        if (time >= last)
            return Keys[^1].Value;

        int after = 1;

        while (Keys[after].Time <= time)
            after++;

        (double fromTime, object from, _) = Keys[after - 1];
        (double toTime, object to, Way way) = Keys[after];
        double share = (time - fromTime) / (toTime - fromTime);

        if (Smooth)
            return Hermite(after - 1, share);

        double eased = way switch
        {
            Way.EaseIn => share * share,
            Way.EaseOut => 1 - (1 - share) * (1 - share),
            Way.EaseInOut => share * share * (3 - 2 * share),
            Way.Hold => 0,
            _ => share
        };

        return Mixed(from, to, eased);
    }

    // The cubic through two keys whose pace at each is the slope from the key before it to the key after, as
    // Catmull and Rom's is, measured over time so that keys unevenly spaced still pass at an even pace.
    private object Hermite(int index, double share)
    {
        (double t0, object p0, _) = Keys[index];
        (double t1, object p1, _) = Keys[index + 1];
        double span = t1 - t0;
        double[] a = Parts(p0), b = Parts(p1);
        double[] m0 = Slope(index), m1 = Slope(index + 1);
        double s2 = share * share, s3 = s2 * share;
        double h00 = 2 * s3 - 3 * s2 + 1, h10 = s3 - 2 * s2 + share, h01 = -2 * s3 + 3 * s2, h11 = s3 - s2;
        double[] parts = new double[a.Length];

        for (int part = 0; part < a.Length; part++)
            parts[part] = h00 * a[part] + h10 * span * m0[part] + h01 * b[part] + h11 * span * m1[part];

        return Made(p0, parts);
    }

    // How fast the value is changing at a key, its own parts per second: the slope across its neighbors, or to the
    // one it has at an end.
    private double[] Slope(int index)
    {
        int before = Math.Max(0, index - 1);
        int after = Math.Min(Keys.Count - 1, index + 1);
        double[] from = Parts(Keys[before].Value), to = Parts(Keys[after].Value);
        double span = Keys[after].Time - Keys[before].Time;

        return from.Select((value, part) => span > 0 ? (to[part] - value) / span : 0).ToArray();
    }

    private static object Mixed(object from, object to, double share)
    {
        double[] a = Parts(from), b = Parts(to);

        return Made(from, a.Select((value, part) => value + (b[part] - value) * share).ToArray());
    }

    // A value as the numbers it is made of, and made again from them in its own kind.
    private static double[] Parts(object value) => value switch
    {
        double number => [number],
        Color color => [color.Red, color.Green, color.Blue, color.Alpha],
        NumberTuple tuple => [tuple.X, tuple.Y, tuple.Z, tuple.W],
        _ => throw new InvalidOperationException($"A key's value cannot be a {value?.GetType().Name}.")
    };

    private static object Made(object like, double[] parts) => like switch
    {
        double => parts[0],
        Color => new Color(parts[0], parts[1], parts[2], parts[3]),
        Point => new Point(parts[0], parts[1], parts[2]),
        Vector => new Vector(parts[0], parts[1], parts[2]),
        _ => new NumberTuple(parts[0], parts[1], parts[2], parts[3])
    };

    private static double Mod(double value, double by) => value - by * Math.Floor(value / by);
}
