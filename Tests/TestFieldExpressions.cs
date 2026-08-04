using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover lowering what a scene wrote into a field expression, and compiling that.
/// <para>
/// The compiled function is checked against the term tree it came from, at points chosen at random:
/// the DSL's own evaluator is slow and allocates, which is exactly why a field is not evaluated that
/// way, but it is the same arithmetic and so makes a proper oracle.  Two implementations of one thing
/// are worth having when one checks the other; they are only a liability when a scene can reach both.
/// </para>
/// </summary>
[TestClass]
public class TestFieldExpressions
{
    private static readonly Token Where = new IdToken("here");

    private Variables _variables;

    [TestInitialize]
    public void CreateVariables()
    {
        _variables = new Variables();
    }

    private static Term Number(double value)
    {
        return LiteralTerm.CreateLiteralTerm(new NumberToken(value.ToString("R"), value));
    }

    private static Term Named(string name)
    {
        return new VariableTerm(new IdToken(name));
    }

    private static Term Call(string name, params Term[] arguments)
    {
        return new FunctionCallTerm(new IdToken(name), [..arguments]);
    }

    /// <summary>
    /// Lowers and compiles the given term, then checks the compiled function against the term itself
    /// at a spread of points, including the negative ones and the origin.
    /// </summary>
    private void AssertAgreesWithTheTerm(Term term)
    {
        FieldFunction function = FieldFunction.Compile(term.ToField(_variables));
        Random random = new (20260803);

        for (int index = 0; index < 200; index++)
        {
            double x = index == 0 ? 0 : random.NextDouble() * 8 - 4;
            double y = index == 0 ? 0 : random.NextDouble() * 8 - 4;
            double z = index == 0 ? 0 : random.NextDouble() * 8 - 4;

            _variables.SetValue("x", x);
            _variables.SetValue("y", y);
            _variables.SetValue("z", z);

            double expected = term.GetValue<double>(_variables);
            double actual = function.Evaluate(x, y, z);

            Assert.IsTrue(expected.Near(actual),
                $"at ({x}, {y}, {z}) the term gives {expected} and the compiled field {actual}: {function}");
        }
    }

    /// <summary>
    /// This tests that the arithmetic lowers and compiles to the same answers the term tree gives.
    /// </summary>
    [TestMethod]
    public void TestArithmeticAgreesWithTheTermTree()
    {
        // The sphere every isosurface starts with: x² + y² + z² - 1.
        AssertAgreesWithTheTerm(new BinaryMinusOperation(
            new BinaryPlusOperation(
                new BinaryPlusOperation(
                    new SquareOperation(Named("x")),
                    new SquareOperation(Named("y"))),
                new SquareOperation(Named("z"))),
            Number(1)));

        // Each operation, and a negation, over all three variables.
        AssertAgreesWithTheTerm(new BinaryDivideOperation(
            new BinaryMultiplyOperation(
                new UnaryMinusOperation(Named("x")),
                new BinaryPlusOperation(Named("y"), Number(3))),
            new BinaryMinusOperation(new CubeOperation(Named("z")), Number(100))));
    }

    /// <summary>
    /// This tests that a call lowers to a call, and gives the same answers.
    /// </summary>
    [TestMethod]
    public void TestCallsAgreeWithTheTermTree()
    {
        AssertAgreesWithTheTerm(Call("sqrt", new BinaryPlusOperation(
            new SquareOperation(Named("x")), new SquareOperation(Named("y")))));
        AssertAgreesWithTheTerm(Call("min", Named("x"), Named("y")));
        AssertAgreesWithTheTerm(Call("abs", new BinaryMinusOperation(Named("x"), Named("z"))));
        AssertAgreesWithTheTerm(Call("pow", Call("abs", Named("x")), Number(4)));
        AssertAgreesWithTheTerm(Call("sin", new BinaryMultiplyOperation(Named("x"), Number(3))));
        AssertAgreesWithTheTerm(Call("clamp", Named("x"), Number(-1), Number(1)));
    }

    /// <summary>
    /// This tests that a scene's own variables are folded into the field as the numbers they are,
    /// since a compiled field has nothing left to look a name up in.
    /// </summary>
    [TestMethod]
    public void TestSceneVariablesBecomeConstants()
    {
        _variables.SetValue("radius", 3.0);

        FieldExpression expression = new BinaryMinusOperation(
                new SquareOperation(Named("x")), new SquareOperation(Named("radius")))
            .ToField(_variables);

        Assert.AreEqual("((x * x) - 9)", expression.ToString());

        // Changing the variable afterward cannot reach the field: it was read when the field was
        // built, which is the whole point of building one.
        _variables.SetValue("radius", 10.0);

        Assert.AreEqual(-9, FieldFunction.Compile(expression).Evaluate(0, 0, 0));
    }

    /// <summary>
    /// This tests that the arithmetic of constants is done while the tree is being built, and that the
    /// terms which change nothing are not emitted at all.  A gradient is where this really tells, since
    /// differentiating makes a great deal of nought and one.
    /// </summary>
    [TestMethod]
    public void TestConstantsAreFoldedAndIdentitiesDropped()
    {
        Assert.AreEqual("5", new BinaryPlusOperation(Number(2), Number(3))
            .ToField(_variables).ToString());
        Assert.AreEqual("2", Call("sqrt", Number(4)).ToField(_variables).ToString());
        Assert.AreEqual("x", new BinaryPlusOperation(Named("x"), Number(0))
            .ToField(_variables).ToString());
        Assert.AreEqual("x", new BinaryMultiplyOperation(Named("x"), Number(1))
            .ToField(_variables).ToString());
        Assert.AreEqual("0", new BinaryMultiplyOperation(Named("x"), Number(0))
            .ToField(_variables).ToString());
        Assert.AreEqual("-x", new BinaryMultiplyOperation(Number(-1), Named("x"))
            .ToField(_variables).ToString());

        // Negating twice cancels.
        Assert.AreEqual("x", new UnaryMinusOperation(new UnaryMinusOperation(Named("x")))
            .ToField(_variables).ToString());

        // Nought over something that might itself be nought is not nought, so that one stands.
        Assert.AreEqual("(0 / x)", new BinaryDivideOperation(Number(0), Named("x"))
            .ToField(_variables).ToString());
    }

    /// <summary>
    /// This tests that x, y and z mean the point being asked about even when the scene has variables
    /// of those names, since a field function is a function of them and of nothing else.
    /// </summary>
    [TestMethod]
    public void TestTheThreeVariablesAreNotSceneVariables()
    {
        _variables.SetValue("x", 99.0);

        FieldFunction function = FieldFunction.Compile(Named("x").ToField(_variables));

        Assert.AreEqual(7, function.Evaluate(7, 0, 0));
    }

    /// <summary>
    /// This tests that what cannot mean anything in a field is turned away as the scene is read, with
    /// something the author can act on.
    /// </summary>
    [TestMethod]
    public void TestWhatAFieldCannotHold()
    {
        // A name the scene never gave a number to.
        TokenException exception = Assert.ThrowsExactly<TokenException>(
            () => Named("wobble").ToField(_variables));

        Assert.Contains("'wobble'", exception.Message);
        Assert.Contains("x, y and z", exception.Message);

        // A tuple, and the vector functions that would take one.  Vectors in a field will come, and
        // until they do the message says which forms there are rather than merely refusing.
        Assert.ThrowsExactly<TokenException>(
            () => new TupleTerm(Where, [Number(1), Number(2), Number(3)]).ToField(_variables));

        exception = Assert.ThrowsExactly<TokenException>(
            () => Call("length", Named("x")).ToField(_variables));

        Assert.Contains("length(vector)", exception.Message);

        // Text, and the things that work on it.
        exception = Assert.ThrowsExactly<TokenException>(
            () => LiteralTerm.CreateLiteralTerm(new StringToken("'", "text")).ToField(_variables));

        Assert.Contains("over numbers", exception.Message);

        // A comparison is not arithmetic; conditionals in a field can come later.
        Assert.ThrowsExactly<TokenException>(
            () => new ComparisonOperation(Named("x"), Number(0), Comparison.Less).ToField(_variables));

        // And a misspelled function is still a misspelled function.
        exception = Assert.ThrowsExactly<TokenException>(
            () => Call("wibble", Named("x")).ToField(_variables));

        Assert.Contains("no function named 'wibble'", exception.Message);
    }
}
