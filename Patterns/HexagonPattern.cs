using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the hexagon pattern.
/// </summary>
public class HexagonPattern : Pattern
{
    private const double XFactor = 0.5;         // Each triangle is split in half for the grid
    private const double ZFactor = 0.866025404; // sqrt(3)/2 -- Height of an equilateral triangle
    private const double ZShift = 5.196152424;  // sqrt(3)/2) * 6 (because the grid is 6 blocks high)

    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 3;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        double x = Math.Abs(point.X);
        double z = point.Z;

        // Avoid mirroring across x-axis.

        z = z < 0.0 ? ZShift - Math.Abs(z) : z;

        // Scale point to make calculations easier.

        double xs = x / XFactor;
        double zs = z / ZFactor;

        // Map points into the 6 x 6 grid where the basic formula works.

        xs -= Math.Floor(xs / 6.0) * 6.0;
        zs -= Math.Floor(zs / 6.0) * 6.0;

        // Get a block in the 6 x 6 grid.

        int xm = (int) Math.Floor(xs) % 6;
        int zm = (int) Math.Floor(zs) % 6;

        switch (xm)
        {
            case 0:
            case 5:
                switch (zm)
                {
                    case 0:
                    case 5:
                        return 0;
                    case 1:
                    case 2:
                        return 1;
                    case 3:
                    case 4:
                        return 2;
                }
                break;
            case 2:
            case 3:
                switch (zm)
                {
                    case 0:
                    case 1:
                        return 2;
                    case 2:
                    case 3:
                        return 0;
                    case 4:
                    case 5:
                        return 1;
                }
                break;
            case 1:
            case 4:
                // Map the point into the block at the origin.
                double xl = xs - xm;
                double zl = zs - zm;

                // These blocks have negative slopes so we flip it horizontally.
                if ((xm + zm) % 2 == 1)
                    xl = 1.0 - xl;

                // Avoid a divide-by-zero error.
                if (xl.Near(0))
                    xl = DoubleExtensions.Epsilon;

                // Is the angle less than or greater than 45 degrees?
                if (zl / xl < 1.0)
                {
                    switch (zm)
                    {
                        case 0:
                        case 3:
                            return 0;
                        case 2:
                        case 5:
                            return 1;
                        case 1:
                        case 4:
                            return 2;
                    }
                }
                else
                {
                    switch (zm)
                    {
                        case 0:
                        case 3:
                            return 2;
                        case 2:
                        case 5:
                            return 0;
                        case 1:
                        case 4:
                            return 1;
                    }
                }
                break;
        }

        return 0;
    }
}
