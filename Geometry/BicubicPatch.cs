using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a bicubic Bezier patch: a smooth curved surface defined by a 4x4 grid
/// of control points.  Unlike this project's other surfaces, a patch has no closed-form ray
/// intersection; POV-Ray's own approach is ported here instead of a simpler fixed tessellation,
/// for better accuracy: the control-point grid is recursively subdivided (via De Casteljau
/// midpoint splitting) into a private tree of bounding-sphere-culled nodes, terminating each
/// branch once its subpatch is flat enough (or a maximum recursion depth is reached) and storing
/// its four corners as two triangles.  A ray walks this tree, testing those leaf triangles with
/// the same Moller-Trumbore math <see cref="Triangle"/> already uses, then refines the *normal*
/// (not the position) to the true smooth surface by evaluating the patch's exact Bernstein-basis
/// tangents at each triangle corner and blending them by the hit's barycentric weights.  Ported
/// from POV-Ray's own bicubic patch primitive (<c>source/backend/shape/bezier.cpp</c>'s
/// <c>bezier_tree_builder</c>/<c>bezier_tree_walker</c>/<c>intersect_subpatch</c>/
/// <c>bezier_value</c>, and the precompute step in <c>source/backend/parser/parse.cpp</c>'s
/// <c>Parse_Bicubic_Patch</c>).
/// </summary>
public class BicubicPatch : Surface
{
    /// <summary>If a subpatch's flatness is measured below this, its own recursion stops.</summary>
    private const double PlaneEpsilon = 1.0e-10;

    /// <summary>Minimal intersection depth for a valid intersection.</summary>
    private const double MinimumDepth = 1.0e-5;

    /// <summary>
    /// This property provides the 4x4 grid of control points that define the patch.  The first
    /// index is the "u" direction, the second is "v".
    /// </summary>
    public Point[,] ControlPoints { get; set; }

    /// <summary>
    /// This property caps how many times a subpatch may be recursively split along its "u"
    /// direction while still not being flat enough to stop.
    /// </summary>
    public int USteps { get; set; } = 3;

    /// <summary>
    /// This property caps how many times a subpatch may be recursively split along its "v"
    /// direction while still not being flat enough to stop.
    /// </summary>
    public int VSteps { get; set; } = 3;

    /// <summary>
    /// This property controls how close to an actual plane a subpatch must be (the maximum
    /// distance any of its 16 control points may lie from that plane) before recursion stops.
    /// </summary>
    public double Flatness { get; set; } = 0.01;

    private PatchNode _root;

    /// <summary>
    /// This method precomputes the subdivision tree used to intersect rays with the patch.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (ControlPoints is not { Length: 16 } || ControlPoints.GetLength(0) != 4 || ControlPoints.GetLength(1) != 4)
            throw new Exception("A patch requires a 4x4 grid of control points.");

        if (USteps < 0 || VSteps < 0)
            throw new Exception("A patch's u/v steps must not be negative.");

        if (Flatness <= 0)
            throw new Exception("A patch's flatness must be positive.");

        _root = BuildNode(ControlPoints, 0, 1, 0, 1, 0);
    }

    /// <summary>
    /// This method determines the axis-aligned box that bounds the patch, from its own control
    /// points -- cheap, and a real win for Group-level ray culling in scenes with many patches.
    /// </summary>
    /// <returns>The bounding box for the patch.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        BoundingBox box = new ();

        foreach (Point point in ControlPoints)
            box.Add(point);

        return box;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the patch and, if so,
    /// where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double length = ray.Direction.Magnitude;
        Ray localRay = new (ray.Origin, ray.Direction.Unit, ray.TimeIndex);

        WalkNode(_root, localRay, length, intersections);
    }

    /// <summary>
    /// This method returns the normal for the patch.  The normal is always precomputed at hit
    /// time (blended from the true surface tangents at the hit's triangle corners), since it
    /// can't be recovered from the position alone.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return ((PrecomputedNormalIntersection) intersection).PrecomputedNormal;
    }

    /// <summary>
    /// This method walks the subdivision tree, culling by each node's bounding sphere first,
    /// recursing into interior nodes, and testing both of a leaf's triangles.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <param name="localRay">The ray, in local, unit-direction space.</param>
    /// <param name="length">The original ray direction's magnitude, to convert local
    /// distances back to the caller's own ray parameterization.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void WalkNode(PatchNode node, Ray localRay, double length, List<Intersection> intersections)
    {
        if (!SphereIsHitBy(localRay, node.Center, node.RadiusSquared))
            return;

        switch (node)
        {
            case InteriorNode interior:
                foreach (PatchNode child in interior.Children)
                    WalkNode(child, localRay, length, intersections);
                break;
            case LeafNode leaf:
                Point c00 = leaf.Corners[0];
                Point c03 = leaf.Corners[1];
                Point c33 = leaf.Corners[2];
                Point c30 = leaf.Corners[3];

                IntersectLeafTriangle(
                    localRay, length, c00, c03, c33,
                    leaf.U0, leaf.V0, leaf.U0, leaf.V1, leaf.U1, leaf.V1, intersections);
                IntersectLeafTriangle(
                    localRay, length, c00, c33, c30,
                    leaf.U0, leaf.V0, leaf.U1, leaf.V1, leaf.U1, leaf.V0, intersections);
                break;
        }
    }

    /// <summary>
    /// This method tests one of a leaf's two triangles, using the same barycentric
    /// Moller-Trumbore test <see cref="Triangle"/> uses for its own flat-triangle intersections.
    /// On a hit, the final normal isn't the flat triangle's own normal, but a blend of the true
    /// patch normal evaluated at each of the triangle's three corners, weighted by the hit's own
    /// barycentric coordinates -- matching POV-Ray's own refinement.
    /// </summary>
    private void IntersectLeafTriangle(
        Ray localRay, double length, Point p0, Point p1, Point p2,
        double u0, double v0, double u1, double v1, double u2, double v2,
        List<Intersection> intersections)
    {
        if (!TryIntersectTriangle(localRay, p0, p1, p2, out double t, out double a, out double b))
            return;

        if (t < MinimumDepth)
            return;

        double r = 1 - a - b;
        (_, Vector n0) = EvaluatePatch(u0, v0);
        (_, Vector n1) = EvaluatePatch(u1, v1);
        (_, Vector n2) = EvaluatePatch(u2, v2);
        Vector normal = n0 * r + n1 * a + n2 * b;
        double normalLengthSquared = normal.Dot(normal);

        normal = normalLengthSquared > PlaneEpsilon ? normal / Math.Sqrt(normalLengthSquared) : new Vector(1, 0, 0);

        intersections.Add(new PrecomputedNormalIntersection(this, t / length, normal));
    }

    /// <summary>
    /// This method solves for the ray/triangle intersection using the standard
    /// Moller-Trumbore approach (the same math <see cref="Triangle.AddIntersections"/> uses),
    /// parameterized over three arbitrary points rather than tied to a <see cref="Triangle"/>
    /// instance, since a patch's leaf triangles are transient.
    /// </summary>
    /// <returns><c>true</c>, if the ray hits the triangle, or <c>false</c>, if not.</returns>
    private static bool TryIntersectTriangle(
        Ray ray, Point p1, Point p2, Point p3, out double t, out double u, out double v)
    {
        Vector e1 = p2 - p1;
        Vector e2 = p3 - p1;
        Vector dirCrossE2 = ray.Direction.Cross(e2);
        double determinant = e1.Dot(dirCrossE2);

        t = u = v = 0;

        if (determinant.Near(0))
            return false;

        double f = 1 / determinant;
        Vector p1ToOrigin = ray.Origin - p1;

        u = f * p1ToOrigin.Dot(dirCrossE2);

        if (u is < 0 or > 1)
            return false;

        Vector originCrossE1 = p1ToOrigin.Cross(e1);

        v = f * ray.Direction.Dot(originCrossE1);

        if (v < 0 || u + v > 1)
            return false;

        t = f * e2.Dot(originCrossE1);

        return true;
    }

    /// <summary>
    /// This method evaluates the patch's own control points (never a subdivided subpatch -- the
    /// (u,v) parameters passed in are always relative to the whole, original patch) at a given
    /// (u,v), returning both the position and the true surface normal there, via a Bernstein-basis
    /// evaluation of position and both tangents.
    /// </summary>
    private (Point Position, Vector Normal) EvaluatePatch(double u, double v)
    {
        double[] uPow = new double[4];
        double[] uInv = new double[4];
        double[] vPow = new double[4];
        double[] vInv = new double[4];
        double[] uDeriv = new double[4];
        double[] uInvDeriv = new double[4];
        double[] vDeriv = new double[4];
        double[] vInvDeriv = new double[4];

        uPow[0] = 1; uInv[0] = 1; uDeriv[0] = 0; uInvDeriv[0] = 0;
        vPow[0] = 1; vInv[0] = 1; vDeriv[0] = 0; vInvDeriv[0] = 0;

        for (int i = 1; i < 4; i++)
        {
            uPow[i] = uPow[i - 1] * u;
            uInv[i] = uInv[i - 1] * (1 - u);
            vPow[i] = vPow[i - 1] * v;
            vInv[i] = vInv[i - 1] * (1 - v);
            uDeriv[i] = i * uPow[i - 1];
            uInvDeriv[i] = -i * uInv[i - 1];
            vDeriv[i] = i * vPow[i - 1];
            vInvDeriv[i] = -i * vInv[i - 1];
        }

        double[] binomial = [1, 3, 3, 1];
        Vector position = new (0, 0, 0);
        Vector uTangent = new (0, 0, 0);
        Vector vTangent = new (0, 0, 0);

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                double c = binomial[i] * binomial[j];
                double ut = uPow[i] * uInv[3 - i];
                double vt = vPow[j] * vInv[3 - j];
                Vector controlPoint = new (ControlPoints[i, j]);

                position += controlPoint * (c * ut * vt);
                uTangent += controlPoint * (c * vt * (uDeriv[i] * uInv[3 - i] + uPow[i] * uInvDeriv[3 - i]));
                vTangent += controlPoint * (c * ut * (vDeriv[j] * vInv[3 - j] + vPow[j] * vInvDeriv[3 - j]));
            }
        }

        Vector normal = uTangent.Cross(vTangent);
        double normalLengthSquared = normal.Dot(normal);
        double uLengthSquared = uTangent.Dot(uTangent);
        double vLengthSquared = vTangent.Dot(vTangent);

        normal = normalLengthSquared > PlaneEpsilon * uLengthSquared * vLengthSquared
            ? normal / Math.Sqrt(normalLengthSquared)
            : new Vector(1, 0, 0);

        return (new Point(position.X, position.Y, position.Z), normal);
    }

    /// <summary>
    /// This method builds one node of the subdivision tree, splitting further (in "u", "v", or
    /// both, depending on which axis limits have already been reached) until the subpatch is
    /// flat enough or both axis limits have been reached, whichever comes first.
    /// </summary>
    private PatchNode BuildNode(Point[,] patch, double u0, double u1, double v0, double v1, int depth)
    {
        (Point center, double radiusSquared) = ComputeBoundingSphere(patch);
        bool uLimited = depth >= USteps;
        bool vLimited = depth >= VSteps;

        if (IsFlatEnough(patch) || (uLimited && vLimited))
        {
            return new LeafNode
            {
                Center = center,
                RadiusSquared = radiusSquared,
                Corners = [patch[0, 0], patch[0, 3], patch[3, 3], patch[3, 0]],
                U0 = u0, U1 = u1, V0 = v0, V1 = v1
            };
        }

        double um = (u0 + u1) / 2;
        double vm = (v0 + v1) / 2;
        PatchNode[] children;

        if (!uLimited && !vLimited)
        {
            (Point[,] left, Point[,] right) = SplitU(patch);
            (Point[,] lowerLeft, Point[,] upperLeft) = SplitV(left);
            (Point[,] lowerRight, Point[,] upperRight) = SplitV(right);

            children =
            [
                BuildNode(lowerLeft, u0, um, v0, vm, depth + 1),
                BuildNode(upperLeft, u0, um, vm, v1, depth + 1),
                BuildNode(lowerRight, um, u1, v0, vm, depth + 1),
                BuildNode(upperRight, um, u1, vm, v1, depth + 1)
            ];
        }
        else if (uLimited)
        {
            (Point[,] bottom, Point[,] top) = SplitV(patch);

            children = [BuildNode(bottom, u0, u1, v0, vm, depth + 1), BuildNode(top, u0, u1, vm, v1, depth + 1)];
        }
        else
        {
            (Point[,] left, Point[,] right) = SplitU(patch);

            children = [BuildNode(left, u0, um, v0, v1, depth + 1), BuildNode(right, um, u1, v0, v1, depth + 1)];
        }

        return new InteriorNode { Center = center, RadiusSquared = radiusSquared, Children = children };
    }

    /// <summary>
    /// This method splits a 4x4 grid of points into left and right halves along its "u"
    /// (first-index) direction, via De Casteljau midpoint splitting of each of the 4 "v" rows.
    /// </summary>
    private static (Point[,] Left, Point[,] Right) SplitU(Point[,] patch)
    {
        Point[,] left = new Point[4, 4];
        Point[,] right = new Point[4, 4];

        for (int v = 0; v < 4; v++)
        {
            (Point a, Point b, Point c, Point d) =
                (patch[0, v], patch[1, v], patch[2, v], patch[3, v]);
            (Point ab, Point bc, Point cd) = (Mid(a, b), Mid(b, c), Mid(c, d));
            (Point abc, Point bcd) = (Mid(ab, bc), Mid(bc, cd));
            Point abcd = Mid(abc, bcd);

            (left[0, v], left[1, v], left[2, v], left[3, v]) = (a, ab, abc, abcd);
            (right[0, v], right[1, v], right[2, v], right[3, v]) = (abcd, bcd, cd, d);
        }

        return (left, right);
    }

    /// <summary>
    /// This method splits a 4x4 grid of points into bottom and top halves along its "v"
    /// (second-index) direction, via De Casteljau midpoint splitting of each of the 4 "u"
    /// columns.
    /// </summary>
    private static (Point[,] Bottom, Point[,] Top) SplitV(Point[,] patch)
    {
        Point[,] bottom = new Point[4, 4];
        Point[,] top = new Point[4, 4];

        for (int u = 0; u < 4; u++)
        {
            (Point a, Point b, Point c, Point d) =
                (patch[u, 0], patch[u, 1], patch[u, 2], patch[u, 3]);
            (Point ab, Point bc, Point cd) = (Mid(a, b), Mid(b, c), Mid(c, d));
            (Point abc, Point bcd) = (Mid(ab, bc), Mid(bc, cd));
            Point abcd = Mid(abc, bcd);

            (bottom[u, 0], bottom[u, 1], bottom[u, 2], bottom[u, 3]) = (a, ab, abc, abcd);
            (top[u, 0], top[u, 1], top[u, 2], top[u, 3]) = (abcd, bcd, cd, d);
        }

        return (bottom, top);
    }

    /// <summary>
    /// This method returns the midpoint of two points.
    /// </summary>
    private static Point Mid(Point a, Point b)
    {
        return new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
    }

    /// <summary>
    /// This method computes a sphere that bounds all 16 of a subpatch's control points: its
    /// center is their average, and its (squared) radius is the largest squared distance any of
    /// them has from that average.
    /// </summary>
    private static (Point Center, double RadiusSquared) ComputeBoundingSphere(Point[,] patch)
    {
        double xc = 0, yc = 0, zc = 0;

        foreach (Point point in patch)
        {
            xc += point.X; yc += point.Y; zc += point.Z;
        }

        xc /= 16; yc /= 16; zc /= 16;

        Point center = new (xc, yc, zc);
        double radiusSquared = 0;

        foreach (Point point in patch)
        {
            double dx = point.X - xc, dy = point.Y - yc, dz = point.Z - zc;
            double distanceSquared = dx * dx + dy * dy + dz * dz;

            if (distanceSquared > radiusSquared)
                radiusSquared = distanceSquared;
        }

        return (center, radiusSquared);
    }

    /// <summary>
    /// This method tests whether the given ray passes through the given bounding sphere, using
    /// a cheap "distance from ray to sphere center vs. radius" check rather than a full
    /// ray/sphere quadratic solve.
    /// </summary>
    private static bool SphereIsHitBy(Ray localRay, Point center, double radiusSquared)
    {
        double x = center.X - localRay.Origin.X;
        double y = center.Y - localRay.Origin.Y;
        double z = center.Z - localRay.Origin.Z;
        double distanceSquared = x * x + y * y + z * z;

        if (distanceSquared < radiusSquared)
            return true;

        double alongRay = x * localRay.Direction.X + y * localRay.Direction.Y + z * localRay.Direction.Z;

        if (alongRay <= 0)
            return false;

        double closestApproachSquared = distanceSquared - alongRay * alongRay;

        return closestApproachSquared <= radiusSquared + PlaneEpsilon;
    }

    /// <summary>
    /// This method measures how close to an actual plane a subpatch is: it picks three of the
    /// subpatch's own corners that aren't (nearly) coincident, builds a plane from them, and
    /// returns the largest distance any of the subpatch's 16 control points has from that plane
    /// -- or <c>false</c>, if no three mutually distinct corners could be found at all.
    /// </summary>
    private bool IsFlatEnough(Point[,] patch)
    {
        Point[] corners = [patch[0, 0], patch[0, 3], patch[3, 3], patch[3, 0]];
        int[][] cornerTriples = [[0, 1, 2], [0, 1, 3], [0, 2, 3], [1, 2, 3]];

        foreach (int[] triple in cornerTriples)
        {
            Point a = corners[triple[0]];
            Point b = corners[triple[1]];
            Point c = corners[triple[2]];

            if ((a - b).Magnitude.Near(0) || (a - c).Magnitude.Near(0) || (b - c).Magnitude.Near(0))
                continue;

            Vector v1 = a - b;
            Vector v2 = c - b;
            Vector cross = v1.Cross(v2);
            double crossLengthSquared = cross.Dot(cross);

            if (crossLengthSquared <= PlaneEpsilon * v1.Dot(v1) * v2.Dot(v2))
                continue;

            Vector normal = cross / Math.Sqrt(crossLengthSquared);
            double d = -normal.Dot(a);
            double maxDistance = 0;

            foreach (Point point in patch)
            {
                double distance = Math.Abs(normal.Dot(point) + d);

                if (distance > maxDistance)
                    maxDistance = distance;
            }

            return maxDistance < Flatness;
        }

        return false;
    }

    private abstract class PatchNode
    {
        public Point Center;
        public double RadiusSquared;
    }

    private sealed class InteriorNode : PatchNode
    {
        public PatchNode[] Children;
    }

    private sealed class LeafNode : PatchNode
    {
        public Point[] Corners;
        public double U0, U1, V0, V1;
    }
}
