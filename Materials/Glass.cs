using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Shapes;

namespace RayTracer.Materials;

/// <summary>
/// Index of refraction:
///
/// air: 1.0
/// glass: 1.3 - 1.7
/// diamond: 2.4
/// </summary>
public class Glass : Material
{
    private readonly double _indexOfRefraction;
    private readonly double _indexOfRefractionInverse;

    public Glass(double indexOfRefraction)
    {
        _indexOfRefraction = indexOfRefraction;
        _indexOfRefractionInverse = 1.0 / indexOfRefraction;
    }

    public override (Ray?, Color) Scatter(Ray ray, Intersection intersection)
    {
        double refractionRatio = intersection.FromOutside
            ? _indexOfRefractionInverse
            : _indexOfRefraction;
        Vector unit = ray.Direction.Unit();
        double cosTheta = unit.CosTheta(intersection.Normal);
        double sinTheta = Math.Sqrt(1.0 - cosTheta * cosTheta);
        bool cannotRefract = refractionRatio * sinTheta > 1.0;
        Vector direction = cannotRefract || Reflectance(cosTheta, refractionRatio) > DoubleExtensions.RandomDouble()
            ? unit.Reflect(intersection.Normal)
            : unit.Refract(intersection.Normal, refractionRatio);

        return (new Ray(intersection.Point, direction), Color.White);
    }

    private static double Reflectance(double cosine, double ratio)
    {
        double r0 = (1 - ratio) / (1 + ratio);

        r0 *= r0;

        return r0 + (1 - r0) * Math.Pow(1 - cosine, 5);
    }
}
