using RayTracer.Graphics;
using RayTracer.Shapes;

namespace RayTracer.Materials;

public class Metal : Material
{
    private readonly Color _color;
    private readonly double _fuzz;

    public Metal(Color color, double fuzz)
    {
        _color = color;
        _fuzz = fuzz < 1 ? fuzz : 1;
    }

    public override (Ray?, Color) Scatter(Ray ray, Intersection intersection)
    {
        Vector reflected = ray.Direction.Unit().Reflect(intersection.Normal);
        Vector offset = Vector.RandomUnitVector() * _fuzz;
        Ray scattered = new(intersection.Point, reflected + offset);

        return scattered.Direction.Dot(intersection.Normal) > 0
            ? (scattered, _color)
            : (null, _color);
    }
}
