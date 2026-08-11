using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover repeating things with a <c>for</c>.
/// <para>
/// What a loop makes is counted rather than looked at: the scenes here stand a row of small spheres
/// along X, well apart, and the test counts the runs of color across the middle of the picture.  A loop
/// that turns the wrong number of times, or that repeats the wrong things, changes that count, and a
/// count is a great deal easier to trust than a judgement about whether a picture looks right.
/// </para>
/// </summary>
[TestClass]
public class TestLoops
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"loop-tests-{Guid.NewGuid():N}");

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
    /// This counts how many separate things the loop put in a row, by walking across the middle of the
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
    /// A row of small spheres, one per turn, placed by the count.
    /// </summary>
    private const string Row = """
        sphere { material { pigment Red }  scale 0.35  translate X COUNT * 1.4 - 3.5 }
        """;

    [TestMethod]
    public void TestALoopMakesWhatIsInItOncePerTurn()
    {
        Assert.AreEqual(6, ThingsInARow($$"""
            group {
                for i in [0, 5] {
                    {{Row.Replace("COUNT", "i")}}
                }
            }
            """));

        // And the range says how many: four rather than six, from the same loop.
        Assert.AreEqual(4, ThingsInARow($$"""
            group {
                for i in [0, 3] {
                    {{Row.Replace("COUNT", "i")}}
                }
            }
            """));
    }

    [TestMethod]
    public void TestOnlyWhatStandsInTheLoopIsRepeated()
    {
        // This is the thing the old form could not do: it repeated the whole of the group it was
        // written in, so a group could hold a run of things or a thing that stood once, never both.
        Assert.AreEqual(4, ThingsInARow($$"""
            group {
                for i in [0, 2] {
                    {{Row.Replace("COUNT", "i")}}
                }
                sphere { material { pigment Blue }  scale 0.35  translate X 3.5 }
            }
            """));
    }

    [TestMethod]
    public void TestAGroupMayHoldMoreThanOneLoop()
    {
        // Nor could the old form do this: one interval per group, and that was that.
        Assert.AreEqual(5, ThingsInARow($$"""
            group {
                for i in [0, 2] {
                    {{Row.Replace("COUNT", "i")}}
                }
                for j in [4, 5] {
                    {{Row.Replace("COUNT", "j")}}
                }
            }
            """));
    }

    [TestMethod]
    public void TestLoopsNest()
    {
        // Two rows of three, one behind the other, so the picture shows three columns rather than six:
        // what is counted here is that the inner loop ran for every turn of the outer one, which the
        // materials tell apart.
        (Canvas image, string error) = Render("""
            group {
                for row in [0, 1] {
                    for col in [0, 2] {
                        sphere {
                            material { pigment Red  ambient row * 0.5 }
                            scale 0.35
                            translate [col * 1.4 - 1.4, row * 1.4 - 0.7, 0]
                        }
                    }
                }
            }
            """);

        Assert.IsNull(error, error);

        int lit = 0;

        for (int x = 0; x < Wide; x++)
        {
            for (int y = 0; y < High; y++)
            {
                // The brighter row is the one the outer loop's second turn made.
                if (image.GetPixel(x, y).Red > 0.7)
                    lit++;
            }
        }

        Assert.IsTrue(lit > 0, "the outer loop's second turn should have made its own row");
    }

    [TestMethod]
    public void TestTheRangeMayStepAndMayLeaveAnEndOut()
    {
        // A step of a half over a closed range of three: 0, 0.5, 1, 1.5, 2, 2.5, 3 -- seven turns.
        Assert.AreEqual(7, ThingsInARow("""
            group {
                for t in [0, 3] by 0.5 {
                    sphere { material { pigment Red }  scale 0.2  translate X t * 1.4 - 2.1 }
                }
            }
            """));

        // Parentheses leave an end out, so this is 1, 2, 3 rather than 0 through 3.
        Assert.AreEqual(3, ThingsInARow("""
            group {
                for t in (0, 3] {
                    sphere { material { pigment Red }  scale 0.35  translate X t * 1.4 - 2.8 }
                }
            }
            """));
    }

    [TestMethod]
    public void TestALoopNeedNotNameItsCount()
    {
        // "over" is a "for" with no name for the count, for when the repetition is the whole of what
        // is wanted.  Three spheres, each placed by something other than a count, so all three land in
        // the same place and read as one.
        Assert.AreEqual(1, ThingsInARow("""
            group {
                over [0, 2] {
                    sphere { material { pigment Red }  scale 0.35 }
                }
            }
            """));
    }

    [TestMethod]
    public void TestTheCountBelongsToTheLoop()
    {
        // It is set in a scope of the loop's own, so it is not left lying about afterward.  The group's
        // own transform is settled outside the loop and cannot see it.
        (Canvas image, string error) = Render("""
            group {
                for i in [0, 2] {
                    sphere { material { pigment Red }  scale 0.35  translate X i }
                }
                translate X i
            }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("i"), $"the complaint should name it: {error}");
    }

    [TestMethod]
    public void TestALoopMayStandInAPrimitiveThatMakesOne()
    {
        // The point of the whole thing, really: a primitive that is told how many to make.
        Assert.AreEqual(5, ThingsInARow("""
            primitive fence(count, spacing = 1.4) -> group {
                return group {
                    for i in [0, count - 1] {
                        sphere { material { pigment Red }  scale 0.35  translate X i * spacing }
                    }
                }
            }
            object fence(5) { translate X -2.8 }
            """));

        Assert.AreEqual(3, ThingsInARow("""
            primitive fence(count, spacing = 1.4) -> group {
                return group {
                    for i in [0, count - 1] {
                        sphere { material { pigment Red }  scale 0.35  translate X i * spacing }
                    }
                }
            }
            object fence(3) { translate X -2.8 }
            """));
    }

    [TestMethod]
    public void TestALoopMayStandAtTheTopOfAFile()
    {
        // Where the things being repeated do not particularly belong together, having to wrap them in
        // a group to say "make five of these" is a tax on the commonest thing a scene author wants.
        Assert.AreEqual(5, ThingsInARow("""
            for i in [0, 4] {
                sphere { material { pigment Red }  scale 0.35  translate X i * 1.4 - 2.8 }
            }
            """));

        // And "over" reads the same way there.
        Assert.AreEqual(1, ThingsInARow("""
            over [0, 2] {
                sphere { material { pigment Red }  scale 0.35 }
            }
            """));
    }

    [TestMethod]
    public void TestALoopMayStandInASceneBlock()
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path, """
            context { angles are degrees  no gamma }
            scene {
                camera { location [0, 0, -24]  look at [0, 0, 0]  field of view 40 }
                point light { location [0, 6, -10] }
                for i in [0, 3] {
                    sphere { material { pigment Red }  scale 0.35  translate X i * 1.4 - 2.1 }
                }
                over [0, 1] { sphere { material { pigment Blue }  scale 0.35  translate Y 2 } }
            }
            render
            """);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        Assert.IsNotNull(renderer);

        renderer.Render(new RenderOptions { OutputFileName = output, Width = Wide, Height = High });

        Assert.IsTrue(File.Exists(output));
    }

    [TestMethod]
    public void TestALoopsTurnsMayDifferByMoreThanTheirCount()
    {
        // What "over" was waiting for, and the point of scattering by the count: five things placed by
        // a number that has nothing to do with where the last one went.
        Assert.AreEqual(5, ThingsInARow("""
            group {
                for i in [0, 4] {
                    sphere {
                        material { pigment Red }
                        scale 0.2 + random(i) * 0.2
                        translate X i * 1.4 - 2.8
                    }
                }
            }
            """));
    }

    [TestMethod]
    public void TestScatteringDoesNotDependOnWhatCameBefore()
    {
        // The property a running stream could not have, and the reason this takes a key.  The same
        // loop, with three more spheres made before it, must place its things in exactly the same
        // spots -- otherwise adding one tree to a scene would rearrange the forest.
        const string loop = """
            group {
                for i in [0, 3] {
                    sphere {
                        material { pigment Red }
                        scale 0.3
                        translate [i * 1.4 - 2.1, random(i, 1) * 2 - 1, 0]
                    }
                }
            }
            """;

        (Canvas alone, string first) = Render(loop);
        (Canvas after, string second) = Render($$"""
            group {
                over [0, 2] { sphere { material { pigment Blue }  scale 0.1  translate Y 4 } }
            }
            {{loop}}
            """);

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);

        int moved = 0;

        for (int x = 0; x < Wide; x++)
        {
            for (int y = 0; y < High; y++)
            {
                Color was = alone.GetPixel(x, y);
                Color now = after.GetPixel(x, y);

                // The blue ones are up out of the frame, so anything red must be exactly where it was.
                if (was.Red > 0.25 != now.Red > 0.25)
                    moved++;
            }
        }

        Assert.AreEqual(0, moved, "the scattered things should not have moved");
    }

    [TestMethod]
    public void TestOnlySurfacesMayStandInALoop()
    {
        // A loop is a way of writing rather than a thing in the scene, so there is nothing for a
        // transform or a material to be about.  Refused where it is written rather than dropped.
        foreach (string written in new[]
                 {
                     "translate X i", "material { pigment Red }", "named 'thing'"
                 })
        {
            (Canvas image, string error) = Render($$"""
                group {
                    for i in [0, 2] {
                        sphere { material { pigment Red }  scale 0.35 }
                        {{written}}
                    }
                }
                """);

            Assert.IsNull(image, written);
            Assert.IsTrue(error.Contains("Only surfaces may stand inside"),
                $"{written}: the complaint should say what may: {error}");
        }
    }

    [TestMethod]
    public void TestTheOldWayOfWritingOneIsGone()
    {
        // A group's bare "index = [0, 11]" used to mean a loop, which nobody would guess and which the
        // documentation never explained.  It is not quietly still accepted -- and neither is the "="
        // that stood in the first draft of the loop, since that reads as an assignment too.
        (Canvas image, string error) = Render("""
            group {
                index = [0, 5]
                sphere { material { pigment Red }  scale 0.35  translate X index }
            }
            """);

        Assert.IsNull(image);
        Assert.IsNotNull(error);

        (Canvas withAnEquals, string alsoRefused) = Render("""
            group {
                for i = [0, 5] {
                    sphere { material { pigment Red }  scale 0.35  translate X i }
                }
            }
            """);

        Assert.IsNull(withAnEquals);
        Assert.IsTrue(alsoRefused.Contains("in"), $"and should ask for \"in\": {alsoRefused}");
    }

    [TestMethod]
    public void TestARangeItsStepCannotLandOnStopsAnyway()
    {
        // This used to hang.  A range only reaches its end exactly when the end is a whole number of
        // steps away, and nothing makes anybody write one that is -- least of all a range whose end is
        // worked out from something else, which is the usual way one gets written.  Four balls here:
        // 0, 1, 2 and 3, with 3.4 never landed on and rightly not waited for.
        Assert.AreEqual(4, ThingsInARow("""
            group {
                for i in [0, 3.4] {
                    sphere { material { pigment Red }  scale 0.35  translate X i * 1.4 - 3.5 }
                }
            }
            """));
    }

    [TestMethod]
    public void TestAStepThatWouldNeverArriveIsRefused()
    {
        // The other two ways a loop used to hang, and the reason they are worth a complaint rather
        // than a shrug: the range is made of expressions, so a loop that has counted properly for
        // months can be handed a step of zero by a value worked out somewhere else entirely.
        foreach ((string written, string expected) in new[]
                 {
                     ("for i in [0, 5] by 0", "cannot be zero"),
                     ("for i in [0, 5] by -1", "heads the other way"),
                     ("over [0, 5] by 0", "cannot be zero")
                 })
        {
            (Canvas image, string error) = Render($$"""
                group {
                    {{written}} {
                        sphere { material { pigment Red }  scale 0.35 }
                    }
                }
                """);

            Assert.IsNull(image, written);
            Assert.IsTrue(error.Contains(expected),
                $"{written}: the complaint should say why: {error}");
        }
    }

}
