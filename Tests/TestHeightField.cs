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

    private static Term Multiply(Term left, Term right)
    {
        return new BinaryMultiplyOperation(left, right);
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
    /// Asked to be smooth, the terrain carries a normal at every grid point taken from the slope of
    /// the ground either side of it -- so the triangles meeting there agree about which way the
    /// surface faces, which is the whole of the difference between smooth and faceted.
    /// <para>
    /// The function is <c>x squared</c>, whose slope is <c>2x</c>, and a central difference of a
    /// quadratic is exact -- so this can be held to the real answer rather than to a tolerance
    /// chosen to make it pass.  The points along the X edges have a neighbour on one side only and
    /// take a one-sided difference, which is not exact, so they are left out.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestSmoothNormalsFollowTheSlopeOfTheGround()
    {
        HeightField field = new ()
        {
            Function = Expression(Multiply(Named("x"), Named("x"))),
            Samples = 21, Closed = false, Smooth = true
        };

        field.PrepareForRendering();

        List<SmoothTriangle> triangles = field.Surfaces.OfType<SmoothTriangle>().ToList();

        Assert.IsTrue(triangles.Count > 0, "a smooth field should be made of smooth triangles");

        int checked_ = 0;

        foreach (SmoothTriangle triangle in triangles)
        {
            foreach ((Point point, Vector normal) in new[]
                     {
                         (triangle.Point1, triangle.Normal1),
                         (triangle.Point2, triangle.Normal2),
                         (triangle.Point3, triangle.Normal3)
                     })
            {
                // The edges in X take a one-sided difference, which a quadratic defeats.
                if (point.X < 1e-9 || point.X > 1 - 1e-9)
                    continue;

                Vector expected = new Vector(-2 * point.X, 1, 0).Unit;

                Assert.AreEqual(expected.X, normal.X, 1e-9, $"normal X at x = {point.X}");
                Assert.AreEqual(expected.Y, normal.Y, 1e-9, $"normal Y at x = {point.X}");
                Assert.AreEqual(expected.Z, normal.Z, 1e-9, $"normal Z at x = {point.X}");

                checked_++;
            }
        }

        Assert.IsTrue(checked_ > 100, $"only {checked_} normals were actually checked");
    }

    /// <summary>
    /// Smoothing is off unless it is asked for, and that is not a detail: turning it on changes the
    /// shading of every height field ever written, so the default has to leave them alone.
    /// </summary>
    [TestMethod]
    public void TestTerrainIsFacetedUnlessSmoothIsAskedFor()
    {
        HeightField field = new ()
        {
            Function = Expression(Multiply(Named("x"), Named("x"))), Samples = 8, Closed = false
        };

        field.PrepareForRendering();

        Assert.IsTrue(field.Surfaces.OfType<Triangle>().Any(), "there should be terrain");
        Assert.IsFalse(field.Surfaces.OfType<SmoothTriangle>().Any(),
            "nothing should be smoothed unless the scene asked for it");
    }

    /// <summary>
    /// **Smoothing is not a thing of the function form.**  The normals come from the sampled grid,
    /// and a picture fills that grid exactly as a function does -- an image height field facets in
    /// just the same way and is helped just as much.  What keeps this off by default is that it
    /// changes existing pictures, not that it does not apply.
    /// </summary>
    [TestMethod]
    public void TestAnImageIsSmoothedToo()
    {
        HeightField field = new ()
        {
            ImageReference = new ImageReference
            {
                ImageName = "terrain.png",
                SourceDirectory = Path.Combine(
                    TestDocumentation.RepositoryRoot, "docs", "examples", "advanced")
            },
            Closed = false, Smooth = true
        };

        field.PrepareForRendering();

        Assert.IsTrue(field.Surfaces.OfType<SmoothTriangle>().Any(),
            "a height field read from a picture should smooth like any other");
    }

    /// <summary>
    /// The walls below the terrain stay faceted, because they really are flat.  Smoothing them would
    /// round off the sides of the block the terrain sits on, which is not a shape anything means.
    /// </summary>
    [TestMethod]
    public void TestTheWallsAreNotSmoothed()
    {
        HeightField field = new ()
        {
            Function = Expression(Multiply(Named("x"), Named("x"))),
            Samples = 8, Closed = true, Smooth = true
        };

        field.PrepareForRendering();

        // The terrain goes straight into the field; the four walls are each their own group.
        foreach (Group wall in field.Surfaces.OfType<Group>())
        {
            Assert.IsFalse(wall.Surfaces.OfType<SmoothTriangle>().Any(),
                "a wall is flat, and should be left flat");
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
