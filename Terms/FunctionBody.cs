using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class holds what a function or a primitive does: some things worked out along the way, and
/// then either an answer or a choice between two more of these.
/// <para>
/// A body always ends in an answer -- never merely trails off -- and a choice is a <i>terminal</i>
/// thing rather than a statement with more after it.  That is a deliberate shape, and it buys two
/// things.  The promise that every path gives back exactly one result stops being an analysis and
/// becomes the grammar: there is nowhere for a second answer to go and nowhere for a missing one to
/// hide.  And names worked out inside a branch cannot leak, since there is no "after the branch" for
/// them to leak into.
/// </para>
/// <para>
/// What it costs is that a value which merely differs by some condition -- a size, a color -- cannot
/// be settled with a branch and then used once.  Both arms would have to repeat the whole answer.  The
/// cure for that is the conditional, which is an expression and so may stand anywhere a value does; a
/// choice is for when the two ways out are genuinely two different answers.  (In a field, neither is
/// available: that language holds arithmetic on numbers and has nothing to choose with.)
/// </para>
/// </summary>
public class FunctionBody
{
    /// <summary>
    /// This property holds the things worked out on the way, in the order they were written.
    /// </summary>
    public List<FunctionBodyStep> Steps { get; init; } = [];

    /// <summary>
    /// This property holds what the body gives back -- an expression for a function, a recipe for a
    /// primitive -- or <c>null</c> when it chooses between two others instead.
    /// </summary>
    public object Answer { get; init; }

    /// <summary>
    /// This property holds what is asked when the body chooses, or <c>null</c> when it does not.
    /// </summary>
    public Term Condition { get; init; }

    /// <summary>
    /// This property holds the token to hang a complaint about that condition on.
    /// </summary>
    public Token ErrorToken { get; init; }

    /// <summary>
    /// This property holds the body followed when the answer to that is yes.
    /// </summary>
    public FunctionBody WhenTrue { get; init; }

    /// <summary>
    /// This property holds the body followed when it is no.
    /// </summary>
    public FunctionBody WhenFalse { get; init; }

    /// <summary>
    /// This method carries the body out as far as its answer, and hands back both that answer and the
    /// scope it was arrived at in.
    /// <para>
    /// The scope comes back with it because the answer has not been worked out yet -- it is an
    /// expression, or a recipe -- and whoever finishes it needs the names it was written among.
    /// </para>
    /// </summary>
    /// <param name="scope">The scope to work in, already holding the values it was called with.</param>
    /// <returns>The answer, and the scope to make sense of it in.</returns>
    public (object Answer, Variables Scope) Follow(Variables scope)
    {
        foreach (FunctionBodyStep step in Steps)
            step.CarryOut(scope);

        if (Condition is null)
            return (Answer, scope);

        object asked = Condition.GetValue(scope);

        if (asked is not bool choice)
        {
            string given = asked is null ? "null" : FunctionSignature.DslNameFor(asked.GetType());

            throw new TokenException($"A condition must be true or false, not {given}.")
            {
                Token = ErrorToken
            };
        }

        FunctionBody taken = choice ? WhenTrue : WhenFalse;

        // The branch gets a scope of its own, so what it works out belongs to it.
        return taken.Follow(new Variables(scope));
    }

    /// <summary>
    /// This property reports whether the body is a single answer with nothing worked out first, which
    /// is the only shape a field can fold in: a field compiles its arithmetic down and, for an
    /// isosurface, differentiates it, and neither can be done to a small procedure.
    /// </summary>
    public bool IsASingleAnswer => Steps.Count == 0 && Condition is null;
}
