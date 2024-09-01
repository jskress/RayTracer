using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;

namespace Tests;

[TestClass]
public class TestPointLights
{
    [TestMethod]
    public void TestConstruction()
    {
        PointLight pointLight = new ();

        Assert.AreSame(Point.Zero, pointLight.Location);
        Assert.AreSame(Colors.White, pointLight.Color);
    }

    [TestMethod]
    public void TestStraightOnLighting()
    {
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);
        Sphere sphere = new ();
        PointLight light = new ()
        {
            Location = new Point(0, 0, -10)
        };
        Color expected = new (1.9, 1.9, 1.9);

        Assert.IsTrue(expected.Matches(light.ApplyPhong(Point.Zero, eye, normal, sphere, false)));
    }

    [TestMethod]
    public void Test45AngleEyeLighting()
    {
        double value = Math.Sqrt(2) / 2;
        Vector eye = new (0, value, -value);
        Vector normal = new (0, 0, -1);
        Sphere sphere = new ();
        PointLight light = new ()
        {
            Location = new Point(0, 0, -10)
        };
        Color expected = new (1.0, 1.0, 1.0);

        Assert.IsTrue(expected.Matches(light.ApplyPhong(Point.Zero, eye, normal, sphere, false)));
    }

    [TestMethod]
    public void Test45AngleLightLighting()
    {
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);
        Sphere sphere = new ();
        PointLight light = new ()
        {
            Location = new Point(0, 10, -10)
        };
        Color expected = new (0.736396, 0.736396, 0.736396);

        Assert.IsTrue(expected.Matches(light.ApplyPhong(Point.Zero, eye, normal, sphere, false)));
    }

    [TestMethod]
    public void Test45AngleReflectionLighting()
    {
        double value = Math.Sqrt(2) / 2;
        Vector eye = new (0, -value, -value);
        Vector normal = new (0, 0, -1);
        Sphere sphere = new ();
        PointLight light = new ()
        {
            Location = new Point(0, 10, -10)
        };
        Color expected = new (1.636396, 1.636396, 1.636396);

        Assert.IsTrue(expected.Matches(light.ApplyPhong(Point.Zero, eye, normal, sphere, false)));
    }

    [TestMethod]
    public void TestBehindLighting()
    {
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);
        Sphere sphere = new ();
        PointLight light = new ()
        {
            Location = new Point(0, 0, 10)
        };
        Color expected = new (0.1, 0.1, 0.1);

        Assert.IsTrue(expected.Matches(light.ApplyPhong(Point.Zero, eye, normal, sphere, false)));
    }

    [TestMethod]
    public void TestShadows()
    {
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);
        Sphere sphere = new ();
        PointLight light = new ()
        {
            Location = new Point(0, 0, -10)
        };
        Color expected = new (0.1, 0.1, 0.1);

        Assert.IsTrue(expected.Matches(light.ApplyPhong(Point.Zero, eye, normal, sphere, true)));
    }

    [TestMethod]
    public void TestStripes()
    {
        Material material = new ()
        {
            Pigment = TestPatterns.CreateStripedPigment(BandType.LinearX),
            Ambient = 1,
            Diffuse = 0,
            Specular = 0
        };
        Sphere sphere = new()
        {
            Material = material
        };
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);
        PointLight light = new ()
        {
            Location = new Point(0, 0, -10)
        };
        Color c1 = light.ApplyPhong(
            new Point(0.9, 0, 0), eye, normal, sphere, false
        );
        Color c2 = light.ApplyPhong(
            new Point(1.1, 0, 0), eye, normal, sphere, false
        );

        Assert.IsTrue(Colors.White.Matches(c1));
        Assert.IsTrue(Colors.Black.Matches(c2));
    }
}
