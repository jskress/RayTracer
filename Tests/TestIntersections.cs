using RayTracer;
using RayTracer.Basics;
using RayTracer.Core;
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
        List<Intersection> intersections = new () { i1, i2 };

        Assert.AreSame(i1, intersections.Hit());

        i1 = new Intersection(sphere, -1);
        i2 = new Intersection(sphere, 1);
        intersections = new List<Intersection> { i1, i2 };

        Assert.AreSame(i2, intersections.Hit());

        i1 = new Intersection(sphere, -2);
        i2 = new Intersection(sphere, -1);
        intersections = new List<Intersection> { i1, i2 };

        Assert.IsNull(intersections.Hit());

        i1 = new Intersection(sphere, 5);
        i2 = new Intersection(sphere, 7);
        intersections = new List<Intersection> { i1, i2, i3, i4 };

        Assert.AreSame(i4, intersections.Hit());
    }

    [TestMethod]
    public void TestPrepareUsing()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Sphere sphere = new ();
        Intersection intersection = new (sphere, 4);

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

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

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Assert.IsTrue(intersection.OverPoint.Z < tolerance);
        Assert.IsTrue(intersection.Point.Z > intersection.OverPoint.Z);
    }

    [TestMethod]
    public void TestUnderPoint()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Sphere sphere = Sphere.CreateGlassSphere();

        sphere.Transform = Transforms.Translate(0, 0, 1);

        Intersection intersection = new (sphere, 5);
        List<Intersection> intersections = new () { intersection };

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

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Assert.IsTrue(expected.Matches(intersection.Reflect));
    }

    [TestMethod]
    public void TestInside()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Sphere sphere = new ();
        Intersection intersection = new (sphere, 4);

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Assert.IsFalse(intersection.Inside);

        ray = new Ray(new Point(0, 0, 0), new Vector(0, 0, 1));
        intersection = new Intersection(sphere, 1);

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Assert.IsTrue(new Point(0, 0, 1).Matches(intersection.Point));
        Assert.IsTrue(new Vector(0, 0, -1).Matches(intersection.Eye));
        Assert.IsTrue(intersection.Inside);
        Assert.IsTrue(new Vector(0, 0, -1).Matches(intersection.Normal));
    }

    [TestMethod]
    public void TestReflectanceTotalInternalReflection()
    {
        double value = Math.Sqrt(2) / 2;
        Sphere sphere = Sphere.CreateGlassSphere();
        Ray ray = new (new Point(0, 0, value), new Vector(0, 1, 0));
        List<Intersection> intersections = new ()
        {
            new Intersection(sphere, -value),
            new Intersection(sphere, value)
        };

        intersections[1].PrepareUsing(ray, intersections);

        Assert.AreEqual(1, intersections[1].Reflectance);
    }

    [TestMethod]
    public void TestReflectancePerpendicularRay()
    {
        Sphere sphere = Sphere.CreateGlassSphere();
        Ray ray = new (Point.Zero, new Vector(0, 1, 0));
        List<Intersection> intersections = new ()
        {
            new Intersection(sphere, -1),
            new Intersection(sphere, 1)
        };

        intersections[1].PrepareUsing(ray, intersections);

        double reflectance = intersections[1].Reflectance;

        Assert.IsTrue(0.04.Near(reflectance));
    }

    [TestMethod]
    public void TestReflectanceN2GreaterThanN1()
    {
        Sphere sphere = Sphere.CreateGlassSphere();
        Ray ray = new (new Point(0, 0.99, -2), new Vector(0, 0, 1));
        List<Intersection> intersections = new ()
        {
            new Intersection(sphere, 1.8589)
        };

        intersections[0].PrepareUsing(ray, intersections);

        double reflectance = intersections[0].Reflectance;

        Assert.IsTrue(0.48873.Near(reflectance));
    }
}
