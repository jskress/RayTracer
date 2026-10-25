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
    public JointCreator Turn { get; private set; } = new ();

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

    // Each of a joint's own properties is set only if it was said, so that the block on a call of a primitive that
    // gives back a joint -- `object Arm() { translate X 2 }` -- moves it without forgetting how it turns.
    protected override void SetProperties(RenderContext context, Variables variables, Group value)
    {
        Joint joint = (Joint) value;

        if (Turn.PivotResolver is not null)
            joint.Pivot = Turn.PivotResolver.Resolve(context, variables);

        if (Turn.AxisResolver is not null)
            joint.Axis = Turn.AxisResolver.Resolve(context, variables).Unit;

        if (Turn.LowestResolver is not null)
            joint.Lowest = Turn.LowestResolver.Resolve(context, variables);

        if (Turn.HighestResolver is not null)
            joint.Highest = Turn.HighestResolver.Resolve(context, variables);

        if (Turn.AngleResolver is not null)
            joint.Angle = Turn.Limited(Turn.AngleResolver.Resolve(context, variables), context, variables);

        if (FacingResolver is not null)
            joint.Facing = FacingResolver.Resolve(context, variables);

        if (AimTerm is not null)
            joint.Aim = TargetOf(AimTerm, variables);

        if (ReachTerm is not null)
        {
            joint.Reach = TargetOf(ReachTerm, variables);
            joint.Effector = EffectorTerm?.GetValue<string>(variables);
        }

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver, its turn a copy of its own: a named joint used with an angle of
    /// its own (`joint Arm { angle 30 }`) writes the angle into the copy, which shared, every use of the name took.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        JointResolver resolver = (JointResolver) base.Clone();

        resolver.Turn = Turn.Copy();

        if (resolver.TransformResolver is { } transforms)
        {
            int at = transforms.TransformCreators.IndexOf(Turn);

            if (at >= 0)
                transforms.TransformCreators[at] = resolver.Turn;
        }

        return resolver;
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
