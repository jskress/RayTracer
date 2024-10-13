using SkiaSharp;

namespace RayTracer.Basics;

/// <summary>
/// This record represents a 2D point.
/// </summary>
public record TwoDPoint(double X, double Y)
{
    public static readonly TwoDPoint Zero = new (0, 0);

    /// <summary>
    /// This method creates a point for the SkiaSharp library from this point.
    /// </summary>
    /// <returns>This point, as a SkiaSharp point.</returns>
    public SKPoint ToSkPoint()
    {
        return new SKPoint((float) X, (float) Y);
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
    public static TwoDPoint operator -(TwoDPoint left, TwoDPoint right)
    {
        return new TwoDPoint(left.X - right.X, left.Y - right.Y);
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
