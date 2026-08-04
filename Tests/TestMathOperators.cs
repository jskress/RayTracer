using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the mathematical symbols an expression may be written with: the roots, the
/// powers, the products, the angle units, and the several code points each of those arrives as.
/// <para>
/// As with <see cref="TestFunctionCalls"/>, the value of an expression is read back through the
/// width of the rendered image, so each case goes through the real parser.  Precedence is what most
/// of these are really about, and precedence is only observable in the answer.
/// </para>
/// </summary>
[TestClass]
public class TestMathOperators
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"operator-tests-{Guid.NewGuid():N}");

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
    /// This tests the root operators, which are sugar for the functions of the same name.
    /// </summary>
    [TestMethod]
    public void TestRoots()
    {
        Assert.AreEqual(80, WidthFrom("√6400"));
        Assert.AreEqual(80, WidthFrom("∛512000"));
        Assert.AreEqual(80, WidthFrom("√(1600 * 4)"));

        // A root takes only what follows it, so this is (√4) * 40 rather than √160.
        Assert.AreEqual(80, WidthFrom("√4 * 40"));

        // Being the sqrt function underneath, a root reports a bad operand the way a call does.
        Assert.Contains("sqrt(number)", ErrorFrom("√[1, 2, 3]"));
    }

    /// <summary>
    /// This tests that a superscript raises what precedes it, and that a power binds tighter than
    /// anything in front of it -- so a minus sign applies to the power, not to its base.  That is
    /// what both mean in print, and it is the reverse of what this DSL used to do.
    /// </summary>
    [TestMethod]
    public void TestPowers()
    {
        Assert.AreEqual(80, WidthFrom("2⁴ * 5"));
        Assert.AreEqual(80, WidthFrom("3⁴ - 1"));
        Assert.AreEqual(80, WidthFrom("2⁰ + 79"));
        Assert.AreEqual(80, WidthFrom("80¹"));

        // -3² is -(3²), so this is 89 - 9 rather than 89 + 9.
        Assert.AreEqual(80, WidthFrom("89 - 3²"));
        Assert.AreEqual(80, WidthFrom("89 + -3²"));
        Assert.AreEqual(80, WidthFrom("97 + -3⁴ + 8²"));
    }

    /// <summary>
    /// This tests that a root reaches over a power written on its operand, so <c>√x³</c> is
    /// <c>√(x³)</c>.
    /// <para>
    /// Arithmetic cannot show this, since the two readings are the same number for every operand --
    /// they differ only in the last bits of the division, and every one of those differences rounds
    /// away.  What does show it is handing the pair something only one of them will accept: a tuple
    /// can be neither squared nor rooted, so whichever operation complains is the one that went
    /// first.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestARootReachesOverAPower()
    {
        string error = ErrorFrom("√[1, 2, 3]²");

        Assert.IsNotNull(error);
        Assert.Contains("Cannot square", error);
        Assert.DoesNotContain("sqrt", error);

        // And the other way about, to show the first message was not simply the only one available:
        // with no power in the way, it is the root that turns the tuple down.
        error = ErrorFrom("√[1, 2, 3]");

        Assert.IsNotNull(error);
        Assert.Contains("sqrt(number)", error);
    }

    /// <summary>
    /// This tests that two powers cannot be stacked.  Each superscript is its own operator, so
    /// <c>x¹⁰</c> would otherwise quietly read as <c>x</c> to the first and then to the zeroth, which
    /// is 1 -- a wrong answer with nothing to show it was wrong.
    /// </summary>
    [TestMethod]
    public void TestAPowerCannotBeStacked()
    {
        string error = ErrorFrom("2¹⁰ * 78");

        Assert.IsNotNull(error);
        Assert.Contains("pow(value, exponent)", error);

        Assert.IsNotNull(ErrorFrom("2²³"));
        Assert.IsNotNull(ErrorFrom("2⁵²"));

        // Which is what the function is for.
        Assert.AreEqual(80, WidthFrom("pow(2, 10) - 944"));

        // What is refused is the two superscripts standing side by side, since only a multi-digit
        // power is written that way.  A power of a power, said out loud with parentheses, is fine.
        Assert.AreEqual(80, WidthFrom("(3²)³ - 649"));
        Assert.AreEqual(80, WidthFrom("(2⁴)² - 176"));
        Assert.AreEqual(80, WidthFrom("(2⁴)⁴ - 65456"));
    }

    /// <summary>
    /// This tests that the product symbols mean a vector product when given two vectors and plain
    /// multiplication otherwise, since printed mathematics uses both for scalars far more often than
    /// for either vector product.
    /// </summary>
    [TestMethod]
    public void TestProducts()
    {
        // Two vectors: the dot and cross products.
        Assert.AreEqual(80, WidthFrom("[1, 2, 3] ⋅ [4, 5, 6] + 48"));
        Assert.AreEqual(80, WidthFrom("length([1, 0, 0] × [0, 1, 0]) * 80"));

        // Anything else: multiplication, including a vector by a number.
        Assert.AreEqual(80, WidthFrom("20 ⋅ 4"));
        Assert.AreEqual(80, WidthFrom("20 × 4"));
        Assert.AreEqual(80, WidthFrom("length(vector [1, 0, 0] × 80)"));

        // And they carry multiplication's precedence, so this is 8 + (9 × 8).
        Assert.AreEqual(80, WidthFrom("8 + 9 × 8"));
        Assert.AreEqual(80, WidthFrom("8 + 9 ⋅ 8"));
        Assert.AreEqual(80, WidthFrom("160 ÷ 2"));
        Assert.AreEqual(80, WidthFrom("60 + 40 ÷ 2"));
    }

    /// <summary>
    /// This tests that one operation written as any of the code points it arrives as means the same
    /// thing, which is what lets a formula be pasted in rather than retyped.
    /// </summary>
    [TestMethod]
    public void TestTheSpellingsOfOneOperation()
    {
        // Dot: the dot operator, a middle dot, a bullet operator and a bullet.
        foreach (string dot in new[] { "⋅", "·", "∙", "•" })
        {
            Assert.AreEqual(80, WidthFrom($"20 {dot} 4"));
            Assert.AreEqual(80, WidthFrom($"[1, 2, 3] {dot} [4, 5, 6] + 48"));
        }

        // Times, both as a cross product and as multiplication.
        foreach (string times in new[] { "×", "⨯" })
            Assert.AreEqual(80, WidthFrom($"20 {times} 4"));

        // Divide: the division sign, a division slash and a fraction slash.
        foreach (string divide in new[] { "÷", "∕", "⁄" })
            Assert.AreEqual(80, WidthFrom($"160 {divide} 2"));

        // Multiply: the asterisk and star operators.
        foreach (string times in new[] { "∗", "⋆" })
            Assert.AreEqual(80, WidthFrom($"20 {times} 4"));

        // Minus, both as subtraction and as a sign, spelled with a real minus and an en dash.
        foreach (string minus in new[] { "−", "–" })
        {
            Assert.AreEqual(80, WidthFrom($"100 {minus} 20"));
            Assert.AreEqual(80, WidthFrom($"100 + {minus}20"));
        }
    }

    /// <summary>
    /// This tests the postfix operators that say what unit an angle was written in.
    /// </summary>
    [TestMethod]
    public void TestAngles()
    {
        Assert.AreEqual(80, WidthFrom("sin(90°) * 80"));
        Assert.AreEqual(80, WidthFrom("sin(90 degrees) * 80"));
        Assert.AreEqual(80, WidthFrom("cos(0 radians) * 80"));
        Assert.AreEqual(80, WidthFrom("toDegrees(180°) - 100"));

        // An angle binds as tightly as a power, so this is 2 * (45°) rather than (2 * 45)°.
        Assert.AreEqual(80, WidthFrom("sin(2 * 45°) * 80"));

        // And it insists on a number.
        Assert.Contains("must be a number", ErrorFrom("[1, 2, 3]°"));
    }
}
