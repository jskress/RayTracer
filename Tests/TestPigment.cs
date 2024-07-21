using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

public class TestPigment : Pigment
{
    public Color? Color { get; private set; }

    public override Color GetColorFor(Point point)
    {
        return Color = new Color(point.X, point.Y, point.Z);
    }

    public override bool Matches(Pigment other)
    {
        return false;
    }
}
