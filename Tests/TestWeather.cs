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
/// These tests cover the weather a model says it has been out in: each part's own material weathered where its
/// patterns say -- dust on what faces up, grime where it is closed in, wear on its edges -- handed down to its
/// parts and through the instances it stands by, and nothing at all where it says nothing.
/// </summary>
[TestClass]
public class TestWeather
{
    private static readonly Color Blue = new (0.05, 0.12, 0.5);

    private static Material Glowing(Color color) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = 0, Specular = 0, Emission = Colors.White
    };

    private static Scene SceneOf(params Surface[] surfaces)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Black), WeatheringSamples = 256 };

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        WeatherSettler.Settle(scene.Surfaces);

        return scene;
    }

    private static Color Seen(Scene scene, Point from, Vector toward)
    {
        Ray ray = new (from, toward.Unit);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 1);
    }

    private static void AssertSame(Color expected, Color seen, string what)
    {
        Assert.AreEqual(expected.Red, seen.Red, 1e-12, what);
        Assert.AreEqual(expected.Green, seen.Green, 1e-12, what);
        Assert.AreEqual(expected.Blue, seen.Blue, 1e-12, what);
    }

    /// <summary>
    /// This tests weather that does nothing: the material is the very same one, so a model weathered by
    /// nothing is exactly as it was.
    /// </summary>
    [TestMethod]
    public void TestWeatherOfNothingChangesNothing()
    {
        Material blue = Glowing(Blue);

        Assert.AreSame(blue, new Weather().Weathered(blue));
        Assert.AreSame(blue, new Weather { Radius = 2 }.Weathered(blue));
    }

    /// <summary>
    /// This tests dust: on a cube's top it is the cube's own color most of the way to the dust's, the noise
    /// leaving at least half of it; on its side, square to the sky, there is none.
    /// </summary>
    [TestMethod]
    public void TestDustSettlesOnWhatFacesUp()
    {
        Cube cube = new () { Material = Glowing(Blue), Weather = new Weather { Dust = 0.6 } };
        Scene scene = SceneOf(cube);
        Color dust = new Weather().DustColor;
        Color side = Seen(scene, new Point(0.2, 0.1, -3), new Vector(0, 0, 1));
        Color top = Seen(scene, new Point(0.2, 3, 0.1), new Vector(0, -1, 0));
        double half = Blue.Red + (dust.Red - Blue.Red) * 0.85 * 0.5;
        double full = Blue.Red + (dust.Red - Blue.Red) * 0.85;

        AssertSame(Blue, side, "the side");
        Assert.IsTrue(top.Red >= half - 1e-9 && top.Red <= full + 1e-9, $"the top: {top.Red} between {half} and {full}");
    }

    /// <summary>
    /// This tests snow: on a cube's top it is the snow's own color, whole, not broken as dust is; on its side
    /// there is none; on a face sloping at forty-five degrees a full fall lies whole, while a light one, which
    /// holds only on gentler slopes than that, leaves it bare.
    /// </summary>
    [TestMethod]
    public void TestSnowLiesWholeOnWhatIsFlatEnoughToHoldIt()
    {
        Color snow = new Weather().SnowColor;
        Scene flat = SceneOf(new Cube { Material = Glowing(Blue), Weather = new Weather { Snow = 1 } });

        AssertSame(snow, Seen(flat, new Point(0.2, 3, 0.1), new Vector(0, -1, 0)), "the top");
        AssertSame(snow, Seen(flat, new Point(-0.7, 3, -0.6), new Vector(0, -1, 0)), "another place on the top");
        AssertSame(Blue, Seen(flat, new Point(0.2, 0.1, -3), new Vector(0, 0, 1)), "the side");

        foreach ((double fall, Color expected) in new[] { (1.0, snow), (0.3, Blue) })
        {
            Scene sloping = SceneOf(new Cube
            {
                Material = Glowing(Blue), Weather = new Weather { Snow = fall }, Transform = Transforms.RotateAroundZ(45)
            });

            AssertSame(expected, Seen(sloping, new Point(0.4, 3, 0.1), new Vector(0, -1, 0)), $"a slope under {fall} of snow");
        }
    }

    /// <summary>
    /// This tests grime: on a floor right beside a wall, deep in where it meets it, the floor is darkened toward
    /// the grime's color; out in the open it is untouched.
    /// </summary>
    [TestMethod]
    public void TestGrimeGathersWhereItIsClosedIn()
    {
        // Solids, so that looking into the floor finds the floor and not the wall: a slab two deep, and a block
        // standing on it whose face is 0.05 from the place.
        Cube floor = new () { Material = Glowing(Colors.White), Transform = Transforms.Translate(0, -1, 0) * Transforms.Scale(10, 1, 10) };
        Cube wall = new ()
        {
            Material = Glowing(Colors.White), Transform = Transforms.Translate(0.55, 2, 0) * Transforms.Scale(0.5, 2, 10)
        };
        Group yard = new Group { Weather = new Weather { Grime = 0.8, Radius = 1 } }.Add(floor).Add(wall);
        Scene scene = SceneOf(yard);
        Color corner = Seen(scene, new Point(0, 2, -1), new Vector(0, -2, 1));
        Color open = Seen(scene, new Point(-5, 2, -1), new Vector(0, -2, 1));

        AssertSame(Colors.White, open, "out in the open");
        Assert.IsLessThan(0.75, corner.Red, $"in the corner: {corner}");
    }

    /// <summary>
    /// This tests wear: along a cube's edges a good share of places bare what the wear says lies beneath, the
    /// noise chipping it; in the middle of its faces none do.
    /// </summary>
    [TestMethod]
    public void TestWearBaresWhatLiesBeneathOnTheEdges()
    {
        Material steel = Glowing(new Color(0.6, 0.61, 0.63));
        Cube cube = new ()
        {
            Material = Glowing(Blue), Transform = Transforms.Scale(2),
            Weather = new Weather { Wear = 0.8, WearTo = steel, Radius = 0.3 }
        };
        Scene scene = SceneOf(cube);
        int bared = 0;
        const int places = 200;

        for (int index = 0; index < places; index++)
        {
            double z = -1.9 + 3.8 * (index + 0.5) / places;

            // Just in from the edge where the top meets the front, looking down at the top.
            // Steel's red is 0.6; the cube's own blue made paler, which wear bares when it names nothing, is 0.38.
            if (Seen(scene, new Point(1.97, 5, z), new Vector(0, -1, 0)).Red > 0.45)
                bared++;
        }

        Assert.IsGreaterThan(places / 5, bared, $"along the edge, {bared} of {places} bared");
        AssertSame(Blue, Seen(scene, new Point(0, 5, 0.3), new Vector(0, -1, 0)), "the middle of the top");
    }

    /// <summary>
    /// This tests dirt kicked up from the ground: on a post standing in a group that is moved up and scaled,
    /// the post is dirtied near the group's own ground, a tenth of the splash up, and clean above it -- the
    /// height measured in the group's own space, not the world's.
    /// </summary>
    [TestMethod]
    public void TestSplashRisesFromTheModelsOwnGround()
    {
        Cube post = new () { Material = Glowing(Blue), Transform = Transforms.Translate(0, 2, 0) * Transforms.Scale(0.5, 2, 0.5) };
        Group model = new Group
        {
            Transform = Transforms.Translate(5, 3, 0) * Transforms.Scale(2),
            Weather = new Weather { Splash = 1 }
        }.Add(post);
        Scene scene = SceneOf(model);

        // The post's face toward -Z stands at z = -1 in the world; its foot at y = 3, and the group's units are two
        // of the world's.
        Color low = Seen(scene, new Point(5, 3 + 2 * 0.1, -5), new Vector(0, 0, 1));
        Color high = Seen(scene, new Point(5, 3 + 2 * 1.5, -5), new Vector(0, 0, 1));
        Color dirt = new Weather().SplashColor;
        double least = Blue.Red + (dirt.Red - Blue.Red) * 0.7 * 0.4;

        AssertSame(Blue, high, "above the splash");
        Assert.IsTrue(low.Red >= least - 1e-9, $"near the ground: {low.Red}, at least {least}");

        // The same, the model being a shape shared by an instance, as a primitive's call makes one: its ground is
        // still its own, carried through the instance.
        Cube shared = new () { Material = Glowing(Blue), Transform = Transforms.Translate(0, 2, 0) * Transforms.Scale(0.5, 2, 0.5) };
        Group shape = new Group { Weather = new Weather { Splash = 1 } }.Add(shared);
        Scene placed = SceneOf(new Instance { Prototype = shape, Transform = Transforms.Translate(5, 3, 0) });

        AssertSame(Blue, Seen(placed, new Point(5, 3 + 1.5, -5), new Vector(0, 0, 1)), "above, through an instance");
        Assert.IsTrue(Seen(placed, new Point(5, 3 + 0.1, -5), new Vector(0, 0, 1)).Red >= least - 1e-9,
            "near the ground, through an instance");

        // The same, the model rising three units as the shutter is open: its ground is where it is at the instant.
        Cube rising = new () { Material = Glowing(Blue), Transform = Transforms.Translate(0, 2, 0) * Transforms.Scale(0.5, 2, 0.5) };
        Group moving = new Group
        {
            Transform = Transforms.Translate(5, 0, 0), MotionAt = fraction => Transforms.Translate(0, 3 * fraction, 0),
            Weather = new Weather { Splash = 1 }
        }.Add(rising);
        Scene sweeping = new () { Background = new SolidPigment(Colors.Black), WeatheringSamples = 256 };

        moving.PrepareForRendering([0, 1]);
        sweeping.Surfaces.Add(moving);
        WeatherSettler.Settle(sweeping.Surfaces);

        Color At(int timeIndex)
        {
            Ray ray = new (new Point(5, 3.1, -5), new Vector(0, 0, 1), timeIndex);
            List<Intersection> hits = sweeping.Intersect(ray);
            Intersection hit = hits.Hit();

            hit.PrepareUsing(ray, hits);

            return sweeping.GetHitColor(hit, 1);
        }

        AssertSame(Blue, At(0), "high on the post before it rises");
        Assert.IsTrue(At(1).Red >= least - 1e-9, "near the ground once it has risen");
    }

    /// <summary>
    /// This tests where a part's weather comes from: its own, or the nearest group's round it, and through the
    /// instance a shared shape is met by -- so that the same shape stands weathered by one instance and clean by
    /// another.
    /// </summary>
    [TestMethod]
    public void TestAPartsWeatherIsItsNearest()
    {
        Weather dusty = new () { Dust = 1 };
        Weather none = new ();
        Cube outer = new () { Material = Glowing(Blue), Transform = Transforms.Translate(-3, 0, 0) };
        Cube inner = new () { Material = Glowing(Blue), Transform = Transforms.Translate(3, 0, 0) };
        Group nested = new Group { Weather = none }.Add(inner);
        Scene scene = SceneOf(new Group { Weather = dusty }.Add(outer).Add(nested));

        Assert.AreNotEqual(Blue.Red, Seen(scene, new Point(-3, 3, 0.1), new Vector(0, -1, 0)).Red, "the group's");
        AssertSame(Blue, Seen(scene, new Point(3, 3, 0.1), new Vector(0, -1, 0)), "the nearer group's, nothing");

        Cube shape = new () { Material = Glowing(Blue) };
        Scene shared = SceneOf(
            new Instance { Prototype = shape, Transform = Transforms.Translate(-3, 0, 0), Weather = dusty },
            new Instance { Prototype = shape, Transform = Transforms.Translate(3, 0, 0) });

        Assert.AreNotEqual(Blue.Red, Seen(shared, new Point(-3, 3, 0.1), new Vector(0, -1, 0)).Red, "a weathered instance");
        AssertSame(Blue, Seen(shared, new Point(3, 3, 0.1), new Vector(0, -1, 0)), "a clean one of the same shape");
    }

    /// <summary>
    /// This tests the words a scene says it with, and that a weather block saying nothing leaves the picture as
    /// it was, bit for bit; and what is refused.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysAModelHasBeenOutInTheWeather()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"weather-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            Color[] Rendered(string weather)
            {
                File.WriteAllText(path,
                    "context { no gamma  weathering samples 32 }\n" +
                    "camera { location [0, 3, -4]  look at [0, 0.5, 0] }\n" +
                    "point light { location [-3, 5, -4] }\n" +
                    "Steel = material { pigment [0.6, 0.61, 0.63]  metallic reflective 0.7  roughness 0.25 }\n" +
                    "group {\n" +
                    "    cube { translate Y 1  material { pigment [0.05, 0.12, 0.5]  specular 0.4  shininess 60 } }\n" +
                    "    plane { material { pigment [0.5, 0.5, 0.5] } }\n" +
                    "    " + weather + "\n" +
                    "}\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{weather}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 48, Height = 36, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                Canvas canvas = new ImageFile(output).Load()[0];

                return Enumerable.Range(0, canvas.Width * canvas.Height)
                    .Select(index => canvas.GetPixel(index % canvas.Width, index / canvas.Width)).ToArray();
            }

            Color[] clean = Rendered("");
            Color[] nothing = Rendered("weather { }");
            Color[] weathered = Rendered("weather { grime 0.6  color [0.2, 0.1, 0.05]  dust 0.5  color [0.7, 0.6, 0.4]  " +
                                         "wear 0.5 to Steel  splash 0.5  color [0.5, 0.4, 0.3]  radius 0.3 }");
            Color[] written = Rendered("weather { wear 0.5 to material { pigment White }  radius 0.3 }");

            bool Same(Color[] one, Color[] other) => one.Zip(other).All(pair => pair.First.Matches(pair.Second));

            Assert.IsTrue(Same(clean, nothing), "weather saying nothing");
            Assert.IsFalse(Same(clean, weathered), "weathered");
            Assert.IsFalse(Same(Rendered("weather { wear 0.5  radius 0.3 }"), written), "worn to a material written out");
            Assert.IsFalse(Same(Rendered("weather { grime 0.6  radius 0.6 }"), Rendered("weather { grime 0.6  color [1, 0, 0]  radius 0.6 }")),
                "the grime's color");
            Assert.IsFalse(Same(Rendered("weather { dust 0.6 }"), Rendered("weather { dust 0.6  color [1, 0, 0] }")),
                "the dust's color");
            Assert.IsFalse(Same(Rendered("weather { grime 0.6 }"), Rendered("weather { grime 0.6  radius 0.6 }")),
                "the reach");
            Assert.IsFalse(Same(clean, Rendered("weather { splash 0.6 }")), "splash");
            Assert.IsFalse(Same(clean, Rendered("weather { snow 1 }")), "snow");
            Assert.IsFalse(Same(Rendered("weather { snow 1 }"), Rendered("weather { snow 1  color [1, 0, 0] }")),
                "the snow's color");
            Assert.IsFalse(Same(Rendered("weather { splash 0.6 }"), Rendered("weather { splash 0.6  color [1, 0, 0] }")),
                "the splash's color");

            TextWriter was = Console.Out;

            Console.SetOut(new StringWriter());

            try
            {
                Assert.ThrowsExactly<Lex.Parser.TokenException>(() => Rendered("weather { grime 1.5 }"), "more than all of it");
                Assert.ThrowsExactly<Lex.Parser.TokenException>(() => Rendered("weather { radius 0 }"), "no reach");
            }
            finally
            {
                Console.SetOut(was);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
