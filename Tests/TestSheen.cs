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
/// These tests cover the sheen of cloth: the Charlie distribution's own arithmetic, the share it gives back,
/// and what that leaves the weave beneath, each held to sums worked out here.
/// </summary>
[TestClass]
public class TestSheen
{
    private static readonly Vector Up = new (0, 1, 0);

    private static Material Cloth(Color pigment, Sheen sheen) => new ()
    {
        Pigment = new SolidPigment(pigment), Ambient = 0, Diffuse = 1, Specular = 0, Sheen = sheen
    };

    private static Scene SceneOf(Material material, Light light = null)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };

        scene.Surfaces.Add(new Plane { Material = material });

        if (light is not null)
            scene.Lights.Add(light);

        return scene;
    }

    private static Vector At(double cos, double phi) =>
        new (Math.Sqrt(1 - cos * cos) * Math.Cos(phi), cos, Math.Sqrt(1 - cos * cos) * Math.Sin(phi));

    // The middle of the floor, seen from the given direction.
    private static Intersection Hit(Scene scene, Vector eye)
    {
        Ray ray = new (Point.Zero + eye, -eye);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return hit;
    }

    // Adds up a function of direction over the half of the sky above the floor.
    private static double OverTheSky(Func<Vector, double> function, int rings = 600, int spokes = 600)
    {
        double total = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            double theta = (ring + 0.5) / rings * Math.PI / 2;

            for (int spoke = 0; spoke < spokes; spoke++)
            {
                double phi = (spoke + 0.5) / spokes * 2 * Math.PI;
                Vector direction = new (
                    Math.Sin(theta) * Math.Cos(phi), Math.Cos(theta), Math.Sin(theta) * Math.Sin(phi));

                total += function(direction) * Math.Sin(theta) * (Math.PI / 2 / rings) * (2 * Math.PI / spokes);
            }
        }

        return total;
    }

    /// <summary>
    /// This tests the Charlie distribution: weighted by how squarely each facet faces out it comes to one, at
    /// every roughness, and it is densest at a graze, where the fibers stand.
    /// </summary>
    [TestMethod]
    public void TestTheFibersAddUpToOne()
    {
        foreach (double roughness in new[] { 0.3, 0.5, 1.0 })
        {
            Sheen sheen = new () { Roughness = roughness };
            double total = OverTheSky(facet => sheen.Distribution(facet.Y) * facet.Y);

            Assert.AreEqual(1, total, 2e-3, $"roughness {roughness}");
            Assert.IsGreaterThan(sheen.Distribution(0.9), sheen.Distribution(0.1), $"densest at a graze, {roughness}");
        }
    }

    /// <summary>
    /// This tests the share the sheen gives back of light from all round, which is what the weave beneath is
    /// left without: the table it is read from agrees with the sum worked out afresh here, it is never more than
    /// all of it, and it is more at a graze than square-on.
    /// </summary>
    [TestMethod]
    public void TestTheShareGivenBackIsTheSheensOwn()
    {
        foreach (double roughness in new[] { 0.3, 0.5, 0.8 })
        {
            Sheen sheen = new () { Roughness = roughness };

            foreach (double cos in new[] { 0.15, 0.5, 0.9 })
            {
                Vector eye = At(cos, 0.3);
                double expected = OverTheSky(light => sheen.Highlight(Up, eye, light) / Math.PI, 800, 400);

                Assert.AreEqual(expected, sheen.ShareAt(cos), 0.01 * expected + 1e-3, $"roughness {roughness} at {cos}");
                Assert.IsLessThan(1, sheen.ShareAt(cos), "no more than all of it");
            }

            Assert.IsGreaterThan(sheen.ShareAt(0.9), sheen.ShareAt(0.15), $"more at a graze, {roughness}");
        }
    }

    /// <summary>
    /// This tests a cloth lit by a lamp: what the weave beneath shows, less what the sheen gives back of the
    /// light from the eye's direction or the lamp's, whichever is more, and the sheen's own shine of the lamp, in
    /// the sheen's color.
    /// </summary>
    [TestMethod]
    public void TestALampLightsTheSheenAndTheWeave()
    {
        Sheen sheen = new () { Color = new Color(0.9, 0.6, 0.3), Roughness = 0.4 };
        Color pigment = new (0.4, 0.1, 0.1);

        foreach ((double cosEye, double cosLight, double phi) in new[] { (0.9, 0.8, 1.0), (0.2, 0.3, Math.PI), (0.5, 0.15, 2.5) })
        {
            Vector eye = At(cosEye, 0);
            Vector light = At(cosLight, phi);
            Scene scene = SceneOf(Cloth(pigment, sheen), new DistantLight { Direction = -light });
            Color seen = scene.GetHitColor(Hit(scene, eye), 2);
            double shine = sheen.Highlight(Up, eye, light);
            double beneath = sheen.Beneath(cosEye, cosLight);
            string what = $"seen at {cosEye}, lit at {cosLight}";

            Assert.AreEqual(pigment.Red * cosLight * beneath + 0.9 * shine, seen.Red, 1e-9, what);
            Assert.AreEqual(pigment.Blue * cosLight * beneath + 0.3 * shine, seen.Blue, 1e-9, what);
            Assert.AreEqual(
                Math.Min(1 - 0.9 * sheen.ShareAt(cosEye), 1 - 0.9 * sheen.ShareAt(cosLight)), beneath, 1e-12, what);
        }
    }

    /// <summary>
    /// This tests the shine coming up toward a graze, where the fibers stand edge-on to both the eye and the
    /// lamp -- a velvet ball's rim, lit from the eye's side: there the sheen is many times what it is seen and
    /// lit square-on.  Lit from the far side instead, the halfway way between them stands square to the cloth,
    /// where the fibers are fewest, and it is far less.
    /// </summary>
    [TestMethod]
    public void TestTheSheenComesUpTowardAGraze()
    {
        Sheen sheen = new () { Roughness = 0.4 };
        double edgeOn = sheen.Highlight(Up, At(0.1, 0), At(0.15, 0.2));

        Assert.IsGreaterThan(10 * sheen.Highlight(Up, At(0.95, 0), At(0.9, 0.2)), edgeOn, "square-on");
        Assert.IsGreaterThan(10 * sheen.Highlight(Up, At(0.1, 0), At(0.15, Math.PI)), edgeOn, "lit from the far side");
    }

    /// <summary>
    /// This tests light reaching the cloth from all round: ambient light and light bounced onto it are left to
    /// the weave less the sheen's share of them from the eye's direction.
    /// </summary>
    [TestMethod]
    public void TestLightFromAllRoundIsLeftToTheWeave()
    {
        // A gray sheen gives back half what a white one would, and the weave is left the rest.
        Sheen sheen = new () { Color = new Color(0.5, 0.5, 0.5), Roughness = 0.5 };
        Vector eye = At(0.6, 0);
        double left = 1 - 0.5 * sheen.ShareAt(0.6);
        Material ambient = Cloth(Colors.White, sheen);

        ambient.Ambient = 1;
        ambient.Diffuse = 0;

        // The lamp beneath the floor, so that nothing else of it arrives.
        Scene lit = SceneOf(ambient, new DistantLight { Direction = new Vector(0, 1, 0) });

        Assert.AreEqual(left, lit.GetHitColor(Hit(lit, eye), 2).Red, 1e-12, "ambient");

        // Bounced light, from a glowing ceiling every bounce finds.
        Scene scene = new () { Background = new SolidPigment(Colors.Black), DiffuseBounces = 1, DiffuseSamples = 16 };

        scene.Surfaces.Add(new Plane { Material = Cloth(Colors.White, sheen) });
        scene.Surfaces.Add(new Plane
        {
            Transform = Transforms.Translate(0, 1, 0),
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Emission = Colors.White
            }
        });

        Assert.AreEqual(left, scene.GetHitColor(Hit(scene, eye), 2).Red, 1e-9, "bounced");

        // What the weave mirrors of a white sky.
        Material mirror = Cloth(Colors.Black, sheen);

        mirror.Diffuse = 0;
        mirror.Reflective = 0.5;

        Scene sky = new () { Background = new SolidPigment(Colors.White) };

        sky.Surfaces.Add(new Plane { Material = mirror });

        Assert.AreEqual(0.5 * left, sky.GetHitColor(Hit(sky, eye), 2).Red, 1e-12, "mirrored");

        // Caustics: a mirror throws a level sun down onto the floor, which turns none of the photons.
        double Gathered(Sheen over)
        {
            Vector across = new (0, 0, 1);
            Vector down = new Vector(1, -1, 0).Unit;
            Scene thrown = new () { CausticPhotons = 50_000 };
            Plane floor = new () { Material = Cloth(Colors.White, over), NoCaustics = true };
            Parallelogram panel = new ()
            {
                Point = new Point(0, 2, 0) - across * 0.5 - down * 0.5, Side1 = across, Side2 = down,
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1
                }
            };

            foreach (Surface surface in new Surface[] { floor, panel })
            {
                surface.PrepareForRendering();
                thrown.Surfaces.Add(surface);
            }

            thrown.Lights.Add(new DistantLight { Direction = new Vector(1, 0, 0) });
            thrown.ShootCausticPhotons();

            Point target = new (0.05, 0, 0.1);
            Ray ray = new (target + eye, -eye);
            List<Intersection> hits = thrown.Intersect(ray);
            Intersection hit = hits.Hit();

            hit.PrepareUsing(ray, hits);

            return thrown.GetHitColor(hit, 4).Red;
        }

        double gathered = Gathered(null);

        Assert.IsGreaterThan(0.5, gathered, "the mirror should throw the sun down");
        Assert.AreEqual(gathered * left, Gathered(sheen), 1e-9 * gathered, "caustics");

        // Light come up from under the cloth is left to the weave as the rest is, beside the sheen's own shine.
        double Walked(Sheen over)
        {
            Scene slabbed = new () { Background = new SolidPigment(Colors.Black) };
            Cube slab = new ()
            {
                Transform = Transforms.Scale(50, 0.025, 50),
                Material = new Material
                {
                    Pigment = new SolidPigment(new Color(0.8, 0.8, 0.8)), Ambient = 0, Diffuse = 1, Specular = 0,
                    Subsurface = new Subsurface { Radius = new Color(0.02, 0.02, 0.02) }, Sheen = over
                }
            };

            slab.PrepareForRendering();
            slabbed.Surfaces.Add(slab);
            slabbed.Lights.Add(new DistantLight { Direction = new Vector(0, -0.8, 0.6) });

            Intersection hit = Hit(slabbed, eye);

            return slabbed.GetHitColor(hit, 2).Red;
        }

        double walked = Walked(null);
        Vector light = new (0, 0.8, -0.6);
        double shine = 0.5 * sheen.Highlight(Up, eye, light);

        Assert.IsGreaterThan(0.1, walked, "light should come up from under the slab");
        Assert.AreEqual(walked * left + shine, Walked(sheen), 2e-3 * walked, "come up from under it");
    }

    /// <summary>
    /// This tests the shine against Filament's cloth, written out here afresh from its two papers: Estevez and
    /// Kulla's distribution, (2 + 1/a) sin^(1/a) over 2 pi, a the square of the roughness, and Neubelt and
    /// Pettineo's 1 / 4(n.l + n.v - n.l n.v) -- times the lamp's cosine and pi, as every highlight here is.
    /// </summary>
    [TestMethod]
    public void TestTheShineIsFilamentsCloth()
    {
        foreach ((double roughness, double cosEye, double cosLight, double phi) in
                 new[] { (0.4, 0.3, 0.2, 0.5), (0.7, 0.8, 0.5, 2.0), (0.25, 0.15, 0.6, 3.0) })
        {
            Sheen sheen = new () { Roughness = roughness };
            Vector eye = At(cosEye, 0);
            Vector light = At(cosLight, phi);
            double a = roughness * roughness;
            double cosHalf = Up.Dot((eye + light).Unit);
            double sin2 = 1 - cosHalf * cosHalf;
            double d = (2 + 1 / a) * Math.Pow(sin2, 1 / (2 * a)) / (2 * Math.PI);
            double v = 1 / (4 * (cosLight + cosEye - cosLight * cosEye));

            Assert.AreEqual(Math.PI * d * v * cosLight, sheen.Highlight(Up, eye, light), 1e-12,
                $"roughness {roughness}, seen at {cosEye}, lit at {cosLight}");
        }
    }

    /// <summary>
    /// This tests where a sheen applies: on something opaque with no other skin.
    /// </summary>
    [TestMethod]
    public void TestASheenIsOnlyOnOpaqueCloth()
    {
        Assert.IsTrue(Cloth(Colors.Red, new Sheen()).SheenApplies);

        Material glass = Cloth(Colors.Red, new Sheen());

        glass.Transparency = 0.5;

        Assert.IsFalse(glass.SheenApplies, "glass");

        Material coated = Cloth(Colors.Red, new Sheen());

        coated.Coat = new Coat();

        Assert.IsFalse(coated.SheenApplies, "under a coat");
    }

    /// <summary>
    /// This tests the words a scene says it with, a lamp low behind the eye and the eye low over a cloth floor:
    /// a sheen shines there, in its own color and by its own roughness, the same in a spectral render; and what
    /// no sheen can be is refused.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysASurfaceIsCloth()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"sheen-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            Color Seen(string finish, string context = "")
            {
                File.WriteAllText(path,
                    $"context {{ no gamma  {context} }}\n" +
                    "camera { location [0, 0.6, -3]  look at [0, 0, 0]  field of view 1 }\n" +
                    "background Black\n" +
                    "distant light { direction [0, -0.3, 1] }\n" +
                    "plane { material { pigment [0.2, 0.05, 0.05]  ambient 0  diffuse 1  specular 0  " + finish + " } }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{finish}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2);
            }

            Color bare = Seen("");
            Color plain = Seen("sheen { }");
            Color colored = Seen("sheen { color [0.2, 0.6, 1]  roughness 0.3 }");
            Color spectral = Seen("sheen { color [0.2, 0.6, 1]  roughness 0.3 }", "spectral");

            Assert.IsGreaterThan(bare.Red + 0.02, plain.Red, "a sheen shines");
            Assert.IsGreaterThan(colored.Red, colored.Blue, "in its own color");
            Assert.AreEqual(colored.Red, spectral.Red, 2.0 / 255, "spectral, red");
            Assert.AreEqual(colored.Green, spectral.Green, 2.0 / 255, "spectral, green");
            Assert.AreEqual(colored.Blue, spectral.Blue, 2.0 / 255, "spectral, blue");

            Assert.ThrowsExactly<Lex.Parser.TokenException>(() => Seen("sheen { roughness 0 }"), "no roughness at all");
            Assert.ThrowsExactly<Lex.Parser.TokenException>(() => Seen("sheen { color 1.2 }"), "more than all of it");

            // A sheen and a coat, or a sheen and a film, are each a skin, and the scene is not read at all.
            TextWriter was = Console.Out;

            Console.SetOut(new StringWriter());

            try
            {
                foreach (string skin in new[] { "coat { }", "film { thickness 300 }" })
                {
                    File.WriteAllText(path,
                        "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1] }\n" +
                        $"plane {{ material {{ sheen {{ }}  {skin} }} }}\n");

                    Assert.IsNull(new LanguageParser(path).Parse(), $"a sheen and {skin}");
                }
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
