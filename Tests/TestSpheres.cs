using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestSpheres
{
    /// <summary>
    /// This is a helper method for creating a glass sphere.
    /// </summary>
    /// <returns>A new glass sphere.</returns>
    public static Sphere CreateGlassSphere()
    {
        return new Sphere
        {
            Material = new Material
            {
                Transparency = 1,
                Interior = new Interior { IndexOfRefraction = 1.5 }
            }
        };
    }

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
        Ray ray = new (origin, Directions.In);
        Sphere sphere = new ();
        List<Intersection> intersections = [];

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
        List<Intersection> intersections = [];

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
        List<Intersection> intersections = [];

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
        List<Intersection> intersections = [];

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
        List<Intersection> intersections = [];

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

        Assert.IsTrue(vector.Matches(sphere.NormalAt(point, null)));

        point = new Point(0, 1, 0);
        vector = new Vector(0, 1, 0);

        Assert.IsTrue(vector.Matches(sphere.NormalAt(point, null)));

        point = new Point(0, 0, 1);
        vector = new Vector(0, 0, 1);

        Assert.IsTrue(vector.Matches(sphere.NormalAt(point, null)));

        point = new Point(value, value, value);
        vector = new Vector(value, value, value);

        Assert.IsTrue(vector.Matches(sphere.NormalAt(point, null)));
        Assert.IsTrue(vector.Matches(vector.Unit));
    }

    [TestMethod]
    public void TestTransformedNormals()
    {
        Sphere sphere = new () { Transform = Transforms.Translate(0, 1, 0) };
        Point point = new (0, 1.70711, -0.70711);
        Vector vector = new (0, 0.707107, -0.707107);
        Vector normal = sphere.NormalAt(point, null);
        double value = Math.Sqrt(2) / 2;

        Assert.IsTrue(vector.Matches(normal));

        sphere = new Sphere
        {
            Transform = Transforms.Scale(1, 0.5, 1) *
                        Transforms.RotateAroundZ(Math.PI / 5)
        };
        point = new Point(0, value, -value);
        normal = sphere.NormalAt(point, null);
        vector = new Vector(0, 0.970143, -0.242536);

        Assert.IsTrue(vector.Matches(normal));
    }

    [TestMethod]
    public void TestMaterial()
    {
        Sphere sphere = new ();

        Assert.IsNull(sphere.Material);

        Material material = new Material { Ambient = 1 };

        sphere.Material = material;

        Assert.AreSame(material, sphere.Material);
    }

    [TestMethod]
    public void TestGlassSphere()
    {
        Sphere glass = CreateGlassSphere();

        Assert.AreSame(Matrix.Identity, glass.Transform);
        Assert.AreEqual(1, glass.Material.Transparency);
        Assert.AreEqual(1.5, glass.Material.Interior.IndexOfRefraction);
    }

    private record N1N2(double N1, double N2);

    [TestMethod]
    public void TestRefractionN1N2()
    {
        Sphere a = CreateGlassSphere();
        Sphere b = CreateGlassSphere();
        Sphere c = CreateGlassSphere();
        Point origin = new (0, 0, -4);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        List<Intersection> intersections =
        [
            new (a, 2),
            new (b, 2.75),
            new (c, 3.25),
            new (b, 4.75),
            new (c, 5.25),
            new (a, 6)
        ];

        a.Transform = Transforms.Scale(2);
        a.Material.Interior.IndexOfRefraction = 1.5;
        b.Transform = Transforms.Translate(0, 0, -0.25);
        b.Material.Interior.IndexOfRefraction = 2.0;
        c.Transform = Transforms.Translate(0, 0, 0.25);
        c.Material.Interior.IndexOfRefraction = 2.5;

        N1N2[] expected =
        [
            new (1.0, 1.5),
            new (1.5, 2.0),
            new (2.0, 2.5),
            new (2.5, 2.5),
            new (2.5, 1.5),
            new (1.5, 1.0)
        ];

        for (int index = 0; index < expected.Length; index++)
        {
            intersections[index].PrepareUsing(ray, intersections);

            Assert.AreEqual(expected[index].N1, intersections[index].N1);
            Assert.AreEqual(expected[index].N2, intersections[index].N2);
        }
    }

    /// <summary>
    /// This tests that a sphere seen from far off is met on its surface.  From a hundred thousand radii away, at
    /// places all over the side facing the ray, every first crossing must lie on the ball: the book's formula
    /// subtracted two numbers each the square of that distance, and put them a few millionths of a radius off it --
    /// enough, for a planet seen from its moon, to start every other shadow ray inside the planet.
    /// </summary>
    [TestMethod]
    public void TestASphereSeenFromFarOffIsMetOnItsSurface()
    {
        Sphere sphere = new ();
        Random random = new (1999);

        for (int index = 0; index < 200; index++)
        {
            Vector away = RandomUnit(random);
            Point origin = new (away.X * 1e5, away.Y * 1e5, away.Z * 1e5);
            Vector onIt = RandomUnit(random);

            if (onIt.Dot(away) < 0.2)
                onIt = (onIt + away * (0.2 - onIt.Dot(away) + 0.1)).Unit;

            Ray ray = new (origin, (new Point(onIt.X, onIt.Y, onIt.Z) - origin).Unit);
            List<Intersection> hits = [];

            sphere.Intersect(ray, hits);

            Intersection hit = hits.Where(crossing => crossing.Distance > 0).MinBy(crossing => crossing.Distance);

            Assert.IsNotNull(hit, "a ray aimed at the ball should meet it");

            double off = (ray.At(hit.Distance) - Point.Zero).Magnitude - 1;

            Assert.AreEqual(0, off, 1e-9, $"a crossing seen from far off lay {off:E2} off the ball");
        }
    }

    /// <summary>
    /// This method returns a direction picked evenly over the sphere of directions.
    /// </summary>
    private static Vector RandomUnit(Random random)
    {
        double z = random.NextDouble() * 2 - 1;
        double angle = random.NextDouble() * Math.PI * 2;
        double across = Math.Sqrt(1 - z * z);

        return new Vector(across * Math.Cos(angle), across * Math.Sin(angle), z);
    }
}
