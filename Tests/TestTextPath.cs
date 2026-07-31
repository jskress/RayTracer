using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces.Extrusions;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover text as a 2D path source -- a "text" block inside a "path" that folds a
/// run of laid-out glyph outlines into the path, so text can be extruded, lathed or swept like
/// any other outline.  The glyph layout itself is the font subsystem's; these confirm the path
/// picks it up, that the block is offered only its own content (no surface grammar), and that
/// an extruded text path actually renders.  They lean on the "Merriweather" font the docs
/// examples already render with.
/// </summary>
[TestClass]
public class TestTextPath
{
    private const string Font = "Merriweather";

    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"text-path-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Resolving a text path yields a general path made of the text's glyph outlines: a
    /// non-empty path whose runs are the glyph contours.
    /// </summary>
    [TestMethod]
    public void TestTextResolvesToGlyphOutlines()
    {
        GeneralPath path = ResolveTextPath("Hi");

        Assert.IsTrue(path.Segments.Count > 0, "the text path should carry the glyphs' outlines");
    }

    /// <summary>
    /// The whole run is laid out into the one path, not just its first glyph, so a wider string
    /// makes a wider path.  A row of four Ms must come out markedly wider than a single I.
    /// </summary>
    [TestMethod]
    public void TestWholeRunIsLaidOutIntoThePath()
    {
        double wide = PathWidth(ResolveTextPath("MMMM"));
        double narrow = PathWidth(ResolveTextPath("I"));

        Assert.IsTrue(narrow > 0, "even a single glyph should have some width");
        Assert.IsTrue(wide > narrow * 2, $"a longer run should be far wider (MMMM={wide}, I={narrow})");
    }

    /// <summary>
    /// Text glyphs come at the font's own scale, where a line is about a unit tall -- small
    /// enough to use directly, unlike an icon.  A single capital should stand well under two
    /// units.
    /// </summary>
    [TestMethod]
    public void TestGlyphsComeAtFontScale()
    {
        double height = PathHeight(ResolveTextPath("H"));

        Assert.IsTrue(height is > 0.3 and < 2, $"a capital should be about a unit tall (was {height})");
    }

    /// <summary>
    /// A text block used as a path takes only its own content, so a surface clause inside it --
    /// here a transform -- is turned away when parsing, with the message the grammar gives.
    /// </summary>
    [TestMethod]
    public void TestSurfaceGrammarIsRejectedInAPathTextBlock()
    {
        string error = ParseError(
            "extrusion {\n" +
            "    path { text { text 'X'  font '" + Font + "'  rotate X 90 } }\n" +
            "    min Y 0  max Y 0.2\n" +
            "}");

        Assert.IsNotNull(error, "a transform inside a path's text block should be rejected");
        StringAssert.Contains(error, "text path property");
    }

    /// <summary>
    /// A text path still needs its string and its font, so a block missing the font is turned
    /// away, just as the text surface's is.
    /// </summary>
    [TestMethod]
    public void TestFontIsRequiredInAPathTextBlock()
    {
        string error = ParseError(
            "extrusion {\n" +
            "    path { text { text 'X' } }\n" +
            "    min Y 0  max Y 0.2\n" +
            "}");

        Assert.IsNotNull(error, "a text path with no font should be rejected");
        StringAssert.Contains(error, "font");
    }

    /// <summary>
    /// An extruded text path is capped like any other extrusion -- the letters have solid
    /// faces, not just side walls.  A text path folds many glyph outlines into one path, and an
    /// extrusion sizes its flat caps from the path's bounding box, so folding the glyphs in has
    /// to grow that box; if it does not, the caps come out degenerate and the letters extrude
    /// hollow.  Firing a ray straight up through a point inside a glyph -- parallel to the
    /// walls, which it therefore cannot touch -- must still be stopped, once by each cap.
    /// </summary>
    [TestMethod]
    public void TestExtrudedTextIsCapped()
    {
        GeneralPath path = ResolveTextPath("H");
        TwoDPoint inside = FindInteriorPoint(path);

        Extrusion extrusion = new () { Path = path, MinimumY = 0, MaximumY = 1, Closed = true };

        extrusion.PrepareForRendering();

        // A path's 2D X and Y become world X and Z, with the extrusion's thickness along Y, so
        // a ray up the Y axis through an interior point can only meet the two caps.
        Ray ray = new (new Point(inside.X, -1, inside.Y), new Vector(0, 1, 0));
        List<Intersection> hits = [];

        extrusion.AddIntersections(ray, hits);

        Assert.AreEqual(2, hits.Count, "an interior ray should be stopped once by each cap");

        List<double> ys = hits
            .Select(hit => ray.At(hit.Distance).Y)
            .OrderBy(y => y)
            .ToList();

        Assert.IsTrue(ys[0].Near(0), $"the bottom cap should sit at Y = 0 (was {ys[0]})");
        Assert.IsTrue(ys[1].Near(1), $"the top cap should sit at Y = 1 (was {ys[1]})");
    }

    /// <summary>
    /// Scans for a point that falls inside the given outline.
    /// </summary>
    private static TwoDPoint FindInteriorPoint(GeneralPath path)
    {
        for (double y = -2; y <= 2; y += 0.01)
        {
            for (double x = -2; x <= 2; x += 0.01)
            {
                TwoDPoint point = new (x, y);

                if (path.Contains(point))
                    return point;
            }
        }

        Assert.Fail("no point was found inside the glyph outline");

        return default;
    }

    /// <summary>
    /// The whole way through: an extruded text path renders as visible geometry.
    /// </summary>
    [TestMethod]
    public void TestExtrudedTextRenders()
    {
        Canvas image = Render(
            "extrusion {\n" +
            "    material { pigment color White  ambient 1  diffuse 0  specular 0 }\n" +
            "    path { text { text 'Ray'  font '" + Font + "'  layout { horizontal position center } } }\n" +
            "    min Y 0  max Y 0.25\n" +
            "    rotate X -90\n" +
            "}",
            out string error);

        Assert.IsNull(error);
        Assert.IsTrue(LitPixels(image) > 100, "the extruded word should be visible");
    }

    /// <summary>
    /// Resolves a text path for the given string in our test font.
    /// </summary>
    private static GeneralPath ResolveTextPath(string text)
    {
        TextPathResolver resolver = new ()
        {
            TextResolver = new LiteralResolver<string> { Value = text },
            FontFamilyNameResolver = new LiteralResolver<string> { Value = Font }
        };

        return resolver.Resolve(new RenderContext(), new Variables());
    }

    /// <summary>
    /// The width of a path's bounding box, from its sampled points.
    /// </summary>
    private static double PathWidth(GeneralPath path)
    {
        List<TwoDPoint> points = path.Sample(4);

        return points.Max(point => point.X) - points.Min(point => point.X);
    }

    /// <summary>
    /// The height of a path's bounding box, from its sampled points.
    /// </summary>
    private static double PathHeight(GeneralPath path)
    {
        List<TwoDPoint> points = path.Sample(4);

        return points.Max(point => point.Y) - points.Min(point => point.Y);
    }

    /// <summary>
    /// A camera looking straight at the origin, with a lamp beside it.
    /// </summary>
    private const string Setup =
        "camera { location [0, 0.4, -6] look at [0, 0.4, 0] }\n" +
        "point light { location [0, 0.4, -6] color White }";

    /// <summary>
    /// Parses the given scene body and returns the captured error text, or <c>null</c> if it
    /// parsed cleanly.
    /// </summary>
    private string ParseError(string body)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");

        File.WriteAllText(path, Setup + "\n" + body);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            return renderer is null ? captured.ToString() : null;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Renders the given scene body and returns the image, or reports the error that stopped
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
    /// The count of lit (non-black) pixels in the image.
    /// </summary>
    private static int LitPixels(Canvas image)
    {
        int count = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Color pixel = image.GetPixel(x, y);

                if ((pixel.Red + pixel.Green + pixel.Blue) / 3 > 0.5)
                    count++;
            }
        }

        return count;
    }
}
