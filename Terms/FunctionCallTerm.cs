using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;
using RayTracer.Fields;

namespace RayTracer.Terms;

/// <summary>
/// This class represents a call to one of the DSL's functions.
/// <para>
/// The work is split between the two times a call is looked at, because the DSL binds late: a
/// scene's variables have no type until the scene is evaluated, so which form of an overloaded
/// function a call means cannot be settled while it is being read.  What <i>can</i> be settled then
/// is that the function exists and takes as many values as the call supplies, and that is checked
/// as the term is built -- a misspelled name is reported against the text that misspelled it, not
/// as a puzzling failure once rendering has begun.  The rest waits for values to look at.
/// </para>
/// </summary>
public class FunctionCallTerm : Term
{
    private readonly string _name;
    private readonly List<Term> _arguments;

    public FunctionCallTerm(Token token, List<Term> arguments) : this(token, token.Text, arguments) {}

    /// <summary>
    /// This constructor is for a call a scene did not spell out as one: an operator that stands for
    /// a function, where the token is the symbol rather than the name.  <c>√x</c> is a call to
    /// <c>sqrt</c> and is treated as exactly that, so an operator cannot drift away from the
    /// function it is sugar for.
    /// </summary>
    /// <param name="token">The token to report errors against.</param>
    /// <param name="name">The name of the function being called.</param>
    /// <param name="arguments">The values the call supplies.</param>
    public FunctionCallTerm(Token token, string name, List<Term> arguments) : base(token)
    {
        _name = name;
        _arguments = arguments;

        // Checked here only when the name is one of the built-in functions.  A scene's own function
        // may not have been declared yet when a call to it is parsed -- and in any case what it takes
        // is not known to this catalog -- so those are checked when the call is actually made.
        if (FunctionCatalog.Instance.IsKnown(_name))
        {
            string problem = FunctionCatalog.Instance.CheckCall(_name, arguments.Count);

            if (problem != null)
                throw new TokenException(problem) { Token = token };
        }
    }

    /// <summary>
    /// This method reports whether this term calls the named function, which is how the parser tells
    /// one operator's sugar from another's after the fact.
    /// </summary>
    /// <param name="name">The function name to test for.</param>
    /// <returns><c>true</c>, if this term calls that function.</returns>
    internal bool IsCallTo(string name)
    {
        return _name == name;
    }

    /// <summary>
    /// This method is used to evaluate this term by calling the function it names.  Each of the
    /// call's values is evaluated first, since it is those that say which form of the function is
    /// meant.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object[] values = _arguments
            .Select(argument => argument.GetValue(variables))
            .ToArray();

        // A scene's own function is looked for first, so that a scene may name one as it likes without
        // having to know what the built-in catalog happens to hold.  It is found by the same walk out
        // through enclosing scopes that finds any other name, which is what makes it obey scope.
        if (variables.GetValue(_name, typeof(UserFunction)) is UserFunction own)
        {
            string wrong = own.CheckCall(values.Length);

            if (wrong != null)
                throw new TokenException(wrong) { Token = ErrorToken };

            return own.Call(values, ErrorToken);
        }

        if (!FunctionCatalog.Instance.IsKnown(_name))
        {
            throw new TokenException($"There is no function named '{_name}'.")
            {
                Token = ErrorToken
            };
        }

        FunctionMatch match = FunctionCatalog.Instance.Match(_name, values);

        if (!match.IsMatch)
            throw new TokenException(match.Error) { Token = ErrorToken };

        return match.Invoke();
    }

    /// <summary>
    /// This method is used to lower this call into a field expression.  A field deals in numbers
    /// throughout, so which form of the function is meant is settled here and for good -- and a form
    /// that wants anything else is turned down with the same message a scene would have seen, which
    /// is how a scene asking for length(vector) inside a function is told what is wrong.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <returns>This term, as a field expression.</returns>
    public override FieldExpression ToField(Variables variables)
    {
        // A scene's own function is folded in bodily -- its body lowered in place of the call, with
        // the call's values standing in for its parameters -- so that everything a field can do with
        // arithmetic it can still do, differentiation included.  That only works while the body is a
        // single expression; one with workings before its answer is a small procedure, and there is
        // no way to fold a procedure into arithmetic.
        if (variables.GetValue(_name, typeof(UserFunction)) is UserFunction own)
        {
            string wrong = own.CheckCall(_arguments.Count);

            if (wrong != null)
                throw new TokenException(wrong) { Token = ErrorToken };

            if (!own.MayBeFoldedIntoAField)
            {
                throw new TokenException(
                    $"The function '{_name}' works things out before its answer, so it cannot be " +
                    "used in a density or an isosurface; those need a single expression to fold in " +
                    "and to differentiate.")
                {
                    Token = ErrorToken
                };
            }

            // Lowered rather than worked out, since an argument may be a whole piece of field
            // arithmetic -- one shape function handed to another -- and there is no number to be had
            // from that until the field is actually asked about a place.
            object[] given = _arguments
                .Select(argument => (object) argument.ToField(variables))
                .ToArray();

            return own.Body.ToField(own.ScopeForFolding(given));
        }

        FieldExpression[] arguments = _arguments
            .Select(argument => argument.ToField(variables))
            .ToArray();
        (FunctionSignature signature, string error) = FunctionCatalog.Instance.ResolveForTypes(
            _name, arguments.Select(_ => typeof(double)).ToArray());

        if (signature is null)
            throw new TokenException(error) { Token = ErrorToken };

        if (signature.ReturnType != typeof(double))
        {
            throw new TokenException(
                $"In a function, '{_name}' must give back a number; this form gives back a " +
                $"{FunctionSignature.DslNameFor(signature.ReturnType)}.")
            {
                Token = ErrorToken
            };
        }

        return FieldCall.Of(signature, arguments, ErrorToken);
    }
}
