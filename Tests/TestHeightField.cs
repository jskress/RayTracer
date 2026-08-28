using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Parser;
using RayTracer.Renderer;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover the height field, which takes its heights either from a picture or from a
/// function.
/// <para>
/// The two forms differ in one way that matters more than it looks: **a picture's heights are
/// multiplied by a quarter and a function's are not.**  That factor is old, undocumented in the
/// code until recently, and every scene built on the image form is composed around it -- so it is
/// pinned here from both sides.  Getting it wrong in either direction is silent: the terrain simply
/// stands at the wrong height, and looks like a terrain either way.
/// </para>
/// </summary>
[TestClass]
public class TestHeightField
{
    private Variables _variables;

    [TestInitialize]
    public void CreateVariables()
    {
        _variables = new Variables();
    }

    private FieldExpression Expression(Term term)
    {
        return term.ToField(_variables);
    }

    private static Term Number(double value)
    {
        return LiteralTerm.CreateLiteralTerm(new NumberToken(value.ToString("R"), value));
    }

    private static Term Named(string name)
    {
        return new VariableTerm(new IdToken(name));
    }

    /// <summary>
    /// Gathers every corner of every triangle the field built.
    /// </summary>
    private static List<Point> CornersOf(HeightField field)
    {
        field.PrepareForRendering();

        return field.Surfaces
            .OfType<Triangle>()
            .SelectMany(triangle => new[] { triangle.Point1, triangle.Point2, triangle.Point3 })
            .ToList();
    }

    /// <summary>
    /// A function's answer is the height outright.  Were the image form's quarter applied here, a
    /// constant half would stand at an eighth, and a terrain written to reach a given height would
    /// quietly come out four times too flat.
    /// </summary>
    [TestMethod]
    public void TestAFunctionStandsAtTheHeightItGives()
    {
        HeightField field = new ()
        {
            Function = Expression(Number(0.5)), Samples = 8, Closed = false
        };
        List<Point> corners = CornersOf(field);

        Assert.IsTrue(corners.Count > 0, "a flat function should still make a surface");

        foreach (Point corner in corners)
        {
            Assert.AreEqual(0.5, corner.Y, 1e-12,
                "a function's answer is the height itself, with no quarter applied");
        }
    }

    /// <summary>
    /// And the other side of it: a picture's heights *are* quartered, which is what every scene
    /// built on the image form expects.  The expectation is read from the same picture through the
    /// same gray scale, so this pins the factor rather than the map.
    /// </summary>
    [TestMethod]
    public void TestAnImageStandsAQuarterAsTallAsItIsBright()
    {
        ImageReference reference = new ()
        {
            ImageName = "terrain.png",
            SourceDirectory = Path.Combine(
                TestDocumentation.RepositoryRoot, "docs", "examples", "advanced")
        };
        Canvas canvas = reference.Canvas.ToGrayScale();
        double brightest = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
                brightest = Math.Max(brightest, canvas.GetPixel(x, y).Red);
        }

        HeightField field = new () { ImageReference = reference, Closed = false };
        double tallest = CornersOf(field).Max(corner => corner.Y);

        Assert.AreEqual(brightest * 0.25, tallest, 1e-12,
            "a height field made from a picture stands a quarter as tall as the picture is bright");
    }

    /// <summary>
    /// The function is evaluated over the same unit square the picture covers, so a function of
    /// <c>x</c> alone must give back exactly the X it was asked at.  That settles the domain and the
    /// axis convention together, and neither is guessable from the outside.
    /// </summary>
    [TestMethod]
    public void TestTheFunctionSeesTheUnitSquare()
    {
        HeightField field = new ()
        {
            Function = Expression(Named("x")), Samples = 9, Closed = false
        };
        List<Point> corners = CornersOf(field);

        foreach (Point corner in corners)
        {
            Assert.AreEqual(corner.X, corner.Y, 1e-12,
                "the height of a field whose function is 'x' must be its own X");
        }

        Assert.AreEqual(0, corners.Min(corner => corner.X), 1e-12, "the square starts at nought");
        Assert.AreEqual(1, corners.Max(corner => corner.X), 1e-12, "and runs to one");
    }

    /// <summary>
    /// The grid is as fine as it was asked to be: a run of N points makes N-1 squares in each
    /// direction, and two triangles for each square.
    /// </summary>
    [TestMethod]
    public void TestSamplesSetsHowFineTheGridIs()
    {
        foreach (int samples in new[] { 3, 8, 17 })
        {
            HeightField field = new ()
            {
                Function = Expression(Named("x")), Samples = samples, Closed = false
            };

            field.PrepareForRendering();

            Assert.AreEqual(2 * (samples - 1) * (samples - 1), field.Surfaces.OfType<Triangle>().Count(),
                $"a {samples}-point grid should make two triangles for each of its squares");
        }
    }

    /// <summary>
    /// A height field takes its heights from one source or the other, and the settings belonging to
    /// each are refused with the wrong one rather than quietly ignored.  A scene that says something
    /// with no effect is better told so.
    /// </summary>
    [TestMethod]
    public void TestTheTwoSourcesAndTheirSettingsAreKeptApart()
    {
        (string Body, string Expected)[] cases =
        [
            // "open" parses on its own, so this reaches the check rather than stopping at the grammar.
            ("open", "One of the \"image\" or \"function\" properties is required."),
            ("image 'terrain.png' function { x }", "but not from both"),
            ("image 'terrain.png' samples 32", "\"samples\" property belongs to a height field made from a function"),
            ("function { x } clip 0", "\"clip\" property belongs to a height field made from an image")
        ];

        foreach ((string body, string expected) in cases)
        {
            string directory = Path.Combine(Path.GetTempPath(), $"height-field-{Guid.NewGuid():N}");

            Directory.CreateDirectory(directory);

            try
            {
                string path = Path.Combine(directory, "scene.igl");

                File.WriteAllText(path,
                    "camera { location [0, 1, -3]  look at [0, 0, 0] }\n" +
                    "point light { location [0, 5, -5] }\n" +
                    $"heightfield {{ {body} }}\n");

                // A scene that will not parse comes back as a null renderer, with what was wrong
                // written to the console rather than thrown.
                StringWriter captured = new ();
                TextWriter was = Console.Out;
                ImageRenderer renderer;

                Console.SetOut(captured);

                try
                {
                    renderer = new LanguageParser(path).Parse();
                }
                finally
                {
                    Console.SetOut(was);
                }

                Assert.IsNull(renderer, $"\"{body}\" should not have been accepted");
                StringAssert.Contains(captured.ToString(), expected,
                    $"the message for \"{body}\" did not say what was wrong");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
