using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class holds a function a scene wrote for itself.
/// <para>
/// It is deliberately a value like any other, kept under its name in the same place a scene's numbers
/// and colors are kept.  Two things fall out of that and both are wanted.  A function is found by the
/// same walk outward through enclosing scopes that finds anything else, so a function written in an
/// included file is visible to the scene that included it and a function written inside something is
/// not visible outside it -- which is what lexical scope means, had for nothing.  And a call can tell
/// a scene's own function from one of the built-in ones by looking, rather than by keeping a second
/// register of names in step with the first.
/// </para>
/// <para>
/// What it holds is a <i>recipe</i> and never a result.  The body is kept as the expression it was
/// written as and worked out afresh at every call, against that call's own values.  Compiling it down
/// to something fixed would be quicker by a hair and would cost the thing this was built for: a value
/// that changes between calls -- the frame's time, when animation arrives -- has to be able to reach
/// in.
/// </para>
/// </summary>
public class UserFunction
{
    /// <summary>
    /// This property holds what the function is called.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// This property holds the names of the values the function takes, in the order it takes them.
    /// </summary>
    public IReadOnlyList<string> ParameterNames { get; }

    /// <summary>
    /// This property holds what each value falls back to when a call leaves it out, or <c>null</c>
    /// where a call must supply it.
    /// </summary>
    public IReadOnlyList<Term> Defaults { get; }

    /// <summary>
    /// This property holds the kind of thing the function was declared to hand back.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// This property holds how many of the values a call is obliged to supply, the rest having
    /// something to fall back to.
    /// </summary>
    public int RequiredCount { get; }

    private readonly FunctionBody _body;
    private readonly Variables _declaredIn;

    public UserFunction(
        string name, IReadOnlyList<string> parameterNames, IReadOnlyList<Term> defaults,
        string kind, FunctionBody body, Variables declaredIn = null)
    {
        Name = name;
        ParameterNames = parameterNames;
        Defaults = defaults;
        Kind = kind;
        RequiredCount = defaults.Count(fallback => fallback is null);

        _body = body;
        _declaredIn = declaredIn;
    }

    /// <summary>
    /// This method returns the same function belonging to the given scope.
    /// <para>
    /// A function is parsed once and may then be declared many times over -- once for each call of
    /// whatever surrounds it, in the case of one written inside another.  What differs between those
    /// is only which scope its body is worked out against, so that is the one thing handed over here;
    /// everything else is shared, having been settled while parsing.
    /// </para>
    /// </summary>
    /// <param name="scope">The scope the function is to belong to.</param>
    /// <returns>The function, belonging to that scope.</returns>
    public UserFunction BoundTo(Variables scope)
    {
        return new UserFunction(Name, ParameterNames, Defaults, Kind, _body, scope);
    }

    /// <summary>
    /// This property reports whether the function may be folded into a field -- a density's shape or
    /// an isosurface's -- which it may only when its body is a single answer: no workings before it,
    /// and no choice.
    /// <para>
    /// The reason is not fussiness.  A field is compiled down to arithmetic over a place and asked
    /// about millions of them, and an isosurface's is differentiated besides, to find which way its
    /// surface faces.  A plain expression can be folded straight in and both of those go on working.
    /// Anything more has nowhere to be folded into: there is no expression to fold, only a small
    /// procedure, and a procedure cannot be differentiated.
    /// </para>
    /// </summary>
    public bool MayBeFoldedIntoAField => _body.IsASingleAnswer;

    /// <summary>
    /// This method returns the function's body with the call's values put in place of its parameter
    /// names, ready to be folded into a field.
    /// </summary>
    /// <param name="arguments">The values the call supplies, already lowered.</param>
    /// <returns>The scope the body should be lowered against.</returns>
    public Variables ScopeForFolding(IReadOnlyList<object> arguments)
    {
        Variables scope = new (_declaredIn);

        for (int index = 0; index < ParameterNames.Count; index++)
        {
            object value = index < arguments.Count ? arguments[index] : Defaults[index]?.GetValue(scope);

            if (value is not null)
                scope.SetValue(ParameterNames[index], value);
        }

        return scope;
    }

    /// <summary>
    /// This property holds the one expression the function comes back with, for folding into a field.
    /// It is only meaningful when <see cref="MayBeFoldedIntoAField"/> says so, a body that works
    /// things out or chooses having no single expression to hand back.
    /// </summary>
    public Term FoldableBody => (Term) _body.Answer;

    /// <summary>
    /// This method reports what is wrong with a call of the given size, or <c>null</c> if nothing is.
    /// </summary>
    /// <param name="count">How many values the call supplies.</param>
    /// <returns>What is wrong, or <c>null</c>.</returns>
    public string CheckCall(int count)
    {
        if (count >= RequiredCount && count <= ParameterNames.Count)
            return null;

        string takes = RequiredCount == ParameterNames.Count
            ? Describe(ParameterNames.Count)
            : $"between {RequiredCount} and {ParameterNames.Count} values";

        return $"The function '{Name}' takes {takes}, and was given {Describe(count)}.";
    }

    /// <summary>
    /// This method works the function out for one call.
    /// <para>
    /// The call gets a scope of its own, and its parent is the scope the function was <i>written</i>
    /// in rather than the one it was called from.  That difference is the whole of why a function may
    /// be trusted: one written in a library sees what its own file set up, and cannot be quietly
    /// changed by whatever names the calling scene happens to have about.
    /// </para>
    /// </summary>
    /// <param name="arguments">The values the call supplies.</param>
    /// <param name="errorToken">The token to hang any complaint on.</param>
    /// <returns>What the function works out to.</returns>
    public object Call(IReadOnlyList<object> arguments, Token errorToken)
    {
        Variables scope = new (_declaredIn);

        for (int index = 0; index < ParameterNames.Count; index++)
        {
            object value = index < arguments.Count
                ? arguments[index]
                : Defaults[index]?.GetValue(scope);

            if (value is null)
            {
                throw new TokenException(
                    $"The function '{Name}' was given nothing for '{ParameterNames[index]}'.")
                {
                    Token = errorToken
                };
            }

            scope.SetValue(ParameterNames[index], value);
        }

        (object answer, Variables reached) = _body.Follow(scope);

        return ((Term) answer).GetValue(reached);
    }

    /// <summary>
    /// This method says a count of values the way a sentence wants it.
    /// </summary>
    /// <param name="count">The count to describe.</param>
    /// <returns>The count, in words.</returns>
    private static string Describe(int count)
    {
        return count == 1 ? "1 value" : $"{count} values";
    }
}
