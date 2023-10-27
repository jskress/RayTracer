using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestCsgSurfaces
{
    private static readonly List<(CsgOperation, bool, bool, bool, bool)> IntersectionAllowedTests = new()
    {
        (CsgOperation.Union, true, true, true, false),
        (CsgOperation.Union, true, true, false, true),
        (CsgOperation.Union, true, false, true, false),
        (CsgOperation.Union, true, false, false, true),
        (CsgOperation.Union, false, true, true, false),
        (CsgOperation.Union, false, true, false, false),
        (CsgOperation.Union, false, false, true, true),
        (CsgOperation.Union, false, false, false, true),

        (CsgOperation.Intersection, true, true, true, true),
        (CsgOperation.Intersection, true, true, false, false),
        (CsgOperation.Intersection, true, false, true, true),
        (CsgOperation.Intersection, true, false, false, false),
        (CsgOperation.Intersection, false, true, true, true),
        (CsgOperation.Intersection, false, true, false, true),
        (CsgOperation.Intersection, false, false, true, false),
        (CsgOperation.Intersection, false, false, false, false),

        (CsgOperation.Difference, true, true, true, false),
        (CsgOperation.Difference, true, true, false, true),
        (CsgOperation.Difference, true, false, true, false),
        (CsgOperation.Difference, true, false, false, true),
        (CsgOperation.Difference, false, true, true, true),
        (CsgOperation.Difference, false, true, false, true),
        (CsgOperation.Difference, false, false, true, false),
        (CsgOperation.Difference, false, false, false, false)
    };
    private static readonly List<(CsgOperation, int, int)> IntersectionFilterTests = new ()
    {
        (CsgOperation.Union, 0, 3),
        (CsgOperation.Intersection, 1, 2),
        (CsgOperation.Difference, 0, 1)
    };

    [TestMethod]
    public void TestConstruction()
    {
        Sphere sphere = new ();
        Cube cube = new ();
        CsgSurface surface = new (CsgOperation.Union)
        {
            Left = sphere,
            Right = cube
        };

        Assert.AreEqual(CsgOperation.Union, surface.Operation);
        Assert.AreSame(sphere, surface.Left);
        Assert.AreSame(cube, surface.Right);
        Assert.AreSame(surface, sphere.Parent);
        Assert.AreSame(surface, cube.Parent);
    }

    [TestMethod]
    public void TestIsIntersectionAllowed()
    {
        foreach ((CsgOperation operation, bool isLeftHit, bool isLeftInside,
                     bool isRightInside, bool expected) in IntersectionAllowedTests)
        {
            CsgSurface surface = new (operation);

            Assert.AreEqual(expected, surface.IsIntersectionAllowed(
                isLeftHit, isLeftInside, isRightInside));
        }
    }

    [TestMethod]
    public void TestFilterIntersections()
    {
        foreach ((CsgOperation operation, int index0, int index1) in IntersectionFilterTests)
        {
            Sphere sphere = new ();
            Cube cube = new ();
            CsgSurface surface = new (operation)
            {
                Left = sphere,
                Right = cube
            };
            List<Intersection> intersections = new ()
            {
                new Intersection(sphere, 1),
                new Intersection(cube, 2),
                new Intersection(sphere, 3),
                new Intersection(cube, 4)
            };
            List<Intersection> original = new (intersections);

            surface.FilterIntersections(intersections);

            Assert.AreEqual(2, intersections.Count);
            Assert.AreSame(original[index0], intersections[0]);
            Assert.AreSame(original[index1], intersections[1]);
        }
    }

    [TestMethod]
    public void TestRayCsgIntersectionMiss()
    {
        CsgSurface surface = new (CsgOperation.Union)
        {
            Left = new Sphere(),
            Right = new Cube()
        };
        Ray ray = new (new Point(0, 2, -5), Directions.In);
        List<Intersection> intersections = new ();

        surface.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayCsgIntersectionHit()
    {
        Sphere s1 = new ();
        Sphere s2 = new ()
        {
            Transform = Transforms.Translate(0, 0, 0.5)
        };
        CsgSurface surface = new (CsgOperation.Union)
        {
            Left = s1,
            Right = s2
        };
        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> intersections = new ();

        surface.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreEqual(4, intersections[0].Distance);
        Assert.AreSame(s1, intersections[0].Surface);
        Assert.AreEqual(6.5, intersections[1].Distance);
        Assert.AreSame(s2, intersections[1].Surface);
    }
}
