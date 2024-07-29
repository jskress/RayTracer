using Lex.Tokens;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents a term that represents a variable reference.
/// </summary>
public class VariableTerm : Term
{
    private readonly string _name;

    public VariableTerm(Token token) : base(token)
    {
        _name = token.Text;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce the current value of a variable. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        return targetTypes.Length == 0
            ? variables.GetValue(_name)
            : targetTypes
                .Select(type => variables.GetValue(_name, type))
                .FirstOrDefault(value => value != null);
    }
}
