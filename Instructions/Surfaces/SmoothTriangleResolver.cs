using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a triangle value.
/// </summary>
public class SmoothTriangleResolver : TriangleResolver<SmoothTriangle>
{
    /// <summary>
    /// This property holds the resolver for the normal 1 property on a triangle.
    /// </summary>
    public Resolver<Vector> Normal1Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the normal 2 property on a triangle.
    /// </summary>
    public Resolver<Vector> Normal2Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the normal 3 property on a triangle.
    /// </summary>
    public Resolver<Vector> Normal3Resolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an
    /// extruded surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, SmoothTriangle value)
    {
        Normal1Resolver.AssignTo(value, target => target.Normal1, context, variables);
        Normal2Resolver.AssignTo(value, target => target.Normal2, context, variables);
        Normal3Resolver.AssignTo(value, target => target.Normal3, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public override string Validate()
    {
        return base.Validate() ?? (Normal1Resolver is null || Normal2Resolver is null || Normal3Resolver is null
            ? "The \"normals\" property is required."
            : null);
    }
}
