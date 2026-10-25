using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Graphics;
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

        if (targetTypes.Length == 0)
        {
            // A name holding more than one thing -- `Coral` is a color and an index of refraction both -- says
            // nothing of which is meant where nothing in particular is asked for, as in `Shade = Coral`; it came
            // back as nothing, and failed far off where the nothing was used.
            IReadOnlyCollection<object> held = variables.ValuesOf(_name);

            if (held.Count > 1)
            {
                string kinds = string.Join(" and ", held.Select(value => KindOf(value)).Order());

                throw new TokenException(
                    $"'{_name}' names more than one thing here -- {kinds} -- so which is meant cannot be told; " +
                    $"say which with a cast, as in \"color {_name}\", or use it where only one of them fits.")
                {
                    Token = ErrorToken
                };
            }

            return variables.GetValue(_name);
        }

        object found = targetTypes
            .Select(type => variables.GetValue(_name, type))
            .FirstOrDefault(value => value != null);

        // Nothing was filed under any of the types asked about, but a name holding one thing may
        // still answer: what it holds might convert.  Handing it back lets the coercion that follows
        // decide, rather than reporting nothing and leaving the caller to say it could not resolve
        // something it never actually looked at.
        //
        // This is what left `pale = [0.8, 0.3, 0.2]` unusable as a pigment: a tuple is not a color and
        // never turned up under one, though it converts to one readily enough.
        return found ?? variables.GetValue(_name);
    }

    // What a value is, as a scene's author would name it.
    private static string KindOf(object value) => value switch
    {
        double => "a number",
        Color => "a color",
        Point => "a point",
        Vector => "a vector",
        string => "a piece of text",
        _ => $"a {value?.GetType().Name.ToLowerInvariant()}"
    };

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
        // A value a folded function was handed may itself be a piece of field arithmetic rather than
        // a settled number -- which is exactly what happens when one shape function is handed to
        // another.  Without this, a vocabulary of shapes could only ever be given constants, and
        // building one shape out of others, which is the whole reason to name them, would be
        // impossible.
        //
        // **It is asked first, before the field's own x, y and z**: a function whose parameters are
        // named `x` and `z` means its parameters by them, as anywhere else a name is declared closer
        // in.  Asked after, the field's coordinates won, and a terrain function written as `Dune(x, z)`
        // and handed places in meters was quietly handed the unit square instead.  Only a folded
        // function's scope binds field arithmetic to a name, so nothing else is changed by the order.
        if (variables.GetValue(_name, typeof(FieldExpression)) is FieldExpression already)
            return already;

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
