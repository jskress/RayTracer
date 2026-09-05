using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class is used to resolve an extrusion whose outline is scaled as it rises.
/// <para>
/// It gives back the same sort of surface an ordinary extrusion does, and differs only in taking
/// a taper.  Having it as a kind of its own is what lets the parser know, on reading the block's
/// first word, whether a taper is a thing this block may say.
/// </para>
/// </summary>
public class TaperedExtrusionResolver : ExtrusionResolver
{
    /// <summary>
    /// This property holds the resolver for the taper property on an extrusion.
    /// </summary>
    public Resolver<double> TaperResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an
    /// extrusion.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Extrusion value)
    {
        TaperResolver.AssignTo(value, target => target.Taper, context, variables);

        base.SetProperties(context, variables, value);
    }
}
