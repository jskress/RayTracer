using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;

namespace Tests;

/// <summary>
/// These tests cover the camera's lens: where across it rays are fired from, that they all still
/// aim at the same spot on the focal plane, and that a camera which asked for no blur is left on
/// the pinhole path it was always on.  What the blur looks like once rendered -- and that a scene
/// can ask for it -- is covered in <see cref="TestCameraClauses"/>.
/// </summary>
[TestClass]
public class TestFocalBlur
{
    private static RenderContext Context() => new () { Width = 201, Height = 101 };

    [TestMethod]
    public void TestAPinholeTakesOneSampleAtTheCentre()
    {
        Lens pinhole = new (0, 5);

        Assert.AreEqual(1, pinhole.SampleCount, "no aperture means no lens to spread across");
        Assert.AreEqual((0.0, 0.0), pinhole.OffsetFor(0));
    }

    [TestMethod]
    public void TestAskingForBlurWithoutAnApertureChangesNothing()
    {
        // Blur samples alone do not open the lens; without a width there is nothing to gather
        // across, and the camera must stay on the single-ray path.
        Lens lens = new (0, 5, 64);

        Assert.AreEqual(1, lens.SampleCount);
    }

    [TestMethod]
    public void TestAWideLensTakesTheSamplesItWasAskedFor()
    {
        Assert.AreEqual(16, new Lens(0.5, 5).SampleCount);
        Assert.AreEqual(32, new Lens(0.5, 5, 32).SampleCount);
        // Even asked for none, a ray has to come from somewhere.
        Assert.AreEqual(1, new Lens(0.5, 5, 0).SampleCount);
    }

    [TestMethod]
    public void TestTheSamplesStayWithinTheLens()
    {
        // The offsets are given on the unit disc and scaled by the aperture when a ray is built,
        // so every one of them must lie within a unit of the centre.
        Lens lens = new (0.5, 5, 64);

        for (int index = 0; index < lens.SampleCount; index++)
        {
            (double x, double y) = lens.OffsetFor(index);

            Assert.IsTrue(Math.Sqrt(x * x + y * y) <= 1.0 + 1e-9,
                $"sample {index} fell outside the lens at ({x:F3}, {y:F3})");
        }
    }

    [TestMethod]
    public void TestTheSamplesSpreadAcrossTheWholeLens()
    {
        // A pattern that clumped in the middle would blur unevenly, so check the samples reach out
        // toward the rim and land on all four sides of the centre.
        Lens lens = new (1, 5, 64);
        double furthest = 0;
        bool left = false, right = false, below = false, above = false;

        for (int index = 0; index < lens.SampleCount; index++)
        {
            (double x, double y) = lens.OffsetFor(index);

            furthest = Math.Max(furthest, Math.Sqrt(x * x + y * y));
            left |= x < -0.3;
            right |= x > 0.3;
            below |= y < -0.3;
            above |= y > 0.3;
        }

        Assert.IsTrue(furthest > 0.8, $"the samples reached only {furthest:F2} of the way out");
        Assert.IsTrue(left && right && below && above, "the samples missed a side of the lens");
    }

    [TestMethod]
    public void TestTheSameSeedGivesTheSameLens()
    {
        Lens first = new (0.5, 5, 32, 7);
        Lens second = new (0.5, 5, 32, 7);
        Lens other = new (0.5, 5, 32, 8);
        bool anyDiffer = false;

        for (int index = 0; index < 32; index++)
        {
            Assert.AreEqual(first.OffsetFor(index), second.OffsetFor(index),
                $"sample {index} differed between two lenses seeded alike");

            if (first.OffsetFor(index) != other.OffsetFor(index))
                anyDiffer = true;
        }

        Assert.IsTrue(anyDiffer, "a different seed should scatter the samples differently");
    }

    [TestMethod]
    public void TestEveryRayThroughTheLensCrossesTheSameFocalPoint()
    {
        // This is the whole of what focal blur rests on: the rays leave the lens from different
        // places but meet again on the focal plane, so whatever sits there is struck by all of
        // them and stays sharp.
        const double focalDistance = 4;

        PixelToRayConverter mechanics = new (
            Context(), Math.PI / 2, Matrix.Identity, new Lens(0.4, focalDistance, 16));
        Point meeting = null;

        for (int index = 0; index < mechanics.Lens.SampleCount; index++)
        {
            Ray ray = mechanics.GetRayForPixel(60, 30, lensIndex: index);
            // Walk each ray out to the focal plane, which lies square across the way the camera
            // looks, and see where it lands.
            double t = focalDistance / -ray.Direction.Z;
            Point landing = ray.Origin + ray.Direction * t;

            if (meeting is null)
                meeting = landing;
            else
            {
                Assert.IsTrue(meeting.Matches(landing),
                    $"sample {index} crossed the focal plane at {landing}, not {meeting}");
            }
        }
    }

    [TestMethod]
    public void TestTheRaysLeaveFromDifferentPlacesAcrossTheLens()
    {
        PixelToRayConverter mechanics = new (
            Context(), Math.PI / 2, Matrix.Identity, new Lens(0.4, 4, 16));
        Point first = mechanics.GetRayForPixel(60, 30, lensIndex: 0).Origin;
        bool anyMoved = false;

        for (int index = 1; index < mechanics.Lens.SampleCount; index++)
        {
            Point origin = mechanics.GetRayForPixel(60, 30, lensIndex: index).Origin;

            // The lens is a disc facing the way the camera looks, so no sample may sit off it.
            Assert.AreEqual(0, origin.Z, 1e-9, "a lens sample left the plane of the lens");
            Assert.IsTrue(Math.Sqrt(origin.X * origin.X + origin.Y * origin.Y) <= 0.4 + 1e-9,
                "a lens sample fell outside the aperture");

            if (!first.Matches(origin))
                anyMoved = true;
        }

        Assert.IsTrue(anyMoved, "every ray left from the same place, so nothing would blur");
    }

    [TestMethod]
    public void TestAPinholeRayIsUnchanged()
    {
        // The pinhole path is kept apart precisely so that it stays identical, so this pins the
        // ray a camera with no aperture gives against the one it always gave.
        PixelToRayConverter mechanics = new (Context(), Math.PI / 2);
        Ray ray = mechanics.GetRayForPixel(100, 50);

        Assert.IsTrue(Point.Zero.Matches(ray.Origin));
        Assert.IsTrue(new Vector(0, 0, -1).Matches(ray.Direction));
    }

    [TestMethod]
    public void TestACameraFocusesOnWhatItLooksAtByDefault()
    {
        // Said nothing else, the focus falls where the camera was aimed, which is nearly always
        // what was meant.
        Camera camera = new () { Location = new Point(0, 0, -5), LookAt = Point.Zero };

        Assert.AreEqual(5, camera.GetFocalDistance(), 1e-9);
    }

    [TestMethod]
    public void TestAFocalPointIsMeasuredAlongTheWayTheCameraLooks()
    {
        // What matters is how far ahead the point lies, not how far off it is: a point away to one
        // side is in focus along with everything else the same distance ahead of the camera.
        Camera camera = new ()
        {
            Location = new Point(0, 0, -5),
            LookAt = Point.Zero,
            FocalPoint = new Point(3, 0, -1)
        };

        Assert.AreEqual(4, camera.GetFocalDistance(), 1e-9);
    }

    [TestMethod]
    public void TestAStatedDistanceWinsOverAPoint()
    {
        Camera camera = new ()
        {
            Location = new Point(0, 0, -5),
            LookAt = Point.Zero,
            FocalPoint = new Point(0, 0, 0),
            FocalDistance = 2
        };

        Assert.AreEqual(2, camera.GetFocalDistance(), 1e-9);
    }
}
