using RayTracer;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestCylinder
{
    private static readonly List<Ray> RaysThatMiss = new ()
    {
        new Ray(new Point(1, 0, 0), Directions.Up.Unit),
        new Ray(Point.Zero, Directions.Up.Unit),
        new Ray(new Point(0, 0, -5), new Vector(1, 1, 1).Unit)
    };

    private static readonly List<(Ray Ray, double T0, double T1)> RaysThatHit = new ()
    {
        (new Ray(new Point(1, 0, -5), Directions.In.Unit), 5, 5),
        (new Ray(new Point(0, 0, -5), Directions.In.Unit), 4, 6),
        (new Ray(new Point(0.5, 0, -5), new Vector(0.1, 1, 1).Unit), 6.807981, 7.088723)
    };

    private static readonly List<(Point, Vector)> NormalsAtPoints = new ()
    {
        (new Point(1, 0, 0), Directions.Right),
        (new Point(0, 5, -1), Directions.Out),
        (new Point(0, -2, 1), Directions.In),
        (new Point(-1, 1, 0), Directions.Left)
    };

    private static readonly List<(Ray, int)> TruncatedIntersections = new ()
    {
        (new Ray(new Point(0, 1.5, 0), new Vector(0.1, 1, 0).Unit), 0),
        (new Ray(new Point(0, 3, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 0, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 2, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 1, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 1.5, -2), Directions.In.Unit), 2)
    };

    private static readonly List<(Ray, int)> CappedIntersections = new ()
    {
        (new Ray(new Point(0, 3, 0), Directions.Down.Unit), 2),
        (new Ray(new Point(0, 3, -2), new Vector(0, -1, 2).Unit), 2),
        (new Ray(new Point(0, 4, -2), new Vector(0, -1, 1).Unit), 2),
        (new Ray(new Point(0, 0, -2), new Vector(0, 1, 2).Unit), 2),
        (new Ray(new Point(0, -1, -2), new Vector(0, 1, 1).Unit), 2)
    };

    private static readonly List<(Point, Vector)> NormalsAtPointsWithCaps = new ()
    {
        (new Point(0, 1, 0), Directions.Down),
        (new Point(0.5, 1, 0), Directions.Down),
        (new Point(0, 1, 0.5), Directions.Down),
        (new Point(0, 2, 0), Directions.Up),
        (new Point(0.5, 2, 0), Directions.Up),
        (new Point(0, 2, 0.5), Directions.Up)
    };

    [TestMethod]
    public void TestRayMissesCylinder()
    {
        Cylinder cylinder = new ();

        foreach (Ray ray in RaysThatMiss)
        {
            List<Intersection> intersections = new ();

            cylinder.AddIntersections(ray, intersections);

            Assert.AreEqual(0, intersections.Count);
        }
    }

    [TestMethod]
    public void TestRayHitsCylinder()
    {
        Cylinder cylinder = new ();

        foreach ((Ray ray, double t0, double t1) in RaysThatHit)
        {
            List<Intersection> intersections = new ();

            cylinder.AddIntersections(ray, intersections);

            Assert.AreEqual(2, intersections.Count);
            Assert.IsTrue(t0.Near(intersections[0].Distance));
            Assert.IsTrue(t1.Near(intersections[1].Distance));
        }
    }

    [TestMethod]
    public void TestCylinderNormals()
    {
        Cylinder cylinder = new ();

        foreach ((Point point, Vector expected) in NormalsAtPoints)
        {
            Vector vector = cylinder.SurfaceNormaAt(point, null);

            Assert.IsTrue(expected.Matches(vector));
        }
    }

    [TestMethod]
    public void TestCylinderAttributes()
    {
        Cylinder cylinder = new ();

        Assert.AreEqual(double.NegativeInfinity, cylinder.MinimumY);
        Assert.AreEqual(double.PositiveInfinity, cylinder.MaximumY);
        Assert.IsFalse(cylinder.Closed);
    }

    [TestMethod]
    public void TestCylinderTruncation()
    {
        Cylinder cylinder = new ()
        {
            MinimumY = 1,
            MaximumY = 2
        };

        foreach ((Ray ray, int count) in TruncatedIntersections)
        {
            List<Intersection> intersections = new ();

            cylinder.AddIntersections(ray, intersections);

            Assert.AreEqual(count, intersections.Count);
        }
    }

    [TestMethod]
    public void TestCappedCylinderTruncation()
    {
        Cylinder cylinder = new ()
        {
            MinimumY = 1,
            MaximumY = 2,
            Closed = true
        };

        foreach ((Ray ray, int count) in CappedIntersections)
        {
            List<Intersection> intersections = new ();

            cylinder.AddIntersections(ray, intersections);

            Assert.AreEqual(count, intersections.Count);
        }
    }

    [TestMethod]
    public void TestCappedCylinderNormals()
    {
        Cylinder cylinder = new ()
        {
            MinimumY = 1,
            MaximumY = 2,
            Closed = true
        };

        foreach ((Point point, Vector expected) in NormalsAtPointsWithCaps)
        {
            Vector vector = cylinder.SurfaceNormaAt(point, null);

            Assert.IsTrue(expected.Matches(vector));
        }
    }
}
