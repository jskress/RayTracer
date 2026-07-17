using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the radial pattern: the angle about the Y axis, so a colour map wraps once
/// per turn.  Where the other band patterns measure how far out a point lies, this one measures
/// which way round it lies -- which is what spokes, wedges, dials and barber poles are made of.
///
/// Note the value runs from 0.25 to 1.25 rather than 0 to 1, which is POV-Ray's own doing and is
/// deliberate: colour maps wrap what they are given, so the quarter turn added on simply moves
/// the seam -- the one place round the turn where the map jumps from its last colour back to its
/// first -- off the -Z axis and onto +X.
/// </summary>
public class RadialPattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        // Right on the axis there is no angle to speak of -- every direction is equally true --
        // so rather than let atan2 pick one arbitrarily, settle on a quarter turn, as POV-Ray
        // does.  The tolerance is POV's too.
        if (Math.Abs(point.X) < 0.001 && Math.Abs(point.Z) < 0.001)
            return 0.25;

        // atan2 runs from -pi to pi; the shift and divide bring that into [0, 1], and the extra
        // quarter turn puts the seam behind the +Z axis rather than across it.
        return 0.25 + (Math.Atan2(point.X, point.Z) + Math.PI) / (2 * Math.PI);
    }
}
