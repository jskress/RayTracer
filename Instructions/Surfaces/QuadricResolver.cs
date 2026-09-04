using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a general quadric value.
/// <para>
/// The ten coefficients arrive in the four groups they fall into naturally -- the squared terms, the
/// cross terms, the linear terms and the constant -- rather than as one run of ten numbers, since
/// nobody can count to the seventh coefficient of a list reliably.
/// </para>
/// </summary>
public class QuadricResolver : SurfaceResolver<Quadric>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the coefficients of x², y² and z².
    /// </summary>
    public Resolver<Vector> SquaresResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the coefficients of xy, xz and yz.
    /// </summary>
    public Resolver<Vector> CrossResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the coefficients of x, y and z.
    /// </summary>
    public Resolver<Vector> LinearResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the constant term.
    /// </summary>
    public Resolver<double> ConstantResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a quadric.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Quadric value)
    {
        if (SquaresResolver is not null)
        {
            Vector squares = SquaresResolver.Resolve(context, variables);

            (value.A, value.B, value.C) = (squares.X, squares.Y, squares.Z);
        }

        if (CrossResolver is not null)
        {
            Vector cross = CrossResolver.Resolve(context, variables);

            (value.D, value.E, value.F) = (cross.X, cross.Y, cross.Z);
        }

        if (LinearResolver is not null)
        {
            Vector linear = LinearResolver.Resolve(context, variables);

            (value.G, value.H, value.I) = (linear.X, linear.Y, linear.Z);
        }

        ConstantResolver.AssignTo(value, target => target.J, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// <para>
    /// A box is required, as it is for an isosurface: most quadrics are endless, and nothing about
    /// ten coefficients says where to stop looking.  Left out, the surface would be tested by every
    /// ray in the scene and drawn wherever it happened to reach.
    /// </para>
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (SquaresResolver is null && CrossResolver is null && LinearResolver is null)
            return "A quadric needs at least one of \"squares\", \"cross\" or \"linear\".";

        return BoundingBoxResolver is null
            ? "A quadric needs a \"bounded by\", since most of them are endless."
            : null;
    }
}
