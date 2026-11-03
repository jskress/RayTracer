using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a laid rope along a spline: its strands twisted about the spline, each a round tube, as
/// three-strand rope is laid up -- manila, hemp, a mooring line -- and wire rope from strands of wire.
/// <para>
/// **The strands touch**: n strands round the spline, each as thick as fits n alike about a middle, r sin(pi/n) / (1 +
/// sin(pi/n)) for a rope r thick, so three strands are each a little under half the rope.  Each turns once about
/// the spline in a lay's length, the right way unless the lay is negative -- right-hand, as three-strand rope is
/// laid -- in the spline's own frames, which turn no more than the spline makes them (see
/// <see cref="RotationMinimizingFrame"/>), so a rope along a bend does not twist round it.
/// </para>
/// <para>
/// **Each strand is straight tube pieces**, sixteen a lay, end to end with round ends, so no joint shows: a strand
/// laid so is off the true helix by about a fiftieth of its own thickness.  The pieces stand in the rope as a group
/// does, boxed and sorted, so a long mooring line of thousands of them is looked into only where a ray goes.
/// </para>
/// </summary>
public class Rope : Group
{
    /// <summary>
    /// This property holds the spline the rope runs along.
    /// </summary>
    public Spline Spline { get; set; }

    /// <summary>
    /// This property holds how thick the rope is: the radius of the round its strands fill.
    /// </summary>
    public double Radius { get; set; } = 0.01;

    /// <summary>
    /// This property holds how many strands it is laid from.
    /// </summary>
    public int Strands { get; set; } = 3;

    /// <summary>
    /// This property holds how far along the rope a strand goes in one turn about it, or <c>null</c> for seven times
    /// its radius, as three-strand rope is commonly laid; negative, the strands turn the other way.
    /// </summary>
    public double? Lay { get; set; }

    // How many straight pieces a strand takes for each turn it makes.
    private const int PiecesPerLay = 16;

    private bool _laid;

    /// <summary>
    /// This method lays the rope's strands along its spline before the group it is prepares itself.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Spline is null)
            throw new Exception("A rope needs a spline to run along.");

        if (Radius <= 0)
            throw new Exception("A rope's radius must be more than nought.");

        if (Strands < 2)
            throw new Exception("A rope is laid from at least two strands; a single one is a tube.");

        if (Lay is 0)
            throw new Exception("A rope's lay must not be nought: its strands would never get anywhere.");

        if (!_laid)
        {
            LayUp();

            _laid = true;
        }

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method lays the strands: each a helix about the spline in its frames, drawn as straight tube pieces.
    /// </summary>
    private void LayUp()
    {
        SplineMeasure measure = Spline.Measure;
        double lay = Lay ?? 7 * Radius;
        double sine = Math.Sin(Math.PI / Strands);
        double strand = Radius * sine / (1 + sine);
        double offset = Radius - strand;
        int steps = Math.Max(8, (int) Math.Ceiling(measure.Length / (Math.Abs(lay) / PiecesPerLay)));
        List<Point> positions = new (steps + 1);
        List<Vector> tangents = new (steps + 1);

        for (int step = 0; step <= steps; step++)
        {
            double distance = measure.Length * step / steps;

            positions.Add(measure.PointAt(distance));
            tangents.Add(measure.HeadingAt(distance));
        }

        List<SweepFrame> frames = RotationMinimizingFrame.Compute(positions, tangents);

        for (int index = 0; index < Strands; index++)
        {
            Point previous = null;

            for (int step = 0; step <= steps; step++)
            {
                SweepFrame frame = frames[step];
                double angle = 2 * Math.PI * (measure.Length * step / steps / lay + (double) index / Strands);
                Point at = frame.Position + (frame.Normal * Math.Cos(angle) + frame.Binormal * Math.Sin(angle)) * offset;

                if (previous is not null)
                    Add(new TubeSegment { Start = previous, StartRadius = strand, End = at, EndRadius = strand });

                previous = at;
            }
        }
    }
}
