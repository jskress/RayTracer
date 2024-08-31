using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a map of pigments value, represented by a pigment set.
/// </summary>
public class PigmentMapResolver : ObjectResolver<PigmentSet>
{
    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of break
    /// values for our pigment set.
    /// </summary>
    public List<Resolver<double>> BreakValueResolvers { get; set; }

    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of pigments
    /// for our pigment set.
    /// </summary>
    public List<IPigmentResolver> PigmentResolvers { get; set; }

    /// <summary>
    /// This property holds the resolver for the banded property of our pigment set.
    /// </summary>
    public Resolver<bool> BandedResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an agate
    /// pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, PigmentSet value)
    {
        for (int index = 0; index < PigmentResolvers.Count; index++)
        {
            double breakValue = BreakValueResolvers[index].Resolve(context, variables);
            Pigment pigment = PigmentResolvers[index].ResolveToPigment(context, variables);
            
            value.AddEntry(pigment, breakValue);
        }

        BandedResolver.AssignTo(value, target => target.Banded, context, variables);
    }
}
