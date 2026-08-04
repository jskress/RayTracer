using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using Lex.Parser;
using RayTracer.Fields;

namespace RayTracer.Terms;

/// <summary>
/// This class represents a term that is a tuple of 3 or 4 terms.
/// </summary>
public class TupleTerm : Term
{
    private readonly Term _x;
    private readonly Term _y;
    private readonly Term _z;
    private readonly Term _w;

    public TupleTerm(Token errorToken, List<Term> terms) : base(errorToken)
    {
        _x = terms[0];
        _y = terms[1];
        _z = terms.Count > 2 ? terms[2] : null;
        _w = terms.Count > 3 ? terms[3] : null;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce a tuple value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        double x = _x.GetValue<double>(variables);
        double y = _y.GetValue<double>(variables);
        double? z = _z?.GetValue<double>(variables, false);
        double? w = _w?.GetValue<double>(variables, false);

        return z.HasValue
            ? new NumberTuple(x, y, z.Value, w ?? double.NaN)
            : new TwoDPoint(x, y);
    }

    /// <summary>
    /// This method is used to lower this tuple into a field expression, which it cannot be: a field is
    /// arithmetic on one number at a time.
    /// <para>
    /// This is worth a message of its own rather than the general refusal, because writing a distance
    /// as <c>length([x, y, z])</c> is the first thing anyone who has met signed distance functions will
    /// reach for, and being told that a tuple is not arithmetic would leave them none the wiser about
    /// what to write instead.
    /// </para>
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <returns>Nothing; this always reports the problem.</returns>
    public override FieldExpression ToField(Variables variables)
    {
        throw new TokenException(
            "A function works on one number at a time, so a tuple cannot appear in one, and neither " +
            "can the functions that take vectors.  Write the arithmetic out in x, y and z instead: " +
            "for a distance from the origin, that is sqrt(x\u00b2 + y\u00b2 + z\u00b2).")
        {
            Token = ErrorToken
        };
    }
}
