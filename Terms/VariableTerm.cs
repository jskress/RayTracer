using Lex.Tokens;
using RayTracer.General;
using Lex.Parser;
using RayTracer.Fields;

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

    /// <summary>
    /// This method is used to lower this variable into a field expression.  The names x, y and z are
    /// the point the field is being asked about, whatever else a scene may have called by them;
    /// anything else must already have a number for its value, which becomes a constant here, since a
    /// compiled field has no variables left to look up.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <returns>This term, as a field expression.</returns>
    public override FieldExpression ToField(Variables variables)
    {
        FieldVariable variable = FieldVariable.For(_name);

        if (variable is not null)
            return variable;

        if (variables.GetValue(_name, typeof(double)) is double number)
            return new FieldConstant(number);

        throw new TokenException(
            $"A function knows the variables x, y and z; '{_name}' is neither one of those nor a " +
            $"name the scene has given a number to.")
        {
            Token = ErrorToken
        };
    }
}
