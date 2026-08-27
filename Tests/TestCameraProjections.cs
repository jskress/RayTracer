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

    /// <summary>
    /// **Every projection must say how fast its rays spread, and none did but two.**  A ray carries
    /// how much of the world one pixel covers so that a pattern can be filtered for that patch
    /// rather than sampled at a point; a ray that says nothing is point-sampled, and a brick wall
    /// seen through such a camera beats into arcs the way it used to through all of them.
    /// <para>
    /// This is written over every projection there is, found by asking the assembly rather than by
    /// listing them, so that a projection added later cannot quietly join without one.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestEveryProjectionSaysHowFastItsRaysSpread()
    {
        RenderContext context = Context();

        foreach (PixelToRayConverter converter in EveryProjection(context))
        {
            Ray ray = converter.GetRayForPixel(Middle, Middle);
            string name = converter.GetType().Name;

            // Orthographic rays run parallel, so they never widen -- what they carry instead is the
            // width they have all along.
            Assert.IsTrue(ray.Spread > 0 || ray.BaseRadius > 0,
                $"{name} makes rays that cover nothing, so every pattern it sees is point sampled");
            Assert.IsTrue(ray.RadiusAt(10) > 0, $"{name} covers nothing ten units out");
        }
    }

    /// <summary>
    /// **The spread must match the sky a pixel really covers**, and this measures that rather than
    /// trusting the arithmetic: it takes the ray for a pixel and the rays for its neighbours to the
    /// right and below, and asks what angle actually lies between them.
    /// <para>
    /// The rule is one-sided, deliberately.  A spread narrower than the truth leaves detail
    /// unfiltered and the aliasing it was built to stop comes back, so falling short is a fault.
    /// Running over merely blurs a little, which is the same trade the footprint itself makes in
    /// taking the longer of its two edges, so it is allowed -- within reason.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheSpreadMatchesWhatAPixelActuallyCovers()
    {
        RenderContext context = Context();

        foreach (PixelToRayConverter converter in EveryProjection(context))
        {
            string name = converter.GetType().Name;

            // An orthographic camera's rays run parallel; there is no angle between neighbours to
            // measure, and its width is carried as a base radius instead.
            if (converter is OrthographicRayConverter)
                continue;

            int measured = 0;

            for (int y = 10; y < Size - 10; y += 17)
            {
                for (int x = 10; x < Size - 10; x += 17)
                {
                    Ray here = converter.GetRayForPixel(x, y);
                    double across = AngleBetween(here, converter.GetRayForPixel(x + 1, y));
                    double down = AngleBetween(here, converter.GetRayForPixel(x, y + 1));
                    double covered = Math.Max(across, down);

                    // A fisheye's frame has corners outside its circle, and every pixel out there is
                    // sent the same way to fetch the background.  Neighbours that point identically
                    // have no angle between them to measure against -- and right at the rim, where
                    // one neighbour is inside the circle and the next is not, the "angle between
                    // them" is the whole width of that seam rather than anything a pixel covers.
                    //
                    // Half a radian is the cutoff: the widest of these projections turns a ray by
                    // about 0.03 of one per pixel at this size, so nothing near half can be a
                    // footprint, and nothing a footprint should be is thrown away by it.
                    if (covered == 0 || covered > 0.5)
                        continue;

                    measured++;

                    Assert.IsTrue(here.Spread >= covered * 0.999,
                        $"{name} at pixel ({x}, {y}) reports a spread of {here.Spread:G6} where the " +
                        $"next pixel along is {covered:G6} away, so it is filtering less than the " +
                        "pixel actually covers and the aliasing comes back");
                    Assert.IsTrue(here.Spread <= covered * 4,
                        $"{name} at pixel ({x}, {y}) reports a spread of {here.Spread:G6} against a " +
                        $"real {covered:G6}, which blurs far more than the pixel warrants");
                }
            }

            Assert.IsTrue(measured > 30,
                $"only {measured} pixels of {name} had neighbours to measure against, which is too " +
                "few to have tested it");
        }
    }

    /// <summary>
    /// This method returns the angle between two rays' directions.
    /// </summary>
    private static double AngleBetween(Ray first, Ray second)
    {
        return Math.Acos(Math.Clamp(first.Direction.Dot(second.Direction), -1, 1));
    }

    /// <summary>
    /// This method returns one of every projection there is, found by asking the assembly for every
    /// converter rather than by naming them, so that one added later is covered without anyone
    /// remembering to add it here.
    /// </summary>
    private static IEnumerable<PixelToRayConverter> EveryProjection(RenderContext context)
    {
        List<Type> kinds = typeof(PixelToRayConverter).Assembly
            .GetTypes()
            .Where(type => type.IsSubclassOf(typeof(PixelToRayConverter)) && !type.IsAbstract)
            .OrderBy(type => type.Name)
            .ToList();

        Assert.IsTrue(kinds.Count >= 6,
            $"only {kinds.Count} projections were found, so this test is not looking where it thinks");

        foreach (Type kind in kinds)
        {
            yield return kind == typeof(SphericalRayConverter)
                ? new SphericalRayConverter(context, Matrix.Identity, Pinhole())
                : (PixelToRayConverter) Activator.CreateInstance(
                    kind, context, Math.PI / 2, Matrix.Identity, Pinhole());
        }
    }
}
