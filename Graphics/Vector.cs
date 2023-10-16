using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents an arbitrary 4-element vector.  We don't use .Net's
/// builtin <c>Vector4</c> since that's based on floats and we want doubles.
/// </summary>
public class Vector
{
    private const double ZeroTolerance = 1e-8;

    /// <summary>
    /// This method generates a random vector.
    /// </summary>
    /// <returns>The random vector.</returns>
    public static Vector RandomVector()
    {
        double x = DoubleExtensions.RandomDouble();
        double y = DoubleExtensions.RandomDouble();
        double z = DoubleExtensions.RandomDouble();

        return new Vector(x, y, z);
    }

    /// <summary>
    /// This method generates a random vector, with each component in a specified
    /// range.
    /// </summary>
    /// <returns>The random vector.</returns>
    private static Vector RandomVector(double min, double max)
    {
        double x = DoubleExtensions.RandomDouble(min, max);
        double y = DoubleExtensions.RandomDouble(min, max);
        double z = DoubleExtensions.RandomDouble(min, max);

        return new Vector(x, y, z);
    }

    /// <summary>
    /// This method returns a random unit vector.
    /// </summary>
    /// <returns>A random unit vector.</returns>
    public static Vector RandomUnitVector()
    {
        Vector vector = RandomVector(-1, 1);

        while (vector.LengthSquared >= 1)
            vector = RandomVector(-1, 1);

        return vector.Unit();
    }

    /// <summary>
    /// This method is used to generate a random vector in the proper hemisphere
    /// based on the given surface normal.
    /// </summary>
    /// <param name="normal">The surface normal that controls which hemisphere the
    /// returned vector is in.</param>
    /// <returns>A random unit vector in the proper hemisphere.</returns>
    public static Vector RandomHemisphereVector(Vector normal)
    {
        Vector vector = RandomUnitVector();

        return vector.Dot(normal) > 0 ? vector : -vector;
    }

    /// <summary>
    /// This method is used to generate a random vector in a flat plain disk.
    /// </summary>
    /// <returns>The random vector.</returns>
    public static Vector RandomInUnitDisk()
    {
        Vector result;

        do
        {
            double x = DoubleExtensions.RandomDouble(-1, 1);
            double y = DoubleExtensions.RandomDouble(-1, 1);

            result = new Vector(x, y, 0);
        }
        while (result.LengthSquared >= 1);

        return result;
    }

    /// <summary>
    /// The X component of the vector.
    /// </summary>
    public double X { get; }

    /// <summary>
    /// The Y component of the vector.
    /// </summary>
    public double Y { get; }

    /// <summary>
    /// The Z component of the vector.
    /// </summary>
    public double Z { get; }

    /// <summary>
    /// This property provides the square of the length of this vector.
    /// </summary>
    public double LengthSquared => Dot(this);

    /// <summary>
    /// This property provides the length of this vector.
    /// </summary>
    public double Length => Math.Sqrt(LengthSquared);

    public Vector(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// This calculates the dot product of this vector with the given one.
    /// </summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The dot product of this vector with the other one.</returns>
    public double Dot(Vector other)
    {
        return X * other.X + Y * other.Y + Z * other.Z;
    }

    /// <summary>
    /// This calculates the cross product of this vector with the given one.
    /// </summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The cross product of this vector with the other one.</returns>
    public Vector Cross(Vector other)
    {
        return new Vector(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X);
    }

    /// <summary>
    /// This method returns this vector in its unit form.
    /// </summary>
    /// <returns>The unit form of this vector.</returns>
    public Vector Unit()
    {
        return this / Length;
    }

    /// <summary>
    /// This method tells us whether this vector is very near zero.
    /// </summary>
    /// <returns><c>true</c>, if all components are very, very close to zero or,
    /// <c>false</c>, otherwise.</returns>
    public bool NearZero()
    {
        return Math.Abs(X) < ZeroTolerance &&
               Math.Abs(Y) < ZeroTolerance &&
               Math.Abs(Z) < ZeroTolerance;
    }

    /// <summary>
    /// This method generates the reflection of this vector around the given normal.
    /// </summary>
    /// <param name="normal">The normal to reflect around.</param>
    /// <returns>This vector, reflected around the given normal.</returns>
    public Vector Reflect(Vector normal)
    {
        return this - normal * Dot(normal) * 2;
    }

    /// <summary>
    /// This is a helper method for determining the cos(𝜃) of this vector,
    /// which should be unit, and the given surface normal.
    /// </summary>
    /// <param name="normal">The surface normal to use.</param>
    /// <returns>The cos(𝜃) value.</returns>
    public double CosTheta(Vector normal)
    {
        Vector negative = -this;

        return Math.Min(negative.Dot(normal), 1.0);
    }

    /// <summary>
    /// This method returns the refracted vector relative to the provided normal.
    /// </summary>
    /// <param name="normal">The surface normal to refract in relation to.</param>
    /// <param name="refractionRatio">The refraction ratio to use.</param>
    /// <returns>The refracted vector.</returns>
    public Vector Refract(Vector normal, double refractionRatio)
    {
        double cosTheta = CosTheta(normal);
        Vector perpendicular = (this + normal * cosTheta) * refractionRatio;
        Vector parallel = normal * -Math.Sqrt(Math.Abs(1.0 - perpendicular.LengthSquared));

        return perpendicular + parallel;
    }

    public override string ToString()
    {
        return $"Vector({X}, {Y}, {Z})";
    }

    // -------------------------------------------------------------------------
    // Operators
    // -------------------------------------------------------------------------

    /// <summary>
    /// Negate a vector.
    /// </summary>
    /// <param name="vector">The vector to negate.</param>
    /// <returns>The negated vector.</returns>
    public static Vector operator -(Vector vector)
    {
        return new Vector(-vector.X, -vector.Y, -vector.Z);
    }

    /// <summary>
    /// Add two vectors.
    /// </summary>
    /// <param name="left">The left vector to add.</param>
    /// <param name="right">The right vector to add.</param>
    /// <returns>The sum of the vectors.</returns>
    public static Vector operator +(Vector left, Vector right)
    {
        return new Vector(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z);
    }

    /// <summary>
    /// Add a number to a vector.
    /// </summary>
    /// <param name="left">The vector to add the number to.</param>
    /// <param name="right">The number to add to the vector.</param>
    /// <returns>The sum of the vector and the number.</returns>
    public static Vector operator +(Vector left, double right)
    {
        return new Vector(
            left.X + right,
            left.Y + right,
            left.Z + right);
    }

    /// <summary>
    /// Subtract two vectors.
    /// </summary>
    /// <param name="left">The left vector to subtract.</param>
    /// <param name="right">The right vector to subtract.</param>
    /// <returns>The difference of the vectors.</returns>
    public static Vector operator -(Vector left, Vector right)
    {
        return new Vector(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z);
    }

    /// <summary>
    /// Multiply two vectors.
    /// </summary>
    /// <param name="left">The first vector to multiply.</param>
    /// <param name="right">The second vector to multiply.</param>
    /// <returns>The result of multiplying the two vectors.</returns>
    public static Vector operator *(Vector left, Vector right)
    {
        return new Vector(
            left.X * right.X,
            left.Y * right.Y,
            left.Z * right.Z);
    }

    /// <summary>
    /// Multiply a vector by a number.
    /// </summary>
    /// <param name="left">The vector to multiply.</param>
    /// <param name="right">The number to multiply.</param>
    /// <returns>The result of multiplying the vector by the number.</returns>
    public static Vector operator *(Vector left, double right)
    {
        return new Vector(
            left.X * right,
            left.Y * right,
            left.Z * right);
    }

    /// <summary>
    /// Multiply a vector by a number.
    /// </summary>
    /// <param name="left">The number to multiply.</param>
    /// <param name="right">The vector to multiply.</param>
    /// <returns>The result of multiplying the vector by the number.</returns>
    public static Vector operator *(double left, Vector right)
    {
        return right * left;
    }

    /// <summary>
    /// Divide a vector by a number.
    /// </summary>
    /// <param name="left">The vector to divide.</param>
    /// <param name="right">The number to divide.</param>
    /// <returns>The result of dividing the vector by the number.</returns>
    public static Vector operator /(Vector left, double right)
    {
        return new Vector(
            left.X / right,
            left.Y / right,
            left.Z / right);
    }
}
