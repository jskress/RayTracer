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
        GeneralPath cylinder = new GeneralPath()
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
        GeneralPath profile = new GeneralPath()
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
        GeneralPath profile = new GeneralPath()
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

    /// <summary>
    /// A lathe used in a CSG must report crossings behind the ray's origin as well as in front,
    /// so the operation can tell inside from outside for a ray that starts within the lathe --
    /// which a shadow or reflection ray, cast from a surface the lathe sits on, does.  Here a
    /// square profile is revolved into an annular tube (1 &lt;= radius &lt;= 2, 0 &lt;= y &lt;= 1)
    /// and a box keeps only the +X, +Z quarter.  A ray that starts inside the tube in the cut-away
    /// quarter and rises toward the kept quarter -- the path a shadow ray takes to an overhead
    /// light -- must not be occluded by the part that was removed.  Dropping the behind-the-origin
    /// crossings inverts the tube's inside/outside tracking and makes the box's far faces read as
    /// occluders, which showed up as a full-ring shadow (and reflection) under a quarter-ring.
    /// </summary>
    [TestMethod]
    public void TestLatheInCsgIsNotOccludedByItsRemovedPart()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(1, 0)
            .LineTo(2, 0)
            .LineTo(2, 1)
            .LineTo(1, 1)
            .ClosePath();
        Lathe lathe = new () { Path = profile };
        Cube cube = new ()
        {
            Transform = Transforms.Translate(10, 0, 10) * Transforms.Scale(10, 10, 10)
        };
        CsgSurface csg = new ()
        {
            Operation = CsgOperation.Intersection, Left = lathe, Right = cube
        };

        csg.PrepareForRendering();

        Point from = new (-1.5, 0.5, -0.2);   // inside the tube, in the removed -X,-Z quarter.
        Point toward = new (0.5, 10, 0.5);     // up and over the kept quarter, as toward a lamp.
        Vector direction = toward - from;
        double distance = direction.Magnitude;
        Ray ray = new (from, direction.Unit);
        List<Intersection> hits = [];

        csg.AddIntersections(ray, hits);

        Assert.IsFalse(
            hits.Any(hit => hit.Distance > 0.0001 && hit.Distance < distance),
            "nothing the CSG keeps should stand between a point in the removed quarter and the lamp");
    }

    /// <summary>
    /// A profile drawn on the negative-X side of the axis revolves to exactly the same surface
    /// as its mirror image, so it must have exactly the same normals.  Its sloped face here runs
    /// from radius 2 at the bottom to radius 1 at the top, so it faces outward and up, along
    /// (2, 1) in (radial, up) terms.  With the radius running negative, the normal used to come
    /// out reflected -- leaning in toward the axis -- which shaded a sloped face as if it lay in
    /// its own shadow.  A flat or upright face only came out reversed, which shading repairs, so
    /// only a slope can tell the two apart.
    /// </summary>
    [TestMethod]
    public void TestProfileOnNegativeSideHasTheSameNormalsAsItsMirror()
    {
        foreach (double side in new[] { 1.0, -1.0 })
        {
            GeneralPath profile = new GeneralPath()
                .MoveTo(0, 0)
                .LineTo(side * 2, 0)
                .LineTo(side * 1, 2)
                .LineTo(0, 2)
                .ClosePath();
            Lathe lathe = new () { Path = profile };
            Ray ray = new (new Point(5, 0.8, 0.3), new Vector(-1, 0.05, 0));
            List<Intersection> intersections = [];

            lathe.PrepareForRendering();
            lathe.AddIntersections(ray, intersections);

            Intersection first = intersections.OrderBy(intersection => intersection.Distance).First();
            Point hitPoint = ray.At(first.Distance);
            Vector radial = new Vector(hitPoint.X, 0, hitPoint.Z).Unit;
            Vector expectedNormal = (radial * 2 + Directions.Up).Unit;
            Vector normal = ((PrecomputedNormalIntersection) first).PrecomputedNormal;

            Assert.IsTrue(expectedNormal.Matches(normal), $"the profile drawn with side {side}");
        }
    }

    /// <summary>
    /// A profile that starts on the axis with a level tangent -- a dome -- makes the ray's
    /// equation all but even in u, so its roots come in near +/- pairs, and a ray that grazes the
    /// dome nearly doubles each pair as well.  That is the problem the exact solver's eigenvalue
    /// iteration stalls on, and it threw, taking the whole render down with it.  This is the very
    /// ray that did so, in the lathe's own space: it passes 2.01 from the axis at a height where
    /// the dome is only 0.99 across, and its four roots are two complex pairs agreeing to three
    /// places, so the right answer is a miss.
    /// </summary>
    [TestMethod]
    public void TestDomeGrazingRayDoesNotStallTheRootFinder()
    {
        GeneralPath profile = new GeneralPath()
            .MoveTo(0, 0.46875)
            .QuadTo(-2.3362542, 0.46875, -4.625, 0);
        LathePathSurface dome = new (profile.Segments[0]);
        Lathe lathe = new () { Path = profile };
        Ray ray = new (
            new Point(0.8749999999999982, 0.44634848052473597, -35.05425470658209),
            new Vector(0.0808888485870876, 9.374057135713709E-05, 2.4986910544096803));

        Assert.AreEqual(0, dome.GetIntersections(lathe, ray).Count());
    }

    /// <summary>
    /// A solid cylinder, radius 1 from height 0 to 1, entered through its top just inside the rim
    /// and left through the far wall.  That is two crossings.  Each segment of a profile accepts a
    /// root a hair beyond its own ends, so as not to lose one to rounding at a corner, and a ray this
    /// close to the rim passes the side wall's continuation just above the top as well -- so the
    /// entry used to be reported twice, once by the top and once by the side.  A CSG counts
    /// crossings to tell inside from outside, and a doubled one turns it inside out.  The ray runs
    /// 0.3 off the axis, since one through the axis meets the profile's closing segment there.
    /// </summary>
    [TestMethod]
    public void TestEntryThroughACornerCrossesOnce()
    {
        Lathe lathe = SolidCylinder(1, 0, 1);
        Point corner = new (Math.Sqrt((1 - 1e-7) * (1 - 1e-7) - 0.09), 1, 0.3);
        Vector direction = new Vector(-1, -0.3, 0).Unit;
        Ray ray = new (corner - direction * 2, direction);
        List<Intersection> hits = [];

        lathe.PrepareForRendering();
        lathe.Intersect(ray, hits);

        Assert.AreEqual(2, hits.Count);
    }

    /// <summary>
    /// A ray that passes a hair outside a corner never touches the lathe, but it crosses both the
    /// side wall's continuation and the top's, in one direction and then the other.  Both must go:
    /// keeping one would turn a CSG's count inside out just as doubling an entry does, and keeping
    /// both would report a touch where there is none.
    /// </summary>
    [TestMethod]
    public void TestPassingJustOutsideACornerFindsNothing()
    {
        Lathe lathe = SolidCylinder(1, 0, 1);
        Point corner = new (Math.Sqrt((1 + 1e-7) * (1 + 1e-7) - 0.09), 1, 0.3);
        Vector direction = new Vector(-1, 1, 0).Unit;
        Ray ray = new (corner - direction * 2, direction);
        List<Intersection> hits = [];

        lathe.PrepareForRendering();
        lathe.Intersect(ray, hits);

        Assert.AreEqual(0, hits.Count);
    }

    /// <summary>
    /// A ray skimming in almost level crosses the side wall's continuation a hair above the top and
    /// only enters through the top well inside the rim, so the two lie too far apart to be one
    /// crossing at the corner.  The first is not a crossing at all, and must not be counted.
    /// </summary>
    [TestMethod]
    public void TestSkimmingInPastACornerCrossesOnce()
    {
        Lathe lathe = SolidCylinder(1, 0, 1);
        Point above = new (Math.Sqrt(1 - 0.09), 1 + 5e-7, 0.3);
        Vector direction = new Vector(-1, -0.001, 0).Unit;
        Ray ray = new (above - direction * 2, direction);
        List<Intersection> hits = [];

        lathe.PrepareForRendering();
        lathe.Intersect(ray, hits);

        Assert.AreEqual(2, hits.Count);
    }

    /// <summary>
    /// Rays swept across a corner a billionth at a time, at several slopes, from well inside it to
    /// well outside: every one must cross an even number of times, since each enters what it leaves.
    /// A ray through the corner itself may find both of its crossings there a hair beyond the
    /// segments that meet, and it is easy to drop both as strays rather than keep one.
    /// </summary>
    [TestMethod]
    public void TestRaysAcrossACornerAlwaysCrossEvenly()
    {
        Lathe lathe = SolidCylinder(1, 0, 1);

        lathe.PrepareForRendering();

        foreach (double slope in new[] { -0.3, -1, -3 })
        {
            Vector direction = new Vector(-1, slope, 0).Unit;

            for (int step = -60; step <= 60; step++)
            {
                double radius = 1 + step * 1e-9;
                Point corner = new (Math.Sqrt(radius * radius - 0.09), 1 + step * 1e-9, 0.3);
                Ray ray = new (corner - direction * 2, direction);
                List<Intersection> hits = [];

                lathe.Intersect(ray, hits);

                Assert.AreEqual(0, hits.Count % 2, $"slope {slope}, {step} billionths from the corner");
            }
        }
    }

    /// <summary>
    /// A ray straight through a corner finds the crossing there twice, once at the end of each
    /// segment that meets there, and rounding may put both a hair beyond their segments.  One of the
    /// two must stay: dropping the first as the second's twin and then the second as a stray, with
    /// its twin gone, loses the crossing altogether.  A ray that does this cannot be aimed, so the
    /// two crossings are pushed beyond their ends by hand, as rounding would.
    /// </summary>
    [TestMethod]
    public void TestACornerCrossingFoundBeyondBothSegmentsIsKeptOnce()
    {
        Lathe lathe = SolidCylinder(1, 0, 1);
        Point corner = new (Math.Sqrt(1 - 0.09), 1, 0.3);
        Vector direction = new Vector(-1, -0.3, 0).Unit;
        Ray ray = new (corner - direction * 2, direction);

        lathe.PrepareForRendering();

        List<(int Segment, Intersection Hit, double Along)> crossings = lathe.FindCrossings(ray)
            .Select(crossing => crossing.Along switch
            {
                > 0.999999 => (crossing.Segment, crossing.Hit, 1 + 1e-9),
                < 0.000001 => (crossing.Segment, crossing.Hit, -1e-9),
                _ => crossing
            })
            .ToList();

        Assert.AreEqual(2, crossings.Count(crossing => crossing.Along is < 0 or > 1));

        lathe.SettleCorners(ray, crossings);

        Assert.AreEqual(2, crossings.Count);
        Assert.AreEqual(1, crossings.Count(crossing => crossing.Hit.Distance.Near(2)));
    }

    /// <summary>
    /// The way a doubled crossing at a corner showed itself: a lathe used to trim something it
    /// wholly encloses -- here a ball inside a cylinder -- let rays that passed just inside its rim
    /// lose the ball's near side, so they saw its far side from within, a back face lit only by
    /// ambient light.  On the Enterprise's secondary hull this was a dotted curve of dark specks,
    /// traced by the rays from the camera that grazed the rim of the wide sleeve its nose lathe
    /// ends in.
    /// </summary>
    [TestMethod]
    public void TestLatheTrimmingAnEnclosedSolidKeepsItsNearSide()
    {
        CsgSurface csg = new ()
        {
            Operation = CsgOperation.Intersection,
            Left = SolidCylinder(2, -2, 2),
            Right = new Sphere()
        };
        Point corner = new (2 - 1e-7, 2, 0);
        Vector direction = (new Point(0, 0, 0) - corner).Unit;
        Ray ray = new (corner - direction, direction);
        List<Intersection> hits = [];

        csg.PrepareForRendering();
        csg.AddIntersections(ray, hits);

        Intersection first = hits.Where(hit => hit.Distance > 0).OrderBy(hit => hit.Distance).First();

        Assert.IsInstanceOfType<Sphere>(first.Surface);
        Assert.IsTrue(first.Distance.Near(1 + Math.Sqrt(8) - 1));
    }

    /// <summary>
    /// This method returns a lathe that is a solid cylinder about the Y axis, capped at both ends.
    /// </summary>
    private static Lathe SolidCylinder(double radius, double bottom, double top)
    {
        return new Lathe
        {
            Path = new GeneralPath()
                .MoveTo(0, bottom)
                .LineTo(radius, bottom)
                .LineTo(radius, top)
                .LineTo(0, top)
                .ClosePath()
        };
    }
}
