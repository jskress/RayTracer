using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

public enum CsgOperation
{
    Union = 0,
    Intersection = 1,
    Difference = 2
}

/// <summary>
/// This class represents a group of other surfaces that make a single surface that results
/// from applying a set operation on the child surfaces.
/// </summary>
public class CsgSurface : Surface
{
    /// <summary>
    /// This property reports the operation this CSG surface will apply.
    /// </summary>
    public CsgOperation Operation { get; set; }

    /// <summary>
    /// The left surface for the operation,
    /// </summary>
    public Surface Left
    {
        get => field;
        set
        {
            value.Parent = this;
            field = value;
            _leftMembers = null;
            _chain = null;
        }
    }

    /// <summary>
    /// The right surface for the operation,
    /// </summary>
    public Surface Right
    {
        get => field;
        set
        {
            value.Parent = this;
            field = value;
            _rightMembers = null;
            _chain = null;
        }
    }

    /// <summary>
    /// This property notes that this combination was not written as such, but is one of the links a
    /// combination of more than two things is built from -- <c>union { a b c }</c> is made as
    /// <c>(a + b) + c</c>, and the inner one is a link.  Anything counting the combinations a scene
    /// asked for counts only those that are not links.
    /// </summary>
    internal bool IsChainLink { get; init; }

    private HashSet<Surface> _leftMembers;
    private HashSet<Surface> _rightMembers;

    // In a chain, which of the things written each surface stands in, shared by every link, and which of them is
    // this link's right side: everything before it is its left.
    private Dictionary<Surface, int> _chain;
    private int _place;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        Left.PrepareForRendering(SampleTimes);
        Right.PrepareForRendering(SampleTimes);

        if (Material is not null)
        {
            Dictionary<Material, Material> merged = [];

            // As far as each instance and no further; see Group.
            foreach (Surface surface in new SurfaceIterator([Left, Right], intoShared: false).Surfaces)
                surface.Material = Material.HandedDown(surface.Material, Material, merged);
        }

        // Every crossing this shape keeps or drops has first to be put on one side or the other, and
        // that was a walk of the side's whole tree for each crossing.  A union of many things is a
        // chain of these, each with everything before it on its left, so a big one walked itself
        // over and over for every ray.  What stands on each side cannot change while it renders, so
        // it is listed once, here -- after the sides are ready, since a tube builds the segments its
        // crossings come from only when it is readied.
        //
        // **A chain is listed once, by the link at its top, for every link in it.**  Listed link by
        // link, each link's left side held everything before it, so the lists came to the square of
        // the things written: a hundred grooves swept along a hull, each a few hundred facets, took
        // twelve seconds to list before the first ray.  Which of the things written a surface stands
        // in says which side of any link it is on.
        if (IsChainLink)
            return;

        if (Left is CsgSurface { IsChainLink: true, Operation: var operation } && operation == Operation)
            ListTheChain();
        else
        {
            _leftMembers = MembersOf(Left);
            _rightMembers = MembersOf(Right);
        }
    }

    /// <summary>
    /// This method lists, for this link at the top of a chain and every link below it, which of the things written
    /// each surface stands in.  The first thing written is the left side of the lowest link; each link's right side
    /// is the next.
    /// </summary>
    private void ListTheChain()
    {
        List<CsgSurface> links = [this];

        while (links[^1].Left is CsgSurface { IsChainLink: true } below && below.Operation == Operation)
            links.Add(below);

        links.Reverse();

        Dictionary<Surface, int> chain = new (ReferenceEqualityComparer.Instance);

        Enlist(chain, links[0].Left, 0);

        for (int index = 0; index < links.Count; index++)
        {
            Enlist(chain, links[index].Right, index + 1);
            links[index]._chain = chain;
            links[index]._place = index + 1;
        }
    }

    /// <summary>
    /// This method notes that everything standing in one of a chain's things stands in that one.
    /// </summary>
    /// <param name="chain">Which thing each surface stands in.</param>
    /// <param name="side">The thing.</param>
    /// <param name="place">Its place among those written.</param>
    private static void Enlist(Dictionary<Surface, int> chain, Surface side, int place)
    {
        foreach (Surface surface in MembersOf(side))
            chain[surface] = place;
    }

    /// <summary>
    /// This method reports whether a surface a crossing was found on stands on this shape's left side.
    /// </summary>
    /// <param name="hit">The surface.</param>
    /// <returns><c>true</c>, if it stands on the left side.</returns>
    private bool OnTheLeft(Surface hit)
    {
        return _chain is not null && _chain.TryGetValue(hit, out int place)
            ? place < _place
            : IsOrIncludes(Left, _leftMembers, hit);
    }

    /// <summary>
    /// This method reports whether a surface a crossing was found on stands on this shape's right side.
    /// </summary>
    /// <param name="hit">The surface.</param>
    /// <returns><c>true</c>, if it stands on the right side.</returns>
    private bool OnTheRight(Surface hit)
    {
        return _chain is not null && _chain.TryGetValue(hit, out int place)
            ? place == _place
            : IsOrIncludes(Right, _rightMembers, hit);
    }

    /// <summary>
    /// This method lists everything that stands on one side of this shape: the side itself and all
    /// it holds.
    /// </summary>
    /// <param name="side">The side to list.</param>
    /// <returns>The surfaces that stand on that side.</returns>
    private static HashSet<Surface> MembersOf(Surface side)
    {
        HashSet<Surface> members = new (ReferenceEqualityComparer.Instance);

        // Hair is found through the surface growing it, so it stands on that surface's side.
        foreach (Surface surface in new SurfaceIterator(side).Surfaces)
        {
            members.Add(surface);

            if (surface.Hair is not null)
                members.Add(surface.Hair);
        }

        return members;
    }

    /// <summary>
    /// This method returns the box a combination of two surfaces sits in, which depends on what the
    /// combination does with them.
    /// <para>
    /// <b>A union</b> holds everything in either one, so it needs a box around both -- and if either
    /// cannot say where it is, neither can the union.
    /// </para>
    /// <para>
    /// <b>An intersection</b> holds only what lies in <i>both</i>, so it cannot reach beyond either of
    /// them: whichever one can say where it is already bounds the whole thing, and one that cannot say
    /// costs nothing.  That is worth having rather than merely tidy -- a sphere cut by a plane is
    /// bounded by the sphere, though a plane goes on forever.  When both can say, the overlap of the
    /// two is tighter than either alone.
    /// </para>
    /// <para>
    /// <b>A difference</b> takes material away from the left one, and taking material away can only
    /// make a thing smaller, so the left one's box holds the result whatever the right one is or is
    /// not.  Half a sphere cut away by a plane is still inside the sphere.
    /// </para>
    /// </summary>
    /// <returns>The box this combination sits in, or <c>null</c> if it has none.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return BoxOfChildren(false);
    }

    /// <summary>
    /// This method returns the region this shape really covers, which is the same combining of its
    /// two sides with each of them asked for its own real extent.
    /// </summary>
    /// <returns>The region the shape occupies, or <c>null</c> if it is unbounded.</returns>
    internal override BoundingBox TrueBoundingBox()
    {
        return BoxOfChildren(true);
    }

    /// <summary>
    /// This method combines the boxes of the two sides the way this shape's operation does.
    /// </summary>
    /// <param name="trueExtent">Whether to ask each side what it really covers.</param>
    /// <returns>The combined box, or <c>null</c> where the operation leaves it unbounded.</returns>
    private BoundingBox BoxOfChildren(bool trueExtent)
    {
        BoundingBox left = BoxAround(Left, trueExtent);
        BoundingBox right = BoxAround(Right, trueExtent);

        switch (Operation)
        {
            case CsgOperation.Union:
                if (left is null || right is null)
                    return null;

                left.Add(right);

                return left;

            case CsgOperation.Intersection:
                if (left is null)
                    return right;

                return right is null ? left : left.Overlap(right);

            default:
                return left;
        }
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        AddIntersectionsReportingStart(ray, intersections);
    }

    /// <summary>
    /// This method finds where a ray crosses this combination and reports whether it begins inside it.
    /// Each side says where the ray begins as it reports its crossings, which is where the walk over
    /// those crossings has to start from; and what this combination makes of the two is what it in
    /// turn says to whatever it is a side of.
    /// </summary>
    /// <param name="ray">The ray to test, in this surface's own space.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    /// <returns><c>true</c>, if the ray, followed back endlessly, is inside this combination.</returns>
    protected override bool AddIntersectionsReportingStart(Ray ray, List<Intersection> intersections)
    {
        List<Intersection> ours = [];
        bool inLeft = Left.IntersectReportingStart(ray, ours);
        bool inRight = Right.IntersectReportingStart(ray, ours);

        ours.Sort();

        FilterIntersections(ours, inLeft, inRight);

        intersections.AddRange(ours);

        return Holds(inLeft, inRight);
    }

    /// <summary>
    /// This method reports whether a ray begins inside this combination, from where it begins with
    /// respect to each side.
    /// </summary>
    /// <param name="ray">The ray to test, in this surface's own space.</param>
    /// <returns><c>true</c>, if the ray, followed back endlessly, is inside this combination.</returns>
    protected override bool StartsInsideHere(Ray ray)
    {
        return Holds(Left.StartsInside(ray), Right.StartsInside(ray));
    }

    /// <summary>
    /// This method reports whether a point is inside this combination, given whether it is inside
    /// each side.
    /// </summary>
    /// <param name="inLeft">Whether the point is inside the left side.</param>
    /// <param name="inRight">Whether the point is inside the right side.</param>
    /// <returns><c>true</c>, if the point is inside the combination.</returns>
    private bool Holds(bool inLeft, bool inRight)
    {
        return Operation switch
        {
            CsgOperation.Union => inLeft || inRight,
            CsgOperation.Intersection => inLeft && inRight,
            CsgOperation.Difference => inLeft && !inRight,
            _ => throw new Exception($"Unknown operation: {Operation}")
        };
    }

    /// <summary>
    /// This method is used to filter unwanted intersections out of the given list.
    /// <para>
    /// **A strand is kept or dropped as its side's surface would be there, but it is never gone into.**
    /// A strand is crossed once, as a ribbon, so it has no inside for a ray to be in; counted as going
    /// in, it would leave the walk believing the ray inside that side from then on.  So hair grown on
    /// one side of a union shows wherever it is out of the other, and is hidden where it is buried in
    /// it.
    /// </para>
    /// </summary>
    /// <param name="intersections">The list of intersections to filter, in order along the ray.</param>
    /// <param name="inLeft">Whether the ray begins inside the left side, which only something
    /// endless can do; see <see cref="Surface.StartsInside"/>.</param>
    /// <param name="inRight">Whether the ray begins inside the right side.</param>
    public void FilterIntersections(
        List<Intersection> intersections, bool inLeft = false, bool inRight = false)
    {
        intersections.RemoveAll(
            intersection =>
            {
                // What stands in this shape's tree is the *instance*, when the crossing came through
                // one -- the shape it points at is shared and lives outside the tree altogether.  It is
                // the outermost stamped so far: any instance further out has yet to stamp the crossing,
                // and any further in stands inside the shape this one points at.
                Surface hit = intersection.Portals.Outermost ?? Hair.StandingFor(intersection.Surface);
                bool leftHit = OnTheLeft(hit);
                bool result = !IsIntersectionAllowed(leftHit, inLeft, inRight);

                if (intersection.Surface is StrandSurface)
                    return result;

                // A crossing on the right side of a difference is the cut's face, which faces the
                // other way from the shape it was cut by.  It is turned over, not marked turned: the
                // shape cut away may itself be a difference, whose own cut face was turned over once
                // already and is turned back here.  Marking it twice as turned left a pocket cut by a
                // shape with a hole in it facing into the solid -- shaded from inside and shadowed by
                // its own floor.
                if (!result && Operation == CsgOperation.Difference && OnTheRight(hit))
                    intersection.ShouldFlipInsideForOut = !intersection.ShouldFlipInsideForOut;

                if (leftHit)
                    inLeft = !inLeft;
                else
                    inRight = !inRight;

                return result;
            });
    }

    /// <summary>
    /// This method decides whether the given child surface is, or is contained by, the
    /// given (potential) parent surface.
    /// <para>
    /// Once this shape has been readied, the answer is looked up in the list made then of what
    /// stands on that side.  Before that -- a shape put together and asked straight away, as a test
    /// will -- there is no list, and the side is walked as it stands.
    /// </para>
    /// </summary>
    /// <param name="parent">The surface to check whether it is, or contains, the child.</param>
    /// <param name="members">What stands on that side, if it has been listed.</param>
    /// <param name="child">the child surface to test.</param>
    /// <returns><c>true</c>, if <c>child</c> either is, or is contained by, <c>parent</c>.</returns>
    private static bool IsOrIncludes(Surface parent, HashSet<Surface> members, Surface child)
    {
        return members?.Contains(child) ??
               new SurfaceIterator(parent).Surfaces.Any(surface => surface == child);
    }

    /// <summary>
    /// This method decides whether an intersection should be kept.
    /// </summary>
    /// <param name="isLeftHit">A flag noting whether the left surface was hit.</param>
    /// <param name="isLeftInside">A flag noting whether the left hit is from the inside.</param>
    /// <param name="isRightInside">A flag noting whether the right hit is from the inside.</param>
    /// <returns><c>true</c>, if the intersection should be kept, or <c>false</c>, if not.</returns>
    public bool IsIntersectionAllowed(bool isLeftHit, bool isLeftInside, bool isRightInside)
    {
        return Operation switch
        {
            CsgOperation.Union => (isLeftHit && !isRightInside) || (!isLeftHit && !isLeftInside),
            CsgOperation.Intersection => (isLeftHit && isRightInside) || (!isLeftHit && isLeftInside),
            CsgOperation.Difference => (isLeftHit && !isRightInside) || (!isLeftHit && isLeftInside),
            _ => throw new Exception($"Unknown operation: {Operation}")
        };
    }

    /// <summary>
    /// This method returns the normal for the cube.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return Directions.Up;
    }
}
