using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the <c>normal</c> clause: how a scene says that a surface is roughened.
/// <para>
/// They render rather than merely parse, for the reason every other rendering test here does: a
/// material is put together when the image is made, not when the file is read, so a clause that
/// parses perfectly may still have nothing behind it.
/// </para>
/// </summary>
[TestClass]
public class TestNormalClause
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"normal-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders a scene holding the given surface, and reports what went wrong, or <c>null</c> if
    /// nothing did.
    /// </summary>
    private string ErrorFrom(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(path,
            "camera { location [0, 0, -5] look at [0, 0, 0] }\n" +
            "light { location [-4, 6, -8] }\n" + scene);

        StringWriter output = new ();
        TextWriter was = Console.Out;

        Console.SetOut(output);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return output.ToString();

            renderer.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "out.png"), Width = 8, Height = 8
            });

            string text = output.ToString();

            return text.Contains("Error") ? text : null;
        }
        catch (Exception exception)
        {
            return exception.ToString();
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    [TestMethod]
    public void TestAPatternMayRoughenASurface()
    {
        Assert.IsNull(ErrorFrom("""
            sphere { material { pigment color [0.8, 0.8, 0.8] normal granite { depth 0.4 } } }
            """));
    }

    [TestMethod]
    public void TestTheDepthAndTheTransformsMayComeInEitherOrder()
    {
        // Both are looked for until neither is there, so neither has to come first.
        Assert.IsNull(ErrorFrom("""
            sphere { material { pigment color [0.8, 0.8, 0.8] normal granite { depth 0.4 scale 2 } } }
            """));
        Assert.IsNull(ErrorFrom("""
            sphere { material { pigment color [0.8, 0.8, 0.8] normal granite { scale 2 depth 0.4 } } }
            """));
    }

    [TestMethod]
    public void TestARougheningMayBeLeftToItsDefaults()
    {
        // Nothing at all inside the braces, which should mean a depth of one and no placing.
        Assert.IsNull(ErrorFrom("""
            sphere { material { pigment color [0.8, 0.8, 0.8] normal granite { } } }
            """));
    }

    [TestMethod]
    public void TestTheWholePatternGrammarIsAvailable()
    {
        // The point of writing this as the pigment's sibling: turbulence, the waves, the seeds and
        // the transforms all come along without being spelled out again.
        Assert.IsNull(ErrorFrom("""
            sphere {
                material {
                    pigment color [0.8, 0.8, 0.8]
                    normal with seed 7 bozo {
                        turbulence { amplitude 0.3 octaves 3 }
                        sine wave
                        frequency 2
                        depth 0.5
                        scale [1, 0.2, 1]
                        rotate Y 30
                    }
                }
            }
            """));
    }

    [TestMethod]
    public void TestThePatternsThatOnlyRoughenAreThere()
    {
        // Ripples and waves earn their keep here rather than in a pigment.
        Assert.IsNull(ErrorFrom("""
            sphere { material { pigment color [0.6, 0.7, 0.9] normal ripples { frequency 5 depth 1 } } }
            sphere {
                material { pigment color [0.6, 0.7, 0.9] normal waves { frequency 5 depth 1 } }
                translate X 3
            }
            """));
    }

    [TestMethod]
    public void TestARougheningIsCarriedByAMaterialThatIsNamedAndExtended()
    {
        Assert.IsNull(ErrorFrom("""
            Rough = material { pigment color [0.8, 0.8, 0.8] normal granite { depth 0.4 } }

            sphere { material Rough }
            sphere { material Rough { pigment color [0.2, 0.4, 0.8] } translate X 3 }
            """));
    }

    [TestMethod]
    public void TestAPatternIsRequired()
    {
        string error = ErrorFrom("""
            sphere { material { pigment color [0.8, 0.8, 0.8] normal { depth 0.4 } } }
            """);

        Assert.IsNotNull(error, "a roughening with no pattern was accepted");
    }
}
