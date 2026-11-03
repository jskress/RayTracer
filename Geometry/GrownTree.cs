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
/// <para>
/// **Or it climbs a surface**, as ivy climbs a wall: given the name of a surface standing beside it rather than a
/// crown, its points are spread over that surface as a field all over it spreads its copies, and every step of
/// growth is walked along the surface -- on across a face, into a corner, round an edge -- so its stems lie on it.
/// It grows from the place on the surface nearest where it stands, and its leaves stand out of the surface.
/// </para>
/// </summary>
public class GrownTree : Group, ICoversASurface
{
    /// <summary>
    /// This property holds the crown: the space the tree is to fill, a closed surface or a group of them, any point
    /// inside any of which is room to grow into.
    /// </summary>
    public Surface Crown { get; set; }

    /// <summary>
    /// This property holds the name of the surface the tree climbs, standing beside it in the same group, in place
    /// of a crown: its points are spread over the whole of it, and its stems grow along it.
    /// </summary>
    public string AllOver { get; set; }

    /// <summary>
    /// This property holds the lowest a point on the surface climbed may be, in the space the tree shares with it,
    /// or <c>null</c> for no limit.
    /// </summary>
    public double? Above { get; set; }

    /// <summary>
    /// This property holds the highest a point on the surface climbed may be, in the space the tree shares with it,
    /// or <c>null</c> for no limit.
    /// </summary>
    public double? Below { get; set; }

    /// <summary>
    /// This property holds the steepest the surface climbed may be where a point stands, as an angle from level, in
    /// radians, or <c>null</c> for no limit.
    /// </summary>
    public double? Steepest { get; set; }

    /// <summary>
    /// This property holds the way the surface climbed must face for a point to stand on it: the chance one stands
    /// is how squarely it faces that way, as a field's is.  Unsaid, points stand whichever way it faces.
    /// </summary>
    public Vector Facing { get; set; }

    /// <summary>
    /// This property holds how strongly points favor the surface facing squarely the way they must.
    /// </summary>
    public double FacingPower { get; set; } = 1;

    /// <summary>
    /// This property holds a pattern whose value at a place on the surface climbed, read in the tree's own space, is
    /// the chance a point stands there, between the edge's two ends.  Unsaid, points stand everywhere.
    /// </summary>
    public DensityShape Patches { get; set; }

    /// <summary>
    /// This property holds the patterned value below which no point stands.
    /// </summary>
    public double PatchesFrom { get; set; }

    /// <summary>
    /// This property holds the patterned value at and above which a point always stands.
    /// </summary>
    public double PatchesTo { get; set; } = 1;

    /// <summary>
    /// This property holds how far apart a climber's points stand on the surface it climbs, in place of how many
    /// there are, or <c>null</c> to spread <see cref="Points"/> of them: one for every square of this side.  It is
    /// what a climber made for any surface says, not knowing how big the surface will be.
    /// </summary>
    public double? Spacing { get; set; }

    /// <summary>
    /// This property holds the furthest the tree's wood may run from where it starts, measured along it, or
    /// <c>null</c> for no limit: how far a climber has spread from its root.  What lies further is left unreached.
    /// </summary>
    public double? Spread { get; set; }

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

    // On a surface climbed, the thickest wood that still bears leaves, in twigs: a climber's leaves stand all along
    // its shoots, but not on the old stems that carry them.
    private const double LeafyWood = 6;

    /// <summary>
    /// This record holds one place on the tree: where it is, the place it grew from, how many grew from it in turn,
    /// and how thick the wood is there -- and on a surface climbed, the way out of the surface there and how far
    /// the wood has run to reach it.
    /// </summary>
    private sealed class Node
    {
        public Point At;
        public int Parent;
        public int Children;
        public double Radius;
        public Vector Normal;
        public double Run;
    }

    private List<Node> _nodes;

    // What walks a climber's growth along the surface it climbs, while it grows.
    private Walker _walker;

    /// <summary>
    /// This method grows the tree and builds its wood and leaves before the group it is prepares itself -- or, for a
    /// tree that climbs a surface, leaves it empty until the surface has been placed, when it grows over it.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (AllOver is not null && Crown is not null)
            throw new Exception("A grown tree grows into a crown or climbs a surface all over, not both.");

        if (AllOver is null && Crown is null)
            throw new Exception("A grown tree needs a crown, the space it is to grow into, or a surface to climb all over.");

        if (AllOver is null && (Above is not null || Below is not null || Steepest is not null || Facing is not null ||
                                Patches is not null))
        {
            throw new Exception(
                "A grown tree's above, below, steepest, facing and patches keep it to part of a surface it climbs all " +
                "over; one growing into a crown has the crown's shape for that.");
        }

        if (Points < 1 || Reach <= 0 || Kill <= 0 || Step <= 0 || Twig <= 0 || Joining <= 0)
            throw new Exception("A grown tree's points, reach, kill, step, twig and joining must all be more than nought.");

        if (Kill >= Reach)
            throw new Exception("A grown tree's kill must be less than its reach, or no point would draw a twig before using itself up.");

        if (Spread is <= 0)
            throw new Exception("How far a grown tree spreads must be more than nought.");

        if (Spacing is not null && AllOver is null)
            throw new Exception("A grown tree's spacing is how far apart its points stand on a surface it climbs all over; one growing into a crown is given its points.");

        if (Spacing is <= 0)
            throw new Exception("A grown tree's spacing must be more than nought.");

        if (FacingPower <= 0)
            throw new Exception("How strongly a grown tree favors a facing must be more than nought.");

        // Grown once: a tree readied again keeps the wood it has, as a surface keeps its hair, rather than growing
        // a second tree through the first.  A climber grows when the surface it climbs is placed.
        if (_nodes is null && AllOver is null)
        {
            Crown.PrepareForRendering();

            Random random = new (Seed ?? 0);
            List<Point> points = Scatter(random);

            _nodes = Grow(points, null);

            Thicken();
            Smooth();
            Build(random);
        }

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method grows a climber over the surface it climbs, once that surface stands where it was told to.  It
    /// is called where a dropped field is dropped (see <see cref="PlacementSettler"/>), at the one moment the
    /// tree's siblings are prepared and not yet arranged, and prepares the tree again now it holds something.
    /// </summary>
    /// <param name="ground">The surface to climb, standing beside the tree.</param>
    internal void GrowOver(Surface ground)
    {
        if (_nodes is null)
        {
            Random random = new (Seed ?? 0);

            _walker = new Walker(this, ground);

            List<Point> points = SpreadOver(ground);

            _nodes = points.Count == 0 ? [] : Grow(points, _walker.Root());

            Thicken();
            Smooth();
            Build(random);

            _walker = null;
        }

        base.PrepareSurfaceForRendering();

        BoundingBox = GetDefaultBoundingBox();
        BoundingBox?.Expand();
    }

    /// <summary>
    /// This method spreads a climber's points over the surface it climbs, keeping those its limits keep.  The number
    /// asked for stands on what is kept: when the limits leave part of the surface, the points are spread again,
    /// more of them, so the part climbed holds as many as were asked.
    /// </summary>
    /// <param name="ground">The surface to climb.</param>
    /// <returns>The points, in the tree's own space.</returns>
    private List<Point> SpreadOver(Surface ground)
    {
        int seed = Seed ?? 0;
        string endless = $"A grown tree may climb only a surface with an end to it, and '{AllOver}' goes on forever.";

        if (Spacing is { } spacing)
        {
            return KeptOf(
                    SurfaceSpread.Over(
                        ground, Transform, spacing, seed,
                        wanted => $"A grown tree climbing '{AllOver}' would spread {wanted:N0} points over it, and may " +
                                  $"spread at most {SurfaceSpread.Most:N0}; give it a wider spacing.",
                        endless),
                    seed)
                .Select(spot => spot.Point)
                .ToList();
        }

        List<SurfaceSpread.Spot> spread = SurfaceSpread.Over(ground, Transform, Points, seed, endless);
        List<SurfaceSpread.Spot> kept = KeptOf(spread, seed);

        if (kept.Count > 0 && kept.Count < spread.Count)
        {
            int more = (int) Math.Min(SurfaceSpread.Most, Math.Round((double) Points * spread.Count / kept.Count));

            kept = KeptOf(SurfaceSpread.Over(ground, Transform, more, seed, endless), seed);
        }

        return kept.Select(spot => spot.Point).ToList();
    }

    /// <summary>
    /// This method returns the places spread over the surface climbed that the tree's limits keep.
    /// </summary>
    private List<SurfaceSpread.Spot> KeptOf(List<SurfaceSpread.Spot> spread, int seed) =>
        SurfaceSpread.KeptBy(this, spread, seed)
            .Where(spot => Patches is null || SurfaceSpread.KeptByThePatches(this, spot.Point, spot.Number, 0, seed))
            .ToList();

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
    /// its kill distance.  A climber starts from its root on the surface instead, and its first stem grows along
    /// the surface toward the nearest point until the points draw it.
    /// </summary>
    /// <param name="points">The points to grow toward.</param>
    /// <param name="root">Where a climber starts, on the surface it climbs, or <c>null</c> for a tree, which starts
    /// at its origin.</param>
    private List<Node> Grow(List<Point> points, Node root)
    {
        List<Node> nodes = [root ?? new Node { At = Point.Zero, Parent = -1 }];
        Grid grid = new (Reach);
        PointGrid near = new (Kill, points);
        bool[] used = new bool[points.Count];
        int live = points.Count;

        grid.Add(nodes[0].At, 0);

        if (root is null)
        {
            double highest = points.Max(point => point.Y);

            // Up the trunk until some point is within reach of its top.
            while (!Draws(points, used, nodes[^1].At))
            {
                if (nodes[^1].At.Y > highest + Reach)
                    throw new Exception("A grown tree's trunk climbed past its crown without coming within reach of it; the crown must stand over the origin, within the tree's reach of the line straight up.");

                AddNode(nodes, grid, nodes.Count - 1, new Vector(0, 1, 0));
            }
        }
        else
        {
            // Along the surface toward the nearest point, until some point is within reach; a stem that can come
            // no nearer -- the points lie across a gap the surface does not bridge -- is refused.
            Point nearest = points.MinBy(point => (point - root.At).Magnitude);
            int most = (int) Math.Ceiling((nearest - root.At).Magnitude / Step) * 4 + 16;

            for (int step = 0; !Draws(points, used, nodes[^1].At); step++)
            {
                int before = nodes.Count;

                if (step < most)
                    AddNode(nodes, grid, nodes.Count - 1, (nearest - nodes[^1].At).Unit);

                if (nodes.Count == before)
                {
                    throw new Exception(
                        $"A grown tree climbing '{AllOver}' could not grow along it from where it stands to within " +
                        "its reach of the points spread over it.");
                }
            }
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
            // earlier rounds used up its own then.  Each new place looks only at the points in the cells round it:
            // asking every point of every new place, a climber over a house spent minutes here.
            for (int node = before; node < nodes.Count; node++)
            {
                foreach (int index in near.Around(nodes[node].At))
                {
                    if (!used[index] && (points[index] - nodes[node].At).Magnitude < Kill)
                    {
                        used[index] = true;
                        live--;
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
        Node from = nodes[parent];
        Point at = from.At + heading * Step;
        Vector normal = null;

        // A climber's step is walked along the surface it climbs, and may find nowhere to land.
        if (_walker is not null)
        {
            if (_walker.StepFrom(from, heading) is not var (landed, facing))
                return;

            (at, normal) = (landed, facing);
        }

        double run = from.Run + (at - from.At).Magnitude;

        if (run > Spread || grid.Nearest(nodes, at, Step * 0.25) >= 0)
            return;

        nodes.Add(new Node { At = at, Parent = parent, Normal = normal, Run = run });
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
            {
                Node node = _nodes[index];

                // A climber's place, eased, is set back down on the surface: halfway between two places on a
                // curve or either side of an edge lies inside it.
                if (_walker is not null && moved[index] != node.At)
                {
                    if (_walker.Settle(moved[index], node.Normal) is var (on, normal))
                        (node.At, node.Normal) = (on, normal);
                }
                else
                    node.At = moved[index];
            }
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

            // A climber's stem lies on the surface, held off it by its own thickness at both ends.
            Add(new TubeSegment
            {
                Start = OnTheWood(parent, node.Radius), StartRadius = node.Radius,
                End = OnTheWood(node, node.Radius), EndRadius = node.Radius
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

        foreach (int place in LeafPlaces())
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
        }
    }

    /// <summary>
    /// This method returns the places leaves stand: on a tree, the end of every twig and the two places before it,
    /// unless a fork comes first; on a climber, every place along its shoots, but not on the old stems that carry
    /// them -- wood thicker than <see cref="LeafyWood"/> twigs.
    /// </summary>
    private IEnumerable<int> LeafPlaces()
    {
        bool climbing = _nodes.Count > 0 && _nodes[0].Normal is not null;

        for (int index = climbing ? 1 : 0; index < _nodes.Count; index++)
        {
            if (climbing)
            {
                if (_nodes[index].Radius <= Twig * LeafyWood * (1 + 1e-9))
                    yield return index;

                continue;
            }

            if (_nodes[index].Children != 0)
                continue;

            int place = index;

            for (int leaf = 0; leaf < 3 && place > 0; leaf++)
            {
                yield return place;

                place = _nodes[place].Parent;

                if (_nodes[place].Children != 1)
                    break;
            }
        }
    }

    /// <summary>
    /// This method returns where on a place's wood something stands that is held off the surface a climber climbs
    /// by the given thickness: on the place itself, for a tree.
    /// </summary>
    private static Point OnTheWood(Node node, double thickness) =>
        node.Normal is null ? node.At : node.At + node.Normal * thickness;

    /// <summary>
    /// This method returns the matrix that stands a leaf at a place, its base there.  Laid along the twig, its +Y runs
    /// the way the wood coming into the place runs -- as drawn, smoothed, rather than the way it first grew -- and its
    /// face, +Z, turns toward the sky, as a leaf's does, give or take an eighth of a turn either way; on a twig
    /// standing upright, which has no sky to one side of it, it turns any way at all.  Hanging, it stands upright in
    /// the tree's own space, turned any way about the upright.
    /// <para>
    /// **On a climber, a leaf stands out of the surface**, as a field's copy stands out of what it covers: its +Y the
    /// way out of the surface there, on top of the stem, and its +Z down the surface, give or take an eighth of a turn,
    /// as a climber's leaves hang on their stalks; on a level face, any way at all.
    /// </para>
    /// </summary>
    /// <param name="node">The place the leaf stands.</param>
    /// <param name="draw">A draw, from nought to one, that sets how far it is turned.</param>
    private Matrix PlacementAt(Node node, double draw)
    {
        Point at = OnTheWood(node, node.Radius);

        if (Hanging)
        {
            double angle = draw * Math.PI * 2;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            return new Matrix([
                cos, 0, sin, at.X,
                0, 1, 0, at.Y,
                -sin, 0, cos, at.Z,
                0, 0, 0, 1
            ]);
        }

        if (node.Normal is not null)
            return StandingOut(node.Normal, at, draw);

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
    /// This method returns the matrix that stands a leaf out of the surface a climber climbs, at a place on its
    /// stem: its +Y the way out of the surface, its +Z down the surface turned by up to an eighth of a turn either
    /// way, or on a level face turned any way at all.
    /// </summary>
    /// <param name="up">The way out of the surface there.</param>
    /// <param name="at">Where the leaf stands.</param>
    /// <param name="draw">A draw, from nought to one, that sets how far it is turned.</param>
    private static Matrix StandingOut(Vector up, Point at, double draw)
    {
        Vector down = new Vector(0, -1, 0) + up * up.Y;
        Vector start;
        double spread;

        if (down.Magnitude > 0.05)
        {
            start = down.Unit;
            spread = (draw - 0.5) * Math.PI / 2;
        }
        else
        {
            start = (Math.Abs(up.X) < 0.9 ? new Vector(1, 0, 0) : new Vector(0, 0, 1)).Cross(up).Unit;
            spread = draw * Math.PI * 2;
        }

        Vector z = (start * Math.Cos(spread) + up.Cross(start) * Math.Sin(spread)).Unit;
        Vector x = up.Cross(z).Unit;

        return new Matrix([
            x.X, up.X, z.X, at.X,
            x.Y, up.Y, z.Y, at.Y,
            x.Z, up.Z, z.Z, at.Z,
            0, 0, 0, 1
        ]);
    }

    /// <summary>
    /// This class walks a climber's growth along the surface it climbs, by rays cast at that surface alone, in the
    /// tree's own space.
    /// <para>
    /// **A step is tried three ways, in turn.**  Straight on along the face, for a corner or a rise in the
    /// surface ahead, which it stops at; then down onto the surface from a step on, however it curves; and,
    /// where the surface has turned away beneath the step -- the top of a wall, the corner of a house -- back
    /// from just past the edge, onto the face beyond it.  A step none of them lands is not taken: growth that
    /// would leave the surface stops there.
    /// </para>
    /// </summary>
    private sealed class Walker
    {
        // How far past an edge, in steps, a step that went over it looks back for the face beyond: the first
        // that finds it is taken, so a thin sheet's other side is found as well as a thick wall's top.
        private static readonly double[] Insets = [0.5, 0.2, 0.05];

        // How many ways the root looks for the surface.
        private const int Looks = 64;

        private readonly GrownTree _tree;
        private readonly Surface _ground;
        private readonly Matrix _inward;
        private readonly Matrix _outward;
        private readonly double _lift;
        private readonly List<Intersection> _hits = [];

        public Walker(GrownTree tree, Surface ground)
        {
            _tree = tree;
            _ground = ground;
            _inward = ground.Transform.Invert() * tree.Transform;
            _outward = _inward.Transpose();
            _lift = tree.Step * 0.05;
        }

        /// <summary>
        /// This method finds where the climber starts: the nearest place on the surface to where it stands, looking
        /// every way as far as its reach.
        /// </summary>
        /// <returns>The place it starts from.</returns>
        public Node Root()
        {
            (Point At, Vector Normal)? best = null;
            double nearest = double.MaxValue;
            double turn = Math.PI * (3 - Math.Sqrt(5));

            for (int look = 0; look < Looks; look++)
            {
                double y = 1 - (look + 0.5) * 2 / Looks;
                double ring = Math.Sqrt(1 - y * y);
                Vector way = new (ring * Math.Cos(turn * look), y, ring * Math.Sin(turn * look));

                if (Probe(Point.Zero, way, _tree.Reach) is { } found &&
                    (found.At - Point.Zero).Magnitude is var distance && distance < nearest)
                {
                    best = found;
                    nearest = distance;
                }
            }

            if (best is not var (at, normal))
            {
                throw new Exception(
                    $"A grown tree climbing '{_tree.AllOver}' must stand within its reach of it, but nothing of it " +
                    "lies that near where it stands.");
            }

            return new Node { At = at, Normal = normal, Parent = -1 };
        }

        /// <summary>
        /// This method walks a step of growth from a place along the surface, the way it heads laid flat on the
        /// surface there.
        /// </summary>
        /// <param name="from">The place grown from.</param>
        /// <param name="heading">The way it is drawn.</param>
        /// <returns>Where the step lands and the way out of the surface there, or <c>null</c> if it lands nowhere.</returns>
        public (Point At, Vector Normal)? StepFrom(Node from, Vector heading)
        {
            Vector normal = from.Normal;
            Vector along = heading - normal * heading.Dot(normal);

            // Drawn straight into the surface or out of it, there is no way along it to go.
            if (along.Magnitude < 1e-6)
                return null;

            along = along.Unit;

            double step = _tree.Step;
            Point lifted = from.At + normal * _lift;

            if (Probe(lifted, along, step) is { } ahead)
                return ahead;

            Point beyond = lifted + along * step;

            if (Probe(beyond, -normal, step * 2) is { } below)
                return below;

            foreach (double inset in Insets)
            {
                if (Probe(beyond - normal * (step * inset + _lift), -along, step * 2) is { } round)
                    return round;
            }

            return null;
        }

        /// <summary>
        /// This method sets a place moved a little off the surface back down on it.
        /// </summary>
        /// <param name="moved">The place, moved.</param>
        /// <param name="normal">The way out of the surface where it was.</param>
        /// <returns>The place on the surface and the way out of it there, or <c>null</c> if it cannot be found.</returns>
        public (Point At, Vector Normal)? Settle(Point moved, Vector normal) =>
            Probe(moved + normal * (_tree.Step * 0.5), -normal, _tree.Step);

        /// <summary>
        /// This method finds the nearest crossing of the surface along a ray, past where it starts and no further
        /// than it is asked to look.
        /// </summary>
        private (Point At, Vector Normal)? Probe(Point from, Vector direction, double longest)
        {
            Ray line = new (from, direction);
            Ray local = _inward.Transform(line);
            (Point At, Vector Normal)? found = null;
            double best = longest;

            _hits.Clear();
            _ground.AddIntersections(local, _hits);

            foreach (Intersection hit in _hits)
            {
                if (hit.Distance <= _lift * 1e-3 || hit.Distance > best || hit.Surface is StrandSurface)
                    continue;

                if (Hair.NormalWithin(_ground, hit, local.At(hit.Distance)) is not { } normal)
                    continue;

                best = hit.Distance;
                found = (line.At(hit.Distance), (_outward * normal).Unit);
            }

            return found;
        }
    }

    /// <summary>
    /// This class holds the points grown toward in cells as wide as the kill distance, so that finding the points a
    /// new place uses up looks only at the cells round it.
    /// </summary>
    private sealed class PointGrid
    {
        private readonly double _size;
        private readonly Dictionary<(int, int, int), List<int>> _cells = [];

        public PointGrid(double size, List<Point> points)
        {
            _size = size;

            for (int index = 0; index < points.Count; index++)
            {
                (int, int, int) cell = CellOf(points[index]);

                if (!_cells.TryGetValue(cell, out List<int> list))
                    _cells[cell] = list = [];

                list.Add(index);
            }
        }

        private (int, int, int) CellOf(Point at) =>
            ((int) Math.Floor(at.X / _size), (int) Math.Floor(at.Y / _size), (int) Math.Floor(at.Z / _size));

        /// <summary>
        /// This method returns the points in the cells round a place, every one within a cell's width of it.
        /// </summary>
        public IEnumerable<int> Around(Point at)
        {
            (int cx, int cy, int cz) = CellOf(at);

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!_cells.TryGetValue((cx + dx, cy + dy, cz + dz), out List<int> list))
                    continue;

                foreach (int index in list)
                    yield return index;
            }
        }
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
