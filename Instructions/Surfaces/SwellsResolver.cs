using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a body of water.
/// </summary>
public class SwellsResolver : SurfaceResolver<Swells>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for how far the water reaches along X.
    /// </summary>
    public Resolver<double> AcrossResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the water reaches along Z.
    /// </summary>
    public Resolver<double> AlongResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how closely a crossing is pinned down.
    /// </summary>
    public Resolver<double> AccuracyResolver { get; set; }

    /// <summary>
    /// This property holds the resolvers for the wave trains the water carries.
    /// </summary>
    public List<IObjectResolver> TrainResolvers { get; private set; } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the water.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Swells value)
    {
        AcrossResolver.AssignTo(value, target => target.Across, context, variables);
        AlongResolver.AssignTo(value, target => target.Along, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        value.Trains.AddRange(TrainResolvers
            .Select(resolver => (SwellTrain) resolver.ResolveToObject(context, variables)));

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return TrainResolvers is null || TrainResolvers.Count == 0
            ? "A body of water needs at least one train of waves."
            : null;
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        SwellsResolver resolver = (SwellsResolver) base.Clone();

        resolver.TrainResolvers = [..resolver.TrainResolvers];

        return resolver;
    }
}
