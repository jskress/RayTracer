using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a sweep: a tessellated surface formed by lofting an arbitrary 2D
/// profile along an arbitrary 3D spline.  Unlike <see cref="Tube"/> (which stays fully
/// analytic by restricting itself to a circular cross-section), a sweep can carry any
/// profile shape, at the cost of being an approximation -- the same tessellation trade-off
/// <see cref="HeightField"/> already accepts, and for the same reason: there's no closed-
/// form solution once the profile isn't circularly symmetric.
///
/// The profile is lofted at a sequence of frames along the spline, oriented with a
/// rotation-minimizing frame (see <see cref="RotationMinimizingFrame"/>) rather than a
/// naive Frenet-Serret frame, so the loft doesn't twist on straight runs or flip at
/// inflection points.  If the profile is a closed shape (its own path ends with a "close"),
/// the two ends are capped with a triangulated fill of the profile (see
/// <see cref="PolygonTriangulator"/>) unless <see cref="Open"/> overrides that; an open
/// profile leaves a ribbon-like open edge at each end regardless, since there's no "inside"
/// to fill.
/// </summary>
public class Sweep : Group
{
    /// <summary>
    /// This property holds the 2D cross-section to loft along our spline.
    /// </summary>
    public GeneralPath Profile { get; set; }

    /// <summary>
    /// This property holds the 3D path our profile is lofted along.
    /// </summary>
    public Spline Spline { get; set; }

    /// <summary>
    /// This property holds the number of tessellation steps to take across each segment of
    /// our spline.  Higher values give a smoother loft at the cost of more triangles.
    /// </summary>
    public int Steps { get; set; } = 24;

    /// <summary>
    /// This property holds the number of tessellation steps to take across each segment of
    /// our profile.
    /// </summary>
    public int ProfileSteps { get; set; } = 16;

    /// <summary>
    /// This property forces the sweep to stay uncapped even when its profile is closed --
    /// useful when a closed profile is wanted for its shape (e.g., a smooth outline) but
    /// the scene doesn't want a solid, capped result.
    /// </summary>
    public bool Open { get; set; }

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        CreateTriangles();

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method builds the sweep's mesh: one rotation-minimizing frame per sampled
    /// spline point, the profile lofted into each frame's plane, and a band of triangles --
    /// bucketed one <see cref="Group"/> per band, mirroring <see cref="HeightField"/>'s own
    /// row-bucketing -- connecting each consecutive pair of rings.
    /// </summary>
    private void CreateTriangles()
    {
        if (!Spline.Discontinuous)
        {
            SegmentContinuity.Validate(
                Spline.Start,
                Spline.Segments.Select(spec => (spec.Control1, spec.Control2, spec.End)),
                "sweep's spline");
        }

        List<ISplineCurve> curves = Spline.GetCurves();
        (List<Point> positions, List<Vector> tangents) = SampleSpline(curves);
        List<SweepFrame> frames = RotationMinimizingFrame.Compute(positions, tangents);
        List<TwoDPoint> profilePoints = Profile.Sample(ProfileSteps);
        List<List<Point>> rings = frames
            .Select(frame => profilePoints
                .Select(point => LoftPoint(frame, point))
                .ToList())
            .ToList();

        for (int ringIndex = 0; ringIndex < rings.Count - 1; ringIndex++)
        {
            Group band = new Group();

            for (int pointIndex = 0; pointIndex < profilePoints.Count - 1; pointIndex++)
            {
                Point a = rings[ringIndex][pointIndex];
                Point b = rings[ringIndex][pointIndex + 1];
                Point c = rings[ringIndex + 1][pointIndex + 1];
                Point d = rings[ringIndex + 1][pointIndex];

                band.Add(new Triangle { Point1 = a, Point2 = c, Point3 = b });
                band.Add(new Triangle { Point1 = a, Point2 = d, Point3 = c });
            }

            Add(band);
        }

        if (!Open && IsProfileClosed(profilePoints))
            AddCaps(rings[0], rings[^1], profilePoints);
    }

    /// <summary>
    /// This method returns whether the sampled profile is a closed shape -- one whose own
    /// path ended with a "close", so its first and last sampled points coincide.
    /// </summary>
    /// <param name="profilePoints">The sampled profile points.</param>
    /// <returns><c>true</c>, if the profile is closed.</returns>
    private static bool IsProfileClosed(List<TwoDPoint> profilePoints)
    {
        TwoDPoint first = profilePoints[0];
        TwoDPoint last = profilePoints[^1];

        return first.X.Near(last.X) && first.Y.Near(last.Y);
    }

    /// <summary>
    /// This method fills in the two end caps of a closed-profile sweep, using
    /// <see cref="PolygonTriangulator"/> to triangulate the (2D) profile once and reusing
    /// that same triangulation, lofted into each end's own frame, for both caps.  The two
    /// caps face opposite directions along the spline, so the end cap's triangles are added
    /// with reversed winding from the start cap's.
    /// </summary>
    /// <param name="startRing">The first ring of lofted profile points.</param>
    /// <param name="endRing">The last ring of lofted profile points.</param>
    /// <param name="profilePoints">The sampled profile points (2D, not lofted), whose last
    /// point duplicates its first (the profile's own closing point) and so is excluded from
    /// triangulation.</param>
    private void AddCaps(List<Point> startRing, List<Point> endRing, List<TwoDPoint> profilePoints)
    {
        List<TwoDPoint> uniquePoints = profilePoints[..^1];
        List<(int A, int B, int C)> triangles = PolygonTriangulator.Triangulate(uniquePoints);

        if (triangles.Count == 0)
            return;

        Group startCap = new Group();
        Group endCap = new Group();

        foreach ((int a, int b, int c) in triangles)
        {
            startCap.Add(new Triangle { Point1 = startRing[a], Point2 = startRing[c], Point3 = startRing[b] });
            endCap.Add(new Triangle { Point1 = endRing[a], Point2 = endRing[b], Point3 = endRing[c] });
        }

        Add(startCap);
        Add(endCap);
    }

    /// <summary>
    /// This method lifts one 2D profile point into world space at the given frame.
    /// </summary>
    /// <param name="frame">The frame to loft the point into.</param>
    /// <param name="point">The 2D profile point to loft.</param>
    /// <returns>The corresponding 3D point.</returns>
    private static Point LoftPoint(SweepFrame frame, TwoDPoint point)
    {
        return frame.Position + frame.Normal * point.X + frame.Binormal * point.Y;
    }

    /// <summary>
    /// This method samples every curve making up our spline at <see cref="Steps"/>
    /// increments, returning the sampled positions and the (unit) tangent at each one.
    /// Consecutive curves share their boundary point, so only the first curve contributes
    /// its starting sample; every other curve contributes only its own interior and ending
    /// samples.
    /// </summary>
    /// <param name="curves">The curves making up our spline.</param>
    /// <returns>The sampled positions and tangents, in spline order.</returns>
    private (List<Point> Positions, List<Vector> Tangents) SampleSpline(List<ISplineCurve> curves)
    {
        List<Point> positions = [];
        List<Vector> tangents = [];

        for (int curveIndex = 0; curveIndex < curves.Count; curveIndex++)
        {
            ISplineCurve curve = curves[curveIndex];
            int startStep = curveIndex == 0 ? 0 : 1;

            for (int step = startStep; step <= Steps; step++)
            {
                double u = (double) step / Steps;

                positions.Add(curve.GetPoint(u));
                tangents.Add(curve.GetTangent(u));
            }
        }

        return (positions, tangents);
    }
}
