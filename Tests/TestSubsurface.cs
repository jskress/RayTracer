using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover light that comes up from under a surface: wax, marble, skin.  Each is held to an answer
/// worked out here where there is one.
/// </summary>
[TestClass]
public class TestSubsurface
{
    private static Material Scattering(Color color, Color radius) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = 1, Specular = 0,
        Subsurface = new Subsurface { Radius = radius }
    };

    private static Material Matte(Color color) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = 1, Specular = 0
    };

    private static Scene SceneOf(Light light, int walks, params Surface[] surfaces)
    {
        Scene scene = new () { SubsurfaceSamples = walks };

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(light);

        return scene;
    }

    private static Color ShadeColor(Scene scene, Point from, Vector toward)
    {
        Ray ray = new (from, toward);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 4);
    }

    // Nine places on the top of a slab, looked at from straight above.
    private static Color AveragedOverTheTop(Scene scene, double height)
    {
        Color sum = Colors.Black;

        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
                sum += ShadeColor(scene, new Point(i * 0.37, height + 1, j * 0.41), new Vector(0, -1, 0));
        }

        return sum * (1.0 / 9);
    }

    /// <summary>
    /// A walk under a solid made by CSG leaves by the solid's own surface: two wax balls a hair apart, joined,
    /// glow as one ball does, where walks leaving by one ball's surface inside the other found no light there.
    /// </summary>
    [TestMethod]
    public void TestAWalkUnderAUnionLeavesByTheUnionsSurface()
    {
        Material wax = Scattering(new Color(0.9, 0.9, 0.9), new Color(0.3, 0.3, 0.3));
        Sphere one = new () { Material = wax };
        CsgSurface joined = new ()
        {
            Operation = CsgOperation.Union,
            Left = new Sphere { Material = wax },
            Right = new Sphere { Material = wax, Transform = Transforms.Translate(0.03, 0, 0) }
        };
        PointLight lamp = new () { Location = new Point(0, 10, -2) };
        Scene alone = SceneOf(lamp, 64, one);
        Scene union = SceneOf(lamp, 64, joined);
        Point from = new (0, 0.3, -5);
        Vector toward = (new Point(0, 0.3, 0) - from).Unit;
        double single = ShadeColor(alone, from, toward).Red;
        double both = ShadeColor(union, from, toward).Red;

        Assert.AreEqual(single, both, single * 0.15, "two balls a hair apart, joined, should glow as one");
    }

    /// <summary>
    /// This tests the table that turns a color into the share kept at each turn: nought for black, rising to
    /// one for white, and the values the walk was worked out to give -- a color of 0.6 needs 95% kept at
    /// every turn, which is what makes a pale surface's light wander so far.
    /// </summary>
    [TestMethod]
    public void TestAColorIsTurnedIntoTheShareKeptAtEachTurn()
    {
        Assert.AreEqual(0, Subsurface.AlbedoFor(0));
        Assert.AreEqual(0.610, Subsurface.AlbedoFor(0.2), 0.002);
        Assert.AreEqual(0.951, Subsurface.AlbedoFor(0.6), 0.001);
        Assert.AreEqual(0.9906, Subsurface.AlbedoFor(0.8), 0.0005);

        double last = -1;

        for (double color = 0; color <= 1; color += 0.01)
        {
            double albedo = Subsurface.AlbedoFor(color);

            Assert.IsGreaterThanOrEqualTo(last, albedo);
            Assert.IsLessThanOrEqualTo(1, albedo);

            last = albedo;
        }
    }

    /// <summary>
    /// This tests the answer everything else rests on: a slab deep enough to have no far side, lit evenly from
    /// straight above, shows exactly its pigment's color, as a matte surface of it would -- dark, pale or
    /// colored, and however far the light travels inside, which only spreads it.
    /// </summary>
    [TestMethod]
    public void TestADeepSlabShowsItsPigment()
    {
        foreach (Color color in new[] { new Color(0.3, 0.3, 0.3), new Color(0.8, 0.8, 0.8), new Color(0.8, 0.45, 0.15) })
        {
            foreach (double radius in new[] { 0.02, 0.2 })
            {
                Scene scene = SceneOf(
                    new DistantLight { Direction = new Vector(0, -1, 0) }, 512,
                    new Cube { Material = Scattering(color, new Color(radius, radius, radius)), Transform = Transforms.Scale(100) });
                Color shown = AveragedOverTheTop(scene, 100);

                Assert.AreEqual(color.Red, shown.Red, 0.03 * color.Red + 0.005, $"red of {color} at {radius}");
                Assert.AreEqual(color.Green, shown.Green, 0.03 * color.Green + 0.005, $"green of {color} at {radius}");
                Assert.AreEqual(color.Blue, shown.Blue, 0.03 * color.Blue + 0.005, $"blue of {color} at {radius}");
            }
        }
    }

    /// <summary>
    /// This tests that each color may travel its own distance and still show as written: red carried ten
    /// times as far as blue, the slab still shows its pigment.  The walks weigh each color by its own odds,
    /// which spreads their answers more than one distance for all does, so this takes more of them.
    /// </summary>
    [TestMethod]
    public void TestEachColorTravelsItsOwnDistance()
    {
        Color color = new (0.7, 0.5, 0.35);
        Scene scene = SceneOf(
            new DistantLight { Direction = new Vector(0, -1, 0) }, 4096,
            new Cube { Material = Scattering(color, new Color(0.3, 0.1, 0.03)), Transform = Transforms.Scale(100) });
        Color shown = AveragedOverTheTop(scene, 100);

        Assert.AreEqual(color.Red, shown.Red, 0.03 * color.Red, $"{shown}");
        Assert.AreEqual(color.Green, shown.Green, 0.03 * color.Green, $"{shown}");
        Assert.AreEqual(color.Blue, shown.Blue, 0.03 * color.Blue, $"{shown}");
    }

    /// <summary>
    /// This tests the walk carried band by band, as a spectral render carries light: a deep slab still shows
    /// its pigment, each band kept at each turn as its own share of the color asks, and each band travelling
    /// its own distance when the radius gives one for each color.
    /// </summary>
    [TestMethod]
    public void TestADeepSlabShowsItsPigmentBandByBand()
    {
        SpectralBasis.Shared.Prepare();

        Color color = new (0.8, 0.45, 0.15);

        foreach (Color radius in new[] { new Color(0.1, 0.1, 0.1), new Color(0.3, 0.1, 0.03) })
        {
            Scene scene = SceneOf(
                new DistantLight { Direction = new Vector(0, -1, 0) }, 1024,
                new Cube { Material = Scattering(color, radius), Transform = Transforms.Scale(100) });
            Color sum = Colors.Black;

            for (int i = -1; i <= 1; i++)
            {
                Ray ray = new (new Point(i * 0.37, 101, 0.2), new Vector(0, -1, 0));
                List<Intersection> hits = scene.Intersect(ray);
                Intersection hit = hits.Hit();

                hit.PrepareUsing(ray, hits);
                sum += scene.GetHitColor<BandSpectrum>(hit, 4).ToColor();
            }

            Color shown = sum * (1.0 / 3);

            Assert.AreEqual(color.Red, shown.Red, 0.04 * color.Red + 0.01, $"red at {radius}: {shown}");
            Assert.AreEqual(color.Green, shown.Green, 0.04 * color.Green + 0.01, $"green at {radius}: {shown}");
            Assert.AreEqual(color.Blue, shown.Blue, 0.04 * color.Blue + 0.01, $"blue at {radius}: {shown}");
        }
    }

    /// <summary>
    /// This tests that light that hardly travels inside looks like a matte surface: on a ball lit from one side,
    /// a radius of a thousandth of the ball's gives what the matte ball gives, place by place.
    /// </summary>
    [TestMethod]
    public void TestLightThatHardlyTravelsLooksMatte()
    {
        Color color = new (0.6, 0.6, 0.6);
        PointLight lamp = new () { Location = new Point(-6, 4, -8) };
        Scene scattering = SceneOf(lamp, 256, new Sphere { Material = Scattering(color, new Color(0.001, 0.001, 0.001)) });
        Scene matte = SceneOf(lamp, 1, new Sphere { Material = Matte(color) });

        foreach ((double x, double y) in new[] { (0.0, 0.0), (-0.4, 0.3), (0.3, -0.2), (0.5, 0.5) })
        {
            Point eye = new (x, y, -5);
            double wanted = ShadeColor(matte, eye, new Vector(0, 0, 1)).Red;
            double shown = ShadeColor(scattering, eye, new Vector(0, 0, 1)).Red;

            Assert.AreEqual(wanted, shown, 0.03 * wanted + 0.002, $"at ({x}, {y})");
        }
    }

    /// <summary>
    /// This tests the thing a matte surface cannot do: a thin slab lit only from beneath shows light on top
    /// when light travels inside further than it is thick, next to none when it travels far less, and a matte
    /// slab none at all.
    /// </summary>
    [TestMethod]
    public void TestLightComesThroughAThinSlab()
    {
        Color color = new (0.8, 0.8, 0.8);

        double Through(Material material) => AveragedOverTheTop(
            SceneOf(new DistantLight { Direction = new Vector(0, 1, 0) }, 256,
                new Cube { Material = material, Transform = Transforms.Scale(50, 0.025, 50) }), 0.025).Red;

        double far = Through(Scattering(color, new Color(0.2, 0.2, 0.2)));
        double near = Through(Scattering(color, new Color(0.0005, 0.0005, 0.0005)));
        double matte = Through(Matte(color));

        Assert.IsGreaterThan(0.1, far, $"{far} came through when light travels far");
        Assert.IsLessThan(0.01, near, $"{near} came through when light hardly travels");
        Assert.AreEqual(0, matte);
    }

    /// <summary>
    /// This tests that the walks are the same every time, so a picture is.
    /// </summary>
    [TestMethod]
    public void TestTheWalksAreTheSameEveryTime()
    {
        Color Once() => ShadeColor(
            SceneOf(new PointLight { Location = new Point(-6, 4, -8) }, 16,
                new Sphere { Material = Scattering(new Color(0.7, 0.4, 0.3), new Color(0.3, 0.1, 0.05)) }),
            new Point(0.2, 0.1, -5), new Vector(0, 0, 1));

        Color first = Once(), second = Once();

        Assert.AreEqual(first.Red, second.Red);
        Assert.AreEqual(first.Green, second.Green);
        Assert.AreEqual(first.Blue, second.Blue);
    }

    /// <summary>
    /// This tests that a shape is walked where it stands, however it was put there: the same thin slab, lit
    /// from beneath, lets the same light through placed outright, standing in a group that moves it, in a
    /// group that moves and scales it, and as a shared shape an instance moves.  A surface is crossed in its
    /// parent's space, so a walk that handed it the world's ray would have found the slab where it is not.
    /// </summary>
    [TestMethod]
    public void TestAShapeIsWalkedWhereItStands()
    {
        Material material = Scattering(new Color(0.8, 0.8, 0.8), new Color(0.2, 0.2, 0.2));
        Matrix lifted = Transforms.Translate(0, 2, 0);
        Matrix slab = Transforms.Scale(50, 0.025, 50);

        double Through(Surface surface) => ShadeColor(
            SceneOf(new DistantLight { Direction = new Vector(0, 1, 0) }, 64, surface),
            new Point(0.1, 3, 0.2), new Vector(0, -1, 0)).Red;

        double outright = Through(new Cube { Material = material, Transform = lifted * slab });
        double grouped = Through(new Group { Transform = lifted }.Add(new Cube { Material = material, Transform = slab }));
        double scaled = Through(new Group { Transform = lifted * Transforms.Scale(2) }
            .Add(new Cube { Material = material, Transform = Transforms.Scale(25, 0.0125, 25) }));
        double shared = Through(new Instance { Prototype = new Cube { Material = material, Transform = slab }, Transform = lifted });

        Assert.IsGreaterThan(0.1, outright, "light should come through");
        Assert.AreEqual(outright, grouped, 1e-6 * outright, "in a group");
        Assert.AreEqual(outright, scaled, 1e-6 * outright, "in a scaled group");
        Assert.AreEqual(outright, shared, 1e-6 * outright, "through an instance");
    }

    /// <summary>
    /// This tests the walk through a sheet against the same walk followed apart, two million times over, for a
    /// pale color (0.8) at thicknesses from three tenths of the distance light travels inside to thirty: the
    /// shares that come back out and that come through.  Thick enough, it gives back the pigment, as a deep
    /// slab does.
    /// </summary>
    [TestMethod]
    public void TestASheetSharesTheLightOutAsTheWalkDoes()
    {
        RgbSpectrum albedo = Subsurface.AlbedoOf(RgbSpectrum.FromReflectance(new Color(0.8, 0.8, 0.8)));
        RgbSpectrum extinction = RgbSpectrum.FromUnbounded(new Color(1, 1, 1));

        foreach ((double thickness, double reflected, double passed) in new[]
                 { (0.3, 0.2036, 0.7908), (1.0, 0.4365, 0.5450), (3.0, 0.6670, 0.2795), (30.0, 0.8003, 0.0024) })
        {
            (RgbSpectrum back, RgbSpectrum through) = Scene.SharedOutBy(albedo, extinction, thickness, 200_000, 99);

            Assert.AreEqual(reflected, back.Average, 0.006, $"given back at {thickness}");
            Assert.AreEqual(passed, through.Average, 0.006, $"let through at {thickness}");
        }
    }

    /// <summary>
    /// This tests a sheet as a scene sees it: lit from in front it shows what it gives back, and lit from
    /// behind what it lets through -- as thick as light travels inside it, 0.44 and 0.55 of a pale color's
    /// light -- where a matte sheet lit from behind shows nothing.
    /// </summary>
    [TestMethod]
    public void TestASheetGlowsWithTheLightBehindIt()
    {
        Color color = new (0.8, 0.8, 0.8);
        Material leaf = Scattering(color, new Color(0.01, 0.01, 0.01));

        leaf.Subsurface.Thickness = 0.01;

        double Seen(Material material, Vector shining) => AveragedOverTheTop(
            SceneOf(new DistantLight { Direction = shining }, 4096,
                new Parallelogram
                {
                    Point = new Point(-5, 0, -5), Side1 = new Vector(10, 0, 0), Side2 = new Vector(0, 0, 10),
                    Material = material
                }), 0).Red;

        Assert.AreEqual(0.4365, Seen(leaf, new Vector(0, -1, 0)), 0.015, "lit from in front");
        Assert.AreEqual(0.5450, Seen(leaf, new Vector(0, 1, 0)), 0.015, "lit from behind");
        Assert.AreEqual(0, Seen(Matte(color), new Vector(0, 1, 0)), "a matte sheet lit from behind");
    }

    /// <summary>
    /// This tests the words a scene says it with: a thin slab lit only from beneath shows light on top when
    /// its material says the light travels under it, and none when it says nothing, and a radius of nothing
    /// is refused.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysLightTravelsUnderASurface()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"subsurface-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            double Top(string subsurface, string shape = "cube", string placed = "scale [50, 0.025, 50]")
            {
                File.WriteAllText(path,
                    "context { no gamma  subsurface samples 64 }\n" +
                    "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 1 }\n" +
                    "distant light { direction [0, 1, 0] }\n" +
                    shape + " { material { pigment [0.8, 0.8, 0.8]  ambient 0  diffuse 1  specular 0  " + subsurface + " }" +
                    "  " + placed + " }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{subsurface}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2).Red;
            }

            Assert.IsGreaterThan(0.05, Top("subsurface { radius 0.2 }"), "light should come through");
            Assert.IsGreaterThan(0.05, Top("subsurface { radius [0.2, 0.1, 0.05] }"), "a radius for each color");
            Assert.IsLessThan(0.01, Top("subsurface { radius 0.0005 }"), "light hardly travelling should not");
            Assert.AreEqual(0, Top(""), "a matte slab lets none through");

            // A sheet, the radius a hundredth: thin, it lets the light behind it through; thick, next to none.
            const string sheet = "at [-5, 0, -5]  sides [10, 0, 0], [0, 0, 10]";

            Assert.IsGreaterThan(0.5, Top("subsurface { radius 0.01  thickness 0.001 }", "parallelogram", sheet), "a thin sheet");
            Assert.IsLessThan(0.1, Top("subsurface { radius 0.01  thickness 0.2 }", "parallelogram", sheet), "a thick sheet");
            Assert.ThrowsExactly<TokenException>(() => Top("subsurface { radius 0 }"), "no distance at all is refused");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
