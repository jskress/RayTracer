using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class checks whether a chain of straight/quadratic/cubic segments -- shared shape
/// between <see cref="Tube"/>'s own control points and <see cref="Spline"/>'s -- is tangent
/// continuous at each of its interior control points: whether the exit direction of one
/// segment matches the entry direction of the next.  A mismatch there ("position
/// continuous, but not tangent continuous") is a kink, and while perfectly valid geometry on
/// its own, it plays badly with anything that tracks orientation along the chain (like a
/// rotation-minimizing frame): the frame has no choice but to absorb the whole mismatch in
/// one single step, which reads as a sudden, surprising twist right at that seam.  Since a
/// continuous chain is the common case, this check runs by default and can be suppressed
/// (for a chain where a sharp kink is actually wanted) by the caller.
/// </summary>
internal static class SegmentContinuity
{
    /// <summary>
    /// This method verifies that every interior control point in the given chain is tangent
    /// continuous, throwing a descriptive exception at the first one that isn't.
    /// </summary>
    /// <param name="start">The chain's own starting point.</param>
    /// <param name="segments">Each segment in the chain, as its (optional) first control
    /// point, (optional) second control point, and end point -- <c>null</c> for both control
    /// points means a straight line, <c>null</c> for just the second means a quadratic
    /// curve, and both present means a cubic curve.</param>
    /// <param name="noun">A noun (e.g. "tube", "sweep's spline") to use in the error
    /// message.</param>
    /// <param name="checkContinuity">Whether to check for kinks.  A chain marked discontinuous
    /// says its kinks are meant, so that half is skipped -- but the shape check below is not, for
    /// the reason given there.</param>
    public static void Validate(
        Point start, IEnumerable<(Point Control1, Point Control2, Point End)> segments, string noun,
        bool checkContinuity = true)
    {
        Point current = start;
        Vector previousExitTangent = null;
        int index = 0;

        foreach ((Point control1, Point control2, Point end) in segments)
        {
            (Vector entryTangent, Vector exitTangent) = GetTangents(current, control1, control2, end);

            // A segment that goes nowhere is checked whatever the chain was marked, because it is
            // not a kink and "discontinuous" does not mean what it would need to mean to excuse it.
            // Saying so here also keeps the message below honest: the tangent of a segment with no
            // length is the zero vector, its unit is a NaN, and the angle worked out from it comes
            // out as "about NaN degrees" -- which names neither the place nor the problem.
            if (IsEmpty(entryTangent) || IsEmpty(exitTangent))
            {
                throw new Exception(
                    $"The {noun} has a segment of no length at control point {index} (near " +
                    $"[{current.X:F3}, {current.Y:F3}, {current.Z:F3}]) -- its start, its control " +
                    "points and its end are all the same place, so it has no direction to travel " +
                    "in.  Remove it, or move one of its points.");
            }

            if (checkContinuity && previousExitTangent is not null &&
                !DirectionsMatch(previousExitTangent, entryTangent))
            {
                double angle = Math.Acos(Math.Clamp(previousExitTangent.Unit.Dot(entryTangent.Unit), -1, 1)) *
                    180 / Math.PI;

                throw new Exception(
                    $"The {noun} isn't tangent-continuous at control point {index} (near " +
                    $"[{current.X:F3}, {current.Y:F3}, {current.Z:F3}]) -- the segments meeting there " +
                    $"bend by about {angle:F1} degrees instead of flowing smoothly into each other. " +
                    "Mark it \"discontinuous\" if this kink is intentional.");
            }

            previousExitTangent = exitTangent;
            current = end;
            index++;
        }
    }

    /// <summary>
    /// This method reports whether a tangent has collapsed to nothing, which is what a segment
    /// whose points all coincide produces.
    /// </summary>
    private static bool IsEmpty(Vector tangent)
    {
        return tangent.Magnitude <= EmptyTolerance;
    }

    /// <summary>
    /// How short a tangent may be before the segment counts as having no length at all.
    /// </summary>
    private const double EmptyTolerance = 1e-12;

    /// <summary>
    /// This method returns the entry and exit tangent directions for a segment, given its
    /// start, (optional) control points and end -- the standard Bezier derivative-at-the-
    /// endpoints formulas, degree depending on which control points are present.
    /// </summary>
    private static (Vector Entry, Vector Exit) GetTangents(Point start, Point control1, Point control2, Point end)
    {
        if (control1 is null)
        {
            Vector direction = end - start;

            return (direction, direction);
        }

        if (control2 is null)
            return (2 * (control1 - start), 2 * (end - control1));

        return (3 * (control1 - start), 3 * (end - control2));
    }

    /// <summary>
    /// This method determines whether two directions are close enough to call "the same",
    /// allowing a little slack for the kind of small rounding a hand-typed decimal
    /// coordinate can introduce, while still catching genuine kinks (which, in practice,
    /// tend to be off by tens of degrees, not fractions of one).
    /// </summary>
    private static bool DirectionsMatch(Vector a, Vector b)
    {
        const double toleranceDegrees = 1.0;
        double cosTolerance = Math.Cos(toleranceDegrees * Math.PI / 180);

        return a.Unit.Dot(b.Unit) >= cosTolerance;
    }
}
