using Lex.Tokens;
using RayTracer.General;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class resolves one call of a pigment a scene wrote for itself.
/// <para>
/// A pigment is named through an expression rather than through a clause of its own -- <c>pigment
/// stone</c> is the name <c>stone</c>, looked up -- so a call of one arrives as an expression too, and
/// an expression is worked out with nothing but the names in scope.  Making a pigment needs more than
/// that: it needs the render's own context.  So the call hands back this instead of a pigment, having
/// worked out its values, and whoever asked finishes the job when it has the context to do it with.
/// </para>
/// </summary>
public class PigmentCallResolver : IPigmentResolver
{
    /// <summary>
    /// This property holds the pigment being called, already belonging to the scope it was written in.
    /// </summary>
    public UserPrimitive Primitive { get; init; }

    /// <summary>
    /// This property holds the values the call supplied, already worked out among the caller's names.
    /// </summary>
    public IReadOnlyList<object> Arguments { get; init; }

    /// <summary>
    /// This property holds the token to hang any complaint on.
    /// </summary>
    public Token ErrorToken { get; init; }

    /// <summary>
    /// This property is not used here; a call carries no turbulence of its own, whatever the pigment
    /// it names may carry.
    /// </summary>
    public Resolver<int?> SeedResolver { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value as a pigment.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The pigment this call makes.</returns>
    public Pigment ResolveToPigment(RenderContext context, Variables variables)
    {
        (IObjectResolver recipe, Variables scope) = Primitive.ChooseFor(Arguments, ErrorToken);

        return (Pigment) recipe.ResolveToObject(context, scope);
    }

    /// <summary>
    /// This method is used to execute the resolver into a generic object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The pigment this call makes.</returns>
    public object ResolveToObject(RenderContext context, Variables variables)
    {
        return ResolveToPigment(context, variables);
    }
}
