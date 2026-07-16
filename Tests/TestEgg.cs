using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestEgg
{
    /// <summary>
    /// This helper builds an egg whose bottom sphere is bigger than its top one (the more
    /// typical, "classic egg" configuration), radii chosen so the connecting collar's
    /// bounds work out to clean-ish numbers.
    /// </summary>
    private static Egg CreateBottomBiggerEgg()
    {
        Egg egg = new () { BottomRadius = 1.5, TopRadius = 0.9 };

        egg.PrepareForRendering();

        return egg;
    }

    [TestMethod]
    public void TestRayMissesEgg()
    {
        Egg egg = CreateBottomBiggerEgg();
        Ray ray = new (new Point(0, 20, 0), Directions.Right);
        List<Intersection> intersections = [];

        egg.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayHitsBottomCapPole()
    {
        // A ray tangent to the bottom sphere's own south pole (Y = -BottomRadius) hits
        // exactly there, below where the connecting collar takes over.  Being an exact
        // tangent point (discriminant zero), SolveQuadratic reports it as two coincident
        // roots rather than collapsing them into one -- both must land at the same distance.
        Egg egg = CreateBottomBiggerEgg();
        Ray ray = new (new Point(0, -1.5, -5), Directions.In);
        List<Intersection> intersections = [];

        egg.AddIntersections(ray, intersections);

        Assert.IsTrue(intersections.Count is 1 or 2);
        Assert.IsTrue(intersections.All(intersection => 5.0.Near(intersection.Distance)));
    }

    [TestMethod]
    public void TestRayHitsConnectingCollarNearTheEquator()
    {
        // Right around Y = 0, this egg's collar radius works out to almost exactly the
        // bottom sphere's own radius (1.5), since the collar meets the bottom sphere
        // tangentially just below there.
        Egg egg = CreateBottomBiggerEgg();
        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> intersections = [];

        egg.AddIntersections(ray, intersections);

        List<double> hitZs = intersections
            .Select(intersection => ray.At(intersection.Distance).Z)
            .OrderBy(z => z)
            .ToList();

        Assert.AreEqual(2, hitZs.Count);
        Assert.IsTrue((-1.5).Near(hitZs[0], 0.001));
        Assert.IsTrue(1.5.Near(hitZs[1], 0.001));
    }

    [TestMethod]
    public void TestRayHitsNearTheTopPole()
    {
        // The top pole sits at Y = BottomRadius + TopRadius = 2.4 -- another exact tangent
        // point, so (as with the bottom pole) two coincident roots are expected.
        Egg egg = CreateBottomBiggerEgg();
        Ray ray = new (new Point(0, 2.4, -5), Directions.In);
        List<Intersection> intersections = [];

        egg.AddIntersections(ray, intersections);

        Assert.IsTrue(intersections.Count is 1 or 2);
        Assert.IsTrue(intersections.All(intersection => 5.0.Near(intersection.Distance)));
    }

    [TestMethod]
    public void TestRayJustPastTheTopPoleMisses()
    {
        Egg egg = CreateBottomBiggerEgg();
        Ray ray = new (new Point(0, 2.5, -5), Directions.In);
        List<Intersection> intersections = [];

        egg.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestTopRadiusGreaterThanOrEqualToBottomRadiusBranch()
    {
        // The other half of Parse_Ovus's two-case precompute: this exercises the branch
        // taken when the top sphere is the bigger (or same-sized) one.  Bottom pole sits at
        // Y = -BottomRadius = -0.8; top pole sits at Y = BottomRadius + TopRadius = 2.0.
        Egg egg = new () { BottomRadius = 0.8, TopRadius = 1.2 };

        egg.PrepareForRendering();

        Ray bottomPoleRay = new (new Point(0, -0.8, -5), Directions.In);
        Ray topPoleRay = new (new Point(0, 2.0, -5), Directions.In);
        Ray missRay = new (new Point(0, -0.9, -5), Directions.In);
        List<Intersection> bottomHits = [];
        List<Intersection> topHits = [];
        List<Intersection> missHits = [];

        egg.AddIntersections(bottomPoleRay, bottomHits);
        egg.AddIntersections(topPoleRay, topHits);
        egg.AddIntersections(missRay, missHits);

        // Both poles are exact tangent points, so (see the bottom-bigger tests above) two
        // coincident roots are expected rather than one.
        Assert.IsTrue(bottomHits.Count is 1 or 2);
        Assert.IsTrue(topHits.Count is 1 or 2);
        Assert.AreEqual(0, missHits.Count);
    }

    [TestMethod]
    public void TestNonPositiveRadiiThrow()
    {
        Egg egg = new () { BottomRadius = 0, TopRadius = 1 };

        Assert.ThrowsExactly<Exception>(egg.PrepareForRendering);
    }

    [TestMethod]
    public void TestTopRadiusTooLargeThrows()
    {
        // POV-Ray silently substitutes a plain sphere in this case; this project favors a
        // clear error over a silent shape substitution the DSL author didn't ask for.
        Egg egg = new () { BottomRadius = 1, TopRadius = 2.5 };

        Assert.ThrowsExactly<Exception>(egg.PrepareForRendering);
    }

    [TestMethod]
    public void TestSurfaceNormalAtThePoles()
    {
        Egg egg = CreateBottomBiggerEgg();

        Assert.IsTrue(Directions.Down.Matches(egg.SurfaceNormaAt(new Point(0, -1.5, 0), null)));
        Assert.IsTrue(Directions.Up.Matches(egg.SurfaceNormaAt(new Point(0, 2.4, 0), null)));
    }

    [TestMethod]
    public void TestSurfaceNormalOnTheConnectingCollarIsUnitLength()
    {
        // The collar's normal formula is algebraically different from the caps' (see
        // Egg.SurfaceNormaAt), so this confirms it still produces a proper unit vector,
        // roughly pointing away from the egg's own axis, at a point known to lie on it.
        Egg egg = CreateBottomBiggerEgg();
        Point point = new (1.5, 0, 0);
        Vector normal = egg.SurfaceNormaAt(point, null);

        Assert.IsTrue(1.0.Near(normal.Magnitude));
        Assert.IsTrue(normal.X > 0);
    }
}
