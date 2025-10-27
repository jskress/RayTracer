using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This record represents a 2D normal.
/// </summary>
public record TwoDVector(double X, double Y)
{
    /// <summary>
    /// This property returns the magnitude (length) of the vector.
    /// </summary>
    public double Magnitude => Math.Sqrt(X * X + Y * Y);

    /// <summary>
    /// This property returns this vector as a unit vector.
    /// </summary>
    public TwoDVector Unit => Normalize();

    /// <summary>
    /// This method normalizes this vector into its unit form.
    /// </summary>
    /// <returns>This vector as a unit vector.</returns>
    private TwoDVector Normalize()
    {
        double divisor = Magnitude;

        return new TwoDVector(X / divisor, Y / divisor);
    }

    /// <summary>
    /// This method is used to create a 3D vector from this one assuming we are in the X/Y
    /// plane.
    /// </summary>
    /// <returns>The 3D vector.</returns>
    public Vector FromXy()
    {
        return new Vector(X, Y, 0);
    }

    /// <summary>
    /// This method is used to create a 3D vector from this one assuming we are in the X/Z
    /// plane.
    /// </summary>
    /// <returns>The 3D vector.</returns>
    public Vector FromXz()
    {
        return new Vector(X, 0, Y);
    }

    /// <summary>
    /// This method is used to create a 2D vector from one in 3D by projecting it to the
    /// X/Y plane.
    /// </summary>
    /// <param name="vector">The vector to project.</param>
    /// <returns>The 2D projected vector.</returns>
    public static TwoDVector ProjectedToXy(Vector vector)
    {
        return new TwoDVector(vector.X, vector.Y);
    }

    /// <summary>
    /// This method is used to create a 2D vector from one in 3D by projecting it to the
    /// X/Z plane.
    /// </summary>
    /// <param name="vector">The vector to project.</param>
    /// <returns>The 2D projected vector.</returns>
    public static TwoDVector ProjectedToXz(Vector vector)
    {
        return new TwoDVector(vector.X, vector.Z);
    }

    /// <summary>
    /// This method is used to multiply a vector by a scalar.
    /// </summary>
    /// <param name="left">The vector to apply the scalar to.</param>
    /// <param name="right">The scalar to multiply the vector by.</param>
    /// <returns>The new vector.</returns>
    public static TwoDVector operator *(TwoDVector left, double right)
    {
        return new TwoDVector(left.X * right, left.Y * right);
    }
}
