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
    /// A closed mesh -- a group of triangles, as a Wavefront file or a closed sweep is -- holds what is inside it,
    /// counted as one hull: asked triangle by triangle, as a group of solids is, nothing was ever inside it.
    /// </summary>
    [TestMethod]
    public void TestAClosedMeshHoldsWhatIsInsideIt()
    {
        Point[] corners =
        [
            new (-1, -1, -1), new (1, -1, -1), new (1, 1, -1), new (-1, 1, -1),
            new (-1, -1, 1), new (1, -1, 1), new (1, 1, 1), new (-1, 1, 1)
        ];
        int[][] faces = [[0, 1, 2, 3], [5, 4, 7, 6], [4, 0, 3, 7], [1, 5, 6, 2], [3, 2, 6, 7], [4, 5, 1, 0]];
        Group mesh = new ();

        foreach (int[] face in faces)
        {
            mesh.Add(new Triangle { Point1 = corners[face[0]], Point2 = corners[face[1]], Point3 = corners[face[2]] });
            mesh.Add(new Triangle { Point1 = corners[face[0]], Point2 = corners[face[2]], Point3 = corners[face[3]] });
        }

        mesh.PrepareForRendering();

        Assert.IsTrue(Containment.Holds(mesh, new Point(0.1, 0.2, 0.3)), "the middle of a closed box of triangles");
        Assert.IsFalse(Containment.Holds(mesh, new Point(1.5, 0.2, 0.3)), "a place outside it");
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

        Assert.IsTrue(Containment.Holds(crown, Point.Zero));
        Assert.IsTrue(Containment.Holds(crown, new Point(-1.2, 0, 0)));
        Assert.IsTrue(Containment.Holds(crown, new Point(1.2, 0, 0)));
        Assert.IsFalse(Containment.Holds(crown, new Point(0, 1.1, 0)));
        Assert.IsFalse(Containment.Holds(crown, new Point(1.6, 0, 0)));

        // And through a group's own move.
        crown = new Group { Transform = Transforms.Translate(0, 6, 0) };
        crown.Add(new Sphere());
        crown.PrepareForRendering();

        Assert.IsTrue(Containment.Holds(crown, new Point(0, 6.5, 0)));
        Assert.IsFalse(Containment.Holds(crown, new Point(0, 0.5, 0)));
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

    // A wall four wide, three high and three tenths thick, its front face on z = 0 and its foot on the ground.
    private static readonly Point WallMiddle = new (0, 1.5, 0.15);
    private static readonly Vector WallHalf = new (2, 1.5, 0.15);

    private static Cube Wall() => new ()
    {
        Name = "wall", Transform = Transforms.Translate(WallMiddle.X, WallMiddle.Y, WallMiddle.Z) *
                                   Transforms.Scale(WallHalf.X, WallHalf.Y, WallHalf.Z)
    };

    /// <summary>
    /// Grows a climber over the ground given, standing beside it in a group, from just in front of the wall's foot.
    /// </summary>
    private static GrownTree Climbing(Surface ground, Action<GrownTree> shape = null)
    {
        // (Its wood, and its leaves, are in its own space: see Outside.)
        GrownTree tree = new ()
        {
            AllOver = "wall", Points = 1500, Reach = 0.3, Kill = 0.06, Step = 0.04, Twig = 0.004, Seed = 5,
            Transform = Transforms.Translate(0.3, 0.1, -0.02)
        };

        shape?.Invoke(tree);
        new Group().Add(ground).Add(tree).PrepareForRendering();

        return tree;
    }

    // A climber's wood, carried out of its own space into the one it shares with the wall.
    private static List<TubeSegment> WoodOutside(GrownTree tree) =>
        WoodOf(tree).Select(piece => new TubeSegment
        {
            Start = tree.Transform * piece.Start, StartRadius = piece.StartRadius,
            End = tree.Transform * piece.End, EndRadius = piece.EndRadius
        }).ToList();

    // How far a place outside a box is from it.
    private static double FromTheBox(Point at, Point middle, Vector half)
    {
        double x = Math.Max(Math.Abs(at.X - middle.X) - half.X, 0);
        double y = Math.Max(Math.Abs(at.Y - middle.Y) - half.Y, 0);
        double z = Math.Max(Math.Abs(at.Z - middle.Z) - half.Z, 0);

        return Math.Sqrt(x * x + y * y + z * z);
    }

    /// <summary>
    /// A climber's stems lie on the surface it climbs, each end held off it by the stem's own thickness, and it grows
    /// from the place on the wall nearest where it stands -- up the face, over the top, down the far side and round
    /// the end.
    /// </summary>
    [TestMethod]
    public void TestAClimberGrowsAlongTheSurfaceItClimbs()
    {
        GrownTree tree = Climbing(Wall());
        List<TubeSegment> wood = WoodOutside(tree);
        List<Point> ends = wood.SelectMany(piece => new[] { piece.Start, piece.End }).ToList();

        Assert.IsTrue(wood.Count > 500, $"The climber grew only {wood.Count} pieces of wood.");

        foreach (TubeSegment piece in wood)
        {
            foreach (Point end in new[] { piece.Start, piece.End })
            {
                Assert.AreEqual(piece.StartRadius, FromTheBox(end, WallMiddle, WallHalf), 1e-6,
                    $"A stem's end at {end} does not lie on the wall.");
            }
        }

        Point root = new (0.3, 0.1, 0);

        // The first wood is the thickest, held off the wall by its own thickness.
        Assert.IsTrue(ends.Min(end => (end - root).Magnitude) < wood.Max(piece => piece.StartRadius) + 0.01,
            "The climber did not start nearest where it stands.");
        Assert.IsTrue(ends.Any(end => end.Y > 3), "The climber did not grow over the top of the wall.");
        Assert.IsTrue(ends.Any(end => end.Z > 0.3 && end.Y < 2.5), "The climber did not grow down the far side.");
        Assert.IsTrue(ends.Any(end => Math.Abs(end.X) > 2 && end.Y < 2.5), "The climber did not grow round the end.");
    }

    /// <summary>
    /// A climber turns into a corner the surface makes, where a fin stands out of the wall, and goes on up the
    /// fin's side: a step that meets the surface ahead stops there, rather than going through a fin thinner than a
    /// step.  Its stems never stand off the surface, though deep in the corner one held off the fin by its
    /// thickness sinks a little into the wall.
    /// </summary>
    [TestMethod]
    public void TestAClimberTurnsIntoACorner()
    {
        Point finMiddle = new (0.9, 1.5, -0.15);
        Vector finHalf = new (0.005, 1.5, 0.15);
        CsgSurface ground = new ()
        {
            Name = "wall", Operation = CsgOperation.Union,
            Left = new Cube
            {
                Transform = Transforms.Translate(WallMiddle.X, WallMiddle.Y, WallMiddle.Z) *
                            Transforms.Scale(WallHalf.X, WallHalf.Y, WallHalf.Z)
            },
            Right = new Cube
            {
                Transform = Transforms.Translate(finMiddle.X, finMiddle.Y, finMiddle.Z) *
                            Transforms.Scale(finHalf.X, finHalf.Y, finHalf.Z)
            }
        };
        GrownTree tree = Climbing(ground);
        List<TubeSegment> wood = WoodOutside(tree);

        foreach (TubeSegment piece in wood)
        {
            foreach (Point end in new[] { piece.Start, piece.End })
            {
                double distance = Math.Min(
                    FromTheBox(end, WallMiddle, WallHalf), FromTheBox(end, finMiddle, finHalf));

                Assert.IsTrue(distance <= piece.StartRadius + 1e-6, $"A stem's end at {end} stands off the wall.");
            }

            // Across the fin's plane only where the fin is not: round its edge, over its top, or elsewhere on the
            // wall.
            if ((piece.Start.X - finMiddle.X) * (piece.End.X - finMiddle.X) < 0)
            {
                bool beside = Math.Max(piece.Start.Z, piece.End.Z) < 0 && Math.Min(piece.Start.Z, piece.End.Z) > -0.3 &&
                              Math.Min(piece.Start.Y, piece.End.Y) > 0 && Math.Max(piece.Start.Y, piece.End.Y) < 3;

                Assert.IsFalse(beside, $"A stem from {piece.Start} to {piece.End} went through the fin.");
            }
        }

        // Up the fin's near side, square to -X, as the climber comes at it from the left.
        Assert.IsTrue(
            wood.Any(piece => Math.Abs(piece.End.X - (0.895 - piece.EndRadius)) < 1e-6 && piece.End.Z < -0.05),
            "The climber did not go on up the side of the fin.");
    }

    /// <summary>
    /// A climber's wood runs no further from its root than its spread, measured along it; unbounded, it runs further.
    /// </summary>
    [TestMethod]
    public void TestAClimberSpreadsNoFurtherThanItsSpread()
    {
        Point root = new (0.3, 0.1, 0);
        double Furthest(GrownTree tree) =>
            WoodOutside(tree).SelectMany(piece => new[] { piece.Start, piece.End }).Max(end => (end - root).Magnitude);

        double bounded = Furthest(Climbing(Wall(), tree => tree.Spread = 1));
        double free = Furthest(Climbing(Wall()));

        Assert.IsTrue(bounded <= 1 + 0.01, $"The climber ran {bounded} from its root, past its spread of one.");
        Assert.IsTrue(bounded > 0.8, $"The climber ran only {bounded} from its root.");
        Assert.IsTrue(free > 2, $"Unbounded, the climber ran only {free} from its root.");
    }

    /// <summary>
    /// A climber's limits keep it to part of the surface, as a field's keep its copies: below a height, its points
    /// and so its growth stay beneath it; facing a way, its points and so its growth stay on the faces that face it.
    /// </summary>
    [TestMethod]
    public void TestAClimbersLimitsKeepItToPartOfTheSurface()
    {
        GrownTree low = Climbing(Wall(), tree => tree.Below = 1.2);
        GrownTree front = Climbing(Wall(), tree => (tree.Facing, tree.FacingPower) = (new Vector(0, 0, -1), 8));

        Assert.IsTrue(WoodOutside(low).Max(piece => piece.End.Y) <= 1.2 + 0.04 + 0.01,
            $"The climber grew up to {WoodOutside(low).Max(piece => piece.End.Y)}, past the height it was kept below.");
        Assert.IsTrue(WoodOutside(low).Max(piece => piece.End.Y) > 1, "The climber kept below a height grew no way up to it.");
        Assert.IsFalse(WoodOutside(front).Any(piece => piece.End.Z > 0.3 || piece.End.Y > 3),
            "The climber kept to the face looking -Z grew over the top or onto the back.");
    }

    /// <summary>
    /// A climber's leaves stand out of the surface on its wood, +Y the way out and +Z down the face within an eighth
    /// of a turn, all along its shoots but not on its oldest wood; on a level face they turn any way.
    /// </summary>
    [TestMethod]
    public void TestAClimbersLeavesStandOutOfTheSurface()
    {
        GrownTree tree = Climbing(Wall(), climber => climber.LeafFactories =
        [
            () => new Sphere { Transform = Transforms.Scale(0.005) }
        ]);
        List<TubeSegment> wood = WoodOutside(tree);
        Dictionary<(double, double, double), TubeSegment> ending = wood
            .GroupBy(piece => Key(piece.End))
            .ToDictionary(group => group.Key, group => group.First());
        List<Instance> leaves = tree.Surfaces.OfType<Instance>().ToList();
        int leafy = wood.Count(piece => piece.EndRadius <= 6 * 0.004 * (1 + 1e-9));

        Assert.AreEqual(leafy, leaves.Count, "The climber's leaves do not stand on all its wood but the oldest.");
        Assert.IsTrue(leaves.Count < wood.Count, "The climber's oldest wood bore leaves.");

        bool level = false;

        foreach (Instance leaf in leaves)
        {
            Matrix placed = tree.Transform * leaf.Transform;
            Point at = placed * Point.Zero;
            Vector up = (placed * new Vector(0, 1, 0)).Unit;
            Vector tip = (placed * new Vector(0, 0, 1)).Unit;

            Assert.IsTrue(ending.ContainsKey(Key(at)), $"A leaf stands at {at}, on no wood.");
            Assert.AreEqual(ending[Key(at)].EndRadius + 0.005,
                FromTheBox(at + up * 0.005, WallMiddle, WallHalf), 1e-6, $"A leaf at {at} does not stand out of the wall.");
            Assert.AreEqual(0, tip.Dot(up), 1e-9);

            if (up.Y > 0.99)
                level = true;
            else if (Math.Abs(up.Y) < 1e-9)
                Assert.IsTrue(tip.Y <= -Math.Cos(Math.PI / 4) + 1e-9, $"A leaf at {at} points {tip}, not down the wall.");
        }

        Assert.IsTrue(level, "No leaf stood on the top of the wall.");
    }

    /// <summary>
    /// What a climber cannot do is refused: standing out of its reach of what it climbs, climbing a surface that goes
    /// on forever, climbing and growing into a crown at once, keeping a crown's tree to part of a surface, and a
    /// spacing for one growing into a crown.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotClimbIsRefused()
    {
        Exception far = Assert.ThrowsExactly<Exception>(() =>
            Climbing(Wall(), tree => tree.Transform = Transforms.Translate(0, 1, -1)));
        Exception endless = Assert.ThrowsExactly<Exception>(() => Climbing(new Plane { Name = "wall" }));
        Exception both = Assert.ThrowsExactly<Exception>(() => Climbing(Wall(), tree => tree.Crown = new Sphere()));
        Exception limits = Assert.ThrowsExactly<Exception>(() =>
            new GrownTree { Crown = new Sphere(), Below = 1 }.PrepareForRendering());
        Exception spacing = Assert.ThrowsExactly<Exception>(() =>
            new GrownTree { Crown = new Sphere(), Spacing = 0.1 }.PrepareForRendering());

        StringAssert.Contains(far.Message, "must stand within its reach");
        StringAssert.Contains(endless.Message, "goes on forever");
        StringAssert.Contains(both.Message, "not both");
        StringAssert.Contains(limits.Message, "keep it to part of a surface");
        StringAssert.Contains(spacing.Message, "spacing is how far apart");
    }

    /// <summary>
    /// The scene language: a climber written out with its limits, spread and spacing, one a primitive makes for the
    /// surface it is handed by name, and what it cannot do refused as the scene is read.
    /// </summary>
    [TestMethod]
    public void TestTheSceneLanguageGrowsAClimber()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"grown-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            (string Error, int Wood) Run(string body)
            {
                string scene = Path.Combine(directory, "scene.igl");
                string output = Path.Combine(directory, "scene.png");
                StringWriter captured = new ();
                TextWriter was = Console.Out;

                File.WriteAllText(scene,
                    "camera { location [0, 1.5, -6]  look at [0, 1.5, 0] }\n" +
                    "point light { location [-4, 6, -6] }\n" +
                    "cube { scale [2, 1.5, 0.15]  translate [0, 1.5, 0.15]  named 'wall' }\n" + body);
                Console.SetOut(captured);

                try
                {
                    ImageRenderer renderer = new LanguageParser(scene).Parse();

                    renderer?.Render(new RenderOptions { OutputFileName = output, Width = 8, Height = 6 });
                }
                catch (Exception exception)
                {
                    return (exception.Message, 0);
                }
                finally
                {
                    Console.SetOut(was);
                }

                return (captured.ToString().Contains("Error") ? captured.ToString() : null, 0);
            }

            Assert.IsNull(Run("""
                grown {
                    all over 'wall'
                    points 800  reach 0.3  kill 0.06  step 0.04  twig 0.004
                    below 2.5  facing vector [0, 0, -1] by 2  steepest 90
                    patches bozo { scale 0.5 } from 0.2 to 0.6
                    spread 1.8
                    leaf { sphere { scale 0.01 } }
                    translate [0.3, 0.1, -0.02]
                }
                """).Error);
            Assert.IsNull(Run("""
                primitive Climber(wall, spread) -> grown {
                    return grown { all over wall  spacing 0.1  reach 0.3  kill 0.06  step 0.04  spread spread }
                }
                object Climber('wall', 1.5) { translate [-0.5, 0.1, -0.02] }
                """).Error);
            StringAssert.Contains(
                Run("grown { all over 'wall'  points 300  spacing 0.1  translate [0.3, 0.1, -0.02] }").Error,
                "not both");
            StringAssert.Contains(
                Run("grown { all over 'stone'  translate [0.3, 0.1, -0.02] }").Error,
                "the names here are 'wall'");
            StringAssert.Contains(
                Run("grown { all over 'wall'  reach 0.3  kill 0.06  step 0.04  on 'wall' }").Error,
                "grown tree climbing something");
            StringAssert.Contains(Run("grown { all over 'wall'  spread 0 }").Error, "more than nought");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
