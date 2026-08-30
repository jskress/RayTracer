using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This enumeration names the ways one surface may be put beside another.
/// <para>
/// There are two kinds and they are not the same question.  A <em>contact</em> puts one thing against
/// another -- on it, beside it, behind it -- so the two end up touching along the boxes they occupy.
/// An <em>alignment</em> puts one edge level with the matching edge of the other and leaves them
/// wherever that puts them, touching or not.  A shelf goes *on* its bracket; a leg goes *under* the
/// table but *lined up with* its end, and no amount of "beside" will say that.
/// <para>
/// Either kind settles one axis and says nothing about the other two.  They are named the way
/// somebody standing in front of the scene would name them, so <c>front</c> is toward -Z, which is
/// where a camera conventionally stands in this renderer.
/// </para>
/// </para>
/// </summary>
public enum PlacementRelation
{
    /// <summary>Sitting on top of the other, touching.  A scene writes this as <c>on</c>;
    /// the name here says which way up it is, where a bare "On" would not.</summary>
    Atop,

    /// <summary>Tucked underneath it, touching.</summary>
    Under,

    /// <summary>Beside it, on the -X side.</summary>
    LeftOf,

    /// <summary>Beside it, on the +X side.</summary>
    RightOf,

    /// <summary>In front of it, toward -Z.</summary>
    FrontOf,

    /// <summary>Behind it, toward +Z.</summary>
    Behind,

    /// <summary>Its -X edge level with the other's, rather than against it.</summary>
    AlignLeft,

    /// <summary>Its +X edge level with the other's.</summary>
    AlignRight,

    /// <summary>Its +Y edge level with the other's.</summary>
    AlignTop,

    /// <summary>Its -Y edge level with the other's.</summary>
    AlignBottom,

    /// <summary>Its -Z edge level with the other's.</summary>
    AlignFront,

    /// <summary>Its +Z edge level with the other's.</summary>
    AlignBack,

    /// <summary>Middled on the other in every direction nothing else has claimed.  This one settles
    /// no direction of its own; it says which thing the rest are measured from.</summary>
    CenteredOn
}

/// <summary>
/// This class holds one thing a scene said about where a surface goes, in terms of another surface
/// rather than in numbers.
/// <para>
/// **This is a request rather than a position**, and it must be, because the answer is not knowable
/// when it is written down: where a lamp goes depends on how tall the table turned out to be, and a
/// table's height is not settled until the table has been built and has worked out the box it
/// occupies.  So a placement is carried, unresolved, until the moment both are known -- see
/// <see cref="PlacementSettler.Settle"/> for where that is and why it is there.
/// </para>
/// </summary>
public class Placement
{
    /// <summary>
    /// This property holds which way round the two surfaces go.
    /// </summary>
    public PlacementRelation Relation { get; init; }

    /// <summary>
    /// This property holds the name of the surface this one is placed against.
    /// </summary>
    public string TargetName { get; init; }

    /// <summary>
    /// This property holds how far past the place the relation names to go, or nought.
    /// <para>
    /// **Which way "past" is depends on the kind of relation, and it has to.**  For a contact it is
    /// a *gap*: away from the thing you are against, so a shelf slung <c>under 'top' by 0.3</c> hangs
    /// clear of it rather than being buried in it.  For an alignment it is an *inset*: toward the
    /// middle of the thing you lined up with, so a leg <c>align left with 'top' by 0.1</c> stands in
    /// from the corner rather than out past it.  One number, and in both cases it means the thing a
    /// person would mean.
    /// </para>
    /// </summary>
    public double Offset { get; init; }

    /// <summary>
    /// This property notes that this relation settles no direction of its own.
    /// </summary>
    internal bool SettlesNoAxis => Relation == PlacementRelation.CenteredOn;

    /// <summary>
    /// This property holds which way along its axis this relation's offset carries the surface.
    /// </summary>
    private int OffsetSign => Relation switch
    {
        PlacementRelation.Atop or PlacementRelation.RightOf or PlacementRelation.Behind or
            PlacementRelation.AlignLeft or PlacementRelation.AlignBottom or
            PlacementRelation.AlignFront => 1,
        _ => -1
    };

    /// <summary>
    /// This property holds which axis this relation settles: 0 for X, 1 for Y and 2 for Z.
    /// </summary>
    internal int Axis => Relation switch
    {
        PlacementRelation.Atop or PlacementRelation.Under or
            PlacementRelation.AlignTop or PlacementRelation.AlignBottom => 1,
        PlacementRelation.LeftOf or PlacementRelation.RightOf or
            PlacementRelation.AlignLeft or PlacementRelation.AlignRight => 0,
        _ => 2
    };

    /// <summary>
    /// This method works out how far along its own axis a surface must move for this relation to
    /// hold, given where the two of them currently stand.
    /// <para>
    /// Both boxes are measured in the space the two surfaces share -- they are siblings, so that is
    /// their parent's -- and the answer is a distance in that same space.
    /// </para>
    /// </summary>
    /// <param name="mine">The box occupied by the surface being placed.</param>
    /// <param name="theirs">The box occupied by the surface it is placed against.</param>
    /// <returns>How far to move, along this relation's axis.</returns>
    internal double DistanceFor(BoundingBox mine, BoundingBox theirs)
    {
        (Point low, Point high) = (mine.Minimum, mine.Maximum);
        (Point theirLow, Point theirHigh) = (theirs.Minimum, theirs.Maximum);

        return Relation switch
        {
            PlacementRelation.Atop => theirHigh.Y - low.Y,
            PlacementRelation.Under => theirLow.Y - high.Y,
            PlacementRelation.LeftOf => theirLow.X - high.X,
            PlacementRelation.RightOf => theirHigh.X - low.X,
            PlacementRelation.FrontOf => theirLow.Z - high.Z,
            PlacementRelation.Behind => theirHigh.Z - low.Z,

            // An alignment matches an edge to the matching edge rather than to the opposite one,
            // which is the whole of the difference between the two kinds.
            PlacementRelation.AlignLeft => theirLow.X - low.X,
            PlacementRelation.AlignRight => theirHigh.X - high.X,
            PlacementRelation.AlignTop => theirHigh.Y - high.Y,
            PlacementRelation.AlignBottom => theirLow.Y - low.Y,
            PlacementRelation.AlignFront => theirLow.Z - low.Z,
            _ => theirHigh.Z - high.Z
        } + Offset * OffsetSign;
    }

    /// <summary>
    /// This method returns how a scene would have written this, for error messages.
    /// </summary>
    /// <returns>The relation and the name it refers to.</returns>
    public override string ToString()
    {
        string word = Relation switch
        {
            PlacementRelation.Atop => "on",
            PlacementRelation.Under => "under",
            PlacementRelation.LeftOf => "left of",
            PlacementRelation.RightOf => "right of",
            PlacementRelation.FrontOf => "front of",
            PlacementRelation.Behind => "behind",
            PlacementRelation.AlignLeft => "align left with",
            PlacementRelation.AlignRight => "align right with",
            PlacementRelation.AlignTop => "align top with",
            PlacementRelation.AlignBottom => "align bottom with",
            PlacementRelation.AlignFront => "align front with",
            PlacementRelation.AlignBack => "align back with",
            _ => "centered on"
        };

        return Offset == 0
            ? $"{word} '{TargetName}'"
            : $"{word} '{TargetName}' by {Offset}";
    }
}
