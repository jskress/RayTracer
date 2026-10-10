using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// Rough glass: the blur of what lies beyond it, the share of the light each facet mirrors and lets
/// through, and the glow of a lamp behind it -- checked against the arithmetic it is built from.
/// </summary>
[TestClass]
public class TestFrostedGlass
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"frosted-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A facet of glass mirrors what a smooth crossing does: four percent square-on going into glass of
    /// index 1.5, everything at a graze, everything that cannot get out of it, and nothing at all where
    /// the two sides have the same index.
    /// </summary>
    [TestMethod]
    public void TestAFacetMirrorsAsGlassDoes()
    {
        Assert.AreEqual(0.04, Microfacets.GlassReflectance(1, 1, 1.5), 1e-12);
        Assert.AreEqual(1, Microfacets.GlassReflectance(0, 1, 1.5), 1e-12);
        Assert.AreEqual(1, Microfacets.GlassReflectance(0.3, 1.5, 1), 1e-12, "turned back inside");
        Assert.AreEqual(0, Microfacets.GlassReflectance(0.5, 1.33, 1.33));
    }

    /// <summary>
    /// What rough glass lets through from a broad bright patch of sky beyond it is the patch's light
    /// times what the facets pass toward the eye, added up over the patch.  Worked out once by the rays
    /// the glass spreads and once by laying the glow's own formula over the patch, the two agree --
    /// which checks the picking, the bending, the weights and the factor of π all at once.  The glass is
    /// a half-space, so a ray that goes into it never comes out to bend again.
    /// </summary>
    [TestMethod]
    public void TestTheRaysAgreeWithTheGlowsFormula()
    {
        foreach (double cos in new[] { 0.9, 0.6, 0.3 })
        {
            Material glass = Glass(0.3);
            Vector eye = new (0, cos, -Math.Sqrt(1 - cos * cos));
            Vector straight = Microfacets.Refract(eye, new Vector(0, 1, 0), 1 / 1.5).Unit;
            double halfAngle = 15 * Math.PI / 180;
            Scene scene = new () { Background = new CapPigment(straight, halfAngle, Colors.White) };
            Plane floor = new () { Material = glass };

            glass.ReflectionSamples = 4096;
            scene.Surfaces.Add(floor);

            double seen = scene.GetRefractedColor(Hit(floor, cos), 2).Red;
            double expected = OverCap(straight, halfAngle,
                light => Microfacets.TransmittedHighlight(new Vector(0, 1, 0), eye, light, 0.09, 1, 1.5)) /
                Math.PI;

            Assert.IsTrue(expected > 0.1, $"nothing to compare at a cosine of {cos}: {expected}");
            Assert.AreEqual(expected, seen, expected * 0.03, $"seen at a cosine of {cos}");
        }
    }

    /// <summary>
    /// Light leaving rough glass near the angle beyond which none could leave at all goes out close to
    /// the surface, where much of it is hidden by the facets beside -- so the rays and the formula must
    /// agree there too.  The eye is inside a glass ball far bigger than the patch it looks through.
    /// </summary>
    [TestMethod]
    public void TestTheRaysAgreeLeavingTheGlass()
    {
        Material glass = Glass(0.5);
        Sphere ball = new () { Material = glass, Transform = Transforms.Translate(0, -1000, 0) * Transforms.Scale(1000) };
        Vector heading = new (0, 0.8, 0.6);
        Ray ray = new (new Point(0, -0.8, -0.6), heading);
        Vector down = new (0, -1, 0);
        Vector eye = -heading;
        Vector straight = Microfacets.Refract(eye, down, 1.5).Unit;
        double halfAngle = 20 * Math.PI / 180;
        Scene scene = new () { Background = new CapPigment(straight, halfAngle, Colors.White) };

        glass.ReflectionSamples = 16384;
        scene.Surfaces.Add(ball);

        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.First(found => found.Distance > 0);

        hit.PrepareUsing(ray, hits);

        Assert.AreEqual(1.5, hit.N1, 1e-12, "the eye was not inside the glass");
        Assert.AreEqual(1, hit.N2, 1e-12);

        double seen = scene.GetRefractedColor(hit, 2).Red;
        double expected = OverCap(straight, halfAngle,
            light => Microfacets.TransmittedHighlight(down, eye, light, 0.25, 1.5, 1)) / Math.PI;

        Assert.IsTrue(expected > 0.05, $"nothing to compare: {expected}");
        Assert.AreEqual(expected, seen, expected * 0.03, "leaving the glass");
    }

    /// <summary>
    /// What rough glass mirrors of a broad bright patch of sky is the facets' reflection of it by the
    /// glass's own index -- not the share the surface's own angle would give -- added up over the patch.
    /// The patch is overhead and the eye low, where the two differ most.
    /// </summary>
    [TestMethod]
    public void TestRoughGlassMirrorsByItsOwnIndex()
    {
        foreach ((double cos, bool overhead) in new[] { (0.6, false), (0.2, true) })
        {
            Material glass = Glass(overhead ? 0.7 : 0.3);
            double alpha = glass.Roughness * glass.Roughness;
            Vector eye = new (0, cos, -Math.Sqrt(1 - cos * cos));
            Vector toward = overhead ? new Vector(0, 1, 0) : new Vector(0, cos, Math.Sqrt(1 - cos * cos));
            double halfAngle = (overhead ? 25 : 15) * Math.PI / 180;
            Scene scene = new () { Background = new CapPigment(toward, halfAngle, Colors.White) };
            Plane floor = new () { Material = glass };

            glass.ReflectionSamples = 16384;
            scene.Surfaces.Add(floor);

            double seen = scene.GetReflectionColor(Hit(floor, cos), 2).Red;
            double expected = OverCap(toward, halfAngle,
                light => Microfacets.Highlight(new Vector(0, 1, 0), eye, light, alpha,
                    c => Microfacets.GlassReflectance(c, 1, 1.5))) / Math.PI;

            Assert.IsTrue(expected > 0.002, $"nothing to compare at a cosine of {cos}: {expected}");
            Assert.AreEqual(expected, seen, expected * 0.05, $"seen at a cosine of {cos}");
        }
    }

    /// <summary>
    /// A lamp behind rough glass glows through it as a small patch of sky as bright, in all, would be
    /// seen through it by the glass's rays: the lamp's color stands for its irradiance divided by π, as
    /// everywhere.
    /// </summary>
    [TestMethod]
    public void TestALampGlowsThroughAsAPatchOfSkyWould()
    {
        foreach (double cos in new[] { 0.9, 0.5 })
        {
            Material glass = Glass(0.4);
            Vector eye = new (0, cos, -Math.Sqrt(1 - cos * cos));
            Vector straight = Microfacets.Refract(eye, new Vector(0, 1, 0), 1 / 1.5).Unit;
            // Small, since seen from inside the glass the glow is narrow -- a few degrees -- and a patch
            // much wider than a fraction of that is no longer a fair stand-in for a lamp at a point.
            double halfAngle = 0.3 * Math.PI / 180;
            double solidAngle = 2 * Math.PI * (1 - Math.Cos(halfAngle));
            Scene scene = new () { Background = new SolidPigment(Colors.Black) };
            Plane floor = new () { Material = glass };

            scene.Surfaces.Add(floor);
            scene.Lights.Add(new DistantLight
            {
                Direction = -straight, Color = new Color(1, 1, 1) * (solidAngle / Math.PI)
            });

            double lit = scene.GetHitColor(Hit(floor, cos), 2).Red;
            double expected = OverCap(straight, halfAngle,
                light => Microfacets.TransmittedHighlight(new Vector(0, 1, 0), eye, light, 0.16, 1, 1.5)) /
                Math.PI;

            Assert.IsTrue(expected > 0.001, $"nothing to compare at a cosine of {cos}: {expected}");
            Assert.AreEqual(expected, lit, expected * 0.02, $"seen at a cosine of {cos}: expected {expected}, lit {lit}");
        }
    }

    /// <summary>
    /// A lamp behind a pane of rough glass glows through it as a small patch of sky as bright, in all,
    /// would be seen through it by the pane's rays -- through both of the pane's faces, each rough.  The
    /// glow is given at the far face only, where the light has one surface left to cross; given at the
    /// near face too, straight on, it came out nearly eight times too bright.  A pane that fades
    /// what crosses it fades the lamp as it fades the patch of sky.
    /// </summary>
    [TestMethod]
    public void TestALampGlowsThroughAPaneAsThroughItsRays()
    {
        (double Cos, double Clarity)[] cases =
            [(1.0, double.PositiveInfinity), (0.8, double.PositiveInfinity), (1.0, 0.05)];

        foreach ((double cos, double clarity) in cases)
        {
            Vector eye = new (0, cos, -Math.Sqrt(1 - cos * cos));
            double halfAngle = 0.3 * Math.PI / 180;
            double solidAngle = 2 * Math.PI * (1 - Math.Cos(halfAngle));
            Scene byRays = new () { Background = new CapPigment(-eye, halfAngle, Colors.White) };
            Scene byLamp = new () { Background = new SolidPigment(Colors.Black) };

            byLamp.Lights.Add(new DistantLight
            {
                Direction = eye, Color = new Color(1, 1, 1) * (solidAngle / Math.PI)
            });

            // The rays must find a patch smaller than a degree through two blurs, so they want many
            // more directions than the lamp, which is found exactly at the far face.
            double rays = ThroughAPaneOf(byRays, 0.3, cos, 65536, clarity).Red;
            double lamp = ThroughAPaneOf(byLamp, 0.3, cos, 4096, clarity).Red;

            Assert.IsTrue(rays > 0.001, $"nothing to compare at a cosine of {cos}: {rays}");
            Assert.AreEqual(rays, lamp, rays * 0.04,
                $"seen at a cosine of {cos}, clarity {clarity}: rays {rays}, lamp {lamp}");
        }
    }

    /// <summary>
    /// A lamp behind a pane of rough glass does not glitter.  Its glow is a narrow, enormously bright
    /// lobe at the far face, which the near face's rays alone find only by chance: around the glow, one
    /// ray in many lands on it and whitens its pixel, and more rays only make more such pixels.  Aimed
    /// at the lamp as well, the pane comes to the same glow smoothly with sixteen directions.
    /// </summary>
    [TestMethod]
    public void TestALampThroughAPaneDoesNotGlitter()
    {
        double[,] truth = RenderedLampBehindAPane(1024, 0.2);
        double[,] sixteen = RenderedLampBehindAPane(16, 0.2);
        (double mean, double noise) = AgainstTruth(sixteen, truth);

        Assert.AreEqual(1, mean, 0.03, "with sixteen directions, the glow was not the same");
        Assert.IsTrue(noise < 0.25, $"the glow glittered: noise {noise:F3}");
    }

    /// <summary>
    /// An area lamp behind a pane is aimed at one of its places per direction, in turn, so that its
    /// places need not have the same number of directions each: with five places and 4101 directions,
    /// one has 821 and the others 820.  Its glow, the places all but on top of each other, is a point
    /// lamp's there.
    /// </summary>
    [TestMethod]
    public void TestAnAreaLampIsAimedAtPlaceByPlace()
    {
        foreach (double cos in new[] { 1.0, 0.8 })
        {
            Vector eye = new (0, cos, -Math.Sqrt(1 - cos * cos));
            Point place = new Point(0, 0, 0) - eye * 2;
            Scene byPoint = new () { Background = new SolidPigment(Colors.Black) };
            Scene byArea = new () { Background = new SolidPigment(Colors.Black) };

            byPoint.Lights.Add(new PointLight { Location = place });
            byArea.Lights.Add(new AreaLight
            {
                Location = place, Axis1 = new Vector(1e-5, 0, 0), Axis2 = new Vector(0, 0, 1e-5),
                USteps = 5, VSteps = 1
            });

            // Looked at with many directions, so that what is compared is how the places are shared
            // rather than how settled either is.
            double point = ThroughAPaneOf(byPoint, 0.2, cos, 4096).Red;
            double area = ThroughAPaneOf(byArea, 0.2, cos, 4101).Red;

            Assert.IsTrue(point > 0.01, $"nothing to compare at a cosine of {cos}: {point}");
            Assert.AreEqual(point, area, point * 0.02, $"at a cosine of {cos}: point {point}, area {area}");
        }
    }

    /// <summary>
    /// A lamp in front of a pane reaches the inside of its far face only through its near face, and is
    /// given there, as the near face's glow seen from within -- not again by a shadow ray straight back
    /// through the glass to the far face, which counted it twice, unbent, and at angles light that has
    /// come in through a pane can never take.
    /// </summary>
    [TestMethod]
    public void TestALampInFrontOfAPaneIsGivenWhereItComesIn()
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };
        Material glass = Glass(0.3);
        Cube pane = new () { Material = glass, Transform = Transforms.Scale(50, 0.02, 50) };

        scene.Surfaces.Add(pane);
        scene.Lights.Add(new DistantLight { Direction = new Vector(0, -1, 0) });

        // From inside, looking up at the near face, the lamp glows through it; looking down at the far
        // face, the lamp is behind the eye, beyond the near face, and that face is where it is given.
        Color nearFace = InsideAPane(scene, new Vector(0, 1, 0));
        Color farFace = InsideAPane(scene, new Vector(0, -1, 0));

        Assert.IsTrue(nearFace.Red > 0.1, $"the near face did not glow from within: {nearFace}");
        Assert.AreEqual(0, farFace.Red, 1e-12, $"the far face mirrored the lamp through the glass: {farFace}");
    }

    /// <summary>
    /// A glowing ball behind a pane of rough glass is shared between its own glow through the pane and
    /// the pane's rays, place by place at the far face.  Shared, it comes to what the rays alone find
    /// looking long enough -- the ball not counted twice -- and with sixteen directions it is smoother
    /// than the rays alone with sixteen.  A shell bent ever so slightly keeps the ball from being shared,
    /// which is how the rays alone are asked for.
    /// </summary>
    [TestMethod]
    public void TestALanternBehindAPaneIsSharedFairly()
    {
        double[,] truth = RenderedLanternBehindAPane(1.0001, 1024, 0.25);
        double[,] shared = RenderedLanternBehindAPane(1, 16, 0.25);
        double[,] alone = RenderedLanternBehindAPane(1.0001, 16, 0.25);
        (double sharedMean, double sharedNoise) = AgainstTruth(shared, truth);
        (double _, double aloneNoise) = AgainstTruth(alone, truth);

        Assert.AreEqual(1, sharedMean, 0.08, "shared, the lantern was not counted once");
        Assert.IsTrue(sharedNoise < aloneNoise * 0.85, $"shared noise {sharedNoise:F3}, alone {aloneNoise:F3}");

        // Behind clearer glass the rays find the lantern more often and take about half of it, its own
        // glow through the glass the other half: where neither has it all, it must still come to once.
        double[,] clearTruth = RenderedLanternBehindAPane(1.0001, 256, 0.12, 48);
        double[,] clearShared = RenderedLanternBehindAPane(1, 16, 0.12, 48);

        double clearMean = AgainstTruth(clearShared, clearTruth).Mean;

        Assert.AreEqual(1, clearMean, 0.08, $"behind clearer glass: {clearMean:F3}");
    }

    /// <summary>
    /// Under a sky of one even brightness, rough glass passes and mirrors between them very nearly what
    /// smooth glass does -- a blur of an even sky is the same even sky -- and loses only the little its
    /// facets hide from each other.
    /// </summary>
    [TestMethod]
    public void TestRoughGlassUnderAnEvenSkyLosesLittle()
    {
        Color smooth = ThroughAPane(0);
        Color rough = ThroughAPane(0.3);

        Assert.IsTrue(smooth.Red > 0.9, $"smooth glass let through {smooth}");
        Assert.AreEqual(smooth.Red, rough.Red, 0.05, $"smooth {smooth}, rough {rough}");
    }

    /// <summary>
    /// Rough glass blurs what is behind it: a checkered wall seen through it loses most of its contrast,
    /// and seen through smooth glass loses none.
    /// </summary>
    [TestMethod]
    public void TestRoughGlassBlursWhatIsBehindIt()
    {
        double clear = Contrast(RenderedPane(""));
        double frosted = Contrast(RenderedPane("roughness 0.3"));

        Assert.IsTrue(clear > 0.2, $"the wall was not seen clearly through clear glass: {clear}");
        Assert.IsTrue(frosted < clear / 3, $"frosted {frosted} against clear {clear}");

        // And glass that spreads colors, rendered in color, blurs as well: each band is bent through a
        // facet of its own.  Less smoothly, since a channel is made of only the handful of bands that
        // fall in it, one facet each, where the glass that spreads nothing looks through sixteen --
        // which is what keeps the cost at one ray a band.
        double spread = Contrast(RenderedPane("roughness 0.3", "spectral", "glass 'SF10'"));

        Assert.IsTrue(spread < clear / 2, $"frosted glass spreading colors {spread} against clear {clear}");
    }

    /// <summary>
    /// A lamp behind frosted glass glows through it where it stands, and behind clear glass shows
    /// nothing, a point of light being something no ray can see.
    /// </summary>
    [TestMethod]
    public void TestALampBehindFrostedGlassGlows()
    {
        Color clear = RenderedLampBehind("");
        Color frosted = RenderedLampBehind("roughness 0.3");

        Assert.IsTrue(clear.Red < 0.02, $"clear glass showed the lamp: {clear}");
        Assert.IsTrue(frosted.Red > 0.2, $"frosted glass did not glow: {frosted}");
    }

    /// <summary>
    /// Rough glass keeps its glints: a lamp mirrored in it shows, though the glass lets all the rest of
    /// the light through.
    /// </summary>
    [TestMethod]
    public void TestRoughGlassKeepsItsGlints()
    {
        Material glass = Glass(0.2);
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };
        Plane floor = new () { Material = glass };

        scene.Surfaces.Add(floor);
        scene.Lights.Add(new PointLight { Location = new Point(0, 10 * 0.7, 10 * Math.Sqrt(0.51)) });

        Assert.IsTrue(scene.GetHitColor(Hit(floor, 0.7), 2).Red > 0.05, "the glass showed no glint");
    }

    /// <summary>
    /// Only the first rough crossing along a path spreads its ray: through a rough pane, the rays that
    /// went in through the near face come out through the far one in one direction each.
    /// </summary>
    [TestMethod]
    public void TestOnlyTheFirstRoughCrossingSpreads()
    {
        Material glass = Glass(0.2);
        Cube pane = new () { Material = glass, Transform = Transforms.Scale(5, 0.1, 5) };
        Scene scene = new () { Statistics = new Statistics(), Background = new SolidPigment(Colors.White) };
        Ray ray = new (new Point(0, 1, -0.5), new Vector(0, -0.9, 0.45).Unit);

        glass.ReflectionSamples = 16;
        scene.Surfaces.Add(pane);

        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.First(found => found.Distance > 0);

        hit.PrepareUsing(ray, hits);
        scene.Statistics = new Statistics();
        scene.GetHitColor(hit, 3);

        // Sixteen rays mirrored and sixteen sent in, each of those going on one way at a time for the
        // two crossings left: a few hundred at most.  Spreading at every crossing would be thousands.
        Assert.IsTrue(scene.Statistics.SceneRays < 400, $"{scene.Statistics.SceneRays} rays");
    }

    /// <summary>
    /// Rough glass that names no reflectance mirrors all that its index says, and a rough surface that
    /// lets no light through gives back four percent: each renders exactly as if it had said so.
    /// </summary>
    [TestMethod]
    public void TestRoughnessGivesGlassItsOwnReflectance()
    {
        AssertSame(
            RenderedBall("transparency 1  roughness 0.2  interior { ior 1.5 }"),
            RenderedBall("transparency 1  roughness 0.2  reflective 1  interior { ior 1.5 }"),
            "glass");
        AssertSame(
            RenderedBall("roughness 0.2"), RenderedBall("roughness 0.2  reflective 0.04"), "paint");
    }

    /// <summary>
    /// Rough glass that spreads colors renders, in color and blurred, bending each band through a facet
    /// of its own.
    /// </summary>
    [TestMethod]
    public void TestRoughGlassThatSpreadsColorsRenders()
    {
        Color seen = Rendered("spectral",
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "background White\n" +
            "sphere { material { pigment White  ambient 0  diffuse 0  transparency 1  roughness 0.2\n" +
            "    interior { glass 'SF10' } } }");

        Assert.IsTrue(seen.Red > 0.3 && seen.Green > 0.3 && seen.Blue > 0.3, $"the glass showed {seen}");
    }

    /// <summary>
    /// A sky that is white within a cap about one direction and black everywhere else.
    /// </summary>
    private class CapPigment(Vector toward, double halfAngle, Color color) : Pigment
    {
        private readonly double _cos = Math.Cos(halfAngle);

        public override Color GetColorFor(Point point)
        {
            Vector heading = new Vector(point.X, point.Y, point.Z).Unit;

            return heading.Dot(toward.Unit) >= _cos ? color : Colors.Black;
        }

        public override bool Matches(Pigment other) => ReferenceEquals(this, other);
    }

    /// <summary>
    /// This method returns clear glass of index 1.5 with the given roughness, showing nothing of its own.
    /// </summary>
    private static Material Glass(double roughness)
    {
        return new Material
        {
            Ambient = 0, Diffuse = 0, Transparency = 1, Reflective = 1, Roughness = roughness,
            Interior = new Interior { IndexOfRefraction = 1.5 }
        };
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

    /// <summary>
    /// This method returns where a ray meets the middle of a surface lying in the X/Z plane, coming down
    /// at the angle whose cosine against its normal is given, prepared for shading.
    /// </summary>
    private static Intersection Hit(Surface floor, double cos)
    {
        double sin = Math.Sqrt(1 - cos * cos);
        Ray ray = new (new Point(0, cos, -sin), new Vector(0, -cos, sin));
        Intersection intersection = new (floor, 1);

        intersection.PrepareUsing(ray, [intersection]);

        return intersection;
    }

    /// <summary>
    /// This method returns what is seen looking down through a pane of glass with the given roughness
    /// under an evenly white sky, at the pane's middle.
    /// </summary>
    private static Color ThroughAPane(double roughness)
    {
        Material glass = Glass(roughness);
        Cube pane = new () { Material = glass, Transform = Transforms.Scale(5, 0.1, 5) };
        Scene scene = new () { Background = new SolidPigment(Colors.White) };
        Ray ray = new (new Point(0, 1, -0.5), new Vector(0, -0.9, 0.45).Unit);

        glass.ReflectionSamples = 256;
        scene.Surfaces.Add(pane);

        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.First(found => found.Distance > 0);

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 6);
    }

    /// <summary>
    /// This method returns what is seen looking down through a thin pane of rough glass, of index 1.5 and
    /// the given roughness and clarity, at the given cosine, in the given scene.
    /// </summary>
    private static Color ThroughAPaneOf(
        Scene scene, double roughness, double cos, int samples, double clarity = double.PositiveInfinity)
    {
        double sin = Math.Sqrt(1 - cos * cos);
        Material glass = Glass(roughness);

        glass.Interior.Clarity = clarity;
        Cube pane = new () { Material = glass, Transform = Transforms.Scale(50, 0.02, 50) };
        Ray ray = new (new Point(0, 0.02 + cos, -sin), new Vector(0, -cos, sin));

        glass.Reflective = 0;
        glass.ReflectionSamples = samples;
        scene.Surfaces.Add(pane);

        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.First(found => found.Distance > 0);

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 6);
    }

    /// <summary>
    /// This method returns what a pane's own surface shows seen from inside the pane looking along the
    /// given direction, without anything it mirrors or lets through: only what the lamps give it.
    /// </summary>
    private static Color InsideAPane(Scene scene, Vector direction)
    {
        Ray ray = new (new Point(0, 0, 0), direction);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.First(found => found.Distance > 0);

        hit.PrepareUsing(ray, hits);
        Assert.IsTrue(hit.Inside, "the ray did not start inside the pane");

        return scene.GetHitColor(hit, 0);
    }

    /// <summary>
    /// This method renders a point lamp behind a pane of rough glass against a black sky, read at sixteen
    /// bits, and returns the red of every pixel.
    /// </summary>
    private double[,] RenderedLampBehindAPane(int samples, double roughness)
    {
        Canvas canvas = RenderedCanvas($"color depth 16  reflection samples {samples}",
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 12 }\n" +
            "background Black\n" +
            "point light { location [0, 0, 1.5]  color [0.4, 0.4, 0.4] }\n" +
            "cube { scale [2, 2, 0.05]\n" +
            $"    material {{ pigment White  ambient 0  diffuse 0  specular 0  transparency 1  roughness {roughness}\n" +
            "        interior { ior 1.5 } } }\n",
            32, 32);
        double[,] red = new double[canvas.Height, canvas.Width];

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
                red[y, x] = canvas.GetPixel(x, y).Red;
        }

        return red;
    }

    /// <summary>
    /// This method renders a glowing ball behind a pane of rough glass, read at sixteen bits, and returns
    /// the red of every pixel.  The ball's shell has the given index, so that one not quite the
    /// surroundings' keeps it from being shared.
    /// </summary>
    private double[,] RenderedLanternBehindAPane(double index, int samples, double roughness, int size = 24)
    {
        Canvas canvas = RenderedCanvas($"color depth 16  reflection samples {samples}",
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 30 }\n" +
            "background Black\n" +
            "cube { scale [2, 2, 0.05]\n" +
            $"    material {{ pigment White  ambient 0  diffuse 0  specular 0  transparency 1  roughness {roughness}\n" +
            "        interior { ior 1.5 } } }\n" +
            "sphere {\n" +
            "    material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1\n" +
            $"        interior {{ ior {index}  medium {{ emission [0.3, 0.27, 0.21]\n" +
            "            density function { max(0, 1 - sqrt(x * x + y * y + z * z)) * 40 } } } }\n" +
            "    no shadow  gives light samples 24  scale 0.3  translate [0, 0, 1.5]\n" +
            "}\n",
            size, size);
        double[,] red = new double[canvas.Height, canvas.Width];

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
                red[y, x] = canvas.GetPixel(x, y).Red;
        }

        return red;
    }

    /// <summary>
    /// This method compares a render with the truth: the ratio of their means where the truth is lit, and
    /// the spread of their ratio from pixel to pixel where there is light enough to divide by.
    /// </summary>
    private static (double Mean, double Noise) AgainstTruth(double[,] render, double[,] truth)
    {
        double sum = 0;
        double truthSum = 0;
        double squares = 0;
        int counted = 0;

        for (int y = 0; y < truth.GetLength(0); y++)
        {
            for (int x = 0; x < truth.GetLength(1); x++)
            {
                if (truth[y, x] <= 0.01)
                    continue;

                double off = render[y, x] / truth[y, x] - 1;

                sum += render[y, x];
                truthSum += truth[y, x];
                squares += off * off;
                counted++;
            }
        }

        Assert.IsTrue(counted > 20, $"too little was lit to compare: {counted}");

        return (sum / truthSum, Math.Sqrt(squares / counted));
    }

    /// <summary>
    /// This method returns how far the brightness of a picture strays from its own average, as a share of
    /// that average.
    /// </summary>
    private static double Contrast(double[] values)
    {
        double mean = values.Average();

        return Math.Sqrt(values.Select(value => (value - mean) * (value - mean)).Average()) / mean;
    }

    /// <summary>
    /// This method renders a checkered wall seen through a pane of glass with the given finish and returns
    /// the red of the pixels in the middle of the picture, where the pane covers the wall.
    /// </summary>
    private double[] RenderedPane(string finish, string context = "", string index = "ior 1.5")
    {
        Canvas canvas = RenderedCanvas(context,
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 20 }\n" +
            "point light { location [0, 5, -10] }\n" +
            "plane { rotate X -90  translate Z 3\n" +
            "    material { pigment checker { White, Black  scale 0.15 }  ambient 1  diffuse 0  specular 0 } }\n" +
            "cube { scale [3, 3, 0.05]\n" +
            "    material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1  reflective 0  " +
            finish + "\n" +
            $"        interior {{ {index} }} }} }}",
            24, 24);
        List<double> red = [];

        for (int y = 6; y < 18; y++)
        {
            for (int x = 6; x < 18; x++)
                red.Add(canvas.GetPixel(x, y).Red);
        }

        return red.ToArray();
    }

    /// <summary>
    /// This method renders a point lamp behind a pane of glass with the given finish, against a black sky,
    /// and returns the color where the lamp stands behind the pane.
    /// </summary>
    private Color RenderedLampBehind(string finish)
    {
        return Rendered("",
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "background Black\n" +
            "point light { location [0, 0, 4] }\n" +
            "cube { scale [3, 3, 0.05]\n" +
            "    material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1  reflective 0  " +
            finish + "\n" +
            "        interior { ior 1.5 } } }");
    }

    /// <summary>
    /// This method renders a ball of the given finish over a checkered floor, lit and under a white sky,
    /// and returns every pixel's red.
    /// </summary>
    private double[] RenderedBall(string finish)
    {
        Canvas canvas = RenderedCanvas("",
            "camera { location [0, 1, -5]  look at [0, 0.5, 0] }\n" +
            "point light { location [-4, 6, -6] }\n" +
            "background White\n" +
            "plane { material { pigment checker { White, Black } } }\n" +
            $"sphere {{ translate Y 1  material {{ pigment [0.4, 0.5, 0.6]  {finish} }} }}",
            16, 12);
        List<double> red = [];

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
                red.Add(canvas.GetPixel(x, y).Red);
        }

        return red.ToArray();
    }

    /// <summary>
    /// This method asserts that two renders are the same pixel for pixel.
    /// </summary>
    private static void AssertSame(double[] expected, double[] actual, string what)
    {
        Assert.AreEqual(expected.Length, actual.Length, what);

        for (int index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], actual[index], $"{what}: pixel {index}");
    }

    /// <summary>
    /// This method renders the given scene with the given context, nine pixels square with no gamma, and
    /// returns the color in the middle.
    /// </summary>
    private Color Rendered(string context, string scene)
    {
        return RenderedCanvas(context, scene, 9, 9).GetPixel(4, 4);
    }

    /// <summary>
    /// This method renders the given scene with the given context at the given size, with no gamma.
    /// </summary>
    private Canvas RenderedCanvas(string context, string scene, int width, int height)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path, $"context {{ no gamma  {context} }}\n{scene}");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = width, Height = height, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0];
    }
}
