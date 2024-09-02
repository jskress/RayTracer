using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.General;

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
        _w = terms.Count == 3 ? null : terms[3];
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
}
