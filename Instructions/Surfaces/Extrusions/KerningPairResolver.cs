using RayTracer.Extensions;
using RayTracer.Fonts;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces.Extrusions;

public class KerningPairResolver : ObjectResolver<KerningPair>
{
    /// <summary>
    /// This property holds the resolver for the left character property of the kerning
    /// pair.
    /// </summary>
    public Resolver<string> LeftCharacterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the right character property of the kerning
    /// pair.
    /// </summary>
    public Resolver<string> RightCharacterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the kern amount property of the kerning pair.
    /// </summary>
    public Resolver<short> KernCharacterResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a material.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, KerningPair value)
    {
        string left = LeftCharacterResolver.Resolve(context, variables);
        string right = RightCharacterResolver.Resolve(context, variables);

        value.Left = left.AsCodePoint();
        value.Right = right.AsCodePoint();

        KernCharacterResolver.AssignTo(value, target => target.Kern, context, variables);
    }
}
