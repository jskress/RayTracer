using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestFootprint
{
    [TestMethod]
    public void TestARayThatWasNeverToldItSpreadsCoversNothing()
    {
        // The default, and the one that matters most: everything that existed before footprints did
        // goes on getting a point sample, because a footprint of nothing is what it is handed.
        Ray ray = new (new Point(0, 0, 0), new Vector(0, 0, 1));

        Assert.AreEqual(0, ray.Spread, 1e-12);
        Assert.AreEqual(0, ray.RadiusAt(1000), 1e-12);
        Assert.IsTrue(Footprint.For(ray.RadiusAt(1000), ray.Direction, new Vector(0, 0, -1)).IsEmpty);
    }

    [TestMethod]
    public void TestAConeWidensWithTheDistanceItHasGone()
    {
        Ray ray = new (new Point(0, 0, 0), new Vector(0, 0, 1), 0, 0.002);

        // Half the angle times the distance, and twice as far is twice as wide.
        Assert.AreEqual(0.1, ray.RadiusAt(100), 1e-9);
        Assert.AreEqual(0.2, ray.RadiusAt(200), 1e-9);
    }

    [TestMethod]
    public void TestAConeGoesOnWideningAfterItHasBounced()
    {
        // A reflected ray starts from the width its parent had reached, not from nothing -- else a
        // wall seen in a mirror across a room would be filtered as though it were an inch away.
        Ray straight = new (new Point(0, 0, 0), new Vector(0, 0, 1), 0, 0.002);
        Ray bounced = new (new Point(0, 0, 0), new Vector(0, 0, 1), 0, 0.002, 100);

        Assert.AreEqual(0.1, straight.RadiusAt(100), 1e-9);
        Assert.AreEqual(0.2, bounced.RadiusAt(100), 1e-9);
    }

    [TestMethod]
    public void TestSquareOnToASurfaceTheFootprintIsTheConeItself()
    {
        // Meeting a wall head-on, the cone's disc lands as a disc, and nothing is stretched.
        //
        // **The edges are the DIAMETER, not the radius**, and this test asserted the radius until a
        // ground-truth comparison proved it wrong.  Whatever samples the patch walks each edge from
        // -0.5 to +0.5 of it, so an edge of one radius covers one radius of surface -- half the
        // pixel.  Everything was being filtered at half the width it should have been, and this test
        // passed throughout, because it asserted what the code did rather than what it owed.
        Footprint footprint = Footprint.For(0.25, new Vector(0, 0, 1), new Vector(0, 0, -1));

        Assert.AreEqual(0.5, footprint.Across.Magnitude, 1e-9);
        Assert.AreEqual(0.5, footprint.Along.Magnitude, 1e-9);
        Assert.AreEqual(0.5, footprint.Width, 1e-9);
    }

    [TestMethod]
    public void TestAtAGlanceTheFootprintStretches()
    {
        // The whole reason this is a parallelogram and not a radius.  The same cone meeting the same
        // wall at ten degrees covers a patch about 1/sin(10) -- some 5.8 times -- longer than it
        // does head-on, because the light is smeared along the surface.  A single radius cannot say
        // that, and a street of buildings is seen at a glance far more often than square on.
        double slope = Math.PI / 18;
        Vector direction = new Vector(Math.Cos(slope), -Math.Sin(slope), 0).Unit;
        Footprint footprint = Footprint.For(0.1, direction, new Vector(0, 1, 0));

        Assert.AreEqual(0.2 / Math.Sin(slope), footprint.Width, 0.02,
            "a glancing ray should cover a patch as much longer as the angle is flatter");
        Assert.IsTrue(footprint.Width > 5 * 0.2,
            $"the glancing footprint measured {footprint.Width:F4} against a head-on 0.2; it is not " +
            "stretching at all, so the shape of the patch is being thrown away");
    }

    [TestMethod]
    public void TestTheStretchIsHeldToALimit()
    {
        // True to the physics, a ray a hair off parallel covers most of the wall.  Honouring that
        // turns a building into one flat colour, so the stretch is capped -- every renderer caps it.
        Vector direction = new Vector(1, -0.0001, 0).Unit;
        Footprint footprint = Footprint.For(0.1, direction, new Vector(0, 1, 0));

        Assert.IsTrue(footprint.Width < 0.2 * 25,
            $"a ray all but parallel to the surface gave a footprint of {footprint.Width:F4}, which " +
            "is unbounded rather than held");
    }

    [TestMethod]
    public void TestAFootprintCarriesThroughATransform()
    {
        // A pattern thinks in its own space, so the patch has to be carried into that space with the
        // point.  A stretch of the space stretches the patch with it.
        Footprint footprint = Footprint.For(0.2, new Vector(0, 0, 1), new Vector(0, 0, -1));
        Footprint scaled = footprint.TransformedBy(Transforms.Scale(3));

        Assert.AreEqual(footprint.Width * 3, scaled.Width, 1e-9);
    }

    [TestMethod]
    public void TestATranslationDoesNotChangeASize()
    {
        // The edges go through as vectors rather than as points precisely so that moving a thing
        // does not resize what it covers.
        Footprint footprint = Footprint.For(0.2, new Vector(0, 0, 1), new Vector(0, 0, -1));
        Footprint moved = footprint.TransformedBy(Transforms.Translate(100, -40, 7));

        Assert.AreEqual(footprint.Width, moved.Width, 1e-9);
    }

    [TestMethod]
    public void TestTheExtentOnAnAxisSpansThePatch()
    {
        // What a lattice pattern needs: how far the patch reaches along each axis on its own.
        Footprint footprint = new (new Vector(0.3, 0, 0), new Vector(0, 0.5, 0));

        Assert.AreEqual(0.3, footprint.ExtentOn(0), 1e-9);
        Assert.AreEqual(0.5, footprint.ExtentOn(1), 1e-9);
        Assert.AreEqual(0.0, footprint.ExtentOn(2), 1e-9);
    }
}
