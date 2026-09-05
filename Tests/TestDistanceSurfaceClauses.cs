using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the two surfaces the scene language gained for sphere tracing, from the scene's
/// side rather than the engine's: that a scene can write one, that what it writes actually reaches
/// the shape, and that what cannot work is refused with something worth reading.
/// <para>
/// **What a property does is checked rather than where it lands.**  Nothing reaches into the surface
/// to read a field back; instead the picture is rendered twice and the two are compared, so a
/// property that arrives and is then ignored fails here just as one that never arrives.
/// </para>
/// </summary>
[TestClass]
public class TestDistanceSurfaceClauses
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"distance-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void TestASceneMayWriteASignedDistanceSurface()
    {
        Assert.IsNull(ErrorFrom("""
                                sdf {
                                    function { sqrt(x² + y² + z²) - 0.6 }
                                    bounded by [-1, -1, -1], [1, 1, 1]
                                }
                                """));
    }

    /// <summary>
    /// A Julia set needs no box: every set of this form lies inside a ball of radius two, so the
    /// shape knows its own extent where a piece of arithmetic does not.
    /// </summary>
    [TestMethod]
    public void TestASceneMayWriteAJuliaSetWithNoBox()
    {
        Assert.IsNull(ErrorFrom("julia { c [-0.2, 0.6, 0.2, 0]  iterations 8 }"));
    }

    /// <summary>
    /// A box cannot be defaulted to anything sensible for a distance function: a piece of arithmetic
    /// says nothing about where its surface might be, so one guessed at here would be a shape cut off
    /// at a guess.
    /// </summary>
    [TestMethod]
    public void TestASignedDistanceSurfaceWithNoBoxIsRefused()
    {
        StringAssert.Contains(
            ErrorFrom("sdf { function { sqrt(x² + y² + z²) - 0.6 } }"), "bounded by");
    }

    [TestMethod]
    public void TestASignedDistanceSurfaceWithNoFunctionIsRefused()
    {
        StringAssert.Contains(
            ErrorFrom("sdf { bounded by [-1, -1, -1], [1, 1, 1] }"), "function");
    }

    /// <summary>
    /// This tests that a Julia set's four numbers arrive as four, the fourth included.
    /// <para>
    /// The fourth is the one at risk: a tuple of three leaves it as not-a-number rather than nought,
    /// and carrying that into the iteration turns the whole shape into nothing at all.  So a `c` of
    /// three must still draw something, and must draw the same thing as a `c` of three and an
    /// explicit nought -- while moving the fourth must change what is drawn, or it is being ignored.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAJuliaSetsFourthNumberIsCarried()
    {
        int withThree = Lit("julia { c [-0.2, 0.6, 0.2]  iterations 8 }");
        int withNought = Lit("julia { c [-0.2, 0.6, 0.2, 0]  iterations 8 }");
        int withMore = Lit("julia { c [-0.2, 0.6, 0.2, 0.35]  iterations 8 }");

        Assert.IsTrue(withThree > 40,
            $"a c of three numbers drew only {withThree} pixels, so its fourth arrived as " +
            "not-a-number and took the shape with it");
        Assert.AreEqual(withNought, withThree,
            "leaving the fourth number out should be the same as writing nought for it");
        Assert.AreNotEqual(withNought, withMore,
            "changing the fourth number changed nothing, so it is not reaching the shape");
    }

    [TestMethod]
    public void TestAJuliaSetsIterationCountIsCarried()
    {
        Assert.AreNotEqual(
            Lit("julia { c [-0.2, 0.6, 0.2, 0]  iterations 2 }"),
            Lit("julia { c [-0.2, 0.6, 0.2, 0]  iterations 12 }"),
            "the iteration count changed nothing, so it is not reaching the shape");
    }

    /// <summary>
    /// Renders a one-surface scene lit by its own color alone and counts where the shape is, so that
    /// what is compared is the shape rather than how it happens to be shaded.
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
            "camera { location [2.6, 2, -3.2]  look at [0, 0, 0]  field of view 40 }\n" +
            "point light { location [-4, 6, -6] }\n" + lit);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        renderer.Render(new RenderOptions
        {
            OutputFileName = output, Width = 40, Height = 40
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
    /// Renders a one-surface scene and hands back the error that stopped it, or <c>null</c>.
    /// </summary>
    private string ErrorFrom(string body)
    {
        string path = Path.Combine(_directory, "bad.igl");

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
