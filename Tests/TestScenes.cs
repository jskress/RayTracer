using RayTracer.Basics;
using RayTracer.ColorSources;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestScenes
{
    [TestMethod]
    public void TestConstruction()
    {
        Scene scene = new ();

        Assert.AreEqual(0, scene.Lights.Count);
        Assert.AreEqual(0, scene.Surfaces.Count);

        scene = Scene.DefaultScene();

        Assert.AreEqual(1, scene.Lights.Count);
        Assert.AreEqual(2, scene.Surfaces.Count);
    }

    [TestMethod]
    public void TestIntersections()
    {
        Scene scene = Scene.DefaultScene();
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
        Scene scene = Scene.DefaultScene();
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Surface surface = scene.Surfaces[0];
        Intersection intersection = new (surface, 4);

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Color color = scene.GetHitColor(intersection, 0);
        Color expected = new (0.38066, 0.47583, 0.2855);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetHitColorInside()
    {
        Scene scene = Scene.DefaultScene();
        scene.Lights[0] = new PointLight
        {
            Location = new Point(0, 0.25, 0)
        };
        Ray ray = new (new Point(0, 0, 0), new Vector(0, 0, 1));
        Surface surface = scene.Surfaces[1];
        Intersection intersection = new (surface, 0.5);

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

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

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Color color = scene.GetHitColor(intersection, 0);
        Color expected = new (0.1, 0.1, 0.1);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetColorFor()
    {
        Scene scene = Scene.DefaultScene();
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 1, 0));

        Assert.IsTrue(Colors.Transparent.Matches(scene.GetColorFor(ray)));

        ray = new Ray(new Point(0, 0, -5), new Vector(0, 0, 1));

        Color expected = new (0.38066, 0.47583, 0.2855);

        Assert.IsTrue(expected.Matches(scene.GetColorFor(ray)));

        scene.Surfaces[0].Material.Ambient = 1;
        scene.Surfaces[1].Material.Ambient = 1;
        ray = new Ray(new Point(0, 0, 0.75), new Vector(0, 0, -1));
        expected = scene.Surfaces[1].Material.ColorSource.GetColorFor(new Sphere(), ray.Origin);

        Assert.IsTrue(expected.Matches(scene.GetColorFor(ray)));
    }

    [TestMethod]
    public void TestIsInShadow()
    {
        Scene scene = Scene.DefaultScene();
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
        Scene scene = Scene.DefaultScene();
        Ray ray = new (Point.Zero, new Vector(0, 0, 1));
        Surface surface = scene.Surfaces[1];

        surface.Material.Ambient = 1;

        Intersection intersection = new (surface, 1);

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Color color = scene.GetReflectionColor(intersection, 1);

        Assert.IsTrue(Colors.Black.Matches(color));
    }

    [TestMethod]
    public void TestGetReflectionColorWithReflection()
    {
        double squareRootOf2 = Math.Sqrt(2);
        double value = squareRootOf2 / 2;
        Scene scene = Scene.DefaultScene();
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

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Color color = scene.GetReflectionColor(intersection, 1);
        Color expected = new (0.19033, 0.23791, 0.14274);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetRefractionColorOpaque()
    {
        Scene scene = Scene.DefaultScene();
        Surface surface = scene.Surfaces[0];
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        List<Intersection> intersections = new ()
        {
            new Intersection(surface, 4),
            new Intersection(surface, 6)
        };

        intersections[0].PrepareUsing(ray, intersections);

        Color color = scene.GetRefractedColor(intersections[0], 5);

        Assert.IsTrue(Colors.Black.Matches(color));

        surface.Material.Transparency = 1;
        surface.Material.IndexOfRefraction = 1.5;
        intersections = new List<Intersection>
        {
            new (surface, 4),
            new (surface, 6)
        };

        intersections[0].PrepareUsing(ray, intersections);

        color = scene.GetRefractedColor(intersections[0], 0);

        Assert.IsTrue(Colors.Black.Matches(color));
    }

    [TestMethod]
    public void TestTotalInternalReflection()
    {
        double value = Math.Sqrt(2) / 2;
        Scene scene = Scene.DefaultScene();
        Surface surface = scene.Surfaces[0];
        Ray ray = new (new Point(0, 0, value), new Vector(0, 1, 0));

        surface.Material.Transparency = 1;
        surface.Material.IndexOfRefraction = 1.5;

        List<Intersection> intersections = new ()
        {
            new Intersection(surface, -value),
            new Intersection(surface, value)
        };

        intersections[1].PrepareUsing(ray, intersections);

        Color color = scene.GetRefractedColor(intersections[1], 5);

        Assert.IsTrue(Colors.Black.Matches(color));
    }

    [TestMethod]
    public void TestGetRefractedColor()
    {
        Scene scene = Scene.DefaultScene();
        Surface a = scene.Surfaces[0];
        Surface b = scene.Surfaces[1];
        TestColorSource colorSource = new ();
        Ray ray = new (new Point(0, 0, 0.1), new Vector(0, 1, 0));

        a.Material.Ambient = 1;
        a.Material.ColorSource = colorSource;
        b.Material.Transparency = 1;
        b.Material.IndexOfRefraction = 1.5;

        List<Intersection> intersections = new ()
        {
            new Intersection(a, -0.9899),
            new Intersection(b, -0.4899),
            new Intersection(b, 0.4899),
            new Intersection(a, 0.9899)
        };

        intersections[2].PrepareUsing(ray, intersections);

        Color color = scene.GetRefractedColor(intersections[2], 5);
        Color expected = new (0, 0.99889, 0.04721);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetHitColorWithRefraction()
    {
        double value = Math.Sqrt(2) / 2;
        Scene scene = Scene.DefaultScene();
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
                ColorSource = new SolidColorSource(new Color(1, 0, 0)),
                Ambient = 0.5
            },
            Transform = Transforms.Translate(0, -3.5, -0.5)
        };
        Ray ray = new (new Point(0, 0, -3), new Vector(0, -value, value));
        List<Intersection> intersections = new ()
        {
            new Intersection(floor, value * 2)
        };

        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ball);

        intersections[0].PrepareUsing(ray, intersections);

        Color color = scene.GetHitColor(intersections[0], 5);
        Color expected = new (0.93642, 0.68642, 0.68642);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetHitColorReflectance()
    {
        double value = Math.Sqrt(2) / 2;
        Scene scene = Scene.DefaultScene();
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
                ColorSource = new SolidColorSource(new Color(1, 0, 0)),
                Ambient = 0.5
            },
            Transform = Transforms.Translate(0, -3.5, -0.5)
        };
        Ray ray = new (new Point(0, 0, -3), new Vector(0, -value, value));
        List<Intersection> intersections = new ()
        {
            new Intersection(floor, value * 2)
        };

        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ball);

        intersections[0].PrepareUsing(ray, intersections);

        Color color = scene.GetHitColor(intersections[0], 5);
        Color expected = new (0.93391, 0.69643, 0.69243);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestApplyPhongForReflection()
    {
        double squareRootOf2 = Math.Sqrt(2);
        double value = squareRootOf2 / 2;
        Scene scene = Scene.DefaultScene();
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

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

        Color color = scene.GetHitColor(intersection, 1);
        Color expected = new (0.87675, 0.92434, 0.82917);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestReflectionRemaining()
    {
        double squareRootOf2 = Math.Sqrt(2);
        double value = squareRootOf2 / 2;
        Scene scene = Scene.DefaultScene();
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

        intersection.PrepareUsing(ray, new List<Intersection> { intersection });

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
