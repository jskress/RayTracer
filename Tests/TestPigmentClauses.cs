using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how a scene writes a pigment, checked by rendering, since a clause that
/// parses may still build the wrong thing.  What each pattern looks like is covered by
/// <see cref="TestPatterns"/>; these are about the words a scene is allowed to write.
/// </summary>
[TestClass]
public class TestPigmentClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pigment-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders a ball wearing the given pigment, and reports the error that stopped it, if any.
    /// </summary>
    private string ErrorFrom(string pigment)
    {
        return BallWearing(pigment).Error;
    }

    /// <summary>
    /// Renders a ball wearing the given pigment and hands back the picture, or the error that
    /// stopped it.  The ball is lit by its own pigment alone -- all ambient, no diffuse or specular
    /// -- when a size is asked for, so that a color read off the picture is the color the pigment
    /// gave rather than that color shaded.
    /// </summary>
    private (Canvas Image, string Error) BallWearing(string pigment, int size = 8)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");
        string finish = size > 8 ? "  ambient 1  diffuse 0  specular 0" : "";

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" +
            $"sphere {{ material {{ {pigment}{finish} }} }}");

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
                OutputFileName = output, Width = size, Height = size
            });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Writes a two-color image to the working directory and hands back its name.  The two colors sit
    /// one above the other, since a spherical map runs the image's height from pole to pole: a ball
    /// wearing it is the first color on top and the second below, whichever way it is turned.
    /// </summary>
    private string TwoColorImage(Color above, Color below)
    {
        string name = Path.Combine(_directory, "two-color.png");
        Canvas canvas = new (2, 2);

        canvas.SetColor(above, 0, 0);
        canvas.SetColor(above, 1, 0);
        canvas.SetColor(below, 0, 1);
        canvas.SetColor(below, 1, 1);

        new ImageFile(name).Save(canvas, new RenderContext { ApplyGamma = false });

        return name;
    }

    /// <summary>
    /// Counts how many pixels of the given picture carry the given color, loosely enough to absorb
    /// being written to a file and read back.
    /// </summary>
    private static int PixelsCarrying(Canvas image, Color color)
    {
        int count = 0;

        for (int x = 0; x < image.Width; x++)
        for (int y = 0; y < image.Height; y++)
        {
            Color pixel = image.GetPixel(x, y);

            if (Math.Abs(pixel.Red - color.Red) < 0.02 &&
                Math.Abs(pixel.Green - color.Green) < 0.02 &&
                Math.Abs(pixel.Blue - color.Blue) < 0.02)
                count++;
        }

        return count;
    }

    [TestMethod]
    public void TestAnAgateNeedsNoTurbulence()
    {
        // Turbulence is what gives agate its wandering bands, so a scene nearly always asks for
        // some -- but leaving it off used to take the render down with a null reference rather
        // than simply drawing the unstirred banding.  Marble and wood had always coped.
        Assert.IsNull(ErrorFrom("pigment agate { [0, Red, 1, Blue] }"));
        Assert.IsNull(ErrorFrom("pigment marble { [0, Red, 1, Blue] }"));
        Assert.IsNull(ErrorFrom("pigment wood { [0, Red, 1, Blue] }"));
    }

    [TestMethod]
    public void TestAnImagePigmentMayBeAskedForUncached()
    {
        // "uncached" makes it the first word of the clause, and the pigment parser dispatches on
        // that first word -- so this used to be mistaken for the name of a pattern and rejected
        // with "Unsupported uncached pattern found."
        string image = Path.Combine(_directory, "texture.png");

        // Any real image will do; render a tiny one to point at.
        File.WriteAllText(Path.Combine(_directory, "make.igl"),
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" +
            "sphere { }");
        new LanguageParser(Path.Combine(_directory, "make.igl")).Parse()
            .Render(new RenderOptions { OutputFileName = image, Width = 8, Height = 8 });

        Assert.IsNull(ErrorFrom($"pigment image '{image}'"));
        Assert.IsNull(ErrorFrom($"pigment uncached image '{image}'"));
        Assert.IsNull(ErrorFrom($"pigment uncached image '{image}' spherical"));
    }

    [TestMethod]
    public void TestAPlanarImageMapTilesRatherThanFallingOffTheImage()
    {
        // Left to repeat, a planar map tiles, so a surface reaching to the left of the origin --
        // which a cube at the origin does -- asks for a negative coordinate.  C#'s remainder
        // keeps the sign of its left operand, so that used to index off the front of the image
        // and take the render down.  A second bug sat beside it: "&&" binds tighter than "||",
        // so the Z guard applied whether or not "once" had been asked for.
        string image = Path.Combine(_directory, "texture.png");

        File.WriteAllText(Path.Combine(_directory, "make.igl"),
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -8] }\n" +
            "sphere { material { pigment checker { Red, Blue } } }");
        new LanguageParser(Path.Combine(_directory, "make.igl")).Parse()
            .Render(new RenderOptions { OutputFileName = image, Width = 16, Height = 16 });

        Assert.IsNull(ErrorFrom($"pigment image '{image}' planar"),
            "a tiling planar map must cope with negative coordinates");
        Assert.IsNull(ErrorFrom($"pigment image '{image}' planar once"));
    }

    [TestMethod]
    public void TestAnImagePigmentInsideAnotherPigmentStillLoadsItsImage()
    {
        // A pigment gets its chance to get ready for rendering just before the first ray is fired,
        // and reading an image is what an image pigment does with that chance.  A pigment that holds
        // other pigments used to keep the chance to itself -- unlike a seed, which it passes down --
        // so a nested image pigment never loaded, and the first ray to ask it for a color took the
        // render down with a null reference.  Every kind of pigment that holds another had it.
        Color above = new (0, 1, 0);
        Color below = new (0, 0, 1);
        string image = TwoColorImage(above, below);

        string[] nestings =
        [
            $"pigment checker {{ image '{image}' spherical, color Red }}",
            $"pigment blend {{ image '{image}' spherical, image '{image}' spherical }}",
            $"pigment layer {{ image '{image}' spherical, color Red }}",
            $"pigment mottled {{ noise {{ octaves 1 }} image '{image}' spherical }}"
        ];

        foreach (string nesting in nestings)
        {
            (Canvas picture, string error) = BallWearing(nesting, 60);

            Assert.IsNull(error, $"{nesting}\n{error}");

            // The picture must actually carry what the image says, not merely have been drawn: the
            // colors are the image's own, and the ball is lit by nothing but its pigment, so they
            // arrive unshaded.  "mottled" is the exception -- dimming by noise is the whole point of
            // it -- so there it is enough that the two halves came out differently at all.
            if (nesting.Contains("mottled"))
            {
                HashSet<string> colors = [];

                for (int x = 0; x < picture.Width; x++)
                for (int y = 0; y < picture.Height; y++)
                    colors.Add(picture.GetPixel(x, y).ToString());

                Assert.IsTrue(colors.Count > 2,
                    $"the mottled ball came out in {colors.Count} colors, so the image never showed");

                continue;
            }

            Assert.IsTrue(PixelsCarrying(picture, above) > 0,
                $"{nesting} never showed the top half of its image");
            Assert.IsTrue(PixelsCarrying(picture, below) > 0,
                $"{nesting} never showed the bottom half of its image");
        }
    }

    [TestMethod]
    public void TestASeedInsideAnotherPigmentTakesEffect()
    {
        // The other half of handing a child pigment its chance to get ready: a seed written on the
        // child is applied when it takes that chance.  A pigment does pass its own seed down to its
        // children, but a child that names a seed of its own is saying something nearer, and nothing
        // used to act on it -- two scenes differing only in a nested seed drew the very same picture.
        const string map = "bozo { [0, Red, 1, Blue] }";
        Canvas third = BallWearing($"pigment blend {{ with seed 3 {map}, color Green }}", 60).Image;
        Canvas ninth = BallWearing($"pigment blend {{ with seed 9 {map}, color Green }}", 60).Image;
        int differences = 0;

        Assert.IsNotNull(third);
        Assert.IsNotNull(ninth);

        for (int x = 0; x < third.Width; x++)
        for (int y = 0; y < third.Height; y++)
        {
            if (!third.GetPixel(x, y).Matches(ninth.GetPixel(x, y)))
                differences++;
        }

        Assert.IsTrue(differences > 100,
            $"two seeds should not draw the same picture, and differed in {differences} pixels");

        // And the nearer seed is the one that counts, which is what the guide has always said a seed
        // on a pigment as a whole means: it reaches everything inside that has not been given one of
        // its own.  So what the outer seed says makes no difference here at all.
        Canvas under7 = BallWearing($"pigment with seed 7 blend {{ with seed 3 {map}, color Green }}", 60).Image;
        Canvas under11 = BallWearing($"pigment with seed 11 blend {{ with seed 3 {map}, color Green }}", 60).Image;

        Assert.IsNotNull(under7);
        Assert.IsNotNull(under11);

        for (int x = 0; x < under7.Width; x++)
        for (int y = 0; y < under7.Height; y++)
        {
            Assert.IsTrue(under7.GetPixel(x, y).Matches(under11.GetPixel(x, y)),
                $"the outer seed reached past a seed of its own at {x}, {y}");
            Assert.IsTrue(under7.GetPixel(x, y).Matches(third.GetPixel(x, y)),
                $"an outer seed changed what the inner seed drew at {x}, {y}");
        }
    }

    [TestMethod]
    public void TestADiscretePatternTakesExactlyTheColorsItNeeds()
    {
        // Each of these wants a fixed number of plain colors rather than a color map, and says
        // so when given the wrong count.
        Assert.IsNull(ErrorFrom("pigment checker { Red, Blue }"));
        Assert.IsNull(ErrorFrom("pigment hexagon { Red, Blue, Green }"));
        Assert.IsNull(ErrorFrom("pigment square { Red, Blue, Green, Yellow }"));

        Assert.IsNotNull(ErrorFrom("pigment checker { Red, Blue, Green }"),
            "a checker takes two colors and should refuse a third");
    }

    [TestMethod]
    public void TestAContinuousPatternTakesAColorMap()
    {
        Assert.IsNull(ErrorFrom("pigment granite { [0, Red, 1, Blue] }"));
        Assert.IsNull(ErrorFrom("pigment linear gradient { [0, Red, 1, Blue] }"));

        Assert.IsNotNull(ErrorFrom("pigment granite { Red, Blue }"),
            "a granite is continuous and wants a map rather than two bare colors");
    }
}
