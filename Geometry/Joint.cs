using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a joint: a group turned about a pivot, by an angle about an axis and within limits, so that
/// what it holds swings as a door on its hinge, a wheel on its axle or an arm at its elbow.  A joint held in another
/// is carried round with it, so joints within joints make a chain.
/// <para>
/// **Turned by its angle, or by what it is asked to do.**  Its angle turns it as it is resolved, as a transform does
/// (and, said in terms of the time, blurs as any timed placement does).  A joint may instead be asked to `aim` -- to
/// turn so that its facing points at a place or a named thing -- or to `reach`, turning itself and the joints within
/// it so that a named thing inside comes as near a place as their limits allow.  Those are settled once the whole
/// scene stands where it will (<see cref="KinematicsSettler"/>), since what they aim at may stand anywhere.
/// </para>
/// </summary>
public class Joint : Group
{
    /// <summary>
    /// This property holds the place the joint turns about, in its own space.
    /// </summary>
    public Point Pivot { get; set; } = Point.Zero;

    /// <summary>
    /// This property holds the axis the joint turns about, in its own space, or null for one that may turn any way
    /// -- which only aiming can do.
    /// </summary>
    public Vector Axis { get; set; }

    /// <summary>
    /// This property holds how far the joint is turned about its axis, in radians, within its limits.
    /// </summary>
    public double Angle { get; set; }

    /// <summary>
    /// This property holds the least the joint may be turned, in radians, or null for no limit.
    /// </summary>
    public double? Lowest { get; set; }

    /// <summary>
    /// This property holds the most the joint may be turned, in radians, or null for no limit.
    /// </summary>
    public double? Highest { get; set; }

    /// <summary>
    /// This property holds the way the joint faces, in its own space, which aiming turns toward what it aims at.
    /// </summary>
    public Vector Facing { get; set; } = new (1, 0, 0);

    /// <summary>
    /// This property holds what the joint aims at, or null.
    /// </summary>
    public Target Aim { get; set; }

    /// <summary>
    /// This property holds what the joint reaches for, or null.
    /// </summary>
    public Target Reach { get; set; }

    /// <summary>
    /// This property holds the name of the thing inside the joint that reaches: the tip of the chain.
    /// </summary>
    public string Effector { get; set; }

    /// <summary>
    /// This property notes whether the joint has anything to settle once the scene stands where it will.
    /// </summary>
    public bool IsSolved => Aim is not null || Reach is not null;

    /// <summary>
    /// This method returns an angle held within the joint's limits.
    /// </summary>
    /// <param name="angle">The angle, in radians.</param>
    /// <returns>The angle, within the limits.</returns>
    public double Limited(double angle)
    {
        if (Lowest is { } lowest && angle < lowest)
            angle = lowest;

        if (Highest is { } highest && angle > highest)
            angle = highest;

        return angle;
    }
}

/// <summary>
/// This record holds what a joint aims at or reaches for: a place in the scene, or the origin of a named thing --
/// a `mark`, or any surface given a name.
/// </summary>
/// <param name="At">The place, in the scene's own space, or null.</param>
/// <param name="Name">The name of the thing, or null.</param>
public sealed record Target(Point At, string Name);
