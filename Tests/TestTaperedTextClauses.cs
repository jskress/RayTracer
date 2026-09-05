using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover text whose glyphs are scaled as they are given depth.
/// <para>
/// The fault worth testing for is not that the taper fails to arrive but that it arrives and drags
/// the letters with it.  A taper draws an outline towards the Y axis, and a line of text is laid
/// out along X, so every letter but the one at the origin sits well off that axis; tapering them
/// where they stand pulls them all towards one point and the text comes out as a starburst.  Each
/// glyph therefore has to be brought to the axis, tapered about its own middle, and put back.
/// </para>
/// <para>
/// So what is measured here is where the letters *are*, from straight on, where a taper about each
/// glyph's own middle should barely change the shape at all -- and separately, from an angle,
/// that the taper is nonetheless doing something.
/// </para>
/// </summary>
[TestClass]
public class TestTaperedTextClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"tapered-text-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void TestASceneMayWriteTaperedText()
    {
        Assert.IsNull(ErrorFrom("tapered text { taper 0.4  text 'AV'  font 'Merriweather' }"));
    }

    /// <summary>
    /// Seen from straight on, the front face is the full size of the letters whatever the taper, so
    /// the run of text must still start and end in the same columns.  Letters dragged towards the
    /// axis would fail this outright: the far ones would move, and the run would end early.
    /// </summary>
    [TestMethod]
    public void TestTheLettersStayWhereTheLayoutPutThem()
    {
        (int _, int leftPlain, int rightPlain) = Seen(
            "text { text 'HHHHHH'  font 'Merriweather'  rotate X -90 }", 0);
        (int _, int leftTapered, int rightTapered) = Seen(
            "tapered text { taper 0.3  text 'HHHHHH'  font 'Merriweather'  rotate X -90 }", 0);

        Assert.IsTrue(rightPlain > leftPlain, "The plain text covered nothing at all.");
        Assert.AreEqual(leftPlain, leftTapered,
            $"The run now starts at column {leftTapered} rather than {leftPlain}.");
        Assert.AreEqual(rightPlain, rightTapered,
            $"The run now ends at column {rightTapered} rather than {rightPlain}.");
    }

    /// <summary>
    /// The taper still has to reach the glyphs, and from an angle it shows.  It covers *more* of
    /// the picture, not less, which is worth saying because the opposite is the obvious guess: the
    /// letters are smaller at the back, so surely they cover less.  What is actually in view at an
    /// angle is the wall down the side of each letter, and a tapered one is a broad sloped face --
    /// here it falls from full size to under a third across a tenth of a unit of depth -- where an
    /// untapered one shows nothing but that tenth of a unit edge-on.
    /// </summary>
    [TestMethod]
    public void TestTheTaperIsCarried()
    {
        (int plain, int _, int _) = Seen(
            "text { text 'HHHHHH'  font 'Merriweather'  rotate X -90 }", 34);
        (int tapered, int _, int _) = Seen(
            "tapered text { taper 0.3  text 'HHHHHH'  font 'Merriweather'  rotate X -90 }", 34);

        Assert.IsTrue(plain > 0, "The plain text covered nothing at all.");
        Assert.IsTrue(tapered > plain * 1.1,
            $"Tapered text covered {tapered} pixels against plain text's {plain}, which is not " +
            "enough of a difference to say the taper arrived.");
    }

    /// <summary>
    /// A taper of 1 is not a taper, and must leave the text exactly as it was.
    /// </summary>
    [TestMethod]
    public void TestNoTaperIsUnchanged()
    {
        (int plain, int leftPlain, int rightPlain) = Seen(
            "text { text 'HHHHHH'  font 'Merriweather'  rotate X -90 }", 34);
        (int one, int leftOne, int rightOne) = Seen(
            "tapered text { taper 1  text 'HHHHHH'  font 'Merriweather'  rotate X -90 }", 34);

        Assert.AreEqual(plain, one, "A taper of 1 changed how much of the picture is covered.");
        Assert.AreEqual(leftPlain, leftOne);
        Assert.AreEqual(rightPlain, rightOne);
    }

    /// <summary>
    /// Renders one text solid, turned by the given angle about Y, and reports how many pixels it
    /// covers along with the leftmost and rightmost columns it reaches.
    /// </summary>
    private (int Count, int Left, int Right) Seen(string body, double turn)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");
        int closing = body.LastIndexOf('}');
        string lit = body[..closing] +
                     "  material { pigment [1, 0.6, 0.3]  ambient 1  diffuse 0  specular 0 }" +
                     $"  rotate Y {turn} }}";

        File.WriteAllText(path,
            "context { angles are degrees  no gamma }\n" +
            "camera { location [0, 0, -14]  look at [0, 0, 0]  field of view 34 }\n" +
            "point light { location [-4, 6, -6] }\n" + lit);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        renderer.Render(new RenderOptions
        {
            OutputFileName = output, Width = 120, Height = 60
        });

        Canvas canvas = new ImageFile(output).Load()[0];
        int count = 0;
        int left = int.MaxValue;
        int right = int.MinValue;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                if (canvas.GetPixel(x, y).Red <= 0.15)
                    continue;

                count++;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        return (count, left, right);
    }

    /// <summary>
    /// Renders a small scene and hands back the error that stopped it, or <c>null</c>.
    /// </summary>
    private string ErrorFrom(string body)
    {
        string path = Path.Combine(_directory, "bad.igl");

        File.WriteAllText(path,
            "camera { location [0, 0, -14]  look at [0, 0, 0] }\n" +
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
