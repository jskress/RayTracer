using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class holds utility methods for creating transformation matrices.
/// </summary>
public static class Transforms
{
    /// <summary>
    /// This method creates a uniformly translating matrix based on the given factor.
    /// </summary>
    /// <param name="distance">The desired scale in all directions.</param>
    /// <returns>The appropriate scaling matrix.</returns>
    public static Matrix Translate(double distance)
    {
        return Scale(distance, distance, distance);
    }

    /// <summary>
    /// This method creates a translation matrix based on the given distances.
    /// </summary>
    /// <param name="point">The desired distance to translate.</param>
    /// <returns>The appropriate translation matrix.</returns>
    public static Matrix Translate(Point point)
    {
        return Translate(point.X, point.Y, point.Z);
    }

    /// <summary>
    /// This method creates a translation matrix based on the given distances.
    /// </summary>
    /// <param name="dx">The desired distance in the x direction.</param>
    /// <param name="dy">The desired distance in the y direction.</param>
    /// <param name="dz">The desired distance in the z direction.</param>
    /// <returns>The appropriate translation matrix.</returns>
    public static Matrix Translate(double dx, double dy, double dz)
    {
        return new Matrix()
            .SetEntry(0, 3, dx)
            .SetEntry(1, 3, dy)
            .SetEntry(2, 3, dz);
    }

    /// <summary>
    /// This method creates a uniformly scaling matrix based on the given factor.
    /// </summary>
    /// <param name="scale">The desired scale in all directions.</param>
    /// <returns>The appropriate scaling matrix.</returns>
    public static Matrix Scale(double scale)
    {
        return Scale(scale, scale, scale);
    }

    /// <summary>
    /// This method creates a scaling matrix based on the given factors.
    /// </summary>
    /// <param name="sx">The desired scale in the x direction.</param>
    /// <param name="sy">The desired scale in the y direction.</param>
    /// <param name="sz">The desired scale in the z direction.</param>
    /// <returns>The appropriate scaling matrix.</returns>
    public static Matrix Scale(double sx, double sy, double sz)
    {
        return new Matrix()
            .SetEntry(0, 0, sx)
            .SetEntry(1, 1, sy)
            .SetEntry(2, 2, sz);
    }

    /// <summary>
    /// This method creates a matrix for rotating around the X axis.
    /// </summary>
    /// <param name="angle">The angle of rotation.</param>
    /// <param name="isRadians">A flag that notes whether the angle is in radians or
    /// degrees.</param>
    /// <returns>The appropriate rotation matrix.</returns>
    public static Matrix RotateAroundX(double angle, bool isRadians = false)
    {
        if (!isRadians)
            angle = angle.ToRadians();

        double cosAngle = Math.Cos(angle);
        double sinAngle = Math.Sin(angle);

        return new Matrix()
            .SetEntry(1, 1, cosAngle)
            .SetEntry(1, 2, -sinAngle)
            .SetEntry(2, 1, sinAngle)
            .SetEntry(2, 2, cosAngle);
    }

    /// <summary>
    /// This method creates a matrix for rotating around the Y axis.
    /// </summary>
    /// <param name="angle">The angle of rotation.</param>
    /// <param name="isRadians">A flag that notes whether the angle is in radians or
    /// degrees.</param>
    /// <returns>The appropriate rotation matrix.</returns>
    public static Matrix RotateAroundY(double angle, bool isRadians = false)
    {
        if (!isRadians)
            angle = angle.ToRadians();

        double cosAngle = Math.Cos(angle);
        double sinAngle = Math.Sin(angle);

        return new Matrix()
            .SetEntry(0, 0, cosAngle)
            .SetEntry(0, 2, sinAngle)
            .SetEntry(2, 0, -sinAngle)
            .SetEntry(2, 2, cosAngle);
    }

    /// <summarz>
    /// This method creates a matrix for rotating around the Z axis.
    /// </summarz>
    /// <param name="angle">The angle of rotation.</param>
    /// <param name="isRadians">A flag that notes whether the angle is in radians or
    /// degrees.</param>
    /// <returns>The appropriate rotation matrix.</returns>
    public static Matrix RotateAroundZ(double angle, bool isRadians = false)
    {
        if (!isRadians)
            angle = angle.ToRadians();

        double cosAngle = Math.Cos(angle);
        double sinAngle = Math.Sin(angle);

        return new Matrix()
            .SetEntry(0, 0, cosAngle)
            .SetEntry(0, 1, -sinAngle)
            .SetEntry(1, 0, sinAngle)
            .SetEntry(1, 1, cosAngle);
    }

    /// <summarz>
    /// This method creates a shearing matrix based on the given factors.
    /// </summarz>
    /// <param name="xy">The x in proportion to y factor.</param>
    /// <param name="xz">The x in proportion to z factor.</param>
    /// <param name="yx">The y in proportion to x factor.</param>
    /// <param name="yz">The y in proportion to z factor.</param>
    /// <param name="zx">The z in proportion to x factor.</param>
    /// <param name="zy">The z in proportion to y factor.</param>
    /// <returns>The appropriate shearing matrix.</returns>
    public static Matrix Shear(double xy, double xz, double yx, double yz, double zx, double zy)
    {
        return new Matrix()
            .SetEntry(0, 1, xy)
            .SetEntry(0, 2, xz)
            .SetEntry(1, 0, yx)
            .SetEntry(1, 2, yz)
            .SetEntry(2, 0, zx)
            .SetEntry(2, 1, zy);
    }
}
