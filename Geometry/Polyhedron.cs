using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a convex polyhedron: the solid on the inside of a set of planes, each n.x = d with n
/// pointing out.  A ray is clipped by every plane in turn, the latest it enters through and the earliest it leaves
/// by, which is a cube's slab test with as many slabs as the solid has faces -- exact, and about as cheap per face.
/// It is what a fracture's sharp pieces are.
/// </summary>
public class Polyhedron : Surface
{
    private Vector[] _normals = [];
    private double[] _offsets = [];
    private BoundingBox _box;

    /// <summary>
    /// This method sets the polyhedron's faces from a polytope.
    /// </summary>
    internal void SetFrom(Polytope polytope)
    {
        _normals = polytope.Faces.Select(face => face.Normal).ToArray();
        _offsets = polytope.Faces.Select(face => face.Offset).ToArray();
        _box = polytope.Bounds();
    }

    /// <summary>
    /// This property reports how many faces the polyhedron has.
    /// </summary>
    public int FaceCount => _normals.Length;

    /// <summary>
    /// This method returns the box round the polyhedron's corners.
    /// </summary>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return _box is null ? null : new BoundingBox().Add(_box.Minimum).Add(_box.Maximum);
    }

    /// <summary>
    /// This method adds where the ray enters and leaves the polyhedron, behind its origin as well as ahead.
    /// </summary>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double near = double.NegativeInfinity;
        double far = double.PositiveInfinity;
        int entering = -1;
        int leaving = -1;

        for (int index = 0; index < _normals.Length; index++)
        {
            Vector normal = _normals[index];
            double toward = normal.Dot(ray.Direction);
            double inside = _offsets[index] - normal.Dot(ray.Origin - Point.Zero);

            if (Math.Abs(toward) < 1e-15)
            {
                // Running along the plane: inside it all the way, or never.
                if (inside < 0)
                    return;

                continue;
            }

            double distance = inside / toward;

            if (toward < 0)
            {
                if (distance > near)
                {
                    near = distance;
                    entering = index;
                }
            }
            else if (distance < far)
            {
                far = distance;
                leaving = index;
            }

            if (near > far)
                return;
        }

        if (entering < 0 || leaving < 0)
            return;

        intersections.Add(new PrecomputedNormalIntersection(this, near, _normals[entering]));
        intersections.Add(new PrecomputedNormalIntersection(this, far, _normals[leaving]));
    }

    /// <summary>
    /// This method returns the normal at a point: the face the ray met, when it is known, else the face the point
    /// lies nearest.
    /// </summary>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        if (intersection is PrecomputedNormalIntersection precomputed)
            return precomputed.PrecomputedNormal;

        int nearest = 0;

        for (int index = 1; index < _normals.Length; index++)
        {
            if (Math.Abs(_normals[index].Dot(point - Point.Zero) - _offsets[index]) <
                Math.Abs(_normals[nearest].Dot(point - Point.Zero) - _offsets[nearest]))
            {
                nearest = index;
            }
        }

        return _normals[nearest];
    }
}
