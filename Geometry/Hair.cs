using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents hair grown over a surface: fur, a rug's pile, moss, a head of hair.
/// <para>
/// **It belongs to the surface it grows on**, as a child of it, so it moves, is placed and is shared
/// along with it.  It is grown in the space that surface is placed in -- its parent's -- and stands in
/// the surface's own space under the reverse of the surface's transform, so the two cancel and the
/// strands keep the shape they were grown in: a cube scaled flat into a rug does not squash its pile.
/// Anything done to the surface afterward -- placing it beside something, setting it moving -- carries
/// the hair along.
/// </para>
/// </summary>
public class Hair : StrandSurface
{
    private const int DefaultSeed = 0;

    // Strands a hair may hold at most, against a density that would otherwise run away with the
    // machine's memory.
    private const int MostStrands = 2_000_000;

    // How many lines are cast at a time while looking for roots.
    private const int Batch = 4096;

    private static readonly Vector Down = new (0, -1, 0);

    /// <summary>
    /// This property holds the surface the hair grows on.
    /// </summary>
    public Surface Host { get; private init; }

    /// <summary>
    /// This property holds how many strands the hair holds.
    /// </summary>
    public int StrandCount => Strands?.StrandCount ?? 0;

    /// <summary>
    /// This property holds how long a strand is, before it varies.
    /// </summary>
    public double StrandLength { get; private init; }

    /// <summary>
    /// This property holds the area the hair was grown over, as measured by the lines that found its roots.
    /// </summary>
    public double Area { get; private init; }

    // The surfaces the strands grew from -- the parts of the host the lines struck -- and which of them
    // each strand grew from.
    private readonly List<Surface> _sources = [];
    private int[] _sourceOf = [];

    // Once materials are settled, the parts the strands are reported on when they wear more than one
    // material, and which part each strand is.
    private Surface[] _parts;
    private int[] _partOf;
    private bool _wearsItsSkin;

    /// <summary>
    /// This method dresses hair that was given no material of its own: each strand wears the material
    /// of the part of the surface it grew from, once every surface's material is settled.
    /// <para>
    /// **The surface the hair is written in need not have a material at all.**  A group's parts may
    /// each have their own, and so may a combination's sides, and fur grown over them should be the
    /// color of the skin it grows from rather than the plain white a surface with no material gets.
    /// Where the strands wear more than one material, each is reported on a part of the hair that
    /// wears its strand's, so everything that asks a crossing's surface for its material is answered
    /// without having to know about hair.
    /// </para>
    /// </summary>
    internal void WearWhatItGrewFrom()
    {
        Dictionary<Material, int> worn = new (ReferenceEqualityComparer.Instance);
        List<Material> materials = [];
        int[] partOfSource = new int[_sources.Count];

        for (int source = 0; source < _sources.Count; source++)
        {
            Material material = _sources[source].Material ?? Host.Material ?? Material.Default;

            if (!worn.TryGetValue(material, out int part))
            {
                worn[material] = part = materials.Count;
                materials.Add(material);
            }

            partOfSource[source] = part;
        }

        _wearsItsSkin = true;
        Material = materials.Count > 0 ? materials[0] : Host.Material;

        if (materials.Count < 2)
            return;

        _parts = materials
            .Select(material => (Surface) new HairPart { Parent = this, Material = material, NoShadow = NoShadow, Seed = Seed })
            .ToArray();
        _partOf = _sourceOf.Select(source => partOfSource[source]).ToArray();
    }

    /// <summary>
    /// This method returns the surface a crossing of the given strand is reported on: the part of the
    /// hair wearing that strand's material, when the strands wear more than one.
    /// </summary>
    internal override Surface WearerOf(int strand) => _parts is null ? this : _parts[_partOf[strand]];

    /// <summary>
    /// This property reports whether the strands wear the materials of what they grew from.
    /// </summary>
    internal override bool WearsItsSkin => _wearsItsSkin;

    /// <summary>
    /// This property reports that the strands grow among others, as hair does.
    /// </summary>
    internal override bool HasNeighbors => true;

    /// <summary>
    /// This method returns the part of the surface a strand grew from, and its root in the hair's space.
    /// </summary>
    internal override (Surface Part, Point Root)? RootOf(int strand) =>
        Strands is null ? null : (_sources[_sourceOf[strand]], Strands.RootOf(strand));

    /// <summary>
    /// This method returns what stands for a surface a crossing was reported on, when it comes to which
    /// side of a combination it belongs to: the hair itself, for one of its parts.
    /// </summary>
    internal static Surface StandingFor(Surface surface) => surface is HairPart ? surface.Parent : surface;

    /// <summary>
    /// This class is a part of a hair whose strands wear more than one material: the strands wearing one
    /// of them are reported on it, and it stands exactly where the hair does.
    /// </summary>
    private sealed class HairPart : StrandSurface
    {
        internal override bool WearsItsSkin => ((Hair) Parent).WearsItsSkin;

        internal override bool HasNeighbors => true;

        internal override (Surface Part, Point Root)? RootOf(int strand) => ((Hair) Parent).RootOf(strand);
    }

    /// <summary>
    /// This method returns the points each strand runs through, root first, in the space the hair was
    /// grown in -- the one its surface is placed in.
    /// </summary>
    /// <returns>The points each strand runs through.</returns>
    internal List<List<Point>> Paths() => Strands?.Paths() ?? [];

    /// <summary>
    /// This method hands every control point of every strand to the given action, in the space the hair
    /// was grown in; see <see cref="StrandSet.VisitPoints"/>.
    /// </summary>
    internal void VisitPoints(Action<Point> visit) => Strands?.VisitPoints(visit);

    /// <summary>
    /// This method grows hair over the given surface, which must already be readied for rendering.
    /// <para>
    /// **Roots are found by casting straight lines at the surface from every direction**, through a sphere
    /// around it: each direction equally likely, and each line through the sphere equally likely among
    /// those of its direction.  Wherever such a line crosses the surface is a root, and lines like these
    /// cross any surface -- convex or not, of any shape -- as often over one patch of it as over any other
    /// of the same area (Cauchy and Crofton's result), so the roots fall evenly however the surface is
    /// made.  How often they cross it also measures its area: twice the area of the sphere's great circle
    /// for each crossing per line.  So a density needs nothing the surface does not already do.
    /// </para>
    /// </summary>
    /// <param name="host">The surface to grow on.</param>
    /// <param name="growth">What to grow.</param>
    /// <returns>The hair.</returns>
    public static Hair Grow(Surface host, HairGrowth growth)
    {
        if (growth.Length <= 0)
            throw new Exception("Hair needs a length greater than nought.");

        if (growth.Density is null && growth.Count is null)
            throw new Exception("Hair needs a density or a count.");

        // Everything here is in the space the host is placed in, which is where the hair is grown.
        BoundingBox box = Surface.BoxAround(host, true);

        if (box is null || box.IsEmpty || !double.IsFinite((box.Maximum - box.Minimum).Magnitude))
            throw new Exception("Hair can grow only on a surface with an end to it; a plane goes on forever.");

        Point center = new (
            (box.Minimum.X + box.Maximum.X) / 2, (box.Minimum.Y + box.Maximum.Y) / 2,
            (box.Minimum.Z + box.Maximum.Z) / 2);
        double radius = (box.Maximum - box.Minimum).Magnitude / 2 * 1.001 + 1e-9;
        if (growth.Count > MostStrands)
            throw TooMany();

        Matrix inward = host.Transform.Invert();
        Matrix outward = inward.Transpose();
        Random random = new (growth.Seed ?? host.Seed ?? DefaultSeed);
        List<Root> roots = [];
        List<Intersection> hits = [];
        Dictionary<Surface, int> sources = new (ReferenceEqualityComparer.Instance);
        long lines = 0;
        long crossings = 0;
        double area;

        while (true)
        {
            for (int index = 0; index < Batch; index++)
                CastLine(host, growth, inward, outward, center, radius, random, roots, hits, sources, ref crossings);

            lines += Batch;
            area = 2 * Math.PI * radius * radius * crossings / lines;

            // Hair that may grow only in a few places can need a great many lines to find room for a
            // count of strands, and none at all where it may grow nowhere.
            if (growth.Count is not null && crossings > 20L * MostStrands)
                throw new Exception($"Hair could not find room for {growth.Count:N0} strands where it may grow.");

            if (growth.Count is null && roots.Count > MostStrands)
                throw TooMany();

            // A density is met once enough lines are cast, however many roots they found: it is
            // their number, not the roots', that sets how fine the sampling is.
            bool enough = growth.Count is { } count
                ? roots.Count >= count
                : lines >= growth.Density!.Value * 2 * Math.PI * radius * radius;

            if (enough || (roots.Count == 0 && lines >= Batch * 16))
                break;
        }

        // A density holds over the surface's whole area, of which the hair grows only where it may.
        int target = growth.Count ?? (int) Math.Round(growth.Density!.Value * area * roots.Count / Math.Max(crossings, 1));

        if (roots.Count > target)
            roots.RemoveRange(target, roots.Count - target);

        Hair hair = new ()
        {
            Host = host,
            _sourceOf = roots.Select(root => root.Source).ToArray(),
            StrandLength = growth.Length,
            Area = area,
            Material = growth.Material,
            NoShadow = host.NoShadow,
            Seed = host.Seed,
            Parent = host,
            Transform = inward
        };

        hair._sources.AddRange(sources.OrderBy(pair => pair.Value).Select(pair => pair.Key));

        if (roots.Count > 0)
            hair.Strands = Shape(roots, growth, area, random);

        return hair;

        static Exception TooMany() => new (
            $"Hair may hold at most {MostStrands:N0} strands, and this would need more; ask for fewer.");
    }

    /// <summary>
    /// This struct holds where a strand grows from and which way is out of the surface there, kept as
    /// plain numbers since there may be a great many of them.
    /// </summary>
    private readonly record struct Root(
        double X, double Y, double Z, double OutX, double OutY, double OutZ, int Source)
    {
        public Point Point => new (X, Y, Z);

        public Vector Normal => new (OutX, OutY, OutZ);
    }

    /// <summary>
    /// This method casts one line at the surface, from a random direction through a random point of the
    /// sphere's cross-section, and keeps every crossing as a root.
    /// <para>
    /// The surface is asked directly, under its own transform as it stands still, rather than through
    /// <see cref="Surface.Intersect"/>: that would take a moving surface where it stands at the
    /// shutter's opening, and bake the motion into the hair, which then moves with it as well.  A line
    /// carried into the surface's space by its transform is still a line, crossing it at the same
    /// distances along, so where it crosses is read off the line as it was cast.
    /// </para>
    /// </summary>
    private static void CastLine(
        Surface host, HairGrowth growth, Matrix inward, Matrix outward, Point center, double radius,
        Random random, List<Root> roots, List<Intersection> hits, Dictionary<Surface, int> sources,
        ref long crossings)
    {
        Vector direction = RandomUnit(random);
        (Vector across, Vector up) = Microfacets.FrameAround(direction);
        double reach = radius * Math.Sqrt(random.NextDouble());
        double turn = 2 * Math.PI * random.NextDouble();
        Point start = center + across * (reach * Math.Cos(turn)) + up * (reach * Math.Sin(turn)) -
                      direction * (2 * radius);
        Ray line = new (start, direction);
        Ray local = inward.Transform(line);

        hits.Clear();
        host.AddIntersections(local, hits);

        foreach (Intersection hit in hits)
        {
            // Hair does not grow on hair: a part of the host may have grown its own.
            if (hit.Surface is StrandSurface || NormalWithin(host, hit, local.At(hit.Distance)) is not { } normal)
                continue;

            Point point = line.At(hit.Distance);

            crossings++;
            normal = (outward * normal).Unit;

            // Hair that grows only in places grows here as often as the places say.
            if (growth.Patches is not null || growth.Facing is not null)
            {
                double chance = growth.Patches?.ValueAt(local.At(hit.Distance)) ?? 1;

                if (growth.Facing is not null)
                    chance *= Math.Pow(Math.Max(0, normal.Dot(growth.Facing.Unit)), growth.FacingPower);

                if (random.NextDouble() >= chance)
                    continue;
            }

            if (!sources.TryGetValue(hit.Surface, out int source))
                sources[hit.Surface] = source = sources.Count;

            roots.Add(new Root(point.X, point.Y, point.Z, normal.X, normal.Y, normal.Z, source));
        }
    }

    /// <summary>
    /// This method works out the normal at a crossing in the host's own space: from the surface that was
    /// struck, through every transform between it and the host.
    /// </summary>
    /// <param name="host">The surface the hair grows on.</param>
    /// <param name="hit">The crossing.</param>
    /// <param name="point">Where it is, in the host's own space.</param>
    /// <returns>The normal there, in the host's own space, or <c>null</c> if the surface struck has no
    /// way back to the host.</returns>
    private static Vector NormalWithin(Surface host, Intersection hit, Point point)
    {
        List<Surface> chain = [];
        Surface surface = hit.Surface;
        Surface portal = hit.Portal;

        // Up from the surface struck to the host, by way of the instance it was found through when it
        // is a shared shape -- whose own parents end before they reach the instance.
        while (surface != host)
        {
            if (surface is null)
                return null;

            chain.Add(surface);

            if (surface.Parent is null && portal is not null)
            {
                surface = portal;
                portal = null;
            }
            else
                surface = surface.Parent;
        }

        for (int index = chain.Count - 1; index >= 0; index--)
            point = chain[index].InverseTransformAt(0) * point;

        Vector normal = hit.Surface.SurfaceNormalAt(point, hit);

        foreach (Surface link in chain)
            normal = (link.TransposedInverseTransformAt(0) * normal).Unit;

        // Where a difference cuts into the surface, the cut face's normal points into what was cut
        // away; the hair must grow out of what is left.
        return hit.ShouldFlipInsideForOut ? -normal : normal;
    }

    /// <summary>
    /// This method shapes a strand from each root, gathers them into locks, and puts them in the compact
    /// store that is hit.
    /// <para>
    /// **Each strand goes into the store as soon as it is shaped**, rather than all being shaped first:
    /// there may be a couple of million of them, and a strand held as points to be looked at again
    /// takes several times the room it takes in the store.  Only the strands leading the locks are kept,
    /// since the rest are drawn toward them; they are shaped first so that they are there to be drawn
    /// toward.
    /// </para>
    /// </summary>
    private static StrandSet Shape(List<Root> roots, HairGrowth growth, double area, Random random)
    {
        int points = Math.Max(5, (int) Math.Ceiling(Math.Abs(growth.Curl) * 8) + 1);
        double thickness = growth.Thickness ?? growth.Length * 0.02;
        double[] widths = new double[points];
        int leaders = growth.Clump > 0 && roots.Count > 1
            ? Math.Max(1, roots.Count / Math.Max(1, growth.ClumpSize))
            : 0;
        Point[][] leading = new Point[leaders][];
        StrandSet strands = new (roots.Count * (points - 1));

        for (int index = 0; index < points; index++)
            widths[index] = thickness * (1 - (double) index / (points - 1));

        // The roots fell in no order, so the first few are as good a scattering of leaders as any.
        for (int index = 0; index < leaders; index++)
            leading[index] = ShapeOne(roots[index], points, growth, random);

        Locks locks = leaders > 0 ? new Locks(leading, area) : null;

        for (int index = 0; index < roots.Count; index++)
        {
            Point[] path = index < leaders ? leading[index] : ShapeOne(roots[index], points, growth, random);

            if (index >= leaders && locks?.LeaderNearest(path[0]) is { } lead)
            {
                for (int point = 1; point < points; point++)
                    path[point] += (lead[point] - path[point]) * (growth.Clump * point / (points - 1.0));
            }

            strands.Add(path, widths);
        }

        strands.Seal();

        return strands;
    }

    /// <summary>
    /// This class finds the strand leading the lock nearest a root, by way of a grid of cells about as
    /// wide as the locks are apart.
    /// </summary>
    private sealed class Locks
    {
        private readonly Point[][] _leading;
        private readonly double _cell;
        private readonly Dictionary<(long, long, long), List<int>> _grid = [];

        public Locks(Point[][] leading, double area)
        {
            _leading = leading;
            _cell = Math.Sqrt(Math.Max(area, 1e-12) / leading.Length);

            for (int leader = 0; leader < leading.Length; leader++)
            {
                (long, long, long) key = CellOf(leading[leader][0]);

                if (!_grid.TryGetValue(key, out List<int> members))
                    _grid[key] = members = [];

                members.Add(leader);
            }
        }

        /// <summary>
        /// This method returns the path of the leading strand nearest the given root, or <c>null</c> if
        /// none is close enough to lead it.
        /// </summary>
        public Point[] LeaderNearest(Point root)
        {
            (long x, long y, long z) = CellOf(root);
            Point[] nearest = null;
            double closest = double.MaxValue;

            for (long dx = -1; dx <= 1; dx++)
            for (long dy = -1; dy <= 1; dy++)
            for (long dz = -1; dz <= 1; dz++)
            {
                if (!_grid.TryGetValue((x + dx, y + dy, z + dz), out List<int> members))
                    continue;

                foreach (int leader in members)
                {
                    double distance = (_leading[leader][0] - root).Magnitude;

                    if (distance < closest)
                    {
                        closest = distance;
                        nearest = _leading[leader];
                    }
                }
            }

            return nearest;
        }

        private (long, long, long) CellOf(Point point)
        {
            return ((long) Math.Floor(point.X / _cell), (long) Math.Floor(point.Y / _cell),
                (long) Math.Floor(point.Z / _cell));
        }
    }

    /// <summary>
    /// This method shapes one strand: out from the surface, leaning toward the comb, sagging as it goes,
    /// winding about its own line if it curls, and wandering if it frizzes.
    /// </summary>
    private static Point[] ShapeOne(Root from, int points, HairGrowth growth, Random random)
    {
        Point root = from.Point;
        Vector normal = from.Normal;
        double length = growth.Length * (1 + growth.Vary * (2 * random.NextDouble() - 1));
        Vector sideways = SidewaysFrom(normal, growth.Comb, random);
        Vector start = normal * Math.Cos(growth.Lean) + sideways * Math.Sin(growth.Lean);
        double step = length / (points - 1);
        double curlRadius = growth.CurlRadius ?? length / 12;
        double phase = 2 * Math.PI * random.NextDouble();
        Point[] path = new Point[points];
        Point along = root;
        Vector frame = sideways;
        Vector wander = new (0, 0, 0);

        path[0] = root;

        for (int index = 1; index < points; index++)
        {
            double fraction = (index - 0.5) / (points - 1);
            Vector heading = Toward(start, Down, Math.Min(1, growth.Droop * fraction), sideways);

            along += heading * step;

            double s = (double) index / (points - 1);
            Point point = along;

            // Carry the frame along without twisting it, so a curl winds evenly.
            frame = (frame - heading * frame.Dot(heading)).Magnitude > 1e-9
                ? (frame - heading * frame.Dot(heading)).Unit
                : SidewaysFrom(heading, null, random);

            Vector other = heading.Cross(frame);

            // A curl winds out from the root over its first stretch, so the strand still leaves the
            // surface where it grows.
            if (growth.Curl != 0)
            {
                double angle = 2 * Math.PI * growth.Curl * s + phase;
                double reach = curlRadius * Math.Min(1, s / 0.15);

                point += (frame * Math.Cos(angle) + other * Math.Sin(angle)) * reach;
            }

            // Frizz wanders, each step from where the last one went, so a strand strays further the
            // longer it grows rather than jittering about its line.
            if (growth.Frizz > 0)
            {
                wander += (frame * (2 * random.NextDouble() - 1) + other * (2 * random.NextDouble() - 1)) *
                          (growth.Frizz * step * 0.5);
                point += wander;
            }

            path[index] = point;
        }

        return path;
    }

    /// <summary>
    /// This method returns a direction square to the given normal: the comb laid flat against the
    /// surface, or, where there is no comb or it points straight out, one picked at random.
    /// </summary>
    private static Vector SidewaysFrom(Vector normal, Vector comb, Random random)
    {
        if (comb is not null)
        {
            Vector flat = comb - normal * comb.Dot(normal);

            if (flat.Magnitude > 1e-9)
                return flat.Unit;
        }

        (Vector across, Vector up) = Microfacets.FrameAround(normal);
        double turn = 2 * Math.PI * random.NextDouble();

        return across * Math.Cos(turn) + up * Math.Sin(turn);
    }

    /// <summary>
    /// This method turns one direction toward another by the given fraction of the angle between them.
    /// <para>
    /// **Pointing exactly away, there is no one way to turn**, and a strand standing straight up never
    /// sagged at all -- which is every strand on a flat top, a rug's included.  It turns the way given
    /// instead, as a hair balanced upright falls whichever way it was already leaning.
    /// </para>
    /// </summary>
    /// <param name="from">The direction to turn.</param>
    /// <param name="to">The direction to turn it toward.</param>
    /// <param name="fraction">How much of the way to turn it.</param>
    /// <param name="otherwise">The way to turn when the two point exactly apart.</param>
    private static Vector Toward(Vector from, Vector to, double fraction, Vector otherwise)
    {
        if (fraction <= 0)
            return from;

        double cos = Math.Clamp(from.Dot(to), -1, 1);
        double angle = Math.Acos(cos);

        if (angle < 1e-9)
            return from;

        Vector other = to - from * cos;

        if (other.Magnitude < 1e-9)
            other = otherwise - from * otherwise.Dot(from);

        if (other.Magnitude < 1e-12)
            return from;

        other = other.Unit;

        return from * Math.Cos(angle * fraction) + other * Math.Sin(angle * fraction);
    }

    /// <summary>
    /// This method returns a direction picked evenly from every direction there is.
    /// </summary>
    private static Vector RandomUnit(Random random)
    {
        double z = 2 * random.NextDouble() - 1;
        double turn = 2 * Math.PI * random.NextDouble();
        double ring = Math.Sqrt(1 - z * z);

        return new Vector(ring * Math.Cos(turn), ring * Math.Sin(turn), z);
    }
}
