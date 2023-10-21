using RayTracer.Basics;
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

        intersection.PrepareUsing(ray);

        Color color = scene.GetHitColor(intersection);
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

        intersection.PrepareUsing(ray);

        Color color = scene.GetHitColor(intersection);
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

        intersection.PrepareUsing(ray);

        Color color = scene.GetHitColor(intersection);
        Color expected = new (0.1, 0.1, 0.1);

        Assert.IsTrue(expected.Matches(color));
    }

    [TestMethod]
    public void TestGetColorFor()
    {
        Scene scene = Scene.DefaultScene();
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 1, 0));

        Assert.IsTrue(Color.Transparent.Matches(scene.GetColorFor(ray)));

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
}
