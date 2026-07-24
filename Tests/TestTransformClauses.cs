using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how a scene writes each sort of transform, checked by rendering, since a
/// transform clause that parses may still put a surface in the wrong place -- or, as "matrix" and
/// "shear" once did, leave punctuation behind that the next transform in the list trips over.
/// What each matrix does to a point is settled at the math level in <see cref="TestTransforms"/>,
/// and what a motion does with one over an open shutter in <see cref="TestMotionBlur"/>; here a
/// motion is only asked which transforms it will take at all.
/// </summary>
[TestClass]
public class TestTransformClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"transform-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A camera that sees the ball straight on and holds still, which is all these tests need of
    /// one.  <see cref="OpenShutter"/> is the same camera with its shutter left open, for when a
    /// motion has to actually be looked at.
    /// </summary>
    private const string StillCamera = "camera { location [0, 0, -8] look at [0, 0, 0] }";

    /// <summary>
    /// The same camera with its shutter open, so that the instants either side of the shutter's
    /// middle are looked at -- which is the only thing that asks a motion's transforms for a
    /// part-way version of themselves.
    /// </summary>
    private const string OpenShutter =
        "camera { location [0, 0, -8] look at [0, 0, 0] shutter 1 blur samples 8 }";

    /// <summary>
    /// Renders a ball under the given transform clauses, flat white on black so that where it
    /// lands can be read straight off the image, and returns the render, or reports the error
    /// that stopped it.
    /// </summary>
    private Canvas Render(
        string transforms, out string error, string camera = StillCamera, int size = 80)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");
        string output = Path.ChangeExtension(path, "png");

        File.WriteAllText(path,
            camera + "\n" +
            "light { location [0, 0, -8] }\n" +
            "sphere {\n" +
            "    material { pigment color [1, 1, 1] ambient 1 diffuse 0 specular 0 }\n" +
            transforms + "\n" +
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

            try
            {
                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = size, Height = size
                });
            }
            catch (Exception exception)
            {
                // A scene may also be refused once it is being rendered rather than while it is
                // being read -- a matrix used as a motion is -- and out here that arrives as an
                // exception, since it is the command line that turns one into a printed complaint.
                error = exception.Message;

                return null;
            }

            error = captured.ToString().Contains("Error") ? captured.ToString() : null;

            return error is null ? new ImageFile(output).Load()[0] : null;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Returns the centre of the lit pixels in the given image, in pixels across and down.  This
    /// is where the ball ended up, which is what a transform clause is judged on.
    /// </summary>
    private static (double X, double Y) CentreOfLight(Canvas image)
    {
        double totalX = 0, totalY = 0, weight = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Color pixel = image.GetPixel(x, y);
                double brightness = (pixel.Red + pixel.Green + pixel.Blue) / 3;

                totalX += x * brightness;
                totalY += y * brightness;
                weight += brightness;
            }
        }

        Assert.IsTrue(weight > 0, "the ball should be somewhere in the image");

        return (totalX / weight, totalY / weight);
    }

    /// <summary>
    /// Returns the number of pixels at which the two images differ.
    /// </summary>
    private static int PixelsThatDiffer(Canvas first, Canvas second)
    {
        Assert.AreEqual(first.Width, second.Width);
        Assert.AreEqual(first.Height, second.Height);

        int count = 0;

        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                if (!first.GetPixel(x, y).Matches(second.GetPixel(x, y)))
                    count++;
            }
        }

        return count;
    }

    [TestMethod]
    public void TestAMatrixParses()
    {
        // The bug this guards: the grammar hands the clause the brackets and the fifteen commas
        // as tokens, and nothing used to take them off the list, so the loop read "[" as the name
        // of the next transform and gave up on the scene entirely.
        Canvas image = Render(
            "matrix [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]", out string error);

        Assert.IsNull(error, $"a matrix should parse: {error}");
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAMatrixPlacesASurfaceWhereTheSameTranslateWould()
    {
        // The values are read across the rows, so the fourth column carries the translation.
        Canvas viaMatrix = Render(
            "matrix [1, 0, 0, 2, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1]", out string matrixError);
        Canvas viaTranslate = Render("translate [2, 1, 0]", out string translateError);
        Canvas untouched = Render("translate [0, 0, 0]", out string plainError);

        Assert.IsNull(matrixError, $"a matrix should parse: {matrixError}");
        Assert.IsNull(translateError);
        Assert.IsNull(plainError);

        Assert.AreEqual(0, PixelsThatDiffer(viaMatrix, viaTranslate),
            "a matrix holding a translation should render the same as that translate");

        // ...and check the pair actually went somewhere, so that two identically broken renders
        // could not pass the comparison above.
        (double movedX, double movedY) = CentreOfLight(viaMatrix);
        (double restingX, double restingY) = CentreOfLight(untouched);

        Assert.IsTrue(Math.Abs(movedX - restingX) > 5,
            $"the ball should have moved across the image, but sat at {movedX:F1} vs {restingX:F1}");
        Assert.IsTrue(Math.Abs(movedY - restingY) > 2,
            $"the ball should have moved down the image, but sat at {movedY:F1} vs {restingY:F1}");
    }

    [TestMethod]
    public void TestAnIdentityMatrixMovesNothing()
    {
        Canvas viaMatrix = Render(
            "matrix [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]", out string matrixError);
        Canvas untouched = Render("translate [0, 0, 0]", out string plainError);

        Assert.IsNull(matrixError, $"a matrix should parse: {matrixError}");
        Assert.IsNull(plainError);
        Assert.AreEqual(0, PixelsThatDiffer(viaMatrix, untouched));
    }

    [TestMethod]
    public void TestAShearParses()
    {
        // Shear is written the same bracketed way as matrix, and was broken in the same way.
        Canvas sheared = Render("shear [1, 0, 0, 0, 0, 0] scale 2", out string shearError);
        Canvas plain = Render("scale 2", out string plainError);

        Assert.IsNull(shearError, $"a shear should parse: {shearError}");
        Assert.IsNull(plainError);
        Assert.AreNotEqual(0, PixelsThatDiffer(sheared, plain),
            "shearing a ball should lean it over, not leave it alone");
    }

    [TestMethod]
    public void TestATransformListMayCarryOnPastAMatrix()
    {
        // The real test of the punctuation being taken off the list exactly: whatever follows the
        // brackets has to be read as the next transform, and nothing else.
        Canvas viaMatrix = Render("""
            matrix [1, 0, 0, 2, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
            translate [0, 1, 0]
            scale 0.5
            """, out string matrixError);
        Canvas viaTranslates = Render("""
            translate [2, 0, 0]
            translate [0, 1, 0]
            scale 0.5
            """, out string translateError);

        Assert.IsNull(matrixError, $"a matrix in a longer list should parse: {matrixError}");
        Assert.IsNull(translateError);
        Assert.AreEqual(0, PixelsThatDiffer(viaMatrix, viaTranslates),
            "the transforms after a matrix should apply just as they would after a translate");
    }

    [TestMethod]
    public void TestATransformListMayCarryOnPastAShear()
    {
        Canvas withShear = Render("shear [0, 0, 0, 0, 0, 0] translate [2, 0, 0]",
            out string shearError);
        Canvas withoutShear = Render("translate [2, 0, 0]", out string plainError);

        Assert.IsNull(shearError, $"a shear in a longer list should parse: {shearError}");
        Assert.IsNull(plainError);
        Assert.AreEqual(0, PixelsThatDiffer(withShear, withoutShear),
            "a shear of nothing should leave the translate that follows it untouched");
    }

    [TestMethod]
    public void TestAMatrixOfTheWrongSizeIsRejected()
    {
        Render("matrix [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0]", out string error);

        Assert.IsNotNull(error, "a matrix of twelve values should be an error");
    }

    [TestMethod]
    public void TestAMatrixMotionSmearsWhereTheSameTranslateMotionWould()
    {
        // A matrix given part way is measured from the identity matrix rather than from zero, so
        // one holding a move slides exactly as the move itself does, instant for instant, and the
        // two smears come out the same.
        Canvas viaMatrix = Render("motion { matrix [1, 0, 0, 2, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] }",
            out string matrixError, OpenShutter);
        Canvas viaTranslate = Render("motion { translate [2, 0, 0] }", out string translateError,
            OpenShutter);
        Canvas still = Render("", out string stillError, OpenShutter);

        Assert.IsNull(matrixError, $"a matrix should serve as a motion: {matrixError}");
        Assert.IsNull(translateError);
        Assert.IsNull(stillError);

        Assert.AreEqual(0, PixelsThatDiffer(viaMatrix, viaTranslate),
            "a matrix holding a move should smear just as that move does");

        // ...and that the pair actually smeared, so two identically still renders could not pass
        // the comparison above.
        Assert.AreNotEqual(0, PixelsThatDiffer(viaMatrix, still),
            "a matrix motion should have moved the ball over the shutter's opening");
    }

    [TestMethod]
    public void TestAMatrixMotionMeasuresAScaleFromOne()
    {
        // The diagonal is the half of it that measuring from zero got wrong: a matrix holding a
        // scale has to grow from one, exactly as "scale" does, rather than from nothing.
        Canvas viaMatrix = Render("motion { matrix [2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 1] }",
            out string matrixError, OpenShutter);
        Canvas viaScale = Render("motion { scale 2 }", out string scaleError, OpenShutter);

        Assert.IsNull(matrixError, $"a matrix holding a scale should serve as a motion: {matrixError}");
        Assert.IsNull(scaleError);
        Assert.AreEqual(0, PixelsThatDiffer(viaMatrix, viaScale),
            "a matrix holding a scale should swell just as that scale does");
    }

    [TestMethod]
    public void TestAMatrixMotionStartsWhereTheSurfaceStands()
    {
        // The instant at the start of the opening has to leave the surface exactly where it was
        // placed.  Measured from zero this came out as sixteen zeros -- a matrix that cannot be
        // inverted, and a moving surface's matrices are inverted to carry rays into its space, so
        // the render would have failed outright rather than merely looked wrong.
        Canvas moved = Render(
            "translate [1, 0, 0]\nmotion { matrix [1, 0, 0, 2, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] }",
            out string error, OpenShutter);

        Assert.IsNull(error, $"a matrix motion should render: {error}");
        Assert.IsNotNull(moved);

        // The ball is somewhere sensible rather than collapsed to a point or lost entirely.
        (double x, double y) = CentreOfLight(moved);

        Assert.IsTrue(x is > 0 and < 80 && y is > 0 and < 80,
            $"the ball should still be in the picture, but its middle was at ({x:F1}, {y:F1})");
    }

    [TestMethod]
    public void TestTheOtherTransformsMayBeUsedAsAMotion()
    {
        Canvas sheared = Render("motion { shear [1, 0, 0, 0, 0, 0] }", out string shearError,
            OpenShutter);
        Canvas moved = Render("motion { translate [1, 0, 0] }", out string moveError, OpenShutter);

        Assert.IsNull(shearError, $"a shear should be allowed as a motion: {shearError}");
        Assert.IsNull(moveError, $"a translate should be allowed as a motion: {moveError}");
        Assert.IsNotNull(sheared);
        Assert.IsNotNull(moved);
    }

    [TestMethod]
    public void TestAMotionDoesNothingWhileTheShutterIsShut()
    {
        // With the shutter shut there is a single instant to see, so nothing ever asks the motion
        // for a part-way transform and the surface stands exactly where it was placed.
        Canvas moving = Render("motion { matrix [1, 0, 0, 2, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] }",
            out string movingError);
        Canvas still = Render("", out string stillError);

        Assert.IsNull(movingError, $"a shut shutter should never reach the motion: {movingError}");
        Assert.IsNull(stillError);
        Assert.AreEqual(0, PixelsThatDiffer(moving, still),
            "with the shutter shut, a motion should make no difference at all");
    }
}
