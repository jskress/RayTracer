using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These tests cover the machinery motion blur rests on: the instants the camera looks at, and a
/// moving surface being found where it stands at each of them -- including from inside a group,
/// whose bounding box has to cover everywhere its children go.  What a scene writes to ask for it,
/// and that a transform given part way goes part of the way, are covered by rendering in
/// <see cref="TestCameraClauses"/>.
/// </summary>
[TestClass]
public class TestMotionBlur
{
    /// <summary>
    /// A motion that slides a surface the given distance along X over the shutter's opening.
    /// </summary>
    private static Func<double, Matrix> SlideAlongX(double distance) =>
        fraction => Transforms.Translate(distance * fraction, 0, 0);

    [TestMethod]
    public void TestAShutterThatDoesNotLingerTakesOneSample()
    {
        CameraSampler still = new (0, 5);

        Assert.AreEqual(1, still.SampleCount, "nothing moves, so there is one instant to see");
        Assert.AreEqual(0, still.TimeFractionFor(0));
    }

    [TestMethod]
    public void TestAnOpenShutterTakesTheSamplesItWasAskedFor()
    {
        Assert.AreEqual(16, new CameraSampler(0, 5, 1).SampleCount);
        Assert.AreEqual(32, new CameraSampler(0, 5, 1, 32).SampleCount);
    }

    [TestMethod]
    public void TestTheLensAndTheShutterShareOneSetOfSamples()
    {
        // The whole point of gathering both from one set: asking for both costs no more rays than
        // asking for either, rather than one ray for every pairing of the two.
        Assert.AreEqual(24, new CameraSampler(0.5, 5, 1, 24).SampleCount);
    }

    [TestMethod]
    public void TestTheInstantsSpanTheOpening()
    {
        // One instant to each equal slice of the opening, so no stretch of it is looked at twice
        // while another is missed entirely.
        CameraSampler sampler = new (0, 5, 1, 20);
        List<double> times = Enumerable.Range(0, 20)
            .Select(sampler.TimeFractionFor)
            .ToList();

        Assert.IsTrue(times.All(time => time is >= 0 and < 1), "an instant fell outside the opening");

        List<int> slices = times.Select(time => (int) (time * 20)).OrderBy(slice => slice).ToList();

        CollectionAssert.AreEqual(Enumerable.Range(0, 20).ToList(), slices,
            "the instants did not fall one to a slice of the opening");
    }

    [TestMethod]
    public void TestHowLongTheShutterStaysOpenSaysHowFarAMotionGets()
    {
        // The shutter is not merely a switch: its value is how much of a surface's motion is caught
        // while it is open.  Held open half as long, everything gets half as far, so the instants
        // must span half the range -- which is exactly what a first cut at this got wrong, since
        // the value was only ever tested against zero and every setting blurred alike.
        CameraSampler whole = new (0, 5, 1, 16, 3);
        CameraSampler half = new (0, 5, 0.5, 16, 3);

        for (int index = 0; index < 16; index++)
        {
            Assert.AreEqual(whole.TimeFractionFor(index) / 2, half.TimeFractionFor(index), 1e-12,
                $"sample {index} did not halve when the shutter was open half as long");
        }

        Assert.IsTrue(Enumerable.Range(0, 16).All(index => half.TimeFractionFor(index) < 0.5),
            "a shutter open half as long should never catch a motion past its half way point");
    }

    [TestMethod]
    public void TestTheInstantsAreTheSameEveryTime()
    {
        CameraSampler first = new (0, 5, 1, 16, 4);
        CameraSampler second = new (0, 5, 1, 16, 4);

        for (int index = 0; index < 16; index++)
            Assert.AreEqual(first.TimeFractionFor(index), second.TimeFractionFor(index));
    }

    [TestMethod]
    public void TestWhenASampleLooksIsNotTiedToWhereItLooksFrom()
    {
        // The places across the lens are laid out in index order, so were the instants too, every
        // sample early in the opening would sit on one side of the lens and every late one on the
        // other -- and a moving thing would smear crookedly rather than along its path.  The
        // instants are shuffled to break that tie.
        CameraSampler sampler = new (1, 5, 1, 32);
        int early = 0;
        int earlyAndLeft = 0;

        for (int index = 0; index < 32; index++)
        {
            if (sampler.TimeFractionFor(index) >= 0.5)
                continue;

            early++;

            if (sampler.OffsetFor(index).X < 0)
                earlyAndLeft++;
        }

        Assert.IsTrue(earlyAndLeft > 0 && earlyAndLeft < early,
            $"the early instants all fell on one side of the lens ({earlyAndLeft} of {early})");
    }

    [TestMethod]
    public void TestASurfaceThatHoldsStillKeepsTheOnePlace()
    {
        Sphere still = new ();

        still.PrepareForRendering([0, 0.5, 1]);

        Assert.IsFalse(still.Moves);
        Assert.AreEqual(1, still.TransformsThroughShutter.Count(),
            "a surface that does not move stands in exactly one place");
    }

    [TestMethod]
    public void TestAMovingSurfaceStandsInAPlaceForEachInstant()
    {
        Sphere ball = new () { MotionAt = SlideAlongX(4) };

        ball.PrepareForRendering([0, 0.5, 1]);

        Assert.IsTrue(ball.Moves);
        Assert.AreEqual(3, ball.TransformsThroughShutter.Count());
    }

    [TestMethod]
    public void TestAMovingSurfaceIsFoundWhereItStandsAtEachInstant()
    {
        // The ball starts at the origin and slides four to the right.  Looked for at the instant
        // the shutter opens it is at the start, and at the instant it closes it is at the end --
        // and, just as tellingly, it is not at the other place at either.
        Sphere ball = new () { MotionAt = SlideAlongX(4) };

        ball.PrepareForRendering([0, 1]);

        Assert.IsTrue(IsHitAt(ball, 0, 0), "at the first instant it should be where it started");
        Assert.IsFalse(IsHitAt(ball, 4, 0), "it should not yet have reached the end");
        Assert.IsTrue(IsHitAt(ball, 4, 1), "at the last instant it should have gone the whole way");
        Assert.IsFalse(IsHitAt(ball, 0, 1), "it should no longer be where it started");
    }

    [TestMethod]
    public void TestAGroupStillFindsAMovingChildAtTheEndOfItsTravels()
    {
        // This is the trap motion sets for a group.  The group tests its own bounding box before
        // troubling its children, so a box drawn around where a moving child starts would turn away
        // the very rays that should have found it further along, and the thing would be sliced off
        // part way through its own blur.  The box has to cover everywhere it goes.
        Sphere mover = new () { MotionAt = SlideAlongX(6) };
        Group group = new ();

        group.Add(mover);
        group.PrepareForRendering([0, 0.5, 1]);

        Assert.IsTrue(IsHitAt(group, 0, 0), "the group lost its child at the start of its travels");
        Assert.IsTrue(IsHitAt(group, 3, 1), "the group lost its child mid-way");
        Assert.IsTrue(IsHitAt(group, 6, 2), "the group lost its child at the end of its travels");
    }

    [TestMethod]
    public void TestAGroupWithNothingMovingIsBoundedAsItAlwaysWas()
    {
        // The swept box must not quietly widen a group whose children all hold still.
        Sphere still = new () { Transform = Transforms.Translate(3, 0, 0) };
        Group group = new ();

        group.Add(still);
        group.PrepareForRendering([0, 0.5, 1]);

        Assert.IsTrue(IsHitAt(group, 3, 0));
        Assert.IsFalse(IsHitAt(group, 6, 0), "the group's box reached past where its child stands");
    }

    /// <summary>
    /// Fires a ray straight down through the given X at the given instant, and reports whether it
    /// struck anything.
    /// </summary>
    private static bool IsHitAt(Surface surface, double x, int timeIndex)
    {
        List<Intersection> intersections = [];

        surface.Intersect(
            new Ray(new Point(x, 10, 0), new Vector(0, -1, 0), timeIndex), intersections);

        return intersections.Count > 0;
    }
}
