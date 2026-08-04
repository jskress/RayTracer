using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover the term tree itself: the values an expression is built out of and what each
/// operation does with them.
/// <para>
/// The other expression tests -- <see cref="TestFunctionCalls"/>, <see cref="TestMathOperators"/>
/// and <see cref="TestConditionalExpressions"/> -- go through the parser and read their answers back
/// out of a rendered image, which is the right way to test that the grammar, the tree builder and
/// the evaluator agree.  These build the terms directly instead, so that what an operation does with
/// a color, a matrix or a missing variable can be asked about on its own, and so that a term that no
/// scene happens to write is still covered.
/// </para>
/// </summary>
[TestClass]
public class TestTerms
{
    private static readonly Token Where = new IdToken("here");

    private Variables _variables;

    [TestInitialize]
    public void CreateVariables()
    {
        _variables = new Variables();
    }

    /// <summary>
    /// Builds a literal term holding the given number.
    /// </summary>
    private static Term Number(double value)
    {
        return LiteralTerm.CreateLiteralTerm(new NumberToken(value.ToString("R"), value));
    }

    /// <summary>
    /// Builds a literal term holding the given text.
    /// </summary>
    private static Term Text(string value)
    {
        return LiteralTerm.CreateLiteralTerm(new StringToken("'", value));
    }

    /// <summary>
    /// Builds a term that reads the named variable.
    /// </summary>
    private static Term Named(string name)
    {
        return new VariableTerm(new IdToken(name));
    }

    /// <summary>
    /// This tests that a literal gives back what its token spelled out, for each sort of token that
    /// can carry a value.
    /// </summary>
    [TestMethod]
    public void TestLiterals()
    {
        Assert.AreEqual(2.5, Number(2.5).GetValue<double>(_variables));
        Assert.AreEqual("text", Text("text").GetValue<string>(_variables));
        Assert.IsTrue(LiteralTerm
            .CreateLiteralTerm(new KeywordToken("true"))
            .GetValue<bool>(_variables));
        Assert.IsFalse(LiteralTerm
            .CreateLiteralTerm(new KeywordToken("false"))
            .GetValue<bool>(_variables));
        Assert.IsNull(LiteralTerm
            .CreateLiteralTerm(new KeywordToken("null"))
            .GetValue<string>(_variables, false));
    }

    /// <summary>
    /// This tests that a variable is read when the term is evaluated rather than when it is built,
    /// which is what lets a scene set a value after writing an expression that uses it, and that a
    /// name holds one value for each type it has been given.
    /// </summary>
    [TestMethod]
    public void TestVariables()
    {
        Term term = Named("size");

        _variables.SetValue("size", 3.0);

        Assert.AreEqual(3.0, term.GetValue<double>(_variables));

        // Read late: the same term, asked again, sees the new value.
        _variables.SetValue("size", 4.0);

        Assert.AreEqual(4.0, term.GetValue<double>(_variables));

        // One value per type under one name, which is how a color and a number may share one.
        _variables.SetValue("size", new Color(1, 0, 0));

        Assert.AreEqual(4.0, term.GetValue<double>(_variables));
        Assert.IsTrue(term.GetValue<Color>(_variables).Matches(new Color(1, 0, 0)));
    }

    /// <summary>
    /// This tests that a tuple becomes whatever sort of thing is asked of it, since that conversion
    /// is what lets one bracketed list of numbers serve as a point, a vector or a color.
    /// </summary>
    [TestMethod]
    public void TestTuplesBecomeWhatIsAskedFor()
    {
        Term term = new TupleTerm(Where, [Number(1), Number(2), Number(3)]);

        Assert.IsTrue(term.GetValue<Vector>(_variables).Matches(new Vector(1, 2, 3)));
        Assert.IsTrue(term.GetValue<Point>(_variables).Matches(new Point(1, 2, 3)));
        Assert.IsTrue(term.GetValue<Color>(_variables).Matches(new Color(1, 2, 3)));

        // A fourth number is the alpha, or W, depending on what it is asked to be.
        term = new TupleTerm(Where, [Number(1), Number(2), Number(3), Number(0.5)]);

        Assert.AreEqual(0.5, term.GetValue<Color>(_variables).Alpha);
    }

    /// <summary>
    /// This tests the arithmetic, which is really a table of what each operation will accept.  The
    /// interesting cases are the ones that are not two numbers.
    /// </summary>
    [TestMethod]
    public void TestArithmeticAcrossTypes()
    {
        Assert.AreEqual(7.0, new BinaryPlusOperation(Number(3), Number(4)).GetValue<double>(_variables));
        Assert.AreEqual(-1.0, new BinaryMinusOperation(Number(3), Number(4)).GetValue<double>(_variables));
        Assert.AreEqual(12.0, new BinaryMultiplyOperation(Number(3), Number(4)).GetValue<double>(_variables));
        Assert.AreEqual(0.75, new BinaryDivideOperation(Number(3), Number(4)).GetValue<double>(_variables));
        Assert.AreEqual(3.0, new BinaryModuloOperation(Number(7), Number(4)).GetValue<double>(_variables));

        // Text joins, and repeats when multiplied.
        Assert.AreEqual("abcdef", new BinaryPlusOperation(Text("abc"), Text("def"))
            .GetValue<string>(_variables));
        Assert.AreEqual("abcabc", new BinaryMultiplyOperation(Text("abc"), Number(2))
            .GetValue<string>(_variables));

        // A vector scales by a number, either way round.
        _variables.SetValue("direction", new Vector(1, 2, 3));

        Assert.IsTrue(new BinaryMultiplyOperation(Named("direction"), Number(2))
            .GetValue<Vector>(_variables)
            .Matches(new Vector(2, 4, 6)));
        Assert.IsTrue(new BinaryMultiplyOperation(Number(2), Named("direction"))
            .GetValue<Vector>(_variables)
            .Matches(new Vector(2, 4, 6)));

        // A matrix moves a point.
        _variables.SetValue("move", Transforms.Translate(1, 2, 3));
        _variables.SetValue("origin", new Point(0, 0, 0));

        Assert.IsTrue(new BinaryMultiplyOperation(Named("move"), Named("origin"))
            .GetValue<Point>(_variables)
            .Matches(new Point(1, 2, 3)));
    }

    /// <summary>
    /// This tests that an operation given types it cannot work with says so, naming both, rather
    /// than failing in some way the author of the scene cannot act on.
    /// </summary>
    [TestMethod]
    public void TestTypeErrorsAreReported()
    {
        _variables.SetValue("direction", new Vector(1, 2, 3));

        TokenException exception = Assert.ThrowsExactly<TokenException>(
            () => new BinaryMultiplyOperation(Named("direction"), Text("abc"))
                .GetValue<object>(_variables));

        Assert.Contains("Cannot multiply", exception.Message);
        Assert.Contains("Vector", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    /// <summary>
    /// This tests the unary operations, including the ones that work on more than numbers.
    /// </summary>
    [TestMethod]
    public void TestUnaryOperations()
    {
        Assert.AreEqual(-3.0, new UnaryMinusOperation(Number(3)).GetValue<double>(_variables));
        Assert.AreEqual(9.0, new SquareOperation(Number(3)).GetValue<double>(_variables));
        Assert.AreEqual(27.0, new CubeOperation(Number(3)).GetValue<double>(_variables));

        // Squaring a color multiplies it by itself, channel by channel.
        _variables.SetValue("tint", new Color(0.5, 0.4, 0.2));

        Color squared = new SquareOperation(Named("tint")).GetValue<Color>(_variables);

        Assert.IsTrue(0.25.Near(squared.Red));
        Assert.IsTrue(0.16.Near(squared.Green));

        // The casts turn one sort of tuple into another.
        Term tuple = new TupleTerm(Where, [Number(1), Number(2), Number(3)]);

        Assert.IsTrue(new UnaryCastOperation<Vector>(tuple)
            .GetValue<Vector>(_variables)
            .Matches(new Vector(1, 2, 3)));
        Assert.IsTrue(new UnaryCastOperation<Point>(tuple)
            .GetValue<Point>(_variables)
            .Matches(new Point(1, 2, 3)));
    }

    /// <summary>
    /// This tests that a string may have values dropped into it, which is how a scene's title says
    /// what the scene was made with.
    /// </summary>
    [TestMethod]
    public void TestStringSubstitution()
    {
        _variables.SetValue("count", 7.0);
        _variables.SetValue("what", "spheres");

        Assert.AreEqual("7", new StringSubstitutionOperation(Text("${count}"))
            .GetValue<string>(_variables));

        // More than one in a string, which a greedy match used to run together into a single name
        // that matched no variable, leaving both untouched.
        Assert.AreEqual("7 spheres", new StringSubstitutionOperation(Text("${count} ${what}"))
            .GetValue<string>(_variables));

        // A name that is not a variable is left exactly as it was written.
        Assert.AreEqual("${nothing}", new StringSubstitutionOperation(Text("${nothing}"))
            .GetValue<string>(_variables));
    }

    /// <summary>
    /// This tests the conversions a value goes through on its way out of a term, since those are
    /// what let one written value satisfy a great many different clauses.
    /// </summary>
    [TestMethod]
    public void TestValuesAreConvertedOnTheWayOut()
    {
        // A number where a whole one is wanted.  Note that it rounds rather than truncating.
        Assert.AreEqual(4, Number(3.7).GetValue<int>(_variables));
        Assert.AreEqual(3, Number(3.2).GetValue<int>(_variables));

        // A named color from its name.
        Assert.IsTrue(Text("Red").GetValue<Color>(_variables).Matches(Colors.Red));

        // Anything at all, where text is wanted.
        Assert.AreEqual("3", Number(3).GetValue<string>(_variables));

        // A value that cannot be converted says which types were wanted.
        TokenException exception = Assert.ThrowsExactly<TokenException>(
            () => Text("not a color at all").GetValue<Vector>(_variables));

        Assert.Contains("Vector", exception.Message);
    }
}
