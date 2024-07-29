using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions;

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
        _z = terms[2];
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
        double x = (double) _x.GetValue(variables, typeof(double));
        double y = (double) _y.GetValue(variables, typeof(double));
        double z = (double) _z.GetValue(variables, typeof(double));
        double? w = (double?) _w?.GetValue(variables, typeof(double));

        return new NumberTuple(x, y, z, w ?? double.NaN);
    }
}
