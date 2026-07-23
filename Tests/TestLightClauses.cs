using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how a scene writes each sort of light, checked the way the rest of the DSL is
/// checked here: by rendering, since a light that parses may still do nothing.  What sort of light
/// each keyword builds is settled at the object level in <see cref="TestDistantAndSpotLights"/>;
/// these confirm the words reach the renderer and behave as their sort should.
/// </summary>
[TestClass]
public class TestLightClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"light-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders the given scene body over a wide floor viewed from above, and returns the rendered
    /// image, or reports the error that stopped it.
    /// </summary>
    private Canvas Render(string body, out string error, int size = 60)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            "camera { location [0, 12, 0] look at [0, 0, 0] up [0, 0, 1] }\n" +
            body + "\n" +
            "plane { material { pigment color [1, 1, 1] ambient 0 diffuse 1 specular 0 } }");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
            {
                error = captured.ToString();

                return null;
            }

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = size, Height = size
            });

            error = captured.ToString().Contains("Error") ? captured.ToString() : null;

            return error is null ? new ImageFile(output).Load()[0] : null;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Returns the brightness, 0 to 1, at the given fraction across the middle row of the image.
    /// </summary>
    private static double BrightnessAcross(Canvas image, double fraction)
    {
        Color pixel = image.GetPixel((int) (image.Width * fraction), image.Height / 2);

        return (pixel.Red + pixel.Green + pixel.Blue) / 3;
    }

    [TestMethod]
    public void TestAPlainLightLightsTheWholeFloor()
    {
        // A lamp overhead lights the floor from the centre right out to the edge, since nothing
        // bounds where its light may fall.
        Canvas image = Render("light { location [0, 12, 0] color White }", out string error);

        Assert.IsNull(error);
        Assert.IsTrue(BrightnessAcross(image, 0.5) > 0.5, "the centre should be lit");
        Assert.IsTrue(BrightnessAcross(image, 0.08) > 0.3, "the edge should be lit too");
    }

    [TestMethod]
    public void TestASpotlightLeavesAPoolWithDarkAround()
    {
        // The same lamp made a spotlight lights a pool in the middle and leaves the edge dark,
        // which is the whole of what the cone is for.
        Canvas image = Render(
            "spot light { location [0, 12, 0] point at [0, 0, 0] radius 8 falloff 14 color White }",
            out string error);

        Assert.IsNull(error);
        Assert.IsTrue(BrightnessAcross(image, 0.5) > 0.5, "the centre of the pool should be lit");
        Assert.IsTrue(BrightnessAcross(image, 0.08) < 0.05, "outside the cone should be dark");
    }

    [TestMethod]
    public void TestADistantLightRendersAndLightsTheFloor()
    {
        // Straight down, a distant light lights the flat floor evenly -- every point faces it at
        // the same angle, since the rays are parallel.
        Canvas image = Render(
            "distant light { direction [0, -1, 0] color White }", out string error);

        Assert.IsNull(error);

        double centre = BrightnessAcross(image, 0.5);
        double edge = BrightnessAcross(image, 0.12);

        Assert.IsTrue(centre > 0.5, "the floor should be lit");
        Assert.IsTrue(Math.Abs(centre - edge) < 0.05,
            $"a parallel light should light a flat floor evenly, but centre {centre:F2} vs edge {edge:F2}");
    }

    [TestMethod]
    public void TestTheThreeSortsMayShareAScene()
    {
        Canvas image = Render("""
            light { location [-6, 8, 0] }
            distant light { direction [1, -1, 0] }
            spot light { location [0, 12, 0] point at [0, 0, 0] }
            """, out string error);

        Assert.IsNull(error, $"three lights in one scene should render: {error}");
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAMissingPatternIsRejected()
    {
        // "point at" wants a point; leaving off "at" is the easy slip, and should be caught.
        Render("spot light { location [0, 5, 0] point [0, 0, 0] }", out string error);

        Assert.IsNotNull(error, "\"point\" without \"at\" should be an error");
    }

    /// <summary>
    /// Renders a small ball hanging over a floor, lit by the given light from one side and viewed
    /// from straight above, then reads the row of brightnesses that runs through the middle of the
    /// ball's shadow.  A hard light gives a row that jumps from lit to dark and back; a soft one
    /// eases through greys at the edges of the shadow.
    /// </summary>
    private double[] ShadowProfile(string lightBody, out string error)
    {
        string path = Path.Combine(_directory, "shadow.igl");
        string output = Path.Combine(_directory, "shadow.png");

        // Straight down, so the image's middle row runs along the world's X, the way the light is
        // offset -- the shadow the ball throws lands along that row where it can be read across.
        File.WriteAllText(path,
            "camera { location [0, 12, 0] look at [0, 0, 0] up [0, 0, 1] }\n" +
            lightBody + "\n" +
            "plane { material { pigment color [1, 1, 1] ambient 0 diffuse 1 specular 0 } }\n" +
            "sphere { material { pigment color [1, 1, 1] ambient 0 diffuse 1 } translate Y 2 }");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
            {
                error = captured.ToString();

                return null;
            }

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 120, Height = 120 });

            error = captured.ToString().Contains("Error") ? captured.ToString() : null;

            if (error is not null)
                return null;

            Canvas image = new ImageFile(output).Load()[0];
            int row = image.Height / 2;

            return Enumerable.Range(0, image.Width)
                .Select(x =>
                {
                    Color pixel = image.GetPixel(x, row);

                    return (pixel.Red + pixel.Green + pixel.Blue) / 3;
                })
                .ToArray();
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Counts how many samples in the row fall in the grey middle, neither nearly dark nor nearly
    /// lit.  A soft shadow has many such; a hard one has almost none.
    /// </summary>
    private static int GreyCount(double[] profile) =>
        profile.Count(value => value is > 0.15 and < 0.75);

    [TestMethod]
    public void TestAPointLightCastsAShadowWithABodyToIt()
    {
        double[] profile = ShadowProfile(
            "point light { location [-6, 8, 0] color White }", out string error);

        Assert.IsNull(error);
        Assert.IsTrue(profile.Any(value => value < 0.1), "there should be a dark shadow");
        Assert.IsTrue(profile.Any(value => value > 0.8), "there should be lit floor");
    }

    [TestMethod]
    public void TestAnAreaLightSoftensTheShadow()
    {
        // The test is the softening itself, told by comparing the same scene under a point light
        // and under an area light centred at the same place.  Both throw the ball's shadow across
        // the same row and light the ball the same way, so what more grey the area light shows is
        // the penumbra it adds and nothing else -- which sidesteps the ball's own curved shading,
        // that both share and neither should be judged by.
        double[] hard = ShadowProfile(
            "point light { location [-6, 8, 0] color White }", out string hardError);
        double[] soft = ShadowProfile(
            "area light { location [-6, 8, 0] axisU [3, 0, 0] axisV [0, 0, 3] steps 6 color White }",
            out string softError);

        Assert.IsNull(hardError);
        Assert.IsNull(softError);
        Assert.IsTrue(soft.Any(value => value < 0.1), "the soft shadow should still have a dark core");
        Assert.IsTrue(GreyCount(soft) > GreyCount(hard) + 6,
            $"the area light should widen the shadow's edge into a penumbra, but the point light " +
            $"showed {GreyCount(hard)} grey pixels and the area light {GreyCount(soft)}");
    }

    [TestMethod]
    public void TestAnAreaLightIsDeterministic()
    {
        // The jitter is fixed, so the same scene renders the same twice over.
        double[] first = ShadowProfile(
            "area light { location [-6, 8, 0] axisU [3, 0, 0] axisV [0, 0, 3] steps 5 color White }",
            out _);
        double[] second = ShadowProfile(
            "area light { location [-6, 8, 0] axisU [3, 0, 0] axisV [0, 0, 3] steps 5 color White }",
            out _);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void TestAnAreaLightMayTurnOffItsJitterAndSetItsSteps()
    {
        ShadowProfile("""
            area light {
                location [-6, 8, 0]
                axisU [3, 0, 0]  axisV [0, 0, 3]
                uSteps 4  vSteps 2
                no jitter
                color White
            }
            """, out string error);

        Assert.IsNull(error, "an area light with its steps set and its jitter off should render");
    }
}
