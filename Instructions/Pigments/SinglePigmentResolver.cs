using Lex.Parser;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a value that is either a solid color or a pigment
/// referenced by a variable.
/// </summary>
public class SinglePigmentResolver : PigmentResolver<Pigment>
{
    /// <summary>
    /// This property holds the term we are to use to resolve our value.
    /// </summary>
    public Term Term { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Pigment Resolve(RenderContext context, Variables variables)
    {
        object result = Term.GetValue(
            variables, typeof(Color), typeof(Pigment), typeof(PatternPigment),
            typeof(BlendedPigment), typeof(NoisyPigment), typeof(SolidPigment));

        if (result == null)
        {
            throw new TokenException("Could not resolve this to a color or pigment.")
            {
                Token = Term.ErrorToken
            };
        }

        return result switch
        {
            Color color => new SolidPigment(color),
            IPigmentResolver resolver => resolver.ResolveToPigment(context, variables),
            Pigment pigment => pigment,
            _ => throw new TokenException("Could not resolve this to a color or pigment.")
        };
    }
}
