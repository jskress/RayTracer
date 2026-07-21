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
            Material = new Material(),
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
    public void TestGetHitColorWithMultipleLightsIsAdditive()
    {
        // GetHitColor() aggregates each light's contribution independently, so lighting a
        // surface with two lights together must equal the sum of lighting it with each
        // light alone (ambient included, since it's applied per-light in ApplyPhong()).
        Scene scene = DefaultScene();
        PointLight secondLight = new () { Location = new Point(10, 10, -10) };
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        Surface surface = scene.Surfaces[0];
        Intersection intersectionForFirstLightOnly = new (surface, 4);

        intersectionForFirstLightOnly.PrepareUsing(ray, [intersectionForFirstLightOnly]);

        Color colorWithFirstLightOnly = scene.GetHitColor(intersectionForFirstLightOnly, 0);

        Scene soloSecondLightScene = DefaultScene();

        soloSecondLightScene.Lights[0] = secondLight;

        Intersection intersectionForSecondLightOnly = new (soloSecondLightScene.Surfaces[0], 4);

        intersectionForSecondLightOnly.PrepareUsing(ray, [intersectionForSecondLightOnly]);

        Color colorWithSecondLightOnly = soloSecondLightScene.GetHitColor(intersectionForSecondLightOnly, 0);

        scene.Lights.Add(secondLight);

        Intersection intersectionForBothLights = new (surface, 4);

        intersectionForBothLights.PrepareUsing(ray, [intersectionForBothLights]);

        Color colorWithBothLights = scene.GetHitColor(intersectionForBothLights, 0);
        Color expected = colorWithFirstLightOnly + colorWithSecondLightOnly;

        Assert.IsTrue(expected.Matches(colorWithBothLights));
    }

    [TestMethod]
    public void TestGetHitColorWithMultipleLightsRespectsPerLightShadows()
    {
        // A blocker sphere sits between the target point and one light, but not the other.
        // GetHitColor() must apply each light's own shadow result to that light's own
        // contribution, not mix them up or apply one light's shadow state to both.
        PointLight blockedLight = new () { Location = new Point(0, 0, -20) };
        PointLight visibleLight = new () { Location = new Point(3, 0, -10) };
        Scene scene = new ();
        Sphere target = new ();
        Sphere blocker = new () { Transform = Transforms.Translate(0, 0, -10) };
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));

        scene.Lights.Add(blockedLight);
        scene.Lights.Add(visibleLight);
        scene.Surfaces.Add(blocker);
        scene.Surfaces.Add(target);

        Intersection intersection = new (target, 4);

        intersection.PrepareUsing(ray, [intersection]);

        Assert.IsTrue(scene.IsInShadow(blockedLight, intersection.OverPoint));
        Assert.IsFalse(scene.IsInShadow(visibleLight, intersection.OverPoint));

        Color actual = scene.GetHitColor(intersection, 0);
        Color expected =
            blockedLight.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal, target, Colors.Black) +
            visibleLight.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal, target, Colors.White);

        Assert.IsTrue(expected.Matches(actual));
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
        surface.Material.Interior.IndexOfRefraction = 1.5;
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
        surface.Material.Interior.IndexOfRefraction = 1.5;

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
        b.Material.Interior.IndexOfRefraction = 1.5;

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
                Interior = new Interior { IndexOfRefraction = 1.5 }
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

        // The book's number here is (0.93642, 0.68642, 0.68642), and it no longer holds, because
        // the book's shadows are opaque no matter what casts them: its half-transparent floor
        // still leaves the ball beneath it in full shadow, lit only by its own ambient.  Now that
        // light is charged for what it passes through rather than stopped by it, half of it
        // reaches the ball.  Only the red channel moves, to the last digit -- the ball is pure red,
        // so light newly arriving on it has nowhere else to go, which is the check that this is
        // the shadow change and not drift from somewhere else.  Some of that light is then turned
        // away again at the floor's own surface, the floor having an index of refraction to bend
        // it by.
        //
        // The floor then stops showing half of its own colour, being half transparent, and that is
        // the larger part of the difference from the book's figure: green and blue come out at
        // exactly half what they were, since nothing else in the scene contributes to them.  A
        // surface can only show as much of itself as it stops.
        Color expected = new (0.774155, 0.343213, 0.343213);

        Assert.IsTrue(expected.Matches(color), color.ToString());
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
                Interior = new Interior { IndexOfRefraction = 1.5 }
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

        // As above, the book's (0.93391, 0.69643, 0.69243) assumed the translucent floor cast an
        // opaque shadow on the ball below it, less what that floor turns away at its own surface --
        // and less again the half of its own colour that a half-transparent floor no longer shows.
        Color expected = new (0.764033, 0.353222, 0.349218);

        Assert.IsTrue(expected.Matches(color), color.ToString());
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
