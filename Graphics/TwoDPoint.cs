using RayTracer.Basics;
using SkiaSharp;

namespace RayTracer.Graphics;

/// <summary>
/// This record represents a 2D point.
/// </summary>
public record TwoDPoint(double X, double Y)
{
    public static readonly TwoDPoint Zero = new (0, 0);

    /// <summary>
    /// This method is used to create a 3D point from this one assuming we are in the X/Y
    /// plane.
    /// </summary>
    /// <returns>The 3D point.</returns>
    public Point FromXy()
    {
        return new Point(X, Y, 0);
    }

    /// <summary>
    /// This method is used to create a 3D point from this one assuming we are in the X/Z
    /// plane.
    /// </summary>
    /// <returns>The 3D point.</returns>
    public Point FromXz()
    {
        return new Point(X, 0, Y);
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }

    /// <summary>
    /// This method is used to create a 2D point from one in 3D by projecting it to the
    /// X/Y plane.
    /// </summary>
    /// <param name="point">The point to project.</param>
    /// <returns>The 2D projected point.</returns>
    public static TwoDPoint ProjectedToXy(Point point)
    {
        return new TwoDPoint(point.X, point.Y);
    }

    /// <summary>
    /// This method is used to create a 2D point from one in 3D by projecting it to the
    /// X/Z plane.
    /// </summary>
    /// <param name="point">The point to project.</param>
    /// <returns>The 2D projected point.</returns>
    public static TwoDPoint ProjectedToXz(Point point)
    {
        return new TwoDPoint(point.X, point.Z);
    }

    /// <summary>
    /// This method creates a point for the SkiaSharp library from this point.
    /// </summary>
    /// <returns>This point, as a SkiaSharp point.</returns>
    public SKPoint ToSkPoint()
    {
        return new SKPoint((float) X, (float) Y);
    }
    // ---------
    // Operators
    // ---------

    /// <summary>
    /// This method adds a vector to a point.
    /// </summary>
    /// <param name="left">The point to add the vector to.</param>
    /// <param name="right">The vector to add to the point.</param>
    /// <returns>The new point.</returns>
    public static TwoDPoint operator +(TwoDPoint left, TwoDVector right)
    {
        return new TwoDPoint(left.X + right.X, left.Y + right.Y);
    }


    /// <summary>
    /// This method is used to add two points together.
    /// </summary>
    /// <param name="left">The first point to add.</param>
    /// <param name="right">The second point to add.</param>
    /// <returns>The new point.</returns>
    public static TwoDPoint operator +(TwoDPoint left, TwoDPoint right)
    {
        return new TwoDPoint(left.X + right.X, left.Y + right.Y);
    }

    /// <summary>
    /// This method is used to subtract one point from another.
    /// </summary>
    /// <param name="left">The point to subtract from.</param>
    /// <param name="right">The point to subtract.</param>
    /// <returns>The new point.</returns>
    public static TwoDVector operator -(TwoDPoint left, TwoDPoint right)
    {
        return new TwoDVector(left.X - right.X, left.Y - right.Y);
    }

    /// <summary>
    /// This method is used to multiply a point by a scalar.
    /// </summary>
    /// <param name="left">The point to apply the scalar to.</param>
    /// <param name="right">The scalar to multiply the point by.</param>
    /// <returns>The new point.</returns>
    public static TwoDPoint operator *(TwoDPoint left, double right)
    {
        return new TwoDPoint(left.X * right, left.Y * right);
    }

    /// <summary>
    /// This method is used to multiply a point by a scalar.
    /// </summary>
    /// <param name="left">The scalar to multiply the point by.</param>
    /// <param name="right">The point to apply the scalar to.</param>
    /// <returns>The new point.</returns>
    public static TwoDPoint operator *(double left, TwoDPoint right)
    {
        return new TwoDPoint(left * right.X, left * right.Y);
    }
}
