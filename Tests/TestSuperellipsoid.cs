using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestSuperellipsoid
{
    /// <summary>
    /// This helper builds a superellipsoid with the given exponents, already prepared for
    /// rendering.
    /// </summary>
    private static Superellipsoid Create(double eastWest, double northSouth)
    {
        Superellipsoid superellipsoid = new () { EastWest = eastWest, NorthSouth = northSouth };

        superellipsoid.PrepareForRendering();

        return superellipsoid;
    }

    [TestMethod]
    public void TestRayMissesSuperellipsoid()
    {
        Superellipsoid superellipsoid = Create(0.5, 0.5);
        Ray ray = new (new Point(5, 5, 5), Directions.Right);
        List<Intersection> intersections = [];

        superellipsoid.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestExponentsOfOneDegenerateToAUnitSphere()
    {
        // With east/west = north/south = 1, the implicit equation reduces exactly to
        // x^2 + y^2 + z^2 - 1 = 0, a unit sphere -- so hits can be checked against the
        // closed-form sphere distances.
        Superellipsoid superellipsoid = Create(1, 1);
        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> intersections = [];

        superellipsoid.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(intersection => intersection.Distance)
            .OrderBy(distance => distance)
            .ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(4.0.Near(distances[0], 0.0001));
        Assert.IsTrue(6.0.Near(distances[1], 0.0001));
    }

    [TestMethod]
    public void TestSurfaceNormalOnTheDegenerateSphereMatchesTheClosedForm()
    {
        Superellipsoid superellipsoid = Create(1, 1);
        Vector normal = superellipsoid.SurfaceNormaAt(new Point(1, 0, 0), null);

        Assert.IsTrue(Directions.Right.Matches(normal));
    }

    [TestMethod]
    public void TestAxisAlignedRaysAlwaysHitAtExactlyOneRegardlessOfExponents()
    {
        // Along a pure coordinate axis, the implicit equation reduces to |axis| - 1 = 0 for
        // any positive exponents, since the other two coordinates are zero -- this holds for
        // a rounded cube just as much as a sphere or octahedron, and is a good cross-check
        // that the sampling/root-finding isn't exponent-dependent in some subtly broken way.
        foreach ((double eastWest, double northSouth) in new[] { (0.2, 0.2), (1.0, 1.0), (2.0, 2.0), (0.3, 1.5) })
        {
            Superellipsoid superellipsoid = Create(eastWest, northSouth);
            Ray ray = new (new Point(0, 0, -5), Directions.In);
            List<Intersection> intersections = [];

            superellipsoid.AddIntersections(ray, intersections);

            List<double> distances = intersections.Select(intersection => intersection.Distance)
                .OrderBy(distance => distance)
                .ToList();

            Assert.AreEqual(2, distances.Count, $"east/west={eastWest}, north/south={northSouth}");
            Assert.IsTrue(4.0.Near(distances[0], 0.0001));
            Assert.IsTrue(6.0.Near(distances[1], 0.0001));
        }
    }

    [TestMethod]
    public void TestOctahedronFlatFaceHit()
    {
        // With east/west = north/south = 2, the implicit equation reduces exactly to
        // |x| + |y| + |z| - 1 = 0, an octahedron -- a ray straight through the point
        // (0.3, 0.3, z) should hit the positive-Z face exactly where z = 1 - 0.3 - 0.3 = 0.4,
        // and the negative-Z face at z = -0.4.
        Superellipsoid superellipsoid = Create(2, 2);
        Ray ray = new (new Point(0.3, 0.3, -5), Directions.In);
        List<Intersection> intersections = [];

        superellipsoid.AddIntersections(ray, intersections);

        List<double> hitZs = intersections.Select(intersection => ray.At(intersection.Distance).Z)
            .OrderBy(z => z)
            .ToList();

        Assert.AreEqual(2, hitZs.Count);
        Assert.IsTrue((-0.4).Near(hitZs[0], 0.0001));
        Assert.IsTrue(0.4.Near(hitZs[1], 0.0001));
    }

    [TestMethod]
    public void TestSurfaceNormalOnTheOctahedronFace()
    {
        Superellipsoid superellipsoid = Create(2, 2);
        Vector normal = superellipsoid.SurfaceNormaAt(new Point(1.0 / 3, 1.0 / 3, 1.0 / 3), null);
        double component = 1 / Math.Sqrt(3);

        Assert.IsTrue(1.0.Near(normal.Magnitude));
        Assert.IsTrue(component.Near(normal.X, 0.0001));
        Assert.IsTrue(component.Near(normal.Y, 0.0001));
        Assert.IsTrue(component.Near(normal.Z, 0.0001));
    }

    [TestMethod]
    public void TestRoundedCubeDiagonalRayThroughNearACornerStillHits()
    {
        // A sharply-rounded "cube" (small exponents) -- a ray aimed roughly at one corner
        // and through to the opposite one should still produce exactly two hits, confirming
        // the sampling-plane approach doesn't lose intersections near where several of those
        // subdividing planes converge.
        Superellipsoid superellipsoid = Create(0.2, 0.2);
        Vector direction = (Point.Zero - new Point(5, 5, 5)).Unit;
        Ray ray = new (new Point(5, 5, 5), direction);
        List<Intersection> intersections = [];

        superellipsoid.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
    }

    [TestMethod]
    public void TestGrazingRayNearOriginFindsBothCrossings()
    {
        // Captured mid-render: a shadow ray originating right at (an epsilon off)
        // Intersection.OverPoint, grazing back across the same flat face at a shallow angle
        // before properly exiting further on.  If AddIntersections skips straight from its
        // origin to a sample a whole DepthTolerance further along the ray's own direction
        // (rather than starting at the origin itself), it can jump clean over this pair of
        // very-close-together crossings and report only the far one -- an incomplete
        // intersection list, which matters to any caller (CSG, refraction, shadow tests
        // themselves) that relies on the full, correct set rather than just "is there a hit
        // closer than X".
        Superellipsoid superellipsoid = Create(2, 2);
        Point origin = new (0.02163492880364526, -0.6056302963682703, -0.37273650687889265);
        Vector direction = new (-0.7018384490415537, 0.5726381489895612, -0.4236842477231782);
        Ray ray = new (origin, direction);
        List<Intersection> intersections = [];

        superellipsoid.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
    }

    [TestMethod]
    public void TestShadowRayHeadingAwayFromAFlatFaceDoesNotSelfIntersect()
    {
        // Reproduces real "shadow acne" found while rendering: a shadow ray's origin is
        // nudged only a tiny epsilon off the surface along the normal (Intersection.OverPoint),
        // so it still starts essentially on the surface itself.  If AddIntersections responds
        // by clamping its first sample forward by a whole DepthTolerance along the *ray's own
        // direction* (rather than starting right at that origin), that forward push -- even for
        // a ray heading straight out along the very normal it was nudged along, i.e. a light
        // that is genuinely on this side of a flat face -- can overshoot back across the
        // surface and manufacture a phantom self-intersection.  A ray leaving a flat, convex
        // face along its own outward normal must never re-enter the shape at all.
        Superellipsoid superellipsoid = Create(2, 2);
        Point facePoint = new (0.3, 0.3, 0.4);
        Vector normal = new Vector(1, 1, 1).Unit;
        Vector tangent = new Vector(1, -1, 0).Unit;

        // Mostly tangent to the face, with only a slight component along the outward normal --
        // an entirely ordinary grazing-angle light direction.
        Vector direction = (tangent * 0.999 + normal * 0.001).Unit;
        Ray ray = new (facePoint + normal * 0.000001, direction);
        List<Intersection> intersections = [];

        superellipsoid.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestNonPositiveExponentsThrow()
    {
        Superellipsoid zeroEastWest = new () { EastWest = 0, NorthSouth = 1 };
        Superellipsoid negativeNorthSouth = new () { EastWest = 1, NorthSouth = -1 };

        Assert.ThrowsExactly<Exception>(zeroEastWest.PrepareForRendering);
        Assert.ThrowsExactly<Exception>(negativeNorthSouth.PrepareForRendering);
    }
}
