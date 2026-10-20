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
/// These tests cover a clear coat: lacquer over whatever a material says lies beneath it.  Each is held to
/// what Schlick's share and the facets' own arithmetic say, worked out here.
/// </summary>
[TestClass]
public class TestCoat
{
    private static readonly Vector Up = new (0, 1, 0);

    // What glass's index mirrors at a given angle from air, by Schlick: four percent square-on.
    private static double Schlick(double cos) => 0.04 + 0.96 * Math.Pow(1 - cos, 5);

    private static Material Coated(Color color, double diffuse, Coat coat = null) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = diffuse, Specular = 0, Coat = coat ?? new Coat()
    };

    private static Scene SceneOf(Material material, Color sky, Light light = null)
    {
        Scene scene = new () { Background = new SolidPigment(sky) };

        scene.Surfaces.Add(new Plane { Material = material });

        if (light is not null)
            scene.Lights.Add(light);

        return scene;
    }

    // The middle of the floor, met coming down from the -Z side at an angle whose cosine is given.
    private static Intersection Hit(Scene scene, double cos)
    {
        double sin = Math.Sqrt(1 - cos * cos);
        Ray ray = new (new Point(0, cos, -sin), new Vector(0, -cos, sin));
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return hit;
    }

    private static Vector EyeAt(double cos) => new (0, cos, -Math.Sqrt(1 - cos * cos));

    // Adds up a function of direction over the half of the sky above the floor.
    private static double OverTheSky(Func<Vector, double> function)
    {
        const int rings = 600;
        const int spokes = 600;
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
    /// This tests the coat's own arithmetic: it mirrors by its index as Schlick says, four percent
    /// square-on from air and less from under water; what gets through crosses it both ways; and of light
    /// from all round it mirrors the cosine-weighted average, which for Schlick's share is exactly
    /// F0 + (1 - F0) / 21.
    /// </summary>
    [TestMethod]
    public void TestTheCoatSharesLightByItsIndex()
    {
        Coat coat = new ();

        foreach (double cos in new[] { 1, 0.7, 0.3, 0.05 })
            Assert.AreEqual(Schlick(cos), coat.ReflectanceAt(cos, 1), 1e-12, $"mirrored at a cosine of {cos}");

        double underWater = (1.5 - 1.33) / (1.5 + 1.33);

        Assert.AreEqual(underWater * underWater, coat.ReflectanceAt(1, 1.33), 1e-12, "under water");
        Assert.AreEqual(0.96 * 0.96, coat.Through<RgbSpectrum>(1, 1, 1).ToColor().Red, 1e-12, "square-on");
        Assert.AreEqual(
            (1 - Schlick(0.5)) * (1 - Schlick(0.8)), coat.Through<RgbSpectrum>(0.5, 0.8, 1).ToColor().Green, 1e-12,
            "in at one angle and out at another");
        Assert.AreEqual(0.96, coat.ThroughOut<RgbSpectrum>(1, 1).ToColor().Blue, 1e-12, "out alone");
        Assert.AreEqual(0.04 + 0.96 / 21, coat.AverageReflectance(1), 1e-4, "from all round");
        Assert.AreEqual(
            (1 - Schlick(0.6)) * (1 - (0.04 + 0.96 / 21)), coat.ThroughFromAround<RgbSpectrum>(0.6, 1).ToColor().Red,
            1e-4, "in from all round and out to the eye");
    }

    /// <summary>
    /// This tests a tinted coat: seen square-on it gives what lies beneath its tint, once for the way in and
    /// out together; at a slant the light crosses more of it, bent by its index on the way in -- at 60
    /// degrees outside, 35 inside, so 1.22 times as far.
    /// </summary>
    [TestMethod]
    public void TestATintDeepensAtASlant()
    {
        Coat coat = new () { Tint = new Color(0.5, 0.8, 1) };
        double inside = Math.Sqrt(1 - 0.75 / (1.5 * 1.5));

        Assert.AreEqual(0.5 * 0.96 * 0.96, coat.Through<RgbSpectrum>(1, 1, 1).ToColor().Red, 1e-12, "square-on");
        Assert.AreEqual(0.96 * 0.96, coat.Through<RgbSpectrum>(1, 1, 1).ToColor().Blue, 1e-12, "where it is clear");
        Assert.AreEqual(
            Math.Pow(0.5, 1 / inside) * Math.Pow(1 - Schlick(0.5), 2),
            coat.Through<RgbSpectrum>(0.5, 0.5, 1).ToColor().Red, 1e-12, "at sixty degrees");
        Assert.AreEqual(0.96 * Math.Sqrt(0.5), coat.ThroughOut<RgbSpectrum>(1, 1).ToColor().Red, 1e-12, "out alone");
    }

    /// <summary>
    /// This tests what lies beneath a coat, lit by a lamp: a white matte floor shows the lamp's cosine, less
    /// what the coat mirrors of the light coming in and of the light going out.  The lamp is on the eye's
    /// side, far from where the coat would mirror it.
    /// </summary>
    [TestMethod]
    public void TestWhatLiesBeneathCrossesTheCoatBothWays()
    {
        Vector toLight = new (0, 0.8, -0.6);
        Light lamp = new DistantLight { Direction = -toLight };

        Scene bare = SceneOf(
            new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0 },
            Colors.Black, lamp);
        Scene coated = SceneOf(Coated(Colors.White, 1), Colors.Black, lamp);
        double seen = coated.GetHitColor(Hit(coated, 0.6), 2).Red;

        Assert.AreEqual(0.8, bare.GetHitColor(Hit(bare, 0.6), 2).Red, 1e-9, "bare, the lamp's cosine");
        Assert.AreEqual(0.8 * (1 - Schlick(0.6)) * (1 - Schlick(0.8)), seen, 1e-4, "under the coat");
    }

    /// <summary>
    /// This tests what the coat mirrors: over black, under a white sky, a smooth coat shows Schlick's share
    /// at every angle; a rough one, the share its facets give back between them, added up here over the
    /// whole sky; and the surface beneath may mirror too, through the coat -- smooth, or rough as metal
    /// flakes are.
    /// </summary>
    [TestMethod]
    public void TestTheCoatMirrorsByItsIndex()
    {
        foreach (double cos in new[] { 1, 0.6, 0.2 })
        {
            Scene smooth = SceneOf(Coated(Colors.Black, 0), Colors.White);

            Assert.AreEqual(Schlick(cos), smooth.GetHitColor(Hit(smooth, cos), 2).Red, 1e-9, $"smooth, at {cos}");
        }

        Material rough = Coated(Colors.Black, 0, new Coat { Roughness = 0.3 });

        rough.ReflectionSamples = 4096;

        Scene roughly = SceneOf(rough, Colors.White);
        double expected = OverTheSky(light => Microfacets.Highlight(
            Up, EyeAt(0.6), light, 0.09, cos => Schlick(cos))) / Math.PI;

        Assert.AreEqual(expected, roughly.GetHitColor(Hit(roughly, 0.6), 2).Red, 0.02 * expected, "rough");

        // A mirror of a half beneath a smooth coat: one ray, shared.
        Material mirror = Coated(Colors.Black, 0);

        mirror.Reflective = 0.5;

        Scene mirrored = SceneOf(mirror, Colors.White);

        Assert.AreEqual(
            Schlick(0.6) + 0.5 * Math.Pow(1 - Schlick(0.6), 2), mirrored.GetHitColor(Hit(mirrored, 0.6), 2).Red,
            1e-9, "a mirror beneath");

        // Metal flakes: a rough mirror beneath a smooth coat.
        Material flakes = Coated(Colors.Black, 0);

        flakes.Reflective = 0.5;
        flakes.Roughness = 0.4;
        flakes.ReflectionSamples = 4096;

        Scene flaked = SceneOf(flakes, Colors.White);
        double beneath = OverTheSky(light => Microfacets.Highlight(
            Up, EyeAt(0.6), light, 0.16, flakes.ReflectanceAt)) / Math.PI;
        double seen = flaked.GetHitColor(Hit(flaked, 0.6), 2).Red;

        Assert.AreEqual(Schlick(0.6) + beneath * Math.Pow(1 - Schlick(0.6), 2), seen, 0.02 * beneath, "flakes beneath");
    }

    /// <summary>
    /// This tests a smooth coat's highlight: a lamp at the eye's mirror shows as the glint of facets a
    /// twentieth rough, Phong's highlight is gone however the material sets it, and a lamp a ray can meet,
    /// which the coat's mirror finds, gives none.
    /// </summary>
    [TestMethod]
    public void TestASmoothCoatGlintsAtTheLampsMirror()
    {
        Vector mirror = new (0, 0.6, 0.8);
        Light lamp = new DistantLight { Direction = -mirror };
        double expected = Microfacets.Highlight(Up, EyeAt(0.6), mirror, 0.0025, cos => Schlick(cos));

        Scene plain = SceneOf(Coated(Colors.Black, 0), Colors.Black, lamp);
        Material phong = Coated(Colors.White, 0);

        phong.Specular = 0.9;
        phong.Shininess = 200;

        Scene withPhong = SceneOf(phong, Colors.Black, lamp);
        Scene seenLamp = SceneOf(
            Coated(Colors.Black, 0), Colors.Black, new SeenLamp { Location = new Point(0, 600, 800) });

        Assert.IsGreaterThan(100, expected, "the glint should be bright");
        Assert.AreEqual(expected, plain.GetHitColor(Hit(plain, 0.6), 2).Red, 1e-9 * expected, "the glint");
        Assert.AreEqual(expected, withPhong.GetHitColor(Hit(withPhong, 0.6), 2).Red, 1e-9 * expected, "no Phong");
        Assert.AreEqual(0, seenLamp.GetHitColor(Hit(seenLamp, 0.6), 2).Red, 1e-12, "a lamp a ray can meet");
    }

    /// <summary>
    /// This tests the light that does not come from one lamp's direction: what the surface beneath gives off
    /// of its own crosses the coat once, on its way out; ambient light, light bounced onto it, caustics
    /// landing on it and light come up from under it reach it from all round and cross twice -- in at the
    /// share light from all round gets through, out at the eye's.  Each is held against the same surface
    /// bare, the photons and walks being the same.
    /// </summary>
    [TestMethod]
    public void TestLightFromAllRoundCrossesTheCoatTwice()
    {
        Coat coat = new ();
        double outOnce = 1 - Schlick(0.6);
        double fromAround = outOnce * (1 - coat.AverageReflectance(1));

        // Its own glow.
        Material glowing = Coated(Colors.White, 0, coat);

        glowing.Emission = Colors.White;

        Scene glow = SceneOf(glowing, Colors.Black);

        Assert.AreEqual(outOnce, glow.GetHitColor(Hit(glow, 0.6), 2).Red, 1e-12, "its own glow");

        // Ambient, with the lamp beneath the floor so that nothing else of it arrives.
        Material ambient = Coated(Colors.White, 0, coat);

        ambient.Ambient = 1;

        Scene lit = SceneOf(ambient, Colors.Black, new DistantLight { Direction = new Vector(0, 1, 0) });

        Assert.AreEqual(fromAround, lit.GetHitColor(Hit(lit, 0.6), 2).Red, 1e-12, "ambient");

        // Bounced light, from a glowing ceiling that every bounce finds; the coat also mirrors the ceiling.
        double Bounced(Material floor)
        {
            Scene scene = new () { Background = new SolidPigment(Colors.Black), DiffuseBounces = 1, DiffuseSamples = 16 };

            scene.Surfaces.Add(new Plane { Material = floor });
            scene.Surfaces.Add(new Plane
            {
                Transform = Transforms.Translate(0, 1, 0),
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
                    Emission = Colors.White
                }
            });

            return scene.GetHitColor(Hit(scene, 0.6), 2).Red;
        }

        double bounced = Bounced(new Material
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0
        });

        Assert.AreEqual(1, bounced, 1e-9, "bounced, bare");
        Assert.AreEqual(Schlick(0.6) + bounced * fromAround, Bounced(Coated(Colors.White, 1, coat)), 1e-9, "bounced");

        // Caustics: a mirror throws a level sun down onto the floor, which looks the same to the photons
        // either way, saying it turns none.
        double Gathered(Material floor)
        {
            Vector across = new (0, 0, 1);
            Vector down = new Vector(1, -1, 0).Unit;
            Scene scene = new () { CausticPhotons = 50_000 };
            Plane plane = new () { Material = floor, NoCaustics = true };
            Parallelogram mirror = new ()
            {
                Point = new Point(0, 2, 0) - across * 0.5 - down * 0.5, Side1 = across, Side2 = down,
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1
                }
            };

            foreach (Surface surface in new Surface[] { plane, mirror })
            {
                surface.PrepareForRendering();
                scene.Surfaces.Add(surface);
            }

            scene.Lights.Add(new DistantLight { Direction = new Vector(1, 0, 0) });
            scene.ShootCausticPhotons();

            // Looked at from the side, so that what the coat mirrors is the empty sky and not the mirror.
            Point target = new (0.05, 0, 0.1);
            Ray ray = new (target + EyeAt(0.6), -EyeAt(0.6));
            List<Intersection> hits = scene.Intersect(ray);
            Intersection hit = hits.Hit();

            hit.PrepareUsing(ray, hits);

            return scene.GetHitColor(hit, 4).Red;
        }

        double gathered = Gathered(new Material
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0
        });

        Assert.IsGreaterThan(0.5, gathered, "the mirror should throw the sun down");
        Assert.AreEqual(gathered * fromAround, Gathered(Coated(Colors.White, 1, coat)), 1e-9 * gathered, "caustics");

        // Light come up from under the surface, in a slab lit from the eye's side, far from the coat's glint.
        double Walked(Coat over)
        {
            Scene scene = new () { Background = new SolidPigment(Colors.Black) };
            Cube slab = new ()
            {
                Transform = Transforms.Scale(50, 0.025, 50),
                Material = new Material
                {
                    Pigment = new SolidPigment(new Color(0.8, 0.8, 0.8)), Ambient = 0, Diffuse = 1, Specular = 0,
                    Subsurface = new Subsurface { Radius = new Color(0.02, 0.02, 0.02) }, Coat = over
                }
            };

            slab.PrepareForRendering();
            scene.Surfaces.Add(slab);
            scene.Lights.Add(new DistantLight { Direction = new Vector(0, -0.8, 0.6) });

            return scene.GetHitColor(Hit(scene, 0.6), 2).Red;
        }

        double walked = Walked(null);

        Assert.IsGreaterThan(0.1, walked, "light should come up from under the slab");
        Assert.AreEqual(walked * fromAround, Walked(coat), 1e-4 * walked, "come up from under it");
    }

    /// <summary>
    /// This tests the coat where a scene stops following rays worth too little of their pixel: what a coat
    /// over black mirrors is worth what the coat mirrors, and is followed.
    /// </summary>
    [TestMethod]
    public void TestACoatsMirrorIsWorthFollowing()
    {
        Scene scene = SceneOf(Coated(Colors.Black, 0), Colors.White);

        scene.Bailout = 1.0 / 1024;

        Assert.AreEqual(Schlick(0.6), scene.GetHitColor(Hit(scene, 0.6), 2).Red, 1e-9);
    }

    /// <summary>
    /// This tests a coated ball seen from inside: the coat is on its outside, so its glow is seen bare.
    /// </summary>
    [TestMethod]
    public void TestACoatIsOnTheOutside()
    {
        Material glowing = Coated(Colors.White, 0);

        glowing.Emission = Colors.White;

        Scene scene = new () { Background = new SolidPigment(Colors.Black) };
        Sphere ball = new () { Material = glowing };

        scene.Surfaces.Add(ball);

        Ray ray = new (Point.Zero, new Vector(0.3, 0.4, 0.5).Unit);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        Assert.IsTrue(hit.Inside, "the ray should meet the ball from inside");
        Assert.AreEqual(1, scene.GetHitColor(hit, 2).Red, 1e-12);
    }

    /// <summary>
    /// This tests a smooth coat under an area light, its 25 places half a unit apart and ten units off: the
    /// window shows as one even patch rather than as 25 dots, each place's glint spread over its share of
    /// the face (a step along each axis, over the distance), and the middle is exactly that.
    /// </summary>
    [TestMethod]
    public void TestASmoothCoatShowsAnAreaLightAsOnePatch()
    {
        AreaLight window = new ()
        {
            Location = new Point(0, 10, 0), Axis1 = new Vector(2, 0, 0), Axis2 = new Vector(0, 0, 2),
            USteps = 5, VSteps = 5, Jitter = false
        };
        Scene scene = SceneOf(Coated(Colors.Black, 0), Colors.Black, window);
        Point eye = new (0, 10, 0);

        Intersection HitAt(double x)
        {
            Point target = new (x, 0, 0.013);
            Ray ray = new (eye, (target - eye).Unit);
            List<Intersection> hits = scene.Intersect(ray);
            Intersection hit = hits.Hit();

            hit.PrepareUsing(ray, hits);

            return hit;
        }

        double[] across = Enumerable.Range(-5, 11).Select(step => scene.GetHitColor(HitAt(step * 0.03), 2).Red).ToArray();

        Assert.IsLessThan(1.05, across.Max() / across.Min(), $"the middle of the window: {string.Join(", ", across)}");

        // Worked out from where the floor is lit, just off it, as the lamp's places are looked at from.
        Intersection middle = HitAt(0);
        double expected = 0;

        for (int u = 0; u < 5; u++)
        {
            for (int v = 0; v < 5; v++)
            {
                Vector toPlace = new Point(-1 + u * 0.5, 10, -1 + v * 0.5) - middle.LitPoint;
                double spread = Math.Sqrt(0.5 * 0.5 + 0.5 * 0.5) / toPlace.Magnitude;
                double alpha = Math.Sqrt(0.0025 * 0.0025 + spread * spread / 4);

                expected += Microfacets.Highlight(Up, middle.Eye, toPlace.Unit, alpha, cos => Schlick(cos)) / 25;
            }
        }

        double seen = scene.GetHitColor(middle, 2).Red;

        Assert.AreEqual(expected, seen, 1e-9 * expected, $"the middle: {seen} against {expected}");
    }

    /// <summary>
    /// This tests the coat met by a ray a rough surface has spread: a smooth coat is made as rough as that
    /// surface, and nothing beneath it is unless asked; a rougher coat stays as it is.
    /// </summary>
    [TestMethod]
    public void TestASmoothCoatSeenInARoughSurfaceIsMadeRough()
    {
        Material material = Coated(Colors.Red, 1);
        Material made = material.RoughAs(0.3, false);

        Assert.AreEqual(0.3, made.Coat.Roughness, 1e-12, "the coat");
        Assert.AreEqual(0, made.Roughness, "beneath");
        Assert.AreSame(made, material.RoughAs(0.3, false), "made once");
        Assert.AreEqual(0, material.Coat.Roughness, "the coat it was made from");
        Assert.AreEqual(0.3, material.RoughAs(0.3).Roughness, 1e-12, "beneath, when asked");
        Assert.AreEqual(0.5, Coated(Colors.Red, 1, new Coat { Roughness = 0.5 }).RoughAs(0.3, false).Coat.Roughness,
            1e-12, "a rougher coat");
    }

    /// <summary>
    /// This tests where a coat applies: on something opaque, not on glass, which it would only make more
    /// glass of.
    /// </summary>
    [TestMethod]
    public void TestACoatIsOnlyOverSomethingOpaque()
    {
        Assert.IsTrue(Coated(Colors.Red, 1).CoatApplies);

        Material glass = Coated(Colors.Red, 1);

        glass.Transparency = 0.9;

        Assert.IsFalse(glass.CoatApplies);
    }

    /// <summary>
    /// This tests a coat throwing the sun: a coated black panel tipped at 45 degrees throws a level sun
    /// straight down, as much of it as the coat mirrors there -- 4.2% -- exactly what a black panel mirroring
    /// glass's four percent by Fresnel throws; and over a mirror, the two shares together.
    /// </summary>
    [TestMethod]
    public void TestACoatThrowsTheSunAsGlassWould()
    {
        double Thrown(Material panel)
        {
            Vector across = new (0, 0, 1);
            Vector down = new Vector(1, -1, 0).Unit;
            Point center = new (0, 2, 0);
            Scene scene = new () { CausticPhotons = 200_000 };
            Plane floor = new ()
            {
                Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0 }
            };
            Parallelogram mirror = new ()
            {
                Point = center - across * 0.5 - down * 0.5, Side1 = across, Side2 = down, Material = panel
            };

            foreach (Surface surface in new Surface[] { floor, mirror })
            {
                surface.PrepareForRendering();
                scene.Surfaces.Add(surface);
            }

            scene.Lights.Add(new DistantLight { Direction = new Vector(1, 0, 0) });
            scene.ShootCausticPhotons();

            Ray ray = new (new Point(0.05, 1, 0.1), Directions.Down);
            List<Intersection> hits = scene.Intersect(ray);
            Intersection hit = hits.Hit();

            hit.PrepareUsing(ray, hits);

            return scene.GetHitColor(hit, 4).Red;
        }

        double coated = Thrown(Coated(Colors.Black, 0));
        double glassy = Thrown(new Material
        {
            Pigment = new SolidPigment(Colors.Black), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 0.04,
            Fresnel = true
        });

        Assert.AreEqual(Schlick(Math.Sqrt(0.5)), coated, 0.03 * coated, "the coat");
        Assert.AreEqual(glassy, coated, 1e-9 * glassy, "as glass's four percent");

        // A mirror of a half beneath the coat, all but smooth, so that a photon goes one way or the other by
        // chance: what lands is the coat's share and the mirror's through the coat, both ways.
        Material beneath = Coated(Colors.Black, 0);

        beneath.Reflective = 0.5;
        beneath.Roughness = 0.01;

        double cos = Math.Sqrt(0.5);
        double expected = Schlick(cos) + beneath.ReflectanceAt(cos) * Math.Pow(1 - Schlick(cos), 2);

        Assert.AreEqual(expected, Thrown(beneath), 0.03 * expected, "a mirror beneath");
    }

    /// <summary>
    /// This tests the words a scene says it with, looking straight down at a coated floor under a white sky:
    /// a coat mirrors four percent, under its own index less, rough a little more; `metallic` may be followed
    /// by one; a tint darkens what lies beneath, the same in a spectral render; and what no coat can be is
    /// refused.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysASurfaceIsCoated()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"coat-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            Color Seen(string finish, string sky = "White", string lamp = "", string context = "")
            {
                File.WriteAllText(path,
                    $"context {{ no gamma  {context} }}\n" +
                    "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 1 }\n" +
                    $"background {sky}\n" + lamp +
                    "plane { material { ambient 0  specular 0  " + finish + " } }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{finish}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2);
            }

            const string black = "pigment Black  diffuse 0  ";
            double underWater = (1.33 - 1) / (1.33 + 1);

            Assert.AreEqual(0.04, Seen(black + "coat { }").Red, 2e-4, "a plain coat");
            Assert.AreEqual(underWater * underWater, Seen(black + "coat { ior 1.33 }").Red, 2e-4, "its own index");
            Assert.AreEqual(underWater * underWater, Seen(black + "coat { index of refraction 1.33 }").Red, 2e-4,
                "its index in full");
            Assert.AreEqual(0.04, Seen(black + "coat { roughness 0.2 }").Red, 0.01, "a rough coat");
            Assert.AreEqual(0.04, Seen(black + "metallic coat { }").Red, 2e-4, "after metallic");

            // A tint, under a lamp a little off overhead, where the coat's glint is next to nothing.
            const string lamp = "distant light { direction [0.3, -1, 0] }\n";
            const string white = "pigment White  diffuse 1  ";
            double cosLight = 1 / Math.Sqrt(1.09);
            double inside = Math.Sqrt(1 - (1 - cosLight * cosLight) / (1.5 * 1.5));
            double clear = Seen(white + "coat { }", "Black", lamp).Red;
            Color tinted = Seen(white + "coat { tint [0.5, 0.8, 1] }", "Black", lamp);
            Color spectral = Seen(white + "coat { tint [0.5, 0.8, 1] }", "Black", lamp, "spectral");

            Assert.AreEqual(Math.Pow(0.5, (1 + 1 / inside) / 2), tinted.Red / clear, 2e-3, "a tint");
            Assert.AreEqual(tinted.Red, spectral.Red, 2.0 / 255, "spectral, red");
            Assert.AreEqual(tinted.Green, spectral.Green, 2.0 / 255, "spectral, green");
            Assert.AreEqual(tinted.Blue, spectral.Blue, 2.0 / 255, "spectral, blue");

            Assert.ThrowsExactly<TokenException>(() => Seen("coat { roughness 2 }"), "too rough");
            Assert.ThrowsExactly<TokenException>(() => Seen("coat { ior 0.5 }"), "an index below one");
            Assert.ThrowsExactly<TokenException>(() => Seen("coat { tint 1.5 }"), "a tint giving more than it takes");

            // A film and a coat are each a skin, and a scene may say only one; it is not read at all.
            File.WriteAllText(path,
                "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1] }\n" +
                "plane { material { film { thickness 300 }  coat { } } }\n");

            TextWriter was = Console.Out;

            Console.SetOut(new StringWriter());

            try
            {
                Assert.IsNull(new LanguageParser(path).Parse(), "a film and a coat");

                File.WriteAllText(path,
                    "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1] }\n" +
                    "plane { material { film { thickness 300 } } }\n");

                Assert.IsNotNull(new LanguageParser(path).Parse(), "the film alone");
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

    /// <summary>
    /// A lamp that says a ray can meet it.
    /// </summary>
    private class SeenLamp : PointLight
    {
        public override bool CanBeSeen => true;
    }
}
