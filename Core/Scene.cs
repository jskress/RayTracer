using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This class represents a world of things to render.
/// </summary>
public class Scene : NamedThing, IDisposable
{
    /// <summary>
    /// This list holds the collection of cameras in the scene.
    /// </summary>
    public List<Camera> Cameras { get; } = [];

    /// <summary>
    /// This list holds the collection of lights in the scene.
    /// </summary>
    public List<PointLight> Lights { get; } = [];

    /// <summary>
    /// This list holds the collection of surfaces (shapes) in the scene.
    /// </summary>
    public List<Surface> Surfaces { get; } = [];

    /// <summary>
    /// This property holds the pigment to use for a pixel when rays do not intersect with
    /// anything in the scene.
    /// </summary>
    public Pigment Background { get; set; } = new SolidPigment(Colors.Transparent);

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
        Color color = Background.GetTransformedColorFor(ray.Origin);

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
        Color surfaceColor = Lights.Aggregate(Colors.Black, (color, light) =>
        {
            bool isInShadow = IsInShadow(light, intersection.OverPoint);

            return color + light.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal,
                intersection.Surface, isInShadow);
        });
        Color reflectedColor = GetReflectionColor(intersection, remaining);
        Color refractedColor = GetRefractedColor(intersection, remaining);
        Material material = intersection.Surface.Material ?? Material.Default;
        Color refColor;

        if (material is {Reflective: > 0, Transparency: > 0})
        {
            double reflectance = intersection.Reflectance;

            refColor = reflectedColor * reflectance +
                       refractedColor * (1 - reflectance);
        }
        else
            refColor = reflectedColor + refractedColor;

        return surfaceColor + refColor;
    }

    /// <summary>
    /// This method returns whether the given point is in shadow with respect to the given
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
        List<Intersection> intersections = Intersect(ray)
            .Where(intersection => !intersection.Surface.NoShadow)
            .ToList();
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
        Material material = intersection.Surface.Material ?? Material.Default;
        double reflective = material.Reflective;

        if (remaining < 1 || reflective == 0)
            return Colors.Black;

        Ray reflectedRay = new (intersection.OverPoint, intersection.Reflect);
        Color color = GetColorFor(reflectedRay, remaining - 1) * reflective;

        // A metal colours what it mirrors, not just its highlight -- it is what makes a gold
        // surface throw back a gold scene rather than a plain one.  The angle here is the eye
        // against the normal, the true angle of incidence, rather than the approximation the
        // highlight has to settle for.
        if (material.Metallic != 0)
        {
            Color pigmentColor = material.Pigment.GetColorFor(intersection.Surface, intersection.Point);

            color *= material.GetMetallicTint(pigmentColor, intersection.Eye.Dot(intersection.Normal));
        }

        return color;
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
        Material material = intersection.Surface.Material ?? Material.Default;
        double transparency = material.Transparency;

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
        Color color = GetColorFor(refractedRay, remaining - 1) * transparency;
        double filter = material.Interior.Filter;

        // Transparency says how much light gets through; the filter says what colour it comes out.
        // Tinting toward the pigment's colour at this very point, rather than toward some single
        // colour for the whole surface, is what lets patterned glass filter as a pattern.  Like
        // the transparency it follows, this is charged once per surface crossed, so a solid tints
        // what passes through it twice -- going in, and coming back out.
        if (filter > 0)
        {
            Color pigmentColor = material.Pigment.GetColorFor(intersection.Surface, intersection.Point);

            color *= Colors.White + (pigmentColor - Colors.White) * filter;
        }

        return color;
    }

    /// <summary>
    /// This method is used to properly clean up our resources.
    /// </summary>
    public void Dispose()
    {
        CleanUpDisposables(Surfaces);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This method is used to chase the given list of surfaces, disposing of the ones that
    /// need it.  It will recurse as necessary.
    /// </summary>
    /// <param name="surfaces">The list of surfaces to chase.</param>
    private static void CleanUpDisposables(List<Surface> surfaces)
    {
        foreach (Surface surface in surfaces)
        {
            if (surface is IDisposable disposable)
                disposable.Dispose();

            switch (surface)
            {
                case Group group:
                {
                    CleanUpDisposables(group.Surfaces);
                    break;
                }
                case CsgSurface csgSurface:
                    CleanUpDisposables([csgSurface.Left, csgSurface.Right]);
                    break;
            }
        }
    }
}
