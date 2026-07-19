using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;

namespace Tests;

[TestClass]
public class TestLSystemCutoff
{
    // Build an L-system from an axiom alone (generation 0, so the axiom is the production
    // verbatim) and prepare it.  Counting cylinders counts the segments that actually got drawn,
    // which is what a cut-off branch is meant to reduce.
    private static LSystem Prepare(string axiom)
    {
        LSystem lsystem = new LSystem
        {
            Axiom = axiom,
            Generations = 0,
            RenderingControls = new LSystemRenderingControls { Angle = 90.0.ToRadians() }
        };

        lsystem.PrepareForRendering();

        return lsystem;
    }

    private static int SegmentsOf(string axiom)
    {
        return Prepare(axiom).Surfaces.Count(surface => surface is Cylinder);
    }

    [TestMethod]
    public void TestACutOffDiscardsTheRestOfItsBranch()
    {
        // The two F's after the '%' belong to the branch it stands in, so they never get drawn --
        // leaving the trunk's two and the branch's first.
        Assert.AreEqual(5, SegmentsOf("F[FFF]F"));
        Assert.AreEqual(3, SegmentsOf("F[F%FF]F"));
    }

    [TestMethod]
    public void TestACutOffLeavesTheBranchItStandsInProperlyClosed()
    {
        // Skipping to the closing bracket must not skip the bracket itself: the turtle still has to
        // be popped, so what follows the branch resumes from where the branch began rather than
        // from wherever the cut-off left off.
        LSystem lsystem = Prepare("F[+F%FF]F");
        List<Cylinder> cylinders = lsystem.Surfaces.OfType<Cylinder>().ToList();
        Point lastStart = cylinders[^1].Transform * Point.Zero;

        Assert.AreEqual(3, cylinders.Count);
        Assert.IsTrue(lastStart.Matches(new Point(1, 0, 0)), lastStart.ToString());
    }

    [TestMethod]
    public void TestOnlyTheMatchingBracketEndsACutOff()
    {
        // A branch opened after the '%' must not be mistaken for the one that closes it: the
        // inner "[FF]" is skipped whole rather than ending the cut-off early.  What survives is
        // the trunk's two segments and the one the branch drew before the '%'.
        Assert.AreEqual(3, SegmentsOf("F[F%F[FF]F]F"));
    }

    [TestMethod]
    public void TestACutOffOutsideAnyBranchDiscardsTheRest()
    {
        Assert.AreEqual(1, SegmentsOf("F%FFF"));
    }

    [TestMethod]
    public void TestACutOffRightBeforeItsBracketDiscardsNothing()
    {
        Assert.AreEqual(SegmentsOf("F[FF]F"), SegmentsOf("F[FF%]F"));
    }

    [TestMethod]
    public void TestOnlyTheCutBranchIsLost()
    {
        // Siblings of a cut-off branch carry on untouched: the trunk's two, the cut branch's one
        // before the '%', and both of the sibling's.
        Assert.AreEqual(5, SegmentsOf("F[F%FF][FF]F"));
    }
}
