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
        // A name nobody ever set is worth saying so about, plainly and with the name in hand.  Left to
        // itself it comes back as nothing, and nothing then fails further along where all that can be
        // reported is that some empty value would not convert -- which does not tell a scene's author
        // the one thing they need, which is what they mistyped.  This matters more than it used to now
        // that a loop's counter belongs to its loop: reaching for a name that has gone out of scope is
        // an ordinary mistake rather than an exotic one.
        if (!variables.ContainsKey(_name))
        {
            throw new TokenException($"Nothing named '{_name}' has been set here.")
            {
                Token = ErrorToken
            };
        }

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

        // A value a folded function was handed may itself be a piece of field arithmetic rather than
        // a settled number -- which is exactly what happens when one shape function is handed to
        // another.  Without this, a vocabulary of shapes could only ever be given constants, and
        // building one shape out of others, which is the whole reason to name them, would be
        // impossible.
        if (variables.GetValue(_name, typeof(FieldExpression)) is FieldExpression already)
            return already;

        throw new TokenException(
            $"A function knows the variables x, y and z; '{_name}' is neither one of those nor a " +
            $"name the scene has given a number to.")
        {
            Token = ErrorToken
        };
    }
}
