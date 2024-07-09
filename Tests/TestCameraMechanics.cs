using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestCameraMechanics
{
    private const double PiOver2 = Math.PI / 2;
    private const double PiOver4 = Math.PI / 4;

    [TestMethod]
    public void TestConstruction()
    {
        Canvas canvas = new (160, 120);
        CameraMechanics mechanics = new CameraMechanics(canvas, PiOver2);

        Assert.AreEqual(160, mechanics.Width);
        Assert.AreEqual(120, mechanics.Height);
        Assert.AreEqual(PiOver2, mechanics.FieldOfView);
        Assert.AreSame(Matrix.Identity, mechanics.Transform);
    }

    [TestMethod]
    public void TestPixelSize()
    {
        Canvas canvas = new (200, 125);
        CameraMechanics mechanics = new (canvas, PiOver2);

        // Round to our expected value.
        double actual = Math.Round(mechanics.PixelSize * 100) / 100;

        Assert.AreEqual(0.01, actual);

        canvas = new Canvas(125, 200);
        mechanics = new CameraMechanics(canvas, PiOver2);

        // Round to our expected value.
        actual = Math.Round(mechanics.PixelSize * 100) / 100;

        Assert.AreEqual(0.01, actual);
    }

    [TestMethod]
    public void TestGetRayFor()
    {
        Canvas canvas = new (201, 101);
        CameraMechanics mechanics = new (canvas, PiOver2);
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
