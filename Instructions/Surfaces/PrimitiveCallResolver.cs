using Lex.Tokens;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class resolves one call of a primitive a scene wrote for itself.
/// <para>
/// What it holds is a copy of the primitive's recipe, taken where the call was written, so that
/// anything the call added in its own block belongs to that call and to no other.  What it looks up
/// when the time comes is the primitive itself, and only for the scope it was written in -- which is
/// the one thing a call cannot know for itself, scopes not existing until a render is under way.
/// </para>
/// </summary>
public class PrimitiveCallResolver : ISurfaceResolver
{
    /// <summary>
    /// This property holds the name of the primitive being called.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the values the call supplies.
    /// </summary>
    public List<Term> Arguments { get; init; }

    /// <summary>
    /// This property holds the primitive's recipe, as written.  It is shared rather than copied,
    /// since resolving it makes a new thing every time in any case.
    /// </summary>
    public ISurfaceResolver Body { get; init; }

    /// <summary>
    /// This property holds whatever this call added in a block of its own, or <c>null</c> if it added
    /// nothing.  It is kept apart from the body deliberately: the two are resolved against different
    /// sets of names, the body against where the primitive was written and this against where the
    /// call was.
    /// </summary>
    public ISurfaceResolver Extras { get; init; }

    /// <summary>
    /// This property holds the token to hang any complaint on.
    /// </summary>
    public Token ErrorToken { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce the surface the call describes.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The surface this call makes.</returns>
    public Surface ResolveToSurface(RenderContext context, Variables variables)
    {
        if (variables.GetValue(Name, typeof(UserPrimitive)) is not UserPrimitive primitive)
            throw new Exception($"Internal error: nothing named {Name} is a primitive.");

        object[] given = Arguments
            .Select(argument => argument.GetValue(variables))
            .ToArray();
        Surface made = Body.ResolveToSurface(context, primitive.ScopeFor(given, ErrorToken));

        // And now whatever the call itself said, in the names the call was written among -- which is
        // how a loop may place a row of these by saying `translate X step` after the call.
        Extras?.ApplyToSurface(context, variables, made);

        return made;
    }

    /// <summary>
    /// This method lays what a call says over a surface already made, which a call itself never has
    /// occasion to do -- a call is a thing to be made, not something to lay over another.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="surface">The surface already made.</param>
    public void ApplyToSurface(RenderContext context, Variables variables, Surface surface)
    {
        throw new NotSupportedException("A call cannot be laid over another surface.");
    }

    /// <summary>
    /// This method returns a copy of this call.  A call is already its own copy of the recipe, so
    /// there is nothing here that two callers could tread on.
    /// </summary>
    /// <returns>A copy of this call.</returns>
    public object Clone()
    {
        return new PrimitiveCallResolver
        {
            Name = Name, Arguments = Arguments, Body = Body, Extras = Extras,
            ErrorToken = ErrorToken
        };
    }

    /// <summary>
    /// This method is used to execute the resolver into a generic object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The surface this call makes.</returns>
    public object ResolveToObject(RenderContext context, Variables variables)
    {
        return ResolveToSurface(context, variables);
    }
}
