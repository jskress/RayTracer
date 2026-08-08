using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;
using RayTracer.Instructions;

namespace RayTracer.Terms;

/// <summary>
/// This class holds a thing a scene knows how to make: a named recipe for a surface, taking values
/// and giving back something to put in a scene.
/// <para>
/// It is a function's twin and differs in one way that decides everything else about it.  A function
/// gives back a number, which is a value, so it can be worked out wherever an expression stands.  This
/// gives back a <i>thing</i>, which in this renderer is never a value but a recipe -- a resolver, run
/// at render time to build the object it describes.  So where a function keeps an expression and works
/// it out per call, this keeps a resolver and resolves it per call, each in a scope of its own.
/// </para>
/// <para>
/// That difference is also why the kind it gives back has to be declared.  A call is read long before
/// anything is built, and the reader of a call -- the parser -- has to know what sort of thing is
/// coming in order to know what may be said about it in the block that follows.  A group takes group
/// clauses and a sphere takes sphere clauses, exactly as they do when a named one is reused.
/// </para>
/// </summary>
public class UserPrimitive
{
    /// <summary>
    /// This property holds what the primitive is called.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// This property holds the names of the values it takes, in order.
    /// </summary>
    public IReadOnlyList<string> ParameterNames { get; }

    /// <summary>
    /// This property holds what each value falls back to when a call leaves it out.
    /// </summary>
    public IReadOnlyList<Term> Defaults { get; }

    /// <summary>
    /// This property holds the kind of thing it was declared to give back -- a kind of surface, or
    /// a pigment.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// This property holds the recipe for what it gives back, as written.  A call takes a copy of
    /// this so that whatever the call adds in its own block belongs to that call alone.
    /// </summary>
    public FunctionBody Body { get; }

    /// <summary>
    /// This property holds how many values a call must supply.
    /// </summary>
    public int RequiredCount { get; }

    private readonly Variables _declaredIn;

    public UserPrimitive(
        string name, IReadOnlyList<string> parameterNames, IReadOnlyList<Term> defaults,
        string kind, FunctionBody body, Variables declaredIn = null)
    {
        Name = name;
        ParameterNames = parameterNames;
        Defaults = defaults;
        Kind = kind;
        Body = body;
        RequiredCount = defaults.Count(fallback => fallback is null);

        _declaredIn = declaredIn;
    }

    /// <summary>
    /// This method returns the same primitive belonging to the given scope.
    /// </summary>
    /// <param name="scope">The scope it is to belong to.</param>
    /// <returns>The primitive, belonging to that scope.</returns>
    public UserPrimitive BoundTo(Variables scope)
    {
        return new UserPrimitive(Name, ParameterNames, Defaults, Kind, Body, scope);
    }

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

        return $"'{Name}' takes {takes}, and was given {Describe(count)}.";
    }

    /// <summary>
    /// This method builds the scope one call is to be resolved in.
    /// <para>
    /// Its parent is where the primitive was <i>written</i> rather than where it was called, for the
    /// same reason a function's is: one kept in a library must see what its own file set up and must
    /// not be reachable by whatever the calling scene happens to have named.
    /// </para>
    /// </summary>
    /// <param name="arguments">The values the call supplies, already worked out.</param>
    /// <param name="errorToken">The token to hang any complaint on.</param>
    /// <returns>The recipe the body arrived at, and the scope to resolve it in.</returns>
    public (IObjectResolver Recipe, Variables Scope) ChooseFor(
        IReadOnlyList<object> arguments, Token errorToken)
    {
        Variables scope = new (_declaredIn);

        for (int index = 0; index < ParameterNames.Count; index++)
        {
            object value = index < arguments.Count
                ? arguments[index]
                : Defaults[index]?.GetValue(scope);

            if (value is null)
            {
                throw new TokenException($"'{Name}' was given nothing for '{ParameterNames[index]}'.")
                {
                    Token = errorToken
                };
            }

            scope.SetValue(ParameterNames[index], value);
        }

        (object answer, Variables reached) = Body.Follow(scope);

        return ((IObjectResolver) answer, reached);
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
