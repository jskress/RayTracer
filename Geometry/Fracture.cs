using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a solid broken into pieces: seeds are scattered through it, and each piece is the part of
/// the solid nearer its own seed than any other -- the seed's Voronoi cell, which is the inside of the planes halfway
/// between it and its neighbors, and so a convex polyhedron.  The pieces fit together exactly, so a crack is only as
/// wide as the gap asked for, and each may be worn round at its edges, moved off and turned.
/// <para>
/// A box -- a cube carried by any transform, or an intersection of them -- is broken exactly: its own six planes
/// bound every piece, and a piece is one polyhedron.  Any other closed solid is broken by cutting each piece from it,
/// the cell and the solid meeting in a CSG intersection, the solid shared between the pieces rather than copied.
/// </para>
/// </summary>
public class Fracture : Group
{
    /// <summary>
    /// This property holds the solid that is broken.
    /// </summary>
    public Surface Solid { get; set; }

    /// <summary>
    /// This property holds how many pieces it breaks into, near enough: as many seeds as this are scattered, and a
    /// seed whose cell is gone to nothing makes no piece.
    /// </summary>
    public int Pieces { get; set; } = 20;

    /// <summary>
    /// This property holds how wide the cracks between pieces are.
    /// </summary>
    public double Gap { get; set; }

    /// <summary>
    /// This property holds how far each piece's edges and corners are rounded.
    /// </summary>
    public double Rounded { get; set; }

    /// <summary>
    /// This property notes whether the solid is broken only across X and Z, every crack running straight up and
    /// down: paving, dried mud, basalt columns.
    /// </summary>
    public bool Flat { get; set; }

    /// <summary>
    /// This property holds how much longer pieces come out along each axis than across it: [2, 1, 1] makes them
    /// twice as long in X, as courses of wall stone are.
    /// </summary>
    public Vector Stretched { get; set; }

    /// <summary>
    /// This property holds a point the pieces grow smaller toward, as a pane's shards do round where it was struck;
    /// a thin solid breaks there in rings and spokes, a thick one only more finely.
    /// </summary>
    public Point Toward { get; set; }

    /// <summary>
    /// This property holds how far round the point the pieces are gone, a hole struck through.
    /// </summary>
    public double Hole { get; set; }

    /// <summary>
    /// This property holds how far each piece is moved off from the middle of the solid -- from the point struck,
    /// when there is one.
    /// </summary>
    public double Apart { get; set; }

    /// <summary>
    /// This property holds, in radians, the most each piece is turned about its own middle.
    /// </summary>
    public double Tumble { get; set; }

    /// <summary>
    /// This property notes whether the pieces are loose stones rather than one thing broken: each is given its own
    /// place in its material's pattern, so the grain of one does not run on into the next.
    /// </summary>
    public bool Loose { get; set; }

    /// <summary>
    /// This property notes whether the pieces are whole: never cut by the solid, but kept where their seeds lie
    /// inside it, so its outline is made of whole stones -- a heap of rubble, a cairn -- rather than of the solid's
    /// own skin.  Seeds are scattered round it as well, so the outermost pieces are no bigger than the rest.
    /// </summary>
    public bool Whole { get; set; }

    /// <summary>
    /// This property holds materials for the pieces, each piece taking one at random; with none, they wear the
    /// fracture's own.
    /// </summary>
    public List<Material> PieceMaterials { get; set; } = [];

    // How many spokes a thin solid struck at a point breaks into round it.
    private const int Spokes = 9;

    private bool _broken;

    /// <summary>
    /// This method breaks the solid, once, before the group it is prepares itself.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (!_broken)
        {
            _broken = true;

            Break();
        }

        base.PrepareSurfaceForRendering();
    }

    private void Break()
    {
        if (Solid is null)
            throw new Exception("A fracture needs a solid: the thing that is broken.");

        if (Pieces < 1 || Gap < 0 || Rounded < 0 || Apart < 0 || Tumble < 0 || Hole < 0)
            throw new Exception("A fracture's pieces must be one or more, and its gap, rounding, hole, apart and tumble no less than nought.");

        if (Stretched is not null && (Stretched.X <= 0 || Stretched.Y <= 0 || Stretched.Z <= 0))
            throw new Exception("A fracture's stretch must be more than nought along every axis.");

        Solid.PrepareForRendering();

        Polytope exact = Whole ? null : ConvexOf(Solid);
        Polytope start = exact;

        if (start is null)
        {
            BoundingBox box = BoxAround(Solid, true);

            if (box is null || box.IsEmpty || !double.IsFinite(box.Minimum.X + box.Minimum.Y + box.Minimum.Z) ||
                !double.IsFinite(box.Maximum.X + box.Maximum.Y + box.Maximum.Z))
            {
                throw new Exception("A fracture's solid must end somewhere: a plane or an open-ended shape cannot be broken into pieces.");
            }

            // A little larger than the solid, so no face of a piece lies on the solid's own surface: a piece is then
            // bounded there by the solid alone, and coincident faces in a CSG are what speckle a pane of glass.
            Vector margin = (box.Maximum - box.Minimum) * 0.01 + new Vector(1e-6, 1e-6, 1e-6);

            start = Polytope.Box(box.Minimum - margin, box.Maximum + margin);
        }

        if (exact is null && !Whole && !Instance.MayBeShared(Solid))
            throw new Exception("A fracture can break only a solid that may be shared among its pieces; one that glows, holds a medium or moves cannot.");

        Random random = new (Seed ?? 0);
        double size = start.Size;
        Vector stretch = Stretched ?? new Vector(1, 1, 1);
        Vector scale = Flat ? new Vector(1 / stretch.X, 0, 1 / stretch.Z) : new Vector(1 / stretch.X, 1 / stretch.Y, 1 / stretch.Z);
        List<Point> seeds = Scatter(random, start, exact, out int kept);

        if (kept == 0)
            throw new Exception("A fracture's solid has no inside to scatter its seeds through; it must be a closed surface.");

        BoundingBox bounds = start.Bounds();
        Point middle = Toward ?? bounds.Minimum + (bounds.Maximum - bounds.Minimum) * 0.5;

        for (int index = 0; index < kept; index++)
        {
            Polytope cell = CellOf(index, seeds, start, scale);

            // Every draw for a piece is made whether or not the piece is kept, so one piece going does not change
            // the next.
            Vector turnAbout = new Vector(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
            double turnBy = random.NextDouble() * Tumble;
            Vector shift = new (random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100, random.NextDouble() * 200 - 100);
            int pick = random.Next(Math.Max(1, PieceMaterials.Count));

            if (cell is null || cell.Volume() <= size * size * size * 1e-12)
                continue;

            Point centroid = cell.Centroid();

            if (Toward is not null && Hole > 0 && (centroid - Toward).Magnitude < Hole)
                continue;

            Matrix placed = Matrix.Identity;

            if (Apart > 0 && (centroid - middle).Magnitude > size * 1e-9)
                placed = Transforms.Translate(Point.Zero + (centroid - middle).Unit * Apart) * placed;

            if (Tumble > 0 && turnAbout.Magnitude > 1e-9)
            {
                placed = Transforms.Translate(centroid) * RotationAbout(turnAbout.Unit, turnBy) *
                         Transforms.Translate(Point.Zero - (centroid - Point.Zero)) * placed;
            }

            Vector offset = Loose ? shift : new Vector(0, 0, 0);
            Surface piece = PieceOf(cell.Carried(Transforms.Translate(Point.Zero - offset)));

            if (exact is null && !Whole)
            {
                Surface solid = new Instance { Prototype = Solid, Transform = Transforms.Translate(Point.Zero - offset) };

                piece = new CsgSurface { Operation = CsgOperation.Intersection, Left = piece, Right = solid };
            }

            piece.Transform = placed * Transforms.Translate(Point.Zero + offset);

            if (PieceMaterials.Count > 0)
                piece.Material = PieceMaterials[pick];

            Add(piece);
        }
    }

    /// <summary>
    /// This method returns a solid as the polytope it exactly is, if it is one: a cube under any transform, an
    /// intersection of such things, or a group holding just one of them -- which is what a solid written out in
    /// braces is.  Anything else is <c>null</c>, and is cut from rather than counted.
    /// </summary>
    internal static Polytope ConvexOf(Surface surface)
    {
        switch (surface)
        {
            case Group { Surfaces.Count: 1 } group when group.GetType() == typeof(Group) && group.MotionAt is null:
                return ConvexOf(group.Surfaces[0])?.Carried(group.Transform);
            case Cube cube when cube.MotionAt is null:
                return Polytope.Cube(cube.Transform);
            case CsgSurface { Operation: CsgOperation.Intersection } csg when csg.MotionAt is null:
            {
                Polytope left = ConvexOf(csg.Left);
                Polytope right = ConvexOf(csg.Right);

                if (left is null || right is null)
                    return null;

                foreach (Polytope.Face face in right.Faces)
                {
                    left = left.Clip(face.Normal, face.Offset);

                    if (left is null)
                        return null;
                }

                return left.Carried(csg.Transform);
            }
            default:
                return null;
        }
    }

    // The seeds: evenly through the solid, or gathered toward the point struck.  Those whose cells become pieces come
    // first, and how many is handed back; for whole pieces, the seeds round the solid that only bound them follow.
    private List<Point> Scatter(Random random, Polytope start, Polytope exact, out int kept)
    {
        BoundingBox box = start.Bounds();
        Vector side = box.Maximum - box.Minimum;
        List<Point> seeds = [];

        if (Whole)
        {
            List<Point> round = [];

            for (int tries = 0; seeds.Count < Pieces && tries < Pieces * 400; tries++)
            {
                Point place = new (
                    box.Minimum.X + random.NextDouble() * side.X,
                    box.Minimum.Y + random.NextDouble() * side.Y,
                    box.Minimum.Z + random.NextDouble() * side.Z);

                (Containment.Holds(Solid, place) ? seeds : round).Add(place);
            }

            kept = seeds.Count;
            seeds.AddRange(round);

            return seeds;
        }

        kept = 0;

        bool Inside(Point place) => exact is not null
            ? exact.Faces.All(face => face.Normal.Dot(place - Point.Zero) <= face.Offset)
            : Containment.Holds(Solid, place);

        Point Anywhere() => new (
            box.Minimum.X + random.NextDouble() * side.X,
            box.Minimum.Y + random.NextDouble() * side.Y,
            box.Minimum.Z + random.NextDouble() * side.Z);

        if (Toward is null)
        {
            for (int tries = 0; seeds.Count < Pieces && tries < Pieces * 200; tries++)
            {
                Point place = Anywhere();

                if (Inside(place))
                    seeds.Add(place);
            }

            kept = seeds.Count;

            return seeds;
        }

        // A thin solid -- a pane, a slab -- struck at a point breaks in rings and spokes round it: seeds on rings
        // growing outward in a steady ratio, nine to a ring, each turned a little, in the plane of its two longest
        // sides.  A thick one is only broken more finely toward the point: seeds kept the likelier the nearer they are.
        double[] extents = [side.X, side.Y, side.Z];
        int[] order = [0, 1, 2];

        Array.Sort(extents.ToArray(), order);

        int thin = Flat ? 1 : order[0];
        bool isThin = Flat || extents[thin] < extents[order[1]] * 0.25;

        if (isThin)
        {
            Vector[] axes = [new Vector(1, 0, 0), new Vector(0, 1, 0), new Vector(0, 0, 1)];
            Vector first = axes[thin == 0 ? 1 : 0];
            Vector second = axes[thin == 2 ? 1 : 2];
            double middle = (box.Minimum - Point.Zero).Dot(axes[thin]) + extents[thin] / 2;
            Point center = Toward + axes[thin] * (middle - (Toward - Point.Zero).Dot(axes[thin]));
            double farthest = Enumerable.Range(0, 8)
                .Select(corner => new Point(
                    (corner & 1) == 0 ? box.Minimum.X : box.Maximum.X,
                    (corner & 2) == 0 ? box.Minimum.Y : box.Maximum.Y,
                    (corner & 4) == 0 ? box.Minimum.Z : box.Maximum.Z))
                .Max(corner => (corner - center).Magnitude);
            int rings = Math.Max(1, (Pieces - 1) / Spokes);
            // The rings begin at the edge of the hole, if there is one: inside it they would only be struck out.
            double nearest = Math.Max(farthest * 0.015, Hole * 0.9);
            double ratio = Math.Pow(farthest / nearest, 1.0 / Math.Max(1, rings - 1));

            // The spokes keep their angles from ring to ring, give or take a little, so the cracks between them run
            // out from the point the way a pane's do, rather than every ring breaking its own way.
            double[] spokes = Enumerable.Range(0, Spokes)
                .Select(spoke => (spoke + (random.NextDouble() - 0.5) * 0.6) * Math.PI * 2 / Spokes)
                .ToArray();

            seeds.Add(center);

            for (int ring = 0; ring < rings; ring++)
            {
                double radius = nearest * Math.Pow(ratio, ring) * (0.9 + random.NextDouble() * 0.2);

                for (int spoke = 0; spoke < Spokes; spoke++)
                {
                    double angle = spokes[spoke] + (random.NextDouble() - 0.5) * 0.12;
                    Point place = center + first * (Math.Cos(angle) * radius) + second * (Math.Sin(angle) * radius);

                    if (Inside(place))
                        seeds.Add(place);
                }
            }

            kept = seeds.Count;

            return seeds;
        }

        double close = Math.Max(side.X, Math.Max(side.Y, side.Z)) * 0.08;

        for (int tries = 0; seeds.Count < Pieces && tries < Pieces * 400; tries++)
        {
            Point place = Anywhere();
            double distance = (place - Toward).Magnitude;

            if (Inside(place) && random.NextDouble() < Math.Pow(close / (close + distance), 2))
                seeds.Add(place);
        }

        kept = seeds.Count;

        return seeds;
    }

    // A seed's cell: what is left of the solid after it is cut by the plane halfway to every neighbor that could
    // reach it -- nearest first, and stopping once the next is too far off to cut anything left -- each plane drawn
    // in by half the gap.  Distances are taken in the stretched space, where the cells are as round as they come.
    private Polytope CellOf(int index, List<Point> seeds, Polytope start, Vector scale)
    {
        Point seed = seeds[index];

        double Apart2(Point other)
        {
            Vector difference = other - seed;

            return difference.X * difference.X * scale.X * scale.X + difference.Y * difference.Y * scale.Y * scale.Y +
                   difference.Z * difference.Z * scale.Z * scale.Z;
        }

        List<int> neighbors = Enumerable.Range(0, seeds.Count)
            .Where(other => other != index)
            .OrderBy(other => Apart2(seeds[other]))
            .ToList();
        Polytope cell = start;
        double reach = double.PositiveInfinity;

        for (int at = 0; at < neighbors.Count; at++)
        {
            Point other = seeds[neighbors[at]];
            double apart = Apart2(other);

            if (at % 8 == 0)
                reach = cell.Vertices().Max(Apart2);

            // A neighbor further than twice the cell's farthest corner cannot cut it.
            if (apart > 4 * reach)
                break;

            if (apart <= 0)
                continue;

            // The plane halfway between, in the stretched space: points x with |S(x - seed)| = |S(x - other)|.
            Vector difference = other - seed;
            Vector normal = new (difference.X * scale.X * scale.X, difference.Y * scale.Y * scale.Y, difference.Z * scale.Z * scale.Z);
            Point halfway = seed + difference * 0.5;
            double length = normal.Magnitude;

            if (length <= 0)
                continue;

            normal /= length;
            cell = cell.Clip(normal, normal.Dot(halfway - Point.Zero) - Gap / 2);

            if (cell is null)
                return null;
        }

        return cell;
    }

    // A piece: the cell itself, or the cell drawn in and grown back round, its edges worn.  A cell too small to
    // draw in so far is worn by half as much, and if that is still too much, not at all.
    private Polyhedron Sharp(Polytope cell)
    {
        Polyhedron piece = new ();

        piece.SetFrom(cell);

        return piece;
    }

    private Surface PieceOf(Polytope cell)
    {
        if (Rounded <= 0)
            return Sharp(cell);

        foreach (double radius in new[] { Rounded, Rounded / 2 })
        {
            Polytope inner = cell.Shrunk(radius);

            if (inner is null)
                continue;

            RoundedPolyhedron piece = new ();

            piece.SetFrom(inner, radius);

            return piece;
        }

        return Sharp(cell);
    }

    // A turn about a unit axis, by Rodrigues.
    private static Matrix RotationAbout(Vector axis, double angle)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double one = 1 - cos;
        double x = axis.X, y = axis.Y, z = axis.Z;

        return new Matrix([
            cos + x * x * one, x * y * one - z * sin, x * z * one + y * sin, 0,
            y * x * one + z * sin, cos + y * y * one, y * z * one - x * sin, 0,
            z * x * one - y * sin, z * y * one + x * sin, cos + z * z * one, 0,
            0, 0, 0, 1
        ]);
    }
}
