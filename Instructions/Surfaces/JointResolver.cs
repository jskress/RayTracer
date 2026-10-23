using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Transforms;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a joint: a group turned about a pivot, and what it aims at or reaches for.
/// </summary>
public class JointResolver : GroupResolver
{
    /// <summary>
    /// This property holds the joint's turn, which stands first among its transforms.
    /// </summary>
    public JointCreator Turn { get; } = new ();

    /// <summary>
    /// This property holds the resolver for the way the joint faces, or null.
    /// </summary>
    public Resolver<Vector> FacingResolver { get; set; }

    /// <summary>
    /// This property holds the term for what the joint aims at, or null.
    /// </summary>
    public Term AimTerm { get; set; }

    /// <summary>
    /// This property holds the term for what the joint reaches for, or null.
    /// </summary>
    public Term ReachTerm { get; set; }

    /// <summary>
    /// This property holds the term for the name of what reaches, or null.
    /// </summary>
    public Term EffectorTerm { get; set; }

    /// <summary>
    /// This method makes a joint, rather than the plain group a group's resolver would.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The joint.</returns>
    public override Group Resolve(RenderContext context, Variables variables)
    {
        Joint joint = new ();

        SetProperties(context, variables, joint);

        return joint;
    }

    protected override void SetProperties(RenderContext context, Variables variables, Group value)
    {
        Joint joint = (Joint) value;

        joint.Pivot = Turn.PivotResolver?.Resolve(context, variables) ?? Point.Zero;
        joint.Axis = Turn.AxisResolver?.Resolve(context, variables)?.Unit;
        joint.Lowest = Turn.LowestResolver?.Resolve(context, variables);
        joint.Highest = Turn.HighestResolver?.Resolve(context, variables);
        joint.Angle = Turn.AngleResolver is null ? 0 : Turn.Limited(Turn.AngleResolver.Resolve(context, variables), context, variables);
        joint.Facing = FacingResolver?.Resolve(context, variables) ?? joint.Facing;
        joint.Aim = TargetOf(AimTerm, variables);
        joint.Reach = TargetOf(ReachTerm, variables);
        joint.Effector = EffectorTerm?.GetValue<string>(variables);

        base.SetProperties(context, variables, value);
    }

    // A place in the scene, or a thing by its name.
    private static Target TargetOf(Term term, Variables variables)
    {
        if (term is null)
            return null;

        return term.GetValue(variables) switch
        {
            string name => new Target(null, name),
            NumberTuple place => new Target(new Point(place.X, place.Y, place.Z), null),
            _ => throw new TokenException("A joint aims at or reaches for a place, or a thing by its name.")
            {
                Token = term.ErrorToken
            }
        };
    }
}
