using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class holds the scattered points that the ripple and wave patterns measure their distances
/// from, and how fast each of them ripples.
/// <para>
/// Both patterns work the same way: a handful of sources are strewn about, and the value at a point
/// is the average of a sine of the distance to each of them.  One source alone would give perfect
/// concentric rings, which reads as a target rather than as water; ten of them interfere with one
/// another and the rings break up into something that looks like a disturbed surface.
/// </para>
/// <para>
/// The sources are worked out once and shared, and are the same every time, which matters: they
/// are the pattern.  Were they drawn afresh per render the same scene would ripple differently
/// each time it was drawn.
/// </para>
/// </summary>
public static class WaveSources
{
    /// <summary>
    /// This is how many sources there are.  POV-Ray uses ten unless a scene says otherwise, and
    /// the number shows: fewer leaves the rings visible, and many more averages the pattern flat.
    /// </summary>
    public const int Count = 10;

    /// <summary>
    /// This property provides the directions the sources lie in.
    /// </summary>
    public static IReadOnlyList<Vector> Directions => LazySources.Value.Directions;

    /// <summary>
    /// This property provides how fast each source ripples, which is what sets the waves pattern
    /// apart from the ripples one: ripples asks every source for the same wavelength, and waves
    /// gives each its own, so the crests never quite line up.
    /// </summary>
    public static IReadOnlyList<double> Frequencies => LazySources.Value.Frequencies;

    private static readonly Lazy<(Vector[] Directions, double[] Frequencies)> LazySources =
        new (Build);

    /// <summary>
    /// This method works out the sources, following POV-Ray so that a converted texture ripples
    /// the way it was drawn to.
    /// <para>
    /// The frequencies come from POV-Ray's own arithmetic exactly, an old linear generator run
    /// from a fixed start.  The directions are the noise field sampled along a line and normalized,
    /// which is POV-Ray's recipe too, though ours will not land on quite the same ten vectors since
    /// the noise underneath is not the same noise.  That costs nothing worth having: what matters
    /// is that they are ten fixed directions scattered about, not which ten.
    /// </para>
    /// </summary>
    /// <returns>The directions and frequencies of the sources.</returns>
    private static (Vector[], double[]) Build()
    {
        Vector[] directions = new Vector[Count];
        double[] frequencies = new double[Count];
        NoiseGenerator generator = NoiseGenerator.ForSeed(null);
        int next = -560851967;

        for (int index = 0; index < Count; index++)
        {
            directions[index] = generator.DNoise(new Point(index, 0, 0)).Unit;

            unchecked
            {
                next = next * 1812433253 + 12345;
            }

            frequencies[index] = ((next >> 16) & 0x7FFF) * 0.000030518509476 + 0.01;
        }

        return (directions, frequencies);
    }

    /// <summary>
    /// This method works out one of these patterns: the average, over every source, of a sine of
    /// how far the point is from it.
    /// </summary>
    /// <param name="point">The point being asked about.</param>
    /// <param name="frequency">How tight the ripples are.</param>
    /// <param name="phase">Where in its cycle each ripple starts.</param>
    /// <param name="perSourceFrequency">Whether each source ripples at its own rate, which is what
    /// makes waves out of ripples.</param>
    /// <returns>The summed sine, before it is brought into range.</returns>
    public static double Sum(
        Point point, double frequency, double phase, bool perSourceFrequency)
    {
        double total = 0;

        for (int index = 0; index < Count; index++)
        {
            Vector source = Directions[index];
            double length = new Vector(
                point.X - source.X, point.Y - source.Y, point.Z - source.Z).Magnitude;

            // A point sitting exactly on a source has no distance to take a sine of, so it is
            // treated as one unit away, which is what POV-Ray does with the same difficulty.
            if (length == 0)
                length = 1;

            double rate = perSourceFrequency ? Frequencies[index] : 1;

            total += Cycloidal(length * frequency * rate + phase) / rate;
        }

        return total / Count;
    }

    /// <summary>
    /// This method is POV-Ray's <c>cycloidal</c>: a sine taken over one turn.  Working from the
    /// fractional part rather than the whole number changes nothing about the answer -- a sine
    /// repeats every turn anyway -- and is kept because it is how POV-Ray words it.
    /// </summary>
    /// <param name="value">The value to take the sine of, in turns.</param>
    /// <returns>The sine of that many turns.</returns>
    private static double Cycloidal(double value) =>
        Math.Sin((value - Math.Floor(value)) * 2 * Math.PI);
}
