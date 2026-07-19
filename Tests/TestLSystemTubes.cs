using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;

namespace Tests;

[TestClass]
public class TestLSystemTubes
{
    // Build an L-system from an axiom alone (generation 0, so the axiom is the production
    // verbatim), rendered as tubes, and prepare it so we can inspect the geometry it produced.
    private static LSystem Prepare(string axiom, double factor = 0.5)
    {
        LSystem lsystem = new LSystem
        {
            Axiom = axiom,
            Generations = 0,
            RenderingControls = new LSystemRenderingControls
            {
                RendererType = LSystemRendererType.Tubes,
                Factor = factor
            }
        };

        lsystem.PrepareForRendering();

        return lsystem;
    }

    private static List<TubeSegment> SegmentsOf(LSystem lsystem)
    {
        return lsystem.Surfaces.OfType<TubeSegment>().ToList();
    }

    [TestMethod]
    public void TestEachDrawnSegmentBecomesOneTubeSegment()
    {
        LSystem lsystem = Prepare("FFF");

        Assert.AreEqual(3, SegmentsOf(lsystem).Count);
    }

    [TestMethod]
    public void TestTubesEmitNoSeparateCaps()
    {
        // The whole point of a tube segment over a cylinder: it is round at both ends already, so
        // unlike the pipes renderer there are no cap spheres (nor any cylinders) to go with it.
        LSystem lsystem = Prepare("F[+F][-F]");

        Assert.AreEqual(0, lsystem.Surfaces.Count(surface => surface is Sphere or Cylinder));
        Assert.AreEqual(lsystem.Surfaces.Count, SegmentsOf(lsystem).Count);
    }

    [TestMethod]
    public void TestConsecutiveSegmentsAgreeOnTheRadiusTheyShare()
    {
        // This is the change's whole reason for being: where "!" shrinks the diameter between two
        // segments, the earlier one must taper to meet the later one rather than butt against it.
        List<TubeSegment> segments = SegmentsOf(Prepare("F!F!FF"));

        Assert.AreEqual(4, segments.Count);

        for (int index = 0; index < segments.Count - 1; index++)
        {
            Assert.AreEqual(
                segments[index].EndRadius, segments[index + 1].StartRadius, 1e-9,
                $"Segments {index} and {index + 1} disagree at the point they share.");
        }
    }

    [TestMethod]
    public void TestASegmentFollowedByAThinnerOneTapers()
    {
        List<TubeSegment> segments = SegmentsOf(Prepare("F!F"));

        Assert.IsTrue(segments[0].EndRadius < segments[0].StartRadius,
            $"Expected a taper, got {segments[0].StartRadius} -> {segments[0].EndRadius}.");
        Assert.AreEqual(segments[0].StartRadius / 2, segments[0].EndRadius, 1e-9);
    }

    [TestMethod]
    public void TestABranchTipKeepsItsOwnRadius()
    {
        // Nothing follows the last segment of a branch, so there is no radius to taper into and it
        // stays round at its own.
        List<TubeSegment> segments = SegmentsOf(Prepare("F!F"));
        TubeSegment tip = segments[^1];

        Assert.AreEqual(tip.StartRadius, tip.EndRadius, 1e-9);
    }

    [TestMethod]
    public void TestTheFirstSiblingDecidesWhatTheTrunkTapersTo()
    {
        // Where the children of a node disagree on radius, the trunk tapers to the first of them --
        // and, just as importantly, is emitted exactly once rather than once per child.
        List<TubeSegment> segments = SegmentsOf(Prepare("F[!F][F]"));

        Assert.AreEqual(3, segments.Count);

        TubeSegment trunk = segments[0];
        TubeSegment firstChild = segments[1];

        Assert.AreEqual(firstChild.StartRadius, trunk.EndRadius, 1e-9);
        Assert.AreEqual(trunk.StartRadius / 2, trunk.EndRadius, 1e-9);
    }

    [TestMethod]
    public void TestAMoveEndsTheChainRatherThanTaperingAcrossIt()
    {
        // "f" moves without drawing, so the segment before it does not touch the segment after it;
        // it has to finish at its own radius instead of reaching for the next one's.
        List<TubeSegment> segments = SegmentsOf(Prepare("F!fF"));

        Assert.AreEqual(2, segments.Count);
        Assert.AreEqual(segments[0].StartRadius, segments[0].EndRadius, 1e-9);
    }

    [TestMethod]
    public void TestLeavesStillStampUnderTheTubesRenderer()
    {
        // Leaves are handled by the base renderer, so they must work the same whichever renderer
        // is drawing the stems.
        LSystem lsystem = Prepare("F~");

        Assert.AreEqual(1, lsystem.Surfaces.Count(surface => surface is BicubicPatch));
        Assert.AreEqual(1, SegmentsOf(lsystem).Count);
    }
}
