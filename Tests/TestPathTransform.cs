using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the 2D transforms a path may carry -- translate, scale and rotate applied
/// to the whole outline before it is given depth -- checked the way the rest of the DSL is
/// checked here: by rendering.  A unit square is extruded flat-on to the camera and stood up, so
/// what the transform does to the outline can be read straight off the silhouette.
/// </summary>
[TestClass]
public class TestPathTransform
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"path-transform-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A camera looking straight at the origin, with a lamp beside it.
    /// </summary>
    private const string Setup =
        "context { angles are degrees }\n" +
        "camera { location [0, 0, -6] look at [0, 0, 0] }\n" +
        "point light { location [0, 0, -6] color White }";

    /// <summary>
    /// Extrudes the given path body flat white, stood up to face the camera, and returns the
    /// rendered image (or reports the error that stopped it).
    /// </summary>
    private Canvas Render(string pathBody, out string error, int size = 200)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            Setup + "\n" +
            "extrusion {\n" +
            "    material { pigment color White  ambient 1  diffuse 0  specular 0 }\n" +
            "    path { " + pathBody + " }\n" +
            "    min Y 0  max Y 0.1\n" +
            "    rotate X -90\n" +
            "}");

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

            renderer.Render(new RenderOptions { OutputFileName = output, Width = size, Height = size });

            error = captured.ToString().Contains("Error") ? captured.ToString() : null;

            return error is null ? new ImageFile(output).Load()[0] : null;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// A closed unit square, centered on the origin.
    /// </summary>
    private const string Square =
        "move to -0.5, -0.5  line to 0.5, -0.5  line to 0.5, 0.5  line to -0.5, 0.5  close";

    /// <summary>
    /// A closed rectangle, two wide and half a unit tall, centered on the origin.
    /// </summary>
    private const string WideRectangle =
        "move to -1, -0.25  line to 1, -0.25  line to 1, 0.25  line to -1, 0.25  close";

    /// <summary>
    /// The bounding box (in pixels) of the lit part of the image.
    /// </summary>
    private static (int MinX, int MaxX, int MinY, int MaxY, int Count) LitBounds(Canvas image)
    {
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1, count = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Color pixel = image.GetPixel(x, y);

                if ((pixel.Red + pixel.Green + pixel.Blue) / 3 > 0.5)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                    count++;
                }
            }
        }

        return (minX, maxX, minY, maxY, count);
    }

    /// <summary>
    /// A translate moves the whole outline, so a square shifted a unit in X comes out sitting to
    /// one side of where it started rather than centered.
    /// </summary>
    [TestMethod]
    public void TestTranslateMovesTheOutline()
    {
        (int minX, int maxX, _, _, _) = LitBounds(Render(Square, out string plainError));
        (int shiftedMinX, int shiftedMaxX, _, _, _) =
            LitBounds(Render(Square + "  translate [1, 0]", out string shiftedError));

        Assert.IsNull(plainError);
        Assert.IsNull(shiftedError);

        double plainCenter = (minX + maxX) / 2.0;
        double shiftedCenter = (shiftedMinX + shiftedMaxX) / 2.0;

        Assert.IsTrue(
            shiftedCenter - plainCenter > 20,
            $"a translate should move the square to one side (plain={plainCenter}, shifted={shiftedCenter})");
    }

    /// <summary>
    /// A scale resizes the whole outline, so a square scaled twice as wide as tall comes out a
    /// wide rectangle.
    /// </summary>
    [TestMethod]
    public void TestScaleResizesTheOutline()
    {
        (int minX, int maxX, int minY, int maxY, _) =
            LitBounds(Render(Square + "  scale [2, 1]", out string error));

        Assert.IsNull(error);

        int width = maxX - minX;
        int height = maxY - minY;

        Assert.IsTrue(
            width > height * 1.7,
            $"a 2x-wide scale should make the square markedly wider than tall (w={width}, h={height})");
    }

    /// <summary>
    /// A rotate turns the whole outline in its own plane, so a wide rectangle turned a quarter
    /// turn comes out taller than it is wide -- the reverse of how it started.
    /// </summary>
    [TestMethod]
    public void TestRotateTurnsTheOutline()
    {
        (int flatMinX, int flatMaxX, int flatMinY, int flatMaxY, _) =
            LitBounds(Render(WideRectangle, out string flatError));
        (int turnedMinX, int turnedMaxX, int turnedMinY, int turnedMaxY, _) =
            LitBounds(Render(WideRectangle + "  rotate 90", out string turnedError));

        Assert.IsNull(flatError);
        Assert.IsNull(turnedError);

        Assert.IsTrue(
            flatMaxX - flatMinX > flatMaxY - flatMinY,
            "the rectangle should start out wider than tall");
        Assert.IsTrue(
            turnedMaxY - turnedMinY > turnedMaxX - turnedMinX,
            "a quarter turn should leave it taller than wide");
    }

    /// <summary>
    /// Transforms compose in the order written -- the first acts on the raw outline, the next on
    /// its result.  Scaling a square up and then translating moves the enlarged square by the
    /// full step, while translating and then scaling multiplies the step by the scale, landing
    /// the square farther out.  The two orders must therefore put it in visibly different places.
    /// </summary>
    [TestMethod]
    public void TestTransformsComposeInOrder()
    {
        (int aMinX, int aMaxX, _, _, _) =
            LitBounds(Render(Square + "  scale 2  translate [1, 0]", out string aError));
        (int bMinX, int bMaxX, _, _, _) =
            LitBounds(Render(Square + "  translate [1, 0]  scale 2", out string bError));

        Assert.IsNull(aError);
        Assert.IsNull(bError);

        double scaleThenMove = (aMinX + aMaxX) / 2.0;
        double moveThenScale = (bMinX + bMaxX) / 2.0;

        Assert.IsTrue(
            moveThenScale - scaleThenMove > 20,
            $"the two orders should differ (scale-then-move={scaleThenMove}, move-then-scale={moveThenScale})");
    }
}
