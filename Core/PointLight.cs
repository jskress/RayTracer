using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a point light source in a rendered scene.
/// </summary>
public class PointLight
{
    /// <summary>
    /// This property notes the location of the light source.
    /// </summary>
    public Point Location { get; set; } = Point.Zero;

    /// <summary>
    /// This property notes the color of the light.
    /// </summary>
    public Color Color { get; set; } = Colors.White;

    /// <summary>
    /// This method is used to determine the color for this light at a particular
    /// point in space, considering a surface normal at the point and the material.
    /// </summary>
    /// <param name="point">The point being illuminated.</param>
    /// <param name="eye">The eye vector.</param>
    /// <param name="normal">The surface normal vector.</param>
    /// <param name="surface"></param>
    /// <param name="inShadow"></param>
    /// <returns>The resulting color.</returns>
    public Color ApplyPhong(Point point, Vector eye, Vector normal, Surface surface, bool inShadow)
    {
        Material material = surface.Material;
        Color color = material.ColorSource.GetColorFor(surface, point) * Color;
        Vector vector = (Location - point).Unit;
        Color ambientColor = color * material.Ambient;

        if (inShadow)
            return ambientColor;

        Color diffuseColor;
        Color specularColor;
        double lightDotNormal = vector.Dot(normal);

        if (lightDotNormal < 0)
            diffuseColor = specularColor = Colors.Black;
        else
        {
            diffuseColor = color * material.Diffuse * lightDotNormal;

            Vector reflect = (-vector).Reflect(normal);
            double reflectDotEye = reflect.Dot(eye);

            if (reflectDotEye < 0)
                specularColor = Colors.Black;
            else
            {
                double factor = Math.Pow(reflectDotEye, material.Shininess);

                specularColor = Color * material.Specular * factor;
            }
        }

        return ambientColor + diffuseColor + specularColor;
    }
}
