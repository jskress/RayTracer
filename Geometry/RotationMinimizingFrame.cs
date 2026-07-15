using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class computes a rotation-minimizing frame (RMF) along a sequence of points and
/// tangents -- the orientation a profile should be lofted with at each point of a swept
/// path so it doesn't twist.  A naive Frenet-Serret frame (built from the curve's own
/// curvature/torsion) flips or degenerates wherever a path runs straight or has an
/// inflection point; a rotation-minimizing frame instead propagates the previous frame
/// forward with the least rotation necessary to track the new tangent, via the "double
/// reflection method" (Wang, Jüttler, Zheng &amp; Liu, "Computation of Rotation Minimizing
/// Frames," ACM Transactions on Graphics, 2008), which is both simple and numerically
/// robust -- it stays exactly orthonormal over long propagations without any renormalizing
/// or drift correction needed.
/// </summary>
internal static class RotationMinimizingFrame
{
    /// <summary>
    /// This method computes one frame per given position/tangent pair.
    /// </summary>
    /// <param name="positions">The positions along the path.</param>
    /// <param name="tangents">The (not necessarily unit) direction of travel at each
    /// position.</param>
    /// <returns>One frame per position, in order.</returns>
    public static List<SweepFrame> Compute(IReadOnlyList<Point> positions, IReadOnlyList<Vector> tangents)
    {
        List<SweepFrame> frames = new (positions.Count);
        Vector tangent = tangents[0].Unit;
        Vector normal = InitialNormal(tangent);

        frames.Add(MakeFrame(positions[0], tangent, normal));

        for (int index = 1; index < positions.Count; index++)
        {
            Point previousPosition = positions[index - 1];
            Vector previousTangent = tangent;

            tangent = tangents[index].Unit;
            normal = PropagateNormal(previousPosition, previousTangent, normal, positions[index], tangent);

            frames.Add(MakeFrame(positions[index], tangent, normal));
        }

        return frames;
    }

    /// <summary>
    /// This method propagates a normal from one frame to the next via the double
    /// reflection method: reflecting the previous frame's tangent and normal across the
    /// perpendicular bisector plane between the two positions, then reflecting the result a
    /// second time to align the (now-reflected) tangent with the new one -- two reflections
    /// compose into the rotation with the least possible twist.
    /// </summary>
    private static Vector PropagateNormal(
        Point previousPosition, Vector previousTangent, Vector previousNormal,
        Point position, Vector tangent)
    {
        Vector v1 = position - previousPosition;
        double c1 = v1.Dot(v1);
        Vector reflectedNormal = previousNormal - v1 * (2 / c1 * v1.Dot(previousNormal));
        Vector reflectedTangent = previousTangent - v1 * (2 / c1 * v1.Dot(previousTangent));
        Vector v2 = tangent - reflectedTangent;
        double c2 = v2.Dot(v2);

        // A vanishing c2 means the tangent didn't change direction at all across this step
        // (reflectedTangent already equals tangent), so the second reflection would divide
        // by zero -- but it would also be a no-op, so the first reflection's normal is
        // already the answer.
        return c2.Near(0) ? reflectedNormal.Unit : (reflectedNormal - v2 * (2 / c2 * v2.Dot(reflectedNormal))).Unit;
    }

    /// <summary>
    /// This method picks an arbitrary, but consistent, normal perpendicular to the given
    /// initial tangent, to seed the frame propagation.
    /// </summary>
    private static Vector InitialNormal(Vector tangent)
    {
        Vector up = Directions.Up;

        if (Math.Abs(tangent.Dot(up)) > 0.999)
            up = Directions.Right;

        return (up - tangent * tangent.Dot(up)).Unit;
    }

    private static SweepFrame MakeFrame(Point position, Vector tangent, Vector normal)
    {
        return new SweepFrame
        {
            Position = position,
            Tangent = tangent,
            Normal = normal,
            Binormal = tangent.Cross(normal)
        };
    }
}
