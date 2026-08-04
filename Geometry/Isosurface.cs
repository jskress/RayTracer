using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Fields;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents an isosurface: the shape a function of three variables makes where it takes
/// one particular value.  Everywhere the function comes out below that value is inside the solid, and
/// everywhere above it is outside, so <c>x² + y² + z² - 1</c> at nought is a sphere of radius one and
/// changing what is written changes the shape rather than which surface type is being used.
/// <para>
/// There is no equation to solve here, as there is for a sphere or a torus, since the function is
/// whatever a scene wrote.  What there is instead is a way of asking, of a whole region at once,
/// whether the surface could possibly be in it: bound the function over that region and see whether the
/// bound even reaches the value being looked for (see <see cref="FieldRange"/>).  A ray is followed by
/// splitting the span it covers in half over and over, throwing away every half the bound rules out,
/// until what is left is small enough to pin a crossing down in.  That is what POV-Ray's
/// <c>max_gradient</c> exists to substitute for, and why there is no such number here: the function
/// says how steep it is, so nobody has to guess.
/// </para>
/// <para>
/// A crossing is found by the function changing sign across it, which means a ray that touches the
/// surface without going through it -- exactly tangent to it -- is not reported.  That is worth knowing
/// and is the harmless way round: such a ray grazes the silhouette, where the pixel is settled by its
/// neighbours anyway, and a touch contributes an even number of crossings, so missing both of them
/// leaves a CSG's count of what is inside and what is outside exactly as correct as before.
/// </para>
/// </summary>
public class Isosurface : Surface
{
    /// <summary>
    /// How far past its box a surface is looked for, so that a crossing exactly on the boundary is
    /// still found.
    /// </summary>
    private const double BoxPadding = 1e-6;

    /// <summary>
    /// How many times a span may be halved before a crossing within it is given up on.  Thirty halvings
    /// take any box a scene is likely to hold down below a millionth of its width, so reaching this
    /// means the function is doing something a marcher cannot follow rather than that the limit is mean.
    /// </summary>
    private const int MaximumDepth = 30;

    /// <summary>
    /// This property holds the function whose value makes the surface.
    /// </summary>
    public FieldExpression Function { get; set; }

    /// <summary>
    /// This property holds the value of the function that makes the surface.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// This property holds how close a crossing must be pinned down before it is reported.
    /// </summary>
    public double Accuracy { get; set; } = 0.0001;

    private FieldFunction _function;
    private FieldGradient _gradient;
    private BoundingBox _domain;

    /// <summary>
    /// This method is called once prior to rendering, and is where the function is turned into
    /// something quick to ask: compiled, along with its three slopes, so that neither the marching nor
    /// the shading walks a tree.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _function = FieldFunction.Compile(Function);
        _gradient = FieldGradient.Of(Function);
        _domain = BoundingBox ?? GetDefaultBoundingBox();
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.  An isosurface has no
    /// extent of its own to work one out from -- a function may make a shape of any size, or none --
    /// so this is the box a scene did not give, and a modest one is better than an endless one.
    /// </summary>
    /// <returns>A default bounding box for the surface.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        BoundingBox box = new ();

        box.Add(new Point(-1, -1, -1));
        box.Add(new Point(1, 1, 1));

        return box;
    }

    /// <summary>
    /// This method is used to determine whether the given ray crosses the surface and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        // The ray is followed with a direction of unit length, so that a span of it is a distance in
        // the space the function is written in and the accuracy asked for means what it says.  What is
        // reported is scaled back to the ray's own parameter at the end.
        double length = ray.Direction.Magnitude;

        if (length.Near(0))
            return;

        Ray localRay = new (ray.Origin, ray.Direction.Unit, ray.TimeIndex);
        (double tMin, double tMax) = _domain.GetIntersections(localRay);

        if (tMin > tMax)
            return;

        tMin -= BoxPadding;
        tMax += BoxPadding;

        // The part of the box behind the origin is searched only when the origin is genuinely inside
        // the solid, which is where the function comes out below the value that makes the surface.  A
        // ray that starts inside -- as one cast for a CSG or for refraction from within does -- needs
        // the crossing behind it, so that walking the sorted crossings can tell inside from outside.  A
        // shadow or reflection ray cast from the surface itself starts just outside, so it keeps a
        // forward-only search and cannot walk back to manufacture a hit on the surface it left.
        if (tMin < 0 && !IsInside(localRay.Origin))
            tMin = 0;

        if (tMax < tMin)
            return;

        // The ray's own numbers are carried about as six doubles rather than as points, because a Point
        // here is an object on the heap and this is the innermost loop in the whole render.
        Point origin = localRay.Origin;
        Vector direction = localRay.Direction;

        March(origin, direction, tMin, ValueAt(origin, direction, tMin),
            tMax, ValueAt(origin, direction, tMax), MaximumDepth, length, intersections);
    }

    /// <summary>
    /// This method follows one span of a ray, looking for the places the function crosses the value that
    /// makes the surface.
    /// <para>
    /// Three questions are asked of a span, in the order that settles it soonest.  Does the function
    /// even reach the surface anywhere in the box the span lies in?  If not, the span is done with, and
    /// that is what makes this quick.  Do the two ends fall on opposite sides of the surface?  Then
    /// there is a crossing between them and it can be pinned down directly, without narrowing the span
    /// any further.  Otherwise the span is halved and each half asked the same, until halving it further
    /// would be finer than the accuracy asked for.
    /// </para>
    /// <para>
    /// The value of the function at each end is passed in rather than worked out here, so that the end
    /// two halves share is only ever evaluated once.
    /// </para>
    /// </summary>
    private void March(
        Point origin, Vector direction, double start, double atStart, double end, double atEnd,
        int depth, double length, List<Intersection> intersections)
    {
        FieldRange bound = BoundOver(origin, direction, start, end);

        // The whole point: a span the function cannot reach the surface within is not looked at again.
        // A bound that says nothing rules nothing out, so that span is still followed.
        if (!bound.IsAnywhere && !bound.Contains(Threshold))
            return;

        if (atStart < 0 != atEnd < 0)
        {
            intersections.Add(new Intersection(this,
                Narrow(origin, direction, start, atStart, end) / length));

            return;
        }

        if (end - start <= Accuracy || depth == 0)
            return;

        double middle = (start + end) / 2;
        double atMiddle = ValueAt(origin, direction, middle);

        March(origin, direction, start, atStart, middle, atMiddle, depth - 1, length, intersections);
        March(origin, direction, middle, atMiddle, end, atEnd, depth - 1, length, intersections);
    }

    /// <summary>
    /// This method bounds the function over a span of a ray.  What is bounded is the box the span lies
    /// in rather than the span itself, which is wider than the truth and so safe: a box holding the
    /// span cannot make the function reach anything the span alone would not.
    /// </summary>
    private FieldRange BoundOver(
        Point origin, Vector direction, double start, double end)
    {
        return Function.Bound(
            FieldRange.Between(origin.X + start * direction.X, origin.X + end * direction.X),
            FieldRange.Between(origin.Y + start * direction.Y, origin.Y + end * direction.Y),
            FieldRange.Between(origin.Z + start * direction.Z, origin.Z + end * direction.Z));
    }

    /// <summary>
    /// This method narrows a span whose two ends fall on opposite sides of the surface down to where the
    /// crossing between them actually is, by halving.
    /// <para>
    /// Halving rather than anything cleverer, even though the slope is to hand and Newton's method would
    /// converge in a fraction of the steps: halving cannot be led astray.  It keeps a crossing bracketed
    /// throughout, so it cannot be thrown off by a slope of nearly nought where a ray grazes the surface,
    /// and this is only ever run once a crossing is known to be there -- a handful of evaluations per
    /// hit, against the thousands per ray that the bounding saves.
    /// </para>
    /// </summary>
    private double Narrow(
        Point origin, Vector direction, double start, double atStart, double end)
    {
        double tolerance = Accuracy * 0.001;

        for (int step = 0; step < 60 && end - start > tolerance; step++)
        {
            double middle = (start + end) / 2;
            double atMiddle = ValueAt(origin, direction, middle);

            if (atMiddle == 0)
                return middle;

            if (atStart < 0 == atMiddle < 0)
            {
                start = middle;
                atStart = atMiddle;
            }
            else
                end = middle;
        }

        return (start + end) / 2;
    }

    /// <summary>
    /// This method returns how far the function is from the surface at a point along the ray: negative
    /// inside, positive outside, and nought on it.
    /// </summary>
    private double ValueAt(Point origin, Vector direction, double distance)
    {
        return _function.Evaluate(
            origin.X + distance * direction.X,
            origin.Y + distance * direction.Y,
            origin.Z + distance * direction.Z) - Threshold;
    }

    /// <summary>
    /// This method reports whether the given point lies inside the solid.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the point is inside.</returns>
    private bool IsInside(Point point)
    {
        return _function.Evaluate(point.X, point.Y, point.Z) - Threshold < 0;
    }

    /// <summary>
    /// This method returns the normal to the surface at the given point, which is the direction the
    /// function climbs fastest there.  Since inside is where the function is lower, climbing fastest is
    /// pointing out.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        Vector gradient = _gradient.At(point);

        // A function whose slope is nought at a crossing -- where it grazes the surface rather than
        // passing through it -- leaves no direction to point in, and normalizing would give a vector of
        // nothing but NaN.  Facing the ray back the way it came is at least a direction.
        return gradient.Magnitude.Near(0) ? Directions.Up : gradient.Unit;
    }
}
