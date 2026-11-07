using RayTracer.Basics;
using RayTracer.Fields;

namespace RayTracer.Patterns;

/// <summary>
/// This class bends the space a pattern is read in: each of the three coordinates a pattern is asked about may be
/// given as a function of the point, and a coordinate left unsaid stays as it was.
/// <para>
/// A pattern can be moved, turned, scaled and sheared, all of which keep a straight line straight, and that is not
/// always what is wanted.  A hull's planks run with her sheer, which is a curve, and close in toward her stem and
/// her stern as her sides grow shallower: laid in a pattern's own straight rows they would cut across both.  Bent
/// by <c>Y { (y - rabbet(x)) / (rail(x) - rabbet(x)) }</c>, the rows run between her keel and her rail wherever
/// she is.
/// </para>
/// <para>
/// The bending is done first, before turbulence stirs the point and before the pattern sees it, in the pattern's
/// own space: the space its own transforms leave it in, which for a pattern that is not scaled is the space of the
/// surface it is laid on.  The patch a ray covers is carried through the bending too, by the change in the bent
/// point across it, so a pattern filtering itself over that patch filters over the right one.
/// </para>
/// </summary>
public sealed class Warp
{
    /// <summary>
    /// This property holds the function the bent X is, or <c>null</c> to leave X as it is.
    /// </summary>
    public FieldExpression X { get; }

    /// <summary>
    /// This property holds the function the bent Y is, or <c>null</c> to leave Y as it is.
    /// </summary>
    public FieldExpression Y { get; }

    /// <summary>
    /// This property holds the function the bent Z is, or <c>null</c> to leave Z as it is.
    /// </summary>
    public FieldExpression Z { get; }

    private readonly FieldFunction _x;
    private readonly FieldFunction _y;
    private readonly FieldFunction _z;

    /// <summary>
    /// This constructor makes a warp from the functions of the coordinates it changes.
    /// </summary>
    /// <param name="x">The function the bent X is, or <c>null</c> to leave X alone.</param>
    /// <param name="y">The function the bent Y is, or <c>null</c> to leave Y alone.</param>
    /// <param name="z">The function the bent Z is, or <c>null</c> to leave Z alone.</param>
    public Warp(FieldExpression x, FieldExpression y, FieldExpression z)
    {
        X = x;
        Y = y;
        Z = z;

        _x = x is null ? null : FieldFunction.Compile(x);
        _y = y is null ? null : FieldFunction.Compile(y);
        _z = z is null ? null : FieldFunction.Compile(z);
    }

    /// <summary>
    /// This method returns where the given point is read once the space is bent.
    /// </summary>
    /// <param name="point">The point, in the pattern's own space.</param>
    /// <returns>The point the pattern is asked about.</returns>
    public Point Bend(Point point)
    {
        double x = point.X;
        double y = point.Y;
        double z = point.Z;

        return new Point(
            _x?.Evaluate(x, y, z) ?? x,
            _y?.Evaluate(x, y, z) ?? y,
            _z?.Evaluate(x, y, z) ?? z);
    }

    /// <summary>
    /// This method carries the patch a ray covers about a point through the bending: each of its edges becomes the
    /// difference between where its two ends are read.  That is exact for a bending that is straight across the
    /// patch, and for one that is not it is the patch's own chord, which is what a filter over the patch wants.
    /// </summary>
    /// <param name="point">The point, in the pattern's own space, before it is bent.</param>
    /// <param name="footprint">The patch about it, in the same space.</param>
    /// <returns>The patch about the bent point.</returns>
    public Footprint Bend(Point point, Footprint footprint)
    {
        if (footprint is null || footprint.IsEmpty)
            return footprint;

        Vector across = footprint.Across * 0.5;
        Vector along = footprint.Along * 0.5;

        return new Footprint(Bend(point + across) - Bend(point - across), Bend(point + along) - Bend(point - along));
    }

    /// <summary>
    /// This method returns whether this warp bends space the same way as the one given.
    /// </summary>
    /// <param name="other">The warp to compare to.</param>
    /// <returns><c>true</c>, if the two are the same, or <c>false</c>, if not.</returns>
    public bool Matches(Warp other)
    {
        return other is not null &&
               Same(X, other.X) && Same(Y, other.Y) && Same(Z, other.Z);

        static bool Same(FieldExpression one, FieldExpression two) =>
            one is null ? two is null : two is not null && one.ToString() == two.ToString();
    }
}
