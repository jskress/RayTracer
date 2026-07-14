using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestParallelogram
{
    /// <summary>
    /// This helper builds a 2x2 square lying flat in the X/Z plane at Y = 0, spanning
    /// X:[-1,1] and Z:[-1,1].  Using a square (equal, perpendicular sides) rather than a
    /// general parallelogram means the in-bounds test can't be accidentally right due to
    /// which side happens to line up with which barycentric coordinate.
    /// </summary>
    private static Parallelogram CreateSquare()
    {
        return new Parallelogram
        {
            Point = new Point(-1, 0, -1),
            Side1 = new Vector(2, 0, 0),
            Side2 = new Vector(0, 0, 2)
        };
    }

    [TestMethod]
    public void TestNormalPointsAwayFromTheSides()
    {
        Parallelogram square = CreateSquare();

        Assert.IsTrue(new Vector(0, 1, 0).Matches(square.Normal));
    }

    [TestMethod]
    public void TestRayHitsThroughTheCenter()
    {
        Parallelogram square = CreateSquare();
        Ray ray = new (new Point(0, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        square.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(5.0.Near(intersections[0].Distance));
    }

    [TestMethod]
    public void TestRayHitsNearACorner()
    {
        Parallelogram square = CreateSquare();
        Ray ray = new (new Point(0.9, 5, 0.9), Directions.Down);
        List<Intersection> intersections = [];

        square.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(5.0.Near(intersections[0].Distance));
    }

    [TestMethod]
    public void TestRayMissesOutsideTheXBound()
    {
        Parallelogram square = CreateSquare();
        Ray ray = new (new Point(5, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        square.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayMissesOutsideTheZBound()
    {
        Parallelogram square = CreateSquare();
        Ray ray = new (new Point(0, 5, 5), Directions.Down);
        List<Intersection> intersections = [];

        square.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayParallelToTheSquareMisses()
    {
        Parallelogram square = CreateSquare();
        Ray ray = new (new Point(0, 5, 0), Directions.Right);
        List<Intersection> intersections = [];

        square.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayPointingAwayMisses()
    {
        Parallelogram square = CreateSquare();
        Ray ray = new (new Point(0, -5, 0), Directions.Down);
        List<Intersection> intersections = [];

        square.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestSurfaceNormalIsConstantAcrossTheSurface()
    {
        Parallelogram square = CreateSquare();
        Point[] points =
        [
            new Point(0, 0, 0),
            new Point(0.9, 0, -0.9),
            new Point(-1, 0, 1)
        ];

        foreach (Point point in points)
            Assert.IsTrue(square.Normal.Matches(square.SurfaceNormaAt(point, null)));
    }
}
