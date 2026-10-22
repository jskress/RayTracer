using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover wires: strands of metal, shaded as the round surfaces they are rather than as fibers,
/// stopping the light that falls on them, and -- finer than a ray is wide -- seen whole across their round.
/// </summary>
[TestClass]
public class TestWire
{
    // A metal seen only by its highlight: no ambient, no diffuse, nothing in the black background to mirror.
    private static Material Metal(double roughness, double anisotropy = 0) => new ()
    {
        Pigment = new SolidPigment(new Color(0.9, 0.8, 0.7)), Ambient = 0, Diffuse = 0,
        Specular = roughness > 0 ? 0 : 0.6, Metallic = 1, Reflective = 0.6, Roughness = roughness, Anisotropy = anisotropy
    };

    // A strand two long along X through the origin.
    private static Strand Wire(double width, Material material)
    {
        Strand strand = new () { Material = material };

        strand.Points.Add(new RibbonControlPoint { Position = new Point(-1, 0, 0), Width = width });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(1, 0, 0), Width = width });

        return strand;
    }

    private static Scene SceneOf(Light light, params Surface[] surfaces)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };

        scene.Lights.Add(light);

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.ArrangeSurfaces();

        return scene;
    }

    // A sun up and in front, square to the wire, so its highlight runs the wire's whole length.
    private static DistantLight Sun() => new () { Direction = new Vector(0, -0.5, 0.8).Unit, Color = Colors.White };

    // What a ray from in front, looking along +Z at the given height, sees.
    private static Color Seen(Scene scene, double y, double spread = 0) =>
        scene.GetColorFor(new Ray(new Point(0, y, -5), new Vector(0, 0, 1), 0, spread));

    /// <summary>
    /// This tests that a strand of metal is shaded as the round surface it is: wherever across it a ray
    /// passes, it shows exactly what a cylinder of its width shows there, highlight and all.
    /// </summary>
    [TestMethod]
    public void TestAWireIsShadedAsTheRoundSurfaceItIs()
    {
        foreach (double roughness in new[] { 0.0, 0.3 })
        {
            Material material = Metal(roughness);
            Scene wire = SceneOf(Sun(), Wire(0.2, material));
            Scene cylinder = SceneOf(Sun(), new Cylinder
            {
                Material = material, MinimumY = -1, MaximumY = 1,
                Transform = Transforms.RotateAroundZ(90) * Transforms.Scale(0.1, 1, 0.1)
            });
            double brightest = 0;

            foreach (double y in new[] { -0.09, -0.05, 0, 0.03, 0.06, 0.09 })
            {
                Color expected = Seen(cylinder, y);
                Color seen = Seen(wire, y);

                // A strand set keeps its points in single precision.
                brightest = Math.Max(brightest, expected.Red);
                Assert.AreEqual(expected.Red, seen.Red, 1e-6 * (1 + expected.Red), $"roughness {roughness}, {y} up");
                Assert.AreEqual(expected.Blue, seen.Blue, 1e-6 * (1 + expected.Blue), $"roughness {roughness}, {y} up");
            }

            Assert.IsGreaterThan(0.05, brightest, $"no highlight to compare at roughness {roughness}");
        }
    }

    /// <summary>
    /// This tests that a strand of anything but metal is still a fiber: lit from behind, hair glows with the
    /// light it lets through, where a wire, which lets none through, is dark.
    /// </summary>
    [TestMethod]
    public void TestOnlyAMetalStrandIsAWire()
    {
        DistantLight behind = new () { Direction = new Vector(0, -0.2, -1).Unit, Color = Colors.White };
        Material hair = new () { Melanin = Melanin.Shades["blond"], Ambient = 0 };

        Assert.IsGreaterThan(0.01, Seen(SceneOf(behind, Wire(0.2, hair)), 0).Red, "backlit hair");
        Assert.AreEqual(0, Seen(SceneOf(behind, Wire(0.2, Metal(0.3))), 0).Red, 1e-12, "a backlit wire");
    }

    /// <summary>
    /// This tests a wire's shadow: it stops all the light a line meets it with, where hair would send some
    /// onward, and of a ray wider than it, exactly its share.
    /// </summary>
    [TestMethod]
    public void TestAWireStopsTheLightItCovers()
    {
        Strand wire = Wire(0.01, Metal(0.3));

        wire.Transform = Transforms.Translate(0, 0.5, 0) * Transforms.RotateAroundY(90);

        Scene scene = SceneOf(Sun(), wire);
        RgbSpectrum line = scene.GetLightReaching<RgbSpectrum>(Point.Zero, new Vector(0, 1, 0), 10, 0, null, false, 0);
        RgbSpectrum wide = scene.GetLightReaching<RgbSpectrum>(Point.Zero, new Vector(0, 1, 0), 10, 0, null, false, 0.05);

        // Half a unit along a ray ten long, it has narrowed by a twentieth.
        Assert.AreEqual(0, line[0], 1e-12, "a line through a wire got past it");
        Assert.AreEqual(1 - 0.005 / (0.05 * 0.95), wide[0], 1e-6, "a wide ray lost other than the wire's share");
    }

    /// <summary>
    /// This tests a wire finer than the ray: seen whole across its round, it shows the same wherever across it
    /// a ray passes -- and what it shows, its share of the ray aside, is what the round of a wire wide enough to
    /// be seen place by place shows on average, lit from in front, from beside, from behind or leaning along
    /// it; polished, rough or wound; by its highlight, its diffuse light and what it mirrors.
    /// </summary>
    [TestMethod]
    public void TestAFineWireShowsItsRoundOnAverage()
    {
        Vector[] lights =
        [
            new (0, -0.5, 0.8), new (0, -1, 0.05), new (0, -0.5, -0.8), new (0.15, -0.5, 0.8)
        ];
        (string Name, Material Material)[] finishes =
        [
            ("polished", Metal(0)), ("rough", Metal(0.15)), ("rougher", Metal(0.35)), ("wound", Metal(0.3, 0.8)),
            ("matte, glowing a little", new Material
            {
                Pigment = new SolidPigment(new Color(0.9, 0.8, 0.7)), Ambient = 0.2, Diffuse = 0.8, Specular = 0,
                Metallic = 1, Reflective = 0, Roughness = 0.3, Emission = new Color(0.1, 0.1, 0.1)
            })
        ];

        // Looking square at the wire, and leaning along it.
        foreach (Vector looking in new Vector[] { new (0, 0, 1), new Vector(0.5, 0, 1).Unit })
        {
            foreach (Vector light in lights)
            {
                foreach ((string name, Material material) in finishes)
                {
                    DistantLight sun = new () { Direction = light.Unit, Color = Colors.White };
                    Scene fine = SceneOf(sun, Wire(0.01, material));
                    Scene wide = SceneOf(sun, Wire(0.2, material));
                    Ray ray = new (new Point(0, 0, 0) - looking * 5, looking, 0, 0.01);
                    double coverage = fine.Intersect(ray).OfType<HairIntersection>().Single().Coverage;
                    double middle = fine.GetColorFor(ray).Red / coverage;
                    const int across = 4000;
                    double average = 0;
                    string what = $"{name}, lit along {light}, looking along {looking}";

                    Assert.IsLessThan(1, coverage, what);
                    Assert.AreEqual(
                        middle,
                        fine.GetColorFor(new Ray(ray.Origin + new Vector(0, 0.017, 0), looking, 0, 0.01)).Red / coverage,
                        1e-9, $"{what}: across a fine wire");

                    for (int index = 0; index < across; index++)
                    {
                        Vector up = new (0, 0.1 * ((index + 0.5) / across * 2 - 1), 0);

                        average += wide.GetColorFor(new Ray(ray.Origin + up, looking)).Red / across;
                    }

                    // Within half a percent, or a ten-thousandth in the far tail of a highlight a few thousandths
                    // bright -- a rough wire lit from behind and seen at a slant, a percent off there.
                    Assert.AreEqual(average, middle, 0.005 * average + 1e-4,
                        $"{what}: the fine wire {middle} against its round's average {average}");
                }
            }
        }
    }

    /// <summary>
    /// This tests what a fine wire mirrors: a bright wall above and behind the eye, mirrored by places spread
    /// across the wire, comes to what the round of a wide wire mirrors on average.
    /// </summary>
    [TestMethod]
    public void TestAFineWireMirrorsWhatItsRoundMirrors()
    {
        foreach (double roughness in new[] { 0.0, 0.2 })
        {
            Material material = Metal(roughness);

            material.Specular = 0;
            material.ReflectionSamples = 4096;

            Plane wall = new ()
            {
                Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Emission = Colors.White },
                Transform = Transforms.Translate(0, 0, -6) * Transforms.RotateAroundX(90)
            };
            Scene fine = SceneOf(Sun(), Wire(0.01, material), wall);
            Scene wide = SceneOf(Sun(), Wire(0.2, material), wall);
            double middle = Seen(fine, 0, 0.01).Red / 0.2;
            const int across = 4000;
            double average = 0;

            // The wall stands behind the eye, so only what the wire mirrors shows it.
            for (int index = 0; index < across; index++)
                average += Seen(wide, 0.1 * ((index + 0.5) / across * 2 - 1)).Red / across;

            Assert.IsGreaterThan(0.05, average, $"nothing mirrored to compare at roughness {roughness}");
            Assert.AreEqual(average, middle, 0.01 * average, $"roughness {roughness}: {middle} against {average}");
        }
    }

    /// <summary>
    /// This tests a wound wire, brushed with no direction said: its grooves run round it, so its facets lean
    /// further along its length than round it.  A direction said is kept, on a wire as anywhere.
    /// </summary>
    [TestMethod]
    public void TestAWireBrushedWithNoDirectionIsWoundRoundItself()
    {
        Strand wire = Wire(0.2, Metal(0.3, 0.8));
        Scene scene = SceneOf(Sun(), wire);
        Ray ray = new (new Point(0, 0.03, -5), new Vector(0, 0, 1));
        List<Intersection> hits = scene.Intersect(ray);
        HairIntersection hit = hits.OfType<HairIntersection>().Single();

        hit.PrepareUsing(ray, hits);

        Facets wound = Facets.At(wire.Material, wire, hit.Point, hit.Normal, 0, default, hit);

        Assert.AreEqual(0, wound.Along.Dot(new Vector(1, 0, 0)), 1e-9, "the grooves of a wound wire run along it");

        wire.Material.BrushedAlong = new Vector(1, 0, 0);

        Facets drawn = Facets.At(wire.Material, wire, hit.Point, hit.Normal, 0, default, hit);

        Assert.AreEqual(1, Math.Abs(drawn.Along.Dot(new Vector(1, 0, 0))), 1e-9, "grooves said to run along it");
    }

    /// <summary>
    /// This tests that photons meet wires: a mirror tipped to throw a level sun straight down onto a floor
    /// lights the floor under it, but not the strip under a wire stretched across the light on its way down,
    /// where hair would have let most of it through.
    /// </summary>
    [TestMethod]
    public void TestAWireCastsAShadowInTheLightAMirrorThrows()
    {
        double Landed(bool wire)
        {
            Vector across = new (0, 0, 1);
            Vector down = new Vector(1, -1, 0).Unit;
            List<Surface> surfaces =
            [
                new Plane { Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0 } },
                new Parallelogram
                {
                    Point = new Point(0, 2, 0) - across * 0.5 - down * 0.5, Side1 = across, Side2 = down,
                    Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1 }
                }
            ];

            if (wire)
            {
                Strand strand = Wire(0.2, Metal(0.3));

                strand.Transform = Transforms.Translate(0, 1, 0) * Transforms.RotateAroundY(90);
                surfaces.Add(strand);
            }

            Scene scene = new () { CausticPhotons = 100_000 };

            foreach (Surface surface in surfaces)
            {
                surface.PrepareForRendering();
                scene.Surfaces.Add(surface);
            }

            scene.Lights.Add(new DistantLight { Direction = new Vector(1, 0, 0) });
            scene.ShootCausticPhotons();

            return scene.Caustics.Photons.Count(photon =>
                Math.Abs(photon.X) < 0.06 && Math.Abs(photon.Z) < 0.3 && Math.Abs(photon.Y) < 1e-6);
        }

        double open = Landed(false);

        Assert.IsGreaterThan(100, open, "the floor under the mirror should be lit");
        Assert.AreEqual(0, Landed(true), "photons got past a wire to the floor under it");
    }

    /// <summary>
    /// This tests that photons are not aimed at wires, whose glints are too fine a target.
    /// </summary>
    [TestMethod]
    public void TestPhotonsAreNotAimedAtWires()
    {
        Scene scene = SceneOf(new PointLight { Location = new Point(0, 5, -5) }, Wire(0.01, Metal(0)),
            new Plane { Material = new Material { Pigment = new SolidPigment(Colors.White) }, Transform = Transforms.Translate(0, -1, 0) });

        scene.CausticPhotons = 10_000;
        scene.ShootCausticPhotons();

        Assert.AreEqual(0, scene.Caustics?.Count ?? 0);
    }

    /// <summary>
    /// This tests that wires far finer than a ray, met one behind another along it, send rays only in
    /// proportion to how much of it each covers.  Each used to send a mirror's sixteen whatever its share,
    /// every one of which met the wires again, sending sixteen more at each: a piano's strings, seen from
    /// far enough off that a ray took in all of them, cost 52 minutes at 40 x 30.
    /// </summary>
    [TestMethod]
    public void TestFineWiresAlongARaySendRaysByTheirShareOfIt()
    {
        Material material = Metal(0);
        Plane wall = new ()
        {
            Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Emission = Colors.White },
            Transform = Transforms.Translate(0, 0, -6) * Transforms.RotateAroundX(90)
        };
        List<Surface> surfaces = [wall];

        // Forty, each a fiftieth of the ray's width where it meets it.
        for (int k = 0; k < 40; k++)
        {
            Strand wire = Wire(0.001, material);

            wire.Transform = Transforms.Translate(0, 0, k * 0.01);
            surfaces.Add(wire);
        }

        Scene scene = SceneOf(Sun(), surfaces.ToArray());

        scene.Statistics = new RayTracer.Renderer.Statistics();
        scene.GetColorFor(new Ray(new Point(0, 0, -5), new Vector(0, 0, 1), 0, 0.01), 2);

        // Sending every ray, the forty sent 640 mirrored ones, which met twenty wires each on their way back,
        // each taking a lamp's sample and sending one more: some 26,000 in all.  By their share, a few hundred.
        Assert.IsLessThan(5000, scene.Statistics.SceneRays, $"{scene.Statistics.SceneRays} rays for one through forty fine wires");
    }

    /// <summary>
    /// This tests that a fine wire sending its reflection by chance, being too fine to earn a ray of its
    /// own, shows on average what it does when it earns all its rays.
    /// </summary>
    [TestMethod]
    public void TestAFineWiresChanceReflectionIsRightOnAverage()
    {
        Plane wall = new ()
        {
            Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Emission = Colors.White },
            Transform = Transforms.Translate(0, 0, -6) * Transforms.RotateAroundX(90)
        };
        double[] averages = new double[2];

        // Sixteen rays a look, too many for a wire a fiftieth of the ray, against a thousand, which it earns.
        foreach ((int index, int samples) in new[] { (0, 16), (1, 1000) })
        {
            Material material = Metal(0);

            material.Specular = 0;
            material.ReflectionSamples = samples;

            Scene scene = SceneOf(Sun(), Wire(0.001, material), wall);
            const int along = 4000;

            for (int i = 0; i < along; i++)
            {
                Point from = new (1.8 * ((i + 0.5) / along) - 0.9, 0, -5);

                averages[index] += scene.GetColorFor(new Ray(from, new Vector(0, 0, 1), 0, 0.01)).Red / along;
            }
        }

        Assert.IsGreaterThan(0.001, averages[1], "nothing mirrored to compare");
        Assert.AreEqual(averages[1], averages[0], 0.08 * averages[1], $"{averages[0]} by chance against {averages[1]}");
    }
}
