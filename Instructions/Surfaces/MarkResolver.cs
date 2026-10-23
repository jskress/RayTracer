using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a mark: a named place, in the space of whatever holds it.
/// </summary>
public class MarkResolver : SurfaceResolver<Mark>
{
    /// <summary>
    /// This property holds the term for the mark's name.
    /// </summary>
    public Term NameTerm { get; set; }

    /// <summary>
    /// This property holds the resolver for where the mark stands.
    /// </summary>
    public Resolver<Point> AtResolver { get; set; }

    protected override void SetProperties(RenderContext context, Variables variables, Mark value)
    {
        base.SetProperties(context, variables, value);

        Point at = AtResolver.Resolve(context, variables);

        value.Name = NameTerm.GetValue<string>(variables);
        value.Transform = RayTracer.Basics.Transforms.Translate(at.X, at.Y, at.Z);
    }
}
