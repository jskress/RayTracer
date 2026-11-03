using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a chain along a spline: oval links, each through the next and turned a quarter from it, as
/// an anchor chain or a swing's chain runs.
/// <para>
/// **A link is sized by its length and the thickness of its bar**: its `link`, end to end outside; its `wire`, the
/// bar's thickness; and its `width` across outside, three and a half bars unless said, as a short-link chain is.
/// Each is a closed round tube bent into an oval, two straights joined by two half rounds, and every link is the one
/// shape, shared.  Links hooked through each other stand an inner length apart -- the link less two bars -- so as
/// many stand along the spline as fit whole, spaced to fill it.
/// </para>
/// <para>
/// **Each link lies along the chord** between the two places it reaches on the spline, and turns a quarter about it
/// from the last, in the spline's own frames, which turn no more than the spline makes them -- so a chain hanging
/// straight down, which has no up to turn a link by, alternates as one along the ground does.
/// </para>
/// </summary>
public class Chain : Group
{
    /// <summary>
    /// This property holds the spline the chain runs along.
    /// </summary>
    public Spline Spline { get; set; }

    /// <summary>
    /// This property holds how long a link is, end to end outside.
    /// </summary>
    public double Link { get; set; } = 0.05;

    /// <summary>
    /// This property holds how thick a link's bar is, or <c>null</c> for a short link's, two elevenths of its
    /// length.
    /// </summary>
    public double? Wire { get; set; }

    /// <summary>
    /// This property holds how wide a link is outside, or <c>null</c> for three and a half bars.
    /// </summary>
    public double? Width { get; set; }

    // How much of a circle a cubic Bezier quarter is out by its control points: 4/3 (sqrt 2 - 1).
    private const double Quarter = 0.5522847498307936;

    private bool _linked;

    /// <summary>
    /// This method links the chain along its spline before the group it is prepares itself.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        double wire = Wire ?? Link / 5.5;
        double width = Width ?? wire * 3.5;

        if (Spline is null)
            throw new Exception("A chain needs a spline to run along.");

        if (Link <= 0 || wire <= 0)
            throw new Exception("A chain's link and wire must be more than nought.");

        if (width <= 2 * wire)
            throw new Exception("A chain's link must be wider than two of its bars, or there is no room inside it.");

        if (Link < width)
            throw new Exception("A chain's link may be no shorter than it is wide.");

        if (!_linked)
        {
            Lay(wire, width);

            _linked = true;
        }

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method stands every link along the spline.
    /// </summary>
    private void Lay(double wire, double width)
    {
        SplineMeasure measure = Spline.Measure;
        double pitch = Link - 2 * wire;
        int links = Math.Max(1, (int) Math.Round(measure.Length / pitch));
        double spacing = measure.Length / links;
        Tube shape = LinkShape(wire, width);
        List<Point> middles = new (links);
        List<Vector> ways = new (links);

        for (int index = 0; index < links; index++)
        {
            Point from = measure.PointAt(spacing * index);
            Point to = measure.PointAt(spacing * (index + 1));

            middles.Add(from + (to - from) * 0.5);
            ways.Add(to - from);
        }

        List<SweepFrame> frames = RotationMinimizingFrame.Compute(middles, ways);

        for (int index = 0; index < links; index++)
        {
            SweepFrame frame = frames[index];
            Vector along = frame.Tangent;
            Vector turned = index % 2 == 0 ? frame.Normal : frame.Binormal;
            Vector across = (turned - along * turned.Dot(along)).Unit;
            Vector up = along.Cross(across);
            Point at = frame.Position;

            Add(new Instance
            {
                Prototype = shape,
                Transform = new Matrix([
                    along.X, across.X, up.X, at.X,
                    along.Y, across.Y, up.Y, at.Y,
                    along.Z, across.Z, up.Z, at.Z,
                    0, 0, 0, 1
                ])
            });
        }
    }

    /// <summary>
    /// This method makes the one link every place in the chain shows: a round bar bent into an oval in the X/Y plane,
    /// its length along X and its width along Y, about the origin.
    /// </summary>
    private Tube LinkShape(double wire, double width)
    {
        double bar = wire / 2;
        double round = (width - wire) / 2;
        double straight = (Link - width) / 2;
        Tube tube = new () { Start = new TubeControlPoint { Center = new Point(straight, -round, 0), Radius = bar } };

        void LineTo(double x, double y) => tube.Segments.Add(new TubeSegmentSpec
        {
            End = new TubeControlPoint { Center = new Point(x, y, 0), Radius = bar }
        });

        // A quarter round about a middle, from one angle to the next a quarter turn less, as the oval is drawn
        // clockwise throughout.
        void QuarterAbout(double middle, double from)
        {
            double to = from - Math.PI / 2;
            Point start = new (middle + round * Math.Cos(from), round * Math.Sin(from), 0);
            Point end = new (middle + round * Math.Cos(to), round * Math.Sin(to), 0);
            Vector leaving = new (Math.Sin(from), -Math.Cos(from), 0);
            Vector arriving = new (Math.Sin(to), -Math.Cos(to), 0);

            tube.Segments.Add(new TubeSegmentSpec
            {
                Control1 = new TubeControlPoint { Center = start + leaving * (Quarter * round), Radius = bar },
                Control2 = new TubeControlPoint { Center = end - arriving * (Quarter * round), Radius = bar },
                End = new TubeControlPoint { Center = end, Radius = bar }
            });
        }

        if (straight > 0)
            LineTo(-straight, -round);

        QuarterAbout(-straight, -Math.PI / 2);
        QuarterAbout(-straight, -Math.PI);

        if (straight > 0)
            LineTo(straight, round);

        QuarterAbout(straight, Math.PI / 2);
        QuarterAbout(straight, 0);

        return tube;
    }
}
