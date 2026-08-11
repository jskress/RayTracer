using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the two things that may stand among surfaces without being one: a choice, and a
/// name worked out on the spot.  A loop is the third and has tests of its own.
/// <para>
/// What they make is counted rather than looked at.  The scenes here stand a row of small spheres along
/// X, well apart, and the test counts the runs of color across the middle of the picture.  A choice
/// that takes the wrong arm, or a name that carries the wrong value, changes that count, and a count is
/// a great deal easier to trust than a judgement about whether a picture looks right.
/// </para>
/// </summary>
[TestClass]
public class TestSurfaceLists
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"list-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private const int Wide = 400;
    private const int High = 60;

    /// <summary>
    /// Renders the given scene and hands back the picture, or whatever stopped it.
    /// </summary>
    private (Canvas Image, string Error) Render(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            "context { angles are degrees  no gamma }\n" +
            "camera { location [0, 0, -24]  look at [0, 0, 0]  field of view 40 }\n" +
            "point light { location [0, 6, -10] }\n" + scene);

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
                OutputFileName = output, Width = Wide, Height = High
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

    /// <summary>
    /// This counts how many separate things ended up in a row, by walking across the middle of the
    /// picture and counting the runs of anything that is not the background.
    /// </summary>
    private int ThingsInARow(string scene)
    {
        (Canvas image, string error) = Render(scene);

        Assert.IsNull(error, error);

        int found = 0;
        bool inside = false;

        for (int x = 0; x < Wide; x++)
        {
            bool lit = false;

            for (int y = 0; y < High; y++)
            {
                Color pixel = image.GetPixel(x, y);

                lit |= pixel.Red + pixel.Green + pixel.Blue > 0.05;
            }

            if (lit && !inside)
                found++;

            inside = lit;
        }

        return found;
    }

    /// <summary>
    /// One small sphere, placed by whatever is put in for the count.
    /// </summary>
    private const string Ball = """
        sphere { material { pigment Red }  scale 0.35  translate X COUNT * 1.4 - 3.5 }
        """;

    private static string BallAt(string where) => Ball.Replace("COUNT", where);

    [TestMethod]
    public void TestAChoiceMakesOneArmOrTheOther()
    {
        Assert.AreEqual(1, ThingsInARow($$"""
            group {
                if (2 > 1) {
                    {{BallAt("1")}}
                }
                else {
                    {{BallAt("2")}}
                    {{BallAt("3")}}
                }
            }
            """));

        Assert.AreEqual(2, ThingsInARow($$"""
            group {
                if (1 > 2) {
                    {{BallAt("1")}}
                }
                else {
                    {{BallAt("2")}}
                    {{BallAt("3")}}
                }
            }
            """));
    }

    [TestMethod]
    public void TestAChoiceNeedsNoElse()
    {
        // The one way this differs from the choice that ends a function's body, where both ways out
        // must give an answer.  Here an arm makes things, and making nothing is a perfectly good thing
        // to do -- so the second arm is left off whenever there is nothing to put in it.
        Assert.AreEqual(1, ThingsInARow($$"""
            group {
                if (2 > 1) {
                    {{BallAt("1")}}
                }
            }
            """));

        Assert.AreEqual(0, ThingsInARow($$"""
            group {
                if (1 > 2) {
                    {{BallAt("1")}}
                }
            }
            """));
    }

    [TestMethod]
    public void TestAChoiceInALoopDecidesEveryTurnForItself()
    {
        // What the whole thing is for.  Six turns, and the ball is made on the three where the count is
        // even, which is a decision taken again on every turn rather than once for the loop.
        Assert.AreEqual(3, ThingsInARow($$"""
            group {
                for i in [0, 5] {
                    if (i % 2 == 0) {
                        {{BallAt("i")}}
                    }
                }
            }
            """));
    }

    [TestMethod]
    public void TestAnElseMayCarryAnotherIf()
    {
        // Written as a chain rather than as a body holding a choice holding a body, which is what saves
        // a run of cases from walking off the right of the page.  Each turn takes exactly one arm, so
        // six turns make six balls however many arms there are.
        Assert.AreEqual(6, ThingsInARow($$"""
            group {
                for i in [0, 5] {
                    if (i < 2) {
                        {{BallAt("i")}}
                    }
                    else if (i < 4) {
                        {{BallAt("i")}}
                    }
                    else {
                        {{BallAt("i")}}
                    }
                }
            }
            """));

        // And the arms really are told apart: only the middle two make anything here.
        Assert.AreEqual(2, ThingsInARow($$"""
            group {
                for i in [0, 5] {
                    if (i < 2) { }
                    else if (i < 4) {
                        {{BallAt("i")}}
                    }
                    else { }
                }
            }
            """));
    }

    [TestMethod]
    public void TestANameIsKnownToTheRestOfItsList()
    {
        // Including inside the surfaces standing there, which is where it is nearly always wanted.
        Assert.AreEqual(3, ThingsInARow("""
            group {
                gap = 1.4
                sphere { material { pigment Red }  scale 0.35  translate X 0 * gap - 3.5 }
                sphere { material { pigment Red }  scale 0.35  translate X 1 * gap - 3.5 }
                sphere { material { pigment Red }  scale 0.35  translate X 2 * gap - 3.5 }
            }
            """));
    }

    [TestMethod]
    public void TestANameInALoopIsWorkedOutAfreshEveryTurn()
    {
        // A name inside a loop is the case that matters, since what it stands for usually depends on
        // the count and so is a different value every turn.  Were it worked out once, all six balls
        // would land on top of each other and come out as one.
        Assert.AreEqual(6, ThingsInARow("""
            group {
                for i in [0, 5] {
                    place = i * 1.4 - 3.5
                    sphere { material { pigment Red }  scale 0.35  translate X place }
                }
            }
            """));
    }

    [TestMethod]
    public void TestANameDoesNotEscapeTheListItStandsIn()
    {
        foreach (string scene in new[]
                 {
                     """
                     group { gap = 1.4  sphere { scale 0.35 } }
                     sphere { scale 0.35  translate X gap }
                     """,
                     """
                     group {
                         if (2 > 1) { gap = 1.4  sphere { scale 0.35 } }
                         sphere { scale 0.35  translate X gap }
                     }
                     """,
                     """
                     group {
                         for i in [0, 1] { gap = 1.4  sphere { scale 0.35 } }
                         sphere { scale 0.35  translate X gap }
                     }
                     """
                 })
        {
            (Canvas image, string error) = Render(scene);

            Assert.IsNull(image, scene);
            Assert.IsTrue(error.Contains("gap"), $"the complaint should name it: {error}");
        }
    }

    [TestMethod]
    public void TestANameMayStandOverOneFromFurtherOutWithoutDisturbingIt()
    {
        // Three balls bunched together inside the group, where the gap is small, and three spread out
        // after it, where the gap is the one the file gave.  Were the inner name to escape, the second
        // three would bunch as well and the count would be two rather than four.
        Assert.AreEqual(4, ThingsInARow("""
            gap = 1.4
            group {
                gap = 0.05
                for i in [0, 2] {
                    sphere { material { pigment Red }  scale 0.35  translate X i * gap - 4.5 }
                }
            }
            for i in [0, 2] {
                sphere { material { pigment Red }  scale 0.35  translate X i * gap + 1.5 }
            }
            """));
    }

    [TestMethod]
    public void TestAChoiceMayStandWhereverSurfacesAreListed()
    {
        // At the top of a file, inside a scene block, inside a group, inside a loop and inside another
        // choice.  The grammar lists these places separately, so each is tried.
        Assert.AreEqual(1, ThingsInARow($$"""
            if (2 > 1) {
                {{BallAt("1")}}
            }
            """));

        Assert.AreEqual(1, ThingsInARow($$"""
            scene {
                camera { location [0, 0, -24]  look at [0, 0, 0]  field of view 40 }
                point light { location [0, 6, -10] }
                if (2 > 1) {
                    {{BallAt("1")}}
                }
            }
            """));

        Assert.AreEqual(1, ThingsInARow($$"""
            group {
                if (2 > 1) {
                    if (3 > 1) {
                        {{BallAt("1")}}
                    }
                }
            }
            """));
    }

    [TestMethod]
    public void TestALoopMayStandInsideAChoice()
    {
        Assert.AreEqual(4, ThingsInARow($$"""
            group {
                if (2 > 1) {
                    for i in [0, 3] {
                        {{BallAt("i")}}
                    }
                }
            }
            """));
    }

    [TestMethod]
    public void TestOnlySurfacesMayStandInAChoice()
    {
        // A choice is a way of writing rather than a thing in the scene, so there is nothing for a
        // transform or a material to be about, exactly as with a loop.
        foreach (string written in new[]
                 {
                     "translate X 1", "material { pigment Red }", "named 'thing'"
                 })
        {
            (Canvas image, string error) = Render($$"""
                group {
                    if (2 > 1) {
                        sphere { material { pigment Red }  scale 0.35 }
                        {{written}}
                    }
                }
                """);

            Assert.IsNull(image, written);
            Assert.IsTrue(error.Contains("Only surfaces may stand inside an \"if\""),
                $"{written}: the complaint should say what may: {error}");
        }
    }

    [TestMethod]
    public void TestAConditionMustBeTrueOrFalse()
    {
        (Canvas image, string error) = Render("""
            group {
                if (3) { sphere { scale 0.35 } }
            }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("must be true or false"), error);
    }

    [TestMethod]
    public void TestAnElseMustCarrySomething()
    {
        // Worth a test of its own because of where it can happen: an "else" may be the last thing in a
        // file, and the parser lets go of the file the moment its last clause has been read.  The
        // complaint has to be made without one.
        foreach (string scene in new[]
                 {
                     "if (2 > 1) { sphere { scale 0.35 } } else",
                     "if (2 > 1) { sphere { scale 0.35 } } else sphere { }",
                     "group { if (2 > 1) { sphere { scale 0.35 } } else }"
                 })
        {
            (Canvas image, string error) = Render(scene);

            Assert.IsNull(image, scene);
            Assert.IsTrue(error.Contains("to follow \"else\""),
                $"{scene}: the complaint should say what is missing: {error}");
        }
    }
}
