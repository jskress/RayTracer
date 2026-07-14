using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestLathe
{
    [TestMethod]
    public void TestShapeIntersections()
    {
        Point origin = new Point(2, 2, 2);
        Point lookAt = new Point(0, 1, 0);
        Vector direction = lookAt - origin;
        Ray ray = new Ray(origin, direction.Unit);
        using GeneralPath cylinder = new GeneralPath()
            .MoveTo(0, 0)
            .LineTo(1, 0)
            .LineTo(1, 2)
            .LineTo(0, 2);
        TwoDRay shapeRay = TwoDRay.ProjectedToXy(ray);

        // Verify some basic stuff.
        Assert.AreEqual(3, cylinder.Segments.Count);
        Assert.AreEqual(2.0, shapeRay.Origin.X);
        Assert.AreEqual(2.0, shapeRay.Origin.Y);
        Assert.IsTrue(shapeRay.Direction.X.Near(-0.666667));
        Assert.IsTrue(shapeRay.Direction.Y.Near(-0.333333));

        // Verify cylinder structure.
        Line line = cylinder.Segments[0] as Line;
        Assert.IsNotNull(line);
        Assert.AreEqual(new TwoDPoint(0, 0), line.Points[0]);
        Assert.AreEqual(new TwoDPoint(1, 0), line.Points[1]);

        // Make sure our shape ray does not intersect segments it shouldn't
        Assert.AreEqual(0, cylinder.Segments[0].GetIntersections(shapeRay).Count());
        Assert.AreEqual(0, cylinder.Segments[2].GetIntersections(shapeRay).Count());

        // Now let's verify the shape intersection.
        TwoDIntersection[] intersections = cylinder.Segments[1].GetIntersections(shapeRay).ToArray();

        Assert.AreEqual(1, intersections.Length);
        Assert.IsTrue(intersections[0].Distance.Near(1.5));
        Assert.AreEqual(new TwoDPoint(1, 1.5), intersections[0].Point);
        Assert.AreEqual(new TwoDVector(1, 0), intersections[0].TwoDNormal);
    }

    /// <summary>
    /// This test uses a ray whose infinite extension does NOT pass through the Y axis --
    /// i.e. a typical camera ray, as opposed to the axis-crossing rays every other test in
    /// this file (and the old, broken implementation) relied on.  The math is hand-derived:
    /// a single vertical wall segment from (r=1, h=0) to (r=1, h=2), hit by a ray with
    /// origin (3, -1, 0.5) and (unnormalized) direction (-1, 1, 0).  Since direction.Z = 0,
    /// the ray sits in the fixed plane Z = 0.5 the whole way, so it crosses the radius-1
    /// cylinder wall (X^2 + Z^2 = 1) where (3-t)^2 = 0.75, i.e. at t = 3 -/+ sqrt(0.75).
    /// Only the smaller root lands within the segment's height range [0, 2]; the larger
    /// root's height (2.87) falls outside it and must be rejected.
    /// </summary>
    [TestMethod]
    public void TestGeneralRayHitsCylindricalWall()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(1, 0)
            .LineTo(1, 2);
        LathePathSurface wall = new (profile.Segments[0]);
        Lathe lathe = new () { Path = profile };
        Ray ray = new (new Point(3, -1, 0.5), new Vector(-1, 1, 0));

        double expectedT = 3 - Math.Sqrt(0.75);

        List<Intersection> intersections = wall.GetIntersections(lathe, ray).ToList();

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(expectedT.Near(intersections[0].Distance));

        Point hitPoint = ray.At(intersections[0].Distance);

        Assert.IsTrue(Math.Sqrt(0.75).Near(hitPoint.X));
        Assert.IsTrue(0.5.Near(hitPoint.Z));
        Assert.IsTrue((hitPoint.Y is >= 0 and <= 2));

        Vector normal = ((PrecomputedNormalIntersection) intersections[0]).PrecomputedNormal;
        Vector expectedNormal = new Vector(hitPoint.X, 0, hitPoint.Z).Unit;

        Assert.IsTrue(expectedNormal.Matches(normal));
    }

    /// <summary>
    /// Same ray as above, but this time going through the full Lathe (bottom cap, wall,
    /// top cap) via its public AddIntersections(), to confirm the fix holds end to end and
    /// not just for one isolated segment.  Since direction.Y != 0, the ray crosses every
    /// height exactly once, including both cap heights (Y=0 at t=1, Y=2 at t=3).  At Y=0
    /// the ray is at (X,Z)=(2,0.5), radius ~2.06 -- well outside the bottom cap's [0,1]
    /// radius range, so no hit there.  At Y=2 the ray is at (X,Z)=(0,0.5), radius exactly
    /// 0.5 -- inside the top cap's range, so that *is* a genuine hit, in addition to the
    /// wall hit already verified above.  Expect exactly those two intersections.
    /// </summary>
    [TestMethod]
    public void TestFullLatheAgainstNonAxisCrossingRay()
    {
        using GeneralPath profile = new GeneralPath()
            .MoveTo(0, 0)
            .LineTo(1, 0)
            .LineTo(1, 2)
            .LineTo(0, 2);
        Lathe lathe = new () { Path = profile };
        Ray ray = new (new Point(3, -1, 0.5), new Vector(-1, 1, 0));
        List<Intersection> intersections = [];

        lathe.PrepareForRendering();
        lathe.AddIntersections(ray, intersections);

        double expectedWallT = 3 - Math.Sqrt(0.75);
        const double expectedCapT = 3.0;

        List<Intersection> sorted = intersections.OrderBy(intersection => intersection.Distance).ToList();

        Assert.AreEqual(2, sorted.Count);
        Assert.IsTrue(expectedWallT.Near(sorted[0].Distance));
        Assert.IsTrue(expectedCapT.Near(sorted[1].Distance));

        Point capHit = ray.At(sorted[1].Distance);

        Assert.IsTrue(new Point(0, 2, 0.5).Matches(capHit));

        Vector capNormal = ((PrecomputedNormalIntersection) sorted[1]).PrecomputedNormal;

        Assert.IsTrue(Directions.Up.Matches(capNormal));
    }

    /// <summary>
    /// Exercises the horizontal-ray branch (ray direction.Y == 0), which is solved
    /// differently since the height equation can't be inverted for t.  A ray traveling
    /// straight across at height 1 (inside the wall's [0, 2] range) must cross the
    /// radius-1 wall at exactly the two points where X^2 + Z^2 = 1 along its path.
    /// </summary>
    [TestMethod]
    public void TestHorizontalRayHitsCylindricalWall()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(1, 0)
            .LineTo(1, 2);
        LathePathSurface wall = new (profile.Segments[0]);
        Lathe lathe = new () { Path = profile };
        Ray ray = new (new Point(-3, 1, 0), Directions.Right);

        List<Intersection> intersections = wall.GetIntersections(lathe, ray)
            .OrderBy(intersection => intersection.Distance)
            .ToList();

        Assert.AreEqual(2, intersections.Count);
        Assert.IsTrue(2.0.Near(intersections[0].Distance));
        Assert.IsTrue(4.0.Near(intersections[1].Distance));

        Point p0 = ray.At(intersections[0].Distance);
        Point p1 = ray.At(intersections[1].Distance);

        Assert.IsTrue(new Point(-1, 1, 0).Matches(p0));
        Assert.IsTrue(new Point(1, 1, 0).Matches(p1));
    }

    /// <summary>
    /// A ray aimed entirely past the (finite) cylinder, off to the side, must not report
    /// any intersections with the wall segment.
    /// </summary>
    [TestMethod]
    public void TestGeneralRayMissesCylindricalWall()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(1, 0)
            .LineTo(1, 2);
        LathePathSurface wall = new (profile.Segments[0]);
        Lathe lathe = new () { Path = profile };
        Ray ray = new (new Point(10, 1, 10), Directions.Right);

        List<Intersection> intersections = wall.GetIntersections(lathe, ray).ToList();

        Assert.AreEqual(0, intersections.Count);
    }

    /// <summary>
    /// The lathe's bounding box must cover the full swept radius in both X and Z, not just
    /// the profile's own (one-sided, non-reflected) X extent -- otherwise a ray hitting the
    /// far side of the revolved surface would be wrongly culled before AddIntersections()
    /// ever runs.  The profile here is a single wall segment fixed at radius 1 (MinX ==
    /// MaxX == 1), so a box built from the profile's raw X extent alone would collapse to a
    /// sliver around X == 1, Z == 1 -- nowhere near this ray, which sits at Z = 0.5 the
    /// whole way and only ever reaches radius 1 on the negative-X side.  Goes through the
    /// public Intersect() so the bounding box is actually exercised, not bypassed.
    /// </summary>
    [TestMethod]
    public void TestBoundingBoxCoversFullRevolvedExtent()
    {
        using GeneralPath profile = new GeneralPath()
            .MoveTo(1, 0)
            .LineTo(1, 2);
        Lathe lathe = new () { Path = profile };
        Ray ray = new (new Point(-3, -1, 0.5), new Vector(1, 1, 0));
        List<Intersection> intersections = [];

        lathe.PrepareForRendering();
        lathe.Intersect(ray, intersections);

        double expectedWallT = 3 - Math.Sqrt(0.75);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(expectedWallT.Near(intersections[0].Distance));
    }

    /// <summary>
    /// Exercises a quadratic-curve profile segment.  Rather than hand-solving the
    /// resulting quartic-in-u equation, this picks an arbitrary curve parameter (u = 0.5,
    /// the symmetric bulge's peak) and computes the exact point there directly via the
    /// quadratic Bezier formula: radius(u) = 1 + 2u - 2u^2, height(u) = 2u, so
    /// radius(0.5) = 1.5, height(0.5) = 1.  A ray whose origin *is* that point guarantees
    /// a true intersection at t = 0.  At the bulge's peak the profile's tangent is purely
    /// vertical (radius'(0.5) = 0), so the expected normal is purely radial.
    /// </summary>
    [TestMethod]
    public void TestQuadCurveSegmentGeneralRay()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(1, 0)
            .QuadTo(2, 1, 1, 2);
        LathePathSurface bulge = new (profile.Segments[0]);
        Lathe lathe = new () { Path = profile };
        Ray ray = new (new Point(1.5, 1, 0), new Vector(1, 0.5, 0.3));

        List<Intersection> intersections = bulge.GetIntersections(lathe, ray)
            .Where(intersection => intersection.Distance.Near(0))
            .ToList();

        Assert.AreEqual(1, intersections.Count);

        Vector normal = ((PrecomputedNormalIntersection) intersections[0]).PrecomputedNormal;

        Assert.IsTrue(new Vector(1, 0, 0).Matches(normal));
    }

    /// <summary>
    /// Same technique as above, but for a cubic-curve profile segment, with control points
    /// deliberately asymmetric so both radius(u) and height(u) are genuine cubics (not
    /// degree-collapsed by symmetry) -- this exercises the full degree-6 polynomial that
    /// results from a cubic profile segment, well beyond what the old hand-rolled solver
    /// (or the original Polynomials class, capped at degree 4) could ever have handled.
    /// radius(u) = 1 + 1.5u - 4.5u^2 + 4u^3 and height(u) = 1.5u + 3u^2 - 1.5u^3 give the
    /// exact point at u = 0.4 used below.
    /// </summary>
    [TestMethod]
    public void TestCubicCurveSegmentGeneralRay()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(1, 0)
            .CubicTo(1.5, 0.5, 0.5, 2, 2, 3);
        LathePathSurface segment = new (profile.Segments[0]);
        Lathe lathe = new () { Path = profile };
        const double expectedRadius = 1.136;
        const double expectedHeight = 0.984;
        Ray ray = new (new Point(expectedRadius, expectedHeight, 0), new Vector(1, 0.5, 0.3));

        List<Intersection> intersections = segment.GetIntersections(lathe, ray)
            .Where(intersection => intersection.Distance.Near(0, 0.0001))
            .ToList();

        Assert.AreEqual(1, intersections.Count);
    }
}
