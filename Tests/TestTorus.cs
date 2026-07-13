using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestTorus
{
    [TestMethod]
    public void TestRayMissesTorus()
    {
        Torus torus = new () { MajorRadius = 2, MinorRadius = 0.5 };
        Ray ray = new (new Point(0, 10, 0), Directions.Right);
        List<Intersection> intersections = [];

        torus.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayHitsTorusAlongEquator()
    {
        // A ray straight through the plane of the torus crosses its tube 4 times: entering
        // and exiting the near side, then entering and exiting the far side.
        Torus torus = new () { MajorRadius = 2, MinorRadius = 0.5 };
        Ray ray = new (new Point(-10, 0, 0), Directions.Right);
        List<Intersection> intersections = [];
        double[] expected = [7.5, 8.5, 11.5, 12.5];

        torus.AddIntersections(ray, intersections);

        List<double> actual = intersections.Select(intersection => intersection.Distance)
            .OrderBy(distance => distance)
            .ToList();

        Assert.AreEqual(expected.Length, actual.Count);

        for (int index = 0; index < expected.Length; index++)
            Assert.IsTrue(expected[index].Near(actual[index]));
    }

    [TestMethod]
    public void TestNonUnitRayDirectionProducesCorrectDistances()
    {
        // A non-unit-length ray direction (as happens for any scaled torus once its ray is
        // transformed into surface space) must still produce distances that are correct in
        // terms of the ray's own, non-unit parameterization: Origin + distance * Direction
        // must land exactly on the torus surface.
        Torus torus = new () { MajorRadius = 2, MinorRadius = 0.5 };
        Ray ray = new (new Point(-10, 0, 0), new Vector(2, 0, 0));
        List<Intersection> intersections = [];
        double[] expected = [3.75, 4.25, 5.75, 6.25];

        torus.AddIntersections(ray, intersections);

        List<double> actual = intersections.Select(intersection => intersection.Distance)
            .OrderBy(distance => distance)
            .ToList();

        Assert.AreEqual(expected.Length, actual.Count);

        for (int index = 0; index < expected.Length; index++)
        {
            Assert.IsTrue(expected[index].Near(actual[index]));

            // Confirm the point this distance describes is genuinely on the torus surface.
            Point point = ray.At(actual[index]);
            double ringDistance = Math.Sqrt(point.X * point.X + point.Z * point.Z);
            double onSurface = (ringDistance - torus.MajorRadius) * (ringDistance - torus.MajorRadius) +
                                point.Y * point.Y;

            Assert.IsTrue(onSurface.Near(torus.MinorRadius * torus.MinorRadius));
        }
    }

    [TestMethod]
    public void TestScaledTorusMatchesEquivalentUnscaledTorus()
    {
        // Scaling a torus by 2 with radii (1, 0.25) should behave exactly like an unscaled
        // torus with radii (2, 0.5): same world-space intersection distances for the same
        // world-space ray.  This exercises the real path a scale transform takes: the ray
        // gets converted to surface space (via InverseTransform) before AddIntersections()
        // ever sees it, which is where a non-unit direction actually comes from.
        Torus scaledTorus = new ()
        {
            MajorRadius = 1,
            MinorRadius = 0.25,
            Transform = Transforms.Scale(2)
        };
        Ray ray = new (new Point(-10, 0, 0), Directions.Right);
        List<Intersection> intersections = [];
        double[] expected = [7.5, 8.5, 11.5, 12.5];

        scaledTorus.Intersect(ray, intersections);

        List<double> actual = intersections.Select(intersection => intersection.Distance)
            .OrderBy(distance => distance)
            .ToList();

        Assert.AreEqual(expected.Length, actual.Count);

        for (int index = 0; index < expected.Length; index++)
            Assert.IsTrue(expected[index].Near(actual[index]));
    }

    [TestMethod]
    public void TestSurfaceNormalAtKnownPoints()
    {
        Torus torus = new () { MajorRadius = 2, MinorRadius = 0.5 };
        List<(Point Point, Vector Expected)> cases =
        [
            // Outer equator: normal points straight away from the major-radius ring.
            (new Point(2.5, 0, 0), new Vector(0.5, 0, 0)),
            // Inner equator: normal points back toward the hole in the middle.
            (new Point(1.5, 0, 0), new Vector(-0.5, 0, 0)),
            // Top of the tube on the +Z side: normal points straight up.
            (new Point(0, 0.5, 2), new Vector(0, 0.5, 0))
        ];

        foreach ((Point point, Vector expected) in cases)
        {
            Vector normal = torus.SurfaceNormaAt(point, null);

            Assert.IsTrue(expected.Matches(normal));
        }
    }
}
