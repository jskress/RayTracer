using RayTracer.Terms;

namespace RayTracer.Fields;

/// <summary>
/// This class holds the derivative of each function a field may call, with respect to each value that
/// function is given.  <see cref="FieldCall"/> puts them together with the chain rule.
/// <para>
/// Each rule is written in the same expressions the field itself is built from, so a derivative is
/// another field expression and can be differentiated again, compiled, or bounded like any other.
/// Several are only true almost everywhere -- <c>abs</c> has no derivative at nought, and neither
/// <c>min</c> nor <c>floor</c> has one where it changes which way it is going -- which is the usual
/// bargain for a surface built out of them: the normal is right everywhere except along a crease,
/// and along a crease there is no one right answer to give.
/// </para>
/// <para>
/// The functions listed in <see cref="WithoutRules"/> may appear in a field but have no rule here, so
/// a field that calls one cannot be asked for its gradient.  That is not an oversight in each case;
/// noise has no derivative to write down, and will be given one by taking differences when it is
/// wired in.  A test holds this file to the catalog, so a function added there without a rule here is
/// a failure rather than a surprise at render time.
/// </para>
/// </summary>
public static class FieldDerivatives
{
    /// <summary>
    /// The functions a field may call but cannot be differentiated through.
    /// </summary>
    public static readonly IReadOnlySet<string> WithoutRules = new HashSet<string>
    {
        "smoothstep", "noise"
    };

    private static readonly Dictionary<string, Func<FieldExpression[], FieldExpression[]>> Rules = new ()
    {
        // d/du √u = 1 / (2√u)
        { "sqrt", u => [Divide(Number(1), Multiply(Number(2), Call("sqrt", u[0])))] },

        // d/du ∛u = 1 / (3(∛u)²)
        { "cbrt", u => [Divide(Number(1), Multiply(Number(3), Square(Call("cbrt", u[0]))))] },

        // d/du uᵛ = v·uᵛ⁻¹, and d/dv uᵛ = uᵛ·ln u.  The second is undefined for a negative u, which
        // would matter were the exponent ever a variable; when it is a constant -- which is every use
        // there has yet been -- its own derivative is nought and the whole term folds away unbuilt.
        { "pow", u =>
            [
                Multiply(u[1], Call("pow", u[0], Subtract(u[1], Number(1)))),
                Multiply(Call("pow", u[0], u[1]), Call("log", u[0]))
            ]
        },

        // Turning radians into degrees only ever scales, so its slope is that scale.
        { "toDegrees", _ => [Number(180 / Math.PI)] },

        { "exp", u => [Call("exp", u[0])] },
        { "log", u => [Divide(Number(1), u[0])] },
        { "log10", u => [Divide(Number(1), Multiply(u[0], Number(Math.Log(10))))] },

        // Almost everywhere: the sign of what went in, and nought for the steps.
        { "abs", u => [Call("sign", u[0])] },
        { "sign", _ => [FieldConstant.Zero] },
        { "floor", _ => [FieldConstant.Zero] },
        { "ceil", _ => [FieldConstant.Zero] },
        { "round", _ => [FieldConstant.Zero] },
        { "trunc", _ => [FieldConstant.Zero] },

        // mod(u, v) = u - v·floor(u/v), so it moves with u, and with v by whole steps of it.
        { "mod", u => [FieldConstant.One, FieldNegation.Of(Call("floor", Divide(u[0], u[1])))] },

        // min and max follow whichever side won, which is 1 for that side and 0 for the other.  The
        // switch is written as a sign rather than as a test, since a field holds arithmetic and
        // nothing else; where the two sides meet it gives each a half, which is as good an answer as
        // there is at a corner.
        { "min", u => [Won(Subtract(u[1], u[0])), Won(Subtract(u[0], u[1]))] },
        { "max", u => [Won(Subtract(u[0], u[1])), Won(Subtract(u[1], u[0]))] },

        // clamp holds still outside its two ends and follows its value between them.
        { "clamp", u =>
            [
                Multiply(Won(Subtract(u[0], u[1])), Won(Subtract(u[2], u[0]))),
                Won(Subtract(u[1], u[0])),
                Won(Subtract(u[0], u[2]))
            ]
        },

        // lerp(a, b, t) = a + (b - a)t
        { "lerp", u =>
            [
                Subtract(Number(1), u[2]),
                u[2],
                Subtract(u[1], u[0])
            ]
        },

        { "sin", u => [Call("cos", u[0])] },
        { "cos", u => [FieldNegation.Of(Call("sin", u[0]))] },
        { "tan", u => [Add(Number(1), Square(Call("tan", u[0])))] },
        { "asin", u => [Divide(Number(1), Call("sqrt", Subtract(Number(1), Square(u[0]))))] },
        { "acos", u => [FieldNegation.Of(
            Divide(Number(1), Call("sqrt", Subtract(Number(1), Square(u[0])))))] },
        { "atan", u => [Divide(Number(1), Add(Number(1), Square(u[0])))] },

        // atan2(y, x): the angle turns with y across the distance, and against x.
        { "atan2", u =>
            [
                Divide(u[1], Add(Square(u[0]), Square(u[1]))),
                FieldNegation.Of(Divide(u[0], Add(Square(u[0]), Square(u[1]))))
            ]
        },

        { "sinh", u => [Call("cosh", u[0])] },
        { "cosh", u => [Call("sinh", u[0])] },
        { "tanh", u => [Subtract(Number(1), Square(Call("tanh", u[0])))] }
    };

    /// <summary>
    /// This method returns the derivative of the named function with respect to each value it is
    /// given, or <c>null</c> if there is no rule for it.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="arguments">The expressions the function is being given.</param>
    /// <returns>One derivative per argument, or <c>null</c>.</returns>
    public static FieldExpression[] PartialsFor(string name, FieldExpression[] arguments)
    {
        if (!Rules.TryGetValue(name, out Func<FieldExpression[], FieldExpression[]> rule))
            return null;

        FieldExpression[] partials = rule(arguments);

        return partials.Length == arguments.Length ? partials : null;
    }

    /// <summary>
    /// This method reports whether there is a rule for the named function.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <returns><c>true</c>, if the function can be differentiated.</returns>
    public static bool HasRuleFor(string name)
    {
        return Rules.ContainsKey(name);
    }

    /// <summary>
    /// This method builds "1 when the given expression is positive, 0 when it is negative, and a half
    /// where it is nought": <c>(1 + sign(u)) / 2</c>.  It is how the functions that pick one of their
    /// values say which one they picked, without a field needing to hold a decision.
    /// </summary>
    /// <param name="expression">The expression whose sign decides.</param>
    /// <returns>The expression that is 1 where that one is positive.</returns>
    private static FieldExpression Won(FieldExpression expression)
    {
        return Divide(Add(Number(1), Call("sign", expression)), Number(2));
    }

    private static FieldExpression Number(double value) => new FieldConstant(value);

    private static FieldExpression Add(FieldExpression left, FieldExpression right) =>
        FieldArithmetic.Of(FieldOperator.Add, left, right);

    private static FieldExpression Subtract(FieldExpression left, FieldExpression right) =>
        FieldArithmetic.Of(FieldOperator.Subtract, left, right);

    private static FieldExpression Multiply(FieldExpression left, FieldExpression right) =>
        FieldArithmetic.Of(FieldOperator.Multiply, left, right);

    private static FieldExpression Divide(FieldExpression left, FieldExpression right) =>
        FieldArithmetic.Of(FieldOperator.Divide, left, right);

    private static FieldExpression Square(FieldExpression operand) => Multiply(operand, operand);

    /// <summary>
    /// This method builds a call to one of the catalog's functions.  Every function named in this file
    /// takes and gives back numbers, so the form wanted is always there; a rule naming one that is not
    /// is a mistake in this file rather than anything a scene did.
    /// </summary>
    /// <param name="name">The name of the function to call.</param>
    /// <param name="arguments">The expressions to give it.</param>
    /// <returns>The call.</returns>
    private static FieldExpression Call(string name, params FieldExpression[] arguments)
    {
        (FunctionSignature signature, string error) = FunctionCatalog.Instance.ResolveForTypes(
            name, arguments.Select(_ => typeof(double)).ToArray());

        if (signature is null)
            throw new InvalidOperationException($"Internal error in a derivative rule: {error}");

        return FieldCall.Of(signature, arguments, null);
    }
}
