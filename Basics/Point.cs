namespace RayTracer.Basics;

/// <summary>
/// This class represents a point.
/// </summary>
public class Point : NumberTuple
{
    public static readonly Point Zero = new (0, 0, 0);

    public Point(double x, double y, double z, double w = 1) : base(x, y, z, w) {}

    // ---------
    // Operators
    // ---------

    /// <summary>
    /// This method adds a vector to a point.
    /// </summary>
    /// <param name="left">The point to add the vector to.</param>
    /// <param name="right">The vector to add to the point.</param>
    /// <returns>The new point.</returns>
    public static Point operator +(Point left, Vector right)
    {
        return new Point(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    /// <summary>
    /// This method adds a vector to a point.
    /// </summary>
    /// <param name="left">The vector to add to the point.</param>
    /// <param name="right">The point to add the vector to.</param>
    /// <returns>The new point.</returns>
    public static Point operator +(Vector left, Point right)
    {
        return new Point(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    /// <summary>
    /// This method subtracts one point from another to produce a vector.
    /// </summary>
    /// <param name="left">The point to subtract from..</param>
    /// <param name="right">The point to subtract.</param>
    /// <returns>The resulting vector.</returns>
    public static Vector operator -(Point left, Point right)
    {
        return new Vector(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    /// <summary>
    /// This method subtracts a vector from a point.
    /// </summary>
    /// <param name="left">The point to add the vector to.</param>
    /// <param name="right">The vector to add to the point.</param>
    /// <returns>The new point.</returns>
    public static Point operator -(Point left, Vector right)
    {
        return new Point(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }
}
