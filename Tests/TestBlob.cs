using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestBlob
{
    /// <summary>
    /// A single sphere component is radially symmetric, so it should behave exactly like a
    /// plain Sphere of the isosurface's own radius.  With strength = 1, radius = 2 and
    /// threshold = 0.25, solving strength * (1 - d^2/R^2)^2 = threshold for the smaller
    /// root gives d = R / sqrt(2) = sqrt(2) -- so this blob should intersect identically to
    /// a Sphere scaled by sqrt(2), for the same ray, hit for hit.
    /// </summary>
    [TestMethod]
    public void TestSingleSphereComponentMatchesPlainSphere()
    {
        Blob blob = new ()
        {
            Threshold = 0.25,
            Components = { new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 } }
        };
        Sphere sphere = new () { Transform = Transforms.Scale(Math.Sqrt(2)) };
        Ray ray = new (new Point(5, 1, -3), new Vector(-1, -0.2, 0.4));
        List<Intersection> blobIntersections = [];
        List<Intersection> sphereIntersections = [];

        blob.PrepareForRendering();
        blob.Intersect(ray, blobIntersections);
        sphere.Intersect(ray, sphereIntersections);

        List<double> blobDistances = blobIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();
        List<double> sphereDistances = sphereIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, blobDistances.Count);
        Assert.AreEqual(sphereDistances.Count, blobDistances.Count);

        for (int index = 0; index < blobDistances.Count; index++)
            Assert.IsTrue(sphereDistances[index].Near(blobDistances[index], 0.0001));
    }

    /// <summary>
    /// For the same single-sphere-component blob, the normal at a hit point must point
    /// straight away from the component's center, exactly as a plain sphere's would.
    /// </summary>
    [TestMethod]
    public void TestNormalForSingleSphereComponentIsRadial()
    {
        Blob blob = new ()
        {
            Threshold = 0.25,
            Components = { new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 } }
        };
        Ray ray = new (new Point(-10, 0, 0), Directions.Right);
        List<Intersection> intersections = [];

        blob.PrepareForRendering();
        blob.AddIntersections(ray, intersections);

        Intersection hit = intersections.OrderBy(i => i.Distance).First();
        Point point = ray.At(hit.Distance);
        Vector normal = blob.SurfaceNormaAt(point, hit);
        Vector expected = new Vector(point).Unit;

        Assert.IsTrue(expected.Matches(normal));
    }

    /// <summary>
    /// Two overlapping sphere components should merge into one continuous surface, rather
    /// than behaving like two separate, unrelated spheres.  A ray straight through both
    /// centers should find exactly two crossings (the merged blob's outer boundary on each
    /// side), and the field value at each hit point -- evaluated completely independently
    /// of the intersection code, by summing each component's own density formula by hand --
    /// must equal the threshold, confirming the found distances truly are where the total
    /// field crosses it.
    /// </summary>
    [TestMethod]
    public void TestTwoSphereComponentsMergeSmoothly()
    {
        BlobSphereComponent left = new () { Center = new Point(-1, 0, 0), Radius = 2, Strength = 1 };
        BlobSphereComponent right = new () { Center = new Point(1, 0, 0), Radius = 2, Strength = 1 };
        Blob blob = new ()
        {
            Threshold = 0.25,
            Components = { left, right }
        };
        Ray ray = new (new Point(-10, 0, 0), Directions.Right);
        List<Intersection> intersections = [];

        blob.PrepareForRendering();
        blob.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);

        foreach (double distance in distances)
        {
            Point point = ray.At(distance);
            double leftDistanceSquared = (point - left.Center).Dot(point - left.Center);
            double rightDistanceSquared = (point - right.Center).Dot(point - right.Center);
            double leftDensity = DensityFor(leftDistanceSquared, left.Strength, left.Radius);
            double rightDensity = DensityFor(rightDistanceSquared, right.Strength, right.Radius);

            Assert.IsTrue((leftDensity + rightDensity).Near(blob.Threshold, 0.0001));
        }
    }

    /// <summary>
    /// This is the same density formula the blob primitives use internally
    /// (strength * (1 - d^2/R^2)^2, zero beyond the influence radius), reproduced
    /// independently here so the merge test above isn't just checking the code against
    /// itself.
    /// </summary>
    private static double DensityFor(double distanceSquared, double strength, double radius)
    {
        double radiusSquared = radius * radius;

        if (distanceSquared > radiusSquared)
            return 0;

        double normalized = 1 - distanceSquared / radiusSquared;

        return strength * normalized * normalized;
    }

    /// <summary>
    /// A ray fired perpendicular to a long cylinder component, through its exact midpoint
    /// (far from either cap), should cross the surface using the same math as the sphere
    /// case -- just measured as perpendicular distance to the axis instead of distance to a
    /// point -- so with the same strength/radius/threshold numbers it crosses at the same
    /// sqrt(2) distance.
    /// </summary>
    [TestMethod]
    public void TestCylinderBodyHitPerpendicularToAxis()
    {
        Blob blob = new ()
        {
            Threshold = 0.25,
            Components =
            {
                new BlobCylinderComponent
                {
                    Start = new Point(0, -10, 0), End = new Point(0, 10, 0), Radius = 2, Strength = 1
                }
            }
        };
        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> intersections = [];

        blob.PrepareForRendering();
        blob.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();
        double expectedNear = 5 - Math.Sqrt(2);
        double expectedFar = 5 + Math.Sqrt(2);

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(expectedNear.Near(distances[0]));
        Assert.IsTrue(expectedFar.Near(distances[1]));
    }

    /// <summary>
    /// A ray fired straight down a cylinder component's own axis must pass smoothly through
    /// the body (where the field is constantly at its maximum, strength = 1, well above the
    /// 0.25 threshold, since perpendicular distance to the axis is zero the whole way) and
    /// only cross the surface twice -- once entering through the apex cap's hemisphere,
    /// once exiting through the base cap's -- rather than registering spurious extra
    /// crossings at the body/cap boundaries.  This is the key test that the three
    /// primitives making up one cylinder component are blending into a single seamless
    /// capsule, not three separate bumps.
    /// </summary>
    [TestMethod]
    public void TestCylinderCapsuleAlongAxisHasExactlyTwoCrossings()
    {
        Blob blob = new ()
        {
            Threshold = 0.25,
            Components =
            {
                new BlobCylinderComponent
                {
                    Start = new Point(0, 0, 0), End = new Point(0, 4, 0), Radius = 2, Strength = 1
                }
            }
        };
        Ray ray = new (new Point(0, 10, 0), Directions.Down);
        List<Intersection> intersections = [];

        blob.PrepareForRendering();
        blob.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();
        double expectedEnter = 6 - Math.Sqrt(2);
        double expectedExit = 10 + Math.Sqrt(2);

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(expectedEnter.Near(distances[0]));
        Assert.IsTrue(expectedExit.Near(distances[1]));
    }

    /// <summary>
    /// A ray aimed entirely away from every component must report no intersections.
    /// </summary>
    [TestMethod]
    public void TestMissesEverything()
    {
        Blob blob = new ()
        {
            Threshold = 0.25,
            Components =
            {
                new BlobSphereComponent { Center = Point.Zero, Radius = 2, Strength = 1 },
                new BlobCylinderComponent
                {
                    Start = new Point(0, -2, 0), End = new Point(0, 2, 0), Radius = 1, Strength = 1
                }
            }
        };
        Ray ray = new (new Point(50, 50, 50), Directions.Up);
        List<Intersection> intersections = [];

        blob.PrepareForRendering();
        blob.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }
}
