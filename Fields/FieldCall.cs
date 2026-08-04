using System.Linq.Expressions;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Terms;

namespace RayTracer.Fields;

/// <summary>
/// This class represents a call to one of the DSL's functions from within a field function.
/// <para>
/// Which form of the function is meant is settled while the tree is being built rather than each time
/// it is asked about, because here -- unlike in a scene, where a variable has no type until it is
/// evaluated -- everything is a number and is known to be.  That is the third of the resolution
/// points the catalog was built for, and it is what lets the compiled field hold a direct call to the
/// method rather than anything that has to look one up.
/// </para>
/// </summary>
public class FieldCall : FieldExpression
{
    /// <summary>
    /// This property holds the form of the function being called.
    /// </summary>
    public FunctionSignature Signature { get; }

    /// <summary>
    /// This property holds the expressions the function is being given.
    /// </summary>
    public FieldExpression[] Arguments { get; }

    private readonly Token _errorToken;

    private FieldCall(FunctionSignature signature, FieldExpression[] arguments, Token errorToken)
    {
        Signature = signature;
        Arguments = arguments;
        _errorToken = errorToken;
    }

    /// <summary>
    /// This method is used to build a call, doing it now if every argument is already a number.  A
    /// scene that writes <c>sqrt(2) * x</c> should pay for that root once, while the scene is being
    /// read, rather than at every point the field is asked about.
    /// </summary>
    /// <param name="signature">The form of the function being called.</param>
    /// <param name="arguments">The expressions the function is being given.</param>
    /// <param name="errorToken">The text that wrote the call, to report against later.</param>
    /// <returns>The call, or the number it always produces.</returns>
    public static FieldExpression Of(
        FunctionSignature signature, FieldExpression[] arguments, Token errorToken)
    {
        if (arguments.All(argument => argument.ConstantValue is not null))
        {
            object[] values = arguments
                .Select(argument => (object) argument.ConstantValue.Value)
                .ToArray();

            return new FieldConstant((double) signature.Invoke(values));
        }

        return new FieldCall(signature, arguments, errorToken);
    }

    /// <summary>
    /// This method is used to emit this call as a .NET expression: a direct call to the method that
    /// implements the function.
    /// </summary>
    internal override Expression ToDotNet(
        ParameterExpression x, ParameterExpression y, ParameterExpression z)
    {
        return Expression.Call(
            Signature.Method,
            Arguments.Select(argument => argument.ToDotNet(x, y, z)));
    }

    public override string ToString()
    {
        return $"{Signature.Name}({string.Join(", ", Arguments.Select(argument => argument.ToString()))})";
    }

    /// <summary>
    /// This method is used to differentiate this call by the chain rule: each value the function is
    /// given contributes the function's own rate of change with respect to that value, times the rate
    /// at which that value itself changes.  For a function of one value that is the familiar
    /// <c>f'(u)·u'</c>; for more than one, the sum of one such term apiece.
    /// </summary>
    public override FieldExpression Differentiate(FieldAxis axis)
    {
        FieldExpression[] partials = FieldDerivatives.PartialsFor(Signature.Name, Arguments);

        if (partials is null)
        {
            throw new TokenException(
                $"A surface built from this needs the slope of its function, and there is no rule " +
                $"for the slope of '{Signature.Name}'.")
            {
                Token = _errorToken
            };
        }

        FieldExpression derivative = FieldConstant.Zero;

        for (int index = 0; index < Arguments.Length; index++)
        {
            derivative = FieldArithmetic.Of(FieldOperator.Add, derivative,
                FieldArithmetic.Of(FieldOperator.Multiply,
                    partials[index], Arguments[index].Differentiate(axis)));
        }

        return derivative;
    }

    /// <summary>
    /// This method is used to work out the range of this call, which is what the function could produce
    /// from the ranges of what it is given (see <see cref="FieldBounds"/>).
    /// </summary>
    public override FieldRange Bound(FieldRange x, FieldRange y, FieldRange z)
    {
        return FieldBounds.RangeFor(
            Signature.Name,
            Arguments.Select(argument => argument.Bound(x, y, z)).ToArray());
    }
}
