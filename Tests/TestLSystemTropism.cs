using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;

namespace Tests;

/// <summary>
/// These tests cover tropism: the small turn a segment takes toward a given direction as it is
/// drawn, which is what makes a branch sag under its own weight or lean away from a wind rather
/// than running dead straight to wherever it was aimed.
/// </summary>
[TestClass]
public class TestLSystemTropism
{
    /// <summary>
    /// Draws a straight run of segments under the given tropism and hands back the tube segments
    /// it produced.  The turtle starts out heading along +X, which is square across a downward
    /// tropism -- the case where the bend is largest and therefore the easiest to hold to a
    /// number.
    /// </summary>
    private static List<TubeSegment> Run(
        double susceptibility, Vector tropism = null, string axiom = "FFFF", double angle = 90)
    {
        LSystem lsystem = new LSystem
        {
            Axiom = axiom,
            Generations = 0,
            RenderingControls = new LSystemRenderingControls
            {
                RendererType = LSystemRendererType.Tubes,
                Factor = 1,
                Angle = angle * Math.PI / 180,
                Susceptibility = susceptibility,
                Tropism = tropism ?? Directions.Down
            }
        };

        lsystem.PrepareForRendering();

        return lsystem.Surfaces.OfType<TubeSegment>().ToList();
    }

    [TestMethod]
    public void TestNoSusceptibilityLeavesARunDeadStraight()
    {
        // This is the property the whole feature rests on: an L-system written before any of this
        // existed must draw exactly as it always did.  The default susceptibility is nought, so
        // every scene in the gallery is covered by this one assertion.
        foreach (TubeSegment segment in Run(0))
        {
            Assert.AreEqual(0, segment.Start.Y, 1e-12);
            Assert.AreEqual(0, segment.End.Y, 1e-12);
            Assert.AreEqual(0, segment.End.Z, 1e-12);
        }
    }

    [TestMethod]
    public void TestATropismBendsTheRunTowardItself()
    {
        List<TubeSegment> segments = Run(0.25);

        Assert.AreEqual(4, segments.Count);
        Assert.IsTrue(segments[^1].End.Y < 0,
            $"a run under a downward tropism should end below where it started, and this one " +
            $"ended at {segments[^1].End.Y}");

        // And it should droop further with every segment rather than taking one kink and then
        // carrying on straight: the turn is applied per segment, so the run is a curve.
        for (int index = 1; index < segments.Count; index++)
        {
            double earlier = segments[index - 1].End.Y - segments[index - 1].Start.Y;
            double later = segments[index].End.Y - segments[index].Start.Y;

            Assert.IsTrue(later < earlier,
                $"segment {index} should fall further than the one before it ({later} against " +
                $"{earlier}); the run is not curving");
        }
    }

    [TestMethod]
    public void TestASegmentAlreadyFacingTheTropismIsLeftAlone()
    {
        // The cross product is nought both when the heading runs along the tropism and when it
        // runs dead against it, so there is no axis to turn about and nothing to do.  A plant
        // growing straight down is not bent further by gravity.
        foreach (TubeSegment segment in Run(0.4, Directions.Right))
        {
            Assert.AreEqual(0, segment.End.Y, 1e-12);
            Assert.AreEqual(0, segment.End.Z, 1e-12);
        }
    }

    [TestMethod]
    public void TestTheBendIsTheTorqueOnTheSegment()
    {
        // Prusinkiewicz and Lindenmayer turn the turtle by e * |H x T| about the axis H x T.  The
        // arithmetic is written out here rather than borrowed from the code, so that a change to
        // the formula fails rather than agreeing with itself.
        const double susceptibility = 0.3;

        TubeSegment first = Run(susceptibility)[0];

        // The turtle heads along +X and the tropism is straight down, so |H x T| is one and the
        // whole susceptibility is spent on the first turn.  The second segment therefore leaves at
        // that angle below the horizontal.
        TubeSegment second = Run(susceptibility)[1];
        Vector along = (second.End - second.Start).Unit;
        double expected = -Math.Sin(susceptibility * 1.0);

        Assert.AreEqual(0, first.End.Y, 1e-12, "the first segment is drawn before any bending");
        Assert.AreEqual(expected, along.Y, 1e-9);
    }

    [TestMethod]
    public void TestASidewaysTropismLeansTheRunSideways()
    {
        // Gravity is the obvious use and not the only one: a tropism pointing along +Z is a wind,
        // and the run should be carried out of its plane rather than downward.
        List<TubeSegment> segments = Run(0.25, Directions.In);

        Assert.IsTrue(segments[^1].End.Z > 0,
            $"a run under a tropism pointing along +Z should be carried that way, and this one " +
            $"ended at {segments[^1].End.Z}");
        Assert.AreEqual(0, segments[^1].End.Y, 1e-12,
            "a sideways tropism should not move the run up or down");
    }

    [TestMethod]
    public void TestTheBendFallsAwayAsTheSegmentTurnsIntoTheTropism()
    {
        // The test above cannot tell e * |H x T| from a plain e, because it starts the turtle
        // square across the tropism where |H x T| is exactly one.  This one starts it at a slant,
        // so the two differ, and it measures the heading out of the render rather than assuming
        // it -- which keeps the check honest whatever the turtle's pitch conventions turn out
        // to be.
        const double susceptibility = 0.35;

        List<TubeSegment> segments = Run(susceptibility, axiom: "&FF", angle: 50);
        Vector first = (segments[0].End - segments[0].Start).Unit;
        Vector second = (segments[1].End - segments[1].Start).Unit;

        double sine = first.Cross(Directions.Down).Magnitude;
        double expected = susceptibility * sine;
        double actual = Math.Acos(Math.Clamp(first.Dot(second), -1, 1));

        Assert.IsTrue(sine < 0.95,
            $"this test is only worth running at a slant, and |H x T| came out {sine}");
        Assert.AreEqual(expected, actual, 1e-9);
    }
}
