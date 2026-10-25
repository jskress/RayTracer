using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This class creates a joint's turn: about its pivot, by its angle about its axis, held within its limits.  It
/// stands first among the joint's transforms, so the joint is turned in its own space and then placed as they say,
/// and because it is one of them, a joint turned in terms of the time blurs as any timed placement does.
/// </summary>
public class JointCreator : TransformCreator
{
    /// <summary>
    /// This property holds the resolver for the place the joint turns about, in its own space.
    /// </summary>
    public Resolver<Point> PivotResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the axis the joint turns about, or null.
    /// </summary>
    public Resolver<Vector> AxisResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far it is turned, in radians, or null.
    /// </summary>
    public Resolver<double> AngleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the least it may be turned, in radians, or null.
    /// </summary>
    public Resolver<double> LowestResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the most it may be turned, in radians, or null.
    /// </summary>
    public Resolver<double> HighestResolver { get; set; }

    /// <summary>
    /// This method returns the joint's turn, the same whatever share of a motion is asked for, since a turn about a
    /// pivot is no motion of its own.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="fraction">How far through a motion, which a joint's turn does not heed.</param>
    /// <returns>The turn.</returns>
    public override Matrix ResolveAt(RenderContext context, Variables variables, double fraction)
    {
        Vector axis = AxisResolver?.Resolve(context, variables);

        if (axis is null || AngleResolver is null)
            return Matrix.Identity;

        double angle = Limited(AngleResolver.Resolve(context, variables), context, variables);
        Point pivot = PivotResolver?.Resolve(context, variables) ?? Point.Zero;

        return RayTracer.Basics.Transforms.Translate(pivot.X, pivot.Y, pivot.Z) *
               RayTracer.Core.Physics.Turn.About(axis.Unit, angle).ToMatrix() *
               RayTracer.Basics.Transforms.Translate(-pivot.X, -pivot.Y, -pivot.Z);
    }

    /// <summary>
    /// This method returns a copy of this turn, for a copy of the joint it belongs to.
    /// </summary>
    /// <returns>The copy.</returns>
    public JointCreator Copy() => (JointCreator) MemberwiseClone();

    /// <summary>
    /// This method returns an angle held within the joint's limits.
    /// </summary>
    /// <param name="angle">The angle, in radians.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The angle, within its limits.</returns>
    public double Limited(double angle, RenderContext context, Variables variables)
    {
        if (LowestResolver?.Resolve(context, variables) is { } lowest && angle < lowest)
            angle = lowest;

        if (HighestResolver?.Resolve(context, variables) is { } highest && angle > highest)
            angle = highest;

        return angle;
    }

    /// <summary>
    /// This method is not asked, since the joint's turn is made whole above.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="doubles">The numbers, of which there are none.</param>
    /// <param name="tuples">The tuples, of which there are none.</param>
    /// <returns>No transform.</returns>
    protected override Matrix CreateTransform(RenderContext context, double[] doubles, NumberTuple[] tuples) =>
        Matrix.Identity;
}
