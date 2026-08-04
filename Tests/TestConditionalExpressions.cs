using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the comparisons, the logical operators and the conditional -- everything an
/// expression needs to choose between two values rather than merely work one out.
/// <para>
/// As in <see cref="TestFunctionCalls"/> and <see cref="TestMathOperators"/>, each expression is put
/// through the real parser and its value read back from the width of the rendered image.
/// </para>
/// </summary>
[TestClass]
public class TestConditionalExpressions
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"conditional-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private int WidthFrom(string expression)
    {
        (int width, string error) = RenderWidth(expression);

        Assert.IsNull(error, $"{expression}: {error}");

        return width;
    }

    private string ErrorFrom(string expression)
    {
        return RenderWidth(expression).Error;
    }

    private (int Width, string Error) RenderWidth(string expression)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            $"context {{ no gamma  width {expression}  height 30 }}\n" +
            "camera { location [0, 1.5, -5]  look at [0, 1, 0] }\n" +
            "point light { location [-10, 10, -10] }\n" +
            "sphere { translate [0, 1, 0] }");

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

    /// <summary>
    /// This tests the conditional, which is the only operator that chooses between two values.
    /// </summary>
    [TestMethod]
    public void TestTheConditional()
    {
        Assert.AreEqual(80, WidthFrom("true ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("false ? 40 : 80"));
        Assert.AreEqual(80, WidthFrom("3 > 2 ? 80 : 40"));

        // The two sides are expressions in their own right, and conditionals nest.
        Assert.AreEqual(80, WidthFrom("2 > 3 ? 40 : 10 * 8"));
        Assert.AreEqual(80, WidthFrom("1 > 2 ? 20 : 2 > 3 ? 40 : 80"));

        // A condition has to be a decision, not a number.
        Assert.Contains("must be true or false", ErrorFrom("5 ? 80 : 40"));
    }

    /// <summary>
    /// This tests that only the chosen side of a conditional is evaluated, which is what makes the
    /// test worth putting there: the side not taken may be one that could not be evaluated at all.
    /// </summary>
    [TestMethod]
    public void TestOnlyTheChosenSideIsEvaluated()
    {
        // Squaring a tuple fails outright, so this can only come out at 80 if the failing side was
        // never looked at.
        Assert.AreEqual(80, WidthFrom("true ? 80 : [1, 2, 3]²"));
        Assert.AreEqual(80, WidthFrom("false ? [1, 2, 3]² : 80"));

        // And to show that side really would have failed, here it is taken.
        Assert.IsNotNull(ErrorFrom("true ? [1, 2, 3]² : 80"));
    }

    /// <summary>
    /// This tests the six comparisons, over numbers and over text.
    /// </summary>
    [TestMethod]
    public void TestComparisons()
    {
        Assert.AreEqual(80, WidthFrom("2 < 3 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("3 <= 3 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("3 > 2 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("3 >= 3 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("3 == 3 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("3 != 4 ? 80 : 40"));

        // Text compares too, in the order it would sort in.
        Assert.AreEqual(80, WidthFrom("'abc' == 'abc' ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("'abc' < 'abd' ? 80 : 40"));

        // Anything else may be asked whether it is equal, since there is no order to put two
        // vectors in -- and a comparison of two of them says so rather than guessing.
        Assert.AreEqual(80, WidthFrom("[1, 2, 3] == [1, 2, 3] ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("[1, 2, 3] != [1, 2, 4] ? 80 : 40"));
        Assert.Contains("Cannot order", ErrorFrom("[1, 2, 3] < [1, 2, 4] ? 80 : 40"));

        // Two numbers are equal when they are near enough, as everything else here treats them.
        Assert.AreEqual(80, WidthFrom("0.1 + 0.2 == 0.3 ? 80 : 40"));
    }

    /// <summary>
    /// This tests the logical operators, in both the symbolic spellings a developer will reach for
    /// and the mathematical ones a formula may be pasted with.
    /// </summary>
    [TestMethod]
    public void TestLogicalOperators()
    {
        Assert.AreEqual(80, WidthFrom("2 < 3 && 4 < 5 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("2 > 3 || 4 < 5 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("!(2 > 3) ? 80 : 40"));

        Assert.AreEqual(80, WidthFrom("2 < 3 ∧ 4 < 5 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("2 > 3 ∨ 4 < 5 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("¬(2 > 3) ? 80 : 40"));

        // "and" binds tighter than "or", as it does in C#, so this reads as true || (false && false)
        // and comes out true.  Level with each other, it would read as (true || false) && false.
        Assert.AreEqual(80, WidthFrom("true || false && false ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("true ∨ false ∧ false ? 80 : 40"));

        // A comparison binds tighter than either, so no parentheses are needed around one.
        Assert.AreEqual(80, WidthFrom("1 < 2 && 3 < 4 && 5 < 6 ? 80 : 40"));

        // And they insist on being given decisions.
        Assert.Contains("needs true or false", ErrorFrom("1 && 2 ? 80 : 40"));
        Assert.Contains("Only true or false can be negated", ErrorFrom("!5 ? 80 : 40"));
    }

    /// <summary>
    /// This tests that each logical operation may be written as a word or as a symbol, and that the
    /// two are the same operator rather than two that merely behave alike -- so the words carry the
    /// same precedence and stop just as early.
    /// </summary>
    [TestMethod]
    public void TestTheWordsAndTheSymbolsAreOneOperator()
    {
        Assert.AreEqual(80, WidthFrom("2 < 3 and 4 < 5 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("2 > 3 or 4 < 5 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("not (2 > 3) ? 80 : 40"));

        // Mixed spellings of one expression, since they are one operator.
        Assert.AreEqual(80, WidthFrom("2 < 3 and 4 < 5 && 6 < 7 ? 80 : 40"));
        Assert.AreEqual(80, WidthFrom("not (2 > 3) ∧ true ? 80 : 40"));

        // "and" over "or", exactly as with the symbols.
        Assert.AreEqual(80, WidthFrom("true or false and false ? 80 : 40"));

        // Stopping early, exactly as with the symbols.
        Assert.AreEqual(80, WidthFrom("false and [1, 2, 3]² > 0 ? 40 : 80"));
        Assert.AreEqual(80, WidthFrom("true or [1, 2, 3]² > 0 ? 80 : 40"));

        // And the same complaint when handed something that is not a decision.
        Assert.Contains("needs true or false", ErrorFrom("1 and 2 ? 80 : 40"));
        Assert.Contains("Only true or false can be negated", ErrorFrom("not 5 ? 80 : 40"));
    }

    /// <summary>
    /// This tests that "and" still does its other job.  It was a keyword before it was an operator --
    /// an L-system writes <c>ignore commands and '…'</c> -- and a word that means two things in one
    /// language is worth a test that says so.
    /// </summary>
    [TestMethod]
    public void TestAndIsStillAnLSystemKeyword()
    {
        string path = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(path,
            """
            context { no gamma  width 80  height 30 }
            camera { location [0, 1.5, -5]  look at [0, 1, 0] }
            point light { location [-10, 10, -10] }
            lsystem {
                axiom 'F'
                productions { 'F' -> 'F+F' }
                generations 2
                ignore commands and '+-'
                controls { angle 25 }
            }
            """);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            Assert.IsNotNull(renderer, $"the scene did not parse: {captured}");
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// This tests that the right side of a logical operator is left alone when the left side has
    /// already settled the answer, so that a test may guard what is written beside it.
    /// </summary>
    [TestMethod]
    public void TestLogicalOperatorsStopEarly()
    {
        // Squaring a tuple fails, so each of these comes out at 80 only if the right side went
        // unevaluated once the left side had decided matters.
        Assert.AreEqual(80, WidthFrom("false && [1, 2, 3]² > 0 ? 40 : 80"));
        Assert.AreEqual(80, WidthFrom("true || [1, 2, 3]² > 0 ? 80 : 40"));

        // With the left side leaving it open, the right side is looked at and does fail.
        Assert.IsNotNull(ErrorFrom("true && [1, 2, 3]² > 0 ? 80 : 40"));
        Assert.IsNotNull(ErrorFrom("false || [1, 2, 3]² > 0 ? 80 : 40"));
    }
}
