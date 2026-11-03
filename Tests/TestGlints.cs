using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover glints: a surface's facets counted under each pixel rather than averaged, so that where few
/// catch a lamp the pixel either misses it or blazes, and where many do the smooth highlight is what shows.
/// </summary>
[TestClass]
public class TestGlints
{
    private static readonly Vector Up = new (0, 1, 0);

    // A footprint square to the surface, the given width each way, lying in the X/Z plane.
    private static Footprint Patch(double width) => new (new Vector(width, 0, 0), new Vector(0, 0, width));

    /// <summary>
    /// A count is drawn as a Poisson law of its mean draws one: its mean and its spread are the law's, whether the
    /// mean is small enough to be summed or large enough to be drawn from the normal law it approaches.
    /// </summary>
    [TestMethod]
    public void TestACountIsDrawnAsAPoissonLawDrawsIt()
    {
        const int Draws = 200_000;

        foreach (double mean in new[] { 0.05, 0.7, 6, 40, 300 })
        {
            double sum = 0, squares = 0;

            for (int index = 0; index < Draws; index++)
            {
                double count = Glints.Poisson(mean, (index + 0.5) / Draws);

                sum += count;
                squares += count * count;
            }

            double average = sum / Draws;
            double spread = squares / Draws - average * average;

            Assert.AreEqual(mean, average, mean * 0.01 + 1e-4, $"a law of mean {mean} drew a mean of {average}");
            Assert.AreEqual(mean, spread, mean * 0.03 + 1e-3, $"a law of mean {mean} drew a spread of {spread}");
        }
    }

    /// <summary>
    /// Over many places, glints give a lamp the smooth highlight on average however few facets each pixel's patch
    /// holds -- most places nothing, a few many times as much; and a patch holding plenty gives exactly the smooth
    /// highlight, as does a ray with no patch at all.
    /// </summary>
    [TestMethod]
    public void TestGlintsAverageToTheSmoothHighlight()
    {
        Glints glints = new () { Size = 0.0005 };
        Vector halfway = new Vector(0.1, 1, 0.05).Unit;
        const double Alpha = 0.12;

        foreach (double width in new[] { 0.002, 0.006, 0.02 })
        {
            Footprint patch = Patch(width);
            double sum = 0;
            int places = 0, lit = 0;

            for (int x = 0; x < 600; x++)
            {
                for (int z = 0; z < 600; z++)
                {
                    Point local = new ((x + 0.5) * width, 0, (z + 0.5) * width);
                    double share = glints.ShareAt(local, patch, patch, Up, halfway, Alpha, 0);

                    sum += share;
                    places++;

                    if (share > 0)
                        lit++;
                }
            }

            Assert.AreEqual(1, sum / places, 0.04, $"glints under a patch {width} wide averaged {sum / places}");

            if (width < 0.01)
                Assert.IsTrue(lit < places / 4, $"under a patch {width} wide, {lit} of {places} places glinted");
        }

        // Under a patch smaller than a facet, which sees one facet or part of one: lit by a wide patch of a lamp,
        // so that enough of the places glint for their average to be worth taking.
        double small = 0;

        for (int x = 0; x < 600; x++)
        for (int z = 0; z < 600; z++)
            small += glints.ShareAt(new Point((x + 0.5) * 0.0006, 0, (z + 0.5) * 0.0006), Patch(0.0003), Patch(0.0003),
                Up, halfway, Alpha, 0.2);

        Assert.AreEqual(1, small / 360000, 0.04, $"glints under a patch smaller than a facet averaged {small / 360000}");
        Assert.AreEqual(1, glints.ShareAt(Point.Zero, Patch(5), Patch(5), Up, halfway, Alpha, 0));
        Assert.AreEqual(1, glints.ShareAt(Point.Zero, Footprint.None, Footprint.None, Up, halfway, Alpha, 0));
    }

    /// <summary>
    /// A glint belongs to the place: asked again, or a little way off within the same patch's cell, the facets
    /// there give the same; moved to another cell, or lit from another way, other facets answer.
    /// </summary>
    [TestMethod]
    public void TestAGlintStaysWhereItIs()
    {
        Glints glints = new () { Size = 0.0005 };
        Footprint patch = Patch(0.004);
        Vector halfway = new Vector(0.05, 1, 0.02).Unit;
        Vector turned = new Vector(0.12, 1, 0.02).Unit;
        int same = 0, otherCell = 0, otherWay = 0, glinting = 0;

        for (int index = 0; index < 20000; index++)
        {
            Point local = new ((index % 200 + 0.3) * 0.004, 0, (index / 200 + 0.3) * 0.004);
            double share = glints.ShareAt(local, patch, patch, Up, halfway, 0.12, 0);

            if (share == 0)
                continue;

            glinting++;

            if (glints.ShareAt(local + new Vector(0.0002, 0, 0.0002), patch, patch, Up, halfway, 0.12, 0) == share &&
                glints.ShareAt(local, patch, patch, Up, halfway, 0.12, 0) == share)
                same++;

            if (glints.ShareAt(local + new Vector(0.05, 0, 0), patch, patch, Up, halfway, 0.12, 0) == share)
                otherCell++;

            if (glints.ShareAt(local, patch, patch, Up, turned, 0.12, 0) > 0)
                otherWay++;
        }

        Assert.IsTrue(glinting > 100, $"only {glinting} places glinted");
        Assert.AreEqual(glinting, same, "a glint moved within its own cell, or on being asked again");
        Assert.IsTrue(otherCell < glinting / 3, $"{otherCell} of {glinting} glints were the same a cell away");
        Assert.IsTrue(otherWay < glinting / 3, $"{otherWay} of {glinting} glints still glinted lit from another way");
    }

    /// <summary>
    /// In a picture, a rough surface whose facets glint gives back as much of the lamp as the same surface smooth,
    /// though all of it in a few pixels; and a matte surface given grains glints over what it shows without them.
    /// </summary>
    [TestMethod]
    public void TestAPictureGlintsAsBrightlyAsItShinesSmooth()
    {
        const string Stage = """
            camera { location [0, 2, -0.6]  look at [0, 0, 0]  field of view 40 }
            distant light { direction [0.1, -1, -0.35]  color [1, 1, 1] }
            """;

        Canvas smooth = Render(Stage + "plane { material { pigment [0.02, 0.02, 0.02]  roughness 0.35 } }");
        Canvas glinting = Render(
            Stage + "plane { material { pigment [0.02, 0.02, 0.02]  roughness 0.35  glints { size 0.0004 } } }");
        Canvas matte = Render(Stage + "plane { material { pigment [0.5, 0.5, 0.5]  specular 0 } }");
        Canvas grains = Render(
            Stage + "plane { material { pigment [0.5, 0.5, 0.5]  specular 0  glints { size 0.0004  roughness 0.35 } } }");

        double Sum(Canvas canvas)
        {
            double total = 0;

            for (int x = 0; x < canvas.Width; x++)
            for (int y = 0; y < canvas.Height; y++)
                total += canvas.GetPixel(x, y).Green;

            return total;
        }

        int Blazing(Canvas canvas, Canvas under)
        {
            int count = 0;

            for (int x = 0; x < canvas.Width; x++)
            for (int y = 0; y < canvas.Height; y++)
                count += canvas.GetPixel(x, y).Green > under.GetPixel(x, y).Green * 3 + 0.05 ? 1 : 0;

            return count;
        }

        Assert.AreEqual(Sum(smooth), Sum(glinting), Sum(smooth) * 0.05,
            "the glinting surface gave back a different share of the lamp from the smooth one");
        Assert.IsTrue(Blazing(glinting, smooth) > 50, "no pixel of the glinting surface blazed");
        Assert.IsTrue(Blazing(grains, matte) > 50, "no grain on the matte surface glinted");
        Assert.IsTrue(Sum(grains) > Sum(matte), "the matte surface's grains gave back nothing");
    }

    /// <summary>
    /// What glints cannot be is refused: facets of no size, glints on a brushed surface, and grains' own roughness on
    /// a surface that is rough already.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotGlintIsRefused()
    {
        const string Stage = "camera { location [0, 2, -4]  look at [0, 0, 0] }\npoint light { location [0, 5, -5] }\n";

        StringAssert.Contains(Error(Stage + "plane { material { glints { size 0 } } }"), "more than nought");
        StringAssert.Contains(
            Error(Stage + "plane { material { roughness 0.3  anisotropy 0.5  glints { size 0.001 } } }"), "brushed");
        StringAssert.Contains(
            Error(Stage + "plane { material { roughness 0.3  glints { size 0.001  roughness 0.5 } } }"), "its own facets");
    }

    /// <summary>
    /// This helper renders a small scene to light, unclamped, and returns the picture.
    /// </summary>
    private static Canvas Render(string scene)
    {
        (Canvas image, string error) = Run(scene, true);

        Assert.IsNull(error, error);

        return image;
    }

    /// <summary>
    /// This helper renders a small scene and returns what stopped it, if anything did.
    /// </summary>
    private static string Error(string scene) => Run(scene, false).Error;

    private static (Canvas Image, string Error) Run(string scene, bool keep)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"glint-tests-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "scene.igl");
        string output = Path.Combine(directory, "out.pfm");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, scene);
        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 160, Height = 120 });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (keep ? new ImageFile(output).Load()[0] : null, null);
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
            Directory.Delete(directory, true);
        }
    }
}
