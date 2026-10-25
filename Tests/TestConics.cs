using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestConics
{
    private static readonly List<(Ray Ray, double T0, double T1)> RaysThatHit =
    [
        (new Ray(new Point(0, 0, -5), Directions.In.Unit), 5, 5),
        (new Ray(new Point(0, 0, -5), new Vector(1, 1, 1).Unit), 8.660254, 8.660254),
        (new Ray(new Point(1, 1, -5), new Vector(-0.5, -1, 1).Unit), 4.550056, 49.449944)
    ];

    private static readonly List<(Ray, int)> CappedIntersections =
    [
        (new Ray(new Point(0, 0, -5), Directions.Up.Unit), 0),
        (new Ray(new Point(0, 0, -0.25), new Vector(0, 1, 1).Unit), 2),
        (new Ray(new Point(0, 0, -0.25), Directions.Up.Unit), 4)
    ];

    private static readonly List<(Point, Vector)> NormalsAtPoints =
    [
        (new Point(0, 0, 0), new Vector(0, 0, 0)),
        (new Point(1, 1, 1), new Vector(1, -Math.Sqrt(2), 1)),
        (new Point(-1, -1, 0), new Vector(-1, 1, 0))
    ];

    [TestMethod]
    public void TestRayHitsConic()
    {
        Conic conic = new () { MinimumY = double.NegativeInfinity, MaximumY = double.PositiveInfinity };

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
        Conic conic = new () { MinimumY = double.NegativeInfinity, MaximumY = double.PositiveInfinity };
        Ray ray = new (new Point(0, 0, -1), new Vector(0, 1, 1).Unit);
        List<Intersection> intersections = [];

        conic.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(0.353553.Near(intersections[0].Distance));
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

    /// <summary>
    /// A closed cone's cap is as wide as the cone is at its height: a ray straight down just inside the wall at
    /// heights other than 0 and 1 meets the cap, and one just outside meets nothing of it, at 0.3 and at 2 alike.
    /// </summary>
    [TestMethod]
    public void TestACapIsAsWideAsTheConeWhereItStands()
    {
        foreach (double top in new[] { 0.3, 2.0 })
        {
            Conic conic = new () { MinimumY = 0.1, MaximumY = top, Closed = true };

            List<Intersection> inside = [];
            List<Intersection> outside = [];

            conic.AddIntersections(new Ray(new Point(top * 0.98, top + 1, 0), new Vector(0, -1, 0)), inside);
            conic.AddIntersections(new Ray(new Point(top * 1.02, top + 1, 0), new Vector(0, -1, 0)), outside);

            Assert.IsTrue(inside.Any(hit => Math.Abs(hit.Distance - 1) < 1e-9), $"just inside the rim of a cap at {top}");
            Assert.IsFalse(outside.Any(hit => Math.Abs(hit.Distance - 1) < 1e-9), $"just outside the rim of a cap at {top}");
            Assert.AreEqual(new Vector(0, 1, 0), conic.SurfaceNormalAt(new Point(top * 0.98, top, 0), null), $"the cap's normal at {top}");
        }
    }

    [TestMethod]
    public void TestConicNormals()
    {
        Conic conic = new ();

        foreach ((Point point, Vector expected) in NormalsAtPoints)
        {
            Vector vector = conic.SurfaceNormalAt(point, null);

            Assert.IsTrue(expected.Matches(vector));
        }
    }
}
