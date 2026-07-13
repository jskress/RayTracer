namespace RayTracer.Basics;

/// <summary>
/// This class represents a point.
/// </summary>
public class Point : NumberTuple, IEquatable<Point>
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
    /// <param name="left">The point to subtract from.</param>
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

    /// <summary>
    /// This method provides the equality operator for two points.
    /// </summary>
    /// <param name="left">The first point to compare.</param>
    /// <param name="right">The second point to compare.</param>
    /// <returns><c>true</c>, if the points are equal, or <c>false</c>, if not.</returns>
    public static bool operator ==(Point left, Point right)
    {
        return ReferenceEquals(left, right) || (left is not null && left.Equals(right));
    }

    /// <summary>
    /// This method provides the inequality operator for two points.
    /// </summary>
    /// <param name="left">The first point to compare.</param>
    /// <param name="right">The second point to compare.</param>
    /// <returns><c>true</c>, if the points are not equal, or <c>false</c>, if they are.</returns>
    public static bool operator !=(Point left, Point right)
    {
        return !(left == right);
    }

    /// <summary>
    /// This method tests whether the given point is the same as this one.
    /// </summary>
    /// <param name="other">The point to compare to.</param>
    /// <returns><c>true</c>, if the points have the same coordinates, or<c>false</c>,
    /// if not.</returns>
    public bool Equals(Point other)
    {
        return other is not null &&
               (ReferenceEquals(this, other) || base.Equals(other));
    }

    /// <summary>
    /// This method tests whether the given object is the same as this one.
    /// </summary>
    /// <param name="other">The object to compare to.</param>
    /// <returns><c>true</c>, if the given object it s a point, and it has the same coordinates
    /// as this one, or<c>false</c>, if not.</returns>
    public override bool Equals(object other)
    {
        return other is not null && (ReferenceEquals(this, other) ||
               (other.GetType() == GetType() && Equals((Point) other)));
    }

    /// <summary>
    /// This method returns the hash code for this object.
    /// </summary>
    /// <returns>This object's hash code.</returns>
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}
