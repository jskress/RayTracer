using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestScenes
{
    /// <summary>
    /// This method generates a default scene with one light and two spheres.
    /// </summary>
    /// <returns>A default scene.</returns>
    public static Scene DefaultScene()
    {
        PointLight pointLight = new()
        {
            Location = new Point(-10, 10, -10)
        };
        Sphere outer = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(new Color(0.8, 1.0, 0.6)),
                Diffuse = 0.7,
                Specular = 0.2
            }
        };
        Sphere inner = new ()
        {
            Transform = Transforms.Scale(0.5)
        };
        Scene scene = new ();

        scene.Lights.Add(pointLight);
        scene.Surfaces.Add(outer);
        scene.Surfaces.Add(inner);

        return scene;
    }

    [TestMethod]
    public void TestConstruction()
    {
        Scene scene = new ();

        Assert.AreEqual(0, scene.Lights.Count);
        Assert.AreEqual(0, scene.Surfaces.Count);

        scene = DefaultScene();

        Assert.AreEqual(1, scene.Lights.Count);
        Assert.AreEqual(2, scene.Surfaces.Count);
    }

    [TestMethod]
    public void TestIntersections()
    {
        Scene scene = DefaultScene();
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        List<Intersection> intersections = scene.Intersect(ray);

        Assert.AreEqual(4, intersections.Count);
        Assert.AreEqual(4, intersections[0].Distance);
        Assert.AreEqual(4.5, intersections[1].Distance);
        Assert.AreEqual(5.5, intersections[2].Distance);
        Assert.AreEqual(6, intersections[3].Distance);
    }

    [TestMethod]
    public void TestGetHitColorOutside()
    {
        Scene scene = DefaultScene();
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Surface surface = scene.Surfaces[0];
        Intersection intersection = new (surface, 4);

        intersection.PrepareUsing(ray, [intersection]);

        Color color = scene.GetHitColor(intersection, 0);
        Color expected = new (0.380661, 0.475826, 0.285496);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetHitColorInside()
    {
        Scene scene = DefaultScene();
        scene.Lights[0] = new PointLight
        {
            Location = new Point(0, 0.25, 0)
        };
        Ray ray = new (new Point(0, 0, 0), new Vector(0, 0, 1));
        Surface surface = scene.Surfaces[1];
        Intersection intersection = new (surface, 0.5);

        intersection.PrepareUsing(ray, [intersection]);

        Color color = scene.GetHitColor(intersection, 0);
        Color expected = new (0.1, 0.1, 0.1);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetHitColorInShadow()
    {
        Scene scene = new ();
        scene.Lights.Add(new PointLight
        {
            Location = new Point(0, 0, -10)
        });
        scene.Surfaces.Add(new Sphere());
        scene.Surfaces.Add(new Sphere
        {
            Transform = Transforms.Translate(0, 0, 10)
        });
        Ray ray = new (new Point(0, 0, 5), new Vector(0, 0, 1));
        Intersection intersection = new (scene.Surfaces[1], 4);

        intersection.PrepareUsing(ray, [intersection]);

        Color color = scene.GetHitColor(intersection, 0);
        Color expected = new (0.1, 0.1, 0.1);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetColorFor()
    {
        Scene scene = DefaultScene();
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 1, 0));

        Assert.IsTrue(Colors.Transparent.Matches(scene.GetColorFor(ray)));

        ray = new Ray(new Point(0, 0, -5), new Vector(0, 0, 1));

        Color expected = new (0.380661, 0.475826, 0.285496);
        Color color = scene.GetColorFor(ray);

        Assert.IsTrue(expected.Matches(color));

        scene.Surfaces[0].Material.Ambient = 1;
        scene.Surfaces[1].Material.Ambient = 1;
        ray = new Ray(new Point(0, 0, 0.75), new Vector(0, 0, -1));
        expected = scene.Surfaces[1].Material.Pigment.GetColorFor(new Sphere(), ray.Origin);

        Assert.IsTrue(expected.Matches(scene.GetColorFor(ray)));
    }

    [TestMethod]
    public void TestIsInShadow()
    {
        Scene scene = DefaultScene();
        Point point = new (0, 10, 0);

        Assert.IsFalse(scene.IsInShadow(scene.Lights[0], point));

        point = new Point(10, -10, 10);

        Assert.IsTrue(scene.IsInShadow(scene.Lights[0], point));

        point = new Point(-20, 20, -20);

        Assert.IsFalse(scene.IsInShadow(scene.Lights[0], point));

        point = new Point(-2, 2, -2);

        Assert.IsFalse(scene.IsInShadow(scene.Lights[0], point));
    }

    [TestMethod]
    public void TestGetReflectionColorNoReflection()
    {
        Scene scene = DefaultScene();
        Ray ray = new (Point.Zero, new Vector(0, 0, 1));
        Surface surface = scene.Surfaces[1];

        surface.Material.Ambient = 1;

        Intersection intersection = new (surface, 1);

        intersection.PrepareUsing(ray, [intersection]);

        Color color = scene.GetReflectionColor(intersection, 1);

        Assert.IsTrue(Colors.Black.Matches(color));
    }

    [TestMethod]
    public void TestGetReflectionColorWithReflection()
    {
        double squareRootOf2 = Math.Sqrt(2);
        double value = squareRootOf2 / 2;
        Scene scene = DefaultScene();
        Plane plane = new ()
        {
            Material = new Material
            {
                Reflective = 0.5
            },
            Transform = Transforms.Translate(0, -1, 0)
        };
        Ray ray = new (new Point(0, 0, -3), new Vector(0, -value, value));

        scene.Surfaces.Add(plane);

        Intersection intersection = new (plane, squareRootOf2);

        intersection.PrepareUsing(ray, [intersection]);

        Color color = scene.GetReflectionColor(intersection, 1);
        Color expected = new (0.190330, 0.237913, 0.142748);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetRefractionColorOpaque()
    {
        Scene scene = DefaultScene();
        Surface surface = scene.Surfaces[0];
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        List<Intersection> intersections =
        [
            new Intersection(surface, 4),
            new Intersection(surface, 6)
        ];

        intersections[0].PrepareUsing(ray, intersections);

        Color color = scene.GetRefractedColor(intersections[0], 5);

        Assert.IsTrue(Colors.Black.Matches(color));

        surface.Material.Transparency = 1;
        surface.Material.IndexOfRefraction = 1.5;
        intersections =
        [
            new Intersection(surface, 4),
            new Intersection(surface, 6)
        ];

        intersections[0].PrepareUsing(ray, intersections);

        color = scene.GetRefractedColor(intersections[0], 0);

        Assert.IsTrue(Colors.Black.Matches(color));
    }

    [TestMethod]
    public void TestTotalInternalReflection()
    {
        double value = Math.Sqrt(2) / 2;
        Scene scene = DefaultScene();
        Surface surface = scene.Surfaces[0];
        Ray ray = new (new Point(0, 0, value), new Vector(0, 1, 0));

        surface.Material.Transparency = 1;
        surface.Material.IndexOfRefraction = 1.5;

        List<Intersection> intersections =
        [
            new Intersection(surface, -value),
            new Intersection(surface, value)
        ];

        intersections[1].PrepareUsing(ray, intersections);

        Color color = scene.GetRefractedColor(intersections[1], 5);

        Assert.IsTrue(Colors.Black.Matches(color));
    }

    [TestMethod]
    public void TestGetRefractedColor()
    {
        Scene scene = DefaultScene();
        Surface a = scene.Surfaces[0];
        Surface b = scene.Surfaces[1];
        TestPigment pigment = new ();
        Ray ray = new (new Point(0, 0, 0.1), new Vector(0, 1, 0));

        a.Material.Ambient = 1;
        a.Material.Pigment = pigment;
        b.Material.Transparency = 1;
        b.Material.IndexOfRefraction = 1.5;

        List<Intersection> intersections =
        [
            new Intersection(a, -0.9899),
            new Intersection(b, -0.4899),
            new Intersection(b, 0.4899),
            new Intersection(a, 0.9899)
        ];

        intersections[2].PrepareUsing(ray, intersections);

        Color color = scene.GetRefractedColor(intersections[2], 5);
        Color expected = new (0, 0.998886, 0.047217);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetHitColorWithRefraction()
    {
        double value = Math.Sqrt(2) / 2;
        Scene scene = DefaultScene();
        Plane floor = new ()
        {
            Material = new Material
            {
                Transparency = 0.5,
                IndexOfRefraction = 1.5
            },
            Transform = Transforms.Translate(0, -1, 0)
        };
        Sphere ball = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(new Color(1, 0, 0)),
                Ambient = 0.5
            },
            Transform = Transforms.Translate(0, -3.5, -0.5)
        };
        Ray ray = new (new Point(0, 0, -3), new Vector(0, -value, value));
        List<Intersection> intersections = [new Intersection(floor, value * 2)];

        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ball);

        intersections[0].PrepareUsing(ray, intersections);

        Color color = scene.GetHitColor(intersections[0], 5);
        Color expected = new (0.936425, 0.686425, 0.686425);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetHitColorReflectance()
    {
        double value = Math.Sqrt(2) / 2;
        Scene scene = DefaultScene();
        Plane floor = new ()
        {
            Material = new Material
            {
                Reflective = 0.5,
                Transparency = 0.5,
                IndexOfRefraction = 1.5
            },
            Transform = Transforms.Translate(0, -1, 0)
        };
        Sphere ball = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(new Color(1, 0, 0)),
                Ambient = 0.5
            },
            Transform = Transforms.Translate(0, -3.5, -0.5)
        };
        Ray ray = new (new Point(0, 0, -3), new Vector(0, -value, value));
        List<Intersection> intersections = [new Intersection(floor, value * 2)];

        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ball);

        intersections[0].PrepareUsing(ray, intersections);

        Color color = scene.GetHitColor(intersections[0], 5);
        Color expected = new (0.933915, 0.696434, 0.692430);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestApplyPhongForReflection()
    {
        double squareRootOf2 = Math.Sqrt(2);
        double value = squareRootOf2 / 2;
        Scene scene = DefaultScene();
        Plane plane = new ()
        {
            Material = new Material
            {
                Reflective = 0.5
            },
            Transform = Transforms.Translate(0, -1, 0)
        };
        Ray ray = new (new Point(0, 0, -3), new Vector(0, -value, value));

        scene.Surfaces.Add(plane);

        Intersection intersection = new (plane, squareRootOf2);

        intersection.PrepareUsing(ray, [intersection]);

        Color color = scene.GetHitColor(intersection, 1);
        Color expected = new (0.876756, 0.924339, 0.829173);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestReflectionRemaining()
    {
        double squareRootOf2 = Math.Sqrt(2);
        double value = squareRootOf2 / 2;
        Scene scene = DefaultScene();
        Plane plane = new ()
        {
            Material = new Material
            {
                Reflective = 0.5
            },
            Transform = Transforms.Translate(0, -1, 0)
        };
        Ray ray = new (new Point(0, 0, -3), new Vector(0, -value, value));

        scene.Surfaces.Add(plane);

        Intersection intersection = new (plane, squareRootOf2);

        intersection.PrepareUsing(ray, [intersection]);

        Color color = scene.GetReflectionColor(intersection, 0);

        Assert.IsTrue(Colors.Black.Matches(color));
    }

    [TestMethod]
    public void TestInfiniteReflectionGuard()
    {
        Scene scene = new ();
        PointLight light = new ()
        {
            Location = Point.Zero
        };
        Plane lower = new ()
        {
            Material = new Material
            {
                Reflective = 1
            },
            Transform = Transforms.Translate(0, -1, 0)
        };
        Plane upper = new ()
        {
            Material = new Material
            {
                Reflective = 1
            },
            Transform = Transforms.Translate(0, 1, 0)
        };
        Ray ray = new (Point.Zero, new Vector(0, 1, 0));

        scene.Lights.Add(light);
        scene.Surfaces.Add(lower);
        scene.Surfaces.Add(upper);

        // This should just return and not loop infinitely.
        _ = scene.GetColorFor(ray);
    }
}
