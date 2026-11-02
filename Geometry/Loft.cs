using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class holds one cross-section of a loft: an outline drawn in its own X/Y plane, and where it stands.
/// </summary>
public sealed class LoftSection
{
    /// <summary>
    /// This property holds the outline, a single run.
    /// </summary>
    public GeneralPath Outline { get; set; }

    /// <summary>
    /// This property holds where the outline stands: carried from its own plane by this, and then, for a loft along a
    /// spline, onto the spline.
    /// </summary>
    public Matrix Transform { get; set; } = Matrix.Identity;

    /// <summary>
    /// This property holds how far along the spline the section stands, as a share of its length, for a loft along one.
    /// </summary>
    public double? At { get; set; }
}

/// <summary>
/// This class represents a loft: a skin stretched through several cross-sections, each an outline placed where it
/// belongs -- the way a boat's hull is drawn from its station lines, or a fuselage from its bulkheads.
/// <para>
/// **It is exact, and nothing in it is a triangle.**  Each section is its outline's cubic segments, a line or a
/// quadratic being a cubic too, and between two sections each segment sweeps out a cubic patch, the skin running
/// through every section smoothly (or in straight lines, for a loft said to be <see cref="Straight"/>).  A patch is
/// held in the box of its control points, which holds every point of it, and the boxes of the quarters it splits
/// into by de Casteljau are worked out once, before the first ray: a ray missing a box misses all of the patch inside
/// it.  On the smallest the crossing is solved for exactly, by Newton's method on the patch itself -- as a
/// <see cref="Parametric"/> surface is found -- so its normals are exact and its outline as smooth as its shape.
/// </para>
/// <para>
/// **The sections need not match.**  Sections drawn with as many segments each correspond segment for segment; ones
/// that differ are cut, exactly, so that places as far round each correspond (see <see cref="Equalize"/>).  Each
/// section's run is followed from where it was drawn from, so where each is begun says which places on one go to which
/// on the next.  A corner drawn into a section runs the length of the loft as a crease.
/// </para>
/// <para>
/// **Closed sections make a solid**, capped flat at both ends by the first and last sections, that may be combined,
/// filled or looked through like any other; a loft said to be <see cref="Open"/>, or of sections that do not close,
/// is a sheet.
/// </para>
/// </summary>
public class Loft : Surface
{
    // How small a piece of a patch must get, as a share of the whole loft's size, before its crossing is solved for
    // rather than its box split again: as for a parametric surface.
    private const double SolveWithinFraction = 0.02;

    // How many times a patch may be halved, all told, before its pieces are solved whatever their size.
    private const int DeepestSplit = 14;

    private const int SolveSteps = 30;
    private const double SolveTo = 1e-12;
    private const double SmallestDeterminant = 1e-14;
    private const double BoxPadding = 1e-6;

    // How finely a spline is followed to find where a section stands along it, and which way it faces.
    private const int SplineSamples = 1024;

    /// <summary>
    /// This property holds the sections, in the order the skin runs through them.
    /// </summary>
    public List<LoftSection> Sections { get; } = [];

    /// <summary>
    /// This property holds the spline the sections stand along, each square to it at its own share of its length,
    /// or <c>null</c> for sections placed by their transforms alone.
    /// </summary>
    public Spline Spline { get; set; }

    /// <summary>
    /// This property notes whether the skin runs in straight lines from section to section -- a ruled loft, a cut
    /// gem's facets or a blade's bevel -- rather than smoothly through them.
    /// </summary>
    public bool Straight { get; set; }

    /// <summary>
    /// This property notes whether the loft's ends are left open, making it a sheet: a sleeve, a shell, a lampshade.
    /// </summary>
    public bool Open { get; set; }

    /// <summary>
    /// This property reports whether the loft is a sheet, with no inside: open, or of sections that do not close.  A
    /// loft not yet given its sections cannot be built, and is what it would be of closed ones unless it is open.
    /// </summary>
    public override bool IsASheet => Sections.Count < 2 ? Open : Built().Sheet;

    private Shape _shape;
    private readonly Lock _building = new ();

    /// <summary>
    /// This class holds one patch: its control points, a 4 by 4 net, the first index along the sections' outlines
    /// and the second across from section to section; and the tree of boxes it is narrowed by.
    /// </summary>
    private sealed class Patch
    {
        public Point[,] Net;
        public Piece Root;
    }

    /// <summary>
    /// This class holds one piece of a patch: the box its control points make, the parameters it covers, and the two
    /// halves it splits into, or none for a piece small enough to solve.
    /// </summary>
    private sealed class Piece
    {
        public BoundingBox Box;
        public double ULow, UHigh, VLow, VHigh;
        public Piece First, Second;
    }

    /// <summary>
    /// This class holds one of the flat ends of a closed loft: its plane, carried from its section's own, the way
    /// out, and the outline that bounds it.
    /// </summary>
    private sealed class Cap
    {
        public Point Origin;
        public Vector AlongX, AlongY, Out;
        public GeneralPath Outline;
        public double XX, XY, YY, Determinant;
    }

    /// <summary>
    /// This class holds one branch of the tree the patches are gathered into, so that a ray opens only the patches
    /// whose boxes lie along it: the box round them all, and either the patches themselves or two smaller branches.
    /// </summary>
    private sealed class Branch
    {
        public BoundingBox Box;
        public int[] Patches;
        public Branch First, Second;
    }

    /// <summary>
    /// This record holds everything the loft is, worked out once from its sections.
    /// </summary>
    private sealed record Shape(Patch[] Patches, Branch Tree, Cap[] Caps, BoundingBox Box, bool Sheet, double Outward,
        double SolveWithin, double Together);

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to perform any expensive
    /// precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        Built();
    }

    /// <summary>
    /// This method returns the box round the loft: round every patch's control points, which hold the whole of it.
    /// </summary>
    /// <returns>The box.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        BoundingBox box = new ();

        box.Add(Built().Box);

        return box;
    }

    /// <summary>
    /// This method returns what the loft is, working it out the first time it is asked.
    /// </summary>
    private Shape Built()
    {
        if (_shape is not null)
            return _shape;

        lock (_building)
            return _shape ??= Build();
    }

    /// <summary>
    /// This method works out the loft from its sections: each outline as cubic segments, as many in each, carried to
    /// where it stands; the patches between each pair; the caps; and which way is out.
    /// </summary>
    private Shape Build()
    {
        if (Sections.Count < 2)
            throw new Exception("A loft needs at least two sections to stretch its skin between.");

        if (Spline is null && Sections.Any(section => section.At is not null))
            throw new Exception("A loft's section stands \"at\" a share of a spline, and this loft has no spline.");

        if (Spline is not null && Sections.Any(section => section.At is null))
            throw new Exception("A loft along a spline needs each section to say \"at\" how far along it it stands.");

        List<List<TwoDPoint[]>> outlines = Sections.Select(section => CubicsOf(section.Outline)).ToList();
        bool closed = Closes(outlines[0]);

        if (outlines.Any(outline => Closes(outline) != closed))
            throw new Exception("A loft's sections must all close, or none of them: a skin cannot be both a solid and a sheet.");

        Equalize(outlines);

        Matrix[] placing = Placings();
        List<Point[][]> sections = outlines
            .Select((outline, index) => outline
                .Select(segment => segment.Select(point => placing[index] * new Point(point.X, point.Y, 0)).ToArray())
                .ToArray())
            .ToList();
        List<Patch> patches = [];
        BoundingBox box = new ();
        double[] apart = Apart(sections);

        for (int span = 0; span < sections.Count - 1; span++)
        {
            for (int segment = 0; segment < sections[span].Length; segment++)
            {
                Point[,] net = new Point[4, 4];

                for (int along = 0; along < 4; along++)
                {
                    Point[] across = AcrossSpan(sections, apart, span, segment, along);

                    for (int step = 0; step < 4; step++)
                    {
                        net[along, step] = across[step];
                        box.Add(across[step]);
                    }
                }

                patches.Add(new Patch { Net = net });
            }
        }

        Point low = box.Minimum, high = box.Maximum;
        double size = (high - low).Magnitude;
        double solveWithin = Math.Max(size * SolveWithinFraction, 1e-12);

        foreach (Patch patch in patches)
            patch.Root = Split(patch.Net, 0, 1, 0, 1, DeepestSplit, solveWithin);

        bool sheet = !closed || Open;
        Cap[] caps = sheet
            ? []
            : [CapOf(0, placing[0], sections, outlines), CapOf(Sections.Count - 1, placing[^1], sections, outlines)];
        Branch tree = Gather(patches, Enumerable.Range(0, patches.Count).ToArray());
        Shape shape = new (patches.ToArray(), tree, caps, box, sheet, 1, solveWithin, Math.Max(size * 1e-9, 1e-12));

        return sheet ? shape : shape with { Outward = OutwardOf(shape, sections, outlines) };
    }

    /// <summary>
    /// This method returns an outline as its cubic segments, a line or a quadratic raised to a cubic exactly, a run
    /// that closes turned counterclockwise -- from where it starts, which stays where it is -- and segments of no length
    /// left out.
    /// </summary>
    private static List<TwoDPoint[]> CubicsOf(GeneralPath outline)
    {
        if (outline is null)
            throw new Exception("A loft's section needs an outline, a path.");

        if (outline.FindAllSubPaths().Count > 1)
            throw new Exception("A loft's section must be a single outline; a second run, or a hole, cannot be lofted.");

        List<TwoDPoint[]> cubics = outline.Segments
            .Select(segment => segment.Points)
            .Select(points => points.Length switch
            {
                2 => [points[0], Toward(points[0], points[1], 1.0 / 3), Toward(points[0], points[1], 2.0 / 3), points[1]],
                3 => [points[0], Toward(points[0], points[1], 2.0 / 3), Toward(points[2], points[1], 2.0 / 3), points[2]],
                _ => points
            })
            .Where(points => LengthOf(points) > 1e-12)
            .ToList();

        if (cubics.Count == 0)
            throw new Exception("A loft's section must have some length to it.");

        if (Closes(cubics) && SignedArea(cubics) < 0)
        {
            cubics.Reverse();
            cubics = cubics.Select(points => points.Reverse().ToArray()).ToList();
        }

        return cubics;

        static TwoDPoint Toward(TwoDPoint from, TwoDPoint to, double share) =>
            new (from.X + (to.X - from.X) * share, from.Y + (to.Y - from.Y) * share);
    }

    // How long a segment's control polygon is: never less than the segment itself.
    private static double LengthOf(TwoDPoint[] points)
    {
        double length = 0;

        for (int index = 1; index < points.Length; index++)
            length += Math.Sqrt(Squared(points[index].X - points[index - 1].X) + Squared(points[index].Y - points[index - 1].Y));

        return length;
    }

    private static double Squared(double value) => value * value;

    // Whether an outline comes back to where it started.
    private static bool Closes(List<TwoDPoint[]> cubics)
    {
        TwoDPoint first = cubics[0][0], last = cubics[^1][3];

        return first.X.Near(last.X) && first.Y.Near(last.Y);
    }

    // The area a closed run of cubic segments bounds, signed: more than nought counterclockwise.  Only its sign is
    // wanted, so the run is followed closely enough by straight lines and the shoelace taken round them.
    private static double SignedArea(List<TwoDPoint[]> cubics)
    {
        double area = 0;

        foreach (TwoDPoint[] p in cubics)
        {
            TwoDPoint last = p[0];

            for (int step = 1; step <= 16; step++)
            {
                double t = step / 16.0, s = 1 - t;
                TwoDPoint next = new (
                    s * s * s * p[0].X + 3 * t * s * s * p[1].X + 3 * t * t * s * p[2].X + t * t * t * p[3].X,
                    s * s * s * p[0].Y + 3 * t * s * s * p[1].Y + 3 * t * t * s * p[2].Y + t * t * t * p[3].Y);

                area += last.X * next.Y - next.X * last.Y;
                last = next;
            }
        }

        return area / 2;
    }

    /// <summary>
    /// This method makes the outlines' segments correspond, one for one.
    /// <para>
    /// Outlines drawn with as many segments each are taken to correspond segment for segment, which is what drawing
    /// them alike means: the chine drawn as a corner in every station of a hull runs straight down it.  Outlines that
    /// differ are matched by how far round each one is instead: each is cut, exactly, wherever any of the others has a
    /// join, at the same share of its own length, so a square flowing into a circle meets it with its corners at the
    /// circle's eighths rather than wherever its longest sides happened to be split.
    /// </para>
    /// </summary>
    private static void Equalize(List<List<TwoDPoint[]>> outlines)
    {
        if (outlines.All(outline => outline.Count == outlines[0].Count))
            return;

        List<double[]> lengths = outlines.Select(outline => outline.Select(segment => ArcLength(segment)).ToArray()).ToList();
        List<double[]> joins = lengths.Select(Joins).ToList();
        List<double> every = joins.SelectMany(shares => shares).OrderBy(share => share).ToList();
        List<double> cuts = [];

        foreach (double share in every)
        {
            if (cuts.Count == 0 || share - cuts[^1] > 1e-6)
                cuts.Add(share);
        }

        for (int index = 0; index < outlines.Count; index++)
            outlines[index] = CutAt(outlines[index], lengths[index], joins[index], cuts);

        // Where each join of an outline falls, as a share of its length, its ends left out.
        static double[] Joins(double[] lengths)
        {
            double total = lengths.Sum(), sum = 0;
            double[] shares = new double[lengths.Length - 1];

            for (int index = 0; index < shares.Length; index++)
            {
                sum += lengths[index];
                shares[index] = sum / total;
            }

            return shares;
        }
    }

    // An outline cut at every one of the shares of its length it is not already joined at.
    private static List<TwoDPoint[]> CutAt(List<TwoDPoint[]> outline, double[] lengths, double[] joins, List<double> cuts)
    {
        double total = lengths.Sum();
        List<TwoDPoint[]> cut = [];
        double start = 0;

        for (int index = 0; index < outline.Count; index++)
        {
            double end = index < joins.Length ? joins[index] : 1;
            TwoDPoint[] rest = outline[index];
            double restFrom = start;

            foreach (double share in cuts.Where(share => share > start + 1e-6 && share < end - 1e-6))
            {
                // How far into what is left of the segment the cut falls, by length.
                double into = (share - restFrom) * total;
                double t = ParameterAt(rest, into);
                (TwoDPoint[] before, TwoDPoint[] after) = SplitAt(rest, t);

                cut.Add(before);
                rest = after;
                restFrom = share;
            }

            cut.Add(rest);
            start = end;
        }

        return cut;
    }

    // The length of a cubic, by 16-point Gauss-Legendre over its speed.
    private static double ArcLength(TwoDPoint[] p) => ArcLength(p, 1);

    private static double ArcLength(TwoDPoint[] p, double upTo)
    {
        double length = 0;

        for (int index = 0; index < GaussNodes.Length; index++)
        {
            double t = upTo * (GaussNodes[index] + 1) / 2, s = 1 - t;
            double dx = 3 * (s * s * (p[1].X - p[0].X) + 2 * s * t * (p[2].X - p[1].X) + t * t * (p[3].X - p[2].X));
            double dy = 3 * (s * s * (p[1].Y - p[0].Y) + 2 * s * t * (p[2].Y - p[1].Y) + t * t * (p[3].Y - p[2].Y));

            length += GaussWeights[index] * Math.Sqrt(dx * dx + dy * dy);
        }

        return length * upTo / 2;
    }

    // The parameter at which a cubic has come the given length, found by halving.
    private static double ParameterAt(TwoDPoint[] p, double length)
    {
        double low = 0, high = 1;

        for (int step = 0; step < 60; step++)
        {
            double middle = (low + high) / 2;

            if (ArcLength(p, middle) < length)
                low = middle;
            else
                high = middle;
        }

        return (low + high) / 2;
    }

    // A cubic split at a parameter into two that make it up exactly, by de Casteljau.
    private static (TwoDPoint[], TwoDPoint[]) SplitAt(TwoDPoint[] p, double t)
    {
        TwoDPoint Lerp(TwoDPoint a, TwoDPoint b) => new (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        TwoDPoint ab = Lerp(p[0], p[1]), bc = Lerp(p[1], p[2]), cd = Lerp(p[2], p[3]);
        TwoDPoint abc = Lerp(ab, bc), bcd = Lerp(bc, cd), at = Lerp(abc, bcd);

        return ([p[0], ab, abc, at], [at, bcd, cd, p[3]]);
    }

    private static readonly double[] GaussNodes =
    [
        -0.9894009349916499, -0.9445750230732326, -0.8656312023878318, -0.7554044083550030,
        -0.6178762444026438, -0.4580167776572274, -0.2816035507792589, -0.0950125098376374,
        0.0950125098376374, 0.2816035507792589, 0.4580167776572274, 0.6178762444026438,
        0.7554044083550030, 0.8656312023878318, 0.9445750230732326, 0.9894009349916499
    ];

    private static readonly double[] GaussWeights =
    [
        0.0271524594117541, 0.0622535239386479, 0.0951585116824928, 0.1246289712555339,
        0.1495959888165767, 0.1691565193950025, 0.1826034150449236, 0.1894506104550685,
        0.1894506104550685, 0.1826034150449236, 0.1691565193950025, 0.1495959888165767,
        0.1246289712555339, 0.0951585116824928, 0.0622535239386479, 0.0271524594117541
    ];

    /// <summary>
    /// This method works out what carries each section from its own plane to where it stands: its own transforms,
    /// and then, along a spline, the spline's frame at its share of the length -- the outline's X and Y laid across the
    /// spline and its Z along it, the frames turning no more than the spline makes them, as a sweep's do.
    /// </summary>
    private Matrix[] Placings()
    {
        if (Spline is null)
            return Sections.Select(section => section.Transform).ToArray();

        SplineMeasure measure = Spline.Measure;
        List<Point> positions = [];
        List<Vector> tangents = [];

        for (int index = 0; index < SplineSamples; index++)
        {
            double distance = measure.Length * index / (SplineSamples - 1);

            positions.Add(measure.PointAt(distance));
            tangents.Add(measure.HeadingAt(distance).Unit);
        }

        List<SweepFrame> frames = RotationMinimizingFrame.Compute(positions, tangents);

        return Sections
            .Select(section =>
            {
                int index = (int) Math.Round(Math.Clamp(section.At!.Value, 0, 1) * (SplineSamples - 1));
                SweepFrame frame = frames[index];
                Matrix onto = new (
                [
                    frame.Normal.X, frame.Binormal.X, frame.Tangent.X, frame.Position.X,
                    frame.Normal.Y, frame.Binormal.Y, frame.Tangent.Y, frame.Position.Y,
                    frame.Normal.Z, frame.Binormal.Z, frame.Tangent.Z, frame.Position.Z,
                    0, 0, 0, 1
                ]);

                return onto * section.Transform;
            })
            .ToArray();
    }

    // How far apart each section stands from the next, middle to middle: the spacing the skin is drawn through them by.
    private static double[] Apart(List<Point[][]> sections)
    {
        Point[] middles = sections
            .Select(section =>
            {
                Vector sum = new (0, 0, 0);

                foreach (Point[] segment in section)
                    sum += new Vector(segment[0].X, segment[0].Y, segment[0].Z);

                return new Point(sum.X / section.Length, sum.Y / section.Length, sum.Z / section.Length);
            })
            .ToArray();

        return Enumerable.Range(0, sections.Count - 1)
            .Select(index => Math.Max((middles[index + 1] - middles[index]).Magnitude, 1e-9))
            .ToArray();
    }

    /// <summary>
    /// This method returns how one control point of one segment runs across one span, from a section to the next, as
    /// the four control points of a cubic: straight, a third and two thirds of the way; or smoothly through every
    /// section, its slope at each the way from the section before to the one after (Catmull-Rom), scaled to how far
    /// apart they stand, and at either end of the loft the way to the one beside it.
    /// </summary>
    private Point[] AcrossSpan(List<Point[][]> sections, double[] apart, int span, int segment, int along)
    {
        Point Here(int index) => sections[index][segment][along];

        Point from = Here(span), to = Here(span + 1);

        if (Straight || sections.Count == 2)
            return [from, from + (to - from) / 3, from + (to - from) * (2.0 / 3), to];

        Vector leaving = span == 0
            ? to - from
            : (to - Here(span - 1)) * (apart[span] / (apart[span - 1] + apart[span]));
        Vector arriving = span + 2 >= sections.Count
            ? to - from
            : (Here(span + 2) - from) * (apart[span] / (apart[span] + apart[span + 1]));

        return [from, from + leaving / 3, to - arriving / 3, to];
    }

    /// <summary>
    /// This method gathers patches into a tree of boxes: halved across the way their middles spread furthest, until a
    /// branch holds two.
    /// </summary>
    private static Branch Gather(List<Patch> patches, int[] which)
    {
        BoundingBox box = new ();

        foreach (int index in which)
            box.Add(patches[index].Root.Box);

        if (which.Length <= 2)
            return new Branch { Box = box, Patches = which };

        Point low = box.Minimum, high = box.Maximum;
        Vector spread = high - low;
        Func<Point, double> along = spread.X >= spread.Y && spread.X >= spread.Z ? point => point.X
            : spread.Y >= spread.Z ? point => point.Y
            : point => point.Z;
        int[] sorted = which
            .OrderBy(index => along(Middle(patches[index].Root.Box)))
            .ToArray();
        int half = sorted.Length / 2;

        return new Branch
        {
            Box = box, First = Gather(patches, sorted[..half]), Second = Gather(patches, sorted[half..])
        };

        static Point Middle(BoundingBox box) => new (
            (box.Minimum.X + box.Maximum.X) / 2, (box.Minimum.Y + box.Maximum.Y) / 2, (box.Minimum.Z + box.Maximum.Z) / 2);
    }

    /// <summary>
    /// This method splits a patch's net into the tree of pieces it is narrowed by: each the box of its control points,
    /// halved across whichever way its net runs longer, until a piece is small enough to solve.
    /// </summary>
    private static Piece Split(Point[,] net, double uLow, double uHigh, double vLow, double vHigh, int depth,
        double solveWithin)
    {
        BoundingBox box = new ();

        foreach (Point point in net)
            box.Add(point);

        box.Expand(BoxPadding);

        Piece piece = new () { Box = box, ULow = uLow, UHigh = uHigh, VLow = vLow, VHigh = vHigh };
        Point low = box.Minimum, high = box.Maximum;

        if (depth == 0 || (high.X - low.X <= solveWithin && high.Y - low.Y <= solveWithin && high.Z - low.Z <= solveWithin))
            return piece;

        double alongU = 0, alongV = 0;

        for (int index = 0; index < 4; index++)
        {
            alongU += (net[3, index] - net[0, index]).Magnitude;
            alongV += (net[index, 3] - net[index, 0]).Magnitude;
        }

        (Point[,] first, Point[,] second) = Halves(net, alongU >= alongV);

        if (alongU >= alongV)
        {
            double middle = (uLow + uHigh) / 2;

            piece.First = Split(first, uLow, middle, vLow, vHigh, depth - 1, solveWithin);
            piece.Second = Split(second, middle, uHigh, vLow, vHigh, depth - 1, solveWithin);
        }
        else
        {
            double middle = (vLow + vHigh) / 2;

            piece.First = Split(first, uLow, uHigh, vLow, middle, depth - 1, solveWithin);
            piece.Second = Split(second, uLow, uHigh, middle, vHigh, depth - 1, solveWithin);
        }

        return piece;
    }

    // A net split at its middle, across the first index or the second, into two that make it up exactly.
    private static (Point[,], Point[,]) Halves(Point[,] net, bool acrossFirst)
    {
        Point[,] first = new Point[4, 4], second = new Point[4, 4];

        for (int line = 0; line < 4; line++)
        {
            Point a = acrossFirst ? net[0, line] : net[line, 0];
            Point b = acrossFirst ? net[1, line] : net[line, 1];
            Point c = acrossFirst ? net[2, line] : net[line, 2];
            Point d = acrossFirst ? net[3, line] : net[line, 3];
            Point ab = Mid(a, b), bc = Mid(b, c), cd = Mid(c, d);
            Point abc = Mid(ab, bc), bcd = Mid(bc, cd), middle = Mid(abc, bcd);
            Point[] low = [a, ab, abc, middle], high = [middle, bcd, cd, d];

            for (int index = 0; index < 4; index++)
            {
                if (acrossFirst)
                {
                    first[index, line] = low[index];
                    second[index, line] = high[index];
                }
                else
                {
                    first[line, index] = low[index];
                    second[line, index] = high[index];
                }
            }
        }

        return (first, second);

        static Point Mid(Point a, Point b) => new ((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.Z + b.Z) / 2);
    }

    /// <summary>
    /// This method makes one of a closed loft's flat ends, in its section's plane.
    /// </summary>
    private Cap CapOf(int index, Matrix placing, List<Point[][]> sections, List<List<TwoDPoint[]>> outlines)
    {
        Point origin = placing * Point.Zero;
        Vector alongX = placing * new Point(1, 0, 0) - origin;
        Vector alongY = placing * new Point(0, 1, 0) - origin;
        Vector square = alongX.Cross(alongY).Unit;

        // Out is away from the rest of the loft: from the section beside this one toward this one.
        int beside = index == 0 ? 1 : index - 1;
        Vector away = MiddleOf(sections[index]) - MiddleOf(sections[beside]);
        double xx = alongX.Dot(alongX), xy = alongX.Dot(alongY), yy = alongY.Dot(alongY);
        GeneralPath outline = new ();

        outline.MoveTo(outlines[index][0][0]);

        foreach (TwoDPoint[] segment in outlines[index])
            outline.CubicTo(segment[1], segment[2], segment[3]);

        return new Cap
        {
            Origin = origin, AlongX = alongX, AlongY = alongY, Out = square.Dot(away) >= 0 ? square : -square,
            Outline = outline, XX = xx, XY = xy, YY = yy, Determinant = xx * yy - xy * xy
        };

        static Point MiddleOf(Point[][] section)
        {
            double x = 0, y = 0, z = 0;

            foreach (Point[] segment in section)
            {
                x += segment[0].X;
                y += segment[0].Y;
                z += segment[0].Z;
            }

            return new Point(x / section.Length, y / section.Length, z / section.Length);
        }
    }

    /// <summary>
    /// This method works out which way the skin's own normal, the way along the outline crossed with the way across the
    /// sections, faces: out or in.  At the middle of each segment of the first section, out is away from the inside of
    /// the outline in its own plane; the most of them say which.
    /// </summary>
    private double OutwardOf(Shape shape, List<Point[][]> sections, List<List<TwoDPoint[]>> outlines)
    {
        int votes = 0;

        for (int segment = 0; segment < outlines[0].Count; segment++)
        {
            Patch patch = shape.Patches[segment];
            (Point at, Vector alongU, Vector alongV) = Evaluate(patch.Net, 0.5, 0);
            Vector normal = alongU.Cross(alongV);

            // The outline runs counterclockwise, so out of it is the way along it turned clockwise, in its own plane.
            TwoDPoint[] p = outlines[0][segment];
            double tx = 0.75 * (p[3].X + p[2].X - p[1].X - p[0].X), ty = 0.75 * (p[3].Y + p[2].Y - p[1].Y - p[0].Y);
            Cap cap = shape.Caps[0];
            Vector outward = cap.AlongX * ty - cap.AlongY * tx;

            votes += normal.Dot(outward) >= 0 ? 1 : -1;
        }

        return votes >= 0 ? 1 : -1;
    }

    /// <summary>
    /// This method works out the point of a patch at a pair of its parameters, and its slopes along each.
    /// </summary>
    private static (Point At, Vector AlongU, Vector AlongV) Evaluate(Point[,] net, double u, double v)
    {
        Span<double> bu = stackalloc double[4], bv = stackalloc double[4], du = stackalloc double[4], dv = stackalloc double[4];

        Basis(u, bu, du);
        Basis(v, bv, dv);

        double x = 0, y = 0, z = 0, ux = 0, uy = 0, uz = 0, vx = 0, vy = 0, vz = 0;

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Point p = net[i, j];
                double w = bu[i] * bv[j], wu = du[i] * bv[j], wv = bu[i] * dv[j];

                x += p.X * w; y += p.Y * w; z += p.Z * w;
                ux += p.X * wu; uy += p.Y * wu; uz += p.Z * wu;
                vx += p.X * wv; vy += p.Y * wv; vz += p.Z * wv;
            }
        }

        return (new Point(x, y, z), new Vector(ux, uy, uz), new Vector(vx, vy, vz));

        static void Basis(double t, Span<double> b, Span<double> d)
        {
            double s = 1 - t;

            b[0] = s * s * s; b[1] = 3 * t * s * s; b[2] = 3 * t * t * s; b[3] = t * t * t;
            d[0] = -3 * s * s; d[1] = 3 * s * s - 6 * t * s; d[2] = 6 * t * s - 3 * t * t; d[3] = 3 * t * t;
        }
    }

    /// <summary>
    /// This method adds every crossing of the given ray with the loft, behind its start as well as ahead: each patch
    /// whose box it passes narrowed to the pieces it passes, each of those solved; and the flat ends.
    /// </summary>
    /// <param name="ray">The ray to intersect with.</param>
    /// <param name="intersections">The list to add intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        Shape shape = Built();
        double length = ray.Direction.Magnitude;

        if (length == 0)
            return;

        // The narrowing and solving work with a direction of unit length, and the answer is scaled back at the end, so
        // the accuracy means the same whatever the ray's own length.
        Ray unit = new (ray.Origin, ray.Direction / length, ray.TimeIndex);
        int before = intersections.Count;

        Walk(unit, shape, shape.Tree, length, intersections);

        for (int index = 0; index < shape.Caps.Length; index++)
            CrossCap(unit, shape.Caps[index], index, length, intersections);

        WeedOutRepeats(intersections, before, shape.Together / length);
    }

    // Goes down the tree of patches through the branches whose boxes the ray passes, and into each patch it reaches.
    private void Walk(Ray ray, Shape shape, Branch branch, double length, List<Intersection> intersections)
    {
        if (!branch.Box.IsHitBy(ray))
            return;

        if (branch.Patches is { } patches)
        {
            foreach (int index in patches)
                Narrow(ray, shape.Patches[index], index, shape.Patches[index].Root, length, intersections);

            return;
        }

        Walk(ray, shape, branch.First, length, intersections);
        Walk(ray, shape, branch.Second, length, intersections);
    }

    // Goes down a patch's tree through the pieces whose boxes the ray passes, solving the smallest.
    private void Narrow(Ray ray, Patch patch, int index, Piece piece, double length, List<Intersection> intersections)
    {
        if (!piece.Box.IsHitBy(ray))
            return;

        if (piece.First is null)
        {
            Solve(ray, patch, index, piece, length, intersections);

            return;
        }

        Narrow(ray, patch, index, piece.First, length, intersections);
        Narrow(ray, patch, index, piece.Second, length, intersections);
    }

    /// <summary>
    /// This method solves for where the ray crosses one piece of a patch, by Newton's method on the patch itself from
    /// the piece's middle: kept within the piece, since every crossing lies in exactly one piece and each has only to
    /// find its own, and run on until the gap is a millionth of a millionth.  A crossing on the join between two
    /// pieces is found by both, and one is weeded out afterwards.
    /// </summary>
    private void Solve(Ray ray, Patch patch, int index, Piece piece, double length, List<Intersection> intersections)
    {
        double u = (piece.ULow + piece.UHigh) / 2, v = (piece.VLow + piece.VHigh) / 2;
        (Point where, Vector alongU, Vector alongV) = Evaluate(patch.Net, u, v);
        double t = (where - ray.Origin).Dot(ray.Direction);

        for (int step = 0; step < SolveSteps; step++)
        {
            Vector gap = ray.Origin + ray.Direction * t - where;

            if (gap.Magnitude <= SolveTo)
            {
                intersections.Add(new LoftIntersection(this, t / length, index, u, v));

                return;
            }

            double determinant = ray.Direction.Dot(alongU.Cross(alongV));

            if (Math.Abs(determinant) < SmallestDeterminant)
                return;

            (double wasT, double wasU, double wasV) = (t, u, v);

            t -= gap.Dot(alongU.Cross(alongV)) / determinant;
            u += ray.Direction.Dot(gap.Cross(alongV)) / determinant;
            v += ray.Direction.Dot(alongU.Cross(gap)) / determinant;
            u = Math.Clamp(u, piece.ULow, piece.UHigh);
            v = Math.Clamp(v, piece.VLow, piece.VHigh);
            (where, alongU, alongV) = Evaluate(patch.Net, u, v);

            if (Math.Abs(t - wasT) + Math.Abs(u - wasU) + Math.Abs(v - wasV) < SolveTo)
                break;
        }

        if ((ray.Origin + ray.Direction * t - where).Magnitude <= Built().Together)
            intersections.Add(new LoftIntersection(this, t / length, index, u, v));
    }

    // Where the ray crosses one of the flat ends, if it crosses inside its outline.
    private void CrossCap(Ray ray, Cap cap, int index, double length, List<Intersection> intersections)
    {
        double facing = cap.Out.Dot(ray.Direction);

        if (Math.Abs(facing) < 1e-14)
            return;

        double t = cap.Out.Dot(cap.Origin - ray.Origin) / facing;
        Vector off = ray.Origin + ray.Direction * t - cap.Origin;
        double a = cap.AlongX.Dot(off), b = cap.AlongY.Dot(off);
        double x = (cap.YY * a - cap.XY * b) / cap.Determinant, y = (cap.XX * b - cap.XY * a) / cap.Determinant;

        if (cap.Outline.Contains(new TwoDPoint(x, y)))
            intersections.Add(new LoftIntersection(this, t / length, -1 - index, 0, 0));
    }

    // Takes out a crossing found twice -- on the join between two pieces, or two patches, or a patch and an end.
    private static void WeedOutRepeats(List<Intersection> intersections, int from, double together)
    {
        if (intersections.Count - from < 2)
            return;

        intersections.Sort(from, intersections.Count - from, Comparer<Intersection>.Create(
            (left, right) => left.Distance.CompareTo(right.Distance)));

        for (int index = intersections.Count - 1; index > from; index--)
        {
            if (Math.Abs(intersections[index].Distance - intersections[index - 1].Distance) <= together)
                intersections.RemoveAt(index);
        }
    }

    /// <summary>
    /// This method returns the loft's normal where a ray crossed it: an end's way out, or the skin's own, the way along
    /// the outline crossed with the way across the sections, turned to face out.  Where the skin folds to a point -- a
    /// section drawn to nothing -- the two lie together and give no normal, and one is borrowed from just inside the
    /// patch.
    /// </summary>
    /// <param name="point">The point, in surface space.</param>
    /// <param name="intersection">The crossing.</param>
    /// <returns>The normal.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        Shape shape = Built();

        if (intersection is not LoftIntersection crossing)
            return new Vector(0, 1, 0);

        if (crossing.Patch < 0)
            return shape.Caps[-1 - crossing.Patch].Out;

        Point[,] net = shape.Patches[crossing.Patch].Net;
        (_, Vector alongU, Vector alongV) = Evaluate(net, crossing.U, crossing.V);
        Vector normal = alongU.Cross(alongV);

        if (normal.Magnitude < 1e-12)
        {
            (_, alongU, alongV) = Evaluate(net, 0.5 + (crossing.U - 0.5) * 0.999, 0.5 + (crossing.V - 0.5) * 0.999);
            normal = alongU.Cross(alongV);
        }

        return normal.Magnitude < 1e-15 ? new Vector(0, 1, 0) : normal.Unit * shape.Outward;
    }
}
