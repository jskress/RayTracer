using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the tree grown by space colonization: points scattered through a crown, each drawing the nearest
/// twig within its reach a step toward it until a twig comes within its kill distance, and the wood thickened back
/// from the twigs by the pipe model.
/// </summary>
[TestClass]
public class TestGrownTree
{
    private const double Joining = 2.5;

    /// <summary>
    /// Grows a tree into a ball three across and two and a half high, standing six above the origin.
    /// </summary>
    private static GrownTree Grown(
        int seed = 3, Vector tropism = null, double susceptibility = 0, Func<Surface> leaf = null,
        Surface crown = null, List<Func<Surface>> leaves = null, bool hanging = false)
    {
        GrownTree tree = new ()
        {
            Crown = crown ?? new Sphere { Transform = Transforms.Translate(0, 6, 0) * Transforms.Scale(3, 2.5, 3) },
            Points = 800,
            Reach = 1.5,
            Kill = 0.3,
            Step = 0.15,
            Twig = 0.02,
            Joining = Joining,
            Tropism = tropism,
            Susceptibility = susceptibility,
            LeafFactories = leaves ?? (leaf is null ? [] : [leaf]),
            Hanging = hanging,
            Seed = seed
        };

        tree.PrepareForRendering();

        return tree;
    }

    private static List<TubeSegment> WoodOf(GrownTree tree) => tree.Surfaces.OfType<TubeSegment>().ToList();

    private static (double, double, double) Key(Point point) => (point.X, point.Y, point.Z);

    /// <summary>
    /// The tree stands on the origin and grows straight up from it until the crown draws it, and then everything it
    /// grows stays about the crown: a twig can overshoot a point it is drawn to by less than the kill distance, and a
    /// point draws from no further than its reach.
    /// </summary>
    [TestMethod]
    public void TestATreeGrowsUpFromTheOriginIntoItsCrown()
    {
        List<TubeSegment> wood = WoodOf(Grown());
        TubeSegment first = wood[0];

        Assert.IsTrue(wood.Count > 300, $"Only {wood.Count} pieces of wood grew.");
        Assert.AreEqual(Point.Zero, first.Start);
        Assert.AreEqual(0, first.End.X, 1e-12);
        Assert.AreEqual(0, first.End.Z, 1e-12);
        Assert.IsTrue(first.End.Y > 0);

        foreach (TubeSegment piece in wood.Where(piece => piece.End.Y > 3.5 - 1.5))
        {
            Assert.IsTrue(Math.Abs(piece.End.X) < 3 + 1.5 && Math.Abs(piece.End.Z) < 3 + 1.5 && piece.End.Y < 8.5 + 1.5,
                $"Wood grew out to {piece.End}, beyond the crown's reach.");
        }

        // And it reaches right across the crown, rather than stopping in a bunch at its foot.
        Assert.IsTrue(wood.Max(piece => piece.End.X) > 2 && wood.Min(piece => piece.End.X) < -2);
        Assert.IsTrue(wood.Max(piece => piece.End.Y) > 7.5);
    }

    /// <summary>
    /// The points are scattered by the tree's seed, so the same seed grows the same tree, place for place, and
    /// another seed another tree.
    /// </summary>
    [TestMethod]
    public void TestTheSameSeedGrowsTheSameTreeAndAnotherDoesNot()
    {
        List<TubeSegment> one = WoodOf(Grown(seed: 3));
        List<TubeSegment> again = WoodOf(Grown(seed: 3));
        List<TubeSegment> other = WoodOf(Grown(seed: 4));

        Assert.AreEqual(one.Count, again.Count);

        for (int index = 0; index < one.Count; index++)
        {
            Assert.AreEqual(one[index].End, again[index].End);
            Assert.AreEqual(one[index].EndRadius, again[index].EndRadius);
        }

        Assert.IsFalse(one.Count == other.Count && one.Zip(other).All(pair => pair.First.End == pair.Second.End),
            "Another seed grew the very same tree.");
    }

    /// <summary>
    /// The pipe model: wherever the wood forks, the radius coming in, raised to the joining, is the sum of those
    /// going out, raised to it; and every twig ends as thick as the twigs are said to be.
    /// </summary>
    [TestMethod]
    public void TestAForkIsAsThickAsItsBranchesByThePipeModel()
    {
        List<TubeSegment> wood = WoodOf(Grown());
        ILookup<(double, double, double), TubeSegment> leaving = wood.ToLookup(piece => Key(piece.Start));
        int forks = 0;
        int tips = 0;

        foreach (TubeSegment piece in wood)
        {
            List<TubeSegment> onward = leaving[Key(piece.End)].ToList();

            if (onward.Count == 0)
            {
                Assert.AreEqual(0.02, piece.EndRadius, 1e-12);
                tips++;
            }
            else if (onward.Count > 1)
            {
                double coming = Math.Pow(piece.EndRadius, Joining);
                double going = onward.Sum(branch => Math.Pow(branch.StartRadius, Joining));

                Assert.AreEqual(coming, going, coming * 1e-9);
                forks++;
            }
        }

        Assert.IsTrue(forks > 50 && tips > 50, $"Only {forks} forks and {tips} tips grew.");

        // So the trunk is as thick as all the tips together.
        Assert.AreEqual(Math.Pow(tips, 1 / Joining) * 0.02, wood[0].StartRadius, 1e-9);
    }

    /// <summary>
    /// No place grows twice.  A place drawn equally from two sides steps between them and comes no nearer either,
    /// so both draw it again the next round, toward the same spot; without the guard against it every such place
    /// sprouted a fresh twig there every round, each a tip, and the pipe model swelled the wood below them into
    /// sausages (a trunk five times too thick in the first render).
    /// </summary>
    [TestMethod]
    public void TestNoPlaceGrowsTwice()
    {
        List<Point> ends = WoodOf(Grown()).Select(piece => piece.End).ToList();

        for (int one = 0; one < ends.Count; one++)
        {
            for (int other = one + 1; other < ends.Count; other++)
            {
                // A quarter of a step, less what smoothing may have moved two neighbors toward each other.
                Assert.IsTrue((ends[one] - ends[other]).Magnitude > 0.15 * 0.1,
                    $"Two places grew at {ends[one]}.");
            }
        }
    }

    /// <summary>
    /// A tree readied a second time keeps the wood it grew rather than growing another through it.
    /// </summary>
    [TestMethod]
    public void TestATreeReadiedAgainGrowsNoMore()
    {
        GrownTree tree = Grown();
        int grown = tree.Surfaces.Count;

        tree.PrepareForRendering();

        Assert.AreEqual(grown, tree.Surfaces.Count);
    }

    /// <summary>
    /// A tropism bends every step of growth toward it, as readily as the susceptibility says: bent down, the wood
    /// heads lower on the whole than wood left to the points alone.
    /// </summary>
    [TestMethod]
    public void TestATropismBendsTheGrowth()
    {
        static double Rise(GrownTree tree) => WoodOf(tree)
            .Where(piece => piece.Start.Y > 3)
            .Average(piece => (piece.End - piece.Start).Unit.Y);

        double free = Rise(Grown());
        double drooping = Rise(Grown(tropism: new Vector(0, -1, 0), susceptibility: 0.4));
        double reaching = Rise(Grown(tropism: new Vector(0, 1, 0), susceptibility: 0.4));

        Assert.IsTrue(drooping < free - 0.05, $"Bent down, the wood rose by {drooping} against {free}.");
        Assert.IsTrue(reaching > free + 0.05, $"Bent up, the wood rose by {reaching} against {free}.");
    }

    /// <summary>
    /// A leaf is made once and shared: every place one stands is an instance of the same surface, standing at the
    /// end of a twig or a step or two behind it, its +Y along the wood coming into it as drawn, and its face, +Z,
    /// turned toward the sky within an eighth of a turn -- on any twig not standing upright.
    /// </summary>
    [TestMethod]
    public void TestLeavesAreSharedAndStandOnTheTwigs()
    {
        int made = 0;
        GrownTree tree = Grown(leaf: () =>
        {
            made++;

            return new Sphere { Transform = Transforms.Translate(0, 0.05, 0) * Transforms.Scale(0.02, 0.05, 0.01) };
        });
        List<TubeSegment> wood = WoodOf(tree);
        List<Instance> leaves = tree.Surfaces.OfType<Instance>().ToList();
        Dictionary<(double, double, double), TubeSegment> ending = wood.ToDictionary(piece => Key(piece.End));
        HashSet<(double, double, double)> forks = wood
            .GroupBy(piece => Key(piece.Start))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        int tips = wood.Count(piece => !wood.Any(onward => Key(onward.Start) == Key(piece.End)));

        Assert.AreEqual(1, made);
        Assert.AreEqual(1, leaves.Select(leaf => leaf.Prototype).Distinct().Count());
        Assert.IsTrue(leaves.Count >= tips && leaves.Count <= tips * 3, $"{leaves.Count} leaves for {tips} tips.");

        foreach (Instance leaf in leaves)
        {
            Point at = leaf.Transform * Point.Zero;
            Vector up = (leaf.Transform * new Vector(0, 1, 0)).Unit;

            Assert.IsTrue(ending.TryGetValue(Key(at), out TubeSegment twig), $"A leaf stands at {at}, on no wood.");
            Assert.IsFalse(forks.Contains(Key(at)), $"A leaf stands at a fork, {at}.");
            Assert.AreEqual(1, (leaf.Transform * new Vector(1, 0, 0)).Magnitude, 1e-9);

            Vector along = (twig.End - twig.Start).Unit;
            Vector face = (leaf.Transform * new Vector(0, 0, 1)).Unit;
            Vector sky = new Vector(0, 1, 0) - along * along.Y;

            Assert.AreEqual(1, up.Dot(along), 1e-9, $"A leaf at {at} points {up}, along wood running {along}.");

            if (sky.Magnitude > 0.05)
                Assert.IsTrue(face.Dot(sky.Unit) >= Math.Cos(Math.PI / 4) - 1e-9, $"A leaf at {at} faces {face}.");

            Assert.AreEqual(0, face.Dot(up), 1e-9);
        }
    }

    /// <summary>
    /// Several leaves: each is made once, and every place a leaf stands takes one of them -- about as often as any
    /// other, and a leaf of nothing (an empty group) keeps its share of the places by standing nowhere.
    /// </summary>
    [TestMethod]
    public void TestSeveralLeavesShareThePlacesBetweenThem()
    {
        int[] made = new int[3];
        Sphere[] spheres = new Sphere[2];
        GrownTree tree = Grown(leaves:
        [
            () => { made[0]++; return spheres[0] = new Sphere { Transform = Transforms.Scale(0.03) }; },
            () => { made[1]++; return spheres[1] = new Sphere { Transform = Transforms.Scale(0.04) }; },
            () => { made[2]++; return new Group(); }
        ]);
        List<Instance> leaves = tree.Surfaces.OfType<Instance>().ToList();
        GrownTree alone = Grown(leaf: () => new Sphere { Transform = Transforms.Scale(0.03) });
        int places = alone.Surfaces.OfType<Instance>().Count();
        int first = leaves.Count(leaf => leaf.Prototype == spheres[0]);
        int second = leaves.Count(leaf => leaf.Prototype == spheres[1]);

        CollectionAssert.AreEqual(new[] { 1, 1, 1 }, made);
        Assert.AreEqual(leaves.Count, first + second);

        // Two thirds of the places, the empty third standing nothing -- within what chance allows over a few
        // hundred places.
        Assert.AreEqual(places * 2 / 3.0, leaves.Count, places * 0.08, $"{leaves.Count} leaves of {places} places.");
        Assert.AreEqual(first, second, places * 0.1, $"{first} of one leaf against {second} of the other.");
    }

    /// <summary>
    /// Hanging leaves stand upright in the tree's own space at the end of their twigs, whichever way the twig runs,
    /// each turned some way of its own about the upright.
    /// </summary>
    [TestMethod]
    public void TestHangingLeavesStandUpright()
    {
        GrownTree tree = Grown(leaf: () => new Sphere { Transform = Transforms.Scale(0.03) }, hanging: true);
        List<Instance> leaves = tree.Surfaces.OfType<Instance>().ToList();
        HashSet<(double, double, double)> ends = WoodOf(tree).Select(piece => Key(piece.End)).ToHashSet();

        Assert.IsTrue(leaves.Count > 100);

        foreach (Instance leaf in leaves)
        {
            Assert.IsTrue(ends.Contains(Key(leaf.Transform * Point.Zero)));
            Assert.AreEqual(1, (leaf.Transform * new Vector(0, 1, 0)).Y, 1e-12);
        }

        Assert.IsTrue(leaves.Select(leaf => Math.Round((leaf.Transform * new Vector(1, 0, 0)).X, 2)).Distinct().Count() > 50,
            "the hanging leaves are not turned each its own way");
    }

    /// <summary>
    /// A crown written as several surfaces is room wherever any of them is, overlaps included: a place inside
    /// two overlapping balls looks out through both, crossing four times, and parity alone would call it outside.
    /// </summary>
    [TestMethod]
    public void TestACrownOfOverlappingPartsHoldsTheOverlap()
    {
        Group crown = new ();

        crown.Add(new Sphere { Transform = Transforms.Translate(-0.5, 0, 0) });
        crown.Add(new Sphere { Transform = Transforms.Translate(0.5, 0, 0) });
        crown.PrepareForRendering();

        Assert.IsTrue(GrownTree.Holds(crown, Point.Zero));
        Assert.IsTrue(GrownTree.Holds(crown, new Point(-1.2, 0, 0)));
        Assert.IsTrue(GrownTree.Holds(crown, new Point(1.2, 0, 0)));
        Assert.IsFalse(GrownTree.Holds(crown, new Point(0, 1.1, 0)));
        Assert.IsFalse(GrownTree.Holds(crown, new Point(1.6, 0, 0)));

        // And through a group's own move.
        crown = new Group { Transform = Transforms.Translate(0, 6, 0) };
        crown.Add(new Sphere());
        crown.PrepareForRendering();

        Assert.IsTrue(GrownTree.Holds(crown, new Point(0, 6.5, 0)));
        Assert.IsFalse(GrownTree.Holds(crown, new Point(0, 0.5, 0)));
    }

    /// <summary>
    /// What cannot grow a tree is refused, with the reason: a crown with no end to it, a crown the trunk can never
    /// come within reach of, and a kill distance as long as the reach, which uses every point up
    /// before it draws anything.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotGrowIsRefused()
    {
        Exception endless = Assert.ThrowsExactly<Exception>(() => Grown(crown: new Plane()));
        Exception aside = Assert.ThrowsExactly<Exception>(() => Grown(
            crown: new Sphere { Transform = Transforms.Translate(10, 6, 0) }));
        Exception killing = Assert.ThrowsExactly<Exception>(() =>
            new GrownTree { Crown = new Sphere(), Reach = 1, Kill = 1 }.PrepareForRendering());

        StringAssert.Contains(endless.Message, "must end somewhere");
        StringAssert.Contains(aside.Message, "climbed past its crown");
        StringAssert.Contains(killing.Message, "kill must be less than its reach");
    }

    /// <summary>
    /// The scene language: a crown written out as surfaces or named, a leaf named or called, and a tree with no crown
    /// refused as it is read.  The words are a grown tree's own and nobody else's: a scene may still name things with
    /// them, as the trees library has long named a parameter `twig` and the undergrowth a loop's counter `step`.
    /// </summary>
    [TestMethod]
    public void TestTheSceneLanguageGrowsATree()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"grown-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string Run(string body)
            {
                string scene = Path.Combine(directory, "scene.igl");
                string output = Path.Combine(directory, "scene.png");
                StringWriter captured = new ();
                TextWriter was = Console.Out;

                File.WriteAllText(scene,
                    "camera { location [0, 5, -16]  look at [0, 4.5, 0] }\n" +
                    "point light { location [-10, 20, -12] }\n" + body);
                Console.SetOut(captured);

                try
                {
                    new LanguageParser(scene).Parse()?.Render(new RenderOptions
                    {
                        OutputFileName = output, Width = 8, Height = 6
                    });
                }
                catch (Exception exception)
                {
                    return exception.Message;
                }
                finally
                {
                    Console.SetOut(was);
                }

                return captured.ToString().Contains("Error") ? captured.ToString() : null;
            }

            Assert.IsNull(Run("""
                grown {
                    crown { sphere { scale [3, 2.5, 3]  translate [0, 6, 0] } }
                    points 300  reach 1.5  kill 0.3  step 0.15  twig 0.02  joining 2
                    tropism [0, -1, 0]  susceptibility 0.2
                    leaf { sphere { scale 0.05 } }
                    with seed 2
                    material { pigment [0.4, 0.3, 0.2] }
                }
                """));
            Assert.IsNull(Run("""
                Crown = sphere { scale [3, 2.5, 3]  translate [0, 6, 0] }
                primitive Leaf(size) -> sphere { return sphere { scale size } }
                grown { crown Crown  points 300  reach 1.5  kill 0.3  leaf Leaf(0.05) }
                """));
            Assert.IsNull(Run("""
                reach = 1.5
                crown = sphere { scale [3, 2.5, 3]  translate [0, 6, 0] }
                primitive Twigs(twig, step, joining) -> group {
                    return group {
                        grown { crown crown  points 300  reach reach  kill 0.3  step step  twig twig  joining joining }
                        for points in [0, 1] { sphere { scale 0.1  translate X points } }
                    }
                }
                object Twigs(0.02, 0.15, 2.5)
                """));
            Assert.IsNull(Run("""
                primitive Leaf(size) -> sphere { return sphere { scale size } }
                grown {
                    crown { sphere { scale [3, 2.5, 3]  translate [0, 6, 0] } }
                    points 300  reach 1.5  kill 0.3
                    leaf Leaf(0.05), { sphere { scale 0.04 } }, Leaf(0.03) { material { pigment Red } } hanging
                }
                """));
            StringAssert.Contains(Run("grown { points 300 }"), "needs a crown");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
