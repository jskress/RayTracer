using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigmentation;

namespace RayTracer.Core;

/// <summary>
/// This class represents a world of things to render.
/// </summary>
public class Scene : NamedThing
{
    /// <summary>
    /// This list holds the collection of cameras in the scene.
    /// </summary>
    public List<Camera> Cameras { get; set; } = [];

    /// <summary>
    /// This list holds the collection of lights in the scene.
    /// </summary>
    public List<PointLight> Lights { get; set; } = [];

    /// <summary>
    /// This list holds the collection of surfaces (shapes) in the scene.
    /// </summary>
    public List<Surface> Surfaces { get; set; } = [];

    /// <summary>
    /// This property holds the color to use for a pixel when rays do not intersect with
    /// anything in the scene.
    /// </summary>
    public Color BackgroundColor { get; set; } = Colors.Transparent;

    /// <summary>
    /// This method determines the color for the given ray.
    /// </summary>
    /// <param name="ray">The ray to determine the color for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The color for the ray.</returns>
    public Color GetColorFor(Ray ray, int remaining = 4)
    {
        List<Intersection> hits = Intersect(ray);
        Intersection hit = hits.Hit();
        Color color = BackgroundColor;

        if (hit != null)
        {
            hit.PrepareUsing(ray, hits);

            color = GetHitColor(hit, remaining);
        }

        return color;
    }

    /// <summary>
    /// This method finds the point of intersection, if any, of the given ray with the
    /// things in the world.
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    public List<Intersection> Intersect(Ray ray)
    {
        List<Intersection> intersections = [];

        foreach (Surface surface in Surfaces)
            surface.Intersect(ray, intersections);

        intersections.Sort();

        return intersections;
    }

    /// <summary>
    /// This method is used to determine the color for the given intersection point.
    /// </summary>
    /// <param name="intersection">The intersection point to derive the color for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The color to use.</returns>
    public Color GetHitColor(Intersection intersection, int remaining)
    {
        return Lights.Aggregate(Colors.Black, (color, light) =>
        {
            bool isInShadow = IsInShadow(light, intersection.OverPoint);
            Color surfaceColor = light.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal,
                intersection.Surface, isInShadow);
            Color reflectedColor = GetReflectionColor(intersection, remaining);
            Color refractedColor = GetRefractedColor(intersection, remaining);
            Material material = intersection.Surface.Material;
            Color refColor;

            if (material is {Reflective: > 0, Transparency: > 0})
            {
                double reflectance = intersection.Reflectance;

                refColor = reflectedColor * reflectance +
                           refractedColor * (1 - reflectance);
            }
            else
                refColor = reflectedColor + refractedColor;

            return surfaceColor + refColor + color;
        });
    }

    /// <summary>
    /// This method returns whether the given point is in shadow in regards to the given
    /// light source.
    /// </summary>
    /// <param name="light">The light source in question.</param>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the point is in shadow, or <c>false</c>, if not.</returns>
    public bool IsInShadow(PointLight light, Point point)
    {
        Vector vector = light.Location - point;
        double distance = vector.Magnitude;
        Ray ray = new (point, vector.Unit);
        List<Intersection> intersections = Intersect(ray);
        Intersection hit = intersections.Hit();

        return hit != null && hit.Distance < distance;
    }

    /// <summary>
    /// This method is used to determine the color of the reflected ray from the given
    /// intersection.
    /// </summary>
    /// <param name="intersection">The intersection to start with.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The reflected color.</returns>
    public Color GetReflectionColor(Intersection intersection, int remaining)
    {
        double reflective = intersection.Surface.Material.Reflective;

        if (remaining < 1 || reflective == 0)
            return Colors.Black;

        Ray reflectedRay = new (intersection.OverPoint, intersection.Reflect);

        return GetColorFor(reflectedRay, remaining - 1) * reflective;
    }

    /// <summary>
    /// This method is used to determine the color of the refracted ray from the given
    /// intersection.
    /// </summary>
    /// <param name="intersection">The intersection to start with.</param>
    /// <param name="remaining">The remaining number of refraction recursions allowed.</param>
    /// <returns>The reflected color.</returns>
    public Color GetRefractedColor(Intersection intersection, int remaining)
    {
        double transparency = intersection.Surface.Material.Transparency;

        if (remaining < 1 || transparency == 0)
            return Colors.Black;

        double ratio = intersection.N1 / intersection.N2;
        double cosI = intersection.Eye.Dot(intersection.Normal);
        double sin2T = ratio * ratio * (1 - cosI * cosI);

        if (sin2T > 1)
            return Colors.Black;

        double cosT = Math.Sqrt(1 - sin2T);
        Vector direction = intersection.Normal * (ratio * cosI - cosT) -
                           intersection.Eye * ratio;
        Point point = intersection.Inside ? intersection.OverPoint : intersection.UnderPoint;

        Ray refractedRay = new(point, direction);
        Color color = GetColorFor(refractedRay, remaining - 1);

        return color * transparency;
    }
}
