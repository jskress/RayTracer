using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover keeping a surface under a name and then using it again with `object`, for the
/// surfaces whose blocks start with two words rather than one.
/// <para>
/// Those blocks tell the parser to skip a token, since "smooth triangle" puts the name or the open
/// brace one place further along than "sphere" does.  But an `object` reference is written
/// `object`, the name, then the brace -- the two words are not there to be skipped -- and the same
/// skip applied there reads past the name, finds the brace, and takes the reference for a fresh
/// definition.  The scene is then told it has left out properties it never meant to write.
/// </para>
/// <para>
/// No gallery scene happens to name a multi-word surface and then use it, which is why nothing
/// caught this.
/// </para>
/// </summary>
[TestClass]
public class TestNamedSurfaceReuse
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"reuse-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void TestANamedSmoothTriangleCanBeUsedAgain()
    {
        Assert.IsNull(ErrorFrom("""
                                Tri = smooth triangle {
                                    points [-1, 0, 0], [1, 0, 0], [0, 2, 0]
                                    normals [0, 0, -1], [0, 0, -1], [0, 0, -1]
                                }
                                object Tri { translate [0.4, 0, 0] }
                                """));
    }

    [TestMethod]
    public void TestANamedGenericShapeCanBeUsedAgain()
    {
        Assert.IsNull(ErrorFrom("""
                                Shield = generic shape {
                                    path {
                                        move to -1, -1
                                        line to 1, -1
                                        line to 1, 1
                                        line to -1, 1
                                        close
                                    }
                                }
                                object Shield { translate [0.4, 0, 0] }
                                """));
    }


    /// <summary>
    /// Two uses of one named surface must stand where each was put, and not carry the other's
    /// transform as well.
    /// <para>
    /// **A shallow copy is what broke this.**  Parsing a run of transform clauses hands the resolver
    /// the transform it already has, so the run accumulates rather than replacing what came before
    /// it -- deliberately.  But a surface resolver was copied with <c>MemberwiseClone</c>, so every
    /// use of one name pointed at the same transform, and each use appended to it.  Two ears written
    /// `object ear { translate A }` and `object ear { translate B }` both ended up at A + B, one
    /// inside the other.
    /// </para>
    /// <para>
    /// **And a pair is the case that hides it.**  Two things placed either side of something sum to
    /// roughly nothing, so the pair did not merely land in one place -- it landed back at the middle,
    /// often out of shot entirely, which reads as the surface having failed to render rather than
    /// as its having moved.  Using the name once was always fine, so the second use took the blame.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTwoUsesOfOneNameStandWhereEachWasPut()
    {
        Canvas image = Rendered("""
                                Ball = sphere { scale 0.35  material { pigment White  ambient 1  diffuse 0  specular 0 } }
                                object Ball { translate [-1.4, 0, 0] }
                                object Ball { translate [ 1.4, 0, 0] }
                                """);

        Assert.IsNotNull(image);
        Assert.IsTrue(Covered(image, 0, image.Width / 3) > 0,
            "the left-hand copy of a twice-used surface was not where it was put");
        Assert.IsTrue(Covered(image, image.Width * 2 / 3, image.Width) > 0,
            "the right-hand copy of a twice-used surface was not where it was put");
        Assert.AreEqual(0, Covered(image, image.Width / 3, image.Width * 2 / 3),
            "both copies landed at the sum of the two transforms, in the middle");
    }

    /// <summary>
    /// The name itself must come out of it unchanged, so a later use is not carrying what the
    /// earlier ones asked for.
    /// <para>
    /// **The two placements here are deliberately lopsided.**  A pair either side of the middle sums
    /// to nothing, so a third use written after them lands in the right place by accident and the
    /// test passes on the bug -- which is exactly what the first draft of this did.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestUsingANameDoesNotChangeWhatItHolds()
    {
        Canvas after = Rendered("""
                                Ball = sphere { scale 0.35  material { pigment White  ambient 1  diffuse 0  specular 0 } }
                                object Ball { translate [-1.4, -1.0, 0] }
                                object Ball { translate [-0.7, -1.0, 0] }
                                object Ball { translate [ 1.4,  1.0, 0] }
                                """);
        Canvas alone = Rendered("""
                                Ball = sphere { scale 0.35  material { pigment White  ambient 1  diffuse 0  specular 0 } }
                                object Ball { translate [ 1.4,  1.0, 0] }
                                """);

        Assert.IsNotNull(after);
        Assert.IsNotNull(alone);

        int lone = Covered(alone, alone.Width * 2 / 3, alone.Width);

        Assert.IsTrue(lone > 0, "the lone copy was not where it was put");
        Assert.AreEqual(lone, Covered(after, after.Width * 2 / 3, after.Width),
            "using the name twice first changed where the third use of it landed");
    }

    /// <summary>
    /// Counts the pixels covered by anything, over a band of columns.
    /// </summary>
    private static int Covered(Canvas image, int fromX, int toX)
    {
        int count = 0;

        for (int y = 0; y < image.Height; y++)
        for (int x = fromX; x < toX; x++)
        {
            if (image.GetPixel(x, y).Red > 0.5)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Counts the pixels covered by anything, over a band of rows.
    /// </summary>
    private static int CoveredRows(Canvas image, int fromY, int toY)
    {
        int count = 0;

        for (int y = fromY; y < toY; y++)
        for (int x = 0; x < image.Width; x++)
        {
            if (image.GetPixel(x, y).Red > 0.5)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Renders a small scene and hands back the picture.
    /// </summary>
    private Canvas Rendered(string body)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");
        string output = Path.ChangeExtension(path, ".png");

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 0, -7]  look at [0, 0, 0]  field of view 40 }\n" +
            "point light { location [-4, 6, -6] }\n" + body + "\n");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions
            {
                OutputFileName = output, Width = 120, Height = 120
            });

            return captured.ToString().Contains("Error") ? null : new ImageFile(output).Load()[0];
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Renders a small scene and hands back the error that stopped it, or <c>null</c>.
    /// </summary>
    private string ErrorFrom(string body)
    {
        string path = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(path,
            "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -6] }\n" + body + "\n");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions
            {
                OutputFileName = Path.ChangeExtension(path, ".png"), Width = 4, Height = 4
            });
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        string said = captured.ToString();

        return said.Contains("Error") ? said : null;
    }
}
