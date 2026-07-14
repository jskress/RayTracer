using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a blob value.
/// </summary>
public class BlobResolver : SurfaceResolver<Blob>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the threshold property on a blob.
    /// </summary>
    public Resolver<double> ThresholdResolver { get; set; }

    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of
    /// components that make up our blob.
    /// </summary>
    public List<IObjectResolver> ComponentResolvers { get; private set; } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a blob.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Blob value)
    {
        ThresholdResolver.AssignTo(value, target => target.Threshold, context, variables);

        value.Components.AddRange(ComponentResolvers
            .Select(resolver => (IBlobComponent) resolver.ResolveToObject(context, variables)));

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return ComponentResolvers is null || ComponentResolvers.Count == 0
            ? "A blob needs at least one sphere or cylinder component."
            : null;
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        BlobResolver resolver = (BlobResolver) base.Clone();

        // Force the list to be physically different, but with the same content.
        resolver.ComponentResolvers = [..resolver.ComponentResolvers];

        return resolver;
    }
}
