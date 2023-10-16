using RayTracer.Graphics;
using RayTracer.Shapes;

namespace RayTracer.Materials;

/// <summary>
/// This class implements a matte material based around a color.
/// </summary>
public class Matte : Material
{
    private readonly Color _color;

    public Matte(Color color)
    {
        _color = color;
    }

    public override (Ray?, Color) Scatter(Ray ray, Intersection intersection)
    {
        Vector scatterDirection = intersection.Normal + Vector.RandomUnitVector();

        if (scatterDirection.NearZero())
            scatterDirection = intersection.Normal;

        return (new Ray(intersection.Point, scatterDirection), _color);
    }
}
