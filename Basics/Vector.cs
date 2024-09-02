namespace RayTracer.Basics;

/// <summary>
/// This class represents a vector.
/// </summary>
public class Vector : NumberTuple
{
    /// <summary>
    /// This property returns the magnitude (length) of the vector.
    /// </summary>
    public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>
    /// This property returns this vector as a unit vector.
    /// </summary>
    public Vector Unit => Normalize();

    public Vector(double x, double y, double z, double w = 0) : base(x, y, z, w) {}

    public Vector(Point point) : base(point.X, point.Y, point.Z, 0) {}

    /// <summary>
    /// This method normalizes this vector into its unit form.
    /// </summary>
    /// <returns>This vector as a unit vector.</returns>
    private Vector Normalize()
    {
        double divisor = Magnitude;

        return new Vector(X / divisor, Y / divisor, Z / divisor);
    }

    /// <summary>
    /// This method produces the dot product of this vector with the given one.
    /// </summary>
    /// <param name="vector">The vector to compute the dot product with.</param>
    /// <returns>The dot product of this vector with the given one.</returns>
    public double Dot(Vector vector)
    {
        return X * vector.X + Y * vector.Y + Z * vector.Z;
    }

    /// <summary>
    /// This method produces the reflection of this vector around the given normal.
    /// </summary>
    /// <param name="normal">The normal to reflect around.</param>
    /// <returns>The reflection of this vector around the normal.</returns>
    public Vector Reflect(Vector normal)
    {
        return this - normal * 2 * Dot(normal);
    }

    /// <summary>
    /// This is a helper method for "cleaning" the vector after transformations which might
    /// set <c>W</c> to something other than zero.
    /// </summary>
    /// <returns>This object, for fluency.</returns>
    public Vector Clean()
    {
        W = 0;

        return this;
    }

    /// <summary>
    /// This method produces the cross product of this vector with the given one.
    /// </summary>
    /// <param name="vector">The vector to compute the cross product with.</param>
    /// <returns>The cross product of this vector with the given one.</returns>
    public Vector Cross(Vector vector)
    {
        return new Vector(
            Y * vector.Z - Z * vector.Y,
            Z * vector.X - X * vector.Z,
            X * vector.Y - Y * vector.X
        );
    }

    // ---------
    // Operators
    // ---------

    /// <summary>
    /// This method negates a vector.
    /// </summary>
    /// <param name="vector">The vector to negate.</param>
    /// <returns>The new point.</returns>
    public static Vector operator -(Vector vector)
    {
        return new Vector(-vector.X, -vector.Y, -vector.Z);
    }

    /// <summary>
    /// This method is used to add two vectors together.
    /// </summary>
    /// <param name="left">The first vector to add.</param>
    /// <param name="right">The second vector to add.</param>
    /// <returns>The new vector.</returns>
    public static Vector operator +(Vector left, Vector right)
    {
        return new Vector(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    /// <summary>
    /// This method is used to subtract one vector from another.
    /// </summary>
    /// <param name="left">The first vector to subtract from.</param>
    /// <param name="right">The second vector to subtract.</param>
    /// <returns>The new vector.</returns>
    public static Vector operator -(Vector left, Vector right)
    {
        return new Vector(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    /// <summary>
    /// This method is used to multiply a vector by a scalar.
    /// </summary>
    /// <param name="left">The vector to apply the scalar to.</param>
    /// <param name="right">The scalar to multiply the vector by.</param>
    /// <returns>The new vector.</returns>
    public static Vector operator *(Vector left, double right)
    {
        return new Vector(left.X * right, left.Y * right, left.Z * right);
    }

    /// <summary>
    /// This method is used to multiply a vector by a scalar.
    /// </summary>
    /// <param name="left">The scalar to multiply the vector by.</param>
    /// <param name="right">The vector to apply the scalar to.</param>
    /// <returns>The new vector.</returns>
    public static Vector operator *(double left, Vector right)
    {
        return new Vector(left * right.X, left * right.Y, left * right.Z);
    }

    /// <summary>
    /// This method is used to divide a vector by a scalar.
    /// </summary>
    /// <param name="left">The vector to apply the scalar to.</param>
    /// <param name="right">The scalar to divide the vector by.</param>
    /// <returns>The new vector.</returns>
    public static Vector operator /(Vector left, double right)
    {
        return new Vector(left.X / right, left.Y / right, left.Z / right);
    }
}
