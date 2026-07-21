namespace RayTracer.Basics;

/// <summary>
/// This class stirs the points a pattern is asked about, by pushing each one along a vector of
/// summed noise.  It is <see cref="LayeredNoise"/> plus the one thing that only makes sense once
/// you mean to move something: how far.
/// </summary>
public class Turbulence : LayeredNoise
{
    /// <summary>
    /// This property holds how far a point may be pushed, per axis.  It is what POV-Ray means by
    /// the number after its <c>turbulence</c> keyword, and giving each axis its own is what lets a
    /// pattern be stirred sideways but left alone along its length.
    /// </summary>
    public Vector Amplitude { get; set; } = new (1, 1, 1);

    /// <summary>
    /// This method stirs the given point, returning where a pattern should be sampled instead of
    /// where it was actually asked about.
    /// <para>
    /// It is worth being clear that this does not touch the pattern at all: the pattern goes on
    /// computing exactly what it always did, and is simply asked about a point that has been pushed
    /// around first.  A ruler-straight boundary becomes a ragged one because the points along it
    /// are no longer where they were.  POV-Ray does the same thing in its Warp_EPoint, and for the
    /// same reason -- it means every pattern gets turbulence without a single one of them having to
    /// know about it.
    /// </para>
    /// <para>
    /// Marble and wood are the exceptions, there as here: they fold turbulence into their own
    /// arithmetic instead, since what they want stirred is the coordinate they are built from
    /// rather than the point in space.
    /// </para>
    /// </summary>
    /// <param name="point">The point a pattern was asked about.</param>
    /// <returns>The point it should be sampled at instead.</returns>
    public Point Warp(Point point)
    {
        Vector displacement = GenerateVector(point);

        return new Point(
            point.X + displacement.X * Amplitude.X,
            point.Y + displacement.Y * Amplitude.Y,
            point.Z + displacement.Z * Amplitude.Z);
    }
}
