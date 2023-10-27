using RayTracer;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestConics
{
    private static readonly List<(Ray Ray, double T0, double T1)> RaysThatHit = new ()
    {
        (new Ray(new Point(0, 0, -5), Directions.In.Unit), 5, 5),
        (new Ray(new Point(0, 0, -5), new Vector(1, 1, 1).Unit), 8.66025, 8.66025),
        (new Ray(new Point(1, 1, -5), new Vector(-0.5, -1, 1).Unit), 4.55006, 49.44994)
    };

    private static readonly List<(Ray, int)> CappedIntersections = new ()
    {
        (new Ray(new Point(0, 0, -5), Directions.Up.Unit), 0),
        (new Ray(new Point(0, 0, -0.25), new Vector(0, 1, 1).Unit), 2),
        (new Ray(new Point(0, 0, -0.25), Directions.Up.Unit), 4)
    };

    private static readonly List<(Point, Vector)> NormalsAtPoints = new ()
    {
        (new Point(0, 0, 0), new Vector(0, 0, 0)),
        (new Point(1, 1, 1), new Vector(1, -Math.Sqrt(2), 1)),
        (new Point(-1, -1, 0), new Vector(-1, 1, 0))
    };

    [TestMethod]
    public void TestRayHitsConic()
    {
        Conic conic = new ();

        foreach ((Ray ray, double t0, double t1) in RaysThatHit)
        {
            List<Intersection> intersections = new ();

            conic.AddIntersections(ray, intersections);

            Assert.AreEqual(2, intersections.Count);
            Assert.IsTrue(t0.Near(intersections[0].Distance));
            Assert.IsTrue(t1.Near(intersections[1].Distance));
        }
    }

    [TestMethod]
    public void TestParallelRay()
    {
        Conic conic = new ();
        Ray ray = new (new Point(0, 0, -1), new Vector(0, 1, 1).Unit);
        List<Intersection> intersections = new ();

        conic.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(0.35355.Near(intersections[0].Distance));
    }

    [TestMethod]
    public void TestCappedConicTruncation()
    {
        Conic conic = new ()
        {
            MinimumY = -0.5,
            MaximumY = 0.5,
            Closed = true
        };

        foreach ((Ray ray, int count) in CappedIntersections)
        {
            List<Intersection> intersections = new ();

            conic.AddIntersections(ray, intersections);

            Assert.AreEqual(count, intersections.Count);
        }
    }

    [TestMethod]
    public void TestConicNormals()
    {
        Conic conic = new ();

        foreach ((Point point, Vector expected) in NormalsAtPoints)
        {
            Vector vector = conic.SurfaceNormaAt(point, null);

            Assert.IsTrue(expected.Matches(vector));
        }
    }
}
