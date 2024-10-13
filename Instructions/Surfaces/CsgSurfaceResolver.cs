using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a CSG surface value.
/// </summary>
public class CsgSurfaceResolver : SurfaceResolver<CsgSurface>, IValidatable
{
    /// <summary>
    /// This property notes the type of CSG operation that should be performed.
    /// one.
    /// </summary>
    public CsgOperation Operation { get; init; }

    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of surfaces
    /// for our CSG operation.
    /// </summary>
    public List<ISurfaceResolver> SurfaceResolvers { get; private set; } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a CSG
    /// surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, CsgSurface value)
    {
        List<Surface> surfaces = SurfaceResolvers
            .Select(resolver => resolver.ResolveToSurface(context, variables))
            .ToList();

        value.Operation = Operation;

        SetChildren(value, surfaces);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method is used to create the proper CSG operation tree from the given list of
    /// surfaces.
    /// </summary>
    /// <param name="parent">The CSG surface to set the left and right surfaces for.</param>
    /// <param name="surfaces">The list of surfaces to pull from.</param>
    private void SetChildren(CsgSurface parent, List<Surface> surfaces)
    {
        while (surfaces.Count > 2)
        {
            CsgSurface child = new CsgSurface
            {
                Operation = Operation,
                Left = surfaces[0],
                Right = surfaces[1]
            };

            surfaces.RemoveFirst();

            surfaces[0] = child;
        }

        parent.Left = surfaces[0];
        parent.Right = surfaces[1];
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return SurfaceResolvers is null || SurfaceResolvers.Count < 2
            ? $"Not enough surfaces provided to perform the {Operation.ToString().ToLower()} operation."
            : null;
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        CsgSurfaceResolver resolver = (CsgSurfaceResolver) base.Clone();

        // Force the lists to be physically different, but with the same content.
        resolver.SurfaceResolvers = [..resolver.SurfaceResolvers];

        return resolver;
    }
}
