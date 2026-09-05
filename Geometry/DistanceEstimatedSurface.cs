using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for surfaces found by <b>sphere tracing</b>: the shape answers, for any
/// point, how far away the nearest surface is, and the ray is walked in steps of exactly that much.
/// <para>
/// **The step is safe because the answer is a lower bound.**  If the nearest surface is a quarter of
/// a unit away then a quarter of a unit can be crossed without meeting anything, whatever direction
/// is taken.  So the march moves in long strides through open space and shortens as it closes on the
/// surface, which is why this is quick where a marcher that knows nothing about the function has to
/// creep.  It is the same reason it cannot be used for just any function: one that grows faster than
/// distance does will report more room than there is, and the march will step straight through the
/// surface and out the far side.  See <see cref="Isosurface"/> for the marcher that takes anything.
/// </para>
/// <para>
/// **Every crossing is found, not merely the first.**  Sphere tracing naturally stops at the first
/// thing it meets, which is all a picture needs -- but a CSG walks the sorted crossings from far
/// behind the origin forward to work out what is solid, and refraction needs them to tell leaving
/// from entering.  So the march carries on past each crossing to the end of the span.
/// </para>
/// </summary>
public abstract class DistanceEstimatedSurface : Surface
{
    /// <summary>A whisker of slack on the span, so a ray grazing the box is not lost to arithmetic.</summary>
    private const double SpanPadding = 1e-6;

    /// <summary>
    /// How many steps one span is given before it is abandoned.  A march that has taken this many has
    /// met something it cannot get past -- a field that flattens out, or one that is not a distance at
    /// all -- and carrying on would cost the render rather than the pixel.
    /// </summary>
    private const int MaximumSteps = 900;

    /// <summary>
    /// This property holds how close the march must come before it calls the surface met, in the
    /// surface's own units.
    /// </summary>
    public double Accuracy { get; set; } = 0.0001;

    /// <summary>
    /// This property reports how far off itself this surface needs a ray to start, and it is worked
    /// out from the accuracy rather than chosen.
    /// <para>
    /// A crossing found by marching is only as exact as the march was asked to be, so the point sits
    /// within an accuracy of the true surface and may be fractionally inside it.  The usual nudge is
    /// a millionth, which for an accuracy of a ten-thousandth leaves every ray starting *within* the
    /// solid: the shadow ray meets the surface it just left, and the shape comes out black but for a
    /// scattering of pixels that happened to escape.  That is what this shape looked like before this
    /// property was written, and no amount of fiddling with how the normal is taken would have
    /// touched it.  A <see cref="Blob"/> says 100 here for the same reason, its crossing being solved
    /// for rather than written down.
    /// </para>
    /// </summary>
    public override double SelfOffsetScale =>
        Math.Max(1, Accuracy / DoubleExtensions.Epsilon * 4);

    /// <summary>
    /// This method must be provided by subclasses to say how far the nearest surface is from a point.
    /// It is negative inside the shape, nought on it, and positive outside.
    /// </summary>
    /// <param name="x">The X of the point to measure from.</param>
    /// <param name="y">The Y of the point to measure from.</param>
    /// <param name="z">The Z of the point to measure from.</param>
    /// <returns>How far the nearest surface is, signed.</returns>
    protected abstract double DistanceAt(double x, double y, double z);

    /// <summary>
    /// This method returns the region the march is confined to.
    /// </summary>
    /// <returns>The box to march within, or <c>null</c> if there is none.</returns>
    protected abstract BoundingBox MarchingDomain { get; }

    /// <summary>
    /// This method finds where a ray crosses the surface.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        // The ray is followed with a direction of unit length, so that a step of the march is a
        // distance in the space the shape is written in and the accuracy means what it says.  What is
        // reported is scaled back to the ray's own parameter at the end.  Judging the direction
        // against a fixed smallest length instead loses the surface once a scene scales it up.
        double length = ray.Direction.Magnitude;

        if (length == 0)
            return;

        BoundingBox domain = MarchingDomain;

        if (domain is null)
            return;

        Ray localRay = new (ray.Origin, ray.Direction.Unit, ray.TimeIndex);
        (double from, double to) = domain.GetIntersections(localRay);

        if (from > to)
            return;

        from -= SpanPadding;
        to += SpanPadding;

        // The stretch behind the origin is walked only when the origin is genuinely inside the shape,
        // which is where the distance comes out below nought.  A ray starting within -- as one cast
        // for a CSG or for refraction from inside does -- needs the crossing behind it.  A shadow or
        // reflection ray leaves from just outside, so it keeps a forward-only march and cannot walk
        // back to manufacture a hit on the surface it came from.
        if (from < 0 && DistanceAt(localRay.Origin.X, localRay.Origin.Y, localRay.Origin.Z) >= 0)
            from = 0;

        if (to < from)
            return;

        March(localRay, from, to, length, intersections);
    }

    /// <summary>
    /// This method walks one span of a ray in steps of whatever room the shape reports, recording
    /// every crossing it meets rather than stopping at the first.
    /// </summary>
    private void March(
        Ray ray, double from, double to, double length, List<Intersection> intersections)
    {
        (double ox, double oy, double oz) = (ray.Origin.X, ray.Origin.Y, ray.Origin.Z);
        (double dx, double dy, double dz) = (ray.Direction.X, ray.Direction.Y, ray.Direction.Z);
        double distance = from;
        bool wasInside = DistanceAt(ox + from * dx, oy + from * dy, oz + from * dz) < 0;

        // **The estimate is only to be trusted outside**, and the march has to be told so.  Outside,
        // the number is how much room there is and stepping by it is both safe and quick.  Inside, an
        // estimate of this kind falls away towards nothing as it goes deeper -- there is no boundary
        // nearby to measure against -- so a march driven by it crawls: a Julia set crossed this way
        // spent its whole allowance of steps covering a ninetieth of the span and stopped while still
        // within the solid, reporting that a ray had gone in and never come out.  So the inside is
        // crossed in strides that cannot take more than a fraction of the budget.  A true distance
        // function never notices, its own answer being the larger of the two nearly always; it is the
        // estimate that has collapsed which this rescues.
        double insideStride = (to - from) / (MaximumSteps * 0.25);

        // **And a floor on the outside step too, for the opposite reason.**  The estimate is a lower
        // bound, so approaching a surface it shrinks towards nothing and the march creeps in ever
        // smaller steps that never quite arrive: the sign never turns over, the ray runs out of steps
        // short of the shape, and what is drawn is peppered with holes where those rays reported
        // nothing.  A floor makes the march cross rather than approach forever, and costs no accuracy
        // at all, since where the crossing lies is found by halving afterwards rather than by
        // arriving there.
        // Only a floor against creeping forever, not a stride: the surface is met by coming within
        // the accuracy of it, which is what sphere tracing does, so the step is free to shrink as the
        // march closes in.
        double outsideFloor = (to - from) / MaximumSteps;
        bool settling = false;

        double previous = from;

        for (int step = 0; step < MaximumSteps && distance <= to; step++)
        {
            double room = DistanceAt(
                ox + distance * dx, oy + distance * dy, oz + distance * dz);
            bool isInside = room < 0;

            // A crossing is where the sign turns over, which is the one test that catches a surface
            // however the march arrived at it.  **Where it lies is then found by halving the step it
            // was caught in, rather than being taken as the step's end.**  That is what lets the
            // march stride: how far it moves and how exactly it answers become two separate things,
            // and without it the answer is only ever as good as the last stride was short -- an
            // inside stride of a fortieth reported crossings a fortieth out, which is forty times the
            // accuracy asked for.
            if (settling)
            {
                // Just past a surface that was met by arriving at it rather than by crossing it.
                // Nothing is recorded until a sample confirms the far side, or the same surface is
                // reported over and over as the march inches through it -- which is what a first
                // attempt at this did, turning every crossing into four.
                if (isInside == wasInside)
                    settling = false;
            }
            else if (isInside != wasInside)
            {
                intersections.Add(new Intersection(
                    this, Narrow(ox, oy, oz, dx, dy, dz, previous, distance, wasInside) / length));
                wasInside = isInside;
            }
            else if (Math.Abs(room) < Accuracy)
            {
                // **Arriving at the surface without crossing it**, which is how sphere tracing
                // usually arrives: the estimate is a lower bound, so it shrinks towards nothing as
                // the march closes in and the sign may never turn over at all.  Within the accuracy
                // is met, and the march then settles onto the far side before it looks again.
                intersections.Add(new Intersection(this, distance / length));

                wasInside = !wasInside;
                settling = true;
            }


            previous = distance;
            distance += isInside
                ? Math.Max(Math.Abs(room), insideStride)
                : Math.Max(room, outsideFloor);
        }

        // **Still inside when the span runs out means the shape reaches its own box**, and the box
        // face is then a real boundary: what is drawn is the shape and the box together, exactly as
        // an intersection of the two would be.  Left unrecorded, such a ray goes in and never comes
        // out -- an odd number of crossings, which a CSG reads as the whole of the rest of the ray
        // being solid.  One ray in forty through a Julia set does this, the set reaching the radius
        // of two it is bounded by.
        if (wasInside)
            intersections.Add(new Intersection(this, to / length));
    }

    /// <summary>
    /// This method finds where within one step of the march the surface actually lies, by halving the
    /// step until the two ends are closer together than the accuracy asked for.
    /// </summary>
    /// <returns>How far along the ray the crossing lies.</returns>
    private double Narrow(
        double ox, double oy, double oz, double dx, double dy, double dz,
        double before, double after, bool insideBefore)
    {
        for (int half = 0; half < 60 && after - before > Accuracy; half++)
        {
            double middle = (before + after) * 0.5;
            bool insideMiddle = DistanceAt(
                ox + middle * dx, oy + middle * dy, oz + middle * dz) < 0;

            if (insideMiddle == insideBefore)
                before = middle;
            else
                after = middle;
        }

        return (before + after) * 0.5;
    }

    /// <summary>
    /// This method returns the normal, taken from the distance field by differences.
    /// <para>
    /// **There is no step size to choose here**, and that is deliberate: the offsets are worked out
    /// from the accuracy the surface is already being found to, so a shape scaled up or down carries
    /// its normals with it.  A fixed offset would be too coarse for a small shape and too fine for a
    /// large one, which is the fault this project has met often enough to have a name for.
    /// </para>
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        double step = Accuracy * 8;
        (double x, double y, double z) = (point.X, point.Y, point.Z);
        Vector normal = new (
            DistanceAt(x + step, y, z) - DistanceAt(x - step, y, z),
            DistanceAt(x, y + step, z) - DistanceAt(x, y - step, z),
            DistanceAt(x, y, z + step) - DistanceAt(x, y, z - step));

        // Where the field is flat there is no direction to give, which happens at the very middle of
        // a shape and at a crossing the march only grazed.  Facing back the way the ray came is at
        // least a direction, as the isosurface does for the same case.
        return normal.Magnitude.Near(0) ? Directions.Up : normal.Unit;
    }
}
