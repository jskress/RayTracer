using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a convex polyhedron with its edges and corners rounded: every point within a radius of a
/// smaller, inner polyhedron.  That is the inner one grown by a ball, and it is exactly the union of the inner
/// polyhedron, a slab the radius deep over each of its faces, a cylinder along each of its edges and a ball at each
/// of its corners.  It is convex, so a ray enters it where it enters the first of those it meets and leaves where it
/// leaves the last: no marching, no tessellation, the same answer a sphere or a cylinder gives.
/// <para>
/// It is what a fracture's worn pieces are: the piece drawn in by the radius on every face, then grown back, so its
/// faces stand where they did and only its edges and corners are taken off.
/// </para>
/// </summary>
public class RoundedPolyhedron : Surface
{
    // A slab over one face of the inner polyhedron: its outward normal and the planes bounding it -- the face's own
    // plane moved out by the radius, the face's plane itself, and one through each of the face's edges, standing
    // square to it.
    private sealed record FaceSlab(Vector Normal, Vector[] Normals, double[] Offsets);

    private sealed record Edge(Point From, Vector Along, double Length);

    private FaceSlab[] _faces = [];
    private Edge[] _edges = [];
    private Point[] _corners = [];
    private double _radius;
    private BoundingBox _box;

    /// <summary>
    /// This property reports how far the edges are rounded.
    /// </summary>
    public double Radius => _radius;

    /// <summary>
    /// This method sets the polyhedron from the inner polytope it is grown from, and how far.
    /// </summary>
    internal void SetFrom(Polytope inner, double radius)
    {
        _radius = radius;
        _faces = inner.Faces.Select(face => SlabOver(face, radius)).ToArray();
        _edges = inner.Edges()
            .Select(edge => new Edge(edge.From, (edge.To - edge.From).Unit, (edge.To - edge.From).Magnitude))
            .ToArray();
        _corners = inner.Vertices().ToArray();

        BoundingBox box = inner.Bounds();

        _box = new BoundingBox()
            .Add(box.Minimum - new Vector(radius, radius, radius))
            .Add(box.Maximum + new Vector(radius, radius, radius));
    }

    private static FaceSlab SlabOver(Polytope.Face face, double radius)
    {
        List<Vector> normals = [face.Normal, -face.Normal];
        List<double> offsets = [face.Offset + radius, -face.Offset];
        int count = face.Corners.Count;

        for (int index = 0; index < count; index++)
        {
            Point from = face.Corners[index];
            Point to = face.Corners[(index + 1) % count];
            Vector side = (to - from).Cross(face.Normal).Unit;

            normals.Add(side);
            offsets.Add(side.Dot(from - Point.Zero));
        }

        return new FaceSlab(face.Normal, normals.ToArray(), offsets.ToArray());
    }

    /// <summary>
    /// This method returns the box round the rounded polyhedron.
    /// </summary>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return _box is null ? null : new BoundingBox().Add(_box.Minimum).Add(_box.Maximum);
    }

    /// <summary>
    /// This method adds where the ray enters and leaves the rounded polyhedron, behind its origin as well as ahead.
    /// </summary>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double first = double.PositiveInfinity;
        double last = double.NegativeInfinity;
        Vector entering = null;
        Vector leaving = null;
        Vector toOrigin = ray.Origin - Point.Zero;

        foreach (FaceSlab face in _faces)
        {
            if (Slab(face, ray, toOrigin) is not var (near, far))
                continue;

            if (near < first)
            {
                first = near;
                entering = face.Normal;
            }

            if (far > last)
            {
                last = far;
                leaving = face.Normal;
            }
        }

        foreach (Edge edge in _edges)
        {
            if (Cylinder(edge, ray) is not var (near, far))
                continue;

            if (near < first)
            {
                first = near;
                entering = AwayFrom(edge, ray.At(near));
            }

            if (far > last)
            {
                last = far;
                leaving = AwayFrom(edge, ray.At(far));
            }
        }

        foreach (Point corner in _corners)
        {
            if (Ball(corner, ray) is not var (near, far))
                continue;

            if (near < first)
            {
                first = near;
                entering = ray.At(near) - corner;
            }

            if (far > last)
            {
                last = far;
                leaving = ray.At(far) - corner;
            }
        }

        if (entering is null || leaving is null || first > last)
            return;

        intersections.Add(new PrecomputedNormalIntersection(this, first, entering.Unit));
        intersections.Add(new PrecomputedNormalIntersection(this, last, leaving.Unit));
    }

    // Where the ray enters and leaves a face's slab: the planes bounding it, clipped in turn.
    private static (double Near, double Far)? Slab(FaceSlab face, Ray ray, Vector toOrigin)
    {
        double near = double.NegativeInfinity;
        double far = double.PositiveInfinity;

        for (int index = 0; index < face.Normals.Length; index++)
        {
            Vector normal = face.Normals[index];
            double toward = normal.Dot(ray.Direction);
            double inside = face.Offsets[index] - normal.Dot(toOrigin);

            if (Math.Abs(toward) < 1e-15)
            {
                if (inside < 0)
                    return null;

                continue;
            }

            double distance = inside / toward;

            if (toward < 0)
                near = Math.Max(near, distance);
            else
                far = Math.Min(far, distance);

            if (near > far)
                return null;
        }

        return (near, far);
    }

    // Where the ray enters and leaves the solid cylinder round an edge, between the planes square to it at its ends.
    private (double Near, double Far)? Cylinder(Edge edge, Ray ray)
    {
        Vector offset = ray.Origin - edge.From;
        double alongRay = ray.Direction.Dot(edge.Along);
        double alongOffset = offset.Dot(edge.Along);
        Vector squareRay = ray.Direction - edge.Along * alongRay;
        Vector squareOffset = offset - edge.Along * alongOffset;
        double a = squareRay.Dot(squareRay);
        double b = 2 * squareOffset.Dot(squareRay);
        double c = squareOffset.Dot(squareOffset) - _radius * _radius;
        double near, far;

        if (a < 1e-15)
        {
            // Running with the edge: inside its cylinder all the way, or never.
            if (c > 0)
                return null;

            near = double.NegativeInfinity;
            far = double.PositiveInfinity;
        }
        else
        {
            double discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
                return null;

            double root = Math.Sqrt(discriminant);

            near = (-b - root) / (2 * a);
            far = (-b + root) / (2 * a);
        }

        // And between the ends: 0 <= alongOffset + t alongRay <= length.
        if (Math.Abs(alongRay) < 1e-15)
        {
            if (alongOffset < 0 || alongOffset > edge.Length)
                return null;
        }
        else
        {
            double one = -alongOffset / alongRay;
            double other = (edge.Length - alongOffset) / alongRay;

            near = Math.Max(near, Math.Min(one, other));
            far = Math.Min(far, Math.Max(one, other));
        }

        return near > far ? null : (near, far);
    }

    // Where the ray enters and leaves a ball at a corner.
    private (double Near, double Far)? Ball(Point corner, Ray ray)
    {
        Vector offset = ray.Origin - corner;
        double a = ray.Direction.Dot(ray.Direction);
        double b = 2 * offset.Dot(ray.Direction);
        double c = offset.Dot(offset) - _radius * _radius;
        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return null;

        double root = Math.Sqrt(discriminant);

        return ((-b - root) / (2 * a), (-b + root) / (2 * a));
    }

    // The way out from an edge's axis to a point on its cylinder.
    private static Vector AwayFrom(Edge edge, Point point)
    {
        Vector offset = point - edge.From;

        return offset - edge.Along * offset.Dot(edge.Along);
    }

    /// <summary>
    /// This method returns the normal at a point: the one the ray's crossing worked out, when it is known, else the
    /// way out from the nearest part of the inner polyhedron.
    /// </summary>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        if (intersection is PrecomputedNormalIntersection precomputed)
            return precomputed.PrecomputedNormal;

        Vector best = null;
        double nearest = double.PositiveInfinity;

        foreach (Point corner in _corners)
        {
            double distance = (point - corner).Magnitude;

            if (distance < nearest)
            {
                nearest = distance;
                best = point - corner;
            }
        }

        foreach (Edge edge in _edges)
        {
            double along = (point - edge.From).Dot(edge.Along);

            if (along < 0 || along > edge.Length)
                continue;

            Vector away = AwayFrom(edge, point);

            if (away.Magnitude < nearest)
            {
                nearest = away.Magnitude;
                best = away;
            }
        }

        foreach (FaceSlab face in _faces)
        {
            double above = face.Normal.Dot(point - Point.Zero) + face.Offsets[1];

            if (Math.Abs(above - _radius) < Math.Abs(nearest - _radius) && Slab(face, new Ray(point, face.Normal), point - Point.Zero) is not null)
            {
                nearest = above;
                best = face.Normal;
            }
        }

        return (best ?? new Vector(0, 1, 0)).Unit;
    }
}
