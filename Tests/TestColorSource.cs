using RayTracer.Basics;
using RayTracer.ColorSources;
using RayTracer.Graphics;

namespace Tests;

public class TestColorSource : ColorSource
{
    public Color? Color { get; private set; }

    public override Color GetColorFor(Point point)
    {
        return Color = new Color(point.X, point.Y, point.Z);
    }

    public override bool Matches(ColorSource other)
    {
        return false;
    }
}
