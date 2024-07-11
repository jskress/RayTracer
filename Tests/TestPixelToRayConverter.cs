using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;

namespace Tests;

[TestClass]
public class TestPixelToRayConverter
{
    private const double PiOver2 = Math.PI / 2;
    private const double PiOver4 = Math.PI / 4;

    [TestMethod]
    public void TestConstruction()
    {
        RenderContext context = new () { Width = 160, Height = 120 };
        PixelToRayConverter mechanics = new PixelToRayConverter(context, PiOver2);

        Assert.AreSame(Matrix.Identity, mechanics.Transform);
    }

    [TestMethod]
    public void TestPixelSize()
    {
        RenderContext context = new () { Width = 200, Height = 125 };
        PixelToRayConverter mechanics = new (context, PiOver2);

        // Round to our expected value.
        double actual = Math.Round(mechanics.PixelSize * 100) / 100;

        Assert.AreEqual(0.01, actual);

        context = new RenderContext { Width = 125, Height = 200 };
        mechanics = new PixelToRayConverter(context, PiOver2);

        // Round to our expected value.
        actual = Math.Round(mechanics.PixelSize * 100) / 100;

        Assert.AreEqual(0.01, actual);
    }

    [TestMethod]
    public void TestGetRayFor()
    {
        RenderContext context = new () { Width = 201, Height = 101 };
        PixelToRayConverter mechanics = new (context, PiOver2);
        Ray ray = mechanics.GetRayForPixel(100, 50);
        Vector direction = new (0, 0, -1);

        Assert.IsTrue(Point.Zero.Matches(ray.Origin));
        Assert.IsTrue(direction.Matches(ray.Direction));

        ray = mechanics.GetRayForPixel(0, 0);
        direction = new Vector(0.665186, 0.332593, -0.668512);

        Assert.IsTrue(Point.Zero.Matches(ray.Origin));
        Assert.IsTrue(direction.Matches(ray.Direction));

        mechanics.Transform = Transforms.RotateAroundY(PiOver4, true) *
                              Transforms.Translate(0, -2, 5);

        double value = Math.Sqrt(2) / 2;
        Point origin = new (0, 2, -5);

        ray = mechanics.GetRayForPixel(100, 50);
        direction = new Vector(value, 0, -value);

        Assert.IsTrue(origin.Matches(ray.Origin));
        Assert.IsTrue(direction.Matches(ray.Direction));
    }
}
