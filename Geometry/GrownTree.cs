using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a tree grown by space colonization (Runions, Lane and Prusinkiewicz, 2007): the space its
/// crown is to fill is scattered with points, and every round each point draws the nearest twig within its reach a
/// step toward it, until the points are used up.  So the tree's form comes from the room it
/// is given rather than from rules about angles and lengths, and no two branches divide alike.
/// <para>
/// It grows up from the origin along +Y until the crown's points first draw it, then out into the crown.  How thick
/// each branch is comes back down from the twigs by the pipe model: where branches join, the wood is as thick as the
/// branches it feeds put together, its radius raised to the joining being the sum of theirs.  Each run of wood between two forks is
/// drawn as a chain of tube segments meeting radius to radius, so a joint never shows, and a leaf -- one surface
/// shared by every place it stands -- goes at the end of every twig and at the two places before it.
/// </para>
/// </summary>
public class GrownTree : Group
{
    /// <summary>
    /// This property holds the crown: the space the tree is to fill, a closed surface or a group of them, any point
    /// inside any of which is room to grow into.
    /// </summary>
    public Surface Crown { get; set; }

    /// <summary>
    /// This property holds how many points are scattered through the crown.
    /// </summary>
    public int Points { get; set; } = 1500;

    /// <summary>
    /// This property holds how far a point reaches to draw a twig toward it.
    /// </summary>
    public double Reach { get; set; } = 1;

    /// <summary>
    /// This property holds how near a twig comes to a point before the point is used up.
    /// </summary>
    public double Kill { get; set; } = 0.25;

    /// <summary>
    /// This property holds how far a twig grows each round.
    /// </summary>
    public double Step { get; set; } = 0.1;

    /// <summary>
    /// This property holds the way every step of growth is bent a little toward, and how readily: a tree's own
    /// weight pulls down, light pulls up.  Nothing unless said.
    /// </summary>
    public Vector Tropism { get; set; }

    /// <summary>
    /// This property holds how readily growth bends toward the tropism.
    /// </summary>
    public double Susceptibility { get; set; }

    /// <summary>
    /// This property holds how thick a twig's end is: the radius every tip has, from which the rest of the wood's
    /// thickness follows.
    /// </summary>
    public double Twig { get; set; } = 0.01;

    /// <summary>
    /// This property holds the pipe model's exponent, how branches add up where they join: the wood's radius raised to
    /// it is the sum of the radii of the branches it feeds raised to it.  Two keeps the wood's cross-section; Leonardo said a tree does that, and the measurements put
    /// most trees between two and three.
    /// </summary>
    public double Joining { get; set; } = 2.5;

    /// <summary>
    /// This property holds how the tree's leaves are made, if it has any: one way or several, each place a leaf
    /// stands taking one of them at random.  A leaf is authored standing on the origin and growing up +Y, as the twig
    /// it ends runs, its face toward +Z -- which is how the trees library draws its blades and the clusters it hangs
    /// on its limbs.  (An L-system's leaf grows along +Z instead, its turtle's heading.)
    /// <para>
    /// **Several, because one is a tree of one cluster.**  Built trees gave every limb's cluster its own number, so
    /// no two turned alike in autumn; a grown tree sharing a single cluster turned all at once, a flat orange.
    /// </para>
    /// </summary>
    public List<Func<Surface>> LeafFactories { get; set; } = [];

    // Each way of making a leaf, made once: when the tree grows, or before, when it is asked whether it may be shared.
    private Surface[] _leaves;

    private Surface[] Leaves() => _leaves ??= LeafFactories.Select(factory => factory()).ToArray();

    /// <summary>
    /// This method reports whether the leaves the tree will hold may be shared between places, which the tree may be
    /// only if they may: a tree has no leaves until it grows, after it has been shared.
    /// </summary>
    internal bool MayShareWhatItHolds() => Leaves().All(Instance.MayBeShared);

    /// <summary>
    /// This property notes whether the leaves hang: stood upright in the tree's own space from the end of the twig,
    /// turned at random about the upright, rather than laid along it.  A willow's curtain hangs so, and so does fruit:
    /// what is hung is authored hanging down -Y from the origin.
    /// </summary>
    public bool Hanging { get; set; }

    // The most rounds a tree may grow: far past what any sensible tree needs, there only so that growth that
    // somehow never settles still stops.
    private const int MostRounds = 4000;

    /// <summary>
    /// This record holds one place on the tree: where it is, the place it grew from, how many grew from it in turn,
    /// and how thick the wood is there.
    /// </summary>
    private sealed class Node
    {
        public Point At;
        public int Parent;
        public int Children;
        public double Radius;
    }

    private List<Node> _nodes;

    /// <summary>
    /// This method grows the tree and builds its wood and leaves before the group it is prepares itself.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Crown is null)
            throw new Exception("A grown tree needs a crown: the space it is to grow into.");

        if (Points < 1 || Reach <= 0 || Kill <= 0 || Step <= 0 || Twig <= 0 || Joining <= 0)
            throw new Exception("A grown tree's points, reach, kill, step, twig and joining must all be more than nought.");

        if (Kill >= Reach)
            throw new Exception("A grown tree's kill must be less than its reach, or no point would draw a twig before using itself up.");

        // Grown once: a tree readied again keeps the wood it has, as a surface keeps its hair, rather than growing
        // a second tree through the first.
        if (_nodes is null)
        {
            Crown.PrepareForRendering();

            Random random = new (Seed ?? 0);
            List<Point> points = Scatter(random);

            _nodes = Grow(points);

            Thicken();
            Smooth();
            Build(random);
        }

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method scatters the points through the crown, evenly, by drawing places in the box round
    /// it and keeping those inside.
    /// </summary>
    private List<Point> Scatter(Random random)
    {
        BoundingBox box = BoxAround(Crown, true);

        if (box is null || box.IsEmpty || !double.IsFinite(box.Minimum.X + box.Minimum.Y + box.Minimum.Z) ||
            !double.IsFinite(box.Maximum.X + box.Maximum.Y + box.Maximum.Z))
            throw new Exception("A grown tree's crown must end somewhere: a plane or an open-ended shape has no inside to grow into.");

        List<Point> points = new (Points);
        Point low = box.Minimum;
        Vector size = box.Maximum - low;
        int tries = 0;

        while (points.Count < Points && tries++ < Points * 200)
        {
            Point place = new (
                low.X + random.NextDouble() * size.X,
                low.Y + random.NextDouble() * size.Y,
                low.Z + random.NextDouble() * size.Z);

            if (Containment.Holds(Crown, place))
                points.Add(place);
        }

        if (points.Count == 0)
            throw new Exception("A grown tree's crown has no inside to scatter its points through; it must be a closed surface.");

        return points;
    }

    /// <summary>
    /// This method grows the tree: up a trunk until the points draw it, then round after round, each point drawing
    /// its nearest place within its reach a step toward it, and each point used up once a place comes within
    /// its kill distance.
    /// </summary>
    private List<Node> Grow(List<Point> points)
    {
        List<Node> nodes = [new Node { At = Point.Zero, Parent = -1 }];
        Grid grid = new (Reach);
        bool[] used = new bool[points.Count];
        int live = points.Count;

        grid.Add(nodes[0].At, 0);

        double highest = points.Max(point => point.Y);

        // Up the trunk until some point is within reach of its top.
        while (!Draws(points, used, nodes[^1].At))
        {
            if (nodes[^1].At.Y > highest + Reach)
                throw new Exception("A grown tree's trunk climbed past its crown without coming within reach of it; the crown must stand over the origin, within the tree's reach of the line straight up.");

            AddNode(nodes, grid, nodes.Count - 1, new Vector(0, 1, 0));
        }

        for (int round = 0; round < MostRounds && live > 0; round++)
        {
            Dictionary<int, Vector> pulls = [];

            for (int index = 0; index < points.Count; index++)
            {
                if (used[index])
                    continue;

                int nearest = grid.Nearest(nodes, points[index], Reach);

                if (nearest < 0)
                    continue;

                Vector toward = (points[index] - nodes[nearest].At).Unit;

                pulls[nearest] = pulls.TryGetValue(nearest, out Vector sum) ? sum + toward : toward;
            }

            if (pulls.Count == 0)
                break;

            int before = nodes.Count;

            // In the order the places were made, so that a tree grows the same however its pulls were gathered.
            foreach (int index in pulls.Keys.Order())
            {
                Vector pull = pulls[index];

                if (pull.Magnitude < 1e-9)
                    continue;

                Vector heading = pull.Unit;

                if (Tropism is not null && Susceptibility != 0)
                    heading = (heading + Tropism.Unit * Susceptibility).Unit;

                AddNode(nodes, grid, index, heading);
            }

            if (nodes.Count == before)
                break;

            // A point is used up once anything grown this round comes within its kill distance; what grew in
            // earlier rounds used up its own then.
            for (int index = 0; index < points.Count; index++)
            {
                if (used[index])
                    continue;

                for (int node = before; node < nodes.Count; node++)
                {
                    if ((points[index] - nodes[node].At).Magnitude < Kill)
                    {
                        used[index] = true;
                        live--;
                        break;
                    }
                }
            }
        }

        return nodes;
    }

    /// <summary>
    /// This method reports whether any point not yet used up is within reach of a place.
    /// </summary>
    private bool Draws(List<Point> points, bool[] used, Point place)
    {
        for (int index = 0; index < points.Count; index++)
        {
            if (!used[index] && (points[index] - place).Magnitude < Reach)
                return true;
        }

        return false;
    }

    /// <summary>
    /// This method grows a new place a step on from another, unless growth has put one there already.
    /// <para>
    /// **That guard is what keeps the tree a tree.**  A place drawn equally from two sides grows a step between
    /// them that comes no nearer either, so both go on drawing the same place, round after round, to the same
    /// spot: unguarded, a place stuck so grew a fresh twig there every round, each a tip of its own, and the
    /// pipe model swelled every limb below it into a sausage.
    /// </para>
    /// </summary>
    private void AddNode(List<Node> nodes, Grid grid, int parent, Vector heading)
    {
        Point at = nodes[parent].At + heading * Step;

        if (grid.Nearest(nodes, at, Step * 0.25) >= 0)
            return;

        nodes.Add(new Node { At = at, Parent = parent });
        nodes[parent].Children++;
        grid.Add(at, nodes.Count - 1);
    }

    /// <summary>
    /// This method works out how thick the wood is everywhere, from the twigs down by the pipe model.  A place's
    /// children were all made after it, so walking the places backwards finishes every child before its parent.
    /// </summary>
    private void Thicken()
    {
        double[] sum = new double[_nodes.Count];

        for (int index = _nodes.Count - 1; index >= 0; index--)
        {
            Node node = _nodes[index];

            node.Radius = node.Children == 0 ? Twig : Math.Pow(sum[index], 1 / Joining);

            if (node.Parent >= 0)
                sum[node.Parent] += Math.Pow(node.Radius, Joining);
        }
    }

    /// <summary>
    /// This method eases the zigzag that growth toward scattered points leaves: twice over, every place with one
    /// child moves halfway toward the middle of its neighbors.  Forks and tips stay where they grew, so the tree's
    /// shape and every branch's start are kept.
    /// </summary>
    private void Smooth()
    {
        // A place's one child, found once: every child is made after its parent, so walking backwards leaves each
        // place holding the first of its children.
        int[] childOf = new int[_nodes.Count];

        for (int index = _nodes.Count - 1; index > 0; index--)
            childOf[_nodes[index].Parent] = index;

        for (int pass = 0; pass < 2; pass++)
        {
            Point[] moved = new Point[_nodes.Count];

            for (int index = 0; index < _nodes.Count; index++)
            {
                Node node = _nodes[index];

                moved[index] = node.At;

                if (node.Parent < 0 || node.Children != 1)
                    continue;

                Point parent = _nodes[node.Parent].At;
                Point middle = parent + (_nodes[childOf[index]].At - parent) * 0.5;

                moved[index] = node.At + (middle - node.At) * 0.5;
            }

            for (int index = 0; index < _nodes.Count; index++)
                _nodes[index].At = moved[index];
        }
    }

    /// <summary>
    /// This method builds the wood and the leaves: a tube segment from every place to each it grew, as thick as the
    /// wood it grew to -- which, the pipe model being what it is, is as thick as the place it grew from unless that
    /// place is a fork, where a branch leaves from inside the thicker wood -- and a leaf -- a shared one, where the
    /// leaf may be shared -- at the end of every twig and at the two places before it, each turned about its twig at
    /// random.
    /// </summary>
    private void Build(Random random)
    {
        for (int index = 1; index < _nodes.Count; index++)
        {
            Node node = _nodes[index];
            Node parent = _nodes[node.Parent];

            Add(new TubeSegment
            {
                Start = parent.At, StartRadius = node.Radius,
                End = node.At, EndRadius = node.Radius
            });
        }

        if (LeafFactories.Count == 0)
            return;

        // Each way of making a leaf is made once, and shared where it may be.  A leaf of nothing stands nowhere: a
        // winter cluster comes back an empty group, and a thousand instances of it would cost a thousand boxes to
        // show nothing -- but it keeps its share of the places, so a winter tree of several clusters, some bare,
        // is bare in the same proportion.
        Surface[] made = Leaves();
        bool[] empty = made.Select(leaf => leaf.GetType() == typeof(Group) && ((Group) leaf).Surfaces.Count == 0).ToArray();
        bool[] sharable = made.Select(Instance.MayBeShared).ToArray();
        bool[] used = new bool[made.Length];

        if (empty.All(nothing => nothing))
            return;

        for (int index = 0; index < _nodes.Count; index++)
        {
            if (_nodes[index].Children != 0)
                continue;

            int place = index;

            for (int leaf = 0; leaf < 3 && place > 0; leaf++)
            {
                Node node = _nodes[place];
                int pick = made.Length == 1 ? 0 : random.Next(made.Length);
                double turn = random.NextDouble();

                if (!empty[pick])
                {
                    Surface copy = sharable[pick] ? new Instance { Prototype = made[pick] }
                        : !used[pick] ? made[pick]
                        : LeafFactories[pick]();

                    used[pick] = true;
                    copy.Transform = PlacementAt(node, turn) * copy.Transform;
                    Add(copy);
                }

                place = node.Parent;

                if (_nodes[place].Children != 1)
                    break;
            }
        }
    }

    /// <summary>
    /// This method returns the matrix that stands a leaf at a place, its base there.  Laid along the twig, its +Y runs
    /// the way the wood coming into the place runs -- as drawn, smoothed, rather than the way it first grew -- and its
    /// face, +Z, turns toward the sky, as a leaf's does, give or take an eighth of a turn either way; on a twig
    /// standing upright, which has no sky to one side of it, it turns any way at all.  Hanging, it stands upright in
    /// the tree's own space, turned any way about the upright.
    /// </summary>
    /// <param name="node">The place the leaf stands.</param>
    /// <param name="draw">A draw, from nought to one, that sets how far it is turned.</param>
    private Matrix PlacementAt(Node node, double draw)
    {
        if (Hanging)
        {
            double angle = draw * Math.PI * 2;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            return new Matrix([
                cos, 0, sin, node.At.X,
                0, 1, 0, node.At.Y,
                -sin, 0, cos, node.At.Z,
                0, 0, 0, 1
            ]);
        }

        Vector along = (node.At - _nodes[node.Parent].At).Unit;
        Vector sky = new Vector(0, 1, 0) - along * along.Y;
        Vector face;
        double spread;

        if (sky.Magnitude > 0.05)
        {
            face = sky.Unit;
            spread = (draw - 0.5) * Math.PI / 2;
        }
        else
        {
            face = Math.Abs(along.X) < 0.9
                ? new Vector(1, 0, 0).Cross(along).Unit
                : new Vector(0, 0, 1).Cross(along).Unit;
            spread = draw * Math.PI * 2;
        }

        Vector z = (face * Math.Cos(spread) + along.Cross(face) * Math.Sin(spread)).Unit;
        Vector x = along.Cross(z).Unit;

        return new Matrix([
            x.X, along.X, z.X, node.At.X,
            x.Y, along.Y, z.Y, node.At.Y,
            x.Z, along.Z, z.Z, node.At.Z,
            0, 0, 0, 1
        ]);
    }

    /// <summary>
    /// This class holds the places grown so far in cells as wide as the reach, so that finding the nearest place
    /// within reach of a point looks only at the cells round it.
    /// </summary>
    private sealed class Grid(double size)
    {
        private readonly Dictionary<(int, int, int), List<int>> _cells = [];

        private (int, int, int) CellOf(Point at) =>
            ((int) Math.Floor(at.X / size), (int) Math.Floor(at.Y / size), (int) Math.Floor(at.Z / size));

        public void Add(Point at, int index)
        {
            (int, int, int) cell = CellOf(at);

            if (!_cells.TryGetValue(cell, out List<int> list))
                _cells[cell] = list = [];

            list.Add(index);
        }

        public int Nearest(List<Node> nodes, Point at, double reach)
        {
            (int cx, int cy, int cz) = CellOf(at);
            int best = -1;
            double bestDistance = reach;

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!_cells.TryGetValue((cx + dx, cy + dy, cz + dz), out List<int> list))
                    continue;

                foreach (int index in list)
                {
                    double distance = (nodes[index].At - at).Magnitude;

                    if (distance < bestDistance || (distance == bestDistance && index < best))
                    {
                        bestDistance = distance;
                        best = index;
                    }
                }
            }

            return best;
        }
    }
}
