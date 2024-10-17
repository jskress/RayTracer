using RayTracer.Fonts;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class is used to resolve a kerning pair collection value.
/// </summary>
public class KerningResolver : ObjectResolver<Kerning>
{
    /// <summary>
    /// This property holds the list of resolvers for the collection of kerning pairs to
    /// add.
    /// </summary>
    public List<KerningPairResolver> PairResolvers { get; } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a material.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Kerning value)
    {
        foreach (KerningPair pair in PairResolvers
                     .Select(resolver => resolver.Resolve(context, variables)))
        {
            value.AddKerning(pair.Left, pair.Right, pair.Kern);
        }
    }
}
