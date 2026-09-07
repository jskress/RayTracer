using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a ribbon: a flat strand of a given width running through a series of
/// points -- a blade of grass, a strap, a leaf of a palm, a length of tape.
/// <para>
/// **It is the flat answer to <see cref="Tube"/>'s round one.**  A tube is a rod: it looks the same
/// whichever side it is seen from, which is right for a stem or a wire and wrong for anything with a
/// face.  A blade of grass turned edge-on very nearly disappears, and that flicker as blades turn is
/// most of what a field of them looks like; a rod cannot do it, because a rod has no edge to turn.
/// </para>
/// <para>
/// **It is a strip of <see cref="BilinearPatch"/>es, one between each pair of points.**  Those solve
/// exactly, in one square root apiece, where lofting a profile along a spline the way
/// <see cref="Sweep"/> does costs a tessellation of two dozen steps a segment -- affordable for one
/// pipe and not for forty thousand blades of grass.  Each patch is handed the normals of the points
/// it spans, so a ribbon of any number of them shades as one surface with no crease at the joins.
/// </para>
/// <para>
/// **Which way it faces is worked out rather than asked for.**  The strip is laid along a
/// rotation-minimizing frame, so it does not spin on straight runs nor flip where the path turns the
/// other way -- both of which a frame built from the curve's own bending does.  <see cref="Twist"/>
/// turns it deliberately, spread evenly from one end to the other.
/// </para>
/// </summary>
public class Ribbon : Group
{
    /// <summary>
    /// This property holds the points the ribbon runs through, each with the width it has there.
    /// </summary>
    public List<RibbonControlPoint> Points { get; } = [];

    /// <summary>
    /// This property holds how far the ribbon turns about its own length, in radians, from the first
    /// point to the last.  It is nought for a ribbon that keeps one face.
    /// </summary>
    public double Twist { get; set; }

    /// <summary>
    /// This method is called once prior to rendering to build the strip of patches the ribbon is
    /// made of.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Points.Count < 2)
            throw new Exception("A ribbon needs at least two points to run between.");

        List<Point> positions = Points.Select(point => point.Position).ToList();
        List<SweepFrame> frames = RotationMinimizingFrame.Compute(positions, TangentsAlong(positions));
        Vector[] across = new Vector[frames.Count];
        Vector[] facing = new Vector[frames.Count];

        for (int index = 0; index < frames.Count; index++)
        {
            // The twist is shared out evenly along the ribbon, so each frame is turned about its own
            // direction of travel by its share of it.  Turning a frame about its tangent is just a
            // turn within the plane its other two axes span, so no matrix is wanted.
            double angle = Twist * index / (frames.Count - 1.0);
            double cosine = Math.Cos(angle);
            double sine = Math.Sin(angle);
            SweepFrame frame = frames[index];

            across[index] = frame.Normal * cosine + frame.Binormal * sine;
            facing[index] = frame.Binormal * cosine - frame.Normal * sine;
        }

        Surfaces.Clear();

        for (int index = 0; index < Points.Count - 1; index++)
        {
            Point near = positions[index];
            Point far = positions[index + 1];
            Vector nearOut = across[index] * (Points[index].Width / 2);
            Vector farOut = across[index + 1] * (Points[index + 1].Width / 2);

            // The corners go round the quadrilateral rather than across it, which is what a patch
            // wants; given the other way they would describe a bow tie.
            Add(new BilinearPatch
            {
                Corners = [near - nearOut, near + nearOut, far + farOut, far - farOut],
                CornerNormals = [facing[index], facing[index], facing[index + 1], facing[index + 1]],
                Material = Material
            });
        }

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method works out which way the ribbon is travelling at each of its points.  A point with
    /// neighbours either side takes the direction from one to the other, so the ribbon turns through
    /// it smoothly rather than kinking at it; the two ends take the one segment they have.
    /// </summary>
    /// <param name="positions">The points the ribbon runs through.</param>
    /// <returns>The direction of travel at each.</returns>
    private static List<Vector> TangentsAlong(List<Point> positions)
    {
        List<Vector> tangents = new (positions.Count);

        for (int index = 0; index < positions.Count; index++)
        {
            int before = Math.Max(index - 1, 0);
            int after = Math.Min(index + 1, positions.Count - 1);

            tangents.Add(positions[after] - positions[before]);
        }

        return tangents;
    }
}
