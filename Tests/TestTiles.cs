using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Options;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests cover laying a grid with tiles by wave function collapse: that every pair of tiles side by side
/// agrees about the side between them and every tile agrees with the outside, that draws follow weights and the
/// seed, that a tile laid in a turn faces as its sides say, and that what cannot be laid is refused.
/// </summary>
[TestClass]
public class TestTiles
{
    private static readonly (int X, int Y, int Z)[] Steps =
        [(1, 0, 0), (0, 0, 1), (-1, 0, 0), (0, 0, -1), (0, 1, 0), (0, -1, 0)];

    /// <summary>
    /// The roads every street plan here is laid from: straights, corners, a junction of three, a crossroads, a dead
    /// end and open ground, all but the crossroads and the ground in four turns.
    /// </summary>
    private static List<WaveFunctionCollapse.State> Roads()
    {
        (string[] Sides, bool Turns, double Weight)[] tiles =
        [
            (["grass", "road", "grass", "road", null, null], true, 3),
            (["road", "grass", "grass", "road", null, null], true, 1),
            (["road", "road", "road", "grass", null, null], true, 0.6),
            (["road", "road", "road", "road", null, null], false, 0.3),
            (["grass", "grass", "grass", "road", null, null], true, 0.2),
            (["grass", "grass", "grass", "grass", null, null], false, 2)
        ];

        return StatesOf(tiles);
    }

    /// <summary>
    /// This method turns tiles into states, as the resolver does: each turned a quarter at a time, its sides with it.
    /// </summary>
    private static List<WaveFunctionCollapse.State> StatesOf((string[] Sides, bool Turns, double Weight)[] tiles)
    {
        List<WaveFunctionCollapse.State> states = [];

        for (int tile = 0; tile < tiles.Length; tile++)
        {
            int turns = tiles[tile].Turns ? 4 : 1;

            for (int turn = 0; turn < turns; turn++)
            {
                string[] sides = tiles[tile].Sides;
                string[] turned = [..Enumerable.Range(0, 4).Select(side => sides[(side + turn) % 4]), sides[4], sides[5]];

                states.Add(new WaveFunctionCollapse.State(tile, turn, turned, tiles[tile].Weight / turns));
            }
        }

        return states;
    }

    /// <summary>
    /// This method checks that every pair of cells side by side agrees about the side between them, and every cell
    /// on the edge with what the outside presents.
    /// </summary>
    private static void AssertLaidRight(
        int[] laid, List<WaveFunctionCollapse.State> states, int across, int up, int along, string[] outside,
        (string, string)[] pairs = null)
    {
        Assert.IsNotNull(laid, "the grid was not laid");

        // The rule, said again here rather than asked of the solver: a side saying nothing fits anything; a label in
        // a pair fits only its partner, either way around; any other fits only itself.
        bool Fits(string one, string other)
        {
            if (one is null || other is null)
                return true;

            pairs ??= [];

            return pairs.Any(pair => pair.Item1 == one || pair.Item2 == one || pair.Item1 == other || pair.Item2 == other)
                ? pairs.Any(pair => (pair.Item1, pair.Item2) == (one, other) || (pair.Item2, pair.Item1) == (one, other))
                : one == other;
        }

        for (int y = 0; y < up; y++)
        {
            for (int z = 0; z < along; z++)
            {
                for (int x = 0; x < across; x++)
                {
                    WaveFunctionCollapse.State here = states[laid[(y * along + z) * across + x]];

                    for (int side = 0; side < 6; side++)
                    {
                        (int nx, int ny, int nz) = (x + Steps[side].X, y + Steps[side].Y, z + Steps[side].Z);

                        if (nx < 0 || ny < 0 || nz < 0 || nx >= across || ny >= up || nz >= along)
                        {
                            Assert.IsTrue(Fits(here.Sides[side], outside[side]),
                                $"({x}, {y}, {z}) shows {here.Sides[side]} to an outside of {outside[side]}");
                            continue;
                        }

                        WaveFunctionCollapse.State next = states[laid[(ny * along + nz) * across + nx]];

                        Assert.IsTrue(
                            Fits(here.Sides[side], next.Sides[WaveFunctionCollapse.Opposite(side)]),
                            $"({x}, {y}, {z}) shows {here.Sides[side]} to its neighbor's " +
                            $"{next.Sides[WaveFunctionCollapse.Opposite(side)]}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// A street plan laid over a grid of 24 by 24 agrees everywhere: every road runs on into a road, every side of
    /// grass meets grass, and no road runs off the edge, for each of a dozen seeds.
    /// </summary>
    [TestMethod]
    public void TestEveryTileAgreesWithItsNeighbors()
    {
        List<WaveFunctionCollapse.State> states = Roads();
        string[] outside = ["grass", "grass", "grass", "grass", null, null];

        for (int seed = 0; seed < 12; seed++)
        {
            int[] laid = new WaveFunctionCollapse(states, 24, 1, 24, seed).Lay(outside, 100);

            AssertLaidRight(laid, states, 24, 1, 24, outside);
        }
    }

    /// <summary>
    /// Labels paired to fit each other fit only each other: a road's curb meets a house's frontage, so every house
    /// fronts a road and no two houses front each other -- which they did, all over, when both sides said "curb" (the
    /// last seed's grid, laid so, is the proof that the pair is what keeps them apart).
    /// </summary>
    [TestMethod]
    public void TestPairedLabelsFitOnlyEachOther()
    {
        List<WaveFunctionCollapse.State> Town(string frontage) => StatesOf(
        [
            (["road", "curb", "road", "curb", null, null], true, 4),
            (["road", "road", "curb", "curb", null, null], true, 2),
            (["road", "road", "road", "curb", null, null], true, 1),
            (["yard", "yard", "yard", frontage, null, null], true, 6),
            (["yard", "yard", "yard", "yard", null, null], false, 2),
            ([frontage, "yard", "yard", frontage, null, null], true, 1),
            ([frontage, "yard", frontage, frontage, null, null], true, 1),
            ([frontage, frontage, frontage, frontage, null, null], false, 1)
        ]);
        string[] outside = ["yard", "yard", "yard", "yard", null, null];
        (string, string)[] pairs = [("curb", "frontage")];
        List<WaveFunctionCollapse.State> states = Town("frontage");
        int facing = 0;

        for (int seed = 0; seed < 8; seed++)
        {
            int[] laid = new WaveFunctionCollapse(states, 16, 1, 16, seed, pairs).Lay(outside, 100);

            AssertLaidRight(laid, states, 16, 1, 16, outside, pairs);
            facing += FrontagesFacing(laid, states, 16, 16, "frontage");
        }

        Assert.AreEqual(0, facing, "houses fronted each other with no road between");

        // The rule itself: a word paired with itself fits itself, one in a pair fits nothing else, either way around,
        // an unpaired word fits only itself, and an unsaid side fits anything.
        WaveFunctionCollapse rule = new (states, 1, 1, 1, 0, [("curb", "frontage"), ("frontage", "frontage")]);

        Assert.IsTrue(rule.Matches("curb", "frontage"));
        Assert.IsTrue(rule.Matches("frontage", "curb"));
        Assert.IsTrue(rule.Matches("frontage", "frontage"));
        Assert.IsFalse(rule.Matches("curb", "curb"));
        Assert.IsFalse(rule.Matches("curb", "yard"));
        Assert.IsFalse(rule.Matches("yard", "frontage"));
        Assert.IsTrue(rule.Matches("yard", "yard"));
        Assert.IsFalse(rule.Matches("yard", "road"));
        Assert.IsTrue(rule.Matches("curb", null));
        Assert.IsTrue(rule.Matches(null, "yard"));

        // Said alike on both sides, with no pair, the curb is a frontage too, and houses face houses.
        List<WaveFunctionCollapse.State> alike = Town("curb");
        int[] unpaired = new WaveFunctionCollapse(alike, 16, 1, 16, 0).Lay(outside, 100);

        AssertLaidRight(unpaired, alike, 16, 1, 16, outside);
        Assert.IsGreaterThan(0, FrontagesFacing(unpaired, alike, 16, 16, "curb"));
    }

    /// <summary>
    /// This method counts the places where a plot's road side faces another plot's, plots being the tiles with no
    /// road side.
    /// </summary>
    private static int FrontagesFacing(
        int[] laid, List<WaveFunctionCollapse.State> states, int across, int along, string frontage)
    {
        bool IsAPlot(WaveFunctionCollapse.State state) => !state.Sides.Take(4).Contains("road");
        int count = 0;

        for (int z = 0; z < along; z++)
        {
            for (int x = 0; x < across; x++)
            {
                WaveFunctionCollapse.State here = states[laid[z * across + x]];

                if (!IsAPlot(here))
                    continue;

                // Looking right and back only, so each pair is counted once.
                foreach (int side in new[] { WaveFunctionCollapse.Right, WaveFunctionCollapse.Back })
                {
                    (int nx, int nz) = (x + Steps[side].X, z + Steps[side].Z);

                    if (nx >= across || nz >= along)
                        continue;

                    WaveFunctionCollapse.State next = states[laid[nz * across + nx]];

                    if (IsAPlot(next) && here.Sides[side] == frontage &&
                        next.Sides[WaveFunctionCollapse.Opposite(side)] == frontage)
                        count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Laid in layers, the tops and bottoms agree too: posts that must stand on posts or on the ground, and under
    /// nothing but a cap or another post, laid four high.
    /// </summary>
    [TestMethod]
    public void TestTilesAgreeAboveAndBelowToo()
    {
        List<WaveFunctionCollapse.State> states = StatesOf(
        [
            ([null, null, null, null, "post", "post"], false, 1),
            ([null, null, null, null, "air", "post"], false, 1),
            ([null, null, null, null, "air", "air"], false, 2)
        ]);
        string[] outside = [null, null, null, null, "air", "post"];

        for (int seed = 0; seed < 6; seed++)
        {
            int[] laid = new WaveFunctionCollapse(states, 6, 4, 6, seed).Lay(outside, 100);

            AssertLaidRight(laid, states, 6, 4, 6, outside);
        }
    }

    /// <summary>
    /// The same seed lays the same grid, and another seed another; and tiles that fit anywhere are drawn as often as
    /// their weights say: three to one comes out within three hundredths of three quarters over 40 by 40.
    /// </summary>
    [TestMethod]
    public void TestDrawsFollowTheSeedAndTheWeights()
    {
        List<WaveFunctionCollapse.State> roads = Roads();
        string[] outside = ["grass", "grass", "grass", "grass", null, null];
        int[] once = new WaveFunctionCollapse(roads, 16, 1, 16, 5).Lay(outside, 100);
        int[] again = new WaveFunctionCollapse(roads, 16, 1, 16, 5).Lay(outside, 100);
        int[] other = new WaveFunctionCollapse(roads, 16, 1, 16, 6).Lay(outside, 100);

        CollectionAssert.AreEqual(once, again, "the same seed should lay the same grid");
        CollectionAssert.AreNotEqual(once, other, "another seed should lay another grid");

        List<WaveFunctionCollapse.State> free = StatesOf(
        [
            ([null, null, null, null, null, null], false, 3), ([null, null, null, null, null, null], false, 1)
        ]);
        int[] laid = new WaveFunctionCollapse(free, 40, 1, 40, 2).Lay([null, null, null, null, null, null], 1);

        Assert.AreEqual(0.75, laid.Count(state => state == 0) / 1600.0, 0.03);
    }

    /// <summary>
    /// What no draw can mend is refused before any draw: an outside no tile can face, or tiles that cannot stand
    /// beside the outside or beside one another at all -- a straight road laid only as written, which must run off a
    /// grass edge -- and the grid is not tried again for them.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotBeLaidIsRefused()
    {
        List<WaveFunctionCollapse.State> roads = Roads();

        Assert.ThrowsExactly<ArgumentException>(() =>
            new WaveFunctionCollapse(roads, 4, 1, 4, 0).Lay(["water", "water", "water", "water", null, null], 10));

        List<WaveFunctionCollapse.State> endless = StatesOf([(["grass", "road", "grass", "road", null, null], false, 1)]);

        Assert.ThrowsExactly<ArgumentException>(() =>
            new WaveFunctionCollapse(endless, 3, 1, 3, 0).Lay(["grass", "grass", "grass", "grass", null, null], 5));
    }

    /// <summary>
    /// A grid stands each tile in its cell, turned as it was laid, and the turn is the one its sides were turned by:
    /// two cells along Z holding a tile whose right side alone is a road, with grass all around, face their roads at
    /// each other -- so the tile in the nearer cell turns its +X to +Z, and the one in the farther to -Z -- each at the
    /// middle of its cell, the grid's middle over the origin.
    /// </summary>
    [TestMethod]
    public void TestATileIsTurnedAsItsSidesAre()
    {
        TileSpec end = new () { Surface = new CubeResolver(), Turns = true };

        for (int side = 0; side < 4; side++)
            end.Sides[side] = new LiteralResolver<string> { Value = side == 0 ? "road" : "grass" };

        TileGridResolver resolver = new ()
        {
            SizeResolver = new LiteralResolver<Vector> { Value = new Vector(1, 1, 2) },
            CellResolver = new LiteralResolver<Vector> { Value = new Vector(4, 4, 4) },
            EdgeResolver = new LiteralResolver<string> { Value = "grass" },
            Tiles = [end]
        };
        TileGrid grid = (TileGrid) resolver.ResolveToSurface(new RenderContext(), new Variables());
        List<Surface> placed = grid.Surfaces.ToList();

        Assert.AreEqual(2, placed.Count);

        Point nearMiddle = placed[0].Transform * Point.Zero;
        Point farMiddle = placed[1].Transform * Point.Zero;
        Vector nearRight = placed[0].Transform * new Vector(1, 0, 0);
        Vector farRight = placed[1].Transform * new Vector(1, 0, 0);

        AssertNear(new Vector(0, 0, -2), nearMiddle - Point.Zero, "the nearer cell's middle");
        AssertNear(new Vector(0, 0, 2), farMiddle - Point.Zero, "the farther cell's middle");
        AssertNear(new Vector(0, 0, 1), nearRight, "the nearer tile's road should face the farther");
        AssertNear(new Vector(0, 0, -1), farRight, "the farther tile's road should face the nearer");
        Assert.IsInstanceOfType<Instance>(placed[0], "a tile built once should be shared");
    }

    private static void AssertNear(Vector expected, Vector actual, string what)
    {
        Assert.AreEqual(expected.X, actual.X, 1e-9, $"{what}: {actual}");
        Assert.AreEqual(expected.Y, actual.Y, 1e-9, $"{what}: {actual}");
        Assert.AreEqual(expected.Z, actual.Z, 1e-9, $"{what}: {actual}");
    }

    /// <summary>
    /// A scene writes a grid of tiles, and it reaches the picture: roads on grass, seen from above, covering the
    /// grid; and what cannot be laid, or is written wrong, is refused with a reason.
    /// </summary>
    [TestMethod]
    public void TestASceneLaysAGrid()
    {
        const string Tiles = """
            primitive Slab(paint) -> cube { return cube { scale [1, 0.05, 1]  material { pigment paint  ambient 0.4 } } }
            tiles {
                size [6, 1, 6]
                cell 2
                tile { Slab([0.2, 0.2, 0.2])  front 'road'  back 'road'  left 'grass'  right 'grass'  turns }
                tile { Slab([0.2, 0.2, 0.2])  front 'road'  right 'road'  back 'grass'  left 'grass'  turns }
                tile { Slab([0.2, 0.6, 0.2])  sides 'grass' }
                edge 'grass'
            }
            """;
        string directory = Path.Combine(Path.GetTempPath(), $"tiles-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");

            File.WriteAllText(path,
                "camera { location [0, 20, -0.01]  look at [0, 0, 0] }\npoint light { location [0, 30, 0] }\n" + Tiles);
            new LanguageParser(path).Parse().Render(new RenderOptions
            {
                OutputFileName = Path.Combine(directory, "out.png"), Width = 40, Height = 40, ProgressStyleText = "none"
            });

            Canvas canvas = new ImageFile(Path.Combine(directory, "out.png")).Load()[0];
            int green = 0, gray = 0;

            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    Color color = canvas.GetPixel(x, y);

                    if (color.Green > color.Red * 1.5)
                        green++;
                    else if (color.Red > 0.05)
                        gray++;
                }
            }

            Assert.IsTrue(green > 100, $"only {green} pixels of grass");
            Assert.IsTrue(green + gray > 500, $"the grid should cover the picture's middle: {green + gray} pixels");

            StringAssert.Contains(Refusal(directory, Tiles.Replace("edge 'grass'", "edge 'sea'")), "cannot be laid at all");
            StringAssert.Contains(Refusal(directory, Tiles.Replace("tile { Slab([0.2, 0.6, 0.2])  sides 'grass' }",
                "tile { sides 'grass' }")), "must say what stands in it");
            StringAssert.Contains(Refusal(directory, Tiles.Replace("size [6, 1, 6]", "size [6, 0, 6]")),
                "at least one cell");
            StringAssert.Contains(Refusal(directory, Tiles.Replace("sides 'grass' }", "sides 'grass'  weight 0 }")),
                "more than nought");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A scene's `fit` reaches the grid: one cell, a dark tile all `curb` and a pale one all `frontage`, against an
    /// outside of `frontage`.  With the pair only the curb can face it; without, only the frontage, the same word.
    /// And a scene may name a value with the word `tiles` and use it there: tried after the surfaces, the top-level
    /// assignment was never reached, and the docs' triplanar example, naming its picture so, stopped parsing.
    /// </summary>
    [TestMethod]
    public void TestAScenesPairsReachTheGrid()
    {
        const string Scene = """
            tiles = 'frontage'
            primitive Slab(paint) -> cube { return cube { scale [1, 0.05, 1]  material { pigment paint  ambient 1  diffuse 0  specular 0 } } }
            tiles {
                size [1, 1, 1]
                cell 8
                tile { Slab([0.05, 0.05, 0.05])  sides 'curb' }
                tile { Slab([0.9, 0.9, 0.9])  sides 'frontage' }
                edge tiles
                fit 'curb' to 'frontage'
            }
            """;
        string directory = Path.Combine(Path.GetTempPath(), $"tiles-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            int renders = 0;

            double Middle(string scene)
            {
                string path = Path.Combine(directory, $"scene-{++renders}.igl");
                string output = Path.Combine(directory, $"out-{renders}.png");

                File.WriteAllText(path,
                    "camera { location [0, 20, -0.01]  look at [0, 0, 0] }\npoint light { location [0, 30, 0] }\n" +
                    scene);
                new LanguageParser(path).Parse().Render(new RenderOptions
                {
                    OutputFileName = output, Width = 20, Height = 20, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(10, 10).Red;
            }

            Assert.IsLessThan(0.4, Middle(Scene), "with the pair, the curb should face the outside");
            Assert.IsGreaterThan(0.7, Middle(Scene.Replace("fit 'curb' to 'frontage'", "")),
                "without the pair, only the frontage should face it");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// A tile that turns is drawn no more often for it: its weight is shared among its four turns.  Two tiles of
    /// equal weight that fit anywhere, one turning and one not, each cover about half of a grid of 20 by 20 -- not
    /// four fifths and one, as a weight given whole to every turn would draw them.
    /// </summary>
    [TestMethod]
    public void TestATileThatTurnsIsDrawnNoMoreOften()
    {
        const string Scene = """
            camera { location [0, 30, -0.01]  look at [0, 0, 0]  field of view 36 }
            point light { location [0, 40, 0] }
            primitive Slab(paint) -> cube {
                return cube { scale [0.5, 0.05, 0.5]  material { pigment paint  ambient 1  diffuse 0  specular 0 } }
            }
            tiles {
                size [20, 1, 20]
                tile { Slab([0.05, 0.05, 0.05])  turns }
                tile { Slab([0.9, 0.9, 0.9]) }
                with seed 5
            }
            """;
        string directory = Path.Combine(Path.GetTempPath(), $"tiles-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            File.WriteAllText(path, Scene);
            new LanguageParser(path).Parse().Render(new RenderOptions
            {
                OutputFileName = output, Width = 60, Height = 60, ProgressStyleText = "none"
            });

            Canvas canvas = new ImageFile(output).Load()[0];
            int dark = 0, pale = 0;

            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    double red = canvas.GetPixel(x, y).Red;

                    if (red > 0.7)
                        pale++;
                    else if (red > 0.1 && red < 0.4)
                        dark++;
                }
            }

            double share = dark / (double) (dark + pale);

            Assert.IsGreaterThan(2500, dark + pale, "the grid should fill most of the picture");
            Assert.IsTrue(Math.Abs(share - 0.5) < 0.1, $"the turning tile covered {share:P0} of the grid");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string Refusal(string directory, string scene)
    {
        string path = Path.Combine(directory, "refused.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path, "camera { location [0, 20, -0.01]  look at [0, 0, 0] }\n" + scene);
        Console.SetOut(captured);

        try
        {
            new LanguageParser(path).Parse()?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(directory, "refused.png"), Width = 4, Height = 4,
                ProgressStyleText = "none"
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

        return captured.ToString();
    }
}
