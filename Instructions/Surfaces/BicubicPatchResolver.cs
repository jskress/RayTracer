using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a bicubic patch value.
/// </summary>
public class BicubicPatchResolver : SurfaceResolver<BicubicPatch>, IValidatable
{
    /// <summary>
    /// This property holds the resolvers for the patch's 4x4 grid of control points.
    /// </summary>
    public Resolver<Point>[,] PointResolvers { get; set; }

    /// <summary>
    /// This property holds the resolver for the patch's "u" step limit.
    /// </summary>
    public Resolver<int> UStepsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the patch's "v" step limit.
    /// </summary>
    public Resolver<int> VStepsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the patch's flatness tolerance.
    /// </summary>
    public Resolver<double> FlatnessResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a bicubic
    /// patch.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, BicubicPatch value)
    {
        if (PointResolvers is not null)
        {
            Point[,] points = new Point[4, 4];

            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                points[i, j] = PointResolvers[i, j].Resolve(context, variables);

            value.ControlPoints = points;
        }

        UStepsResolver.AssignTo(value, target => target.USteps, context, variables);
        VStepsResolver.AssignTo(value, target => target.VSteps, context, variables);
        FlatnessResolver.AssignTo(value, target => target.Flatness, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return PointResolvers is null
            ? "The \"points\" property is required."
            : null;
    }
}
