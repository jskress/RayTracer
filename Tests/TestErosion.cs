using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Pigments;
using RayTracer.Renderer;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover a height field's erosion: rivers cutting valleys, crags shedding scree and rain running down,
/// and the maps of what each did.  Each process is held to what it is for -- where it wears and where it lays down
/// -- and to wearing the same land the same way however finely the field is sampled, which is what lets a scene be
/// tried out coarse and drawn fine.
/// </summary>
[TestClass]
public class TestErosion
{
    private const double Width = 1000;
    private const double Depth = 1000;

    /// <summary>
    /// Makes a grid of heights from a function over the unit square, as a height field samples one.
    /// </summary>
    private static double[,] Grid(int samples, Func<double, double, double> height)
    {
        double[,] grid = new double[samples, samples];

        for (int row = 0; row < samples; row++)
        {
            for (int column = 0; column < samples; column++)
                grid[column, row] = height(column / (samples - 1.0), row / (samples - 1.0));
        }

        return grid;
    }

    /// <summary>
    /// A valley: sides rising a third of a meter for every meter out from its middle, its floor falling a fifth of
    /// a meter in a meter toward the edge at z nought, where its river leaves.
    /// </summary>
    private static double Valley(double x, double z) => 100 + 300 * Math.Abs(x - 0.5) + 200 * z;

    /// <summary>
    /// Rolling land falling toward one edge, with hollows and spurs, for comparing one sampling with another.
    /// </summary>
    private static double Rolling(double x, double z) =>
        300 * z + 60 * Math.Sin(7 * x + 1) * Math.Cos(5 * z) + 35 * Math.Sin(15 * x - 2 * z) +
        20 * Math.Cos(23 * z + 3 * x);

    /// <summary>
    /// A sheer crag 200 meters tall across the middle of the land.
    /// </summary>
    private static double Crag(double x, double z) => x < 0.5 ? 0 : 200;

    private static ErodedGround Worn(double[,] heights, Erosion erosion, double width = Width, double depth = Depth) =>
        erosion.Erode(heights, width, 1, depth, 0);

    private static double At(double[,] heights, double x, double z)
    {
        int last = heights.GetLength(0) - 1;

        return heights[(int) Math.Round(x * last), (int) Math.Round(z * last)];
    }

    private static double MapAt(ErodedGround ground, ErosionMap map, double x, double z)
    {
        int samples = ground.Heights.GetLength(0);
        int column = (int) Math.Round(x * (samples - 1));
        int row = (int) Math.Round(z * (samples - 1));

        return ground.MapOf(map)[row * samples + column];
    }

    /// <summary>
    /// A river cuts down where the water gathers: the valley's floor, draining the land on both sides, wears far more
    /// than its sides, which drain only themselves.  Rivers carry away and lay nothing down, and the field's edge,
    /// where they leave, does not wear.
    /// </summary>
    [TestMethod]
    public void TestRiversCutDeepestWhereTheMostWaterRuns()
    {
        double[,] before = Grid(65, Valley);
        ErodedGround ground = Worn(before, new Erosion { Rivers = 0.3 });
        double floor = At(before, 0.5, 0.5) - At(ground.Heights, 0.5, 0.5);
        double side = At(before, 0.1, 0.5) - At(ground.Heights, 0.1, 0.5);

        Assert.IsTrue(floor > 0, "the valley's floor should have been cut down");
        Assert.IsTrue(floor > 5 * side, $"the floor was cut {floor:F2} m and a side {side:F2} m");
        Assert.IsTrue(ground.Deposits.All(value => value == 0), "a river laid something down");

        for (int index = 0; index < 65; index++)
        {
            Assert.AreEqual(before[index, 0], ground.Heights[index, 0], "the edge a river leaves by wore");
            Assert.AreEqual(before[0, index], ground.Heights[0, index], "the edge a river leaves by wore");
        }
    }

    /// <summary>
    /// The sides of a valley a river cuts fall back to the angle of repose once the cutting leaves them steeper, so
    /// a river makes a valley and not a slot: no slope the cutting made steeper is left steeper than that.
    /// </summary>
    [TestMethod]
    public void TestAValleysSidesFallBackToTheirAngleOfRepose()
    {
        double[,] before = Grid(65, Valley);
        Erosion erosion = new () { Rivers = 2, Repose = 30 * Math.PI / 180 };
        ErodedGround ground = Worn(before, erosion);
        double step = Width / 64;
        double steepest = 0;
        double deepest = 0;

        for (int row = 1; row < 64; row++)
        {
            for (int column = 1; column < 63; column++)
            {
                double across = Math.Abs(ground.Heights[column + 1, row] - ground.Heights[column, row]) / step;
                double was = Math.Abs(before[column + 1, row] - before[column, row]) / step;

                if (across > was)
                    steepest = Math.Max(steepest, across);

                deepest = Math.Max(deepest, before[column, row] - ground.Heights[column, row]);
            }
        }

        Assert.IsTrue(deepest > 20, $"the river cut only {deepest:F1} m");
        Assert.AreEqual(Math.Tan(erosion.Repose), steepest, 0.01,
            "the cutting left a valley's side steeper than its angle of repose, or never made one as steep");
    }

    /// <summary>
    /// A lake's bed lies under still water: the rivers run across it and leave it as it was.
    /// </summary>
    [TestMethod]
    public void TestALakeBedLiesStillUnderItsWater()
    {
        // A bowl 60 meters deep sunk in land falling toward z nought.
        static double Bowl(double x, double z)
        {
            double away = Math.Sqrt((x - 0.5) * (x - 0.5) + (z - 0.6) * (z - 0.6));

            return 200 * z + (away < 0.2 ? -60 * (1 - away / 0.2) : 0);
        }

        double[,] before = Grid(65, Bowl);
        ErodedGround ground = Worn(before, new Erosion { Rivers = 0.5 });

        Assert.AreEqual(At(before, 0.5, 0.6), At(ground.Heights, 0.5, 0.6), 1e-12, "a lake's bed was worn");
        Assert.IsTrue(At(before, 0.5, 0.2) > At(ground.Heights, 0.5, 0.2), "the river below the lake did not cut");
    }

    /// <summary>
    /// Rivers cut the same land the same way at any sampling: their rates come from areas and lengths in the world,
    /// not from points of the grid.  Counted in points, a grid twice as fine drains four times as many into every
    /// place.
    /// </summary>
    [TestMethod]
    public void TestRiversCutAFinerGridAsACoarser()
    {
        AssertTheSameAtEitherSampling(new Erosion { Rivers = 0.3 }, 0.25);
    }

    /// <summary>
    /// Scree sheds from a sheer crag and comes to rest at its foot: what the crag lost lies below it, none of it
    /// lost, and nothing it laid down stands steeper than its angle of repose -- in the world, so the same holds of
    /// a field stretched to twice its width, its grid's points twice as far apart.
    /// </summary>
    [TestMethod]
    public void TestScreeRestsAtItsAngleOfReposeInTheWorld()
    {
        foreach (double width in new[] { Width, 2 * Width })
        {
            double[,] before = Grid(101, Crag);
            Erosion erosion = new () { Scree = 0.5 };
            ErodedGround ground = Worn(before, erosion, width);
            double step = width / 100;
            double worn = 0;
            double laid = 0;
            double steepest = 0;

            for (int row = 0; row < 101; row++)
            {
                for (int column = 0; column < 101; column++)
                {
                    double change = ground.Heights[column, row] - before[column, row];

                    if (change > 0)
                        laid += change;
                    else
                        worn -= change;

                    if (column < 100 && ground.MapOf(ErosionMap.Deposits)[row * 101 + column] > 0.5 &&
                        ground.MapOf(ErosionMap.Deposits)[row * 101 + column + 1] > 0.5)
                    {
                        steepest = Math.Max(steepest,
                            Math.Abs(ground.Heights[column + 1, row] - ground.Heights[column, row]) / step);
                    }
                }
            }

            Assert.IsTrue(worn > 1000, $"the crag {width} m wide shed only {worn:F1} m of rock");
            Assert.AreEqual(worn, laid, worn * 1e-9, "scree was lost or made");
            Assert.IsTrue(At(ground.Heights, 0.45, 0.5) > 0, "nothing came to rest at the crag's foot");
            Assert.IsTrue(steepest <= Math.Tan(erosion.Repose) * 1.02,
                $"scree on a field {width} m wide rests at a slope of {steepest:F3}");
            Assert.IsTrue(steepest >= Math.Tan(erosion.Repose) * 0.8,
                $"scree on a field {width} m wide rests well short of its angle, at {steepest:F3}");
        }
    }

    /// <summary>
    /// Scree weathers as far at any sampling: in rounds by the grid's points across, so a finer grid, crossed in more
    /// rounds, has its crags fall back as far.
    /// </summary>
    [TestMethod]
    public void TestScreeShedsAsFarOnAFinerGrid()
    {
        AssertTheSameAtEitherSampling(new Erosion { Scree = 0.3 }, 0.25, Crag);
    }

    /// <summary>
    /// Rain wears its gullies above and lays its grit below: what was laid down lies lower than what was worn away,
    /// and never more of it, since grit is carried off the land's edges too.
    /// </summary>
    [TestMethod]
    public void TestRainWearsAboveAndLaysDownBelow()
    {
        double[,] before = Grid(129, Rolling);
        ErodedGround ground = Worn(before, new Erosion { Rain = 0.5 });
        double worn = 0;
        double laid = 0;
        double wornHeight = 0;
        double laidHeight = 0;

        for (int row = 0; row < 129; row++)
        {
            for (int column = 0; column < 129; column++)
            {
                double change = ground.Heights[column, row] - before[column, row];

                if (change > 0)
                {
                    laid += change;
                    laidHeight += change * before[column, row];
                }
                else
                {
                    worn -= change;
                    wornHeight -= change * before[column, row];
                }
            }
        }

        Assert.IsTrue(laid > 0 && worn > laid, $"rain wore {worn:F1} and laid {laid:F1}");
        Assert.IsTrue(laidHeight / laid < wornHeight / worn - 10,
            $"grit was laid at {laidHeight / laid:F1} m on average, and worn at {wornHeight / worn:F1} m");
    }

    /// <summary>
    /// A drop wears nothing below where it goes next.  Worn further, a hole by a drop's path deepened itself, each
    /// drop falling into it faster and wearing it deeper, until the ground ran tens of thousands of times as deep as
    /// it was tall: so the ground's lowest point never sinks.
    /// </summary>
    [TestMethod]
    public void TestRainDigsNoHoles()
    {
        double[,] before = Grid(257, Rolling);
        ErodedGround ground = Worn(before, new Erosion { Rain = 2 });
        double lowest = before.Cast<double>().Min();

        Assert.IsTrue(ground.Heights.Cast<double>().Min() >= lowest - 1e-9,
            $"rain dug to {ground.Heights.Cast<double>().Min():F1} m, below the land's lowest, {lowest:F1} m");
    }

    /// <summary>
    /// Rain falls on a grid of its own, so it wears the same land the same way at any sampling: on the field's own
    /// grid, a finer one wore gullies as much narrower, and as much deeper.
    /// </summary>
    [TestMethod]
    public void TestRainWearsAFinerGridAsACoarser()
    {
        AssertTheSameAtEitherSampling(new Erosion { Rain = 1 }, 0.25);
    }

    /// <summary>
    /// The same seed lets the same rain fall, drop for drop; another lets other rain fall.
    /// </summary>
    [TestMethod]
    public void TestRainFallsByItsSeed()
    {
        double[,] before = Grid(65, Rolling);
        ErodedGround first = new Erosion { Rain = 0.5, Seed = 3 }.Erode(before, Width, 1, Depth, 0);
        ErodedGround again = new ErodingGroundOf(before).Rained(0.5, 3);
        ErodedGround other = new Erosion { Rain = 0.5, Seed = 4 }.Erode(before, Width, 1, Depth, 0);

        CollectionAssert.AreEqual(first.Heights, again.Heights, "the same seed let other rain fall");
        CollectionAssert.AreNotEqual(first.Heights, other.Heights, "another seed let the same rain fall");
    }

    /// <summary>
    /// Water gathers going down: the flow at the valley's mouth is more than half way up it, which is more than on
    /// its side, which is next to nothing; and the map stays under one.
    /// </summary>
    [TestMethod]
    public void TestFlowGathersDownstream()
    {
        ErodedGround ground = Worn(Grid(65, Valley), new Erosion { Rivers = 0.1 });
        double mouth = MapAt(ground, ErosionMap.Flow, 0.5, 1.0 / 64);
        double middle = MapAt(ground, ErosionMap.Flow, 0.5, 0.5);
        double side = MapAt(ground, ErosionMap.Flow, 0.1, 0.5);

        Assert.IsTrue(mouth > middle && middle > 2 * side, $"flow at the mouth {mouth}, the middle {middle}, a side {side}");
        Assert.IsTrue(ground.Flow.All(value => value is >= 0 and < 1), "the flow map left nought to one");
    }

    /// <summary>
    /// A lake is one water: every point of it holds the flow leaving it.  Run across the lake as if over the level it
    /// was filled to, the flow drew a fan of straight lines across it from where it spills.
    /// </summary>
    [TestMethod]
    public void TestALakeIsOneWater()
    {
        static double Bowl(double x, double z)
        {
            double away = Math.Sqrt((x - 0.5) * (x - 0.5) + (z - 0.5) * (z - 0.5));

            return 100 + 200 * Math.Abs(z - 0.2) + (away < 0.25 ? -80 * (1 - away / 0.25) : 0);
        }

        float[] flow = new ErodingGround(Grid(65, Bowl), Width, 1, Depth, null).FlowMap();
        int middle = 32 * 65 + 32;

        Assert.AreEqual(flow[middle], flow[middle + 3], "the lake's flow was not one water");
        Assert.AreEqual(flow[middle], flow[middle - 3 * 65], "the lake's flow was not one water");
        Assert.IsTrue(flow[middle] > flow[middle + 20], "the lake holds no more water than its shore");
    }

    /// <summary>
    /// A ground worn once is not worn again: the same land, worn the same way, is handed back as it was, so an
    /// animation wears its land for its first frame only.
    /// </summary>
    [TestMethod]
    public void TestTheSameGroundIsWornOnce()
    {
        double[,] heights = Grid(33, Rolling);
        Erosion erosion = new () { Rivers = 0.2, Rain = 0.2 };

        Assert.AreSame(Worn(heights, erosion), Worn((double[,]) heights.Clone(), erosion),
            "the same land, worn the same way, was worn again");
        Assert.AreNotSame(Worn(heights, erosion), Worn(heights, new Erosion { Rivers = 0.2, Rain = 0.3 }),
            "land worn another way was handed back as the first");
    }

    /// <summary>
    /// A height field wears its ground at its size in the world, measured through its transforms: a curving ramp in
    /// its own units, stretched to a hundred meters by two hundred tall, steepens from level to a slope of four, and
    /// wears exactly as that ground does when said in the world -- not as the same ramp a meter each way, whose
    /// scree comes to rest elsewhere.  Its maps are read by the pattern on any part of it, and on nothing else.
    /// </summary>
    [TestMethod]
    public void TestAFieldWearsAtItsSizeInTheWorld()
    {
        Variables variables = new ();
        Term x = new VariableTerm(new IdToken("x"));
        Erosion erosion = new () { Scree = 0.3 };
        HeightField field = new ()
        {
            Function = new BinaryMultiplyOperation(x, x).ToField(variables),
            Samples = 33,
            Erosion = erosion,
            Closed = false,
            Transform = Transforms.Scale(100, 200, 100)
        };

        field.PrepareForRendering();

        double[,] ramp = Grid(33, (across, _) => across * across);
        ErodedGround expected = erosion.Erode(ramp, 100, 200, 100, 0);
        ErodedGround unstretched = erosion.Erode(ramp, 1, 1, 1, 0);

        CollectionAssert.AreNotEqual(expected.Deposits, unstretched.Deposits, "the ramp wore alike at either size");

        for (int row = 0; row < 33; row++)
        {
            for (int column = 0; column < 33; column++)
            {
                Point place = new (column / 32.0, 0, row / 32.0);

                Assert.AreEqual(expected.Deposits[row * 33 + column], field.MapAt(ErosionMap.Deposits, place), 1e-6,
                    "the field wore its ground at some other size than it stands in the world");
                Assert.AreEqual(expected.Wear[row * 33 + column], field.MapAt(ErosionMap.Wear, place), 1e-6,
                    "the field wore its ground at some other size than it stands in the world");
            }
        }

        Triangle triangle = field.Surfaces.OfType<Triangle>().First();
        ErosionPattern deposits = new () { Map = ErosionMap.Deposits };
        Point between = new (0.31, 0, 0.47);

        using (Pigment.WornBy(triangle))
        {
            Assert.AreEqual(field.MapAt(ErosionMap.Deposits, between), deposits.Evaluate(between), 1e-12,
                "the pattern did not read the field's map");
        }

        using (Pigment.WornBy(new Sphere()))
            Assert.AreEqual(0, deposits.Evaluate(between), "a map was read on something that is not a height field");
    }

    /// <summary>
    /// An erosion block is read, and what it may not say is refused, with a reason.
    /// </summary>
    [TestMethod]
    public void TestErosionIsReadAndRefused()
    {
        Assert.IsNull(Refusal("function { x * 0.3 } samples 17 erosion { rivers 0.2 scree 0.1 repose 33 rain 0.1 " +
                              "sea level 0.05 with seed 4 } material { pigment flow { [0, Red, 1, Blue] } }"));
        Assert.IsNull(Refusal("function { x * 0.3 } samples 17 erosion { rivers 0.2 } " +
                              "material { pigment deposits { [0, Red, 1, Blue] } }"));
        Assert.IsNull(Refusal("function { x * 0.3 } samples 17 erosion { scree 0.1 } " +
                              "material { pigment wear { [0, Red, 1, Blue] } }"));
        StringAssert.Contains(Refusal("function { x } erosion { }"), "at least one of");
        StringAssert.Contains(Refusal("function { x } erosion { rain 1 repose 30 }"), "needs \"scree\" or \"rivers\"");
        StringAssert.Contains(Refusal("function { x } erosion { rivers -1 }"), "less than nothing");
        StringAssert.Contains(Refusal("function { x } erosion { scree 1 repose 95 }"), "between level and sheer");
    }

    /// <summary>
    /// Wears the same land sampled two ways, and holds the wearing to being the same at the points the two share:
    /// the change at each, against the mean change.
    /// </summary>
    private static void AssertTheSameAtEitherSampling(
        Erosion erosion, double allowed, Func<double, double, double> height = null)
    {
        height ??= Rolling;

        double[,] coarse = Grid(65, height);
        double[,] fine = Grid(129, height);
        ErodedGround coarseWorn = Worn(coarse, erosion);
        ErodedGround fineWorn = Worn(fine, erosion);
        double differs = 0;
        double changes = 0;

        for (int row = 0; row < 65; row++)
        {
            for (int column = 0; column < 65; column++)
            {
                double one = coarseWorn.Heights[column, row] - coarse[column, row];
                double other = fineWorn.Heights[2 * column, 2 * row] - fine[2 * column, 2 * row];

                differs += Math.Abs(one - other);
                changes += Math.Abs(one);
            }
        }

        Assert.IsTrue(changes > 0, "nothing wore");
        Assert.IsTrue(differs / changes < allowed,
            $"the two samplings wore differently, by {differs / changes:P0} of what they wore");
    }

    /// <summary>
    /// Parses a scene holding a height field with the given body and draws it, returning what was said wrong, or
    /// <c>null</c> if nothing was.
    /// </summary>
    private static string Refusal(string body)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"erosion-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, "scene.igl");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(path,
            "camera { location [0.5, 2, -1]  look at [0.5, 0, 0.5] }\n" +
            "point light { location [0, 5, -5] }\n" +
            $"heightfield {{ {body} }}\n");
        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(directory, "out.png"), Width = 4, Height = 4
            });
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
            Directory.Delete(directory, true);
        }

        string said = captured.ToString();

        return said.Contains("Error") ? said : null;
    }

    /// <summary>
    /// Wears a ground by rain alone, straight through the ground being worn rather than the erosion that keeps what
    /// it wore, so asking twice really does let the rain fall twice.
    /// </summary>
    private sealed class ErodingGroundOf(double[,] heights)
    {
        internal ErodedGround Rained(double perPoint, int seed)
        {
            ErodingGround ground = new (heights, Width, 1, Depth, null);

            ground.FallAsRain(perPoint, seed);

            return ground.Finished();
        }
    }
}
