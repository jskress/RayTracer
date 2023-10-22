using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestSpheres
{
    [TestMethod]
    public void TestConstruction()
    {
        Sphere sphere = new ();
        Matrix transform = Transforms.Translate(2, 3, 4);

        Assert.AreSame(Matrix.Identity, sphere.Transform);

        sphere.Transform = transform;

        Assert.AreSame(transform, sphere.Transform);
    }

    [TestMethod]
    public void TestIntersection()
    {
        Point origin = new (0, 0, -5);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        Sphere sphere = new ();
        List<Intersection> intersections = new ();

        sphere.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreSame(sphere, intersections[0].Surface);
        Assert.AreEqual(4.0, intersections[0].Distance);
        Assert.AreSame(sphere, intersections[1].Surface);
        Assert.AreEqual(6.0, intersections[1].Distance);

        origin = new Point(0, 1, -5);
        ray = new Ray(origin, direction);

        intersections.Clear();
        sphere.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreSame(sphere, intersections[0].Surface);
        Assert.AreEqual(5.0, intersections[0].Distance);
        Assert.AreSame(sphere, intersections[1].Surface);
        Assert.AreEqual(5.0, intersections[1].Distance);

        origin = new Point(0, 2, -5);
        ray = new Ray(origin, direction);

        intersections.Clear();
        sphere.Intersect(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestInside()
    {
        Point origin = new (0, 0, 0);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        Sphere sphere = new ();
        List<Intersection> intersections = new ();

        sphere.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreSame(sphere, intersections[0].Surface);
        Assert.AreEqual(-1.0, intersections[0].Distance);
        Assert.AreSame(sphere, intersections[1].Surface);
        Assert.AreEqual(1.0, intersections[1].Distance);
    }

    [TestMethod]
    public void TestBehind()
    {
        Point origin = new (0, 0, 5);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        Sphere sphere = new ();
        List<Intersection> intersections = new ();

        sphere.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreSame(sphere, intersections[0].Surface);
        Assert.AreEqual(-6.0, intersections[0].Distance);
        Assert.AreSame(sphere, intersections[1].Surface);
        Assert.AreEqual(-4.0, intersections[1].Distance);
    }

    [TestMethod]
    public void TestScaledIntersection()
    {
        Point origin = new (0, 0, -5);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        Matrix transform = Transforms.Scale(2);
        Sphere sphere = new () { Transform = transform };
        List<Intersection> intersections = new ();

        sphere.Intersect(ray, intersections);

        Assert.AreEqual(2, intersections.Count);
        Assert.AreEqual(3, intersections[0].Distance);
        Assert.AreEqual(7, intersections[1].Distance);
    }

    [TestMethod]
    public void TestTranslatedIntersection()
    {
        Point origin = new (0, 0, -5);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        Matrix transform = Transforms.Translate(5, 0, 0);
        Sphere sphere = new () { Transform = transform };
        List<Intersection> intersections = new ();

        sphere.Intersect(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestNormals()
    {
        Sphere sphere = new ();
        Point point = new (1, 0, 0);
        Vector vector = new (1, 0, 0);
        double value = Math.Sqrt(3) / 3;

        Assert.IsTrue(vector.Matches(sphere.NormaAt(point)));

        point = new Point(0, 1, 0);
        vector = new Vector(0, 1, 0);

        Assert.IsTrue(vector.Matches(sphere.NormaAt(point)));

        point = new Point(0, 0, 1);
        vector = new Vector(0, 0, 1);

        Assert.IsTrue(vector.Matches(sphere.NormaAt(point)));

        point = new Point(value, value, value);
        vector = new Vector(value, value, value);

        Assert.IsTrue(vector.Matches(sphere.NormaAt(point)));
        Assert.IsTrue(vector.Matches(vector.Unit));
    }

    [TestMethod]
    public void TestTransformedNormals()
    {
        Sphere sphere = new () { Transform = Transforms.Translate(0, 1, 0) };
        Point point = new (0, 1.70711, -0.70711);
        Vector vector = new (0, 0.70711, -0.70711);
        Vector normal = sphere.NormaAt(point);
        double value = Math.Sqrt(2) / 2;

        Assert.IsTrue(vector.Matches(normal));

        sphere = new Sphere
        {
            Transform = Transforms.Scale(1, 0.5, 1) *
                        Transforms.RotateAroundZ(Math.PI / 5)
        };
        point = new Point(0, value, -value);
        normal = sphere.NormaAt(point);
        vector = new Vector(0, 0.97014, -0.24254);

        Assert.IsTrue(vector.Matches(normal));
    }

    [TestMethod]
    public void TestMaterial()
    {
        Sphere sphere = new ();
        Material material = new ();

        Assert.IsNotNull(sphere.Material);
        Assert.IsTrue(material.Matches(sphere.Material));

        material = new Material { Ambient = 1 };

        sphere.Material = material;

        Assert.AreSame(material, sphere.Material);
    }

    [TestMethod]
    public void TestGlassSphere()
    {
        Sphere glass = Sphere.CreateGlassSphere();

        Assert.AreSame(Matrix.Identity, glass.Transform);
        Assert.AreEqual(1, glass.Material.Transparency);
        Assert.AreEqual(1.5, glass.Material.IndexOfRefraction);
    }

    private record N1N2(double N1, double N2);

    [TestMethod]
    public void TestRefractionN1N2()
    {
        Sphere a = Sphere.CreateGlassSphere();
        Sphere b = Sphere.CreateGlassSphere();
        Sphere c = Sphere.CreateGlassSphere();
        Point origin = new (0, 0, -4);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        List<Intersection> intersections = new ()
        {
            new Intersection(a, 2),
            new Intersection(b, 2.75),
            new Intersection(c, 3.25),
            new Intersection(b, 4.75),
            new Intersection(c, 5.25),
            new Intersection(a, 6)
        };

        a.Transform = Transforms.Scale(2);
        a.Material.IndexOfRefraction = 1.5;
        b.Transform = Transforms.Translate(0, 0, -0.25);
        b.Material.IndexOfRefraction = 2.0;
        c.Transform = Transforms.Translate(0, 0, 0.25);
        c.Material.IndexOfRefraction = 2.5;

        N1N2[] expected =
        {
            new (1.0, 1.5),
            new (1.5, 2.0),
            new (2.0, 2.5),
            new (2.5, 2.5),
            new (2.5, 1.5),
            new (1.5, 1.0)
        };

        for (int index = 0; index < expected.Length; index++)
        {
            intersections[index].PrepareUsing(ray, intersections);

            Assert.AreEqual(expected[index].N1, intersections[index].N1);
            Assert.AreEqual(expected[index].N2, intersections[index].N2);
        }
    }
}
