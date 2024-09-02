using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a triangle value.
/// </summary>
public class TriangleResolver<TValue> : SurfaceResolver<TValue>, IValidatable
    where TValue : Triangle, new()
{
    /// <summary>
    /// This property holds the resolver for the point 1 property on a triangle.
    /// </summary>
    public Resolver<Point> Point1Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the point 2 property on a triangle.
    /// </summary>
    public Resolver<Point> Point2Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the point 3 property on a triangle.
    /// </summary>
    public Resolver<Point> Point3Resolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an
    /// extruded surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        Point1Resolver.AssignTo(value, target => target.Point1, context, variables);
        Point2Resolver.AssignTo(value, target => target.Point2, context, variables);
        Point3Resolver.AssignTo(value, target => target.Point3, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public virtual string Validate()
    {
        return Point1Resolver is null || Point2Resolver is null || Point3Resolver is null
            ? "The \"points\" property is required."
            : null;
    }
}

/// <summary>
/// This class is used to resolve a triangle value.
/// </summary>
public class TriangleResolver : TriangleResolver<Triangle>;
