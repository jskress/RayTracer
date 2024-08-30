using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a list of pigments value, represented by a pigment set.
/// </summary>
public class PigmentListResolver : ObjectResolver<PigmentSet>
{
    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of pigments
    /// for our pigment set.
    /// </summary>
    public List<IPigmentResolver> PigmentResolvers { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an agate
    /// pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, PigmentSet value)
    {
        foreach (IPigmentResolver resolver in PigmentResolvers)
            value.AddEntry(resolver.ResolveToPigment(context, variables));

        value.Banded = false;
    }
}
