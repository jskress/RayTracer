using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class settles the surfaces that were placed in terms of other surfaces rather than in
/// numbers -- a lamp put <c>atop 'table'</c> rather than at a height somebody had to work out.
/// <para>
/// **It runs where it does for a reason, and the reason is bounding boxes.**  A relation is a
/// statement about the regions two surfaces occupy, so neither one can be answered until both have
/// been built and have worked out their own box.  That happens in
/// <see cref="Surface.PrepareForRendering(double[])"/>, and a group prepares every child before it
/// arranges them -- so the moment between those two is the only one where every box is known and
/// nothing has yet been decided on the strength of where things stand.  Settling any earlier reads
/// boxes that do not exist; any later, and the group has already sorted its children into a tree
/// built around positions that are about to change.
/// </para>
/// <para>
/// **Placement is between siblings.**  Two surfaces in the same group share a coordinate space, so
/// the arithmetic is a subtraction; two in different groups do not, and the answer would have to be
/// carried through however many transforms lie between them and their common ancestor -- and then
/// would be undone the moment either group was moved.  Restricting it keeps a placement meaning the
/// same thing wherever the group it lives in ends up.
/// </para>
/// </summary>
internal static class PlacementSettler
{
    /// <summary>
    /// This method settles every placement among a set of siblings, moving each surface that was
    /// placed until it stands where it was said to.
    /// </summary>
    /// <param name="siblings">The surfaces sharing one space, all of them prepared.</param>
    internal static void Settle(IList<Surface> siblings)
    {
        List<Surface> placed = siblings
            .Where(surface => surface.Placements is { Count: > 0 })
            .ToList();

        if (placed.Count == 0)
            return;

        Dictionary<string, Surface> byName = NamesAmong(siblings);

        foreach (Surface surface in InDependencyOrder(placed, byName))
            Settle(surface, byName);
    }

    /// <summary>
    /// This method indexes the siblings that carry a name, complaining if two of them carry the same
    /// one -- since a placement naming that would have no way to say which was meant.
    /// </summary>
    /// <param name="siblings">The surfaces sharing one space.</param>
    /// <returns>The named ones, by name.</returns>
    private static Dictionary<string, Surface> NamesAmong(IList<Surface> siblings)
    {
        Dictionary<string, Surface> byName = new ();

        foreach (Surface surface in siblings.Where(surface => surface.Name is not null))
        {
            if (!byName.TryAdd(surface.Name, surface))
            {
                throw new Exception(
                    $"Two things here are both named '{surface.Name}', so a placement naming it " +
                    "cannot say which was meant; give them names of their own.");
            }
        }

        return byName;
    }

    /// <summary>
    /// This method puts the placed surfaces into an order in which each one can be settled: whatever
    /// a surface is placed against is settled before it is.
    /// <para>
    /// **A chain of placements is the ordinary case, not the exotic one** -- a lamp on a table on a
    /// rug -- and settling them in the order they were written would read the table's box before the
    /// table had moved onto the rug.  Nothing would fail; the lamp would simply hang in the air where
    /// the table used to be, which is the kind of wrong that is easy to look at and not see.
    /// </para>
    /// </summary>
    /// <param name="placed">The surfaces that were placed against something.</param>
    /// <param name="byName">The named siblings, by name.</param>
    /// <returns>Those surfaces, each following whatever it depends on.</returns>
    private static List<Surface> InDependencyOrder(
        List<Surface> placed, Dictionary<string, Surface> byName)
    {
        List<Surface> order = [];
        HashSet<Surface> settled = [];
        List<Surface> underway = [];

        void Visit(Surface surface)
        {
            if (settled.Contains(surface))
                return;

            int already = underway.IndexOf(surface);

            if (already >= 0)
            {
                IEnumerable<string> ring = underway
                    .Skip(already)
                    .Append(surface)
                    .Select(link => $"'{link.Name ?? "something unnamed"}'");

                throw new Exception(
                    $"These placements run in a circle: {string.Join(" -> ", ring)}.  Each is " +
                    "waiting on the next to settle, so none of them ever can.");
            }

            underway.Add(surface);

            foreach (Placement placement in surface.Placements ?? [])
            {
                if (byName.TryGetValue(placement.TargetName, out Surface target))
                    Visit(target);
            }

            underway.RemoveAt(underway.Count - 1);
            settled.Add(surface);

            if (surface.Placements is { Count: > 0 })
                order.Add(surface);
        }

        foreach (Surface surface in placed)
            Visit(surface);

        return order;
    }

    /// <summary>
    /// This method moves one surface until every relation it was given holds.
    /// <para>
    /// **Each relation settles one axis, and the axes nobody claimed are centered.**  Saying a lamp
    /// goes atop a table says where it stands vertically and nothing at all about the other two, and
    /// the middle of the table is both the obvious answer and the only one that does not need a
    /// second number from whoever wrote it.  Add a second relation and it takes its own axis over,
    /// leaving whatever is still unclaimed centered on the first thing named.
    /// </para>
    /// </summary>
    /// <param name="surface">The surface to move.</param>
    /// <param name="byName">The named siblings, by name.</param>
    private static void Settle(Surface surface, Dictionary<string, Surface> byName)
    {
        BoundingBox mine = Surface.BoxAround(surface, true);

        if (mine is null || mine.IsEmpty)
        {
            throw new Exception(
                $"The thing placed {surface.Placements[0]} occupies no region a placement could be " +
                "worked out from; a plane, or anything else without bounds, cannot be placed this way.");
        }

        double[] move = [0, 0, 0];
        bool[] claimed = [false, false, false];
        BoundingBox first = null;
        BoundingBox middledOn = null;

        foreach (Placement placement in surface.Placements)
        {
            BoundingBox theirs = BoxOf(placement, byName);

            first ??= theirs;

            // "Centered on" settles no direction of its own -- it says which thing the directions
            // nobody claimed are measured from, which is otherwise whatever was named first.
            if (placement.SettlesNoAxis)
            {
                if (middledOn is not null)
                {
                    throw new Exception(
                        $"Something is placed {placement}, but it was already told what to be " +
                        "centered on; there can only be one thing it is middled on.");
                }

                middledOn = theirs;

                continue;
            }

            if (claimed[placement.Axis])
            {
                throw new Exception(
                    $"Something is placed {placement}, but it was already given somewhere to be " +
                    $"along the same axis; a surface can be told one thing per direction.");
            }

            claimed[placement.Axis] = true;
            move[placement.Axis] = placement.DistanceFor(mine, theirs);
        }

        // Whatever is still unclaimed is centered: on the thing that was named for it, or failing
        // that on the first thing named at all, which is what "on the table" means to anybody who
        // says it.
        double[] middleOfMine = Middle(mine);
        double[] middleOfTheirs = Middle(middledOn ?? first);

        for (int axis = 0; axis < 3; axis++)
        {
            if (!claimed[axis])
                move[axis] = middleOfTheirs[axis] - middleOfMine[axis];
        }

        // The boxes were measured in the space this surface shares with its siblings, so the move is
        // in that space too -- which is to say it goes on the far side of the surface's own transform
        // rather than among it.
        surface.Transform = Transforms.Translate(move[0], move[1], move[2]) * surface.Transform;
    }

    /// <summary>
    /// This method finds the box occupied by whatever a placement names, complaining usefully when
    /// there is no such thing among the siblings.
    /// </summary>
    /// <param name="placement">The placement naming it.</param>
    /// <param name="byName">The named siblings, by name.</param>
    /// <returns>The box that thing occupies.</returns>
    private static BoundingBox BoxOf(Placement placement, Dictionary<string, Surface> byName)
    {
        if (!byName.TryGetValue(placement.TargetName, out Surface target))
        {
            string known = byName.Count == 0
                ? "nothing beside it carries a name at all"
                : $"the names here are {string.Join(", ", byName.Keys.Order().Select(name => $"'{name}'"))}";

            throw new Exception(
                $"Something is placed {placement}, but {known}.  A surface may only be placed " +
                "against one standing beside it in the same group.");
        }

        BoundingBox box = Surface.BoxAround(target, true);

        if (box is null || box.IsEmpty)
        {
            throw new Exception(
                $"Something is placed {placement}, but '{placement.TargetName}' occupies no region " +
                "a placement could be worked out from.");
        }

        return box;
    }

    /// <summary>
    /// This method returns the middle of a box, as three numbers that can be indexed by axis.
    /// </summary>
    /// <param name="box">The box to measure.</param>
    /// <returns>Its middle, along each axis.</returns>
    private static double[] Middle(BoundingBox box)
    {
        (Point low, Point high) = (box.Minimum, box.Maximum);

        return [(low.X + high.X) * 0.5, (low.Y + high.Y) * 0.5, (low.Z + high.Z) * 0.5];
    }
}
