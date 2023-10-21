using RayTracer.Basics;
using RayTracer.ColorSources;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a world of things to render.
/// </summary>
public class Scene
{
    /// <summary>
    /// This method generates a default scene with one light and two spheres.
    /// </summary>
    /// <returns>A default scene.</returns>
    public static Scene DefaultScene()
    {
        PointLight pointLight = new()
        {
            Location = new Point(-10, 10, -10)
        };
        Sphere outer = new ()
        {
            Material = new Material
            {
                ColorSource = new ConstantColorSource(new Color(0.8, 1.0, 0.6)),
                Diffuse = 0.7,
                Specular = 0.2
            }
        };
        Sphere inner = new ()
        {
            Transform = Transforms.Scale(0.5)
        };
        Scene scene = new ();

        scene.Lights.Add(pointLight);
        scene.Surfaces.Add(outer);
        scene.Surfaces.Add(inner);

        return scene;
    }

    /// <summary>
    /// This list holds the collection of lights in the scene.
    /// </summary>
    public List<PointLight> Lights { get; set; } = new ();

    /// <summary>
    /// This list holds the collection of surfaces (shapes) in the scene.
    /// </summary>
    public List<Surface> Surfaces { get; set; } = new ();

    /// <summary>
    /// This property holds the color to use for a pixel when rays do not intersect with
    /// anything in the scene.
    /// </summary>
    public Color BackgroundColor { get; set; } = Color.Transparent;

    /// <summary>
    /// This method determines the color for the given ray.
    /// </summary>
    /// <param name="ray">The ray to determine the color for.</param>
    /// <returns>The color for the ray.</returns>
    public Color GetColorFor(Ray ray)
    {
        List<Intersection> hits = Intersect(ray);
        Intersection? hit = hits.Hit();
        Color color = BackgroundColor;

        if (hit != null)
        {
            hit.PrepareUsing(ray);

            color = GetHitColor(hit);
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
        List<Intersection> intersections = new ();

        foreach (Surface surface in Surfaces)
            surface.Intersect(ray, intersections);

        intersections.Sort();

        return intersections;
    }

    /// <summary>
    /// This method is used to determine the color for the given intersection point.
    /// </summary>
    /// <param name="intersection">The intersection point to derive the color for.</param>
    /// <returns>The color to use.</returns>
    public Color GetHitColor(Intersection intersection)
    {
        return Lights.Aggregate(Color.Black, (color, light) =>
            light.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal,
                intersection.Surface, IsInShadow(light, intersection.OverPoint)) + color);
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
        Intersection? hit = intersections.Hit();

        return hit != null && hit.Distance < distance;
    }
}
