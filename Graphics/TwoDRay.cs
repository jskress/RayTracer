using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This record represents a 2D ray.
/// </summary>
public class TwoDRay
{
    /// <summary>
    /// The origin of the ray.
    /// </summary>
    public TwoDPoint Origin {get; set;}

    /// <summary>
    /// The direction vector of the ray.
    /// </summary>
    public TwoDVector Direction {get; set;}

    /// <summary>
    /// This method returns a point at some distance along the ray.
    /// </summary>
    /// <param name="distance"></param>
    /// <returns></returns>
    public TwoDPoint At(double distance)
    {
        return Origin + Direction * distance;
    }

    /// <summary>
    /// This method is used to create a 3D ray from this one assuming we are in the X/Y
    /// plane.
    /// </summary>
    /// <returns>The 3D ray.</returns>
    public Ray FromXy()
    {
        return new Ray(Origin.FromXy(), Direction.FromXy());
    }

    /// <summary>
    /// This method is used to create a 3D ray from this one assuming we are in the X/Z
    /// plane.
    /// </summary>
    /// <returns>The 3D ray.</returns>
    public Ray FromXz()
    {
        return new Ray(Origin.FromXz(), Direction.FromXz());
    }

    /// <summary>
    /// This method is used to create a 2D ray from one in 3D by projecting it to the
    /// X/Y plane.
    /// </summary>
    /// <param name="ray">The ray to project.</param>
    /// <returns>The 2D projected ray.</returns>
    public static TwoDRay ProjectedToXy(Ray ray)
    {
        return new TwoDRay
        {
            Origin = TwoDPoint.ProjectedToXy(ray.Origin),
            Direction = TwoDVector.ProjectedToXy(ray.Direction)
        };
    }

    /// <summary>
    /// This method is used to create a 2D ray from one in 3D by projecting it to the
    /// X/Z plane.
    /// </summary>
    /// <param name="ray">The ray to project.</param>
    /// <returns>The 2D projected ray.</returns>
    public static TwoDRay ProjectedToXz(Ray ray)
    {
        return new TwoDRay
        {
            Origin = TwoDPoint.ProjectedToXz(ray.Origin),
            Direction = TwoDVector.ProjectedToXz(ray.Direction)
        };
    }
}
