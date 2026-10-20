using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover blends of whole materials by a pattern: the material a hit settles on, mixed in proportion
/// between the two the pattern falls between, or one whole, and the shading that follows from it.
/// </summary>
[TestClass]
public class TestMaterialBlend
{
    private static Material Glowing(Color color, double roughness = 0, double reflective = 0) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = 0, Specular = 0, Emission = Colors.White,
        Roughness = roughness, Reflective = reflective
    };

    // A gradient running from nought at x = 0 to one at x = 1 across a floor.
    private static GradientPattern AlongX() => new () { BandType = BandType.LinearX };

    private static Material Blended(Pattern pattern, double[] breaks, params Material[] materials)
    {
        Material first = materials[0].CopiedForBlending();

        first.Blend = new MaterialBlend { Pattern = pattern, Breaks = breaks, Materials = materials };

        return first;
    }

    private static (Scene Scene, Plane Floor) FloorOf(Material material, Color sky = null)
    {
        Scene scene = new () { Background = new SolidPigment(sky ?? Colors.Black) };
        Plane floor = new () { Material = material };

        floor.PrepareForRendering();
        scene.Surfaces.Add(floor);

        return (scene, floor);
    }

    // Where a ray coming straight down meets the floor at x, prepared.
    private static Intersection HitAt(Scene scene, double x, double z = 0.3)
    {
        Ray ray = new (new Point(x, 1, z), new Vector(0, -1, 0));
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return hit;
    }

    /// <summary>
    /// This tests a mixture of two materials: every number of their finishes mixed in proportion, their colors
    /// mixed, and what cannot be mixed -- here a coat -- the nearer's.
    /// </summary>
    [TestMethod]
    public void TestAMixtureIsPartWayBetween()
    {
        Material glossy = new ()
        {
            Pigment = new SolidPigment(new Color(1, 0, 0)), Ambient = 0.1, Diffuse = 0.8, Specular = 0.6, Shininess = 100,
            Reflective = 0.2, Roughness = 0, Metallic = 0, Coat = new Coat()
        };
        Material matte = new ()
        {
            Pigment = new SolidPigment(new Color(0, 0, 1)), Ambient = 0.3, Diffuse = 0.4, Specular = 0, Shininess = 10,
            Reflective = 0, Roughness = 0.8, Metallic = 1, Emission = Colors.White
        };
        Material quarter = MaterialBlend.Mixed(glossy, matte, 0.25);
        Material threeQuarters = MaterialBlend.Mixed(glossy, matte, 0.75);

        Assert.AreEqual(0.15, quarter.EffectiveAmbient, 1e-12, "ambient");
        Assert.AreEqual(0.7, quarter.Diffuse, 1e-12, "diffuse");
        Assert.AreEqual(0.45, quarter.Specular, 1e-12, "specular");
        Assert.AreEqual(77.5, quarter.Shininess, 1e-12, "shininess");
        Assert.AreEqual(0.15, quarter.Reflective, 1e-12, "reflective");
        Assert.AreEqual(0.2, quarter.Roughness, 1e-12, "roughness");
        Assert.AreEqual(0.25, quarter.Metallic, 1e-12, "metallic");
        Assert.AreEqual(0.25, quarter.Emission.Red, 1e-12, "emission, a glow from none");

        Color color = quarter.Pigment.GetColorFor(Point.Zero);

        Assert.AreEqual(0.75, color.Red, 1e-12, "the color, red");
        Assert.AreEqual(0.25, color.Blue, 1e-12, "the color, blue");
        Assert.IsNotNull(quarter.Coat, "a coat, from the nearer");
        Assert.IsNull(threeQuarters.Coat, "no coat, from the nearer");
        Assert.IsNull(quarter.Blend, "a mixture is no blend itself");
    }

    /// <summary>
    /// This tests the material a hit settles on: below the first value the first material whole, above the
    /// last the last, and between two the mixture in proportion -- kept, so that a second hit at the same step
    /// is given the same one.
    /// </summary>
    [TestMethod]
    public void TestAHitSettlesWhereThePatternFalls()
    {
        Material smooth = Glowing(Colors.Red, roughness: 0.1);
        Material rough = Glowing(Colors.Blue, roughness: 0.9);
        Material blended = Blended(AlongX(), [0.2, 0.6], smooth, rough);
        (Scene scene, _) = FloorOf(blended);
        MaterialBlend blend = blended.Blend;

        Assert.AreSame(smooth, blend.At(HitAt(scene, 0.1), scene), "below the first value");
        Assert.AreSame(rough, blend.At(HitAt(scene, 0.8), scene), "above the last");

        Material between = blend.At(HitAt(scene, 0.3), scene);

        Assert.AreEqual(0.1 + 0.8 * 0.25, between.Roughness, 0.8 / 256, "a quarter of the way");
        Assert.AreSame(between, blend.At(HitAt(scene, 0.3, 0.7), scene), "the same step, kept");
    }

    /// <summary>
    /// This tests the shading that follows: a floor blended from a red glow to a blue one shows the glows mixed
    /// where the pattern falls between them, and a floor blended from a mirror to nothing mirrors the sky in
    /// proportion -- the finish changing with the color.
    /// </summary>
    [TestMethod]
    public void TestAHitIsShadedWithWhatItSettledOn()
    {
        (Scene glows, _) = FloorOf(Blended(AlongX(), [0, 1], Glowing(Colors.Red), Glowing(Colors.Blue)));
        Color seen = glows.GetHitColor(HitAt(glows, 0.25), 1);

        Assert.AreEqual(0.75, seen.Red, 1.0 / 256, "red");
        Assert.AreEqual(0.25, seen.Blue, 1.0 / 256, "blue");

        Material mirror = Glowing(Colors.Black, reflective: 1);
        Material dull = Glowing(Colors.Black);

        mirror.Emission = null;
        dull.Emission = null;

        (Scene mirrored, _) = FloorOf(Blended(AlongX(), [0, 1], mirror, dull), Colors.White);

        Assert.AreEqual(0.75, mirrored.GetHitColor(HitAt(mirrored, 0.25), 2).Red, 1.0 / 256, "three quarters a mirror");
    }

    /// <summary>
    /// This tests a pattern that picks among a fixed number of things: a checker of two materials, each square
    /// one of them whole.
    /// </summary>
    [TestMethod]
    public void TestACheckerPicksAMaterialOutright()
    {
        Material red = Glowing(Colors.Red);
        Material blue = Glowing(Colors.Blue);
        Material blended = Blended(new CheckerPattern(), [], red, blue);
        (Scene scene, _) = FloorOf(blended);

        Assert.AreSame(red, blended.Blend.At(HitAt(scene, 0.5, 0.5), scene), "one square");
        Assert.AreSame(blue, blended.Blend.At(HitAt(scene, 1.5, 0.5), scene), "the next");
    }

    /// <summary>
    /// This tests a blend whose material is itself a blend: settled at the hit first, and mixed as any other.
    /// </summary>
    [TestMethod]
    public void TestABlendMayHoldABlend()
    {
        Material red = Glowing(Colors.Red);
        Material green = Glowing(new Color(0, 1, 0));
        Material blue = Glowing(Colors.Blue);
        Material inner = Blended(new CheckerPattern(), [], green, blue);
        Material outer = Blended(AlongX(), [0, 1], red, inner);
        (Scene scene, _) = FloorOf(outer);

        // At x = 0.5 and z = 1.5 the checker's square is blue, its second: half red, half blue.  A blend left
        // unsettled would show its first, green, which stands for it where there is no hit to settle it.
        Color seen = scene.GetHitColor(HitAt(scene, 0.5, 1.5), 1);

        Assert.AreEqual(0.5, seen.Red, 1.0 / 256, "red");
        Assert.AreEqual(0.5, seen.Blue, 1.0 / 256, "blue");
        Assert.AreEqual(0, seen.Green, 1e-12, "no green");
    }

    /// <summary>
    /// This tests a blend by a weathering pattern: a floor beside a wall, blended from black to white by how
    /// closed in it is, shows exactly that.
    /// </summary>
    [TestMethod]
    public void TestABlendByWeatherIsShownItsSurroundings()
    {
        Material blended = Blended(new CrevicesPattern { Radius = 1 }, [0, 1], Glowing(Colors.Black), Glowing(Colors.White));
        Scene scene = new () { Background = new SolidPigment(Colors.Black), WeatheringSamples = 2048 };
        Plane floor = new () { Material = blended };
        Plane wall = new ()
        {
            Material = Glowing(Colors.Black), Transform = Transforms.Translate(0.3, 0, 0) * Transforms.RotateAroundZ(90)
        };

        foreach (Surface surface in new Surface[] { floor, wall })
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        Ray ray = new (new Point(0, 2, -1), new Vector(0, -2, 1).Unit);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        double seen = scene.GetHitColor(hit, 1).Red;
        double expected = ClosedIn(1, 0.3);

        Assert.AreEqual(expected, seen, 0.01 * expected + 1.0 / 256);
    }

    // How closed in a place on a floor is, with a wall the given distance off along +X; see TestWeathering.
    private static double ClosedIn(double reach, double toTheRight)
    {
        const int rings = 800;
        const int spokes = 800;
        double total = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            double theta = (ring + 0.5) / rings * Math.PI / 2;

            for (int spoke = 0; spoke < spokes; spoke++)
            {
                double phi = (spoke + 0.5) / spokes * 2 * Math.PI;
                double x = Math.Sin(theta) * Math.Cos(phi);
                double distance = x > 0 ? toTheRight / x : double.PositiveInfinity;

                total += (distance < reach ? 1 - distance / reach : 0) * Math.Cos(theta) * Math.Sin(theta) *
                         (Math.PI / 2 / rings) * (2 * Math.PI / spokes);
            }
        }

        return total / Math.PI;
    }

    /// <summary>
    /// This tests the words a scene says it with: a blend by a pattern with a map of named materials and
    /// written-out ones, after its own entries and before its transforms; a checker of two; and its materials
    /// settled before rendering as the surface's own is, the scene's ambient scaling reaching them.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysAMaterialIsBlended()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"blend-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // Looking straight down at the floor at x = 0.25, with a lamp beneath it so that only ambient light
            // arrives.
            Color Seen(string material, string context = "")
            {
                File.WriteAllText(path,
                    $"context {{ no gamma  {context} }}\n" +
                    "camera { location [0.25, 4, 0.3]  look at [0.25, 0, 0.3]  up [0, 0, 1]  field of view 0.5 }\n" +
                    "background Black\n" +
                    "distant light { direction [0, 1, 0] }\n" +
                    "Red = material { pigment Red  diffuse 0  specular 0 }\n" +
                    "Blue = material { pigment Blue  diffuse 0  specular 0 }\n" +
                    "plane { material " + material + " }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{material}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 5, Height = 5, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2);
            }

            // Ambient unsaid is a tenth, settled before rendering for each of the blend's materials.
            Color named = Seen("blend linear X gradient { [0, Red, 1, Blue] }");

            Assert.AreEqual(0.075, named.Red, 2e-3, "named, red");
            Assert.AreEqual(0.025, named.Blue, 2e-3, "named, blue");

            Color written = Seen("blend linear X gradient { [0, material { pigment Red  ambient 1  diffuse 0  specular 0 }, " +
                                 "1, material { pigment Blue  ambient 1  diffuse 0  specular 0 }] }");

            Assert.AreEqual(0.75, written.Red, 2e-3, "written out, red");

            Color scaled = Seen("blend linear X gradient { [0, Red, 1, Blue]  scale 2 }");

            Assert.AreEqual(0.1 * 0.875, scaled.Red, 2e-3, "the pattern stretched twice as far");
            Assert.AreEqual(0, Seen("blend linear X gradient { [0, Red, 1, Blue] }", "scale ambient by 0").Red, 1e-9,
                "the scene's ambient scaling reaches the blend's materials");
            Assert.AreEqual(0.1, Seen("blend checker { Red, Blue }").Red, 2e-3, "a checker of two");
            Assert.AreEqual(0.1, Seen("blend crevices { radius 0.1  [0, Red, 1, Blue] }").Red, 2e-3,
                "a weathering pattern, its own entry first: open ground, the first material whole");
            Assert.AreEqual(0.1, Seen("blend crevices { [0, Red, 1, Blue]  radius 0.1 }").Red, 2e-3,
                "a weathering pattern, its own entry after the materials");

            // One material is no blend, and is refused as the scene is read.
            TextWriter was = Console.Out;

            Console.SetOut(new StringWriter());

            try
            {
                File.WriteAllText(path,
                    "camera { location [0, 4, 0]  look at [0, 0, 0]  up [0, 0, 1] }\n" +
                    "Red = material { pigment Red }\n" +
                    "plane { material blend linear X gradient { [0, Red] } }\n");

                Assert.IsNull(new LanguageParser(path).Parse(), "one material");
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
