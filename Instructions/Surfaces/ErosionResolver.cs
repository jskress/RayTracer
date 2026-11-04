using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve how a height field's ground has been worn: its rivers, its scree and its rain.
/// </summary>
public class ErosionResolver : ObjectResolver<Erosion>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for how far the rivers have cut back into the land.
    /// </summary>
    public Resolver<double> RiversResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how long the crags have been shedding scree.
    /// </summary>
    public Resolver<double> ScreeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the steepest angle loose rock rests at.
    /// </summary>
    public Resolver<double> ReposeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many drops of rain fall for each point of the grid.
    /// </summary>
    public Resolver<double> RainResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the level of the sea.
    /// </summary>
    public Resolver<double?> SeaLevelResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the seed the rain falls by.
    /// </summary>
    public Resolver<int?> SeedResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the erosion.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The erosion to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Erosion value)
    {
        RiversResolver.AssignTo(value, target => target.Rivers, context, variables);
        ScreeResolver.AssignTo(value, target => target.Scree, context, variables);
        ReposeResolver.AssignTo(value, target => target.Repose, context, variables);
        RainResolver.AssignTo(value, target => target.Rain, context, variables);
        SeaLevelResolver.AssignTo(value, target => target.SeaLevel, context, variables);
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or <c>null</c>, if
    /// all is well.  An erosion must wear the ground some way, and an angle of repose is the scree's and the
    /// rivers' valleys'.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (RiversResolver is null && ScreeResolver is null && RainResolver is null)
            return "An erosion must say at least one of \"rivers\", \"scree\" or \"rain\".";

        return ReposeResolver is not null && ScreeResolver is null && RiversResolver is null
            ? "The \"repose\" angle is the angle scree and the sides of a river's valley rest at, so it needs " +
              "\"scree\" or \"rivers\"."
            : null;
    }
}
