using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a "lathe".  It is defined by one or more closed paths in 2D
/// that are rotated around the Y axis.
/// </summary>
public class Lathe : Surface
{
    /// <summary>
    /// This property holds the path that represents the outline of the shape in the
    /// X/Y plane that will be rotated to create the surface.
    /// </summary>
    public GeneralPath Path { get; set; }

    private readonly List<LathePathSurface> _surfaces = [];

    // For each segment, the one that starts where it ends and the one that ends where it starts:
    // the neighbors it shares a corner with, or -1 where there is none.
    private int[] _following = [];
    private int[] _preceding = [];

    private Cylinder _bounds;
    private double _radius;

    // How close two crossings must lie to be one crossing at a corner, in the lathe's own units:
    // the two are found a hair apart at most, so this is a ten-thousandth of the lathe's size.
    private double _cornerTolerance;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        List<IPathSegment> segments = Path.NormalizeFor3D().Segments;

        _surfaces.Clear();
        _surfaces.AddRange(segments.Select(segment => new LathePathSurface(segment)));

        _following = Enumerable.Repeat(-1, segments.Count).ToArray();
        _preceding = Enumerable.Repeat(-1, segments.Count).ToArray();

        for (int index = 0; index < segments.Count; index++)
        {
            TwoDPoint end = segments[index].Points[^1];
            int next = segments.FindIndex(segment => segment.Points[0] == end);

            if (next < 0)
                continue;

            _following[index] = next;
            _preceding[next] = index;
        }

        _radius = Math.Max(Math.Abs(Path.MinX), Math.Abs(Path.MaxX));
        _cornerTolerance = 0.0001 * Math.Max(1, Math.Max(_radius, Path.MaxY - Path.MinY));

        _bounds = new Cylinder
        {
            MinimumY = Path.MinY - DoubleExtensions.Epsilon,
            MaximumY = Path.MaxY + DoubleExtensions.Epsilon,
            Transform = Basics.Transforms.Scale(
                _radius + DoubleExtensions.Epsilon, 1, _radius + DoubleExtensions.Epsilon)
        };
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.  Since
    /// revolving the profile around the Y axis sweeps its radius through every angle, the
    /// box must span the full radius in both X and Z, not just the profile's own (possibly
    /// one-sided) X extent.
    /// </summary>
    /// <returns>A default bounding box, if any, for the surface.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(-_radius, Path.MinY, -_radius))
            .Add(new Point(_radius, Path.MaxY, _radius));
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        if (Misses(ray))
            return;

        List<(int Segment, Intersection Hit, double Along)> crossings = FindCrossings(ray);

        if (crossings.Any(crossing => crossing.Along is < 0 or > 1))
            SettleCorners(ray, crossings);

        intersections.AddRange(crossings.Select(crossing => crossing.Hit));
    }

    /// <summary>
    /// This method finds where the ray crosses each segment's surface, before any crossing a hair
    /// beyond a segment's end has been settled.
    /// </summary>
    /// <param name="ray">The ray to test, in the lathe's own space.</param>
    /// <returns>The crossings, each with its segment and how far along that segment it falls.</returns>
    internal List<(int Segment, Intersection Hit, double Along)> FindCrossings(Ray ray)
    {
        List<(int Segment, Intersection Hit, double Along)> crossings = [];

        for (int index = 0; index < _surfaces.Count; index++)
        {
            foreach ((Intersection hit, double along) in _surfaces[index].GetCrossings(this, ray))
                crossings.Add((index, hit, along));
        }

        return crossings;
    }

    /// <summary>
    /// This method decides what each crossing found a hair beyond the end of its segment stands for.
    /// <para>
    /// Each segment accepts a crossing a little past either end, so that one exactly at a corner,
    /// which rounding may put just outside both segments that meet there, is not lost between them.
    /// But a ray passing near a corner also crosses one segment's continuation, which is not part of
    /// the lathe at all.  Left in, that crossing is counted as well as the real one beside it, and a
    /// CSG, which counts crossings to tell inside from outside, is turned inside out for the rest of
    /// the ray.  So a crossing beyond its segment's end stays only if the neighbor it shares that
    /// corner with crossed at the same place.  In the same direction, it is the same crossing twice,
    /// and the one beyond its segment goes.  In the opposite direction the ray grazed the corner, in
    /// and straight out again: both stay if the neighbor's lies on its own segment, where the ray
    /// touched the lathe, and both go if it too lies beyond, since the ray passed just outside.  With
    /// no such crossing beside it, it was never a crossing.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray the crossings lie along.</param>
    /// <param name="crossings">The crossings found, which are pruned in place.</param>
    internal void SettleCorners(Ray ray, List<(int Segment, Intersection Hit, double Along)> crossings)
    {
        HashSet<int> dropped = [];
        HashSet<int> settled = [];

        for (int index = 0; index < crossings.Count; index++)
        {
            (int segment, Intersection hit, double along) = crossings[index];

            if (along is >= 0 and <= 1 || dropped.Contains(index) || settled.Contains(index))
                continue;

            int neighbor = along > 1 ? _following[segment] : _preceding[segment];
            Point point = ray.At(hit.Distance);
            double direction = Facing(ray, hit);
            int partner = -1;

            for (int other = 0; other < crossings.Count; other++)
            {
                if (crossings[other].Segment == neighbor && !dropped.Contains(other) &&
                    (ray.At(crossings[other].Hit.Distance) - point).Magnitude <= _cornerTolerance)
                {
                    partner = other;
                    break;
                }
            }

            if (partner < 0)
                dropped.Add(index);
            else if (Math.Sign(Facing(ray, crossings[partner].Hit)) == Math.Sign(direction))
            {
                // The same crossing twice: this one goes, and the other is settled, so that when both
                // lie beyond their segments -- a ray through the corner itself -- one of them stays.
                dropped.Add(index);
                settled.Add(partner);
            }
            else if (crossings[partner].Along is < 0 or > 1)
            {
                dropped.Add(index);
                dropped.Add(partner);
            }
            else
                settled.Add(partner);
        }

        for (int index = crossings.Count - 1; index >= 0; index--)
        {
            if (dropped.Contains(index))
                crossings.RemoveAt(index);
        }
    }

    /// <summary>
    /// This method reports which way the ray crosses the lathe's surface at a hit: below nought going
    /// one way through it and above going the other, the same way for every segment of one profile.
    /// </summary>
    private static double Facing(Ray ray, Intersection hit)
    {
        return ray.Direction.Dot(((PrecomputedNormalIntersection) hit).PrecomputedNormal);
    }

    private bool Misses(Ray ray)
    {
        List<Intersection> intersections = [];

        _bounds.Intersect(ray, intersections);

        return intersections.Count == 0;
    }

    /// <summary>
    /// This method returns the normal for the lathe.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return ((PrecomputedNormalIntersection) intersection).PrecomputedNormal;
    }
}
