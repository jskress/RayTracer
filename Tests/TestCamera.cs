using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Pixels;

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

        // The book's own case, with its numbers corrected.  The book prints the first two rows shorter
        // than one -- by 0.986, the sine of the angle between the way this camera looks and its "up" --
        // because its transform never makes the sideways axis of unit length, and a camera built that way
        // sees wider than its field of view.  See TestATiltedCameraSeesAsWideAsItsFieldOfView.
        expected = new Matrix(
        [
            -0.514496, 0.514496,  0.685994, -2.400980,
             0.778924, 0.614940,  0.122988, -2.869720,
            -0.358569, 0.597614, -0.717137,  0.000000,
             0.000000, 0.000000,  0.000000,  1.000000
        ]);

        Assert.IsTrue(expected.Matches(camera.Transform));
    }

    /// <summary>
    /// A tilted camera sees exactly as wide as its field of view says, however far it is tilted: the ray
    /// through the middle of the picture's right-hand edge, and through the middle of its top, stand as
    /// far from the way the camera looks as the field of view puts them.
    /// <para>
    /// **The book's view transform crosses the way the camera looks with "up" and leaves the result as
    /// it comes**, which is of unit length only when the two are square to each other.  Tilted, the
    /// camera's sideways and upward axes both came out shorter -- by the cosine of the tilt -- and every
    /// picture was taken that much wider than its field of view: 1.5% at ten degrees down, 41% at
    /// forty-five, and looking straight down, everything shrank to a point.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestATiltedCameraSeesAsWideAsItsFieldOfView()
    {
        RenderContext context = new () { Width = 201, Height = 101 };
        double field = 60.0.ToRadians();

        // How far apart, across the picture, the middles of two pixels a given number apart are seen.
        double Across(int pixels) => Math.Atan(pixels * 2 * Math.Tan(field / 2) / context.Width);

        foreach (double tilt in (double[]) [0, 10, 45, 80, -30])
        {
            Camera camera = new ()
            {
                Location = new Point(1, 2, 3),
                LookAt = new Point(1, 2 - Math.Sin(tilt.ToRadians()), 3 + Math.Cos(tilt.ToRadians())),
                FieldOfView = field
            };
            PerspectiveRayConverter converter = new (context, field) { Transform = camera.Transform };
            Vector middle = converter.GetRayForPixel(100, 50).Direction.Unit;
            Vector side = converter.GetRayForPixel(200, 50).Direction.Unit;
            Vector top = converter.GetRayForPixel(100, 0).Direction.Unit;

            Assert.AreEqual(Across(100), Math.Acos(middle.Dot(side)), 1e-9, $"tilted {tilt} degrees, the picture's side");
            Assert.AreEqual(Across(50), Math.Acos(middle.Dot(top)), 1e-9, $"tilted {tilt} degrees, the picture's top");
        }
    }

    /// <summary>
    /// A camera looking straight along its "up" has no way to say which way is sideways, and says so
    /// rather than taking an empty picture -- or, now that the axis is made of unit length, a picture of
    /// nothing but numbers that are not numbers.
    /// </summary>
    [TestMethod]
    public void TestACameraCannotLookAlongItsUp()
    {
        Camera camera = new () { Location = new Point(0, 40, 0), LookAt = Point.Zero };

        Exception thrown = Assert.ThrowsExactly<Exception>(() => camera.Transform);

        StringAssert.Contains(thrown.Message, "cannot look straight along its up direction");

        camera.Up = new Vector(0, 0, 1);

        Assert.IsTrue(new Vector(0, 0, -1).Matches(new Vector(0, -1, 0) * camera.Transform),
            "given another up, a camera looking straight down should look along its own -Z");
    }

    [TestMethod]
    public void TestRender()
    {
        Scene scene = TestScenes.DefaultScene();
        Camera camera = new ();
        RenderContext context = new RenderContext
        {
            Width = 11, Height = 11,
            AntiAliasing = new AliasingOption() 
        };
        Color expected = new (0.380661, 0.475826, 0.285496);

        Canvas canvas = camera.Render(context, scene);

        Assert.IsTrue(expected.Matches(canvas.GetPixel(5, 5)));
    }
}
