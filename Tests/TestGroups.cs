using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestGroups
{
    [TestMethod]
    public void TestConstruction()
    {
        Group group = new ();

        Assert.IsTrue(Matrix.Identity.Matches(group.Transform));
        Assert.AreEqual(0, group.Surfaces.Count);
        Assert.IsNull(group.Parent);
    }

    [TestMethod]
    public void TestAddSurface()
    {
        Group group = new ();
        Sphere sphere = new ();

        group.Add(sphere);

        Assert.AreEqual(1, group.Surfaces.Count);
        Assert.AreSame(sphere, group.Surfaces[0]);
        Assert.AreSame(group, sphere.Parent);
    }

    [TestMethod]
    public void TestEmptyGroupIntersection()
    {
        Group group = new ();
        Ray ray = new (Point.Zero, Directions.In);
        List<Intersection> intersections = new ();

        group.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestNonEmptyGroupIntersection()
    {
        Group group = new ();
        Sphere s1 = new ();
        Sphere s2 = new ()
        {
            Transform = Transforms.Translate(0, 0, -3)
        };
        Sphere s3 = new ()
        {
            Transform = Transforms.Translate(5, 0, 0)
        };
        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> intersections = new ();

        group.Add(s1);
        group.Add(s2);
        group.Add(s3);

        group.AddIntersections(ray, intersections);

        Assert.AreEqual(4, intersections.Count);
        Assert.AreSame(s2, intersections[0].Surface);
        Assert.AreSame(s2, intersections[1].Surface);
        Assert.AreSame(s1, intersections[2].Surface);
        Assert.AreSame(s1, intersections[3].Surface);
    }

    [TestMethod]
    public void TestGroupTransforms()
    {
        Group group = new ()
        {
            Transform = Transforms.Scale(2)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0, 0)
        };
        Ray ray = new Ray(new Point(10, 0, -10), Directions.In);
        List<Intersection> intersections = new ();

        group.Add(sphere);
        group.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
    }

    [TestMethod]
    public void TestWorldToSurface()
    {
        Group outer = new ()
        {
            Transform = Transforms.RotateAroundY(Math.PI / 2, true)
        };
        Group inner = new ()
        {
            Transform = Transforms.Scale(2)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0,0)
        };

        outer.Add(inner);
        inner.Add(sphere);

        Point point = sphere.WorldToSurface(new Point(-2, 0, -10));
        Point expected = new (0, 0, -1);

        Assert.IsTrue(expected.Matches(point));
    }

    [TestMethod]
    public void TestNormalToWorld()
    {
        Group outer = new ()
        {
            Transform = Transforms.RotateAroundY(Math.PI / 2, true)
        };
        Group inner = new ()
        {
            Transform = Transforms.Scale(1, 2, 3)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0,0)
        };

        outer.Add(inner);
        inner.Add(sphere);

        double value = Math.Sqrt(3) / 3;
        Vector normal = sphere.NormalToWorld(new Vector(value, value, value));
        Vector expected = new (0.28571, 0.42857, -0.85714);

        Assert.IsTrue(expected.Matches(normal));
    }

    [TestMethod]
    public void TestGroupNormalAt()
    {
        Group outer = new ()
        {
            Transform = Transforms.RotateAroundY(Math.PI / 2, true)
        };
        Group inner = new ()
        {
            Transform = Transforms.Scale(1, 2, 3)
        };
        Sphere sphere = new ()
        {
            Transform = Transforms.Translate(5, 0,0)
        };

        outer.Add(inner);
        inner.Add(sphere);

        Vector normal = sphere.NormaAt(new Point(1.7321, 1.1547, -5.5774), null);
        Vector expected = new (0.28570, 0.42854, -0.85716);

        Assert.IsTrue(expected.Matches(normal));
    }
}
