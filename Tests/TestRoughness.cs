using System.Runtime.CompilerServices;
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
/// A rough surface: one spread of facets for its highlight and its reflection alike, checked against
/// the arithmetic it is built from rather than against how it looks.
/// </summary>
[TestClass]
public class TestRoughness
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"rough-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// The facets, weighted by how squarely each faces out, add up to exactly one, at every roughness.
    /// </summary>
    [TestMethod]
    public void TestTheFacetsAddUpToOne()
    {
        foreach (double alpha in new[] { 0.05, 0.3, 0.8 })
        {
            double total = 0;
            const int steps = 20000;

            for (int step = 0; step < steps; step++)
            {
                double theta = (step + 0.5) / steps * Math.PI / 2;

                total += Microfacets.Distribution(Math.Cos(theta), alpha) * Math.Cos(theta) *
                         Math.Sin(theta) * (Math.PI / 2 / steps) * 2 * Math.PI;
            }

            Assert.AreEqual(1, total, 1e-3, $"alpha {alpha}");
        }
    }

    /// <summary>
    /// All of a surface is seen looking straight down at it, and less and less of it toward a graze.
    /// </summary>
    [TestMethod]
    public void TestMaskingFallsTowardAGraze()
    {
        Assert.AreEqual(1, Microfacets.Masking(1, 0.3), 1e-12);
        Assert.AreEqual(0, Microfacets.Masking(0, 0.3));
        Assert.IsTrue(Microfacets.Masking(0.2, 0.3) < Microfacets.Masking(0.6, 0.3));
        Assert.IsTrue(Microfacets.Masking(0.2, 0.6) < Microfacets.Masking(0.2, 0.3), "rougher hides more");
    }

    /// <summary>
    /// Every facet picked faces out of the surface and toward the eye.
    /// </summary>
    [TestMethod]
    public void TestPickedFacetsFaceTheEye()
    {
        Vector eye = new Vector(0.6, 0, 0.8).Unit;

        for (int index = 0; index < 400; index++)
        {
            Vector facet = Microfacets.SampleVisibleNormal(eye, 0.4, (index % 20 + 0.5) / 20, (index / 20 + 0.5) / 20);

            Assert.AreEqual(1, facet.Magnitude, 1e-9);
            Assert.IsTrue(facet.Z >= 0 && eye.Dot(facet) > 0, $"facet {facet}");
        }
    }

    /// <summary>
    /// What a rough floor's rays bring back from a broad bright patch of sky is the patch's light times
    /// the facets' reflection of it, added up over the patch.  Worked out once by the rays the surface
    /// spreads and once by laying the highlight's own formula over the patch, the two agree -- which
    /// checks the picking, the weights and the factor of π the highlight carries, all at once.
    /// <para>
    /// The last case looks for the sky overhead from a graze, across a very rough floor.  There a facet
    /// that sends the eye's ray straight up is seen far more squarely than the floor is, so it mirrors
    /// less than half of what the floor's own angle would say: a reflection weighed by the floor's angle
    /// rather than each facet's comes out more than twice too bright, which nothing near the mirror
    /// direction shows.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheRaysAgreeWithTheHighlightsFormula()
    {
        (double Cos, bool Overhead, double HalfAngle, double Roughness, int Samples, double Tolerance)[] cases =
        [
            (0.9, false, 15, 0.3, 4096, 0.03),
            (0.6, false, 15, 0.3, 4096, 0.03),
            (0.3, false, 15, 0.3, 4096, 0.03),
            (0.2, true, 25, 0.7, 16384, 0.05)
        ];

        foreach ((double cos, bool overhead, double degrees, double roughness, int samples, double tolerance) in cases)
        {
            Material material = new ()
            {
                Reflective = overhead ? 0.2 : 0.3, Roughness = roughness, ReflectionSamples = samples
            };
            Vector eye = new (0, cos, -Math.Sqrt(1 - cos * cos));
            Vector toward = overhead ? new Vector(0, 1, 0) : new Vector(0, cos, Math.Sqrt(1 - cos * cos));
            double halfAngle = degrees * Math.PI / 180;
            double alpha = roughness * roughness;
            Scene scene = new () { Background = new CapPigment(toward, halfAngle, Colors.White) };
            Plane floor = new () { Material = material };

            scene.Surfaces.Add(floor);

            double seen = scene.GetReflectionColor(Hit(floor, cos), 2).Red;
            double expected = OverCap(toward, halfAngle,
                light => Microfacets.Highlight(new Vector(0, 1, 0), eye, light, alpha, material.ReflectanceAt)) /
                Math.PI;
            string what = $"seen at a cosine of {cos}{(overhead ? ", looking overhead" : "")}";

            Assert.IsTrue(expected > 0.005, $"nothing to compare, {what}: {expected}");
            Assert.AreEqual(expected, seen, expected * tolerance, what);
        }
    }

    /// <summary>
    /// A lamp in the sky gives a rough floor the highlight that a small patch of sky as bright, in all,
    /// would give through its rays: the lamp's color stands for its irradiance divided by π, as it does
    /// for every surface the lamp lights.
    /// </summary>
    [TestMethod]
    public void TestALampShinesAsAPatchOfSkyWould()
    {
        foreach (double cos in new[] { 0.9, 0.5 })
        {
            Material material = new () { Reflective = 0.3, Roughness = 0.4, Ambient = 0, Diffuse = 0 };
            Vector eye = new (0, cos, -Math.Sqrt(1 - cos * cos));
            Vector mirror = new (0, cos, Math.Sqrt(1 - cos * cos));
            double halfAngle = 1.5 * Math.PI / 180;
            double solidAngle = 2 * Math.PI * (1 - Math.Cos(halfAngle));
            Scene scene = new () { Background = new SolidPigment(Colors.Black) };
            Plane floor = new () { Material = material };

            scene.Surfaces.Add(floor);
            scene.Lights.Add(new DistantLight
            {
                Direction = -mirror, Color = new Color(1, 1, 1) * (solidAngle / Math.PI)
            });

            double lit = scene.GetHitColor(Hit(floor, cos), 2).Red;
            double expected = OverCap(mirror, halfAngle,
                light => Microfacets.Highlight(new Vector(0, 1, 0), eye, light, 0.16, material.ReflectanceAt)) /
                Math.PI;

            Assert.IsTrue(expected > 0.001, $"nothing to compare at a cosine of {cos}: {expected}");
            Assert.AreEqual(expected, lit, expected * 0.02, $"seen at a cosine of {cos}");
        }
    }

    /// <summary>
    /// A rough surface takes its highlight from its facets, so <c>specular</c> and <c>shininess</c>
    /// have no say in it.
    /// </summary>
    [TestMethod]
    public void TestSpecularAndShininessAreUnused()
    {
        Material material = new () { Reflective = 0.3, Roughness = 0.3, Ambient = 0, Diffuse = 0 };
        Scene scene = LampScene(material, out Plane floor);
        Color first = scene.GetHitColor(Hit(floor, 0.7), 1);

        material.Specular = 0.1;
        material.Shininess = 5;

        Assert.IsTrue(first.Red > 0.01, $"there was no highlight: {first}");
        Assert.IsTrue(first.Matches(scene.GetHitColor(Hit(floor, 0.7), 1)));
    }

    /// <summary>
    /// A lamp a ray can meet already shows in what a rough surface mirrors, so it gives that surface no
    /// highlight; a lamp no ray can meet does.  The sky and glowing volumes are the lamps rays meet.
    /// </summary>
    [TestMethod]
    public void TestALampRaysCanSeeGivesNoHighlight()
    {
        Material material = new () { Reflective = 0.3, Roughness = 0.3, Ambient = 0, Diffuse = 0 };
        Plane floor = new () { Material = material };
        Intersection hit = Hit(floor, 0.7);
        Point place = new (0, 10 * 0.7, 10 * Math.Sqrt(0.51));

        Assert.IsTrue(new PointLight { Location = place }.ApplyPhong(
            hit.Point, hit.Eye, hit.Normal, floor, new Color(1, 1, 1)).Red > 0.01, "a lamp");
        Assert.IsTrue(new SeenLamp { Location = place }.ApplyPhong(
            hit.Point, hit.Eye, hit.Normal, floor, new Color(1, 1, 1)).Matches(Colors.Black), "a lamp rays can meet");
        Assert.IsTrue(new SkyLight().CanBeSeen);
        Assert.IsTrue(((VolumeLight) RuntimeHelpers.GetUninitializedObject(typeof(VolumeLight))).CanBeSeen);
        Assert.IsFalse(new AreaLight().CanBeSeen);
    }

    /// <summary>
    /// A glowing volume reaches a rough floor both as a highlight from its own samples and through the
    /// floor's reflection, each place in the glow shared between the two by how likely each was to find
    /// it.  Shared, it comes to what the reflection alone finds when it looks long enough -- not twice
    /// that -- and with sixteen directions it is many times smoother than the reflection alone with
    /// sixteen.  A shell bent ever so slightly keeps the light from being shared, which is how the
    /// reflection alone is asked for.
    /// </summary>
    [TestMethod]
    public void TestALanternIsSharedFairly()
    {
        double[,] truth = RenderedLantern(1.0001, 4096);
        double[,] shared = RenderedLantern(1, 16);
        double[,] alone = RenderedLantern(1.0001, 16);
        (double sharedMean, double sharedNoise) = AgainstTruth(shared, truth);
        (double aloneMean, double aloneNoise) = AgainstTruth(alone, truth);

        Assert.AreEqual(1, sharedMean, 0.04, "shared, the lantern was not counted once");
        Assert.AreEqual(1, aloneMean, 0.06, "the reflection alone disagreed with itself");
        Assert.IsTrue(sharedNoise * 4 < aloneNoise, $"shared noise {sharedNoise:F3}, alone {aloneNoise:F3}");
    }

    /// <summary>
    /// A rough surface follows Fresnel whether it says so or not.
    /// </summary>
    [TestMethod]
    public void TestARoughSurfaceAlwaysFollowsFresnel()
    {
        Material material = new () { Reflective = 0.04, Roughness = 0.3 };

        Assert.IsTrue(material.FresnelApplies);
        Assert.AreEqual(0.04 + 0.96 * Math.Pow(0.9, 5), material.ReflectanceAt(0.1), 1e-12);
    }

    /// <summary>
    /// The same point looks in the same directions every time, so a rough reflection comes out the same
    /// twice over.
    /// </summary>
    [TestMethod]
    public void TestARoughReflectionIsTheSameEveryTime()
    {
        Scene scene = new () { Background = new CapPigment(new Vector(0, 0.6, 0.8), 0.3, Colors.White) };
        Plane floor = new () { Material = new Material { Reflective = 0.5, Roughness = 0.3 } };

        scene.Surfaces.Add(floor);

        Color first = scene.GetReflectionColor(Hit(floor, 0.6), 1);
        Color second = scene.GetReflectionColor(Hit(floor, 0.6), 1);

        Assert.IsTrue(first.Red > 0.01);
        Assert.AreEqual(first.Red, second.Red);
        Assert.AreEqual(first.Green, second.Green);
        Assert.AreEqual(first.Blue, second.Blue);
    }

    /// <summary>
    /// Only the first rough surface along a path spreads its ray: between two rough mirrors facing each
    /// other, the second and every later bounce look one way only, so the rays grow with the bounces
    /// rather than with the samples raised to their power.
    /// </summary>
    [TestMethod]
    public void TestOnlyTheFirstRoughSurfaceSpreads()
    {
        Material material = new () { Reflective = 1, Roughness = 0.2, ReflectionSamples = 16 };
        Plane floor = new () { Material = material };
        Plane ceiling = new () { Material = material, Transform = Transforms.Translate(0, 1, 0) * Transforms.RotateAroundX(180) };
        Scene scene = new () { Statistics = new Statistics() };

        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ceiling);
        scene.GetReflectionColor(Hit(floor, 0.8), 4);

        // Sixteen rays, each bouncing three more times at most: 64.  Spreading at every bounce would
        // be 16 + 256 + 4096.
        Assert.IsTrue(scene.Statistics.SceneRays <= 64, $"{scene.Statistics.SceneRays} rays");
        Assert.IsTrue(scene.Statistics.SceneRays >= 16, $"{scene.Statistics.SceneRays} rays");
    }

    /// <summary>
    /// A scene says it with <c>roughness</c>.  Alone, it is a satin finish, giving back what paint and
    /// plastic do square-on; told to give back nothing, it shows nothing.
    /// </summary>
    [TestMethod]
    public void TestRoughnessAloneIsASatinFinish()
    {
        Assert.IsTrue(RenderedBall("roughness 0.3").Red > 0.5, "roughness alone");
        Assert.IsTrue(RenderedBall("roughness 0.3  reflective 0").Matches(Colors.Black), "with nothing to give back");
    }

    /// <summary>
    /// A spectral render agrees with an RGB one under white light.
    /// </summary>
    [TestMethod]
    public void TestASpectralRenderAgrees()
    {
        Color rgb = RenderedFloor("");
        Color spectral = RenderedFloor("spectral");

        Assert.IsTrue(rgb.Red > 0.05, $"the floor showed nothing: {rgb}");
        Assert.AreEqual(rgb.Red, spectral.Red, 2.0 / 255);
        Assert.AreEqual(rgb.Green, spectral.Green, 2.0 / 255);
        Assert.AreEqual(rgb.Blue, spectral.Blue, 2.0 / 255);
    }

    /// <summary>
    /// A roughness beyond the scale, or fewer than one direction to look in, says what is wrong.
    /// </summary>
    [TestMethod]
    public void TestBadValuesAreReported()
    {
        StringAssert.Contains(Said("", "roughness 1.5"), "roughness runs from nothing");
        StringAssert.Contains(Said("reflection samples 0", "roughness 0.3"), "at least one direction");
    }

    /// <summary>
    /// The scene says how many directions a rough surface looks in, and the material is told.
    /// </summary>
    [TestMethod]
    public void TestTheSceneSaysHowManyDirections()
    {
        Assert.IsNull(Said("reflection samples 4", "roughness 0.3"));
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
    /// A lamp that says a ray can meet it.
    /// </summary>
    private class SeenLamp : PointLight
    {
        public override bool CanBeSeen => true;
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
    /// This method returns a scene with one lamp above and to the far side of a floor wearing the
    /// given material.
    /// </summary>
    private static Scene LampScene(Material material, out Plane floor)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };

        floor = new Plane { Material = material };
        scene.Surfaces.Add(floor);
        scene.Lights.Add(new PointLight { Location = new Point(0, 10 * 0.7, 10 * Math.Sqrt(0.51)) });

        return scene;
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
    /// This method renders a glowing ball over a rough black floor and returns the red of every pixel,
    /// read at sixteen bits so that a dim reflection is measured rather than rounded away.
    /// </summary>
    private double[,] RenderedLantern(double index, int samples)
    {
        string path = Path.Combine(_directory, "lantern.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            $"context {{ no gamma  color depth 16  reflection samples {samples} }}\n" +
            "camera { location [0, 2, -6]  look at [0, 0, 1] }\n" +
            "background Black\n" +
            "plane { material { pigment Black  ambient 0  diffuse 0  roughness 0.25  reflective 1 } }\n" +
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

    /// <summary>
    /// This method compares a render of the floor with the truth: the ratio of their means over the
    /// floor (leaving out the lantern itself, which is too bright to measure), and the spread of their
    /// ratio from pixel to pixel where there is light enough to divide by.
    /// </summary>
    private static (double Mean, double Noise) AgainstTruth(double[,] render, double[,] truth)
    {
        double sum = 0;
        double truthSum = 0;
        double squares = 0;
        int counted = 0;

        for (int y = 21; y < truth.GetLength(0); y++)
        {
            for (int x = 0; x < truth.GetLength(1); x++)
            {
                if (truth[y, x] <= 0 || truth[y, x] >= 0.95)
                    continue;

                sum += render[y, x];
                truthSum += truth[y, x];

                if (truth[y, x] > 0.01)
                {
                    double off = render[y, x] / truth[y, x] - 1;

                    squares += off * off;
                    counted++;
                }
            }
        }

        Assert.IsTrue(counted > 50 && truthSum > 1, $"too little of the floor was lit to compare: {counted}");

        return (sum / truthSum, Math.Sqrt(squares / counted));
    }

    /// <summary>
    /// This method renders a black ball lit from the camera's side, with the given finish, and returns
    /// the color in its middle, which is all highlight.
    /// </summary>
    private Color RenderedBall(string finish)
    {
        return Rendered("", "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
                            "point light { location [0, 0, -10] }\n" +
                            $"sphere {{ material {{ pigment Black  ambient 0  diffuse 0  {finish} }} }}");
    }

    /// <summary>
    /// This method renders a black rough floor under a white sky, seen across, and returns the color in
    /// the middle of the picture, which is the sky as the floor mirrors it.
    /// </summary>
    private Color RenderedFloor(string context)
    {
        return Rendered(context, "camera { location [0, 1, -3]  look at [0, 0, 0] }\n" +
                                 "background White\n" +
                                 "plane { material { pigment Black  ambient 0  diffuse 0  roughness 0.3 } }");
    }

    /// <summary>
    /// This method renders the given scene with the given context, nine pixels square with no gamma,
    /// and returns the color in the middle.
    /// </summary>
    private Color Rendered(string context, string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path, $"context {{ no gamma  {context} }}\n{scene}");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 9, Height = 9, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0].GetPixel(4, 4);
    }

    /// <summary>
    /// This method renders a ball with the given context and finish and returns what the render said,
    /// or <c>null</c> if it said nothing amiss.
    /// </summary>
    private string Said(string context, string finish)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            RenderedBallWith(context, finish);
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        string said = captured.ToString();

        return said.Contains("Error") ? said : null;
    }

    private void RenderedBallWith(string context, string finish)
    {
        Rendered(context, "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
                          "point light { location [0, 0, -10] }\n" +
                          $"sphere {{ material {{ pigment Red  {finish} }} }}");
    }
}
