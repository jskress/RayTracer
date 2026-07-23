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
    public List<Light> Lights { get; } = [];

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
            Color lightReaching = GetLightReaching(light, intersection.OverPoint);

            return color + light.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal,
                intersection.Surface, lightReaching);
        });
        Color reflectedColor = GetReflectionColor(intersection, remaining);
        Color refractedColor = GetRefractedColor(intersection, remaining);
        Material material = intersection.Surface.Material ?? Material.Default;
        Color refColor;

        // What a surface lets past, it cannot also show.  The pigment may say so colour by colour,
        // which is what lets one pattern be a window in some places and a wall in others, so the
        // question is asked at this point rather than of the material as a whole.
        double transparency = material.PigmentMayTransmit
            ? material.TransparencyFor(
                material.Pigment.GetColorFor(intersection.Surface, intersection.Point))
            : material.Transparency;

        if (material.Reflective > 0 && (material.Transparency > 0 || material.PigmentMayTransmit))
        {
            double reflectance = intersection.Reflectance;

            refColor = reflectedColor * reflectance +
                       refractedColor * (1 - reflectance);
        }
        else
            refColor = reflectedColor + refractedColor;

        // A surface shows only as much of itself as it stops.  Perfectly clear glass therefore
        // contributes nothing of its own and is seen entirely through, which is what makes a
        // transmitting pigment a window rather than merely a dimmer wall.
        Color color = surfaceColor * (1 - transparency) + refColor;

        // If this hit is on the far side of a surface, the ray reached it by travelling through
        // whatever the surface is made of, and a substance that fades light charges for the trip.
        // Everything gathered here made that crossing -- what came from beyond, and what the inner
        // wall itself reflected -- so the fade is applied to all of it at once.  The distance is
        // the ray's own, which is exactly the path through the substance because the ray began
        // where the light entered it.
        if (intersection.Inside)
            color *= material.Interior.GetFadeOver(intersection.Distance);

        return color;
    }

    /// <summary>
    /// This method returns how much of the given light reaches the given point: white, if the
    /// light is in full view of it, black, if something opaque stands in the way, and something
    /// in between if what stands in the way lets light through.
    /// <para>
    /// Every surface between the point and the light is charged, rather than only the nearest one,
    /// since light has to survive all of them to arrive.  A surface that lets light through also
    /// gets to colour it, which is what casts a green shadow under green glass.  What this
    /// deliberately does not do is bend the shadow ray on its way through: light really is
    /// refracted into the bright and dark bands of a caustic, but finding them means tracing
    /// forward from the light rather than backward from the surface, which is a different
    /// algorithm entirely -- POV-Ray needs its photon mapping for the same reason.
    /// </para>
    /// </summary>
    /// <param name="light">The light source in question.</param>
    /// <param name="point">The point to test.</param>
    /// <returns>The fraction of the light's colour that arrives at the point.</returns>
    public Color GetLightReaching(Light light, Point point)
    {
        (Vector direction, double distance) = light.TowardFrom(point);
        Ray ray = new (point, direction);
        Color reaching = Colors.White;

        foreach (Intersection intersection in Intersect(ray))
        {
            // Only things that actually stand between the point and the light can shade it;
            // anything behind the point, or beyond the light, is irrelevant.  Zero counts as being
            // in the way, which is what Hit() has always taken it to mean -- rays start at a point
            // nudged off the surface precisely so that a grazing hit at no distance at all is a
            // real occluder rather than the surface shading itself.
            if (intersection.Distance < 0 || intersection.Distance >= distance ||
                intersection.Surface.NoShadow)
                continue;

            Material material = intersection.Surface.Material ?? Material.Default;
            Interior interior = material.Interior;

            // Shadow rays never have their intersections prepared, since that work would be wasted
            // on all but the nearest hit, so what is needed of the crossing is worked out here --
            // and only when something actually asks for it.  The pigment is sampled where the light
            // crossed, so that patterned glass shadows as a pattern, and once sampled it serves for
            // both how much light gets past and what colour it comes out.
            bool needsPigment = interior.Filter > 0 || material.PigmentMayTransmit;
            Point where = needsPigment || interior.Refracts
                ? ray.At(intersection.Distance)
                : null;
            Color surfaceColor = needsPigment
                ? material.Pigment.GetColorFor(intersection.Surface, where)
                : null;
            double transparency = surfaceColor is null
                ? material.Transparency
                : material.TransparencyFor(surfaceColor);

            if (transparency <= 0)
                return Colors.Black;

            reaching *= transparency;

            if (interior.Filter > 0 || interior.Refracts)
            {
                if (interior.Filter > 0)
                    reaching *= interior.GetFilterTint(surfaceColor);

                // Some of the light never gets in at all, being mirrored off the surface instead,
                // and how much depends on how glancing its approach is.  This is what keeps clear
                // glass from being invisible to its own shadow: struck head-on it lets nearly
                // everything through, but around the rim, where the light only grazes it, most is
                // turned away.  A glass ball therefore casts a faint shadow gathered into a dark
                // ring, which is close to what one really does -- what is still missing is the
                // bright spot in the middle, where refraction gathers the light it bent aside.
                // That is a caustic, and finding it means tracing forward from the light.
                if (interior.Refracts)
                {
                    Vector normal = intersection.Surface.NormaAt(where, intersection);

                    reaching *= 1 - interior.GetReflectanceAt(ray.Direction.Dot(normal));
                }
            }
        }

        return reaching;
    }

    /// <summary>
    /// This method returns whether the given point is in shadow with respect to the given
    /// light source, meaning that no light from it arrives at all.
    /// </summary>
    /// <param name="light">The light source in question.</param>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the point is in shadow, or <c>false</c>, if not.</returns>
    public bool IsInShadow(Light light, Point point)
    {
        return GetLightReaching(light, point).Matches(Colors.Black);
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

        // The pigment may say, colour by colour, how much light gets past it, so where it might,
        // it is sampled and has its say.  Sampled once here and reused for the filter below, since
        // both want the surface's colour at the very same point.
        Color pigmentColor = material.PigmentMayTransmit || material.Interior.Filter > 0
            ? material.Pigment.GetColorFor(intersection.Surface, intersection.Point)
            : null;
        double transparency = pigmentColor is null
            ? material.Transparency
            : material.TransparencyFor(pigmentColor);

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

        // Transparency says how much light gets through; the filter says what colour it comes out.
        // Tinting toward the pigment's colour at this very point, rather than toward some single
        // colour for the whole surface, is what lets patterned glass filter as a pattern.  Like
        // the transparency it follows, this is charged once per surface crossed, so a solid tints
        // what passes through it twice -- going in, and coming back out.
        if (material.Interior.Filter > 0)
            color *= material.Interior.GetFilterTint(pigmentColor);

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
