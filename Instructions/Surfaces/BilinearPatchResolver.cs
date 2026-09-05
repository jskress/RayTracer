using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a bilinear patch value.
/// </summary>
public class BilinearPatchResolver : SurfaceResolver<BilinearPatch>, IValidatable
{
    /// <summary>
    /// This property holds the resolvers for the patch's four corners.
    /// </summary>
    public Resolver<Point>[] CornerResolvers { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a bilinear
    /// patch.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, BilinearPatch value)
    {
        if (CornerResolvers is not null)
        {
            value.Corners = CornerResolvers
                .Select(resolver => resolver.Resolve(context, variables))
                .ToArray();
        }

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return CornerResolvers is null
            ? "The \"points\" property is required."
            : null;
    }
}
