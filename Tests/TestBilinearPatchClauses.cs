using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the bilinear patch from the scene's side: that a scene can write one, that the
/// corners it writes reach the shape, and that a named one can be used again.
/// <para>
/// That last matters because `bilinear patch` is a block whose start is two words, and those have a
/// history here -- see <see cref="TestNamedSurfaceReuse"/> for what went wrong with the others.
/// </para>
/// </summary>
[TestClass]
public class TestBilinearPatchClauses
{
    private const string Flat = "points [-1, 0, -1], [1, 0, -1], [1, 0, 1], [-1, 0, 1]";
    private const string Lifted = "points [-1, 0, -1], [1, 0, -1], [1, 1.4, 1], [-1, 0, 1]";

    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"bilinear-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void TestASceneMayWriteABilinearPatch()
    {
        Assert.IsNull(ErrorFrom($"bilinear patch {{ {Flat} }}"));
    }

    [TestMethod]
    public void TestAPatchWithNoCornersIsRefused()
    {
        string error = ErrorFrom("bilinear patch { material { pigment [1, 1, 1] } }");

        Assert.IsNotNull(error, "A patch with no corners should not have been accepted.");
        Assert.IsTrue(error.Contains("points", StringComparison.OrdinalIgnoreCase),
            $"The complaint should say what was missing, but said: {error}");
    }

    /// <summary>
    /// The corners have to reach the shape rather than merely be accepted, so the same patch is
    /// drawn flat and then with one corner lifted.  It covers *more* of the picture afterwards,
    /// not less, and the obvious guess is the other way about: tilting a quarter of the patch away
    /// from a camera looking straight down surely shows less of it.  What outweighs that is
    /// perspective -- the lifted corner is a good deal nearer the camera than the flat one was,
    /// and nearer things are bigger.
    /// </summary>
    [TestMethod]
    public void TestTheCornersAreCarried()
    {
        int flat = Lit($"bilinear patch {{ {Flat} }}");
        int lifted = Lit($"bilinear patch {{ {Lifted} }}");

        Assert.IsTrue(flat > 0, "The flat patch covered nothing at all.");
        Assert.IsTrue(lifted > flat * 1.05,
            $"The lifted patch covered {lifted} pixels against the flat one's {flat}, which is " +
            "not enough of a difference to say the corner arrived.");
    }

    [TestMethod]
    public void TestANamedPatchCanBeUsedAgain()
    {
        Assert.IsNull(ErrorFrom($$"""
                                  Panel = bilinear patch { {{Lifted}} }
                                  object Panel { translate [0.4, 0, 0] }
                                  """));
    }

    /// <summary>
    /// Renders one patch seen from straight above and counts how many pixels it covers.
    /// </summary>
    private int Lit(string body)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");
        int closing = body.LastIndexOf('}');
        string lit = body[..closing] +
                     "  material { pigment [1, 0.6, 0.3]  ambient 1  diffuse 0  specular 0 } }";

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 6, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 30 }\n" +
            "point light { location [-4, 8, -4] }\n" + lit);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        renderer.Render(new RenderOptions
        {
            OutputFileName = output, Width = 60, Height = 60
        });

        Canvas canvas = new ImageFile(output).Load()[0];
        int count = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                if (canvas.GetPixel(x, y).Red > 0.15)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Renders a small scene and hands back the error that stopped it, or <c>null</c>.
    /// </summary>
    private string ErrorFrom(string body)
    {
        string path = Path.Combine(_directory, "bad.igl");

        File.WriteAllText(path,
            "camera { location [0, 4, -5]  look at [0, 0, 0] }\n" +
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
