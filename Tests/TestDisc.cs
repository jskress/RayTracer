using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestDisc
{
    /// <summary>
    /// This helper builds a disc of radius 2, lying flat in the X/Z plane at Y = 0, centered
    /// at the origin.
    /// </summary>
    private static Disc CreateDisc()
    {
        return new Disc
        {
            Center = Point.Zero,
            Normal = Directions.Up,
            Radius = 2
        };
    }

    [TestMethod]
    public void TestNormalMatchesTheGivenNormal()
    {
        Disc disc = CreateDisc();

        Assert.IsTrue(Directions.Up.Matches(disc.Normal));
    }

    [TestMethod]
    public void TestNormalIsNormalized()
    {
        Disc disc = new ()
        {
            Center = Point.Zero,
            Normal = new Vector(0, 2, 0),
            Radius = 2
        };

        Assert.IsTrue(Directions.Up.Matches(disc.Normal));
    }

    [TestMethod]
    public void TestRayHitsThroughTheCenter()
    {
        Disc disc = CreateDisc();
        Ray ray = new (new Point(0, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        disc.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(5.0.Near(intersections[0].Distance));
    }

    [TestMethod]
    public void TestRayHitsNearTheEdge()
    {
        Disc disc = CreateDisc();
        Ray ray = new (new Point(1.9, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        disc.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(5.0.Near(intersections[0].Distance));
    }

    [TestMethod]
    public void TestRayMissesBeyondTheRadius()
    {
        Disc disc = CreateDisc();
        Ray ray = new (new Point(2.1, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        disc.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayMissesInsideTheInnerRadiusHole()
    {
        Disc ring = new ()
        {
            Center = Point.Zero,
            Normal = Directions.Up,
            Radius = 2,
            InnerRadius = 1
        };
        Ray ray = new (new Point(0.5, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        ring.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayHitsBetweenTheInnerAndOuterRadii()
    {
        Disc ring = new ()
        {
            Center = Point.Zero,
            Normal = Directions.Up,
            Radius = 2,
            InnerRadius = 1
        };
        Ray ray = new (new Point(1.5, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        ring.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(5.0.Near(intersections[0].Distance));
    }

    [TestMethod]
    public void TestRayParallelToTheDiscMisses()
    {
        Disc disc = CreateDisc();
        Ray ray = new (new Point(0, 5, 0), Directions.Right);
        List<Intersection> intersections = [];

        disc.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayPointingAwayMisses()
    {
        Disc disc = CreateDisc();
        Ray ray = new (new Point(0, -5, 0), Directions.Down);
        List<Intersection> intersections = [];

        disc.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestSurfaceNormalIsConstantAcrossTheSurface()
    {
        Disc disc = CreateDisc();
        Point[] points =
        [
            new Point(0, 0, 0),
            new Point(1.9, 0, 0),
            new Point(-1, 0, 1)
        ];

        foreach (Point point in points)
            Assert.IsTrue(disc.Normal.Matches(disc.SurfaceNormaAt(point, null)));
    }
}
