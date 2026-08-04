using System.Linq.Expressions;

namespace RayTracer.Fields;

/// <summary>
/// This class holds a field function that has been compiled: a real delegate, over three doubles,
/// that the JIT has turned into machine code.
/// <para>
/// A marcher asks a field for its value along every ray it follows, so this is the hottest arithmetic
/// in a scene that carries one -- millions of calls per frame, from several threads at once.  Walking
/// a tree to answer each of them, boxing a double at every node as the DSL's own evaluator must, is
/// not a way to spend that.  Compiling instead costs a millisecond or so once, while the scene is
/// being prepared, and is why there is no separate "interpret while you work, compile for the final
/// render" mode to get wrong: it is quick enough to do every time.
/// </para>
/// </summary>
public class FieldFunction
{
    /// <summary>
    /// This property holds the expression this function was compiled from, which the gradient and the
    /// bounding both work from.
    /// </summary>
    public FieldExpression Expression { get; }

    private readonly Func<double, double, double, double> _function;

    private FieldFunction(FieldExpression expression, Func<double, double, double, double> function)
    {
        Expression = expression;
        _function = function;
    }

    /// <summary>
    /// This method is used to compile a field expression into a callable function.
    /// </summary>
    /// <param name="expression">The expression to compile.</param>
    /// <returns>The compiled function.</returns>
    public static FieldFunction Compile(FieldExpression expression)
    {
        ParameterExpression x = System.Linq.Expressions.Expression.Parameter(typeof(double), "x");
        ParameterExpression y = System.Linq.Expressions.Expression.Parameter(typeof(double), "y");
        ParameterExpression z = System.Linq.Expressions.Expression.Parameter(typeof(double), "z");
        Func<double, double, double, double> function = System.Linq.Expressions.Expression
            .Lambda<Func<double, double, double, double>>(expression.ToDotNet(x, y, z), x, y, z)
            .Compile();

        return new FieldFunction(expression, function);
    }

    /// <summary>
    /// This method returns the value of the field at the given point.
    /// </summary>
    /// <param name="x">The X of the point.</param>
    /// <param name="y">Its Y.</param>
    /// <param name="z">Its Z.</param>
    /// <returns>The value of the field there.</returns>
    public double Evaluate(double x, double y, double z)
    {
        return _function(x, y, z);
    }

    public override string ToString()
    {
        return Expression.ToString();
    }
}
