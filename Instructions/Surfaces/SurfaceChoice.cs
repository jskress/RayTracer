using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class holds a choice standing in a list of surfaces: a question, the things to make when the
/// answer is yes, and the things to make when it is not.
/// <para>
/// This is not the <c>if</c> that ends a function's body, though it is written with the same word, and
/// the difference is worth being plain about.  That one is the last thing in a body and both ways out
/// of it must give an answer, which is what makes "exactly one answer on every path" a fact about the
/// shape of the thing rather than something worked out afterwards.  This one stands among surfaces,
/// where the question is not what to answer but what to make, and <i>nothing</i> is a perfectly good
/// thing to make.  So the <c>else</c> is optional here, and a choice with none simply makes nothing
/// when the answer is no.
/// </para>
/// <para>
/// An <c>else</c> followed by another <c>if</c> is read as a choice standing alone where the second
/// arm would have been, which is how a run of cases is written down the page rather than off the right
/// of it.
/// </para>
/// </summary>
public class SurfaceChoice : SurfaceListEntry
{
    /// <summary>
    /// This property holds the question the choice asks.
    /// </summary>
    public Term Condition { get; init; }

    /// <summary>
    /// This property holds the token to complain about should the question not answer true or false.
    /// </summary>
    public Token ErrorToken { get; init; }

    /// <summary>
    /// This property holds what to make when the answer is yes.
    /// </summary>
    public List<ISurfaceResolver> WhenTrue { get; set; } = [];

    /// <summary>
    /// This property holds what to make when it is no, which is nothing at all when the choice was
    /// written without an <c>else</c>.
    /// </summary>
    public List<ISurfaceResolver> WhenFalse { get; set; } = [];

    /// <summary>
    /// This property holds what to call this when complaining that it is not a surface.
    /// </summary>
    protected override string Description => "A choice";

    /// <summary>
    /// This method asks the question and makes whichever set of things the answer calls for.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The scope of the list this stands in.</param>
    /// <param name="add">What to do with each surface it makes.</param>
    public override void AddSurfacesTo(
        RenderContext context, Variables variables, Action<Surface> add)
    {
        object asked = Condition.GetValue(variables);

        if (asked is not bool answer)
        {
            string given = asked is null ? "null" : FunctionSignature.DslNameFor(asked.GetType());

            throw new TokenException($"A condition must be true or false, not {given}.")
            {
                Token = ErrorToken
            };
        }

        AddAllTo(context, variables, answer ? WhenTrue : WhenFalse, add);
    }

    /// <summary>
    /// This method returns a copy of this choice, with lists of its own so that two copies cannot tread
    /// on each other.
    /// </summary>
    /// <returns>A copy of this choice.</returns>
    public override object Clone()
    {
        SurfaceChoice choice = (SurfaceChoice) MemberwiseClone();

        choice.WhenTrue = [..choice.WhenTrue];
        choice.WhenFalse = [..choice.WhenFalse];

        return choice;
    }
}
