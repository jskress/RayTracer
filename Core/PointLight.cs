using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a point light source in a rendered scene.
/// </summary>
public class PointLight : NamedThing
{
    /// <summary>
    /// This property notes the location of the light source.
    /// </summary>
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    public Point Location { get; set; } = Point.Zero;

    /// <summary>
    /// This property notes the color of the light.
    /// </summary>
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
    public Color Color { get; set; } = Colors.White;

    /// <summary>
    /// This method is used to determine the color for this light at a particular
    /// point in space, considering a surface normal at the point and the material.
    /// </summary>
    /// <param name="point">The point being illuminated.</param>
    /// <param name="eye">The eye vector.</param>
    /// <param name="normal">The surface normal vector.</param>
    /// <param name="surface">The surface being illuminated.</param>
    /// <param name="lightReaching">How much of this light arrives at the point: white, if it is
    /// in full view of the light, black, if something opaque stands in the way, and something in
    /// between if what stands in the way lets light through.</param>
    /// <returns>The resulting color.</returns>
    public Color ApplyPhong(
        Point point, Vector eye, Vector normal, Surface surface, Color lightReaching)
    {
        Material material = surface.Material ?? Material.Default;
        // The pigment's own colour is kept as well as the lit one, because a metallic highlight
        // tints by the surface's colour alone -- using the lit colour would fold the light in twice.
        Color pigmentColor = material.Pigment.GetColorFor(surface, point);
        Color color = pigmentColor * Color;
        Vector vector = (Location - point).Unit;

        // Ambient light stands in for light that has bounced around the scene rather than come
        // straight from this source, so it is the one term a shadow does not take away.
        Color ambientColor = color * material.Ambient;

        if (lightReaching.Matches(Colors.Black))
            return ambientColor;

        Color diffuseColor;
        Color specularColor;
        double lightDotNormal = vector.Dot(normal);

        if (lightDotNormal < 0)
            diffuseColor = specularColor = Colors.Black;
        else
        {
            // How much of the light the surface takes, before its colour is applied.  Brilliance
            // shapes how the falloff runs as the surface turns away, and the grain then takes fine
            // flecks back out of it -- both in that order, and both POV-Ray's.
            double intensity = material.Brilliance == 1
                ? lightDotNormal
                : Math.Pow(lightDotNormal, material.Brilliance);

            intensity = Math.Max(0, intensity * material.Diffuse - material.GrainAt(point));

            diffuseColor = color * intensity;

            Vector reflect = (-vector).Reflect(normal);
            double reflectDotEye = reflect.Dot(eye);

            if (reflectDotEye < 0)
                specularColor = Colors.Black;
            else
            {
                double factor = Math.Pow(reflectDotEye, material.Shininess);

                specularColor = Color * material.Specular * factor;

                // A metal's highlight takes the colour of the metal rather than of the light.  The
                // angle used is the light against the normal, which is the approximation POV-Ray
                // makes here too: the halfway vector would be more correct, but Phong exists
                // precisely to avoid computing it.
                if (material.Metallic != 0)
                    specularColor *= material.GetMetallicTint(pigmentColor, lightDotNormal);
            }
        }

        // Whatever the light lost on its way here is charged against both terms that depend on it
        // arriving.  Since it is a colour rather than a fraction, light that came through coloured
        // glass lights this surface in that colour.
        //
        // The two terms are charged separately rather than added and charged together, which looks
        // like the long way round and is done on purpose: it keeps the additions grouped exactly as
        // they were before any of this existed.  Floating point addition does not associate, so
        // regrouping them shifts the odd pixel by one level even when nothing has been lost -- and
        // a scene with nothing transparent in it should come out bit for bit unchanged.
        return ambientColor + diffuseColor * lightReaching + specularColor * lightReaching;
    }
}
