using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a run of keys into the value that changes over time between them.  Each key's
/// time and value are worked out where the keys are named, so a key may be written in terms of anything named
/// before it.
/// </summary>
public class KeysResolver : Resolver<Keyframes>, IObjectResolver
{
    /// <summary>
    /// This property holds the keys as written: each a time, a value, and the way the value comes to it.
    /// </summary>
    public List<(Term Time, Term Value, Keyframes.Way Way)> Keys { get; init; } = [];

    /// <summary>
    /// This property notes whether the value passes smoothly through every key.
    /// </summary>
    public bool Smooth { get; init; }

    /// <summary>
    /// This property notes whether the keys begin again once the last is reached.
    /// </summary>
    public bool Loops { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Keyframes Resolve(RenderContext context, Variables variables)
    {
        List<(double Time, object Value, Keyframes.Way Way)> keys = [];

        foreach ((Term time, Term value, Keyframes.Way way) in Keys)
        {
            // The value is taken as it comes, so that a tuple stays a tuple and only a color written as one is one.
            double at = time.GetValue<double>(variables);
            object worth = value.GetValue(variables);

            if (keys.Count > 0 && at <= keys[^1].Time)
                throw new TokenException("Each key must come later than the one before it.") { Token = time.ErrorToken };

            if (worth is not (double or Color or NumberTuple))
            {
                throw new TokenException("A key's value must be a number, a point or vector, or a color.")
                {
                    Token = value.ErrorToken
                };
            }

            if (keys.Count > 0 && worth.GetType() != keys[0].Value.GetType())
            {
                throw new TokenException("Every key's value must be the same kind of thing as the first's.")
                {
                    Token = value.ErrorToken
                };
            }

            keys.Add((at, worth, way));
        }

        return new Keyframes { Keys = keys, Smooth = Smooth, Loops = Loops };
    }

    /// <summary>
    /// This method is used to execute the resolver to produce a value as an object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public object ResolveToObject(RenderContext context, Variables variables) => Resolve(context, variables);
}
