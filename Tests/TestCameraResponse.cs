using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pixels;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how the light a render ends with becomes a picture: the camera's exposure, and the film
/// curve that develops it.
/// </summary>
[TestClass]
public class TestCameraResponse
{
    private static readonly Random Random = new (19);

    /// <summary>
    /// This tests that the neutral curve leaves every color whose brightest channel is below eight tenths of
    /// white exactly as it was, so that a scene's colors come out as they were written.
    /// </summary>
    [TestMethod]
    public void TestTheNeutralCurveLeavesTheShadesAlone()
    {
        for (int index = 0; index < 1000; index++)
        {
            Color light = new (Random.NextDouble() * 0.8, Random.NextDouble() * 0.8, Random.NextDouble() * 0.8);
            Color shown = FilmCurve.Neutral.Develop(light);

            Assert.AreEqual(light.Red, shown.Red);
            Assert.AreEqual(light.Green, shown.Green);
            Assert.AreEqual(light.Blue, shown.Blue);
        }
    }

    /// <summary>
    /// This tests that the neutral curve eases brighter light toward white without reaching it: the brighter
    /// the light, the brighter the picture, never past white, smoothly away from the knee, with a gray kept
    /// gray and a color paled the brighter it gets.
    /// </summary>
    [TestMethod]
    public void TestTheNeutralCurveEasesBrightLightTowardWhite()
    {
        double last = 0;

        for (double peak = 0.8; peak < 1000; peak *= 1.05)
        {
            Color shown = FilmCurve.Neutral.Develop(new Color(peak, peak, peak));

            Assert.AreEqual(shown.Red, shown.Green, 1e-12, "a gray stays gray");
            Assert.AreEqual(shown.Red, shown.Blue, 1e-12, "a gray stays gray");
            Assert.IsGreaterThan(last, shown.Red, $"brighter light at {peak} shows brighter");
            Assert.IsLessThan(1, shown.Red, $"never past white, at {peak}");

            last = shown.Red;
        }

        Assert.AreEqual(0.8001, FilmCurve.Neutral.Develop(new Color(0.8001, 0, 0)).Red, 1e-6, "smooth at the knee");

        double Paleness(Color color) =>
            Math.Min(color.Red, Math.Min(color.Green, color.Blue)) / Math.Max(color.Red, Math.Max(color.Green, color.Blue));

        double was = Paleness(FilmCurve.Neutral.Develop(new Color(0.9, 0.09, 0.0225)));
        double brighter = Paleness(FilmCurve.Neutral.Develop(new Color(9, 0.9, 0.225)));

        Assert.IsGreaterThan(was, brighter, "a brighter color is paler");
    }

    /// <summary>
    /// This tests the soft curve, the default: each channel is eased toward white by itself, so a channel below
    /// eight tenths of white is left alone whatever the others do, gray goes as the neutral curve takes it, and
    /// a bright orange turns yellower the brighter it is, its red eased before its green -- the way to white a
    /// flame takes in a photograph.
    /// </summary>
    [TestMethod]
    public void TestTheSoftCurveEasesEachChannelByItself()
    {
        Assert.AreSame(FilmCurve.Soft, new RenderContext().FilmCurve, "soft is the default");

        for (int index = 0; index < 1000; index++)
        {
            Color light = new (Random.NextDouble() * 4, Random.NextDouble() * 0.8, Random.NextDouble() * 0.8);
            Color shown = FilmCurve.Soft.Develop(light);

            Assert.AreEqual(light.Green, shown.Green, "a channel below the knee is left alone");
            Assert.AreEqual(light.Blue, shown.Blue, "a channel below the knee is left alone");
            Assert.AreEqual(light.Red < 0.8 ? light.Red : 1 - 0.04 / (light.Red - 0.6), shown.Red, 1e-12);
        }

        Assert.AreEqual(FilmCurve.Neutral.Develop(new Color(3, 3, 3)).Red, FilmCurve.Soft.Develop(new Color(3, 3, 3)).Red, 1e-12);

        double was = 0;

        foreach (double brightness in new[] { 1.0, 2, 4, 8 })
        {
            Color shown = FilmCurve.Soft.Develop(new Color(brightness, brightness * 0.5, brightness * 0.15));
            double yellowness = shown.Green / shown.Red;

            Assert.IsGreaterThan(was, yellowness, $"brighter orange, yellower, at {brightness}");

            was = yellowness;
        }
    }

    /// <summary>
    /// This tests the AgX curve: gray in, gray out, brighter in, brighter out, never past white, middle gray
    /// at what its curve puts it -- 0.18 of white to about 0.214, half the screen's range once written with
    /// the sRGB curve -- and colors where AgX itself puts them.
    /// </summary>
    [TestMethod]
    public void TestTheAgxCurveIsAFilmsCurve()
    {
        double last = -1;

        for (double light = 0.001; light < 100; light *= 1.1)
        {
            Color shown = FilmCurve.AgX.Develop(new Color(light, light, light));

            Assert.AreEqual(shown.Red, shown.Green, 1e-3, $"gray at {light}");
            Assert.AreEqual(shown.Red, shown.Blue, 1e-3, $"gray at {light}");
            Assert.IsGreaterThanOrEqualTo(last, shown.Red, $"brighter at {light}");
            Assert.IsLessThanOrEqualTo(1, shown.Red);

            last = shown.Red;
        }

        Assert.AreEqual(0.214, FilmCurve.AgX.Develop(new Color(0.18, 0.18, 0.18)).Green, 0.005);

        // Colors, against AgX as three.js writes it -- its matrices taken as the columns GLSL takes them as,
        // worked out apart from this code -- so that a matrix turned on its side, or left out, is caught: gray
        // alone passes through every matrix whose rows each sum to one.
        (Color Light, Color Shown)[] colors =
        [
            (new Color(0.6, 0.2, 0.05), new Color(0.497081, 0.238922, 0.108316)),
            (new Color(1, 0, 0), new Color(0.723276, 0.105847, 0.066736)),
            (new Color(0.1, 0.5, 2.0), new Color(0.263858, 0.491649, 0.810696))
        ];

        foreach ((Color light, Color wanted) in colors)
        {
            Color shown = FilmCurve.AgX.Develop(light);

            Assert.AreEqual(wanted.Red, shown.Red, 1e-5, $"red of {light}");
            Assert.AreEqual(wanted.Green, shown.Green, 1e-5, $"green of {light}");
            Assert.AreEqual(wanted.Blue, shown.Blue, 1e-5, $"blue of {light}");
        }
    }

    /// <summary>
    /// This tests that no curve at all cuts light off at white, channel by channel, as every render did before
    /// there were curves.
    /// </summary>
    [TestMethod]
    public void TestNoCurveCutsOffAtWhite()
    {
        Color shown = FilmCurve.None.Develop(new Color(3, 0.5, -1, 0.25));

        Assert.AreEqual(1, shown.Red);
        Assert.AreEqual(0.5, shown.Green);
        Assert.AreEqual(0, shown.Blue);
        Assert.AreEqual(0.25, shown.Alpha, "what a pixel covers is not light");
    }

    /// <summary>
    /// This tests that each stop of exposure doubles the light the camera takes, and that the command line's
    /// exposure takes the place of the camera's own.
    /// </summary>
    [TestMethod]
    public void TestEachStopDoublesTheLight()
    {
        Canvas Taken(double stops, double? overall = null)
        {
            Camera camera = new () { Exposure = stops };
            RenderContext context = new () { Width = 11, Height = 11, Exposure = overall };

            return camera.Render(context, TestScenes.DefaultScene());
        }

        Canvas plain = Taken(0), doubled = Taken(1), overruled = Taken(3, 1), halved = Taken(-1);

        for (int y = 0; y < plain.Height; y++)
        {
            for (int x = 0; x < plain.Width; x++)
            {
                Color light = plain.GetPixel(x, y);

                Assert.AreEqual(light.Red * 2, doubled.GetPixel(x, y).Red, 1e-12);
                Assert.AreEqual(light.Green * 2, overruled.GetPixel(x, y).Green, 1e-12, "the command line's");
                Assert.AreEqual(light.Blue / 2, halved.GetPixel(x, y).Blue, 1e-12);
            }
        }
    }

    /// <summary>
    /// This tests that the sampler tells samples apart as the picture will show them: a scene lit a sixteenth
    /// as brightly, which it takes for smooth, is looked at more closely exposed four stops up, where its
    /// edges are as plain as the bright scene's.
    /// </summary>
    [TestMethod]
    public void TestTheSamplerLooksAtWhatThePictureShows()
    {
        long SamplesTaken(double lighting, double stops)
        {
            Scene scene = TestScenes.DefaultScene();
            Statistics statistics = new ();
            RenderContext context = new ()
            {
                Width = 11, Height = 11, Statistics = statistics,
                AntiAliasing = new AliasingOption { AdaptiveDepth = 3 }
            };

            scene.Lights[0].Color = new Color(lighting, lighting, lighting);
            new Camera { Exposure = stops }.Render(context, scene);

            return statistics.Samples;
        }

        long dim = SamplesTaken(1 / 16.0, 0);
        long exposed = SamplesTaken(1 / 16.0, 4);

        Assert.IsGreaterThan(dim, exposed, $"a dim scene took {dim} samples, and exposed for, {exposed}");
    }

    /// <summary>
    /// This tests the words a scene says it with, and what reaches the file: white, written through the
    /// soft curve by default, comes back as nine tenths of white's light; through no curve, or with no
    /// gamma, as white; and a quarter of white exposed a stop up as half.  A bright orange's green shows
    /// which curve it went through: the soft curve leaves it alone, the neutral one keeps the orange's hue.
    /// </summary>
    [TestMethod]
    public void TestAScenesSaysHowToDevelopIt()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"response-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // A flat, wholly ambient plane filling the view, looked at straight on.
            double Middle(string context, string camera, double ambient, string pigment = "White", bool green = false)
            {
                File.WriteAllText(path,
                    $"context {{ {context} }}\n" +
                    $"camera {{ location [0, 0, -3]  look at [0, 0, 0]  field of view 10  {camera} }}\n" +
                    "point light { location [0, 0, -5]  color White }\n" +
                    $"plane {{ material {{ pigment {pigment}  ambient {ambient}  diffuse 0  specular 0 }}  rotate X 90 }}\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{context}' and '{camera}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                Canvas image = new ImageFile(output).Load()[0];

                Color pixel = image.GetPixel(2, 2);

                return image.Encoding.ToLinear(green ? pixel.Green : pixel.Red);
            }

            Assert.AreEqual(0.9, Middle("", "", 1), 1e-4, "soft, unsaid");
            Assert.AreEqual(0.6, Middle("film curve soft", "", 2, "[1, 0.3, 0.1]", true), 1e-4, "soft leaves the green");
            Assert.AreEqual(
                FilmCurve.Neutral.Develop(new Color(2, 0.6, 0.2)).Green,
                Middle("film curve neutral", "", 2, "[1, 0.3, 0.1]", true), 1e-4, "neutral keeps the hue");
            Assert.AreEqual(1, Middle("film curve none", "", 1), 1e-4, "no curve");
            Assert.AreEqual(1, Middle("no gamma", "", 1), 1e-4, "no gamma");
            Assert.AreEqual(0.5, Middle("", "exposure 1", 0.25), 1e-4, "a stop up");
            Assert.AreEqual(
                FilmCurve.AgX.Develop(new Color(0.5, 0.5, 0.5)).Red, Middle("film curve agx", "", 0.5), 1e-4, "AgX");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
