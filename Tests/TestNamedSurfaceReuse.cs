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
