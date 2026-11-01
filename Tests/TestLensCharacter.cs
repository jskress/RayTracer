using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// A lens's character: how it darkens toward its corners, how it bows straight lines, and how it parts red from
/// blue.
/// </summary>
[TestClass]
public class TestLensCharacter
{
    private const int Width = 201;
    private const int Height = 101;

    private static RenderContext Context() => new () { Width = Width, Height = Height };

    // Where, on the plane one unit out, a pinhole's ray through a pixel's middle lands.
    private static (double X, double Y) Landing(Ray ray) => (ray.Direction.X / -ray.Direction.Z,
        ray.Direction.Y / -ray.Direction.Z);

    /// <summary>
    /// A camera that says none of it is the camera it always was, ray for ray.
    /// </summary>
    [TestMethod]
    public void TestALensWithNoCharacterIsUnchanged()
    {
        CameraSampler lens = new (0.3, 4, 0, 16);
        PerspectiveRayConverter before = new (Context(), Math.PI / 2, Matrix.Identity, lens);
        PerspectiveRayConverter after = new (Context(), Math.PI / 2, Matrix.Identity, lens, 0, 0);

        foreach ((int x, int y) in new[] { (0, 0), (100, 50), (200, 100), (17, 83) })
        {
            for (int index = 0; index < lens.SampleCount; index++)
            {
                Ray one = before.GetRayForPixel(x, y, sampleIndex: index);
                Ray other = after.GetRayForPixel(x, y, sampleIndex: index);

                Assert.IsTrue(one.Origin.Matches(other.Origin) && one.Direction.Matches(other.Direction));
                Assert.AreEqual(one.Spread, other.Spread);
                Assert.IsTrue(after.Passes(x, y, 0.5, 0.5, 0, 0, index));
            }

            Assert.AreEqual(1, after.FalloffAt(x, y));
        }
    }

    /// <summary>
    /// Distortion leaves the middle of the picture where it was and stretches the place a pixel looks at by one and
    /// the distortion times the square of how far toward a corner it is; the ray widens as fast as the stretch grows.
    /// More than nought sends the corners' rays further out, so straight lines bow outward: a barrel.
    /// </summary>
    [TestMethod]
    public void TestDistortionStretchesTowardTheCorners()
    {
        const double Distortion = 0.2;
        PerspectiveRayConverter pinhole = new (Context(), Math.PI / 2);
        PerspectiveRayConverter bowed = new (Context(), Math.PI / 2, Matrix.Identity, null, Distortion, 0);
        double halfWidth = 1, halfHeight = (double) Height / Width;
        double halfDiagonal2 = halfWidth * halfWidth + halfHeight * halfHeight;

        (double mx, double my) = Landing(bowed.GetRayForPixel(100, 50));

        Assert.AreEqual(0, mx, 1e-12);
        Assert.AreEqual(0, my, 1e-12);

        foreach ((int x, int y) in new[] { (0, 0), (200, 100), (30, 70), (150, 10) })
        {
            (double px, double py) = Landing(pinhole.GetRayForPixel(x, y));
            Ray ray = bowed.GetRayForPixel(x, y);
            (double bx, double by) = Landing(ray);
            double reach = (px * px + py * py) / halfDiagonal2;

            Assert.AreEqual(px * (1 + Distortion * reach), bx, 1e-9);
            Assert.AreEqual(py * (1 + Distortion * reach), by, 1e-9);
            Assert.AreEqual(pinhole.PixelSize * (1 + 3 * Distortion * reach), ray.Spread, 1e-12);
        }
    }

    /// <summary>
    /// A lens that vignettes lets through, at each pixel's middle, the fourth power of the cosine of how far off the
    /// axis the pixel looks, blended in by how much it vignettes.
    /// </summary>
    [TestMethod]
    public void TestVignettingFallsOffAsTheFourthPowerOfTheCosine()
    {
        PerspectiveRayConverter full = new (Context(), Math.PI / 2, Matrix.Identity, null, 0, 1);
        PerspectiveRayConverter half = new (Context(), Math.PI / 2, Matrix.Identity, null, 0, 0.5);
        PerspectiveRayConverter pinhole = new (Context(), Math.PI / 2);

        foreach ((int x, int y) in new[] { (100, 50), (0, 0), (200, 37), (60, 90) })
        {
            Vector look = pinhole.GetRayForPixel(x, y).Direction;
            double cos = -look.Z / look.Magnitude;
            double natural = Math.Pow(cos, 4);

            Assert.AreEqual(natural, full.FalloffAt(x, y), 1e-12);
            Assert.AreEqual(0.5 + 0.5 * natural, half.FalloffAt(x, y), 1e-12);
        }
    }

    /// <summary>
    /// With the lens open, its barrel lets through all of the opening at the middle of the picture and, at a corner,
    /// only where the opening overlaps a second one as wide, standing off by the vignetting times its radius: a cat's
    /// eye, longer round the middle of the picture than toward it.  Two circles of radius one whose middles stand d
    /// apart overlap by 2 acos(d / 2) - (d / 2) sqrt(4 - d^2).
    /// </summary>
    [TestMethod]
    public void TestTheBarrelCutsTheOpeningIntoACatsEye()
    {
        const int Samples = 8192;
        CameraSampler lens = new (0.5, 4, 0, Samples);
        PerspectiveRayConverter open = new (Context(), Math.PI / 2, Matrix.Identity, lens, 0, 1);
        PerspectiveRayConverter pinhole = new (Context(), Math.PI / 2);
        double halfDiagonal = Math.Sqrt(1 + (double) Height / Width * ((double) Height / Width));

        Assert.AreEqual(Samples, Enumerable.Range(0, Samples).Count(index => open.Passes(100, 50, 0.5, 0.5, 0, 0, index)));

        (double cx, double cy) = Landing(pinhole.GetRayForPixel(200, 0));
        double d = Math.Sqrt(cx * cx + cy * cy) / halfDiagonal;
        double overlap = (2 * Math.Acos(d / 2) - d / 2 * Math.Sqrt(4 - d * d)) / Math.PI;
        List<(double X, double Y)> passed = Enumerable.Range(0, Samples)
            .Where(index => open.Passes(200, 0, 0.5, 0.5, 0, 0, index))
            .Select(lens.OffsetFor)
            .ToList();

        Assert.AreEqual(overlap, (double) passed.Count / Samples, 0.01, "the share of the opening left at the corner");

        // How far the cat's eye reaches toward the picture's middle, and round it.
        double unit = Math.Sqrt(cx * cx + cy * cy);
        double[] toward = passed.Select(p => (p.X * cx + p.Y * cy) / unit).ToArray();
        double[] round = passed.Select(p => (-p.X * cy + p.Y * cx) / unit).ToArray();

        Assert.IsTrue(round.Max() - round.Min() > 1.4 * (toward.Max() - toward.Min()), "longer round than toward");
    }

    /// <summary>
    /// Chromatic aberration draws the red picture larger by half its amount and the blue smaller by half, about the
    /// middle, green standing still: a line a hundred pixels from the middle shows its red that much further out and
    /// its blue that much further in, and a line through the middle stays where it is.
    /// </summary>
    [TestMethod]
    public void TestChromaticAberrationPartsRedFromBlueTowardTheCorners()
    {
        const double Amount = 0.04;
        Canvas canvas = new (Width, Height);

        for (int y = 0; y < Height; y++)
        {
            canvas.SetColor(new Color(1, 1, 1), 100, y);
            canvas.SetColor(new Color(1, 1, 1), 190, y);
        }

        new ChromaticAberration(Amount).Part(canvas);

        // Where along the row each color stands, within reach of each line.
        double Middle(Func<Color, double> channel, int from, int to)
        {
            double sum = 0, weight = 0;

            for (int x = from; x <= to; x++)
            {
                double value = channel(canvas.GetPixel(x, Height / 2));

                sum += value * (x + 0.5);
                weight += value;
            }

            return sum / weight;
        }

        double away = 190.5 - Width / 2.0;

        Assert.AreEqual(100.5, Middle(c => c.Red, 95, 106), 1e-9);
        Assert.AreEqual(100.5, Middle(c => c.Blue, 95, 106), 1e-9);
        Assert.AreEqual(190.5, Middle(c => c.Green, 185, 200), 1e-9);
        Assert.AreEqual(Width / 2.0 + away * (1 + Amount / 2), Middle(c => c.Red, 185, 200), 0.02);
        Assert.AreEqual(Width / 2.0 + away * (1 - Amount / 2), Middle(c => c.Blue, 180, 200), 0.02);
    }

    /// <summary>
    /// This helper renders a small scene and returns the picture, or what went wrong.
    /// </summary>
    private static (Canvas Image, string Output) Render(string scene, int width = 40, int height = 30)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"lens-tests-{Guid.NewGuid():N}");
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

            renderer.Render(new RenderOptions { OutputFileName = output, Width = width, Height = height });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], captured.ToString());
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

    /// <summary>
    /// A pixel at a picture's corner, seen through an open lens that vignettes against a sky of even light, gets the
    /// share of the lens's samples its barrel lets through, times the natural falloff there: the samples it stops
    /// bring no light, and still say nothing of what the pixel covers.
    /// </summary>
    [TestMethod]
    public void TestACornerGetsWhatTheBarrelAndTheSlantLetThrough()
    {
        (Canvas image, string output) = Render(
            "camera { location [0, 0, -5]  look at [0, 0, 0]  aperture 0.2  blur samples 4096  vignetting 1 }\n" +
            "background White\n");

        Assert.IsNotNull(image, output);

        CameraSampler lens = new (0.2, 5, 0, 4096);
        PerspectiveRayConverter converter = new (
            new RenderContext { Width = 40, Height = 30 }, 50.0 * Math.PI / 180, Matrix.Identity, lens, 0, 1);
        Color corner = image.GetPixel(0, 0);

        Assert.AreEqual(Passed(0, 0) * converter.FalloffAt(0, 0), corner.Red, 1e-3);
        Assert.AreEqual(1, corner.Alpha, 1e-6);

        // Even beside the middle, half a pixel off it, the barrel takes a sliver.
        Assert.AreEqual(Passed(20, 15) * converter.FalloffAt(20, 15), image.GetPixel(20, 15).Red, 1e-3);

        double Passed(int x, int y) =>
            (double) Enumerable.Range(0, 4096).Count(index => converter.Passes(x, y, 0.5, 0.5, 0, 0, index)) / 4096;
    }

    /// <summary>
    /// A lens's character is a perspective camera's: another projection says so and ignores it; and what cannot be
    /// is refused.
    /// </summary>
    [TestMethod]
    public void TestOtherProjectionsIgnoreItAndWhatCannotBeIsRefused()
    {
        (_, string output) = Render(
            "fisheye camera { location [0, 0, -5]  look at [0, 0, 0]  vignetting 0.5 }\nbackground White\n");

        StringAssert.Contains(output, "its vignetting, distortion and chromatic aberration are ignored");
        StringAssert.Contains(Render("camera { vignetting 1.5 }\n").Output, "vignetting runs from 0");
        StringAssert.Contains(Render("camera { distortion -0.4 }\n").Output, "folds its corners back");
        StringAssert.Contains(Render("camera { chromatic aberration 0.6 }\n").Output, "from 0 to under a half");
    }
}
