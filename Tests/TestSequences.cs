using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover the sequence: a run of values a scene can write down as one thing, and walk.
/// <para>
/// The language had no way to carry several of anything about, so anything a scene had several of
/// had to be written out one call at a time and a library could not be handed the set to work over.
/// A sequence holds any value, a sequence included, which is what lets a scene write a record --
/// a list of lists, each one a thing with fields -- without the language needing a record of its own.
/// </para>
/// </summary>
[TestClass]
public class TestSequences
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"sequence-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A list gathers whatever it is given, says how many, and gives one back by its place.  It is
    /// made by a function rather than by syntax of its own, because a call already parses with any
    /// number of values between its parentheses -- so this needed nothing added to the grammar.
    /// </summary>
    [TestMethod]
    public void TestAListGathersValuesAndGivesThemBack()
    {
        object made = FunctionCatalog.Instance.Match("list", 3.0, 5.0, 8.0).Invoke();

        Assert.IsInstanceOfType<Sequence>(made);

        Sequence sequence = (Sequence) made;

        Assert.AreEqual(3.0, (double) FunctionCatalog.Instance.Match("count", sequence).Invoke());
        Assert.AreEqual(5.0, FunctionCatalog.Instance.Match("item", sequence, 1.0).Invoke());
        Assert.AreEqual(8.0, FunctionCatalog.Instance.Match("item", sequence, 2.0).Invoke());
    }

    /// <summary>
    /// **A list holds lists**, and that is the point of it rather than a curiosity: it is how a scene
    /// writes a run of things that each have several parts, which no tuple can do -- a tuple holds
    /// four numbers at the most and is a point by the time it is read.
    /// </summary>
    [TestMethod]
    public void TestAListHoldsLists()
    {
        Sequence inner = (Sequence) FunctionCatalog.Instance.Match("list", 1.0, 2.0).Invoke();
        Sequence outer = (Sequence) FunctionCatalog.Instance.Match("list", inner, 9.0).Invoke();

        Assert.AreEqual(2.0, (double) FunctionCatalog.Instance.Match("count", outer).Invoke());

        object first = FunctionCatalog.Instance.Match("item", outer, 0.0).Invoke();

        Assert.IsInstanceOfType<Sequence>(first);
        Assert.AreEqual(2.0, (double) FunctionCatalog.Instance.Match("count", (Sequence) first).Invoke());
    }

    /// <summary>
    /// Asking for something that is not there says what is there, since the number is nearly always
    /// worked out rather than written and "index out of range" would leave you none the wiser.
    /// </summary>
    [TestMethod]
    public void TestAskingPastTheEndSaysWhatIsThere()
    {
        Sequence sequence = (Sequence) FunctionCatalog.Instance.Match("list", 1.0, 2.0).Invoke();

        try
        {
            FunctionCatalog.Instance.Match("item", sequence, 5.0).Invoke();

            Assert.Fail("asking for item 5 of a list of two should not have been allowed");
        }
        catch (Exception exception)
        {
            StringAssert.Contains(exception.Message, "list of 2");
            StringAssert.Contains(exception.Message, "0 to 1");
        }
    }

    /// <summary>
    /// **The test that matters**: walking a list makes exactly what writing the same things out one
    /// by one makes.  Held to the picture rather than to a count of surfaces, since what a scene is
    /// for is the picture.
    /// </summary>
    [TestMethod]
    public void TestWalkingAListMakesWhatWritingItOutMakes()
    {
        Canvas walked = Rendered(
            "balls = list(list(-2.0, 0.5), list(0.0, 0.8), list(2.0, 0.4))\n" +
            "for ball in balls {\n" +
            "    sphere { scale item(ball, 1)  translate [item(ball, 0), 0, 0] }\n" +
            "}\n");
        Canvas longhand = Rendered(
            "sphere { scale 0.5  translate [-2.0, 0, 0] }\n" +
            "sphere { scale 0.8  translate [0.0, 0, 0] }\n" +
            "sphere { scale 0.4  translate [2.0, 0, 0] }\n");

        AssertSame(walked, longhand, "a walked list should make what writing it out makes");
    }

    /// <summary>
    /// And a loop counting through a range still counts, which is the thing that had to keep working.
    /// </summary>
    [TestMethod]
    public void TestCountingThroughARangeStillCounts()
    {
        Canvas counted = Rendered(
            "for i in [0, 2] {\n" +
            "    sphere { scale 0.5  translate [i * 2 - 2, 0, 0] }\n" +
            "}\n");
        Canvas longhand = Rendered(
            "sphere { scale 0.5  translate [-2, 0, 0] }\n" +
            "sphere { scale 0.5  translate [ 0, 0, 0] }\n" +
            "sphere { scale 0.5  translate [ 2, 0, 0] }\n");

        AssertSame(counted, longhand, "a range loop should still count as it did");
    }

    /// <summary>
    /// **Every way of writing a range still counts, and that is the point of this test.**  A range
    /// says whether each end is in or out the way mathematics does -- a square bracket takes the end,
    /// a parenthesis leaves it out -- so a range may begin with a parenthesis.  A loop that told a
    /// range from a list by looking only for a square bracket would read `(0, 3]` as a list.
    /// </summary>
    [TestMethod]
    public void TestEveryWayOfWritingARangeStillCounts()
    {
        (string Range, string Longhand)[] cases =
        [
            ("[0, 3]", "0 1 2 3"),
            ("(0, 3)", "1 2"),
            ("(0, 3]", "1 2 3"),
            ("[0, 3)", "0 1 2")
        ];

        foreach ((string range, string longhand) in cases)
        {
            Canvas counted = Rendered(
                $"for i in {range} {{ sphere {{ scale 0.4  translate [i - 1.5, 0, 0] }} }}\n");
            string written = string.Join("", longhand.Split(' ')
                .Select(at => $"sphere {{ scale 0.4  translate [{at} - 1.5, 0, 0] }}\n"));

            AssertSame(counted, Rendered(written), $"the range {range} counted wrongly");
        }
    }

    /// <summary>
    /// **A list in parentheses opens exactly as a range does**, and is still read as a list.  This is
    /// the case the whole arrangement turns on: `(0, 5]` is a range and `(balls)` is a list, and
    /// which one it is is not known until the comma either arrives or does not -- so the range has to
    /// be tried and quietly abandoned.
    /// </summary>
    [TestMethod]
    public void TestAListInParenthesesIsStillAList()
    {
        Canvas walked = Rendered(
            "b = list(-1.0, 1.0)\n" +
            "for x in (b) { sphere { scale 0.5  translate [x, 0, 0] } }\n");
        Canvas longhand = Rendered(
            "sphere { scale 0.5  translate [-1.0, 0, 0] }\n" +
            "sphere { scale 0.5  translate [ 1.0, 0, 0] }\n");

        AssertSame(walked, longhand, "a list in parentheses should still be walked");
    }

    /// <summary>
    /// A range in square brackets is unambiguous, so a malformed one is complained about as a range
    /// rather than being tried as something else and complained about as that.
    /// </summary>
    [TestMethod]
    public void TestAMalformedRangeIsComplainedAboutAsARange()
    {
        StringAssert.Contains(
            WhatWentWrong("for i in [0 2] { sphere { } }\n"), "Expecting a comma here");
    }

    /// <summary>
    /// A loop written to walk something that is not a list says so, and says what to write instead.
    /// </summary>
    [TestMethod]
    public void TestWalkingSomethingThatIsNotAListSaysSo()
    {
        StringAssert.Contains(
            WhatWentWrong("for ball in 7 { sphere { } }\n"), "this is not one");
    }

    /// <summary>
    /// And a step, which counts a range along, is refused rather than ignored when there is no range
    /// to count.
    /// </summary>
    [TestMethod]
    public void TestAStepIsRefusedWhenWalkingAList()
    {
        StringAssert.Contains(
            WhatWentWrong("for ball in list(1, 2) by 2 { sphere { } }\n"), "has no step to take");
    }

    /// <summary>
    /// Renders a scene body over a fixed camera and light, and hands back the picture.
    /// </summary>
    private Canvas Rendered(string body)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");
        string output = Path.ChangeExtension(path, ".png");

        File.WriteAllText(path,
            "context { no gamma  width 80  height 60 }\n" +
            "camera { location [0, 2, -8]  look at [0, 0, 0]  field of view 60 }\n" +
            "point light { location [-5, 5, -5] }\n" + body);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        Assert.IsNotNull(renderer, "the scene did not parse");

        renderer.Render(new RenderOptions { OutputFileName = output });

        return new ImageFile(output).Load()[0];
    }

    /// <summary>
    /// Parses a scene that should not parse, and hands back what was said about it.
    /// </summary>
    private string WhatWentWrong(string body)
    {
        string path = Path.Combine(_directory, $"bad-{Guid.NewGuid():N}.igl");

        File.WriteAllText(path,
            "camera { location [0, 2, -8]  look at [0, 0, 0] }\n" +
            "point light { location [-5, 5, -5] }\n" + body);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions
            {
                OutputFileName = Path.ChangeExtension(path, ".png")
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

        return captured.ToString();
    }

    private static void AssertSame(Canvas left, Canvas right, string what)
    {
        Assert.AreEqual(left.Width, right.Width, what);
        Assert.AreEqual(left.Height, right.Height, what);

        for (int y = 0; y < left.Height; y++)
        {
            for (int x = 0; x < left.Width; x++)
            {
                Assert.AreEqual(left.GetPixel(x, y).Red, right.GetPixel(x, y).Red, 1e-9,
                    $"{what} (they differ at {x}, {y})");
            }
        }
    }
}
