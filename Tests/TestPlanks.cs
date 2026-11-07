using RayTracer.Basics;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the planks pattern -- boards side by side, butted end to end, each its own shade, with rounded
/// seams between -- and the warp that bends a pattern's space, which is what lays a hull's planks along her sheer.
/// </summary>
[TestClass]
public class TestPlanks
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"planks-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A seam reads nought, a board its own number from a half to one, and across the seam's width the board rises
    /// smoothly out of it -- which is what lets a roughening take a slope across it at all.
    /// </summary>
    [TestMethod]
    public void TestASeamReadsNoughtAndABoardItsOwnNumber()
    {
        PlanksPattern planks = new () { PlankSize = new Vector(4, 0.2, 0.2), SeamSize = 0.02 };

        // Rows stand on nought, so the seams across Y lie half a board either side of it.
        Assert.AreEqual(0, planks.Evaluate(new Point(1.3, 0.1, 0)), 1e-12, "on the seam");

        double board = planks.Evaluate(new Point(1.3, 0, 0));

        Assert.IsTrue(board is >= 0.5 and <= 1, $"a board read {board}");
        Assert.AreEqual(board, planks.Evaluate(new Point(1.3, 0.085, 0)), 1e-12, "past the seam's width the board is flat");

        double rising = planks.Evaluate(new Point(1.3, 0.095, 0));

        Assert.IsTrue(rising > 0 && rising < board, $"halfway into the seam it read {rising} of {board}");
        Assert.AreEqual(board / 2, rising, 1e-9, "the rise is a smoothstep, a half at its middle");
    }

    /// <summary>
    /// A floor at nought, or a wall through it, lies along the middle of a row of boards.  Laid from a seam, as a
    /// brick's courses are, the commonest floor there is came out seam from end to end.
    /// </summary>
    [TestMethod]
    public void TestAFloorAtNoughtLiesAlongTheMiddleOfARow()
    {
        PlanksPattern planks = new () { PlankSize = new Vector(4, 0.15, 0.15), SeamSize = 0.01 };
        int seams = 0;

        for (int step = 0; step < 400; step++)
        {
            double x = step * 0.01;

            if (planks.Evaluate(new Point(x, 0, 0.0)) < 0.5)
                seams++;
        }

        // Only the butts, a seam's width each, where a row along X meets the next board.
        Assert.IsLessThanOrEqualTo(4, seams, $"{seams} of 400 places along a floor at nought were seam");
    }

    /// <summary>
    /// The butts of neighboring rows never stand close: each row is moved along by the golden ratio's share of a
    /// board from the one before.
    /// </summary>
    [TestMethod]
    public void TestButtsInNeighboringRowsStandApart()
    {
        PlanksPattern planks = new () { PlankSize = new Vector(1, 0.1, 0.1), SeamSize = 0.004 };
        List<double>[] butts = new List<double>[6];

        for (int row = 0; row < butts.Length; row++)
        {
            butts[row] = [];

            double previous = 1;

            // In steps of a twentieth of a seam, so that none is stepped over.
            for (int step = 0; step <= 15000; step++)
            {
                double x = step * 0.0002;
                double value = planks.Evaluate(new Point(x, row * 0.1, 0));

                if (value < 0.25 && previous >= 0.25)
                    butts[row].Add(x);

                previous = value;
            }

            Assert.IsGreaterThanOrEqualTo(2, butts[row].Count, $"row {row} had {butts[row].Count} butts in three boards");
        }

        for (int row = 1; row < butts.Length; row++)
        {
            foreach (double butt in butts[row])
            {
                double nearest = butts[row - 1].Min(other => Math.Abs(other - butt));

                Assert.IsGreaterThan(0.37, nearest, $"a butt in row {row} at {butt} stood {nearest} from one in the row before");
            }
        }
    }

    /// <summary>
    /// The boards differ: a deck of one shade throughout is what brick gave.
    /// </summary>
    [TestMethod]
    public void TestBoardsAreShadedOneFromAnother()
    {
        PlanksPattern planks = new () { PlankSize = new Vector(2, 0.15, 0.15), SeamSize = 0.01 };
        HashSet<double> shades = [];

        for (int row = 0; row < 20; row++)
            shades.Add(Math.Round(planks.Evaluate(new Point(0.5, row * 0.15, 0)), 6));

        Assert.IsGreaterThanOrEqualTo(15, shades.Count, $"twenty boards came in {shades.Count} shades");
        Assert.IsTrue(shades.All(shade => shade is >= 0.5 and <= 1), "a board's shade fell outside a half to one");
    }

    /// <summary>
    /// A warp hands the pattern the bent point: a coordinate it says is a function of the point, the rest as they are.
    /// </summary>
    [TestMethod]
    public void TestAWarpHandsThePatternTheBentPoint()
    {
        // y - x^2: rows bent up into a bowl.
        FieldExpression bowl = FieldArithmetic.Of(FieldOperator.Subtract, FieldVariable.Y,
            FieldArithmetic.Of(FieldOperator.Multiply, FieldVariable.X, FieldVariable.X));
        PlanksPattern straight = new () { PlankSize = new Vector(4, 0.2, 0.2), SeamSize = 0.02 };
        PlanksPattern bent = new ()
        {
            PlankSize = new Vector(4, 0.2, 0.2), SeamSize = 0.02, Warp = new Warp(null, bowl, null)
        };

        foreach (Point point in new[] { new Point(0.5, 0.3, 0.2), new Point(-1.2, 1.7, 0), new Point(2, 4.1, -3) })
        {
            Point there = new (point.X, point.Y - point.X * point.X, point.Z);

            Assert.AreEqual(straight.ValueFor(there), bent.ValueFor(point), 1e-12, $"at {point}");
        }

        // The seam half a board up at the middle is a full unit higher a unit out to either side.
        Assert.AreEqual(0, bent.ValueFor(new Point(0, 0.1, 0)), 1e-12);
        Assert.AreEqual(0, bent.ValueFor(new Point(1, 1.1, 0)), 1e-12);
        Assert.AreEqual(0, bent.ValueFor(new Point(-1, 1.1, 0)), 1e-12);
    }

    /// <summary>
    /// The patch a ray covers goes through the warp too, so a pattern that filters itself over it filters over the
    /// patch where it reads: squeezed tenfold, a seam a tenth the width of the patch is a tenth as much of it.
    /// </summary>
    [TestMethod]
    public void TestAWarpCarriesThePatchThroughTheBending()
    {
        Warp squeeze = new (null, FieldArithmetic.Of(FieldOperator.Multiply, new FieldConstant(10), FieldVariable.Y), null);
        Footprint patch = new (new Vector(0.1, 0, 0), new Vector(0, 0.1, 0));
        Footprint bent = squeeze.Bend(new Point(0.3, 0.2, 0), patch);

        Assert.AreEqual(0.1, bent.Across.Magnitude, 1e-12);
        Assert.AreEqual(1.0, bent.Along.Magnitude, 1e-12);

        // The pattern is handed the bent patch: filtering a squeezed pattern over a small patch is filtering the
        // straight one over the patch ten times as tall, where it reads.  Here that patch reaches across the seam a
        // twentieth of a unit up, which the patch as it was would not.
        PlanksPattern straight = new () { PlankSize = new Vector(40, 0.5, 0.5), SeamSize = 0.05 };
        PlanksPattern squeezed = new ()
        {
            PlankSize = new Vector(40, 0.5, 0.5), SeamSize = 0.05, Warp = squeeze
        };
        Point point = new (0.3, 0.02, 0);
        Footprint small = new (new Vector(0.02, 0, 0), new Vector(0, 0.02, 0));
        Point there = squeeze.Bend(point);

        Assert.AreEqual(straight.ValueFor(there, squeeze.Bend(point, small)), squeezed.ValueFor(point, small), 1e-12,
            "the squeezed pattern was not filtered over the bent patch");
        Assert.AreNotEqual(straight.ValueFor(there, small), squeezed.ValueFor(point, small),
            "the patch as it was gave the same answer, so nothing here was tested");
    }

    /// <summary>
    /// A scene writes a warp in braces, each coordinate it bends fenced as a parametric surface's are, beside the
    /// turbulence in either order; and a plank's sizes either side of the colors, as a brick's.
    /// </summary>
    [TestMethod]
    public void TestAWarpAndPlankSizesAreWrittenAsABricksAreAndInEitherOrder()
    {
        const string Colors = "[0, Black, 0.5, Tan, 1, Sienna]";
        const string Sizes = "plank size [1, 0.12, 0.12]  seam size 0.02";
        const string Bend = "warp { Y { y - 0.3 * x * x } }";

        (Canvas plain, string plainError) = WallWearing($"pigment planks {{ {Sizes}  {Colors} }}");
        (Canvas before, string beforeError) = WallWearing($"pigment planks {{ {Sizes}  {Bend}  {Colors} }}");
        (Canvas after, string afterError) = WallWearing($"pigment planks {{ {Bend}  {Colors}  {Sizes} }}");
        (Canvas stirred, string stirredError) = WallWearing(
            $"pigment planks {{ {Sizes}  {Bend}  turbulence 0  {Colors} }}");
        (Canvas stirredFirst, string stirredFirstError) = WallWearing(
            $"pigment planks {{ {Sizes}  turbulence 0  {Bend}  {Colors} }}");

        Assert.IsNull(plainError, plainError);
        Assert.IsNull(beforeError, beforeError);
        Assert.IsNull(afterError, afterError);
        Assert.IsNull(stirredError, stirredError);
        Assert.IsNull(stirredFirstError, stirredFirstError);

        int different = 0;

        for (int y = 0; y < plain.Height; y++)
        {
            for (int x = 0; x < plain.Width; x++)
            {
                Assert.IsTrue(after.GetPixel(x, y).Matches(before.GetPixel(x, y)), $"sizes after the colors at ({x}, {y})");
                Assert.IsTrue(stirred.GetPixel(x, y).Matches(before.GetPixel(x, y)), $"warp then turbulence at ({x}, {y})");
                Assert.IsTrue(stirredFirst.GetPixel(x, y).Matches(before.GetPixel(x, y)), $"turbulence then warp at ({x}, {y})");

                if (!plain.GetPixel(x, y).Matches(before.GetPixel(x, y)))
                    different++;
            }
        }

        Assert.IsGreaterThan(plain.Width * plain.Height / 10, different, "the warp bent almost nothing");
    }

    /// <summary>
    /// A warp says each coordinate once, and at least one.
    /// </summary>
    [TestMethod]
    public void TestAWarpSaysEachCoordinateOnceAndAtLeastOne()
    {
        string twice = WallWearing("pigment planks { warp { Y { y }  Y { y + 1 } }  [0, Black, 1, Tan] }").Error;
        string none = WallWearing("pigment planks { warp { }  [0, Black, 1, Tan] }").Error;

        Assert.IsNotNull(twice);
        StringAssert.Contains(twice, "says what Y is once");
        Assert.IsNotNull(none);
        StringAssert.Contains(none, "at least one of X, Y and Z");
    }

    /// <summary>
    /// A warp bends whatever a pattern is read for: a roughening's slope is taken across the bent seams, so the grooves
    /// a warped planks normal cuts follow the warp.
    /// </summary>
    [TestMethod]
    public void TestAWarpBendsARougheningAsItBendsAPigment()
    {
        (Canvas straight, string straightError) = WallWearing(
            "pigment Tan  normal planks { plank size [100, 0.25, 0.25]  seam size 0.06  depth 0.6  rotate X 90 }", lit: true);
        (Canvas bent, string bentError) = WallWearing(
            "pigment Tan  normal planks { plank size [100, 0.25, 0.25]  seam size 0.06  warp { Y { y - 0.25 * x * x } }  " +
            "depth 0.6  rotate X 90 }", lit: true);

        Assert.IsNull(straightError, straightError);
        Assert.IsNull(bentError, bentError);

        // Straight, the grooves cross the picture level: a column near the side shows them at the same rows as the
        // middle one.  Bent, they sag in the middle: a groove a unit up at the middle stands 0.14 higher three quarters
        // of the way out, four and a half pixels of the sixty-four the wall's two units fill.
        int middle = straight.Width / 2;
        int side = straight.Width / 8;

        Assert.AreEqual(0, ShiftBetween(straight, middle, side), 1, "the straight grooves should run level");
        Assert.AreEqual(-4.5, ShiftBetween(bent, middle, side), 1.5, "the bent grooves should stand higher at the side");

        // How far the side column's grooves stand below the middle's, in rows: the shift lining up the two columns'
        // changes from row to row best, which are the grooves' edges whatever the light does across the wall.
        static int ShiftBetween(Canvas image, int middle, int side)
        {
            int best = 0;
            double least = double.MaxValue;

            for (int shift = -10; shift <= 10; shift++)
            {
                double apart = 0;

                for (int y = 12; y < image.Height - 13; y++)
                    apart += Math.Abs(Change(image, middle, y) - Change(image, side, y + shift));

                if (apart < least)
                {
                    least = apart;
                    best = shift;
                }
            }

            return best;
        }

        static double Change(Canvas image, int column, int y) =>
            image.GetPixel(column, y + 1).Red - image.GetPixel(column, y).Red;
    }

    /// <summary>
    /// Renders a wall square to the camera, a unit either side of the middle, wearing the given material's insides,
    /// and hands back the picture or the error that stopped it.  The wall is a plane turned upright, so a pigment that
    /// wants the world's own coordinates says <c>rotate X 90</c>; unlit, the wall shows its pigment alone.
    /// </summary>
    private (Canvas Image, string Error) WallWearing(string insides, bool lit = false)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");
        string finish = lit ? "  specular 0" : "  ambient 1  diffuse 0  specular 0";

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 22.62 }\n" +
            "point light { location [0, 6, -4] }\n" +
            $"plane {{ rotate X -90  material {{ {insides}{finish} }} }}");

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions { OutputFileName = output, Width = 64, Height = 64 });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
        }
    }
}
