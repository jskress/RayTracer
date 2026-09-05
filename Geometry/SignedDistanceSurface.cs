using RayTracer.Basics;
using RayTracer.Fields;
using RayTracer.Geometry;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a shape written as a <b>signed distance function</b>: arithmetic in
/// <c>x</c>, <c>y</c> and <c>z</c> that answers, for any point, how far the nearest surface is --
/// negative inside, nought on it, positive outside.
/// <para>
/// **What it buys is that distances compose.**  Two shapes joined is the smaller of their two
/// distances, the part in common is the larger, and one cut out of another is the larger of the first
/// and the negated second.  Because those are distances rather than choices, any of them can be
/// *softened*: taking a smooth minimum instead of a minimum blends the two into each other over a
/// width you name, which is a fillet had for a line of arithmetic.  Rounding, hollowing and repeating
/// a shape forever are each about as short.
/// </para>
/// <para>
/// **The one thing it asks of you is that the function really is a distance.**  Sphere tracing steps
/// by whatever the function reports, so a function that grows faster than distance does -- and
/// <c>x² + y² + z² - 1</c> is one, being a value rather than a distance -- reports more room than
/// there is and the march steps clean through the surface.  What it draws then is a shape with pieces
/// missing.  Where a function is not a distance, use an <see cref="Isosurface"/>: it takes anything
/// at all and cannot miss a crossing, at the price of being slower.
/// </para>
/// <para>
/// A `bounded by` is required, for the reason an isosurface requires one: nothing about a piece of
/// arithmetic says where to stop looking for its surface.
/// </para>
/// </summary>
public class SignedDistanceSurface : DistanceEstimatedSurface
{
    /// <summary>
    /// This property holds the arithmetic that gives the distance to the surface.
    /// </summary>
    public FieldExpression Function { get; set; }

    private FieldFunction _function;
    private BoundingBox _domain;

    /// <summary>
    /// This method compiles the function once, so that the march does not walk a tree at every step.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _function = FieldFunction.Compile(Function);
        _domain = BoundingBox;
    }

    /// <summary>
    /// This property holds the region the march is confined to, which is the box the scene gave.
    /// </summary>
    protected override BoundingBox MarchingDomain => _domain;

    /// <summary>
    /// This method asks the function how far the nearest surface is.
    /// </summary>
    protected override double DistanceAt(double x, double y, double z)
    {
        return _function.Evaluate(x, y, z);
    }
}
