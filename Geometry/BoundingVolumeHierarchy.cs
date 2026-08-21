using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class arranges a set of surfaces into a tree of nested boxes, so that a ray may reach the few
/// it could possibly hit without being shown the rest.
/// <para>
/// A <see cref="Group"/> tests a box around itself and then, for a ray that gets in, hands that ray to
/// every child it holds.  That is fine for the handful of things an author writes out by hand and
/// hopeless for what a generator produces: a height field is a hundred and thirty thousand triangles,
/// and a stand of trees is limbs within limbs within limbs.  The group's own box answers "is any of
/// this near the ray"; nothing answered "which of it".
/// </para>
/// <para>
/// So the children are sorted into a binary tree of boxes here, once, before the render starts.  A
/// ray descends only into boxes it actually crosses, which turns a walk over everything into a walk
/// over the depth of the tree plus whatever is genuinely nearby.
/// </para>
/// <para>
/// <b>Every crossing is still reported, including the ones behind the ray's origin.</b>  The usual
/// trick of a hierarchy like this is to track the nearest hit found so far and refuse to open any box
/// that lies beyond it, which is sound when all anyone wants is the first thing the ray meets.  This
/// renderer wants more than that: a CSG surface decides what is inside it by counting crossings, and
/// refraction needs the ones behind the origin to know it is leaving rather than entering.  So no
/// distance pruning happens here at all -- a box is opened if the ray crosses its extent anywhere
/// along its whole infinite length, ahead or behind.
/// </para>
/// </summary>
internal class BoundingVolumeHierarchy
{
    /// <summary>
    /// How many surfaces a leaf may hold before it is worth splitting again.  Below a handful, the
    /// box test that would decide between two halves costs about as much as simply testing both.
    /// </summary>
    private const int LeafSize = 4;

    /// <summary>
    /// How many surfaces a group must hold before a tree is worth building at all.  A group of three
    /// things is walked faster than any arrangement of it can be searched.
    /// </summary>
    internal const int WorthBuilding = 8;

    private abstract class Node
    {
        internal BoundingBox Box;
    }

    private sealed class Branch : Node
    {
        internal Node Left;
        internal Node Right;
    }

    private sealed class Leaf : Node
    {
        internal Surface[] Surfaces;
    }

    private readonly Node _root;

    /// <summary>
    /// This method builds a hierarchy over the given surfaces, each paired with the box it occupies in
    /// the space of the group holding it.
    /// </summary>
    /// <param name="entries">The surfaces to arrange, and where each one sits.</param>
    /// <returns>The hierarchy, or null when there is nothing worth arranging.</returns>
    internal static BoundingVolumeHierarchy Build(List<(Surface Surface, BoundingBox Box)> entries)
    {
        return entries.Count < WorthBuilding
            ? null
            : new BoundingVolumeHierarchy(Split(entries, 0, entries.Count));
    }

    private BoundingVolumeHierarchy(Node root)
    {
        _root = root;
    }

    /// <summary>
    /// This method builds one node over a run of the entry list, dividing that run in place as it
    /// goes.
    /// <para>
    /// The run is cut across whichever axis its surfaces are most spread out along, at the middle of
    /// that spread.  Splitting by position rather than by count is what makes the tree follow the
    /// shape of what it holds: a height field's triangles are evenly spread and come out evenly
    /// divided, while a stand of trees separates into trees before it separates into limbs.  A cut
    /// that leaves one side empty -- everything piled at one coordinate -- falls back to halving the
    /// run by count, which always makes progress.
    /// </para>
    /// </summary>
    private static Node Split(List<(Surface Surface, BoundingBox Box)> entries, int from, int to)
    {
        BoundingBox box = new ();

        for (int index = from; index < to; index++)
            box.Add(entries[index].Box);

        if (to - from <= LeafSize)
        {
            Surface[] surfaces = new Surface[to - from];

            for (int index = from; index < to; index++)
                surfaces[index - from] = entries[index].Surface;

            return new Leaf { Box = box, Surfaces = surfaces };
        }

        int middle = Divide(entries, from, to);

        return new Branch
        {
            Box = box,
            Left = Split(entries, from, middle),
            Right = Split(entries, middle, to)
        };
    }

    /// <summary>
    /// This method rearranges a run of the entry list so that everything on the near side of the cut
    /// comes first, and reports where the cut fell.
    /// </summary>
    private static int Divide(List<(Surface Surface, BoundingBox Box)> entries, int from, int to)
    {
        // The axis chosen is the one the *centers* are most spread along, not the one the boxes cover
        // most.  A row of long thin boxes lying side by side covers most ground along its length, and
        // cutting it there would put every box in both halves; what separates them is where they sit.
        BoundingBox centers = new ();

        for (int index = from; index < to; index++)
            centers.Add(CenterOf(entries[index].Box));

        Point low = centers.Minimum;
        Point high = centers.Maximum;
        double spreadX = high.X - low.X;
        double spreadY = high.Y - low.Y;
        double spreadZ = high.Z - low.Z;
        int axis = spreadX >= spreadY && spreadX >= spreadZ ? 0 : spreadY >= spreadZ ? 1 : 2;
        double at = (Coordinate(low, axis) + Coordinate(high, axis)) / 2;
        int middle = from;

        for (int index = from; index < to; index++)
        {
            if (Coordinate(CenterOf(entries[index].Box), axis) < at)
            {
                (entries[middle], entries[index]) = (entries[index], entries[middle]);

                middle++;
            }
        }

        // Everything landed on one side, which happens when the centers all share a coordinate -- a
        // wall of triangles standing in one plane, say.  Halving by count is arbitrary but it divides,
        // and a tree that fails to divide never terminates.
        return middle == from || middle == to ? (from + to) / 2 : middle;
    }

    private static Point CenterOf(BoundingBox box)
    {
        Point low = box.Minimum;
        Point high = box.Maximum;

        return new Point((low.X + high.X) / 2, (low.Y + high.Y) / 2, (low.Z + high.Z) / 2);
    }

    private static double Coordinate(Point point, int axis)
    {
        return axis switch
        {
            0 => point.X,
            1 => point.Y,
            _ => point.Z
        };
    }

    /// <summary>
    /// This method hands the ray to every surface in a box the ray crosses, and to no others.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    internal void Intersect(Ray ray, List<Intersection> intersections)
    {
        Visit(_root, ray, intersections);
    }

    private static void Visit(Node node, Ray ray, List<Intersection> intersections)
    {
        if (!node.Box.IsHitBy(ray))
            return;

        if (node is Leaf leaf)
        {
            foreach (Surface surface in leaf.Surfaces)
                surface.Intersect(ray, intersections);
        }
        else
        {
            Branch branch = (Branch) node;

            Visit(branch.Left, ray, intersections);
            Visit(branch.Right, ray, intersections);
        }
    }

    /// <summary>
    /// This method hands the ray to the surfaces in the boxes it crosses, over a stretch of the ray
    /// rather than the whole of it.  This is the shadow query's traversal, and it is kept apart from
    /// the ordinary one on purpose rather than sharing a path with an infinite bound: the two differ
    /// in what they are allowed to throw away, not merely in how far they look.
    /// </summary>
    internal void IntersectWithin(Ray ray, List<Intersection> intersections, double maxDistance)
    {
        VisitWithin(_root, ray, intersections, maxDistance);
    }

    private static void VisitWithin(
        Node node, Ray ray, List<Intersection> intersections, double maxDistance)
    {
        (double from, double to) = node.Box.GetIntersections(ray);

        // A miss, a box wholly behind the point, or a box wholly past the light.  A shadow query
        // discards all three, so the surfaces inside need never be asked.  What is still never done,
        // even here: nothing is pruned for lying beyond the nearest crossing found so far, which is
        // the usual trick for a hierarchy and would break CSG and refraction both.
        if (from > to || to < 0 || from > maxDistance)
            return;

        if (node is Leaf leaf)
        {
            foreach (Surface surface in leaf.Surfaces)
                surface.IntersectWithin(ray, intersections, maxDistance);
        }
        else
        {
            Branch branch = (Branch) node;

            VisitWithin(branch.Left, ray, intersections, maxDistance);
            VisitWithin(branch.Right, ray, intersections, maxDistance);
        }
    }
}
