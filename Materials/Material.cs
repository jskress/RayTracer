using RayTracer.Graphics;
using RayTracer.Shapes;

namespace RayTracer.Materials;

public abstract class Material
{
    public abstract (Ray?, Color) Scatter(Ray ray, Intersection intersection);
}
