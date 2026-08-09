using Lex.Parser;
using Lex.Tokens;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover differentiating a field function.
/// <para>
/// Every rule is checked against the thing it is an alternative to: the slope measured by moving a
/// little either way and taking the difference.  Finite differences are the wrong way to get a normal
/// -- there is a step size to pick, too large blurs an edge and too small drowns in rounding -- but
/// they are an excellent way to find out whether a rule written out by hand has a sign the wrong way
/// round, which is exactly the mistake forty rules invite and the one nothing else would catch.
/// </para>
/// </summary>
[TestClass]
public class TestFieldGradients
{
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
    /// Differentiates and compiles the given term, then checks each of its three slopes against the
    /// slope measured by taking the difference of the function either side of the point.  Points are
    /// drawn from the given range, which each case chooses to stay clear of the places its own
    /// function has no slope at -- a root at nought, a tangent at a right angle, the crease down the
    /// middle of an absolute value.
    /// </summary>
    private void AssertSlopesMatchDifferences(Term term, double low = -3, double high = 3)
    {
        FieldExpression expression = term.ToField(_variables);
        FieldFunction function = FieldFunction.Compile(expression);
        FieldGradient gradient = FieldGradient.Of(expression);
        Random random = new (20260804);
        const double step = 1e-6;

        for (int index = 0; index < 100; index++)
        {
            double x = low + random.NextDouble() * (high - low);
            double y = low + random.NextDouble() * (high - low);
            double z = low + random.NextDouble() * (high - low);

            foreach (FieldAxis axis in Enum.GetValues<FieldAxis>())
            {
                double measured = (
                    function.Evaluate(
                        x + (axis == FieldAxis.X ? step : 0),
                        y + (axis == FieldAxis.Y ? step : 0),
                        z + (axis == FieldAxis.Z ? step : 0)) -
                    function.Evaluate(
                        x - (axis == FieldAxis.X ? step : 0),
                        y - (axis == FieldAxis.Y ? step : 0),
                        z - (axis == FieldAxis.Z ? step : 0))) / (2 * step);
                double exact = gradient.Along(axis, x, y, z);
                double tolerance = 1e-4 * Math.Max(1, Math.Abs(measured));

                Assert.IsTrue(Math.Abs(measured - exact) < tolerance,
                    $"d/d{axis} of {expression} at ({x:F4}, {y:F4}, {z:F4}): " +
                    $"the rule says {exact}, the difference measures {measured}");
            }
        }
    }

    /// <summary>
    /// This tests the slopes of the arithmetic, including the product and quotient rules.
    /// </summary>
    [TestMethod]
    public void TestTheSlopesOfArithmetic()
    {
        // The sphere: its gradient is the well-known (2x, 2y, 2z).
        AssertSlopesMatchDifferences(new BinaryMinusOperation(
            new BinaryPlusOperation(
                new BinaryPlusOperation(new SquareOperation(Named("x")), new SquareOperation(Named("y"))),
                new SquareOperation(Named("z"))),
            Number(1)));

        // A product and a quotient of things that all vary, so no rule can hide behind a constant.
        AssertSlopesMatchDifferences(new BinaryMultiplyOperation(
            new BinaryPlusOperation(Named("x"), Named("y")),
            new BinaryMinusOperation(Named("z"), Named("x"))));
        AssertSlopesMatchDifferences(new BinaryDivideOperation(
            new BinaryMultiplyOperation(Named("x"), Named("y")),
            new BinaryPlusOperation(new SquareOperation(Named("z")), Number(4))));
        AssertSlopesMatchDifferences(new UnaryMinusOperation(new CubeOperation(Named("y"))));
    }

    /// <summary>
    /// This tests the slope of each function that has a rule, over a range where that function has one.
    /// </summary>
    [TestMethod]
    public void TestTheSlopeOfEveryFunctionWithARule()
    {
        // Well away from the root at nought.
        AssertSlopesMatchDifferences(Call("sqrt", new BinaryPlusOperation(
            new SquareOperation(Named("x")), Number(5))), 1, 3);
        AssertSlopesMatchDifferences(Call("cbrt", new BinaryPlusOperation(Named("x"), Number(8))), 1, 3);
        AssertSlopesMatchDifferences(Call("pow", Call("abs", Named("x")), Number(3)), 1, 3);
        AssertSlopesMatchDifferences(Call("exp", new BinaryMultiplyOperation(Named("x"), Number(0.5))));
        AssertSlopesMatchDifferences(Call("log", new BinaryPlusOperation(Named("x"), Number(10))), 1, 3);
        AssertSlopesMatchDifferences(Call("log10", new BinaryPlusOperation(Named("y"), Number(10))), 1, 3);

        // Away from the crease at nought, where these have no slope to check.
        AssertSlopesMatchDifferences(Call("abs", Named("x")), 1, 3);
        AssertSlopesMatchDifferences(Call("mod", Named("x"), Number(10)), 1, 3);
        AssertSlopesMatchDifferences(Call("min", Named("x"), Named("y")), 1, 3);
        AssertSlopesMatchDifferences(Call("max", Named("x"), new BinaryMultiplyOperation(Named("y"), Number(2))), 1, 3);
        AssertSlopesMatchDifferences(Call("clamp", Named("x"), Number(-1), Number(1)), 1.5, 3);
        AssertSlopesMatchDifferences(Call("lerp", Named("x"), Named("y"), Named("z")));

        // The steps, whose slope is nought wherever they have one at all.
        AssertSlopesMatchDifferences(Call("floor", Named("x")), 1.1, 1.9);
        AssertSlopesMatchDifferences(Call("sign", Named("x")), 1, 3);

        AssertSlopesMatchDifferences(Call("sin", Named("x")));
        AssertSlopesMatchDifferences(Call("cos", new BinaryMultiplyOperation(Named("y"), Number(2))));
        AssertSlopesMatchDifferences(Call("tan", Named("x")), -1, 1);
        AssertSlopesMatchDifferences(Call("asin", new BinaryMultiplyOperation(Named("x"), Number(0.2))), -1, 1);
        AssertSlopesMatchDifferences(Call("acos", new BinaryMultiplyOperation(Named("x"), Number(0.2))), -1, 1);
        AssertSlopesMatchDifferences(Call("atan", Named("z")));
        AssertSlopesMatchDifferences(Call("atan2", Named("y"), new BinaryPlusOperation(Named("x"), Number(6))), 1, 3);
        AssertSlopesMatchDifferences(Call("sinh", Named("x")), -1, 1);
        AssertSlopesMatchDifferences(Call("cosh", Named("y")), -1, 1);
        AssertSlopesMatchDifferences(Call("tanh", Named("z")));
        AssertSlopesMatchDifferences(Call("toDegrees", Named("x")));
    }

    /// <summary>
    /// This tests a function of the three variables at once, put together the way a scene would -- so
    /// that the chain rule is exercised through several levels rather than one.
    /// </summary>
    [TestMethod]
    public void TestTheSlopeOfSomethingDeep()
    {
        // A torus: (√(x² + z²) - 2)² + y² - 0.25
        Term distanceFromTheRing = new BinaryMinusOperation(
            Call("sqrt", new BinaryPlusOperation(
                new SquareOperation(Named("x")), new SquareOperation(Named("z")))),
            Number(2));

        AssertSlopesMatchDifferences(new BinaryMinusOperation(
            new BinaryPlusOperation(
                new SquareOperation(distanceFromTheRing),
                new SquareOperation(Named("y"))),
            Number(0.25)), 1, 3);

        // A twist, which is where the trigonometry and the arithmetic meet.
        AssertSlopesMatchDifferences(new BinaryMinusOperation(
            new BinaryMultiplyOperation(
                Call("sin", new BinaryMultiplyOperation(Named("y"), Number(2))),
                Named("x")),
            Call("cos", Named("z"))));
    }

    /// <summary>
    /// This tests that a field calling something there is no slope for is turned down when the gradient
    /// is asked for, against the text that wrote the call.
    /// </summary>
    [TestMethod]
    public void TestAFunctionWithNoRuleIsReported()
    {
        FieldExpression expression = Call("smoothstep", Number(0), Number(1), Named("x"))
            .ToField(_variables);

        // The function itself is perfectly usable; it is only its slope that is missing.
        Assert.AreEqual(0.5, FieldFunction.Compile(expression).Evaluate(0.5, 0, 0));

        TokenException exception = Assert.ThrowsExactly<TokenException>(
            () => FieldGradient.Of(expression));

        Assert.Contains("'smoothstep'", exception.Message);
        Assert.Contains("no rule", exception.Message);
    }

    /// <summary>
    /// This holds the rules to the catalog.  A function added to the catalog without a slope, and
    /// without being named as one that has none, would otherwise work perfectly until the first scene
    /// asked a surface built from it which way it faced.
    /// </summary>
    [TestMethod]
    public void TestEveryFunctionHasARuleOrIsNamedAsHavingNone()
    {
        List<string> unaccounted = [];

        foreach (string name in FunctionCatalog.Instance.Names)
        {
            // A function that cannot appear in a field at all needs no slope: the vector forms are
            // turned away when the field is built, long before anything asks for a gradient.
            bool couldAppear = FunctionCatalog.Instance
                .SignaturesFor(name)
                .Any(signature => signature.ReturnType == typeof(double) &&
                                  !signature.NotInAField &&
                                  signature.ParameterTypes.All(type => type == typeof(double)));

            if (!couldAppear || FieldDerivatives.HasRuleFor(name) ||
                FieldDerivatives.WithoutRules.Contains(name))
                continue;

            unaccounted.Add(name);
        }

        Assert.IsEmpty(unaccounted,
            "these functions may appear in a field but have no slope rule, and are not named as " +
            $"having none: {string.Join(", ", unaccounted)}");
    }
}
