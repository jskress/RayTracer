using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover which names a scene can see from where, and the functions a scene writes for
/// itself.
/// <para>
/// Until now the answer was "all of them from everywhere": the class that holds a scene's names could
/// nest one set inside another and never did, only one ever being built for a whole render.  A group's
/// loop counter was therefore written into the scene at large and left lying about after the group had
/// finished with it.  These pin the behaviour that replaces that, because the functions this is
/// groundwork for cannot be written at all until a call can have names of its own.
/// </para>
/// </summary>
[TestClass]
public class TestVariableScoping
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"scoping-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders the given scene and hands back the picture, or whatever stopped it.
    /// </summary>
    private (Canvas Image, string Error) Render(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path, "context { angles are degrees  no gamma }\n" + scene);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = 30, Height = 30
            });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.ToString());
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    private const string Staging = """
        camera { location [0, 0, -14]  look at [0, 0, 0]  field of view 40 }
        point light { location [-4, 4, -6] }
        """;

    [TestMethod]
    public void TestALoopCounterDoesNotOutliveItsLoop()
    {
        // What a scope is for.  The counter belongs to the group, and a scene that reaches for it
        // afterward should be told plainly rather than quietly handed whatever the loop left behind --
        // which, before this, was the loop's last value.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            group {
                index = [0, 2]
                sphere { material { pigment Red }  scale 0.4  translate X index }
            }
            sphere { material { pigment Blue }  scale 0.4  translate [index, 1.5, 0] }
            """);

        Assert.IsNull(image, "the scene should not have rendered at all");
        Assert.IsTrue(error.Contains("index"),
            $"the complaint should name the variable, and was: {error}");
    }

    [TestMethod]
    public void TestALoopStillSeesWhatSurroundsIt()
    {
        // A scope hands on what it does not hold itself, so everything named outside the group is
        // still in view inside it.  Without that, giving the loop a scope would have broken every
        // scene that uses one.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            spacing = 1.1
            paint = material { pigment Red }
            group {
                index = [0, 2]
                sphere { material paint  scale 0.4  translate X index * spacing }
            }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestTwoLoopsMayUseTheSameNameWithoutColliding()
    {
        // Each loop holds its counter itself, so an inner one does not overwrite an outer one.  The
        // inner group's own transform is settled with the names its *parent* loop is holding, and its
        // children with the names its own loop is holding -- so both counters are in play at once,
        // under the same name, meaning different things in different places.  Sharing one set of names
        // for a whole render made that impossible.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            group {
                index = [0, 2]
                group {
                    index = [0, 2]
                    sphere { material { pigment Red }  scale 0.3  translate X index * 1.2 }
                    translate Y index * 1.2
                }
            }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);

        // Nine spheres in a grid, so red must appear at several heights and several widths.  One
        // counter shadowing the other would give a single row or a single column.
        HashSet<int> rows = [];
        HashSet<int> columns = [];

        for (int x = 0; x < 30; x++)
        {
            for (int y = 0; y < 30; y++)
            {
                Color pixel = image.GetPixel(x, y);

                if (pixel.Red > 0.25 && pixel.Green < 0.15)
                {
                    rows.Add(y / 4);
                    columns.Add(x / 4);
                }
            }
        }

        Assert.IsTrue(rows.Count >= 3, $"the outer counter should spread them over rows: {rows.Count}");
        Assert.IsTrue(columns.Count >= 3,
            $"the inner counter should spread them over columns: {columns.Count}");
    }

    [TestMethod]
    public void TestAGroupsOwnPropertiesCannotSeeItsCounter()
    {
        // A group has one transform and a loop has many turns, so the counter has no single value to
        // mean there.  It used to quietly take whatever the loop had left in it -- the last turn's --
        // which is an answer, but not one anybody asked for.  Now it says so.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            group {
                index = [0, 2]
                sphere { material { pigment Red }  scale 0.4  translate X index }
                translate Y index
            }
            """);

        Assert.IsNull(image, "the scene should not have rendered");
        Assert.IsTrue(error.Contains("index"), $"and should name the counter: {error}");
    }

    [TestMethod]
    public void TestASceneMayWriteAFunctionOfItsOwn()
    {
        // Parameters, a fallback, workings along the way, and an answer -- called from an ordinary
        // expression, which is the whole point of it.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function reach(step, spacing = 1.2) -> number {
                grown = 1 + step * 0.4
                return grown * spacing
            }
            group {
                index = [0, 2]
                sphere { material { pigment Red }  scale 0.35  translate X reach(index) }
            }
            sphere { material { pigment Blue }  scale 0.35  translate X reach(0, 0) }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAFunctionMayHaveASmallerOneOfItsOwn()
    {
        // A helper used only by one function has no business being visible to the whole scene, which
        // is the point of allowing this: a library may export the one name it means to.  The inner one
        // is bound to the *call's* scope, so it sees the values the outer one was handed -- which is
        // what makes it a helper rather than a separate function that must be passed everything.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function spiral(step) -> number {
                function easedBy(amount) -> number { return step * amount }
                stretch = easedBy(0.45)
                return 1 + stretch
            }
            group {
                index = [0, 2]
                sphere { material { pigment Red }  scale 0.35  translate X spiral(index) }
            }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestASmallerFunctionIsNotVisibleOutside()
    {
        // The other half of it, and the reason a library can be trusted not to litter.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function spiral(step) -> number {
                function easedBy(amount) -> number { return step * amount }
                return easedBy(0.45)
            }
            sphere { scale easedBy(2) }
            """);

        Assert.IsNull(image, "the helper should not be reachable from outside");
        Assert.IsTrue(error.Contains("easedBy"), $"and should be named in the complaint: {error}");
    }

    [TestMethod]
    public void TestAFunctionHoldingAnotherCannotBeFoldedIntoAField()
    {
        // Following from the same rule as workings: what a field can fold in is an expression, and a
        // function that first declares another is a small procedure rather than an expression.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function ball(radius) -> number {
                function shrunk(by) -> number { return radius * by }
                return max(0, shrunk(0.9) - √(x² + y² + z²))
            }
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium { scattering 1.6  density function { ball(0.9) } } }
                }
                scale 1.4
            }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("ball"), $"the complaint should name it: {error}");
    }

    [TestMethod]
    public void TestACallIsCheckedAgainstWhatTheFunctionTakes()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function reach(step, spacing = 1.2) -> number { return step * spacing }
            sphere { scale reach() }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("reach") && error.Contains("takes"),
            $"the complaint should say what it takes: {error}");
    }

    [TestMethod]
    public void TestFallbacksMustComeLast()
    {
        // A call leaves values off the end, so a fallback before a required one promises something
        // that could never be taken up.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function reach(spacing = 1.2, step) -> number { return step * spacing }
            sphere { scale reach(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("step"), $"the complaint should name it: {error}");
    }

    [TestMethod]
    public void TestAPlainFunctionMayBeFoldedIntoADensity()
    {
        // A body that is a single answer folds bodily into a field, so everything a density does with
        // arithmetic still works.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function ball(radius) -> number { return max(0, radius - √(x² + y² + z²)) }
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium { scattering 1.6  density function { ball(0.9) } } }
                }
                scale 1.4
            }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAFunctionWithWorkingsIsRefusedInADensity()
    {
        // The line drawn deliberately: a field is compiled down and, for an isosurface,
        // differentiated, which can be done to an expression folded in and cannot be done to a small
        // procedure.  Being told so plainly is the whole of the promise.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function ball(radius) -> number {
                edge = radius - √(x² + y² + z²)
                return max(0, edge)
            }
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium { scattering 1.6  density function { ball(0.9) } } }
                }
                scale 1.4
            }
            """);

        Assert.IsNull(image, "the scene should not have rendered");
        Assert.IsTrue(error.Contains("ball") && error.Contains("density"),
            $"the complaint should name the function and say where: {error}");
    }

    [TestMethod]
    public void TestAMisspelledNameSaysWhichName()
    {
        // The commonest mistake there is, and it used to come back as a complaint that some empty
        // value would not convert to a number -- true, and useless, since it never said which name was
        // empty or where.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            radius = 0.5
            sphere { material { pigment Red }  scale radiuss }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("radiuss"), $"the complaint should name it, and was: {error}");
    }
}
