using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

public class TestPigment : Pigment
{
    public override Color GetColorFor(Point point)
    {
        return new Color(point.X, point.Y, point.Z);
    }

    public override bool Matches(Pigment other)
    {
        return false;
    }
}
