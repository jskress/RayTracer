using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the tapered extrusion from the scene's side rather than the engine's: that a
/// scene can write one, that the taper it writes actually reaches the shape, and that a named one
/// can be used again.
/// <para>
/// That last is not merely thoroughness.  A block whose start is two words -- "tapered extrusion",
/// like "smooth triangle" before it -- has its tokens shifted along by one, and the parser is told
/// so; an `object` reference carries no such words and must not have them skipped.  See
/// <see cref="TestNamedSurfaceReuse"/> for what that costs when it is got wrong.
/// </para>
/// </summary>
[TestClass]
public class TestTaperedExtrusionClauses
{
    private const string Square = """
                                  path {
                                      move to -0.5, -0.5
                                      line to 0.5, -0.5
                                      line to 0.5, 0.5
                                      line to -0.5, 0.5
                                      close
                                  }
                                  """;

    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"tapered-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void TestASceneMayWriteATaperedExtrusion()
    {
        Assert.IsNull(ErrorFrom($$"""
                                  tapered extrusion {
                                      taper 0.4
                                      {{Square}}
                                      min Y -1  max Y 1
                                  }
                                  """));
    }

    [TestMethod]
    public void TestANegativeTaperIsRefused()
    {
        string error = ErrorFrom($$"""
                                   tapered extrusion {
                                       taper -0.5
                                       {{Square}}
                                       min Y -1  max Y 1
                                   }
                                   """);

        Assert.IsNotNull(error, "A taper below nought should not have been accepted.");
        Assert.IsTrue(error.Contains("taper", StringComparison.OrdinalIgnoreCase),
            $"The complaint should say what was wrong, but said: {error}");
    }

    /// <summary>
    /// The taper has to reach the shape, not merely be accepted.  Two tapers are drawn and the lit
    /// pixels counted: a post drawn in to a third of its size must cover noticeably less of the
    /// picture than one that is not drawn in at all.
    /// </summary>
    [TestMethod]
    public void TestTheTaperIsCarried()
    {
        int straight = Lit($$"""
                             tapered extrusion {
                                 taper 1
                                 {{Square}}
                                 min Y -1  max Y 1
                             }
                             """);
        int drawnIn = Lit($$"""
                            tapered extrusion {
                                taper 0.33
                                {{Square}}
                                min Y -1  max Y 1
                            }
                            """);

        Assert.IsTrue(straight > 0, "The straight post covered nothing at all.");
        Assert.IsTrue(drawnIn < straight * 0.85,
            $"A tapered post covered {drawnIn} pixels against the straight one's {straight}, " +
            "which is not enough of a difference to say the taper arrived.");
    }

    [TestMethod]
    public void TestANamedTaperedExtrusionCanBeUsedAgain()
    {
        Assert.IsNull(ErrorFrom($$"""
                                  Post = tapered extrusion {
                                      taper 0.4
                                      {{Square}}
                                      min Y -1  max Y 1
                                  }
                                  object Post { translate [0.4, 0, 0] }
                                  """));
    }

    /// <summary>
    /// Renders a one-surface scene lit flat and counts how many pixels the surface covers.
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
            "camera { location [0, 0, -6]  look at [0, 0, 0]  field of view 40 }\n" +
            "point light { location [-4, 6, -6] }\n" + lit);

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

    /// <summary>
    /// An extrusion may be given the *name* of an outline as well as one written out where it stands,
    /// which is what lets a primitive be handed the shape it is to extrude.
    /// <para>
    /// The two are rendered and compared rather than merely run: a named outline that arrived empty
    /// would raise no complaint and draw nothing, and "it did not error" would not notice.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAnExtrusionMayBeGivenANamedOutline()
    {
        int written = Lit($$"""
                          extrusion {
                              {{Square}}
                              min Y -1  max Y 1
                          }
                          """);
        int named = Lit($$"""
                        Outline = {{Square}}
                        extrusion {
                            path Outline
                            min Y -1  max Y 1
                        }
                        """);
        int given = Lit($$"""
                        Outline = {{Square}}
                        primitive Post(shape) -> group {
                            return group { extrusion { path shape  min Y -1  max Y 1 } }
                        }
                        object Post(Outline) {
                        }
                        """);

        Assert.IsTrue(written > 0, "the outline written out covered nothing at all");
        Assert.AreEqual(written, named,
            $"a named outline covered {named} pixels against {written} for the same one written out");
        Assert.AreEqual(written, given,
            $"an outline handed to a primitive covered {given} pixels against {written}");
    }
}
