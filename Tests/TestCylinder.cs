using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestCylinder
{
    private static readonly List<Ray> RaysThatMiss =
    [
        new (new Point(1, 0, 0), Directions.Up.Unit),
        new (Point.Zero, Directions.Up.Unit),
        new (new Point(0, 0, -5), new Vector(1, 1, 1).Unit)
    ];

    private static readonly List<(Ray Ray, double T0, double T1)> RaysThatHit =
    [
        (new Ray(new Point(1, 0, -5), Directions.In.Unit), 5, 5),
        (new Ray(new Point(0, 0, -5), Directions.In.Unit), 4, 6),
        (new Ray(new Point(0.5, 0, -5), new Vector(0.1, 1, 1).Unit), 6.807981, 7.088723)
    ];

    private static readonly List<(Point, Vector)> NormalsAtPoints =
    [
        (new Point(1, 0, 0), Directions.Right),
        (new Point(0, 5, -1), Directions.Out),
        (new Point(0, -2, 1), Directions.In),
        (new Point(-1, 1, 0), Directions.Left)
    ];

    private static readonly List<(Ray, int)> TruncatedIntersections =
    [
        (new Ray(new Point(0, 1.5, 0), new Vector(0.1, 1, 0).Unit), 0),
        (new Ray(new Point(0, 3, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 0, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 2, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 1, -5), Directions.In.Unit), 0),
        (new Ray(new Point(0, 1.5, -2), Directions.In.Unit), 2)
    ];

    private static readonly List<(Ray, int)> CappedIntersections =
    [
        (new Ray(new Point(0, 3, 0), Directions.Down.Unit), 2),
        (new Ray(new Point(0, 3, -2), new Vector(0, -1, 2).Unit), 2),
        (new Ray(new Point(0, 4, -2), new Vector(0, -1, 1).Unit), 2),
        (new Ray(new Point(0, 0, -2), new Vector(0, 1, 2).Unit), 2),
        (new Ray(new Point(0, -1, -2), new Vector(0, 1, 1).Unit), 2)
    ];

    private static readonly List<(Point, Vector)> NormalsAtPointsWithCaps =
    [
        (new Point(0, 1, 0), Directions.Down),
        (new Point(0.5, 1, 0), Directions.Down),
        (new Point(0, 1, 0.5), Directions.Down),
        (new Point(0, 2, 0), Directions.Up),
        (new Point(0.5, 2, 0), Directions.Up),
        (new Point(0, 2, 0.5), Directions.Up)
    ];

    [TestMethod]
    public void TestRayMissesCylinder()
    {
        Cylinder cylinder = new () { MinimumY = double.NegativeInfinity, MaximumY = double.PositiveInfinity };

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
        Cylinder cylinder = new () { MinimumY = double.NegativeInfinity, MaximumY = double.PositiveInfinity };

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
            Vector vector = cylinder.SurfaceNormalAt(point, null);

            Assert.IsTrue(expected.Matches(vector));
        }
    }

    [TestMethod]
    public void TestCylinderAttributes()
    {
        Cylinder cylinder = new ();

        Assert.AreEqual(-1, cylinder.MinimumY);
        Assert.AreEqual(1, cylinder.MaximumY);
        Assert.IsTrue(cylinder.Closed);
    }

    [TestMethod]
    public void TestCylinderTruncation()
    {
        Cylinder cylinder = new ()
        {
            MinimumY = 1,
            MaximumY = 2,
            Closed = false
        };

        foreach ((Ray ray, int count) in TruncatedIntersections)
        {
            List<Intersection> intersections = [];

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

    /// <summary>
    /// A cylinder scaled far more across than along -- the Ringworld's floor, a hundred and fifty million kilometers
    /// round and a million and a half across -- is met from inside by a ray square to its axis in the world, however
    /// little of that the ray keeps across in the cylinder's own space.  Until 2026-11-05 a ray there within a
    /// millionth of the axis was taken to run along the wall, and the floor was never seen from inside it.
    /// </summary>
    [TestMethod]
    public void TestACylinderScaledFarMoreAcrossThanAlongIsMetFromInside()
    {
        foreach (double radius in new[] { 1e3, 5e8, 1.5e11 })
        {
            Cylinder floor = new ()
            {
                MinimumY = -1e12, MaximumY = 1e12, Closed = false,
                Transform = Transforms.Translate(0, radius - 1, 0) * Transforms.RotateAroundZ(90) *
                            Transforms.Scale(radius, 1, radius)
            };

            foreach (Vector direction in new[] { new Vector(0, 0.34, 0.94), new Vector(-0.45, 0.34, 0.83) })
            {
                Ray ray = new (new Point(0, 2, 0), direction.Unit);
                List<Intersection> crossings = [];

                floor.Intersect(ray, crossings);

                Intersection ahead = crossings.SingleOrDefault(crossing => crossing.Distance > 0);

                Assert.IsNotNull(ahead, $"a ray up and along {direction} from the floor of a cylinder {radius} round " +
                                        "found no wall ahead");

                Point met = ray.At(ahead.Distance);
                double fromTheAxis = Math.Sqrt((met.Y - (radius - 1)) * (met.Y - (radius - 1)) + met.Z * met.Z);

                Assert.AreEqual(radius, fromTheAxis, radius * 1e-9, "the wall was met off the wall");
            }
        }
    }

    /// <summary>
    /// The caps of a cylinder scaled far more along than across -- a rod a hundred meters thick and two million
    /// kilometers long -- are met by a ray crossing an end at a slant, though the ray keeps next to nothing of its
    /// part along the rod in the rod's own space.
    /// </summary>
    [TestMethod]
    public void TestTheCapsOfACylinderScaledFarMoreAlongThanAcrossAreMet()
    {
        Cylinder rod = new () { Transform = Transforms.Scale(100, 1e9, 100) };
        Ray ray = new (new Point(-150, 1e9 + 100, 0), new Vector(1, -1, 0).Unit);
        List<Intersection> crossings = [];

        rod.Intersect(ray, crossings);

        Assert.IsTrue(crossings.Any(crossing => Math.Abs(ray.At(crossing.Distance).Y - 1e9) < 1e-3),
            $"the rod's top was not met: crossings at {string.Join(", ", crossings.Select(crossing => crossing.Distance))}");
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
            Vector vector = cylinder.SurfaceNormalAt(point, null);

            Assert.IsTrue(expected.Matches(vector));
        }
    }
}
