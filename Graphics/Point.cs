namespace RayTracer.Graphics;

/// <summary>
/// This class represents a point in the raytracer.
/// </summary>
public class Point : Vector
{
    public Point(double x, double y, double z) : base(x, y, z) {}

    public Point(Vector vector) : base(vector.X, vector.Y, vector.Z) {}

    // -------------------------------------------------------------------------
    // Operators
    // -------------------------------------------------------------------------

    /// <summary>
    /// Add a vector to a point.
    /// </summary>
    /// <param name="left">The left point to add.</param>
    /// <param name="right">The right vector to add.</param>
    /// <returns>The sum of the vectors.</returns>
    public static Point operator +(Point left, Vector right)
    {
        return new Point(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z);
    }

    /// <summary>
    /// Subtract a vector from a point.
    /// </summary>
    /// <param name="left">The left point to subtract.</param>
    /// <param name="right">The right vector to subtract.</param>
    /// <returns>The difference of the vectors.</returns>
    public static Point operator -(Point left, Vector right)
    {
        return new Point(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z);
    }
}
