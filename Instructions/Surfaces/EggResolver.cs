using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve an egg value.
/// </summary>
public class EggResolver : SurfaceResolver<Egg>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the bottom radius property of our egg.
    /// </summary>
    public Resolver<double> BottomRadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the top radius property of our egg.
    /// </summary>
    public Resolver<double> TopRadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an egg.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Egg value)
    {
        BottomRadiusResolver.AssignTo(value, target => target.BottomRadius, context, variables);
        TopRadiusResolver.AssignTo(value, target => target.TopRadius, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return BottomRadiusResolver is null || TopRadiusResolver is null
            ? "The \"radii\" property is required."
            : null;
    }
}
