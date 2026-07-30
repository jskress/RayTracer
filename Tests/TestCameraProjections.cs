using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;

namespace Tests;

/// <summary>
/// These tests cover the camera projections other than perspective (which
/// <see cref="TestPerspectiveRayConverter"/> covers).  Each is checked by the rays it makes at a
/// few telling pixels: the middle, which every projection sends straight ahead, and the edges,
/// where each bends its own way.  The image is 201 square, so the middle pixel sits exactly at the
/// center and looks straight down the -Z axis.
/// </summary>
[TestClass]
public class TestCameraProjections
{
    private const int Size = 201;
    private const int Middle = 100;
    private static readonly Vector Forward = new (0, 0, -1);

    private static RenderContext Context() => new () { Width = Size, Height = Size };

    private static CameraSampler Pinhole() => new (0, 1);

    [TestMethod]
    public void TestEveryProjectionSendsTheMiddlePixelStraightAhead()
    {
        RenderContext context = Context();

        foreach (PixelToRayConverter converter in new PixelToRayConverter[]
        {
            new OrthographicRayConverter(context, Math.PI / 2, Matrix.Identity, Pinhole()),
            new FisheyeRayConverter(context, Math.PI, Matrix.Identity, Pinhole()),
            new UltraWideRayConverter(context, Math.PI, Matrix.Identity, Pinhole()),
            new PanoramicRayConverter(context, Math.PI, Matrix.Identity, Pinhole()),
            new SphericalRayConverter(context, Matrix.Identity, Pinhole())
        })
        {
            Ray ray = converter.GetRayForPixel(Middle, Middle);

            Assert.IsTrue(Forward.Matches(ray.Direction),
                $"{converter.GetType().Name} bent the middle pixel to {ray.Direction}");
        }
    }

    [TestMethod]
    public void TestOrthographicRaysAreAllParallelButLeaveDifferentPlaces()
    {
        OrthographicRayConverter converter = new (
            Context(), Math.PI / 2, Matrix.Identity, Pinhole());

        Ray middle = converter.GetRayForPixel(Middle, Middle);
        Ray corner = converter.GetRayForPixel(0, 0);

        // Parallel: both point straight ahead.
        Assert.IsTrue(Forward.Matches(middle.Direction));
        Assert.IsTrue(Forward.Matches(corner.Direction));

        // But they leave from different places on the image plane.
        Assert.IsFalse(middle.Origin.Matches(corner.Origin),
            "an orthographic camera's rays should leave from spread-out points");
    }

    [TestMethod]
    public void TestFisheyeTipsUpwardAndLeavesTheCornersToTheBackground()
    {
        FisheyeRayConverter converter = new (
            Context(), Math.PI, Matrix.Identity, Pinhole());

        // A pixel above the middle tips the ray upward.
        Ray above = converter.GetRayForPixel(Middle, Middle - 60);

        Assert.IsTrue(above.Direction.Y > 0, "a pixel above the middle should tip the ray up");
        Assert.IsTrue(above.Direction.Z < 0, "and it should still look ahead, not behind");

        // A corner sits outside the circle, so its ray is sent away from the scene (behind).
        Ray corner = converter.GetRayForPixel(0, 0);

        Assert.IsTrue(corner.Direction.Z > 0, "a corner outside the circle should look behind");
    }

    [TestMethod]
    public void TestUltraWideBendsSidewaysTowardTheEdges()
    {
        UltraWideRayConverter converter = new (
            Context(), Math.PI, Matrix.Identity, Pinhole());

        // The left of the image is the camera's +X, so a pixel there tips the ray that way.  At a
        // half-circle across, the very edge looks all the way to the side.
        Ray left = converter.GetRayForPixel(0, Middle);

        Assert.IsTrue(left.Direction.X > 0, "a pixel on the left should tip the ray to the left");
        Assert.IsTrue(Math.Abs(left.Direction.Y) < 1e-9, "a pixel level with the middle stays level");
    }

    [TestMethod]
    public void TestPanoramicKeepsAColumnInOneVerticalPlane()
    {
        PanoramicRayConverter converter = new (
            Context(), Math.PI, Matrix.Identity, Pinhole());

        // The mark of the cylinder is that uprights stay upright: two pixels in the same column
        // share a longitude, so their rays lie in the same vertical plane -- the same ratio of the
        // sideways part to the forward part.
        Ray high = converter.GetRayForPixel(40, 40);
        Ray low = converter.GetRayForPixel(40, 160);

        Assert.AreEqual(
            high.Direction.X / high.Direction.Z,
            low.Direction.X / low.Direction.Z, 1e-9,
            "a panoramic camera should keep a column in one vertical plane");

        // And one of them is off the middle horizontally, so the plane is not straight ahead.
        Ray left = converter.GetRayForPixel(0, Middle);

        Assert.IsTrue(left.Direction.X > 0, "a pixel on the left should sweep the ray to the left");
    }

    [TestMethod]
    public void TestSphericalLooksAllTheWayAround()
    {
        SphericalRayConverter converter = new (Context(), Matrix.Identity, Pinhole());

        // The left and right edges are a half-turn from ahead, so they look behind.
        Ray left = converter.GetRayForPixel(0, Middle);

        Assert.IsTrue(left.Direction.Z > 0, "the edge of a spherical view should look behind");

        // The top edge looks nearly straight up, the bottom nearly straight down.
        Ray top = converter.GetRayForPixel(Middle, 0);
        Ray bottom = converter.GetRayForPixel(Middle, Size - 1);

        Assert.IsTrue(top.Direction.Y > 0.98, "the top of a spherical view should look up");
        Assert.IsTrue(bottom.Direction.Y < -0.98, "the bottom should look down");
    }

    [TestMethod]
    public void TestACurvedProjectionIgnoresTheAperture()
    {
        // The curved projections have no lens, so an aperture buys them nothing: every sample of a
        // pixel leaves the same place, going the same way, however many the sampler holds.
        FisheyeRayConverter converter = new (
            Context(), Math.PI, Matrix.Identity, new CameraSampler(0.5, 4, 0, 16));

        Ray first = converter.GetRayForPixel(70, 90, sampleIndex: 0);
        Ray other = converter.GetRayForPixel(70, 90, sampleIndex: 7);

        Assert.IsTrue(first.Origin.Matches(other.Origin), "an aperture should not spread the origin");
        Assert.IsTrue(first.Direction.Matches(other.Direction), "nor bend the ray");
    }
}
