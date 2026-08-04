using Lex.Tokens;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover bounding a field function over a box of space.
/// <para>
/// The rule a bound must obey runs one way only: it may be wider than the truth but never narrower.
/// So these check the property rather than particular numbers -- take a box, sample the field all over
/// it, and insist the bound contains everything found.  A bound that is too generous passes, as it
/// should, since all it costs is work; a bound that is too tight is the failure worth catching, because
/// what it costs is a surface disappearing in patches wherever the marcher wrongly skipped.
/// </para>
/// <para>
/// Sampling cannot prove a bound correct -- it can only fail to disprove it -- so these lean on volume:
/// a few hundred boxes, of widely different sizes and places, sampled on a grid within each.  That has
/// been enough to catch every rule written the wrong way round so far.
/// </para>
/// </summary>
[TestClass]
public class TestFieldBounds
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
    /// Takes the given term over a great many boxes, samples it throughout each, and insists the bound
    /// claimed for the box contains every value found in it.
    /// </summary>
    /// <param name="term">The term to bound.</param>
    /// <param name="mustBeUseful">Whether the bound must also be a finite one.  A rule that answers
    /// "anywhere" is always safe, so containment alone cannot tell a real rule from a missing one;
    /// this is what says the rule is actually doing something.</param>
    private void AssertTheBoundHoldsOver(Term term, bool mustBeUseful = true)
    {
        FieldExpression expression = term.ToField(_variables);
        FieldFunction function = FieldFunction.Compile(expression);
        Random random = new (20260804);
        int useful = 0;

        for (int box = 0; box < 300; box++)
        {
            // Boxes from the tiny to the wide, anywhere within a few units of the origin.
            double width = Math.Pow(10, random.NextDouble() * 2 - 1.5);
            double x = random.NextDouble() * 6 - 3;
            double y = random.NextDouble() * 6 - 3;
            double z = random.NextDouble() * 6 - 3;
            FieldRange alongX = new (x, x + width);
            FieldRange alongY = new (y, y + width);
            FieldRange alongZ = new (z, z + width);
            FieldRange bound = expression.Bound(alongX, alongY, alongZ);

            if (bound.IsAnywhere)
                continue;

            useful++;

            const int steps = 4;

            for (int i = 0; i <= steps; i++)
            for (int j = 0; j <= steps; j++)
            for (int k = 0; k <= steps; k++)
            {
                double sampleX = alongX.Low + alongX.Width * i / steps;
                double sampleY = alongY.Low + alongY.Width * j / steps;
                double sampleZ = alongZ.Low + alongZ.Width * k / steps;
                double value = function.Evaluate(sampleX, sampleY, sampleZ);

                if (double.IsNaN(value))
                    continue;

                double tolerance = 1e-9 * Math.Max(1, Math.Abs(value));

                Assert.IsTrue(bound.Contains(value, tolerance),
                    $"{expression} over x{alongX} y{alongY} z{alongZ} was bounded to {bound}, " +
                    $"but at ({sampleX:F4}, {sampleY:F4}, {sampleZ:F4}) it is {value}");
            }
        }

        if (mustBeUseful)
        {
            Assert.IsTrue(useful > 150,
                $"{expression} was bounded to 'anywhere' for all but {useful} of 300 boxes, so " +
                $"nothing here is really being bounded");
        }
    }

    /// <summary>
    /// This tests the bounds of the arithmetic, including the two cases that need care: a product where
    /// either side may straddle nought, and a division by something that might be nought.
    /// </summary>
    [TestMethod]
    public void TestTheBoundsOfArithmetic()
    {
        AssertTheBoundHoldsOver(new BinaryMinusOperation(
            new BinaryPlusOperation(
                new BinaryPlusOperation(new SquareOperation(Named("x")), new SquareOperation(Named("y"))),
                new SquareOperation(Named("z"))),
            Number(1)));

        // Both sides straddle nought here, so every corner of the product matters.
        AssertTheBoundHoldsOver(new BinaryMultiplyOperation(
            new BinaryPlusOperation(Named("x"), Named("y")),
            new BinaryMinusOperation(Named("z"), Named("x"))));

        // A divisor that cannot be nought, and so can be bounded.
        AssertTheBoundHoldsOver(new BinaryDivideOperation(
            Named("x"), new BinaryPlusOperation(new SquareOperation(Named("y")), Number(1))));

        AssertTheBoundHoldsOver(new UnaryMinusOperation(new CubeOperation(Named("y"))));
    }

    /// <summary>
    /// This tests that a divisor which might be nought is not bounded at all, since the answer there
    /// really could be anything.
    /// </summary>
    [TestMethod]
    public void TestDividingBySomethingThatMightBeNought()
    {
        FieldExpression expression = new BinaryDivideOperation(Number(1), Named("x"))
            .ToField(_variables);

        Assert.IsTrue(expression.Bound(new FieldRange(-1, 1), default, default).IsAnywhere);

        // Away from nought it is perfectly boundable.
        Assert.IsFalse(expression.Bound(new FieldRange(1, 2), default, default).IsAnywhere);
    }

    /// <summary>
    /// This tests the bound of each function that has a rule.
    /// </summary>
    [TestMethod]
    public void TestTheBoundOfEveryFunctionWithARule()
    {
        AssertTheBoundHoldsOver(Call("sqrt", new BinaryPlusOperation(
            new SquareOperation(Named("x")), Number(1))));
        AssertTheBoundHoldsOver(Call("cbrt", Named("x")));
        AssertTheBoundHoldsOver(Call("exp", Named("x")));
        AssertTheBoundHoldsOver(Call("log", new BinaryPlusOperation(
            new SquareOperation(Named("x")), Number(1))));
        AssertTheBoundHoldsOver(Call("log10", new BinaryPlusOperation(
            new SquareOperation(Named("y")), Number(1))));
        AssertTheBoundHoldsOver(Call("abs", Named("x")));
        AssertTheBoundHoldsOver(Call("sign", Named("x")));
        AssertTheBoundHoldsOver(Call("floor", Named("x")));
        AssertTheBoundHoldsOver(Call("ceil", Named("y")));
        AssertTheBoundHoldsOver(Call("round", Named("z")));
        AssertTheBoundHoldsOver(Call("trunc", Named("x")));
        AssertTheBoundHoldsOver(Call("toDegrees", Named("x")));
        AssertTheBoundHoldsOver(Call("mod", Named("x"), Number(2)));
        AssertTheBoundHoldsOver(Call("min", Named("x"), Named("y")));
        AssertTheBoundHoldsOver(Call("max", Named("x"), Named("z")));
        AssertTheBoundHoldsOver(Call("clamp", Named("x"), Number(-1), Number(1)));
        AssertTheBoundHoldsOver(Call("lerp", Named("x"), Named("y"), Named("z")));
        AssertTheBoundHoldsOver(Call("smoothstep", Number(0), Number(1), Named("x")));
        AssertTheBoundHoldsOver(Call("atan2", Named("y"), Named("x")));
        AssertTheBoundHoldsOver(Call("atan", Named("x")));
        AssertTheBoundHoldsOver(Call("sinh", Named("x")));
        AssertTheBoundHoldsOver(Call("cosh", Named("x")));
        AssertTheBoundHoldsOver(Call("tanh", Named("x")));

        // The waves, which have to notice a crest or a trough inside the range.
        AssertTheBoundHoldsOver(Call("sin", Named("x")));
        AssertTheBoundHoldsOver(Call("cos", Named("y")));
        AssertTheBoundHoldsOver(Call("sin", new BinaryMultiplyOperation(Named("x"), Number(20))));

        // Powers, even and odd, whole and fractional.
        AssertTheBoundHoldsOver(Call("pow", Named("x"), Number(4)));
        AssertTheBoundHoldsOver(Call("pow", Named("x"), Number(3)));
        AssertTheBoundHoldsOver(Call("pow", Call("abs", Named("x")), Number(0.5)), false);

        // Those with a domain smaller than the boxes tested here answer "anywhere" for the boxes that
        // fall outside it, which is the honest answer rather than a rule that quietly narrows them.
        AssertTheBoundHoldsOver(Call("asin", Named("x")), false);
        AssertTheBoundHoldsOver(Call("acos", Named("x")), false);
        AssertTheBoundHoldsOver(Call("tan", Named("x")), false);
    }

    /// <summary>
    /// This tests a function of all three variables, put together as a scene would, so that ranges are
    /// carried through several levels of arithmetic rather than one.
    /// </summary>
    [TestMethod]
    public void TestTheBoundOfSomethingDeep()
    {
        // The torus again: (√(x² + z²) - 2)² + y² - 0.25
        Term distanceFromTheRing = new BinaryMinusOperation(
            Call("sqrt", new BinaryPlusOperation(
                new SquareOperation(Named("x")), new SquareOperation(Named("z")))),
            Number(2));

        AssertTheBoundHoldsOver(new BinaryMinusOperation(
            new BinaryPlusOperation(
                new SquareOperation(distanceFromTheRing), new SquareOperation(Named("y"))),
            Number(0.25)));

        // A twisted column, where a wave meets the arithmetic.
        AssertTheBoundHoldsOver(new BinaryMinusOperation(
            new BinaryPlusOperation(
                new SquareOperation(new BinaryMultiplyOperation(
                    Named("x"), Call("cos", Named("y")))),
                new SquareOperation(new BinaryMultiplyOperation(
                    Named("z"), Call("sin", Named("y"))))),
            Number(1)));
    }

    /// <summary>
    /// This tests that a bound of the whole of a field's own box tells the marcher what it needs: that
    /// the surface is in there at all.  A sphere of radius one crosses nought within a box that holds
    /// it and cannot within a box off to one side of it.
    /// </summary>
    [TestMethod]
    public void TestABoundCanRuleABoxInOrOut()
    {
        FieldExpression sphere = new BinaryMinusOperation(
            new BinaryPlusOperation(
                new BinaryPlusOperation(new SquareOperation(Named("x")), new SquareOperation(Named("y"))),
                new SquareOperation(Named("z"))),
            Number(1)).ToField(_variables);

        FieldRange around = sphere.Bound(
            new FieldRange(-2, 2), new FieldRange(-2, 2), new FieldRange(-2, 2));

        Assert.IsTrue(around.Contains(0), "the surface is in this box, so its bound must allow it");

        FieldRange wellOutside = sphere.Bound(
            new FieldRange(5, 6), new FieldRange(5, 6), new FieldRange(5, 6));

        Assert.IsFalse(wellOutside.Contains(0),
            "the surface is nowhere near this box, and a bound that cannot say so is no use");

        FieldRange deepInside = sphere.Bound(
            new FieldRange(-0.1, 0.1), new FieldRange(-0.1, 0.1), new FieldRange(-0.1, 0.1));

        Assert.IsFalse(deepInside.Contains(0), "this box is entirely within the sphere");
    }

    /// <summary>
    /// This tests the one bound rule here arrived at by measurement rather than by reasoning.  Noise is
    /// bounded by what it is in the middle of a box, give or take how much it could change across it, and
    /// the slope that allows for was measured rather than derived -- so it is worth leaning on hard.
    /// <para>
    /// Boxes are drawn from the tiny to the wide, and each is sampled far more densely than the general
    /// test samples, since what would go wrong here is a bound too tight by a little in a small box.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheBoundOfNoiseHolds()
    {
        FieldExpression expression = Call("noise", Named("x"), Named("y"), Named("z"))
            .ToField(_variables);
        FieldFunction function = FieldFunction.Compile(expression);
        Random random = new (20260804);

        for (int box = 0; box < 600; box++)
        {
            double width = Math.Pow(10, random.NextDouble() * 3 - 3);
            double x = random.NextDouble() * 20 - 10;
            double y = random.NextDouble() * 20 - 10;
            double z = random.NextDouble() * 20 - 10;
            FieldRange alongX = new (x, x + width);
            FieldRange alongY = new (y, y + width);
            FieldRange alongZ = new (z, z + width);
            FieldRange bound = expression.Bound(alongX, alongY, alongZ);

            const int steps = 6;

            for (int i = 0; i <= steps; i++)
            for (int j = 0; j <= steps; j++)
            for (int k = 0; k <= steps; k++)
            {
                double sampleX = alongX.Low + alongX.Width * i / steps;
                double sampleY = alongY.Low + alongY.Width * j / steps;
                double sampleZ = alongZ.Low + alongZ.Width * k / steps;
                double value = function.Evaluate(sampleX, sampleY, sampleZ);

                Assert.IsTrue(bound.Contains(value, 1e-9),
                    $"noise over a box of {width:G4} at ({x:F3}, {y:F3}, {z:F3}) was bounded to " +
                    $"{bound}, but at ({sampleX:F5}, {sampleY:F5}, {sampleZ:F5}) it is {value}");
            }
        }
    }

    /// <summary>
    /// This holds the bound rules to the catalog, the way the slopes are held to it.  A function with
    /// neither a rule nor a mention here would silently make every field that used it unboundable, and
    /// so every surface built from one slow rather than wrong -- which is the sort of thing that goes
    /// unnoticed for a long time.
    /// </summary>
    [TestMethod]
    public void TestEveryFunctionHasABoundRuleOrIsNamedAsHavingNone()
    {
        // Nothing is exempt: every function a field may call has a range rule.
        IReadOnlySet<string> withoutRules = new HashSet<string>();
        List<string> unaccounted = [];

        foreach (string name in FunctionCatalog.Instance.Names)
        {
            bool couldAppear = FunctionCatalog.Instance
                .SignaturesFor(name)
                .Any(signature => signature.ReturnType == typeof(double) &&
                                  signature.ParameterTypes.All(type => type == typeof(double)));

            if (!couldAppear || FieldBounds.HasRuleFor(name) || withoutRules.Contains(name))
                continue;

            unaccounted.Add(name);
        }

        Assert.IsEmpty(unaccounted,
            "these functions may appear in a field but have no rule for their range, and are not " +
            $"named as having none: {string.Join(", ", unaccounted)}");
    }
}
