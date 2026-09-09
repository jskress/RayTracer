using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class resolves one call of a primitive a scene wrote for itself.
/// <para>
/// What it holds is whatever the call itself added, which belongs to that call and to no other.  What
/// it looks up when the time comes is the primitive, since only then is it known what the primitive's
/// body arrives at: a body may choose its answer from what it was given, so the recipe is not a thing
/// a call can be handed when it is read.
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
        Surface made = BuildOrShare(context, primitive, given);

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
            Name = Name, Arguments = Arguments, Extras = Extras, ErrorToken = ErrorToken
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

    /// <summary>
    /// This method makes what the call asked for, sharing one shape among every call that asked for
    /// exactly the same thing.
    /// <para>
    /// **What makes this safe is that a primitive's body cannot see the caller.**  Its scope is built
    /// on where the primitive was *declared*, so the only things that can change what the body makes
    /// are the values it was given -- and the one function that could have smuggled state in,
    /// <c>random</c>, is keyed by its arguments too.  Two calls with the same values therefore make
    /// the same shape, and one of them will do for both.
    /// </para>
    /// <para>
    /// The first call is wrapped the same way as every other, rather than being kept as the shape
    /// itself.  Nothing then depends on which call happened to come first.
    /// </para>
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="primitive">The primitive being called.</param>
    /// <param name="given">The values the call supplied.</param>
    /// <returns>The surface for this call.</returns>
    private Surface BuildOrShare(RenderContext context, UserPrimitive primitive, object[] given)
    {
        // **Only a primitive that gives back a group may be shared, and the reason is the block a call
        // may carry.**  That block is laid over whatever the call made, by a resolver that knows the
        // primitive's declared kind -- and it begins by checking the surface *is* that kind, giving up
        // quietly when it is not.  An instance is not a sphere, so a call of a sphere-making primitive
        // would have had its own `translate` dropped without a word, and two of the thing would stand
        // in the same place.  A group can hold an instance, so the call is handed a group as it always
        // was and the sharing hides inside it.
        //
        // That is no great loss: a primitive that makes anything worth building once makes a group.
        string key = primitive.Kind == "group" && !(Extras?.SetsMaterial ?? false)
            ? KeyFor(primitive, given)
            : null;

        if (key is not null && context.SharedShapes.TryGetValue(key, out Surface already))
            return new Group().Add(new Instance { Prototype = already });

        Surface built = Build(context, primitive, given);

        if (key is null || !Instance.MayBeShared(built))
            return built;

        context.SharedShapes[key] = built;

        return new Group().Add(new Instance { Prototype = built });
    }

    /// <summary>
    /// This method runs the primitive's body and hands back what it made.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="primitive">The primitive being called.</param>
    /// <param name="given">The values the call supplied.</param>
    /// <returns>The surface the body made.</returns>
    private Surface Build(RenderContext context, UserPrimitive primitive, object[] given)
    {
        (IObjectResolver recipe, Variables scope) = primitive.ChooseFor(given, ErrorToken);

        return ((ISurfaceResolver) recipe).ResolveToSurface(context, scope);
    }

    /// <summary>
    /// This method returns what a call asked for, written down, or <c>null</c> when it cannot be
    /// written down faithfully.
    /// <para>
    /// **Anything not recognised means no sharing, rather than a guess.**  Two different things whose
    /// text happens to match would be one shape standing for both, which is the one way this could go
    /// wrong quietly -- so the type is written down beside the value, and a value of a kind not
    /// listed here refuses the key outright.
    /// </para>
    /// </summary>
    /// <param name="primitive">The primitive being called.</param>
    /// <param name="given">The values the call supplied.</param>
    /// <returns>A key for the call, or <c>null</c> if one cannot be made.</returns>
    /// <remarks>
    /// This is internal rather than private so that it can be tested directly.  What it decides is
    /// invisible in a rendered image by design -- sharing that changed what was drawn would be a bug
    /// -- so there is nowhere else to see whether it got the answer right.
    /// </remarks>
    internal static string KeyFor(UserPrimitive primitive, object[] given)
    {
        StringBuilder key = new ();

        key.Append(RuntimeHelpers.GetHashCode(primitive)).Append(':').Append(primitive.Name);

        foreach (object value in given)
        {
            if (!TryWriteDown(value, key))
                return null;
        }

        return key.ToString();
    }

    /// <summary>
    /// This method writes one value into a key, if it is of a kind that can be written faithfully.
    /// </summary>
    /// <param name="value">The value to write down.</param>
    /// <param name="key">The key being built.</param>
    /// <returns><c>true</c>, if the value could be written down.</returns>
    private static bool TryWriteDown(object value, StringBuilder key)
    {
        key.Append('|').Append(value?.GetType().Name ?? "nothing").Append('=');

        switch (value)
        {
            case null:
                return true;
            case double number:
                key.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return true;
            case bool flag:
                key.Append(flag);
                return true;
            case string text:
                key.Append(text.Length).Append(':').Append(text);
                return true;
            case NumberTuple tuple:
                key.Append(tuple);
                return true;
            case Material material:
                // **A material is keyed by *which* one it is, not by what it looks like.**  Two calls
                // handed the same named material share; two handed different ones do not, even where
                // the two are identical in every property.  That errs in the safe direction -- a share
                // refused costs a rebuild, where a share granted wrongly paints a piece the wrong
                // colour.
                //
                // This is the identity hash, as the primitive itself is keyed a few lines above, and
                // carries the same caveat: two objects could in principle collide.  Without this case
                // a material fell through to the default and refused the key outright, so handing a
                // primitive its paint silently cost the sharing -- measured at 0.67s against 1.21s for
                // thirty-two calls of one shape.
                key.Append(RuntimeHelpers.GetHashCode(material));
                return true;
            case Sequence sequence:
                key.Append(sequence.Count).Append('(');

                foreach (object held in sequence.Values)
                {
                    if (!TryWriteDown(held, key))
                        return false;
                }

                key.Append(')');
                return true;
            default:
                return false;
        }
    }
}
