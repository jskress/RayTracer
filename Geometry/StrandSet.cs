using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class holds a great many strands -- hairs, fibers, blades of fur -- compactly: each a chain of
/// cubic Bézier segments with a width that tapers along it, kept as plain numbers in a handful of arrays
/// rather than as a surface apiece.
/// <para>
/// **A surface apiece cannot reach fur.**  A ribbon costs about twenty kilobytes once it is a group of
/// patches with a box and a tree of its own: ten thousand of them took 0.88 GB and fifty thousand 1.7.
/// Fur wants hundreds of thousands of strands, and a head of hair more.  Here a segment is sixteen
/// numbers, in single precision, and the strands share one tree over all their segments.
/// </para>
/// <para>
/// **A strand is hit as a flat ribbon that always faces the ray**, after PBRT: the segment is carried
/// into a frame looking down the ray, split in half until each piece is nearly straight, and the piece
/// is checked against the strand's width there.  It is shaded as though it were round -- see
/// <see cref="HairIntersection"/> -- so a strand thinner than anything the eye could tell from a
/// cylinder costs no more than a sheet.
/// </para>
/// </summary>
public sealed class StrandSet
{
    // A segment's numbers: its four control points (twelve), its width at either end (two) and where it
    // starts and ends along its strand, from nought at the root to one at the tip (two).
    private const int Stride = 16;

    // The most times a segment is halved on the way to a piece straight enough to test.
    private const int DeepestSplit = 10;

    // How many segments a leaf of the tree holds.
    private const int LeafSize = 4;

    // How many segments a part of the tree must hold before its halves are built at once.
    private const int ParallelFrom = 65536;

    private readonly List<float> _building;
    private readonly List<int> _buildingStrands;
    private float[] _segments = [];
    private int[] _strandOf = [];
    private int[] _firstOf = [];
    private int _count;

    // The tree, as arrays: each node's box (six numbers), and either the first of its segments and how
    // many (a leaf), or its two children (a branch, its count nought).
    private float[] _nodeBoxes = [];
    private int[] _nodeFirst = [];
    private int[] _nodeCount = [];
    private int[] _nodeSecond = [];
    private int[] _order = [];

    /// <summary>
    /// This property holds how many strands have been added.
    /// </summary>
    public int StrandCount { get; private set; }

    /// <summary>
    /// This property holds how many segments the strands came to.
    /// </summary>
    public int SegmentCount => _count;

    /// <summary>
    /// This property holds the box around every strand, once <see cref="Seal"/> has been called.
    /// </summary>
    public BoundingBox Bounds { get; private set; } = new ();

    /// <summary>
    /// This creates a set, with room set aside for the given number of segments -- a strand has one
    /// fewer than it has points -- so that a set of millions is not copied over and over as it fills.
    /// </summary>
    /// <param name="segments">How many segments to make room for.</param>
    public StrandSet(int segments = 0)
    {
        _building = new List<float>(segments * Stride);
        _buildingStrands = new List<int>(segments);
    }

    /// <summary>
    /// This method adds a strand running smoothly through the given points, with the given width at
    /// each: a Catmull-Rom curve, so it passes through every point and turns smoothly at each.
    /// </summary>
    /// <param name="points">The points the strand runs through, root first.  At least two.</param>
    /// <param name="widths">The strand's width at each point.</param>
    public void Add(IReadOnlyList<Point> points, IReadOnlyList<double> widths)
    {
        int count = points.Count;

        if (count < 2)
            throw new ArgumentException("A strand needs at least two points to run between.");

        if (widths.Count != count)
            throw new ArgumentException("A strand needs a width at each of its points.");

        for (int index = 0; index < count - 1; index++)
        {
            Point from = points[index];
            Point to = points[index + 1];

            // The tangent at each point is half the step across it, or the one step at either end.
            Vector leaving = index == 0
                ? to - from
                : (to - points[index - 1]) * 0.5;
            Vector arriving = index == count - 2
                ? to - from
                : (points[index + 2] - from) * 0.5;
            Point second = from + leaving / 3;
            Point third = to - arriving / 3;

            Append(from);
            Append(second);
            Append(third);
            Append(to);
            _building.Add((float) widths[index]);
            _building.Add((float) widths[index + 1]);
            _building.Add((float) index / (count - 1));
            _building.Add((float) (index + 1) / (count - 1));
            _buildingStrands.Add(StrandCount);
        }

        StrandCount++;
    }

    /// <summary>
    /// This method adds a control point's three numbers to the segment being built.
    /// </summary>
    private void Append(Point point)
    {
        _building.Add((float) point.X);
        _building.Add((float) point.Y);
        _building.Add((float) point.Z);
    }

    /// <summary>
    /// This method finishes the set: the strands added become what is hit, and the tree over them is
    /// built.  Nothing may be added after.
    /// </summary>
    public void Seal()
    {
        _segments = _building.ToArray();
        _strandOf = _buildingStrands.ToArray();
        _count = _segments.Length / Stride;
        _firstOf = new int[StrandCount];

        // Each strand's segments were added together, so its first is where its number first appears.
        for (int segment = _count - 1; segment >= 0; segment--)
            _firstOf[_strandOf[segment]] = segment;
        _building.Clear();
        _building.TrimExcess();
        _buildingStrands.Clear();
        _buildingStrands.TrimExcess();

        float[] boxes = new float[_count * 6];
        float[] middles = new float[_count * 3];

        for (int segment = 0; segment < _count; segment++)
        {
            SegmentBox(segment, boxes.AsSpan(segment * 6, 6));

            for (int axis = 0; axis < 3; axis++)
                middles[segment * 3 + axis] = (boxes[segment * 6 + axis] + boxes[segment * 6 + 3 + axis]) * 0.5f;
        }

        _order = Enumerable.Range(0, _count).ToArray();

        Dictionary<int, int> nodesFor = [];
        int nodes = _count > 0 ? NodesFor(_count, nodesFor) : 0;

        _nodeBoxes = new float[nodes * 6];
        _nodeFirst = new int[nodes];
        _nodeCount = new int[nodes];
        _nodeSecond = new int[nodes];

        if (_count > 0)
            Build(0, _count, 0, boxes, middles, nodesFor);

        BoundingBox bounds = new ();

        if (_count > 0)
        {
            bounds.Add(new Point(_nodeBoxes[0], _nodeBoxes[1], _nodeBoxes[2]));
            bounds.Add(new Point(_nodeBoxes[3], _nodeBoxes[4], _nodeBoxes[5]));
        }

        Bounds = bounds;
    }

    /// <summary>
    /// This method puts the segments of a stretch of the order either side of its middle one, along an
    /// axis: those whose middles are nearer the start of the axis before it, and the rest after.
    /// <para>
    /// **Only the split matters, not the order either side of it**, since each side is split again in
    /// its turn.  Sorting the stretch to find it, as this first did, cost thirteen seconds of a two
    /// million strand coat; picking it out this way -- Hoare's selection -- costs a fraction of one.
    /// </para>
    /// </summary>
    private void PartitionAround(int from, int to, int middle, float[] middles, int axis)
    {
        int low = from;
        int high = to - 1;

        while (high > low)
        {
            float a = middles[_order[low] * 3 + axis];
            float b = middles[_order[(low + high) / 2] * 3 + axis];
            float c = middles[_order[high] * 3 + axis];
            float pivot = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
            int left = low;
            int right = high;

            while (left <= right)
            {
                while (middles[_order[left] * 3 + axis] < pivot)
                    left++;

                while (middles[_order[right] * 3 + axis] > pivot)
                    right--;

                if (left <= right)
                {
                    (_order[left], _order[right]) = (_order[right], _order[left]);
                    left++;
                    right--;
                }
            }

            if (middle <= right)
                high = right;
            else if (middle >= left)
                low = left;
            else
                break;
        }
    }

    /// <summary>
    /// This method returns the point a strand starts from, once the set is sealed.
    /// </summary>
    /// <param name="strand">Which strand, counting from nought in the order they were added.</param>
    /// <returns>Its first point.</returns>
    public Point RootOf(int strand)
    {
        int at = _firstOf[strand] * Stride;

        return new Point(_segments[at], _segments[at + 1], _segments[at + 2]);
    }

    /// <summary>
    /// This method returns the points each strand runs through, root first, as they were added, once the
    /// set is sealed.  It is for looking at the strands rather than hitting them.
    /// </summary>
    /// <returns>The points each strand runs through, in the order the strands were added.</returns>
    internal List<List<Point>> Paths()
    {
        List<List<Point>> paths = [];

        for (int segment = 0; segment < _count; segment++)
        {
            int at = segment * Stride;

            if (paths.Count == _strandOf[segment])
                paths.Add([new Point(_segments[at], _segments[at + 1], _segments[at + 2])]);

            paths[^1].Add(new Point(_segments[at + 9], _segments[at + 10], _segments[at + 11]));
        }

        return paths;
    }

    /// <summary>
    /// This method works out the box around one segment: its control points, which a Bézier curve never
    /// strays outside, widened by half its widest.
    /// </summary>
    private void SegmentBox(int segment, Span<float> box)
    {
        int at = segment * Stride;
        float half = Math.Max(_segments[at + 12], _segments[at + 13]) * 0.5f;

        for (int axis = 0; axis < 3; axis++)
        {
            float low = float.MaxValue;
            float high = float.MinValue;

            for (int point = 0; point < 4; point++)
            {
                float value = _segments[at + point * 3 + axis];

                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }

            box[axis] = low - half;
            box[3 + axis] = high + half;
        }
    }

    /// <summary>
    /// This method returns how many nodes the tree over the given number of segments has.
    /// <para>
    /// **A tree split at the middle every time has a shape that depends on nothing but its size**, so
    /// where each part of it goes can be worked out before any of it is built -- which is what lets the
    /// two halves of a big one be built at once, each into its own stretch of the arrays.  There are only
    /// two sizes of part at each depth, so remembering them makes this quick.
    /// </para>
    /// </summary>
    private static int NodesFor(int segments, Dictionary<int, int> known)
    {
        if (segments <= LeafSize)
            return 1;

        if (!known.TryGetValue(segments, out int nodes))
        {
            nodes = 1 + NodesFor(segments / 2, known) + NodesFor(segments - segments / 2, known);
            known[segments] = nodes;
        }

        return nodes;
    }

    /// <summary>
    /// This method builds the part of the tree over a stretch of the order, as the given node and those
    /// following it: a leaf if the stretch is short, or else a node whose two halves are split at the
    /// middle along the box's longest side.
    /// </summary>
    private void Build(int from, int to, int node, float[] boxes, float[] middles, Dictionary<int, int> nodesFor)
    {
        int at = node * 6;

        for (int axis = 0; axis < 3; axis++)
        {
            _nodeBoxes[at + axis] = float.MaxValue;
            _nodeBoxes[at + 3 + axis] = float.MinValue;
        }

        for (int index = from; index < to; index++)
        {
            int segment = _order[index];

            for (int axis = 0; axis < 3; axis++)
            {
                _nodeBoxes[at + axis] = Math.Min(_nodeBoxes[at + axis], boxes[segment * 6 + axis]);
                _nodeBoxes[at + 3 + axis] = Math.Max(_nodeBoxes[at + 3 + axis], boxes[segment * 6 + 3 + axis]);
            }
        }

        if (to - from <= LeafSize)
        {
            _nodeFirst[node] = from;
            _nodeCount[node] = to - from;
            _nodeSecond[node] = -1;

            return;
        }

        int longest = 0;

        for (int axis = 1; axis < 3; axis++)
        {
            if (_nodeBoxes[at + 3 + axis] - _nodeBoxes[at + axis] >
                _nodeBoxes[at + 3 + longest] - _nodeBoxes[at + longest])
                longest = axis;
        }

        int middle = from + (to - from) / 2;
        int left = node + 1;
        int right = left + NodesFor(middle - from, nodesFor);

        PartitionAround(from, to, middle, middles, longest);

        _nodeFirst[node] = left;
        _nodeCount[node] = 0;
        _nodeSecond[node] = right;

        // The halves touch nothing in common, so a big one is built two at a time.
        if (to - from >= ParallelFrom)
        {
            Parallel.Invoke(
                () => Build(from, middle, left, boxes, middles, nodesFor),
                () => Build(middle, to, right, boxes, middles, nodesFor));
        }
        else
        {
            Build(from, middle, left, boxes, middles, nodesFor);
            Build(middle, to, right, boxes, middles, nodesFor);
        }
    }

    /// <summary>
    /// This method finds every place the given ray crosses a strand, as a hit on the given surface.
    /// </summary>
    /// <param name="ray">The ray, in the strands' own space.</param>
    /// <param name="owner">The surface the hits are on.</param>
    /// <param name="intersections">The list to add the hits to.</param>
    /// <param name="nearest">How far back along the ray to look: negative infinity for the whole line,
    /// which a surface owes an ordinary query, or nought for a shadow's.</param>
    /// <param name="farthest">How far along the ray to look.</param>
    public void Intersect(
        Ray ray, StrandSurface owner, List<Intersection> intersections, double nearest = double.NegativeInfinity,
        double farthest = double.PositiveInfinity)
    {
        if (_count == 0)
            return;

        Vector direction = ray.Direction;
        double length = direction.Magnitude;

        if (length == 0)
            return;

        RayFrame frame = new (ray, length);
        Span<int> stack = stackalloc int[64];
        int top = 0;
        int before = intersections.Count;

        stack[top++] = 0;

        while (top > 0)
        {
            int node = stack[--top];

            if (!frame.Crosses(_nodeBoxes, node * 6, nearest, farthest))
                continue;

            if (_nodeCount[node] > 0)
            {
                for (int index = _nodeFirst[node]; index < _nodeFirst[node] + _nodeCount[node]; index++)
                    IntersectSegment(_order[index], frame, owner, intersections, nearest, farthest);
            }
            else
            {
                stack[top++] = _nodeFirst[node];
                stack[top++] = _nodeSecond[node];
            }
        }

        RemoveDoubledJoints(intersections, before);
    }

    /// <summary>
    /// This method drops a crossing found twice: by both of the pieces that meet at a joint.
    /// <para>
    /// A ray passes a joint on one side or the other of the line through it square to the strand, and
    /// each piece claims only its own side -- but a ray lying exactly on that line is claimed by both
    /// when the arithmetic rounds each test a hair the generous way.  It is as rare as a ray along a
    /// line can be, and it would count the one crossing twice wherever a crossing is counted: a shadow
    /// through two strands rather than one, or a ray taken to be inside where it is out.  The same
    /// strand crossed twice at the same distance is one crossing, so one of the two is let go.
    /// </para>
    /// </summary>
    private static void RemoveDoubledJoints(List<Intersection> intersections, int from)
    {
        for (int index = intersections.Count - 1; index > from; index--)
        {
            HairIntersection later = (HairIntersection) intersections[index];

            for (int other = from; other < index; other++)
            {
                HairIntersection earlier = (HairIntersection) intersections[other];

                if (earlier.Strand == later.Strand &&
                    Math.Abs(earlier.Distance - later.Distance) <= 1e-9 * (1 + Math.Abs(later.Distance)))
                {
                    intersections.RemoveAt(index);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// This method finds where the ray crosses one segment.
    /// </summary>
    private void IntersectSegment(
        int segment, in RayFrame frame, StrandSurface owner, List<Intersection> intersections, double nearest,
        double farthest)
    {
        int at = segment * Stride;
        Span<double> controls = stackalloc double[12];

        // The control points, carried into the frame that looks down the ray: x and y across it, z along.
        for (int point = 0; point < 4; point++)
        {
            double x = _segments[at + point * 3] - frame.OriginX;
            double y = _segments[at + point * 3 + 1] - frame.OriginY;
            double z = _segments[at + point * 3 + 2] - frame.OriginZ;

            controls[point * 3] = x * frame.AcrossX + y * frame.AcrossY + z * frame.AcrossZ;
            controls[point * 3 + 1] = x * frame.UpX + y * frame.UpY + z * frame.UpZ;
            controls[point * 3 + 2] = x * frame.AlongX + y * frame.AlongY + z * frame.AlongZ;
        }

        double startWidth = _segments[at + 12];
        double endWidth = _segments[at + 13];

        // How many times to halve the segment before treating a piece as straight: enough that a piece
        // strays from its chord by no more than a twentieth of the strand's width (PBRT's measure).
        double bend = 0;

        for (int index = 0; index < 2; index++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                bend = Math.Max(bend, Math.Abs(
                    controls[index * 3 + axis] - 2 * controls[(index + 1) * 3 + axis] +
                    controls[(index + 2) * 3 + axis]));
            }
        }

        double tolerance = Math.Max(startWidth, endWidth) * 0.05;
        int depth = tolerance > 0 && bend > 0
            ? Math.Clamp((int) Math.Round(Math.Log2(1.41421356237 * 6 * bend / (8 * tolerance)) / 2), 0, DeepestSplit)
            : 0;

        Span<double> whole = stackalloc double[12];

        controls.CopyTo(whole);
        Split(controls, whole, 0, 1, depth, segment, frame, owner, intersections, nearest, farthest);
    }

    /// <summary>
    /// This method halves a piece of a segment until it is straight enough, then tests the ray against it.
    /// </summary>
    /// <param name="piece">The piece's control points, in the ray's frame.</param>
    /// <param name="whole">The whole segment's control points, in the ray's frame.</param>
    private void Split(
        ReadOnlySpan<double> piece, ReadOnlySpan<double> whole, double from, double to, int depth,
        int segment, in RayFrame frame, StrandSurface owner, List<Intersection> intersections, double nearest,
        double farthest)
    {
        int at = segment * Stride;
        double startWidth = _segments[at + 12];
        double endWidth = _segments[at + 13];
        double half = Math.Max(
            startWidth + (endWidth - startWidth) * from, startWidth + (endWidth - startWidth) * to) * 0.5;

        // The piece's box, across the ray, must take in the ray itself.
        double lowX = double.MaxValue, highX = double.MinValue;
        double lowY = double.MaxValue, highY = double.MinValue;
        double lowZ = double.MaxValue, highZ = double.MinValue;

        for (int point = 0; point < 4; point++)
        {
            lowX = Math.Min(lowX, piece[point * 3]);
            highX = Math.Max(highX, piece[point * 3]);
            lowY = Math.Min(lowY, piece[point * 3 + 1]);
            highY = Math.Max(highY, piece[point * 3 + 1]);
            lowZ = Math.Min(lowZ, piece[point * 3 + 2]);
            highZ = Math.Max(highZ, piece[point * 3 + 2]);
        }

        if (lowX - half > 0 || highX + half < 0 || lowY - half > 0 || highY + half < 0 ||
            highZ + half < nearest * frame.Length || lowZ - half > farthest * frame.Length)
            return;

        if (depth > 0)
        {
            Span<double> left = stackalloc double[12];
            Span<double> right = stackalloc double[12];
            double middle = (from + to) * 0.5;

            Halve(piece, left, right);
            Split(left, whole, from, middle, depth - 1, segment, frame, owner, intersections, nearest, farthest);
            Split(right, whole, middle, to, depth - 1, segment, frame, owner, intersections, nearest, farthest);

            return;
        }

        // The piece is straight enough.  The ray must pass between the lines through either end square
        // to the curve there, or it is the next piece's to find.  A ray exactly on the line between two
        // pieces belongs to the later one: the earlier's end is left open, so a joint is crossed once
        // rather than by both the pieces that meet there.
        double startEdge = (piece[4] - piece[1]) * -piece[1] + piece[0] * (piece[0] - piece[3]);
        double endEdge = (piece[7] - piece[10]) * -piece[10] + piece[9] * (piece[9] - piece[6]);

        if (startEdge < 0 || endEdge <= 0)
            return;

        double chordX = piece[9] - piece[0];
        double chordY = piece[10] - piece[1];
        double chordSquared = chordX * chordX + chordY * chordY;

        if (chordSquared == 0)
            return;

        double fraction = (-piece[0] * chordX - piece[1] * chordY) / chordSquared;
        double u = Math.Clamp(from + (to - from) * fraction, from, to);
        double width = startWidth + (endWidth - startWidth) * u;

        Evaluate(whole, u, out double curveX, out double curveY, out double curveZ);

        double distanceSquared = curveX * curveX + curveY * curveY;

        if (distanceSquared > width * width * 0.25)
            return;

        double distance = curveZ / frame.Length;

        if (distance < nearest || distance > farthest)
            return;

        intersections.Add(MakeHit(segment, u, width, distance, frame, owner));
    }

    /// <summary>
    /// This method splits a cubic Bézier in two at its middle, by de Casteljau's construction.
    /// </summary>
    private static void Halve(ReadOnlySpan<double> piece, Span<double> left, Span<double> right)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            double p0 = piece[axis], p1 = piece[3 + axis], p2 = piece[6 + axis], p3 = piece[9 + axis];
            double p01 = (p0 + p1) * 0.5, p12 = (p1 + p2) * 0.5, p23 = (p2 + p3) * 0.5;
            double p012 = (p01 + p12) * 0.5, p123 = (p12 + p23) * 0.5;
            double middle = (p012 + p123) * 0.5;

            left[axis] = p0;
            left[3 + axis] = p01;
            left[6 + axis] = p012;
            left[9 + axis] = middle;
            right[axis] = middle;
            right[3 + axis] = p123;
            right[6 + axis] = p23;
            right[9 + axis] = p3;
        }
    }

    /// <summary>
    /// This method finds the point a fraction of the way along a cubic Bézier.
    /// </summary>
    private static void Evaluate(ReadOnlySpan<double> controls, double u, out double x, out double y, out double z)
    {
        double v = 1 - u;
        double a = v * v * v, b = 3 * v * v * u, c = 3 * v * u * u, d = u * u * u;

        x = a * controls[0] + b * controls[3] + c * controls[6] + d * controls[9];
        y = a * controls[1] + b * controls[4] + c * controls[7] + d * controls[10];
        z = a * controls[2] + b * controls[5] + c * controls[8] + d * controls[11];
    }

    /// <summary>
    /// This method makes the hit for a crossing of a segment, working out the normal it is shaded with.
    /// <para>
    /// **A strand is shaded as though it were round.**  It is hit as a flat ribbon facing the ray, and
    /// where across it the ray passed says where on a cylinder of the same width it would have struck: at
    /// the middle the cylinder faces the eye, and toward either edge it turns away, until at the edge it
    /// is seen side on.  So the normal is the ray's way back, square to the strand, turned toward the edge
    /// the ray is nearer by as much as a cylinder would be.
    /// </para>
    /// </summary>
    private Intersection MakeHit(int segment, double u, double width, double distance, in RayFrame frame, StrandSurface owner)
    {
        int at = segment * Stride;
        Span<double> controls = stackalloc double[12];

        for (int index = 0; index < 12; index++)
            controls[index] = _segments[at + index];

        Evaluate(controls, u, out double centerX, out double centerY, out double centerZ);

        // The strand's direction there.
        double v = 1 - u;
        double tangentX = 0, tangentY = 0, tangentZ = 0;

        for (int index = 0; index < 3; index++)
        {
            double weight = index switch { 0 => v * v, 1 => 2 * v * u, _ => u * u };

            tangentX += weight * (controls[(index + 1) * 3] - controls[index * 3]);
            tangentY += weight * (controls[(index + 1) * 3 + 1] - controls[index * 3 + 1]);
            tangentZ += weight * (controls[(index + 1) * 3 + 2] - controls[index * 3 + 2]);
        }

        Vector tangent = new Vector(tangentX, tangentY, tangentZ);

        tangent = tangent.Magnitude > 0 ? tangent.Unit : new Vector(frame.AlongX, frame.AlongY, frame.AlongZ);

        // The way back along the ray, square to the strand: the face of the ribbon.
        Vector back = new (-frame.AlongX, -frame.AlongY, -frame.AlongZ);
        Vector face = back - tangent * back.Dot(tangent);

        face = face.Magnitude > 1e-12 ? face.Unit : back;

        // Where across the strand the ray passed, and so how far round the cylinder.
        Point hit = new (
            frame.OriginX + frame.AlongX * distance * frame.Length,
            frame.OriginY + frame.AlongY * distance * frame.Length,
            frame.OriginZ + frame.AlongZ * distance * frame.Length);
        Vector offset = hit - new Point(centerX, centerY, centerZ);
        Vector side = offset - tangent * offset.Dot(tangent) - face * offset.Dot(face);
        double across = side.Magnitude;
        Vector normal = face;
        double signedAcross = 0;

        if (across > 1e-12 && width > 0)
        {
            double sine = Math.Min(1, across / (width * 0.5));

            normal = face * Math.Sqrt(1 - sine * sine) + side * (sine / across);
            signedAcross = sine * Math.Sign(side.Dot(tangent.Cross(face)));
        }

        double along = _segments[at + 14] + (_segments[at + 15] - _segments[at + 14]) * u;

        return new HairIntersection(
            owner.WearerOf(_strandOf[segment]), distance, normal, along, signedAcross, _strandOf[segment],
            new Point(centerX, centerY, centerZ), width * 0.5, tangent,
            frame.AlongX * tangent.X + frame.AlongY * tangent.Y + frame.AlongZ * tangent.Z);
    }

    /// <summary>
    /// This struct holds a ray carried into a frame of its own: its origin, its length, and three axes,
    /// one along it and two across.
    /// </summary>
    private readonly struct RayFrame
    {
        public readonly double OriginX, OriginY, OriginZ;
        public readonly double AlongX, AlongY, AlongZ;
        public readonly double AcrossX, AcrossY, AcrossZ;
        public readonly double UpX, UpY, UpZ;
        public readonly double Length;
        private readonly double _inverseX, _inverseY, _inverseZ;
        private readonly double _directionX, _directionY, _directionZ;

        public RayFrame(Ray ray, double length)
        {
            Length = length;
            OriginX = ray.Origin.X;
            OriginY = ray.Origin.Y;
            OriginZ = ray.Origin.Z;
            _directionX = ray.Direction.X;
            _directionY = ray.Direction.Y;
            _directionZ = ray.Direction.Z;
            AlongX = _directionX / length;
            AlongY = _directionY / length;
            AlongZ = _directionZ / length;
            _inverseX = 1 / _directionX;
            _inverseY = 1 / _directionY;
            _inverseZ = 1 / _directionZ;

            // Any two axes square to the ray and to each other will do.
            if (Math.Abs(AlongX) > Math.Abs(AlongY))
            {
                double scale = 1 / Math.Sqrt(AlongX * AlongX + AlongZ * AlongZ);

                AcrossX = -AlongZ * scale;
                AcrossY = 0;
                AcrossZ = AlongX * scale;
            }
            else
            {
                double scale = 1 / Math.Sqrt(AlongY * AlongY + AlongZ * AlongZ);

                AcrossX = 0;
                AcrossY = AlongZ * scale;
                AcrossZ = -AlongY * scale;
            }

            UpX = AlongY * AcrossZ - AlongZ * AcrossY;
            UpY = AlongZ * AcrossX - AlongX * AcrossZ;
            UpZ = AlongX * AcrossY - AlongY * AcrossX;
        }

        /// <summary>
        /// This method reports whether the ray crosses a node's box within the given stretch of itself.
        /// </summary>
        public bool Crosses(float[] boxes, int at, double nearest, double farthest)
        {
            double low = nearest;
            double high = farthest;

            if (!Slab(boxes[at], boxes[at + 3], OriginX, _directionX, _inverseX, ref low, ref high) ||
                !Slab(boxes[at + 1], boxes[at + 4], OriginY, _directionY, _inverseY, ref low, ref high) ||
                !Slab(boxes[at + 2], boxes[at + 5], OriginZ, _directionZ, _inverseZ, ref low, ref high))
                return false;

            return low <= high;
        }

        private static bool Slab(
            double minimum, double maximum, double origin, double direction, double inverse, ref double low,
            ref double high)
        {
            if (direction == 0)
                return origin >= minimum && origin <= maximum;

            double near = (minimum - origin) * inverse;
            double far = (maximum - origin) * inverse;

            if (near > far)
                (near, far) = (far, near);

            low = Math.Max(low, near);
            high = Math.Min(high, far);

            return low <= high;
        }
    }
}
