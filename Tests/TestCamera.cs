using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Scanners;

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

        expected = new Matrix(new []
        {
            -0.50709, 0.50709,  0.67612, -2.36643,
             0.76772, 0.60609,  0.12122, -2.82843,
            -0.35857, 0.59761, -0.71714,  0.00000,
             0.00000, 0.00000,  0.00000,  1.00000,
        });

        Assert.IsTrue(expected.Matches(camera.Transform));
    }

    [TestMethod]
    public void TestRender()
    {
        Scene scene = Scene.DefaultScene();
        Camera camera = new ();
        Canvas canvas = new (11, 11);
        Color expected = new (0.38066, 0.47583, 0.2855);

        camera.Render(scene, canvas, new SingleThreadScanner());

        Assert.IsTrue(expected.Matches(canvas.GetPixel(5, 5)));
    }
}
