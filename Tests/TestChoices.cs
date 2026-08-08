using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover a choice inside a function or a primitive: an <c>if</c> that picks which of two
/// answers the body gives back.
/// <para>
/// A choice here is deliberately not a statement.  It always ends the body it appears in, and both
/// ways out have to give an answer.  That makes "exactly one answer, on every path" a matter of the
/// grammar rather than of an analysis, and it means a name worked out inside one arm cannot be seen
/// outside it, there being no "after" for it to be seen in.  Most of what is worth testing is that
/// shape holding: that a body may branch, that a branch may work things out of its own, that only the
/// side taken is carried out, and that the ways of writing it wrongly are refused while it is being
/// read.
/// </para>
/// </summary>
[TestClass]
public class TestChoices
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"choice-tests-{Guid.NewGuid():N}");

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
                OutputFileName = output, Width = 40, Height = 30
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

    /// <summary>
    /// This finds how wide the picture came out, which is how a function's answer is read back: the
    /// number it gives is what the width was set from.
    /// </summary>
    private (int Width, string Error) WidthFrom(string scene)
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
                return (0, captured.ToString());

            renderer.Render(new RenderOptions { OutputFileName = output });

            string text = captured.ToString();

            return text.Contains("Error")
                ? (0, text)
                : (new ImageFile(output).Load()[0].Width, null);
        }
        catch (Exception exception)
        {
            return (0, exception.ToString());
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    private int Width(string scene)
    {
        (int width, string error) = WidthFrom(scene);

        Assert.IsNull(error, error);

        return width;
    }

    [TestMethod]
    public void TestAFunctionMayChooseItsAnswer()
    {
        Assert.AreEqual(80, Width("""
            function pick(n) -> number {
                if (n > 2) { return 80 } else { return 40 }
            }
            context { width pick(3)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """));

        Assert.AreEqual(40, Width("""
            function pick(n) -> number {
                if (n > 2) { return 80 } else { return 40 }
            }
            context { width pick(1)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """));
    }

    [TestMethod]
    public void TestAnArmMayWorkThingsOutOfItsOwn()
    {
        // This is the thing a switch of the C# sort cannot do and the reason for taking the trouble:
        // an arm is a body in its own right, so what it needs it may work out where it is used rather
        // than having to be lifted out into a function of its own.
        Assert.AreEqual(80, Width("""
            function pick(n) -> number {
                if (n > 2) {
                    doubled = n * 2
                    over = doubled + 2
                    return over * 10
                }
                else { return 40 }
            }
            context { width pick(3)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """));
    }

    [TestMethod]
    public void TestCasesMayBeChained()
    {
        // "else if" rather than an else holding an if.  The two mean the same thing, and the point of
        // having it is entirely that a run of cases reads down the page instead of marching off the
        // right of it.
        const string scene = """
            function band(n) -> number {
                if (n < 1) { return 20 }
                else if (n < 2) { over = n * 10  return over + 30 }
                else if (n < 3) { return 60 }
                else { return 80 }
            }
            context { width band(WHICH)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """;

        Assert.AreEqual(20, Width(scene.Replace("WHICH", "0.5")));
        Assert.AreEqual(60, Width(scene.Replace("WHICH", "2.5")));
        Assert.AreEqual(80, Width(scene.Replace("WHICH", "9")));

        // The middle arm works something out of its own, which is the thing that says a chained arm is
        // a whole body and not some lesser construct.
        Assert.AreEqual(45, Width(scene.Replace("WHICH", "1.5")));
    }

    [TestMethod]
    public void TestAPrimitiveMayChainCasesToo()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive marker(size) -> sphere {
                if (size > 2) { return sphere { material { pigment Red }  scale size } }
                else if (size > 1) { return sphere { material { pigment Green }  scale size } }
                else { return sphere { material { pigment Blue }  scale size } }
            }
            object marker(2.4) { translate X -3.5 }
            object marker(1.4) { }
            object marker(0.6) { translate X 3.5 }
            """);

        Assert.IsNull(error, error);

        bool red = false;
        bool green = false;
        bool blue = false;

        for (int x = 0; x < 40; x++)
        {
            for (int y = 0; y < 30; y++)
            {
                Color pixel = image.GetPixel(x, y);

                red |= pixel.Red > 0.25 && pixel.Green < 0.15 && pixel.Blue < 0.15;
                green |= pixel.Green > 0.25 && pixel.Red < 0.15 && pixel.Blue < 0.15;
                blue |= pixel.Blue > 0.25 && pixel.Red < 0.15 && pixel.Green < 0.15;
            }
        }

        Assert.IsTrue(red && green && blue,
            $"all three arms should have been taken, and got red {red}, green {green}, blue {blue}");
    }

    [TestMethod]
    public void TestAChainStillNeedsItsLastWayOut()
    {
        // Chaining changes nothing about the rule: the last "else" is what makes every path answer, so
        // a chain that trails off is refused exactly as a lone choice is.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function band(n) -> number {
                if (n < 1) { return 20 }
                else if (n < 2) { return 40 }
            }
            sphere { scale band(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("else"), $"the complaint should ask for it: {error}");
    }

    [TestMethod]
    public void TestElseWantsABraceOrAnotherIf()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function band(n) -> number {
                if (n < 1) { return 20 }
                else return 40
            }
            sphere { scale band(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("open brace") && error.Contains("if"),
            $"the complaint should offer both: {error}");
    }

    [TestMethod]
    public void TestChoicesNest()
    {
        // An arm ends in a choice of its own as readily as in an answer.  This is the long way of
        // writing what the chain above writes flatly, and it has to keep working: the chain is only
        // sugar for it, and the tree that comes out is the same.
        const string scene = """
            function band(n) -> number {
                if (n < 1) { return 20 }
                else {
                    if (n < 2) { return 40 }
                    else { return 80 }
                }
            }
            context { width band(WHICH)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """;

        Assert.AreEqual(20, Width(scene.Replace("WHICH", "0.5")));
        Assert.AreEqual(40, Width(scene.Replace("WHICH", "1.5")));
        Assert.AreEqual(80, Width(scene.Replace("WHICH", "9")));
    }

    [TestMethod]
    public void TestOnlyTheArmTakenIsCarriedOut()
    {
        // The side not taken may be one that could not be worked out at all, and that has to cost
        // nothing -- otherwise a choice could not be used to keep a body away from a case it has no
        // answer for, which is most of what anybody wants one for.
        Assert.AreEqual(80, Width("""
            function pick(n) -> number {
                if (n > 2) { return 80 }
                else {
                    doomed = [1, 2, 3]²
                    return doomed
                }
            }
            context { width pick(3)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """));

        // And to show that side really would have failed, here it is taken.
        Assert.IsNotNull(WidthFrom("""
            function pick(n) -> number {
                if (n > 2) { return 80 }
                else {
                    doomed = [1, 2, 3]²
                    return doomed
                }
            }
            context { width pick(1)  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            sphere { translate [0, 1, 0] }
            """).Error);
    }

    [TestMethod]
    public void TestWhatAnArmWorksOutBelongsToThatArm()
    {
        // A name set in one arm is not a name the other arm has, and neither is it one the body that
        // held the choice has.  There is no "after the choice" for it to leak into, which is half the
        // point of the choice ending the body.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function pick(n) -> number {
                if (n > 2) { mine = 5  return mine }
                else { return mine }
            }
            sphere { scale pick(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("mine"), $"the complaint should name it: {error}");
    }

    [TestMethod]
    public void TestAPrimitiveMayChooseWhatItMakes()
    {
        // The same shape on the other twin, and the more useful of the two: one name that stands for
        // a family of things, picking among them by what it was told.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive marker(size) -> sphere {
                if (size > 1) {
                    big = size * 1.2
                    return sphere { material { pigment Red }  scale big }
                }
                else { return sphere { material { pigment Blue }  scale size } }
            }
            object marker(2) { translate X -2.5 }
            object marker(0.6) { translate X 2.5 }
            """);

        Assert.IsNull(error, error);

        bool red = false;
        bool blue = false;

        for (int x = 0; x < 40; x++)
        {
            for (int y = 0; y < 30; y++)
            {
                Color pixel = image.GetPixel(x, y);

                red |= pixel.Red > 0.25 && pixel.Blue < 0.15 && x < 20;
                blue |= pixel.Blue > 0.25 && pixel.Red < 0.15 && x > 20;
            }
        }

        Assert.IsTrue(red, "the larger call should have taken the first arm");
        Assert.IsTrue(blue, "the smaller call should have taken the second");
    }

    [TestMethod]
    public void TestWhatACallAddsStillBelongsToTheCall()
    {
        // Whichever arm the body took, the block on the call is laid over the answer afterward and in
        // the names the call was written among -- so a choice inside changes nothing about that.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive marker(size) -> sphere {
                if (size > 1) { return sphere { material { pigment Red } } }
                else { return sphere { material { pigment Red } } }
            }
            step = 3
            object marker(2) { translate X -step }
            object marker(0.5) { translate X step }
            """);

        Assert.IsNull(error, error);

        HashSet<int> bands = [];

        for (int x = 0; x < 40; x++)
        {
            for (int y = 0; y < 30; y++)
            {
                if (image.GetPixel(x, y).Red > 0.25)
                    bands.Add(x / 8);
            }
        }

        Assert.IsTrue(bands.Count >= 2,
            $"the two calls should stand apart, and covered {bands.Count} bands");
    }

    [TestMethod]
    public void TestBothArmsAreReadAsTheKindThatWasPromised()
    {
        // The kind is checked in each arm rather than once for the body, which is the only way a
        // choice and a promised kind can live together.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive marker(size) -> sphere {
                if (size > 1) { return sphere { material { pigment Red } } }
                else { return cube { material { pigment Red } } }
            }
            object marker(2)
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("sphere"), $"the complaint should say what was promised: {error}");
    }

    [TestMethod]
    public void TestAChoiceMustHaveBothWaysOut()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function pick(n) -> number {
                if (n > 2) { return 80 }
                return 40
            }
            sphere { scale pick(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("else"), $"the complaint should ask for it: {error}");
    }

    [TestMethod]
    public void TestNothingMayFollowAChoice()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function pick(n) -> number {
                if (n > 2) { return 80 } else { return 40 }
                extra = 3
            }
            sphere { scale pick(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("close brace"), $"the complaint should say why: {error}");
    }

    [TestMethod]
    public void TestAnArmMustGiveAnAnswer()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function pick(n) -> number {
                if (n > 2) { return 80 } else { m = 2 }
            }
            sphere { scale pick(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("return"), $"the complaint should ask for one: {error}");
    }

    [TestMethod]
    public void TestAConditionMustBeADecision()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function pick(n) -> number {
                if (n) { return 80 } else { return 40 }
            }
            sphere { scale pick(1) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("true or false"), $"the complaint should say so: {error}");
    }

    [TestMethod]
    public void TestAFunctionThatChoosesCannotBeUsedAsAField()
    {
        // A field is compiled down and, for an isosurface, differentiated, and neither can be done to
        // something that picks its expression while the picture is being drawn.  The conditional is
        // the way to say the same thing there, since it is one expression.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function shape(n) -> number {
                if (n > 2) { return n } else { return 0 - n }
            }
            isosurface {
                function { shape(x) + y² + z² - 1 }
                bounded by [-2, -2, -2], [2, 2, 2]
            }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("single expression"), $"the complaint should say why: {error}");
    }

    [TestMethod]
    public void TestAFieldCannotYetTakeAConditionalEither()
    {
        // This records where the boundary actually is, and it is not where one would want it.  The
        // conditional is one expression, so it *ought* to be the way to say in a field what a choice
        // says in a body -- but the field's own small language holds arithmetic on numbers and nothing
        // else: no comparisons, no true or false, and so nothing to choose with.  Until it has those,
        // a field that varies by a condition has to be written as arithmetic that happens to do the
        // same thing.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            function shape(n) -> number { return n > 0 ? n : 0 - n }
            isosurface {
                function { shape(x) + y² + z² - 1 }
                bounded by [-2, -2, -2], [2, 2, 2]
                material { pigment Red }
            }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("only arithmetic on numbers"),
            $"the complaint should say what a field may hold: {error}");
    }
}
