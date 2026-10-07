using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// An opaque surface whose mirroring follows Fresnel: square-on it mirrors what it says, at a graze very
/// nearly everything, and what it mirrors comes out of the light it shows as its own.
/// </summary>
[TestClass]
public class TestFresnel
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"fresnel-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Square-on, a surface mirrors exactly what it says; at a graze, all of it; and between, it follows
    /// Schlick's curve.
    /// </summary>
    [TestMethod]
    public void TestReflectanceRisesFromSquareOnToAGraze()
    {
        Material material = new () { Reflective = 0.04, Fresnel = true };

        Assert.AreEqual(0.04, material.ReflectanceAt(1));
        Assert.AreEqual(1, material.ReflectanceAt(0), 1e-12);
        Assert.AreEqual(0.04 + 0.96 / 32, material.ReflectanceAt(0.5), 1e-12);
        Assert.IsTrue(material.ReflectanceAt(0.3) > material.ReflectanceAt(0.6), "it did not climb toward a graze");
    }

    /// <summary>
    /// Without Fresnel, a surface mirrors the same share at every angle, as it always has.
    /// </summary>
    [TestMethod]
    public void TestWithoutFresnelReflectanceIsTheSameAtEveryAngle()
    {
        Material material = new () { Reflective = 0.3 };

        foreach (double cos in new[] { 1, 0.5, 0.1, 0 })
            Assert.AreEqual(0.3, material.ReflectanceAt(cos));
    }

    /// <summary>
    /// Fresnel has nothing to do on a surface that mirrors nothing, or on one that lets light through,
    /// which shares its light by its index of refraction already.
    /// </summary>
    [TestMethod]
    public void TestFresnelLeavesAloneWhatItDoesNotApplyTo()
    {
        Material dull = new () { Fresnel = true };
        Material clear = new () { Reflective = 0.5, Transparency = 0.5, Fresnel = true };

        Assert.IsFalse(dull.FresnelApplies);
        Assert.AreEqual(0, dull.ReflectanceAt(0));
        Assert.IsFalse(clear.FresnelApplies);
        Assert.AreEqual(0.5, clear.ReflectanceAt(0));
    }

    /// <summary>
    /// A floor that follows Fresnel mirrors the white sky by its square-on share when looked straight
    /// down at, and by Schlick's share when looked across.
    /// </summary>
    [TestMethod]
    public void TestASlantedFloorMirrorsMore()
    {
        Scene scene = SkyScene(Colors.White);
        Plane floor = Floor(new Material { Reflective = 0.04, Fresnel = true }, scene);

        AssertNear(new Color(0.04, 0.04, 0.04), scene.GetReflectionColor(Hit(floor, 1), 1), "straight down");

        double slanted = 0.04 + 0.96 * Math.Pow(0.8, 5);

        AssertNear(new Color(slanted, slanted, slanted), scene.GetReflectionColor(Hit(floor, 0.2), 1), "across");
        floor.Material.Fresnel = false;
        AssertNear(new Color(0.04, 0.04, 0.04), scene.GetReflectionColor(Hit(floor, 0.2), 1), "without it");
    }

    /// <summary>
    /// What a surface mirrors never reaches its pigment, so the light it shows as its own is what it does
    /// not mirror -- here against a black sky, so that the mirrored part is nothing and only the surface's
    /// own light is left to compare.
    /// </summary>
    [TestMethod]
    public void TestASurfaceShowsOnlyWhatItDoesNotMirror()
    {
        Scene scene = SkyScene(Colors.Black);
        Material material = new ()
        {
            Pigment = new SolidPigment(new Color(0.5, 0.5, 0.5)),
            Ambient = 0.1, Diffuse = 0.9, Specular = 0, Reflective = 0.3
        };
        Plane floor = Floor(material, scene);

        foreach (double cos in new[] { 1, 0.6, 0.2 })
        {
            material.Fresnel = false;

            Color plain = scene.GetHitColor(Hit(floor, cos), 1);

            material.Fresnel = true;

            Color fresnel = scene.GetHitColor(Hit(floor, cos), 1);
            double kept = 1 - Schlick(0.3, cos);

            Assert.IsTrue(plain.Red > 0.1, $"the floor was unlit at {cos}");
            AssertNear(plain * kept, fresnel, $"seen at a cosine of {cos}");
        }
    }

    /// <summary>
    /// The highlight is left alone: a floor showing nothing but a highlight shows the same one either way.
    /// </summary>
    [TestMethod]
    public void TestHighlightsAreLeftAlone()
    {
        Scene scene = SkyScene(Colors.Black);
        Material material = new ()
        {
            Ambient = 0, Diffuse = 0, Specular = 0.9, Shininess = 10, Reflective = 0.3
        };
        Plane floor = Floor(material, scene);
        double half = Math.Sqrt(0.5);
        // The light stands at [0, 10, -10], so the eye at [0, 5, 5] sees it mirrored in the floor.
        Ray ray = new (new Point(0, 5, 5), new Vector(0, -half, -half));
        Intersection intersection = new (floor, 5 / half);

        intersection.PrepareUsing(ray, [intersection]);

        Color plain = scene.GetHitColor(intersection, 1);

        material.Fresnel = true;

        Color fresnel = scene.GetHitColor(intersection, 1);

        Assert.IsTrue(plain.Red > 0.5, $"there was no highlight to compare: {plain}");
        Assert.IsTrue(plain.Matches(fresnel), $"the highlight went from {plain} to {fresnel}");
    }

    /// <summary>
    /// A metal keeps its tint and gains Fresnel's strength: square-on it mirrors as it always did, and
    /// across it mirrors the same colors more strongly.
    /// </summary>
    [TestMethod]
    public void TestAMetalKeepsItsTint()
    {
        Scene scene = SkyScene(Colors.White);
        Material material = new ()
        {
            Pigment = new SolidPigment(new Color(0.9, 0.6, 0.2)), Metallic = 1, Reflective = 0.5
        };
        Plane floor = Floor(material, scene);
        Color squarePlain = scene.GetReflectionColor(Hit(floor, 1), 1);
        Color acrossPlain = scene.GetReflectionColor(Hit(floor, 0.2), 1);

        material.Fresnel = true;

        AssertNear(squarePlain, scene.GetReflectionColor(Hit(floor, 1), 1), "square-on");
        AssertNear(
            acrossPlain * (Schlick(0.5, 0.2) / 0.5), scene.GetReflectionColor(Hit(floor, 0.2), 1),
            "across");
        Assert.IsTrue(squarePlain.Red > 2 * squarePlain.Blue, $"the metal was not tinted: {squarePlain}");
    }

    /// <summary>
    /// A surface that lets light through is shaded exactly as it was, Fresnel or not.
    /// </summary>
    [TestMethod]
    public void TestATransparentSurfaceIsUnchanged()
    {
        Scene scene = SkyScene(Colors.White);
        Material material = new () { Reflective = 0.5, Transparency = 0.5 };
        Plane floor = Floor(material, scene);
        Color plain = scene.GetHitColor(Hit(floor, 0.2), 1);

        material.Fresnel = true;

        Assert.IsTrue(plain.Matches(scene.GetHitColor(Hit(floor, 0.2), 1)));
    }

    /// <summary>
    /// A scene says it with `fresnel`: a black floor seen across, under a white sky, shows the sky by
    /// Schlick's share with it and by its square-on share without.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysFresnel()
    {
        // The camera looks at the floor's middle from one unit up and ten back.
        double cos = 1 / Math.Sqrt(101);
        double expected = 0.04 + 0.96 * Math.Pow(1 - cos, 5);

        AssertNear(new Color(expected, expected, expected), RenderedFloor("fresnel"), "with fresnel");
        AssertNear(new Color(0.04, 0.04, 0.04), RenderedFloor(""), "without");
    }

    /// <summary>
    /// A spectral render agrees, under a white sky.
    /// </summary>
    [TestMethod]
    public void TestASpectralRenderAgrees()
    {
        AssertNear(RenderedFloor("fresnel"), RenderedFloor("fresnel", "spectral"), "spectral against RGB");
    }

    /// <summary>
    /// An inherited material takes its finish from the one handed down, so it may not say `fresnel`.
    /// </summary>
    [TestMethod]
    public void TestAnInheritedMaterialCannotSayFresnel()
    {
        string path = Path.Combine(_directory, "inherited.igl");

        File.WriteAllText(path,
            "camera { location [0, 3, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -6] }\n" +
            "plane { material inherited { fresnel } }\n");

        string said = Said(() => new LanguageParser(path).Parse()?.Render(new RenderOptions
        {
            OutputFileName = Path.ChangeExtension(path, ".png"), Width = 8, Height = 6,
            ProgressStyleText = "none"
        }));

        StringAssert.Contains(said, "only decals may be written in one");
    }

    /// <summary>
    /// This method returns Schlick's share for a surface mirroring the given amount square-on, seen at
    /// the angle whose cosine is given -- worked out here rather than asked of the material, so that a
    /// material getting it wrong cannot also set the answer it is checked against.
    /// </summary>
    private static double Schlick(double squareOn, double cos)
    {
        return squareOn + (1 - squareOn) * Math.Pow(1 - cos, 5);
    }

    /// <summary>
    /// This method returns a scene with one light above and the given color for its sky.
    /// </summary>
    private static Scene SkyScene(Color sky)
    {
        Scene scene = new () { Background = new SolidPigment(sky) };

        scene.Lights.Add(new PointLight { Location = new Point(0, 10, -10) });

        return scene;
    }

    /// <summary>
    /// This method lays a floor wearing the given material in the given scene.
    /// </summary>
    private static Plane Floor(Material material, Scene scene)
    {
        Plane floor = new () { Material = material };

        scene.Surfaces.Add(floor);

        return floor;
    }

    /// <summary>
    /// This method returns where a ray meets the floor's middle, coming down at the angle whose cosine
    /// against the floor's normal is given, prepared for shading.
    /// </summary>
    private static Intersection Hit(Plane floor, double cos)
    {
        double sin = Math.Sqrt(1 - cos * cos);
        Ray ray = new (new Point(0, cos, -sin), new Vector(0, -cos, sin));
        Intersection intersection = new (floor, 1);

        intersection.PrepareUsing(ray, [intersection]);

        return intersection;
    }

    /// <summary>
    /// This method renders a black floor under a white sky, seen across from one unit up, and returns
    /// the color in the middle of the picture, which is the sky as the floor mirrors it.
    /// </summary>
    private Color RenderedFloor(string finish, string context = "")
    {
        string path = Path.Combine(_directory, "floor.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            $"context {{ no gamma  {context} }}\n" +
            "camera { location [0, 1, -10]  look at [0, 0, 0] }\n" +
            "point light { location [0, 10, -10] }\n" +
            "background White\n" +
            "plane { material { pigment Black  ambient 0  diffuse 0  specular 0  reflective 0.04  " +
            finish + " } }");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 9, Height = 9, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0].GetPixel(4, 4);
    }

    /// <summary>
    /// This method runs the given action and returns what it wrote to the console, or the message of
    /// whatever it threw.
    /// </summary>
    private static string Said(Action action)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }

    /// <summary>
    /// This method asserts that two colors match to within a level of an eight-bit image: rounding
    /// accounts for half of one, and a value on the edge between two levels may land on either.
    /// </summary>
    private static void AssertNear(Color expected, Color actual, string what)
    {
        const double level = 1.0 / 255;

        Assert.AreEqual(expected.Red, actual.Red, level, $"{what}: red, {actual} for {expected}");
        Assert.AreEqual(expected.Green, actual.Green, level, $"{what}: green, {actual} for {expected}");
        Assert.AreEqual(expected.Blue, actual.Blue, level, $"{what}: blue, {actual} for {expected}");
    }
}
