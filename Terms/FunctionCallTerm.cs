using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;

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

    internal FunctionCallTerm(Token token, List<Term> arguments) : this(token, token.Text, arguments) {}

    /// <summary>
    /// This constructor is for a call a scene did not spell out as one: an operator that stands for
    /// a function, where the token is the symbol rather than the name.  <c>√x</c> is a call to
    /// <c>sqrt</c> and is treated as exactly that, so an operator cannot drift away from the
    /// function it is sugar for.
    /// </summary>
    /// <param name="token">The token to report errors against.</param>
    /// <param name="name">The name of the function being called.</param>
    /// <param name="arguments">The values the call supplies.</param>
    internal FunctionCallTerm(Token token, string name, List<Term> arguments) : base(token)
    {
        _name = name;
        _arguments = arguments;

        string problem = FunctionCatalog.Instance.CheckCall(_name, arguments.Count);

        if (problem != null)
            throw new TokenException(problem) { Token = token };
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
        FunctionMatch match = FunctionCatalog.Instance.Match(_name, values);

        if (!match.IsMatch)
            throw new TokenException(match.Error) { Token = ErrorToken };

        return match.Invoke();
    }
}
