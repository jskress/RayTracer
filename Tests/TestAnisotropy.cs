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
/// These tests cover brushed surfaces: facets leaning further across the grooves than along them.  Each is held
/// to the facets' own arithmetic, the round case to the old, and the brushing to the way it was said to run.
/// </summary>
[TestClass]
public class TestAnisotropy
{
    private static readonly Vector Up = new (0, 1, 0);

    // A floor brushed along X, seen through its own frame: along the grooves is X, across them -Z, up is Y.
    private static Facets Brushed(double roughness, double anisotropy)
    {
        (double along, double across) = Facets.AlphasFor(roughness, anisotropy);

        return new Facets(Up, new Vector(1, 0, 0), along, across);
    }

    private static Material Steel(double roughness, double anisotropy) => new ()
    {
        Pigment = new SolidPigment(Colors.Black), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 0.6,
        Roughness = roughness, Anisotropy = anisotropy
    };

    private static Vector At(double theta, double phi) =>
        new (Math.Sin(theta) * Math.Cos(phi), Math.Cos(theta), Math.Sin(theta) * Math.Sin(phi));

    /// <summary>
    /// This tests the leans each way: their product is the square of the roughness, so it stays the average,
    /// nought is alike every way, and one is a tenth as far along the grooves as across.
    /// </summary>
    [TestMethod]
    public void TestTheRoughnessStaysTheAverage()
    {
        foreach (double anisotropy in new[] { 0, 0.3, 0.7, 1 })
        {
            (double along, double across) = Facets.AlphasFor(0.4, anisotropy);

            Assert.AreEqual(0.16 * 0.16, along * across, 1e-12, $"at {anisotropy}");
        }

        (double roundAlong, double roundAcross) = Facets.AlphasFor(0.4, 0);
        (double fullAlong, double fullAcross) = Facets.AlphasFor(0.4, 1);

        Assert.AreEqual(roundAlong, roundAcross, 1e-15, "alike every way");
        Assert.AreEqual(0.1, fullAlong / fullAcross, 1e-12, "a tenth");
    }

    /// <summary>
    /// This tests brushed facets that are not brushed at all against the facets every rough surface has had:
    /// the same density, masking, odds and highlight, to the last few bits.
    /// </summary>
    [TestMethod]
    public void TestRoundFacetsAreTheOldFacets()
    {
        Facets round = Brushed(0.45, 0);
        double alpha = 0.45 * 0.45;
        Vector eye = At(0.7, 0.4);

        foreach ((double theta, double phi) in new[] { (0.1, 0.3), (0.5, 2.0), (1.2, 4.0), (0.9, 5.5) })
        {
            Vector direction = At(theta, phi);
            Vector halfway = (eye + direction).Unit;
            Func<double, double> reflectance = cos => 0.04 + 0.96 * Math.Pow(1 - cos, 5);

            Assert.AreEqual(Microfacets.Distribution(halfway.Y, alpha), round.Distribution(round.ToLocal(halfway)),
                1e-12 * Microfacets.Distribution(halfway.Y, alpha), "density");
            Assert.AreEqual(Microfacets.Masking(direction.Y, alpha), round.Masking(round.ToLocal(direction)), 1e-12,
                "masking");
            Assert.AreEqual(Microfacets.DirectionOdds(Up, eye, direction, alpha), round.DirectionOdds(eye, direction),
                1e-12 * Microfacets.DirectionOdds(Up, eye, direction, alpha), "odds");
            Assert.AreEqual(Microfacets.Highlight(Up, eye, direction, alpha, reflectance),
                round.Highlight(eye, direction, reflectance),
                1e-12 * Microfacets.Highlight(Up, eye, direction, alpha, reflectance), "highlight");
        }
    }

    /// <summary>
    /// This tests the stretched distribution: weighted by how squarely each facet faces out, it comes to one,
    /// however unevenly the facets lean -- added up here over the hemisphere.
    /// </summary>
    [TestMethod]
    public void TestBrushedFacetsAddUpToOne()
    {
        foreach ((double roughness, double anisotropy) in new[] { (0.3, 0.9), (0.6, 0.5), (0.8, 1.0) })
        {
            Facets facets = Brushed(roughness, anisotropy);
            const int rings = 2000;
            const int spokes = 720;
            double total = 0;

            for (int ring = 0; ring < rings; ring++)
            {
                double theta = (ring + 0.5) / rings * Math.PI / 2;

                for (int spoke = 0; spoke < spokes; spoke++)
                {
                    double phi = (spoke + 0.5) / spokes * 2 * Math.PI;
                    Vector facet = facets.ToLocal(At(theta, phi));

                    total += facets.Distribution(facet) * Math.Cos(theta) * Math.Sin(theta) *
                             (Math.PI / 2 / rings) * (2 * Math.PI / spokes);
                }
            }

            Assert.AreEqual(1, total, 2e-3, $"roughness {roughness}, anisotropy {anisotropy}");
        }
    }

    /// <summary>
    /// This tests that the brushed facets the eye's rays are sent off are the facets the highlight is worked
    /// out from: a brushed floor under a patch of white sky mirrors what the highlight's formula, added up over
    /// the patch, says it should -- with the patch off to one side of the mirror direction across the grooves,
    /// and as far off along them, where far less is seen.
    /// </summary>
    [TestMethod]
    public void TestTheRaysAgreeWithTheFacets()
    {
        Material material = Steel(0.35, 0.9);

        material.ReflectionSamples = 8192;

        Vector eye = new (0, 0.6, -0.8);
        Vector mirror = new (0, 0.6, 0.8);
        Facets facets = new (Up, new Vector(1, 0, 0), Facets.AlphasFor(0.35, 0.9).Along, Facets.AlphasFor(0.35, 0.9).Across);
        Dictionary<string, double> seen = [];

        foreach ((string name, Vector toward) in new[]
                 {
                     ("across", (mirror + new Vector(0, 0.25, -0.2)).Unit),
                     ("along", (mirror + new Vector(0.24, 0, 0)).Unit)
                 })
        {
            const double halfAngle = 10 * Math.PI / 180;
            Scene scene = new () { Background = new CapPigment(toward, halfAngle) };
            Plane floor = new () { Material = material };

            scene.Surfaces.Add(floor);

            Ray ray = new (new Point(0, 0.6, -0.8), new Vector(0, -0.6, 0.8));
            Intersection hit = new (floor, 1);

            hit.PrepareUsing(ray, [hit]);

            double reflected = scene.GetReflectionColor(hit, 2).Red;
            double expected = OverCap(toward, halfAngle, light => facets.Highlight(eye, light, material.ReflectanceAt)) /
                              Math.PI;

            Assert.IsGreaterThan(0.002, expected, $"nothing to compare {name}: {expected}");
            Assert.AreEqual(expected, reflected, 0.03 * expected, name);
            seen[name] = reflected;
        }

        Assert.IsGreaterThan(5 * seen["along"], seen["across"], "across the grooves should show far more");
    }

    /// <summary>
    /// This tests the streak a lamp makes in a brushed floor, seen from straight over it with the lamp
    /// overhead: it reaches far across the grooves and hardly at all along them, and a floor brushed round an
    /// axis streaks out from it.
    /// </summary>
    [TestMethod]
    public void TestALampStreaksAcrossTheGrooves()
    {
        double SeenAt(Material material, double x, double z, Matrix placed = null)
        {
            Scene scene = new () { Background = new SolidPigment(Colors.Black) };
            Plane floor = new () { Material = material, Transform = placed ?? Matrix.Identity };

            scene.Surfaces.Add(floor);
            scene.Lights.Add(new PointLight { Location = new Point(0, 10, 0) });

            Point target = new (x, 0, z);
            Point eye = new (0, 10, 0);
            Ray ray = new (eye, (target - eye).Unit);
            List<Intersection> hits = scene.Intersect(ray);
            Intersection hit = hits.Hit();

            hit.PrepareUsing(ray, hits);

            return scene.GetHitColor(hit, 2).Red;
        }

        Material brushed = Steel(0.3, 0.9);

        Assert.IsGreaterThan(20 * SeenAt(brushed, 1.5, 0), SeenAt(brushed, 0, 1.5), "brushed along X");

        Material round = Steel(0.3, 0);

        Assert.AreEqual(SeenAt(round, 1.5, 0), SeenAt(round, 0, 1.5), 1e-9, "alike every way");

        // Brushed round Y with the floor's middle ten units off along +X, the grooves near the lamp are circles
        // about that middle, running along Z: so the streak runs along X, toward and away from it.
        Material turned = Steel(0.3, 0.9);

        turned.BrushedAround = new Vector(0, 1, 0);

        Matrix offCenter = Transforms.Translate(10, 0, 0);

        Assert.IsGreaterThan(
            20 * SeenAt(turned, 0, 1.5, offCenter), SeenAt(turned, 1.5, 0, offCenter), "brushed round Y");
    }

    /// <summary>
    /// This tests which way the grooves run where a ray meets a surface: as said in the surface's own space,
    /// turned with it, laid flat on it, and round an axis as circles.
    /// </summary>
    [TestMethod]
    public void TestTheGroovesRunTheWayTheySaid()
    {
        Material along = Steel(0.3, 0.9);

        along.BrushedAlong = new Vector(1, 0, 1);

        Plane flat = new () { Material = along };
        Plane turned = new () { Material = along, Transform = Transforms.RotateAroundY(45) };
        Facets onFlat = Facets.At(along, flat, Point.Zero, Up, 0, default);
        Facets onTurned = Facets.At(along, turned, Point.Zero, Up, 0, default);

        Assert.AreEqual(1, Math.Abs(onFlat.Along.Dot(new Vector(1, 0, 1).Unit)), 1e-12, "as said");
        Assert.AreEqual(1, Math.Abs(onTurned.Along.Dot(new Vector(1, 0, 0))), 1e-12, "turned with the surface");

        // A direction tilted out of the floor is laid flat on it.
        Material tilted = Steel(0.3, 0.9);

        tilted.BrushedAlong = new Vector(0, 1, 1);

        Assert.AreEqual(1, Math.Abs(Facets.At(tilted, flat, Point.Zero, Up, 0, default).Along.Z), 1e-12, "laid flat");

        Material round = Steel(0.3, 0.9);

        round.BrushedAround = new Vector(0, 1, 0);

        Assert.AreEqual(1, Math.Abs(Facets.At(round, flat, new Point(2, 0, 0), Up, 0, default).Along.Z), 1e-12,
            "a circle, at +X");
        Assert.AreEqual(1, Math.Abs(Facets.At(round, flat, new Point(0, 0, -3), Up, 0, default).Along.X), 1e-12,
            "a circle, at -Z");
        Assert.IsNull(Facets.At(Steel(0.3, 0), flat, Point.Zero, Up, 0, default), "not brushed");
    }

    /// <summary>
    /// This tests a glowing lantern seen in a brushed floor: shared between its own samples and the floor's
    /// rays by the brushed facets' odds, it comes to what the rays alone find looking long enough, as it does
    /// in a floor rough alike every way (see TestRoughness).  Shared by the wrong odds, it would not.  The
    /// floor is sharp enough along its grooves that both ways matter: at a roughness of 0.3 the lantern's own
    /// samples took nearly all of it, and wrong odds went unseen.
    /// </summary>
    [TestMethod]
    public void TestALanternInABrushedFloorIsSharedFairly()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"brushed-lantern-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            double[,] Rendered(double index, int samples)
            {
                string path = Path.Combine(directory, "lantern.igl");
                string output = Path.Combine(directory, $"out-{Guid.NewGuid():N}.png");

                File.WriteAllText(path,
                    $"context {{ no gamma  color depth 16  reflection samples {samples} }}\n" +
                    "camera { location [0, 2, -6]  look at [0, 0, 1] }\n" +
                    "background Black\n" +
                    "plane { material { pigment Black  ambient 0  diffuse 0  roughness 0.15  anisotropy 0.9  reflective 1 } }\n" +
                    "sphere {\n" +
                    "    material {\n" +
                    "        pigment White  ambient 0  diffuse 0  specular 0  transparency 1\n" +
                    $"        interior {{ ior {index}  medium {{ emission [0.3, 0.27, 0.21]\n" +
                    "            density function { max(0, 1 - sqrt(x * x + y * y + z * z)) * 40 } } }\n" +
                    "    }\n" +
                    "    no shadow\n" +
                    "    gives light samples 24\n" +
                    "    scale 0.4\n" +
                    "    translate [0, 1.2, 4]\n" +
                    "}\n");

                new LanguageParser(path).Parse().Render(new RenderOptions
                {
                    OutputFileName = output, Width = 64, Height = 48, ProgressStyleText = "none"
                });

                Canvas canvas = new ImageFile(output).Load()[0];
                double[,] red = new double[canvas.Height, canvas.Width];

                for (int y = 0; y < canvas.Height; y++)
                {
                    for (int x = 0; x < canvas.Width; x++)
                        red[y, x] = canvas.GetPixel(x, y).Red;
                }

                return red;
            }

            // A shell bent ever so slightly keeps the light from being shared: the rays alone.
            double[,] truth = Rendered(1.0001, 4096);
            double[,] shared = Rendered(1, 16);
            double sum = 0;
            double truthSum = 0;

            for (int y = 21; y < truth.GetLength(0); y++)
            {
                for (int x = 0; x < truth.GetLength(1); x++)
                {
                    if (truth[y, x] <= 0 || truth[y, x] >= 0.95)
                        continue;

                    sum += shared[y, x];
                    truthSum += truth[y, x];
                }
            }

            Assert.IsGreaterThan(1, truthSum, "too little of the floor was lit to compare");
            Assert.AreEqual(1, sum / truthSum, 0.04, "shared, the lantern was not counted once");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// This tests where brushing applies: to something rough and opaque.  Glass frosts alike every way.
    /// </summary>
    [TestMethod]
    public void TestOnlyARoughOpaqueSurfaceIsBrushed()
    {
        Assert.IsTrue(Steel(0.3, 0.5).IsAnisotropic);
        Assert.IsFalse(Steel(0, 0.5).IsAnisotropic, "smooth");

        Material glass = Steel(0.3, 0.5);

        glass.Transparency = 1;

        Assert.IsFalse(glass.IsAnisotropic, "glass");
    }

    /// <summary>
    /// This tests a brushed mirror throwing the sun: the photons it throws spread across its grooves and hardly
    /// along them, so the patch they light on the floor is long one way and narrow the other, measured between
    /// the quartiles of where they land.
    /// </summary>
    [TestMethod]
    public void TestABrushedMirrorThrowsAStreak()
    {
        Vector across = new (0, 0, 1);
        Vector down = new Vector(1, -1, 0).Unit;
        Material steel = Steel(0.35, 0.95);

        // Brushed along the mirror's own Z, the facets lean in the plane of the sun and the floor's normal,
        // spreading the light along X on the floor and not along Z.
        steel.BrushedAlong = new Vector(0, 0, 1);

        Scene scene = new () { CausticPhotons = 100_000 };
        Plane floor = new () { Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0 } };
        Parallelogram mirror = new ()
        {
            Point = new Point(0, 4, 0) - across * 0.05 - down * 0.05, Side1 = across * 0.1, Side2 = down * 0.1,
            Material = steel
        };

        foreach (Surface surface in new Surface[] { floor, mirror })
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(new DistantLight { Direction = new Vector(1, 0, 0) });
        scene.ShootCausticPhotons();

        double[] xs = scene.Caustics.Photons.Where(photon => Math.Abs(photon.Y) < 1e-6).Select(photon => (double) photon.X).ToArray();
        double[] zs = scene.Caustics.Photons.Where(photon => Math.Abs(photon.Y) < 1e-6).Select(photon => (double) photon.Z).ToArray();

        Assert.IsGreaterThan(1000, xs.Length, "photons should land");

        // Between the quartiles: GGX's long tail throws a few photons a long way off at a graze, and an
        // average of squares measures nothing but those.
        double Spread(double[] values)
        {
            double[] sorted = values.Order().ToArray();

            return sorted[sorted.Length * 3 / 4] - sorted[sorted.Length / 4];
        }

        Assert.IsGreaterThan(4 * Spread(zs), Spread(xs), $"the streak: {Spread(xs)} by {Spread(zs)}");
    }

    /// <summary>
    /// This tests the words a scene says it with: `anisotropy` and which way, along a named axis or any
    /// direction or round an axis, after `metallic` too; and what cannot be is refused.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysASurfaceIsBrushed()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"anisotropy-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // The lamp overhead and the eye looking down off to one side, X or Z: across the grooves the
            // streak reaches the eye's place, along them it does not.
            double Seen(string finish, string offset, string placed = "")
            {
                File.WriteAllText(path,
                    "context { no gamma }\n" +
                    $"camera {{ location [{offset}]  look at [0, 0, 0]  field of view 1 }}\n" +
                    "background Black\n" +
                    "point light { location [0, 10, 0] }\n" +
                    "plane { material { pigment Black  ambient 0  diffuse 0  specular 0  reflective 0.6  roughness 0.3  " +
                    finish + " }  " + placed + " }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{finish}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2).Red;
            }

            const string fromX = "3, 10, 0";
            const string fromZ = "0, 10, 3";

            Assert.AreEqual(Seen("", fromX), Seen("", fromZ), 1e-4, "not brushed");
            Assert.IsGreaterThan(5 * Seen("anisotropy 0.9", fromX), Seen("anisotropy 0.9", fromZ), "along X, unsaid");
            Assert.IsGreaterThan(5 * Seen("anisotropy 0.9 along X", fromX), Seen("anisotropy 0.9 along X", fromZ), "along X");
            Assert.IsGreaterThan(5 * Seen("anisotropy 0.9 along Z", fromZ), Seen("anisotropy 0.9 along Z", fromX), "along Z");
            Assert.IsGreaterThan(5 * Seen("anisotropy 0.9 along [0, 0, 2]", fromZ), Seen("anisotropy 0.9 along [0, 0, 2]", fromX),
                "along a direction");
            Assert.IsGreaterThan(5 * Seen("metallic anisotropy 0.9 along Z", fromZ), Seen("metallic anisotropy 0.9 along Z", fromX),
                "after metallic");

            // Round an axis ten units off along Z, the circles near the middle run along X -- which is not the
            // way a floor brushed along Y would fall back to, Y lying square to it.
            const string offCenter = "translate [0, 0, 10]";

            Assert.IsGreaterThan(
                5 * Seen("anisotropy 0.9 around Y", fromX, offCenter), Seen("anisotropy 0.9 around Y", fromZ, offCenter),
                "around Y");

            Assert.ThrowsExactly<TokenException>(() => Seen("anisotropy 1.5", fromX), "more than one");
            Assert.ThrowsExactly<TokenException>(() => Seen("anisotropy 0.5 along [0, 0, 0]", fromX), "no way at all");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A sky that is white within a cap about one direction and black everywhere else.
    /// </summary>
    private class CapPigment(Vector toward, double halfAngle) : Pigment
    {
        private readonly double _cos = Math.Cos(halfAngle);

        public override Color GetColorFor(Point point)
        {
            Vector heading = new Vector(point.X, point.Y, point.Z).Unit;

            return heading.Dot(toward.Unit) >= _cos ? Colors.White : Colors.Black;
        }

        public override bool Matches(Pigment other) => ReferenceEquals(this, other);
    }

    /// <summary>
    /// This method adds up a function of direction over a cap of the sky.
    /// </summary>
    private static double OverCap(Vector toward, double halfAngle, Func<Vector, double> function)
    {
        Vector middle = toward.Unit;
        (Vector across, Vector along) = Microfacets.FrameAround(middle);
        const int rings = 300;
        const int spokes = 600;
        double total = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            double theta = (ring + 0.5) / rings * halfAngle;

            for (int spoke = 0; spoke < spokes; spoke++)
            {
                double phi = (spoke + 0.5) / spokes * 2 * Math.PI;
                Vector direction = middle * Math.Cos(theta) +
                                   (across * Math.Cos(phi) + along * Math.Sin(phi)) * Math.Sin(theta);

                total += function(direction) * Math.Sin(theta) * (halfAngle / rings) * (2 * Math.PI / spokes);
            }
        }

        return total;
    }
}
