using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover giving a light a name and using it again.
/// <para>
/// Every other thing a scene describes could already be named and reused -- a material, a pigment, a
/// surface of any kind -- and a light could not, which mattered once libraries began to hold more than
/// textures.  A sky is two halves that have to agree: the sky you look at and the light it casts.  A
/// library that can package only the first half hands the second back to the author, and getting it
/// wrong is invisible rather than loud, since a scene with no sky light quietly keeps the flat ambient
/// fudge that a sky light is meant to replace.
/// </para>
/// </summary>
[TestClass]
public class TestNamedLights
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

    private const int Wide = 120;
    private const int High = 90;

    /// <summary>
    /// Renders the given scene and hands back the picture, or whatever stopped it.
    /// </summary>
    private (Canvas Image, string Error) Render(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path, "context { angles are degrees  no gamma }\n" + scene);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = Wide, Height = High
            });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.ToString());
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    private const string Staging = """
        camera { location [0, 2, -8]  look at [0, 1, 0]  field of view 45 }
        background [0.05, 0.05, 0.07]
        sphere { material { pigment White  ambient 0 }  translate Y 1 }
        """;

    /// <summary>
    /// How brightly the ball is lit, taken from the middle of it.
    /// </summary>
    private static double Brightness(Canvas image)
    {
        Color pixel = image.GetPixel(Wide / 2, (int) (High * 0.55));

        return pixel.Red + pixel.Green + pixel.Blue;
    }

    [TestMethod]
    public void TestALightMayBeNamedAndUsedAgain()
    {
        (Canvas image, string error) = Render($$"""
            Lamp = point light { location [-5, 6, -6] }
            {{Staging}}
            light Lamp
            """);

        Assert.IsNull(error, error);
        Assert.IsTrue(Brightness(image) > 0.2, "the named lamp should have lit the ball");
    }

    [TestMethod]
    public void TestEverySortOfLightMayBeNamed()
    {
        // All five, since the words before "light" are what tell them apart and the naming has to
        // know every one of them.
        foreach (string light in new[]
                 {
                     "point light { location [-5, 6, -6] }",
                     "light { location [-5, 6, -6] }",
                     "distant light { direction [0.6, -0.6, 0.4] }",
                     "spot light { location [-5, 6, -6]  point at [0, 1, 0] }",
                     "area light { location [-5, 6, -6]  axisU [1, 0, 0]  axisV [0, 1, 0] }",
                     "sky light { samples 8 }"
                 })
        {
            (Canvas image, string error) = Render($$"""
                Named = {{light}}
                {{Staging}}
                light Named
                """);

            Assert.IsNull(error, $"{light}: {error}");
            Assert.IsTrue(Brightness(image) > 0.05, $"{light} should have lit something");
        }
    }

    [TestMethod]
    public void TestAUseMayAdjustWhatItFoundWithoutDisturbingTheName()
    {
        // The same rule a named surface follows: what a use adds belongs to that use.  If the two
        // shared one resolver, the second lamp's color would be on the first as well and both halves
        // of the picture would come out the same.
        (Canvas image, string error) = Render("""
            Lamp = point light { location [-6, 6, -6] }
            camera { location [0, 2, -9]  look at [0, 1, 0]  field of view 50 }
            background [0.02, 0.02, 0.03]
            light Lamp { color [1, 0.1, 0.1] }
            light Lamp { location [6, 6, -6]  color [0.1, 0.1, 1] }
            plane { material { pigment White  ambient 0 }  translate Y -0.6 }
            sphere { material { pigment White  ambient 0 }  translate Y 1 }
            """);

        Assert.IsNull(error, error);

        bool reddish = false;
        bool bluish = false;

        for (int x = 0; x < Wide; x++)
        {
            for (int y = 0; y < High; y++)
            {
                Color pixel = image.GetPixel(x, y);

                reddish |= pixel.Red > pixel.Blue + 0.15;
                bluish |= pixel.Blue > pixel.Red + 0.15;
            }
        }

        Assert.IsTrue(reddish && bluish,
            $"each use should keep its own color, and got red {reddish}, blue {bluish}");
    }

    [TestMethod]
    public void TestANamedSkyAndItsLightTravelTogether()
    {
        // What the whole thing is for.  A sky is a pigment and the light it casts is a light, and a
        // scene that takes one without the other gets a picture that disagrees with itself.
        (Canvas image, string error) = Render("""
            DuskSky = pigment physical sky { sun elevation 12  sun azimuth 105  turbidity 3 }
            DuskLight = sky light { samples 12 }
            camera { location [0, 2, -8]  look at [0, 1, 0]  field of view 45 }
            background DuskSky
            light DuskLight
            plane { material { pigment [0.45, 0.45, 0.42] } }
            sphere { material { pigment [0.8, 0.3, 0.2] }  translate Y 1 }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestUsingALightNobodyNamedIsRefused()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            light Nosuchlight
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("Nosuchlight"), $"the complaint should name it: {error}");
    }
}
