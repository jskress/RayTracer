using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestCamera
{
    [TestMethod]
    public void TestViewTransform()
    {
        Camera camera = new ()
        {
            Location = Point.Zero,
            LookAt = new Point(0, 0, -1)
        };

        Assert.IsTrue(Matrix.Identity.Matches(camera.Transform));

        camera = new Camera
        {
            Location = Point.Zero,
            LookAt = new Point(0, 0, 1)
        };

        Matrix expected = Transforms.Scale(-1, 1, -1);

        Assert.IsTrue(expected.Matches(camera.Transform));

        camera = new Camera
        {
            Location = new Point(0, 0, 8),
            LookAt = new Point(0, 0, 0)
        };

        expected = Transforms.Translate(0, 0, -8);

        Assert.IsTrue(expected.Matches(camera.Transform));

        camera = new Camera
        {
            Location = new Point(1, 3, 2),
            LookAt = new Point(4, -2, 8),
            Up = new Vector(1, 1, 0)
        };

        expected = new Matrix(
        [
            -0.507093, 0.507093,  0.676123, -2.366432,
             0.767716, 0.606092,  0.121218, -2.828427,
            -0.358569, 0.597614, -0.717137,  0.000000,
             0.000000, 0.000000,  0.000000,  1.000000
        ]);

        Assert.IsTrue(expected.Matches(camera.Transform));
    }

    [TestMethod]
    public void TestRender()
    {
        Scene scene = Scene.DefaultScene();
        Camera camera = new ();
        RenderContext context = new RenderContext { Width = 11, Height = 11 };
        Color expected = new (0.380661, 0.475826, 0.285496);

        Canvas canvas = camera.Render(context, scene);

        Assert.IsTrue(expected.Matches(canvas.GetPixel(5, 5)));
    }
}
