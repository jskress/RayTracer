using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the per-point "scale" a spline may carry, checked the way the rest of the
/// DSL is checked here: by rendering, since a scale that parses may still never reach the
/// geometry.  What the scale does to the loft is settled at the object level in
/// <see cref="TestSweep.TestScaleGrowsProfileAlongSpline"/>; these confirm the word reaches the
/// renderer -- on every spline command -- and actually widens what is swept.
/// </summary>
[TestClass]
public class TestSweepClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"sweep-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A camera looking straight at the origin from -Z, with a lamp beside it, so a bar swept
    /// up the Y axis stands upright in the image and its width across each row can be read
    /// straight off.  The bar is drawn flat white (ambient 1) so any lit pixel is the sweep and
    /// the black background is everything else.
    /// </summary>
    private const string Setup =
        "camera { location [0, 0, -10] look at [0, 0, 0] }\n" +
        "point light { location [0, 0, -10] color White }";

    /// <summary>
    /// Renders the given sweep body, and returns the image, or reports the error that stopped
    /// it.
    /// </summary>
    private Canvas Render(string body, out string error, int size = 200)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path, Setup + "\n" + body);

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
    /// Counts the lit (non-black) pixels across the image row the given fraction of the way
    /// down -- the width of whatever the sweep covers at that height.
    /// </summary>
    private static int LitWidth(Canvas image, double fractionDown)
    {
        int row = (int) (image.Height * fractionDown);
        int count = 0;

        for (int x = 0; x < image.Width; x++)
        {
            Color pixel = image.GetPixel(x, row);

            if ((pixel.Red + pixel.Green + pixel.Blue) / 3 > 0.5)
                count++;
        }

        return count;
    }

    /// <summary>
    /// The widest the sweep gets across any row -- how big its cross-section grew at its
    /// fattest.
    /// </summary>
    private static int MaxWidth(Canvas image)
    {
        int widest = 0;

        for (int row = 0; row < image.Height; row++)
            widest = Math.Max(widest, LitWidth(image, (double) row / image.Height));

        return widest;
    }

    /// <summary>
    /// The total lit pixels in the image -- enough to tell that something was swept at all.
    /// </summary>
    private static int TotalLit(Canvas image)
    {
        int count = 0;

        for (int row = 0; row < image.Height; row++)
            count += LitWidth(image, (double) row / image.Height);

        return count;
    }

    /// <summary>
    /// A closed square profile, so the bar has a definite width to measure.
    /// </summary>
    private const string BarProfile =
        "profile { move to -0.3, -0.3  line to 0.3, -0.3  line to 0.3, 0.3  line to -0.3, 0.3  close }";

    /// <summary>
    /// Wraps the given spline in a flat-white bar sweep with the square profile above.
    /// </summary>
    private static string Bar(string spline) =>
        "sweep {\n" +
        "    material { pigment color White  ambient 1  diffuse 0  specular 0 }\n" +
        "    " + BarProfile + "\n" +
        "    " + spline + "\n" +
        "    steps 8\n" +
        "}";

    /// <summary>
    /// A bar swept straight up the Y axis at a constant (default) scale is the same width top
    /// and bottom.  This is the control for <see cref="TestScaleWidensTheTopOfASweep"/>: it
    /// shows the head-on view itself doesn't taper the bar, so a taper there means the scale
    /// did it.
    /// </summary>
    [TestMethod]
    public void TestUnscaledSweepIsTheSameWidthTopAndBottom()
    {
        Canvas image = Render(Bar("spline { move to 0, -2.5, 0  line to 0, 2.5, 0 }"), out string error);

        Assert.IsNull(error);

        int top = LitWidth(image, 0.3);
        int bottom = LitWidth(image, 0.7);

        Assert.IsTrue(top > 0 && bottom > 0, "the bar should be visible top and bottom");
        Assert.IsTrue(
            Math.Abs(top - bottom) <= 4, $"a constant-scale bar should not taper (top={top}, bottom={bottom})");
    }

    /// <summary>
    /// The same bar, scaled 1 at the foot and 3 at the top, must grow toward the top: the
    /// widest it ever gets must dwarf the constant-width unscaled bar, and within the scaled
    /// bar the top must be wider than the foot -- the scale reaching the geometry through the
    /// DSL and being interpolated along the run.
    /// </summary>
    [TestMethod]
    public void TestScaleWidensTheTopOfASweep()
    {
        Canvas plain = Render(Bar("spline { move to 0, -2.5, 0  line to 0, 2.5, 0 }"), out string plainError);
        Canvas scaled = Render(
            Bar("spline { move to 0, -2.5, 0  scale 1  line to 0, 2.5, 0  scale 3 }"), out string scaledError);

        Assert.IsNull(plainError);
        Assert.IsNull(scaledError);

        int plainMax = MaxWidth(plain);
        int scaledMax = MaxWidth(scaled);

        Assert.IsTrue(
            scaledMax > plainMax * 2,
            $"the scaled bar's widest ({scaledMax}) should dwarf the unscaled bar's ({plainMax})");

        int top = LitWidth(scaled, 0.3);
        int bottom = LitWidth(scaled, 0.7);

        Assert.IsTrue(bottom > 0, "the foot of the scaled bar should still be visible");
        Assert.IsTrue(top > bottom, $"the scaled bar should taper toward the foot (top={top}, bottom={bottom})");
    }

    /// <summary>
    /// The scale suffix is wired onto every spline command the same way, so the quad and curve
    /// forms must take it too, not just move and line.  A scene that uses it on all four must
    /// parse and render without complaint.
    /// </summary>
    [TestMethod]
    public void TestScaleParsesOnEverySplineCommand()
    {
        Canvas image = Render(
            Bar("discontinuous spline {\n" +
                "        move to -2, 0, 0  scale 0.5\n" +
                "        line to -1, 0, 0  scale 0.75\n" +
                "        quad -0.5, 1, 0 to 0.5, 0, 0  scale 1\n" +
                "        curve 1, 1, 0, 2, -1, 0 to 3, 0, 0  scale 1.5\n" +
                "    }"),
            out string error);

        Assert.IsNull(error);
        Assert.IsTrue(TotalLit(image) > 0, "the swept bar should be visible");
    }
}
