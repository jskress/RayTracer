using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve how a surface is bent along a spline.
/// </summary>
public class DeformationResolver : ObjectResolver<Deformation>
{
    /// <summary>
    /// This property holds the term that names the spline.
    /// </summary>
    public Term SplineTerm { get; set; }

    /// <summary>
    /// This property holds the resolver for where the centerline starts, or null for the origin.
    /// </summary>
    public Resolver<Point> FromResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the way the centerline runs.
    /// </summary>
    public Resolver<Vector> TowardResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the slices are turned about the spline by its end, or null.
    /// </summary>
    public Resolver<double> TwistResolver { get; set; }

    /// <summary>
    /// This property notes that the centerline is laid over the whole spline.
    /// </summary>
    public bool Stretched { get; set; }

    protected override void SetProperties(RenderContext context, Variables variables, Deformation value)
    {
        if (SplineTerm.GetValue(variables) is not Spline spline)
            throw new TokenException("What follows \"along\" must be a spline.") { Token = SplineTerm.ErrorToken };

        value.Spline = spline;
        value.From = FromResolver?.Resolve(context, variables);
        value.Toward = TowardResolver.Resolve(context, variables);
        value.Twist = TwistResolver?.Resolve(context, variables) ?? 0;
        value.Stretched = Stretched;
    }
}
