using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigmentation;

/// <summary>
/// This class defines the base class for something that can accept a point in space and
/// return a color for it.  This gives us support for patterns, gradients and so forth, in
/// addition to solid colors.
/// </summary>
public abstract class Pigmentation
{
    /// <summary>
    /// This property holds the transformation matrix for the pigmentation.
    /// </summary>
    public Matrix Transform
    {
        get => _transform;
        set
        {
            _transform = value;

            if (_inverseTransform.IsValueCreated)
                _inverseTransform = new Lazy<Matrix>(CreateInverseTransform);
        }
    }

    /// <summary>
    /// This property provides the inverse of the pigmentation's transform.
    /// </summary>
    private Matrix InverseTransform => _inverseTransform.Value;

    private Matrix _transform;
    private Lazy<Matrix> _inverseTransform;

    protected Pigmentation()
    {
        _transform = Matrix.Identity;
        _inverseTransform = new Lazy<Matrix>(CreateInverseTransform);
    }

    /// <summary>
    /// This method creates the inverse of our transformation matrix.
    /// </summary>
    /// <returns>The inverse of our transformation matrix.</returns>
    private Matrix CreateInverseTransform()
    {
        return _transform.Invert();
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.
    /// </summary>
    /// <param name="surface">The surface to get the color for.</param>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public Color GetColorFor(Surface surface, Point point)
    {
        Point objectPoint = surface.WorldToSurface(point);
        Point patternPoint = InverseTransform * objectPoint;

        return GetColorFor(patternPoint);
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public abstract Color GetColorFor(Point point);

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public abstract bool Matches(Pigmentation other);
}
