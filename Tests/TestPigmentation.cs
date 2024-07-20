using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Pigmentation;

namespace Tests;

public class TestPigmentation : Pigmentation
{
    public Color? Color { get; private set; }

    public override Color GetColorFor(Point point)
    {
        return Color = new Color(point.X, point.Y, point.Z);
    }

    public override bool Matches(Pigmentation other)
    {
        return false;
    }
}
