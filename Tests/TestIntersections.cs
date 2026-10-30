using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestIntersections
{
    [TestMethod]
    public void TestHit()
    {
        Sphere sphere = new ();
        Intersection i1 = new (sphere, 1);
        Intersection i2 = new (sphere, 2);
        Intersection i3 = new (sphere, -3);
        Intersection i4 = new (sphere, 2);
        List<Intersection> intersections = [i1, i2];

        Assert.AreSame(i1, intersections.Hit());

        i1 = new Intersection(sphere, -1);
        i2 = new Intersection(sphere, 1);
        intersections = [i1, i2];

        Assert.AreSame(i2, intersections.Hit());

        i1 = new Intersection(sphere, -2);
        i2 = new Intersection(sphere, -1);
        intersections = [i1, i2];

        Assert.IsNull(intersections.Hit());

        i1 = new Intersection(sphere, 5);
        i2 = new Intersection(sphere, 7);
        intersections = [i1, i2, i3, i4];

        Assert.AreSame(i4, intersections.Hit());
    }

    [TestMethod]
    public void TestPrepareUsing()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Sphere sphere = new ();
        Intersection intersection = new (sphere, 4);

        intersection.PrepareUsing(ray, [intersection]);

        Assert.AreSame(sphere, intersection.Surface);
        Assert.AreEqual(4, intersection.Distance);
        Assert.IsTrue(new Point(0, 0, -1).Matches(intersection.Point));
        Assert.IsTrue(new Vector(0, 0, -1).Matches(intersection.Eye));
        Assert.IsTrue(new Vector(0, 0, -1).Matches(intersection.Normal));
    }

    [TestMethod]
    public void TestPrepareUsingAcne()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(0, 0, 1)
        };
        Intersection intersection = new (sphere, 5);
        double tolerance = -DoubleExtensions.Epsilon / 2;

        intersection.PrepareUsing(ray, [intersection]);

        Assert.IsTrue(intersection.OverPoint.Z < tolerance);
        Assert.IsTrue(intersection.Point.Z > intersection.OverPoint.Z);
    }

    [TestMethod]
    public void TestUnderPoint()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Sphere sphere = TestSpheres.CreateGlassSphere();

        sphere.Transform = Transforms.Translate(0, 0, 1);

        Intersection intersection = new (sphere, 5);
        List<Intersection> intersections = [intersection];

        intersection.PrepareUsing(ray, intersections);

        Assert.IsTrue(DoubleExtensions.Epsilon / 2 < intersection.UnderPoint.Z);
        Assert.IsTrue(intersection.Point.Z < intersection.UnderPoint.Z);
    }

    [TestMethod]
    public void TestReflectionGeneration()
    {
        double squareRootOf2 = Math.Sqrt(2);
        double value = squareRootOf2 / 2;
        Plane plane = new ();
        Ray ray = new (new Point(0, 1, -1), new Vector(0, -value, value));
        Intersection intersection = new (plane, squareRootOf2);
        Vector expected = new (0, value, value);

        intersection.PrepareUsing(ray, [intersection]);

        Assert.IsTrue(expected.Matches(intersection.Reflect));
    }

    [TestMethod]
    public void TestInside()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Sphere sphere = new ();
        Intersection intersection = new (sphere, 4);

        intersection.PrepareUsing(ray, [intersection]);

        Assert.IsFalse(intersection.Inside);

        ray = new Ray(new Point(0, 0, 0), new Vector(0, 0, 1));
        intersection = new Intersection(sphere, 1);

        intersection.PrepareUsing(ray, [intersection]);

        Assert.IsTrue(new Point(0, 0, 1).Matches(intersection.Point));
        Assert.IsTrue(new Vector(0, 0, -1).Matches(intersection.Eye));
        Assert.IsTrue(intersection.Inside);
        Assert.IsTrue(new Vector(0, 0, -1).Matches(intersection.Normal));
    }

    [TestMethod]
    public void TestReflectanceTotalInternalReflection()
    {
        double value = Math.Sqrt(2) / 2;
        Sphere sphere = TestSpheres.CreateGlassSphere();
        Ray ray = new (new Point(0, 0, value), new Vector(0, 1, 0));
        List<Intersection> intersections =
        [
            new (sphere, -value),
            new (sphere, value)
        ];

        intersections[1].PrepareUsing(ray, intersections);

        Assert.AreEqual(1, intersections[1].Reflectance);
    }

    [TestMethod]
    public void TestReflectancePerpendicularRay()
    {
        Sphere sphere = TestSpheres.CreateGlassSphere();
        Ray ray = new (Point.Zero, new Vector(0, 1, 0));
        List<Intersection> intersections =
        [
            new (sphere, -1),
            new (sphere, 1)
        ];

        intersections[1].PrepareUsing(ray, intersections);

        double reflectance = intersections[1].Reflectance;

        Assert.IsTrue(0.04.Near(reflectance));
    }

    [TestMethod]
    public void TestReflectanceN2GreaterThanN1()
    {
        Sphere sphere = TestSpheres.CreateGlassSphere();
        Ray ray = new (new Point(0, 0.99, -2), new Vector(0, 0, 1));
        List<Intersection> intersections = [new (sphere, 1.8589)];

        intersections[0].PrepareUsing(ray, intersections);

        double reflectance = intersections[0].Reflectance;

        Assert.IsTrue(0.48873.Near(reflectance));
    }

    /// <summary>
    /// This tests that a ray cast from a crossing a long way from the world's middle sets off outside the surface it
    /// left.  A ball the Earth's size, seen from as far off as the Moon sees it and from ten, a hundred and a
    /// thousand times as far: every ray leaving its surface straight out must leave it.  At the Moon's distance the
    /// sphere's own arithmetic put its crossings inside it; further off, the rounding of the places themselves did,
    /// past the millionth a ray sets off by -- a planet in a space scene came out speckled with its own shadow.
    /// </summary>
    [TestMethod]
    public void TestARayFromAFarCrossingLeavesTheSurface()
    {
        Random random = new (2026);
        Point eye = new (0, 23.9, 0);

        foreach (double further in new[] { 1.0, 10, 100, 1000 })
        {
            double radius = 6.371e6 * further;
            Point middle = new (4.8108e7 * further, 2.0788e7 * further, -3.8081e8 * further);
            Sphere planet = new ()
            {
                Transform = Transforms.Translate(middle.X, middle.Y, middle.Z) * Transforms.Scale(radius)
            };
            Vector toward = (middle - eye).Unit;
            Vector side = toward.Cross(new Vector(0, 1, 0)).Unit;
            Vector up = side.Cross(toward).Unit;

            for (int index = 0; index < 100; index++)
            {
                double across = (random.NextDouble() * 2 - 1) * 0.9;
                double down = (random.NextDouble() * 2 - 1) * Math.Sqrt(0.81 - across * across);
                Point target = middle + (side * across + up * down) * radius;
                Ray ray = new (eye, (target - eye).Unit);
                List<Intersection> hits = [];

                planet.Intersect(ray, hits);

                Intersection hit = hits.Where(crossing => crossing.Distance > 0).MinBy(crossing => crossing.Distance);

                Assert.IsNotNull(hit, "a ray aimed at the planet should meet it");

                hit.PrepareUsing(ray, hits);

                List<Intersection> again = [];

                planet.Intersect(new Ray(hit.OverPoint, hit.Normal), again);

                Assert.IsFalse(again.Any(crossing => crossing.Distance > 0),
                    $"{further} times the Moon's distance off, a ray leaving the planet met it again");
            }
        }
    }
}
