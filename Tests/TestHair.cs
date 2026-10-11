using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// Hair grown over a surface: where its roots fall, the shape each strand takes, and how the hair goes
/// wherever its surface goes and is found through it.
/// </summary>
[TestClass]
public class TestHair
{
    /// <summary>
    /// A count is met exactly, whatever the surface's area.
    /// </summary>
    [TestMethod]
    public void TestACountGrowsExactlyThatMany()
    {
        Assert.AreEqual(500, Furry(new HairGrowth { Count = 500, Length = 0.2 }).Hair.StrandCount);
    }

    /// <summary>
    /// A density grows as many strands as the area holds, and the lines that find the roots measure that
    /// area on the way -- for a sphere, and for a cube scaled flat into a slab, whose area is that of the
    /// slab and not the cube it was made from.
    /// </summary>
    [TestMethod]
    public void TestADensityGrowsInProportionToTheArea()
    {
        Hair ball = Furry(new HairGrowth { Density = 1000, Length = 0.1 }).Hair;
        Hair slab = Furry(new HairGrowth { Density = 1000, Length = 0.1 }, Slab()).Hair;

        Assert.AreEqual(4 * Math.PI, ball.Area, 4 * Math.PI * 0.02);
        Assert.AreEqual(1000 * 4 * Math.PI, ball.StrandCount, 1000 * 4 * Math.PI * 0.03);
        Assert.AreEqual(18.4, slab.Area, 18.4 * 0.02);
        Assert.AreEqual(18400, slab.StrandCount, 18400 * 0.03);
    }

    /// <summary>
    /// Roots fall evenly over a surface however it is shaped: the slab's top holds its share of them by
    /// area, eight parts in eighteen point four, and not a sixth of them as a face of the cube it was
    /// made from would.
    /// </summary>
    [TestMethod]
    public void TestRootsFallEvenlyOverTheSurface()
    {
        List<List<Point>> paths = Furry(new HairGrowth { Density = 400, Length = 0.1 }, Slab()).Hair.Paths();
        double onTop = paths.Count(path => path[0].Y > 0.0999) / (double) paths.Count;

        Assert.AreEqual(8 / 18.4, onTop, 0.02);
    }

    /// <summary>
    /// The same seed grows the same hair, and another seed other hair.
    /// </summary>
    [TestMethod]
    public void TestTheSeedDecidesTheHair()
    {
        List<List<Point>> first = Furry(new HairGrowth { Count = 50, Length = 0.3, Frizz = 0.5, Seed = 3 }).Hair.Paths();
        List<List<Point>> again = Furry(new HairGrowth { Count = 50, Length = 0.3, Frizz = 0.5, Seed = 3 }).Hair.Paths();
        List<List<Point>> other = Furry(new HairGrowth { Count = 50, Length = 0.3, Frizz = 0.5, Seed = 4 }).Hair.Paths();

        Assert.IsTrue(first.Zip(again).All(pair => pair.First.Zip(pair.Second).All(points => points.First.Matches(points.Second))));
        Assert.IsFalse(first.Zip(other).All(pair => pair.First[0].Matches(pair.Second[0])));
    }

    /// <summary>
    /// A strand grows from the surface, straight out along its normal when nothing bends it, to its length.
    /// </summary>
    [TestMethod]
    public void TestStrandsGrowStraightOutOfTheSurface()
    {
        foreach (List<Point> path in Furry(new HairGrowth { Count = 200, Length = 0.5, Vary = 0 }).Hair.Paths())
        {
            Vector root = path[0] - Point.Zero;

            Assert.AreEqual(1, root.Magnitude, 1e-5, "a root was off the surface");
            Assert.AreEqual(0, (path[^1] - (Point.Zero + root * 1.5)).Magnitude, 1e-5, "a strand did not grow straight out");
        }
    }

    /// <summary>
    /// Strands lean toward the comb, laid along the surface where they grow.
    /// </summary>
    [TestMethod]
    public void TestHairLeansTowardTheComb()
    {
        Vector comb = new (1, 0, 0);
        HairGrowth growth = new () { Count = 500, Length = 0.5, Vary = 0, Lean = Math.PI / 3, Comb = comb };

        foreach (List<Point> path in Furry(growth).Hair.Paths().Where(path => Math.Abs(path[0].X) < 0.9))
        {
            Vector normal = (path[0] - Point.Zero).Unit;
            Vector way = (path[^1] - path[0]).Unit;
            Vector along = (comb - normal * comb.Dot(normal)).Unit;
            Vector leaning = (way - normal * way.Dot(normal)).Unit;

            Assert.AreEqual(Math.Cos(Math.PI / 3), way.Dot(normal), 1e-4, "a strand leaned by the wrong amount");
            Assert.AreEqual(1, leaning.Dot(along), 1e-4, "a strand leaned the wrong way");
        }
    }

    /// <summary>
    /// A drooping strand growing sideways bends until its tip hangs down.
    /// </summary>
    [TestMethod]
    public void TestDroopBendsStrandsDown()
    {
        List<List<Point>> sideways = Furry(new HairGrowth { Count = 2000, Length = 0.5, Droop = 1 }).Hair.Paths()
            .Where(path => Math.Abs(path[0].Y) < 0.05).ToList();

        Assert.IsTrue(sideways.Count > 5, "too few strands at the equator to judge by");

        foreach (List<Point> path in sideways)
        {
            Assert.IsTrue((path[1] - path[0]).Unit.Y > -0.5, "a strand sagged before it had grown");
            Assert.IsTrue((path[^1] - path[^2]).Unit.Y < -0.9, "a strand's tip did not hang down");
        }
    }

    /// <summary>
    /// A strand standing straight up droops too, falling whichever way it was turned: there is no one way
    /// from up to down, and the strands on a flat top -- a rug's, every one of them -- once stood unmoved.
    /// </summary>
    [TestMethod]
    public void TestAnUprightStrandDroopsToo()
    {
        List<List<Point>> top = Furry(new HairGrowth { Count = 400, Length = 0.5, Droop = 1 }, Slab()).Hair.Paths()
            .Where(path => path[0].Y > 0.0999).ToList();

        Assert.IsTrue(top.Count > 50, "too few strands on top to judge by");

        foreach (List<Point> path in top)
            Assert.IsTrue((path[^1] - path[^2]).Unit.Y < -0.9, "an upright strand did not droop");
    }

    /// <summary>
    /// Hair does not grow on hair: fur grown over a group, one of whose parts has fur of its own, roots in
    /// the part's skin and not in its fur.
    /// </summary>
    [TestMethod]
    public void TestHairDoesNotGrowOnHair()
    {
        Group group = new () { HairGrowth = new HairGrowth { Count = 500, Length = 0.2 } };

        group.Add(new Sphere { HairGrowth = new HairGrowth { Count = 3000, Length = 0.5, Thickness = 0.05 } });
        group.PrepareForRendering();

        foreach (List<Point> path in group.Hair.Paths())
            Assert.AreEqual(1, (path[0] - Point.Zero).Magnitude, 1e-5, "a strand grew from another strand");
    }

    /// <summary>
    /// A curling strand winds about its line, so it is longer than the way from its root to its tip; a
    /// plain one is not.
    /// </summary>
    [TestMethod]
    public void TestCurlWindsAStrand()
    {
        Assert.AreEqual(1, Winding(new HairGrowth { Count = 50, Length = 0.5 }), 1e-6);
        Assert.IsTrue(Winding(new HairGrowth { Count = 50, Length = 0.5, Curl = 2 }) > 1.3);
    }

    /// <summary>
    /// Clumped strands gather into locks: drawn all the way, every strand's tip is on its lock's; not drawn
    /// at all, no two tips meet.
    /// </summary>
    [TestMethod]
    public void TestClumpGathersStrandsIntoLocks()
    {
        Assert.AreEqual(0, SharedTips(new HairGrowth { Count = 600, Length = 0.5 }), 1e-9);
        Assert.IsTrue(SharedTips(new HairGrowth { Count = 600, Length = 0.5, Clump = 1, ClumpSize = 20 }) > 0.9);
    }

    /// <summary>
    /// Hair grows in the space its surface is placed in, so a surface scaled up does not stretch its
    /// strands; and it goes where the surface goes, there and afterward.
    /// </summary>
    [TestMethod]
    public void TestHairGoesWhereItsSurfaceGoes()
    {
        Sphere sphere = new () { Transform = Transforms.Translate(5, 0, 0) * Transforms.Scale(2) };

        Furry(new HairGrowth { Count = 2000, Length = 0.5, Vary = 0, Thickness = 0.05 }, sphere);

        foreach (List<Point> path in sphere.Hair.Paths())
        {
            Assert.AreEqual(2, (path[0] - new Point(5, 0, 0)).Magnitude, 1e-5, "a root was off the surface");
            Assert.AreEqual(0.5, (path[^1] - path[0]).Magnitude, 1e-5, "a strand was stretched with its surface");
        }

        // Moved once it has grown, as placing it beside something does.
        Assert.IsTrue(HairCrossings(sphere, 5) > 0, "the hair was not found through its surface");

        sphere.Transform = Transforms.Translate(-10, 0, 0) * sphere.Transform;

        Assert.AreEqual(0, HairCrossings(sphere, 5), "the hair stayed behind when its surface moved");
        Assert.IsTrue(HairCrossings(sphere, -5) > 0, "the hair did not follow its surface");
    }

    /// <summary>
    /// The box around a surface holds its hair, so nothing turns away a ray that would find it; what the
    /// surface really covers does not, so a thing placed atop it sits on its skin.
    /// </summary>
    [TestMethod]
    public void TestTheBoxHoldsTheHairButTheTrueExtentDoesNot()
    {
        Sphere sphere = Furry(new HairGrowth { Count = 400, Length = 0.5, Vary = 0 });

        Assert.IsTrue(Surface.BoxAround(sphere).Maximum.Y > 1.45);
        Assert.AreEqual(1, Surface.BoxAround(sphere, true).Maximum.Y, 1e-9);
    }

    /// <summary>
    /// A plane goes on forever, so it has no area to spread hair over.
    /// </summary>
    [TestMethod]
    public void TestAPlaneCannotGrowHair()
    {
        Plane plane = new () { HairGrowth = new HairGrowth { Count = 10, Length = 0.1 } };

        Assert.ThrowsExactly<Exception>(() => plane.PrepareForRendering());
    }

    /// <summary>
    /// Hair on one side of a union shows where it is out of the other side and is hidden where it is
    /// buried in it -- and passing a strand never counts as going into anything, so the other side's own
    /// crossings come through whole.  The hair grows over two spheres of different materials, so its
    /// strands are reported on parts of it, one for each; those stand on the side the hair does.
    /// </summary>
    [TestMethod]
    public void TestHairOnAUnionShowsOnlyOutsideTheOtherSide()
    {
        Group furry = new ();

        furry.Add(new Sphere { Material = new Material(), Transform = Transforms.Translate(-0.5, 0, 0) * Transforms.Scale(0.8) });
        furry.Add(new Sphere { Material = new Material(), Transform = Transforms.Translate(0.5, 0, 0) * Transforms.Scale(0.8) });
        furry.HairGrowth = new HairGrowth { Count = 3000, Length = 1, Vary = 0, Thickness = 0.05 };

        CsgSurface union = new ()
        {
            Operation = CsgOperation.Union, Left = furry, Right = new Sphere { Transform = Transforms.Scale(1.5) }
        };
        Random random = new (11);
        HashSet<Surface> wearers = [];

        union.PrepareForRendering();
        furry.Hair.WearWhatItGrewFrom();

        for (int index = 0; index < 400; index++)
        {
            // Aimed within the outer sphere's outline, so every ray crosses it twice.
            double reach = 1.4 * Math.Sqrt(random.NextDouble());
            double turn = 2 * Math.PI * random.NextDouble();
            Ray ray = new (new Point(reach * Math.Cos(turn), reach * Math.Sin(turn), -5), new Vector(0, 0, 1));
            List<Intersection> hits = [];

            union.Intersect(ray, hits);

            List<Intersection> solid = hits.Where(hit => hit is not HairIntersection).ToList();

            Assert.AreEqual(2, solid.Count, $"ray {index} lost the outer sphere's crossings: " +
                string.Join(", ", hits.Select(hit => $"{hit.Surface.GetType().Name}@{(ray.At(hit.Distance) - Point.Zero).Magnitude:F3}")));
            Assert.IsTrue(solid.All(hit => Math.Abs((ray.At(hit.Distance) - Point.Zero).Magnitude - 1.5) < 1e-6));

            foreach (Intersection hit in hits.OfType<HairIntersection>())
            {
                Assert.IsTrue((ray.At(hit.Distance) - Point.Zero).Magnitude > 1.5 - 0.03, "buried hair was kept");
                wearers.Add(hit.Surface);
            }
        }

        Assert.AreEqual(2, wearers.Count, "the hair was not reported on a part for each material");
    }

    /// <summary>
    /// A ray passing a strand is in whatever it was in before, and stays there: a strand inside glass
    /// changes nothing about the glass's way out.
    /// </summary>
    [TestMethod]
    public void TestPassingAStrandEntersNothing()
    {
        Sphere glass = TestSpheres.CreateGlassSphere();
        Strand strand = new ();

        strand.Points.Add(new RibbonControlPoint { Position = new Point(-0.5, 0, 0), Width = 0.1 });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(0.5, 0, 0), Width = 0.1 });
        strand.PrepareForRendering();

        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1));
        List<Intersection> hits = [];

        glass.Intersect(ray, hits);
        strand.Intersect(ray, hits);
        hits.Sort();

        Assert.AreEqual(3, hits.Count);
        Assert.IsInstanceOfType<HairIntersection>(hits[1]);

        hits[1].PrepareUsing(ray, hits);
        hits[2].PrepareUsing(ray, hits);

        Assert.AreEqual(1.5, hits[1].N1, 1e-9, "the strand was not met inside the glass");
        Assert.AreEqual(1.5, hits[1].N2, 1e-9, "passing the strand left the glass");
        Assert.AreEqual(1.5, hits[2].N1, 1e-9, "the way out of the glass was not from the glass");
        Assert.AreEqual(1, hits[2].N2, 1e-9, "the way out of the glass was not into the air");
    }

    /// <summary>
    /// A ray leaving a strand toward a light on the side the strand faces does not meet the strand again.
    /// From the ribbon it was hit on, half way out, it would: that ribbon is inside the round strand the
    /// shading pretends it is, and the ribbon a ray toward the light is tested against lies across its way.
    /// </summary>
    [TestMethod]
    public void TestAStrandsLitSideIsNotShadowedByItself()
    {
        Strand strand = new ();

        strand.Points.Add(new RibbonControlPoint { Position = new Point(-1, 0, 0), Width = 0.2 });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(1, 0, 0), Width = 0.2 });
        strand.PrepareForRendering();

        Ray ray = new (new Point(0, 0.05, -5), new Vector(0, 0, 1));
        List<Intersection> hits = [];

        strand.Intersect(ray, hits);

        Intersection hit = hits.Single();
        Vector toLight = new Vector(0, -0.3, -1).Unit;

        hit.PrepareUsing(ray, hits);

        Assert.IsTrue(hit.Normal.Dot(toLight) > 0, "the light is not on the side the strand faces");
        Assert.IsTrue(Ahead(strand, new Ray(hit.Point, toLight)) > 0, "from the ribbon the strand was not in the way");
        Assert.AreEqual(0, Ahead(strand, new Ray(hit.OverPoint, toLight)), "the strand shadowed its own lit side");
    }

    /// <summary>
    /// Hair casts a shadow as its surface does.
    /// </summary>
    [TestMethod]
    public void TestHairCastsAShadowAsItsSurfaceDoes()
    {
        Sphere sphere = Furry(new HairGrowth { Count = 20, Length = 0.2 });
        Group group = new () { NoShadow = true };

        group.Add(sphere);
        ShadowSettler.Settle([group], false);

        Assert.IsTrue(sphere.Hair.NoShadow);
    }

    private static int Ahead(Surface surface, Ray ray)
    {
        List<Intersection> hits = [];

        surface.Intersect(ray, hits);

        return hits.Count(hit => hit.Distance > 0);
    }

    /// <summary>
    /// This method returns how many crossings of hair rays looking along Z find, aimed at a square around
    /// the given X.
    /// </summary>
    private static int HairCrossings(Surface surface, double x)
    {
        int count = 0;

        for (double across = -2.5; across <= 2.5; across += 0.1)
        {
            for (double up = -2.5; up <= 2.5; up += 0.1)
            {
                List<Intersection> hits = [];

                surface.Intersect(new Ray(new Point(x + across, up, -10), new Vector(0, 0, 1)), hits);
                count += hits.OfType<HairIntersection>().Count();
            }
        }

        return count;
    }

    /// <summary>
    /// This method returns how much longer the strands are, on average, than the way from root to tip.
    /// </summary>
    private static double Winding(HairGrowth growth)
    {
        return Furry(growth).Hair.Paths().Average(path =>
        {
            double length = 0;

            for (int index = 1; index < path.Count; index++)
                length += (path[index] - path[index - 1]).Magnitude;

            return length / (path[^1] - path[0]).Magnitude;
        });
    }

    /// <summary>
    /// This method returns the share of strands whose tip is on another strand's.
    /// </summary>
    private static double SharedTips(HairGrowth growth)
    {
        List<Point> tips = Furry(growth).Hair.Paths().Select(path => path[^1]).ToList();
        int shared = tips.Count(tip => tips.Count(other => (other - tip).Magnitude < 1e-6) > 1);

        return shared / (double) tips.Count;
    }

    /// <summary>
    /// This method returns a cube scaled flat into a slab four across, two deep and a fifth high, whose
    /// area is eighteen point four.
    /// </summary>
    private static Cube Slab() => new () { Transform = Transforms.Scale(2, 0.1, 1) };

    /// <summary>
    /// This method gives a surface -- a unit sphere unless another is given -- hair to grow, and readies
    /// it so that it grows it.
    /// </summary>
    private static TSurface Furry<TSurface>(HairGrowth growth, TSurface surface, bool prepare = true)
        where TSurface : Surface
    {
        surface.HairGrowth = growth;

        if (prepare)
            surface.PrepareForRendering();

        return surface;
    }

    private static Sphere Furry(HairGrowth growth, bool prepare = true) => Furry(growth, new Sphere(), prepare);
}
