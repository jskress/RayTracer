using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a catenary: a spline hung between two places, by its sag or by its length.  See
/// <see cref="Catenary"/>.
/// </summary>
public class CatenaryResolver : ObjectResolver<Spline>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for where the cable hangs from.
    /// </summary>
    public Resolver<Point> FromResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for where it hangs to.
    /// </summary>
    public Resolver<Point> ToResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far its middle hangs below the straight line between its ends.
    /// </summary>
    public Resolver<double> SagResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how long the cable is.
    /// </summary>
    public Resolver<double> LengthResolver { get; set; }

    /// <summary>
    /// This method hangs the cable and lays the curve it hangs in into the spline.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The spline to fill.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Spline value)
    {
        Spline hung = Catenary.Hung(
            FromResolver.Resolve(context, variables), ToResolver.Resolve(context, variables),
            SagResolver?.Resolve(context, variables), LengthResolver?.Resolve(context, variables));

        value.Start = hung.Start;
        value.Segments.AddRange(hung.Segments);
    }

    /// <summary>
    /// This method makes sure the catenary says where it hangs and how.
    /// </summary>
    /// <returns>The text of an error message, or <c>null</c>, if all is well.</returns>
    public string Validate()
    {
        if (FromResolver is null || ToResolver is null)
            return "A catenary needs the places it hangs from and to.";

        return (SagResolver is null) == (LengthResolver is null)
            ? "A catenary hangs by its sag or by its length; say one of them."
            : null;
    }
}
