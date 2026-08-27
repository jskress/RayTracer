using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Fields;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a body of water: a stretch of surface carrying one or more trains of waves,
/// lying in the X/Z plane with its rest level at <c>y = 0</c>.
/// <para>
/// The waves are **trochoids** rather than sines, which is what makes them look like water rather
/// than like a rumpled sheet.  Each piece of water travels in a circle as a wave passes, so it bunches
/// up under a crest and spreads out under a trough -- narrow peaked crests, long flat troughs, and the
/// asymmetry between them coming out of the motion rather than out of a sharpening knob that has to be
/// tuned by eye.  <see cref="SwellField"/> holds that arithmetic and says more about it.
/// </para>
/// <para>
/// **Why this is a surface of its own rather than an isosurface with a clever function.**  The shape
/// cannot be written as a function of position -- it says where water *goes*, not what is at a place --
/// so it has to be turned round before it can be asked anything, which no field expression can do.
/// Having its own type buys two further things: the normal is worked out from the shape rather than
/// sampled from differences of the field, and the range the water can reach over a box is a closed
/// form rather than a walk over an expression tree.
/// </para>
/// </summary>
public class Swells : Surface
{
    /// <summary>
    /// How far past its box the surface is looked for, so that a crossing exactly on the boundary is
    /// still found.
    /// </summary>
    private const double BoxPadding = 1e-6;

    /// <summary>
    /// How many times a span may be halved before the search gives up on it.
    /// </summary>
    private const int MaximumDepth = 30;

    /// <summary>
    /// This property holds the wave trains this water carries.
    /// </summary>
    public List<SwellTrain> Trains { get; } = [];

    /// <summary>
    /// This property holds how far the water reaches along X.  It is centered on the origin.
    /// </summary>
    public double Across { get; set; } = 100;

    /// <summary>
    /// This property holds how far the water reaches along Z.  It is centered on the origin.
    /// </summary>
    public double Along { get; set; } = 100;

    /// <summary>
    /// This property holds how close a crossing is pinned down before it is reported.
    /// </summary>
    public double Accuracy { get; set; } = 0.0001;

    /// <summary>
    /// This property reports the field the water's shape is worked out by, once it has been made
    /// ready.
    /// </summary>
    public SwellField Field => _field;

    private SwellField _field;
    private BoundingBox _domain;

    protected override void PrepareSurfaceForRendering()
    {
        _field = new SwellField(Trains);
        _domain = BoundingBox ?? GetDefaultBoundingBox();
    }

    /// <summary>
    /// This method works out the box the water lies in: as far as it was told to reach along X and Z,
    /// and no further up or down than every train's amplitude added together.
    /// </summary>
    /// <returns>The box holding the water.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        double reach = new SwellField(Trains).Reach + BoxPadding;

        return new BoundingBox()
            .Add(new Point(-Across / 2, -reach, -Along / 2))
            .Add(new Point(Across / 2, reach, Along / 2));
    }

    /// <summary>
    /// This method finds where a ray crosses the water.
    /// <para>
    /// It works the way an isosurface does, because the problem is the same one: follow the ray in
    /// spans, throw away every span the water could not possibly reach into, and halve the rest until
    /// a span is found with water on one side of it and air on the other.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any crossings to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double length = ray.Direction.Magnitude;

        if (length.Near(0))
            return;

        Ray localRay = new (ray.Origin, ray.Direction.Unit, ray.TimeIndex);
        (double tMin, double tMax) = _domain.GetIntersections(localRay);

        if (tMin > tMax)
            return;

        tMin -= BoxPadding;
        tMax += BoxPadding;

        // Only a ray that genuinely starts under the water looks behind itself, so that one cast from
        // the surface -- toward a light, or as a reflection -- cannot walk back and find the very
        // surface it left.
        if (tMin < 0 && !IsInside(localRay.Origin))
            tMin = 0;

        if (tMax < tMin)
            return;

        Point origin = localRay.Origin;
        Vector direction = localRay.Direction;

        March(origin, direction, tMin, DepthAt(origin, direction, tMin),
            tMax, DepthAt(origin, direction, tMax), MaximumDepth, length, intersections);
    }

    /// <summary>
    /// This method follows a span of a ray, halving it until it finds where the water's surface lies
    /// within it, and throwing the span away whole where the water cannot reach it at all.
    /// </summary>
    private void March(
        Point origin, Vector direction, double start, double atStart, double end, double atEnd,
        int depth, double length, List<Intersection> intersections)
    {
        if (CannotReach(origin, direction, start, end))
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
        double atMiddle = DepthAt(origin, direction, middle);

        March(origin, direction, start, atStart, middle, atMiddle, depth - 1, length, intersections);
        March(origin, direction, middle, atMiddle, end, atEnd, depth - 1, length, intersections);
    }

    /// <summary>
    /// This method reports whether the water can be ruled out of a span of the ray entirely: it can,
    /// when the ray stays above every height the water could reach there, or below every one.
    /// </summary>
    private bool CannotReach(Point origin, Vector direction, double start, double end)
    {
        FieldRange acrossX = FieldRange.Between(
            origin.X + start * direction.X, origin.X + end * direction.X);
        FieldRange acrossZ = FieldRange.Between(
            origin.Z + start * direction.Z, origin.Z + end * direction.Z);
        FieldRange rayY = FieldRange.Between(
            origin.Y + start * direction.Y, origin.Y + end * direction.Y);
        FieldRange waterY = _field.HeightOver(acrossX, acrossZ);

        // Wholly above the water it could reach, or wholly below it.
        return rayY.Low > waterY.High || rayY.High < waterY.Low;
    }

    /// <summary>
    /// This method returns how far above the water a point on the ray stands -- negative where it is
    /// under it, which is what a crossing is a change of sign of.
    /// </summary>
    private double DepthAt(Point origin, Vector direction, double distance)
    {
        double x = origin.X + distance * direction.X;
        double y = origin.Y + distance * direction.Y;
        double z = origin.Z + distance * direction.Z;

        return y - _field.HeightAt(x, z);
    }

    /// <summary>
    /// This method closes in on a crossing already known to lie in a span, by halving.
    /// </summary>
    private double Narrow(
        Point origin, Vector direction, double start, double atStart, double end)
    {
        double tolerance = Accuracy * 0.001;

        for (int step = 0; step < 60 && end - start > tolerance; step++)
        {
            double middle = (start + end) / 2;
            double atMiddle = DepthAt(origin, direction, middle);

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
    /// This method reports whether a point lies under the water.
    /// </summary>
    private bool IsInside(Point point)
    {
        return point.Y < _field.HeightAt(point.X, point.Z);
    }

    /// <summary>
    /// This method returns which way the water faces at a point, worked out from the shape itself
    /// rather than sampled from the field around it.
    /// </summary>
    /// <param name="point">The point to find the normal at, in surface space.</param>
    /// <param name="intersection">The crossing this is for.</param>
    /// <returns>The normal there.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return _field.NormalAt(point.X, point.Z);
    }
}
