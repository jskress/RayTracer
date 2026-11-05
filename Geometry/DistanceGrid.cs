using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class holds how far the skin of a solid is from every place in a box round it, signed -- negative inside --
/// sampled on a grid of cubic cells and read between its nodes by straight lines along each axis.
/// <para>
/// **It is what lets anything at all be measured from**, where a distance is otherwise known only for shapes whose
/// arithmetic says it: a sphere's, a box's, a torus's.  A word of text, a lathe, a CSG or a model from a library
/// has no such formula, but every one of them can be crossed by a ray.  So rays are sent along every line of the
/// grid in each of the three directions, and what they cross says two things.  Between the crossings a line is
/// inside or out, which signs every node -- by two lines in three, so a ray grazing an edge or running along a face
/// cannot mis-sign a node alone.  And the crossings themselves are places exactly on the skin, so the distance of a
/// node is the distance to the nearest of them: found for every node by jump flooding (Rong and Tan 2006), each
/// node looking at what its neighbors half the grid away, then a quarter, and so on down to one cell, have found
/// nearest, ending with a pass at one cell again.
/// </para>
/// <para>
/// The crossings lie on the grid's lines, so the skin between lines is known only through them: a distance comes
/// out within about a quarter of a cell of the true one for a ball (and at most about seven tenths, the farthest a
/// place on the skin can lie from a line), and a corner sharper than a cell is rounded.  A face lying exactly along a
/// line of the grid is no trouble: the line through it is outvoted by the two across it.
/// </para>
/// </summary>
internal sealed class DistanceGrid
{
    private readonly float[] _distances;

    /// <summary>This property holds where the grid's first node stands.</summary>
    internal Point Least { get; }

    /// <summary>This property holds how far apart the nodes are along every axis.</summary>
    internal double Cell { get; }

    /// <summary>This property holds how many nodes there are along X, Y and Z.</summary>
    internal (int X, int Y, int Z) Nodes { get; }

    /// <summary>This property reports how many crossings of the skin the grid was measured from.</summary>
    internal int Crossings { get; }

    private DistanceGrid(Point least, double cell, (int X, int Y, int Z) nodes, float[] distances, int crossings)
    {
        Least = least;
        Cell = cell;
        Nodes = nodes;
        _distances = distances;
        Crossings = crossings;
    }

    /// <summary>
    /// This method measures a solid over a box.
    /// </summary>
    /// <param name="solid">The solid, readied, in the space the box is in.</param>
    /// <param name="box">The box to measure over; the solid should lie well within it.</param>
    /// <param name="cell">How far apart the nodes are.</param>
    /// <returns>The grid, or <c>null</c> if no ray crossed the solid at all.</returns>
    internal static DistanceGrid Of(Surface solid, BoundingBox box, double cell)
    {
        Point least = box.Minimum;
        Vector size = box.Maximum - least;
        (int X, int Y, int Z) nodes = (
            (int) Math.Ceiling(size.X / cell) + 1, (int) Math.Ceiling(size.Y / cell) + 1,
            (int) Math.Ceiling(size.Z / cell) + 1);
        int count = nodes.X * nodes.Y * nodes.Z;
        byte[] votes = new byte[count];
        List<Point> seeds = [];
        int[] nearest = new int[count];

        Array.Fill(nearest, -1);

        for (int axis = 0; axis < 3; axis++)
            Cast(solid, least, cell, nodes, axis, votes, seeds, nearest);

        if (seeds.Count == 0)
            return null;

        Flood(least, cell, nodes, seeds, nearest);

        float[] distances = new float[count];

        Parallel.For(0, nodes.Z, z =>
        {
            for (int y = 0; y < nodes.Y; y++)
            {
                for (int x = 0; x < nodes.X; x++)
                {
                    int at = (z * nodes.Y + y) * nodes.X + x;
                    double distance = nearest[at] < 0
                        ? cell * (nodes.X + nodes.Y + nodes.Z)
                        : Distance(least, cell, x, y, z, seeds[nearest[at]]);

                    distances[at] = (float) (votes[at] >= 2 ? -distance : distance);
                }
            }
        });

        return new DistanceGrid(least, cell, nodes, distances, seeds.Count);
    }

    /// <summary>
    /// This method sends a ray along every line of the grid in one direction, voting for the nodes it finds inside and
    /// offering each node beside a crossing that crossing as the nearest place on the skin.
    /// </summary>
    private static void Cast(
        Surface solid, Point least, double cell, (int X, int Y, int Z) nodes, int axis, byte[] votes,
        List<Point> seeds, int[] nearest)
    {
        int along = axis == 0 ? nodes.X : axis == 1 ? nodes.Y : nodes.Z;
        int first = axis == 0 ? nodes.Y : nodes.X;
        int second = axis == 2 ? nodes.Y : nodes.Z;
        Vector direction = axis == 0 ? new Vector(1, 0, 0) : axis == 1 ? new Vector(0, 1, 0) : new Vector(0, 0, 1);
        List<double>[] found = new List<double>[first * second];

        // The lines are crossed in parallel, each its own, and what they found is gathered in their order afterwards,
        // so the grid comes out the same however the work was shared.
        Parallel.For(0, first * second, line =>
        {
            (int u, int v) = (line % first, line / first);
            Point origin = NodeOn(least, cell, axis, u, v, -1);
            List<(double In, double Out)> runs = Inside(solid, new Ray(origin, direction));
            List<double> crossings = [];

            foreach ((double into, double outOf) in runs)
            {
                crossings.Add(into);
                crossings.Add(outOf);

                // The nodes between the way in and the way out are inside: node i stands i + 1 cells along the ray.
                int from = Math.Max(0, (int) Math.Floor(into / cell) - 1);
                int to = Math.Min(along - 1, (int) Math.Ceiling(outOf / cell));

                for (int i = from; i <= to; i++)
                {
                    double t = (i + 1) * cell;

                    if (t > into && t < outOf)
                        votes[IndexOn(nodes, axis, u, v, i)]++;
                }
            }

            found[line] = crossings;
        });

        for (int line = 0; line < found.Length; line++)
        {
            (int u, int v) = (line % first, line / first);
            Point origin = NodeOn(least, cell, axis, u, v, -1);

            foreach (double t in found[line])
            {
                Point place = origin + direction * t;
                int seed = seeds.Count;

                seeds.Add(place);

                // The nodes either side of the crossing, along the line.
                int below = (int) Math.Floor(t / cell) - 1;

                foreach (int i in new[] { below, below + 1 })
                {
                    if (i < 0 || i >= along)
                        continue;

                    int at = IndexOn(nodes, axis, u, v, i);
                    (int x, int y, int z) = Unpack(nodes, at);

                    if (nearest[at] < 0 ||
                        Distance(least, cell, x, y, z, place) < Distance(least, cell, x, y, z, seeds[nearest[at]]))
                    {
                        nearest[at] = seed;
                    }
                }
            }
        }
    }

    /// <summary>
    /// This method returns the runs of a ray inside a solid: inside any part of a group (its parts may overlap, and
    /// a ray through two of them crosses an even number of times from inside both), inside what an instance shares,
    /// and otherwise between alternate crossings.
    /// </summary>
    private static List<(double In, double Out)> Inside(Surface solid, Ray ray)
    {
        List<(double In, double Out)> runs = [];

        switch (solid)
        {
            case Group group when !Containment.HoldsSheets(group):
            {
                Ray within = group.InverseTransformAt(0).Transform(ray);

                foreach (Surface part in group.Surfaces)
                    runs.AddRange(Inside(part, within));

                return Merged(runs);
            }
            case Instance instance:
                return Inside(instance.Prototype, instance.InverseTransformAt(0).Transform(ray));
        }

        List<Intersection> crossings = [];

        solid.Intersect(ray, crossings);

        List<double> distances = crossings.Select(crossing => crossing.Distance).Order().ToList();

        for (int at = 0; at + 1 < distances.Count; at += 2)
            runs.Add((distances[at], distances[at + 1]));

        return runs;
    }

    /// <summary>
    /// This method joins runs that overlap or touch into one.
    /// </summary>
    private static List<(double In, double Out)> Merged(List<(double In, double Out)> runs)
    {
        List<(double In, double Out)> merged = [];

        foreach ((double into, double outOf) in runs.OrderBy(run => run.In))
        {
            if (merged.Count > 0 && into <= merged[^1].Out)
                merged[^1] = (merged[^1].In, Math.Max(merged[^1].Out, outOf));
            else
                merged.Add((into, outOf));
        }

        return merged;
    }

    /// <summary>
    /// This method spreads the nearest crossing found to every node, by jump flooding: each pass every node takes the
    /// nearest of what it and the 26 nodes a step away found, the step halving from half the grid down to one cell,
    /// and one more pass at one cell mends most of what the halving missed.  Each pass reads the last and writes
    /// anew, so the answer does not hang on the order the nodes were visited in.
    /// </summary>
    private static void Flood(Point least, double cell, (int X, int Y, int Z) nodes, List<Point> seeds, int[] nearest)
    {
        int longest = Math.Max(nodes.X, Math.Max(nodes.Y, nodes.Z));
        List<int> steps = [];

        for (int step = 1 << (int) Math.Ceiling(Math.Log2(Math.Max(2, longest))) - 1; step >= 1; step >>= 1)
            steps.Add(step);

        steps.Add(1);

        int[] read = nearest;
        int[] write = new int[nearest.Length];

        foreach (int step in steps)
        {
            int[] from = read;
            int[] into = write;

            Parallel.For(0, nodes.Z, z =>
            {
                for (int y = 0; y < nodes.Y; y++)
                {
                    for (int x = 0; x < nodes.X; x++)
                    {
                        int best = from[(z * nodes.Y + y) * nodes.X + x];
                        double bestDistance = best < 0 ? double.MaxValue : Distance(least, cell, x, y, z, seeds[best]);

                        for (int dz = -step; dz <= step; dz += step)
                        {
                            int nz = z + dz;

                            if (nz < 0 || nz >= nodes.Z)
                                continue;

                            for (int dy = -step; dy <= step; dy += step)
                            {
                                int ny = y + dy;

                                if (ny < 0 || ny >= nodes.Y)
                                    continue;

                                for (int dx = -step; dx <= step; dx += step)
                                {
                                    int nx = x + dx;

                                    if (nx < 0 || nx >= nodes.X)
                                        continue;

                                    int candidate = from[(nz * nodes.Y + ny) * nodes.X + nx];

                                    if (candidate < 0 || candidate == best)
                                        continue;

                                    double distance = Distance(least, cell, x, y, z, seeds[candidate]);

                                    if (distance < bestDistance || (distance == bestDistance && candidate < best))
                                    {
                                        best = candidate;
                                        bestDistance = distance;
                                    }
                                }
                            }
                        }

                        into[(z * nodes.Y + y) * nodes.X + x] = best;
                    }
                }
            });

            (read, write) = (into, from);
        }

        if (!ReferenceEquals(read, nearest))
            Array.Copy(read, nearest, nearest.Length);
    }

    /// <summary>
    /// This method returns the signed distance at a place, read between the eight nodes round it; a place past the
    /// grid is read at its edge.
    /// </summary>
    internal double DistanceAt(double x, double y, double z)
    {
        double fx = Math.Clamp((x - Least.X) / Cell, 0, Nodes.X - 1.000001);
        double fy = Math.Clamp((y - Least.Y) / Cell, 0, Nodes.Y - 1.000001);
        double fz = Math.Clamp((z - Least.Z) / Cell, 0, Nodes.Z - 1.000001);
        (int ix, int iy, int iz) = ((int) fx, (int) fy, (int) fz);
        (double tx, double ty, double tz) = (fx - ix, fy - iy, fz - iz);
        int at = (iz * Nodes.Y + iy) * Nodes.X + ix;
        int row = Nodes.X, slab = Nodes.X * Nodes.Y;
        double c00 = _distances[at] + (_distances[at + 1] - _distances[at]) * tx;
        double c10 = _distances[at + row] + (_distances[at + row + 1] - _distances[at + row]) * tx;
        double c01 = _distances[at + slab] + (_distances[at + slab + 1] - _distances[at + slab]) * tx;
        double c11 = _distances[at + slab + row] +
                     (_distances[at + slab + row + 1] - _distances[at + slab + row]) * tx;
        double c0 = c00 + (c10 - c00) * ty;
        double c1 = c01 + (c11 - c01) * ty;

        return c0 + (c1 - c0) * tz;
    }

    /// <summary>
    /// This method returns which way the distance grows fastest at a place, taken by differences a cell either way
    /// unless said, so that it turns smoothly from cell to cell rather than jumping at every face of one.
    /// </summary>
    internal Vector GradientAt(double x, double y, double z, double step = 0)
    {
        double h = step > 0 ? step : Cell;

        return new Vector(
            DistanceAt(x + h, y, z) - DistanceAt(x - h, y, z),
            DistanceAt(x, y + h, z) - DistanceAt(x, y - h, z),
            DistanceAt(x, y, z + h) - DistanceAt(x, y, z - h)) / (2 * h);
    }

    // Where a node stands, in a line along an axis: u and v the line's place across, i how far along.
    private static Point NodeOn(Point least, double cell, int axis, int u, int v, int i) => axis switch
    {
        0 => new Point(least.X + i * cell, least.Y + u * cell, least.Z + v * cell),
        1 => new Point(least.X + u * cell, least.Y + i * cell, least.Z + v * cell),
        _ => new Point(least.X + u * cell, least.Y + v * cell, least.Z + i * cell)
    };

    private static int IndexOn((int X, int Y, int Z) nodes, int axis, int u, int v, int i) => axis switch
    {
        0 => (v * nodes.Y + u) * nodes.X + i,
        1 => (v * nodes.Y + i) * nodes.X + u,
        _ => (i * nodes.Y + v) * nodes.X + u
    };

    private static (int X, int Y, int Z) Unpack((int X, int Y, int Z) nodes, int at) =>
        (at % nodes.X, at / nodes.X % nodes.Y, at / (nodes.X * nodes.Y));

    private static double Distance(Point least, double cell, int x, int y, int z, Point place)
    {
        double dx = least.X + x * cell - place.X;
        double dy = least.Y + y * cell - place.Y;
        double dz = least.Z + z * cell - place.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
