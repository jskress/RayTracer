using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover rigging: a cable hung between two places as a catenary, a rope laid of strands along a spline,
/// a chain of links along one, and a tube along one.
/// </summary>
[TestClass]
public class TestRigging
{
    private static readonly Point Left = new (-2, 3, 0);
    private static readonly Point Right = new (3, 2.2, 1.5);

    /// <summary>
    /// A catenary hung by its sag runs from its first end to its second, and its middle -- halfway along the span --
    /// hangs as far below the straight line between them as it was told; hung by its length, it is that long.
    /// </summary>
    [TestMethod]
    public void TestACatenaryHangsByItsSagOrItsLength()
    {
        Spline bySag = Catenary.Hung(Left, Right, 0.6, null);
        Spline byLength = Catenary.Hung(Left, Right, null, 8);
        List<ISplineCurve> curves = bySag.GetCurves();

        Assert.IsTrue(bySag.Start.Matches(Left), "the catenary did not start where it was hung from");
        Assert.IsTrue(bySag.Segments[^1].End.Matches(Right), "the catenary did not end where it was hung to");

        // Halfway along the span, found by walking the curve to where it is halfway across.
        Vector across = new (Right.X - Left.X, 0, Right.Z - Left.Z);
        double Across(Point point) => (point - Left).Dot(across.Unit);
        double low = 0, high = bySag.Measure.Length;

        for (int round = 0; round < 60; round++)
        {
            double middle = (low + high) / 2;

            if (Across(bySag.Measure.PointAt(middle)) < across.Magnitude / 2)
                low = middle;
            else
                high = middle;
        }

        Point halfway = bySag.Measure.PointAt(low);

        Assert.AreEqual((Left.Y + Right.Y) / 2 - 0.6, halfway.Y, 1e-5, "the catenary's middle hung the wrong depth");
        Assert.AreEqual(8, byLength.Measure.Length, 8 * 1e-4, "the catenary was not as long as it was told");
        Assert.IsTrue(byLength.Start.Matches(Left) && byLength.Segments[^1].End.Matches(Right));

        foreach (ISplineCurve curve in curves)
            Assert.IsTrue(curve.GetPoint(0.5).Y < Math.Max(Left.Y, Right.Y), "the catenary rose above its higher end");
    }

    /// <summary>
    /// A catenary's spline lies on the exact curve, to within a few millionths of its length: every place along it is
    /// as high above the lowest as a cosh says for its distance across from there, the shape a found from the curve's
    /// own length and span.
    /// </summary>
    [TestMethod]
    public void TestACatenaryIsTheCurveAChainHangsIn()
    {
        // Ends level, so the lowest place is the middle.
        Point from = new (0, 5, 0), to = new (10, 5, 0);
        Spline spline = Catenary.Hung(from, to, null, 12);

        // The length of a level catenary is 2a sinh(span / 2a): found again here by bisection, independently.
        double low = 0.1, high = 100;

        for (int round = 0; round < 200; round++)
        {
            double a = (low + high) / 2;

            if (2 * a * Math.Sinh(5 / a) > 12)
                low = a;
            else
                high = a;
        }

        double shape = (low + high) / 2;
        double bottom = 5 - shape * (Math.Cosh(5 / shape) - 1);

        foreach (ISplineCurve curve in spline.GetCurves())
        {
            for (int sample = 0; sample <= 10; sample++)
            {
                Point at = curve.GetPoint(sample / 10.0);
                double expected = bottom + shape * (Math.Cosh((at.X - 5) / shape) - 1);

                // Within three millionths of the cable's length.
                Assert.AreEqual(expected, at.Y, 12 * 3e-6, $"the catenary's spline left the curve at x = {at.X}");
                Assert.AreEqual(0, at.Z, 1e-12);
            }
        }
    }

    /// <summary>
    /// What cannot hang is refused: neither sag nor length or both, a cable no longer than the distance it spans,
    /// and ends one straight above the other.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotHangIsRefused()
    {
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() => Catenary.Hung(Left, Right, null, null)).Message,
            "sag or by its length");
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() => Catenary.Hung(Left, Right, 1, 9)).Message,
            "sag or by its length");
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() => Catenary.Hung(Left, Right, null, 5)).Message,
            "longer than the distance");
        StringAssert.Contains(
            Assert.ThrowsExactly<Exception>(() => Catenary.Hung(Point.Zero, new Point(0, 3, 0), 0.5, null)).Message,
            "straight above");
    }

    // A rope or chain's spline: a quarter circle in the X/Y plane, three across, then a straight run down -Y, so the
    // tests see a bend and a run hanging straight down.
    private static Spline Bend()
    {
        const double K = 0.5522847498307936 * 3;

        return new Spline
        {
            Start = new Point(0, 3, 0),
            Discontinuous = false,
            Segments =
            {
                new SplineSegmentSpec
                {
                    Control1 = new Point(K, 3, 0), Control2 = new Point(3, K, 0), End = new Point(3, 0, 0)
                },
                new SplineSegmentSpec { End = new Point(3, -3, 0) }
            }
        };
    }

    // How far a place is from the bend's middle line: off the quarter circle about the origin, or off the straight
    // run down from its end.
    private static double OffTheBend(Point at)
    {
        if (at.Y >= 0)
        {
            double off = Math.Sqrt(at.X * at.X + at.Y * at.Y) - 3;

            return Math.Sqrt(off * off + at.Z * at.Z);
        }

        return Math.Sqrt((at.X - 3) * (at.X - 3) + at.Z * at.Z);
    }

    /// <summary>
    /// A rope's strands touch about its middle: each as thick as fits so many alike round a middle, every piece of
    /// every strand lying that far less from the spline, and each strand turning once about it in a lay -- round the
    /// bend and down the straight run alike.
    /// </summary>
    [TestMethod]
    public void TestARopesStrandsAreLaidAboutItsSpline()
    {
        Rope rope = new () { Spline = Bend(), Radius = 0.1, Strands = 3, Lay = 0.6 };

        rope.PrepareForRendering();

        List<TubeSegment> pieces = rope.Surfaces.OfType<TubeSegment>().ToList();
        double strand = 0.1 * Math.Sin(Math.PI / 3) / (1 + Math.Sin(Math.PI / 3));

        Assert.IsTrue(pieces.Count > 3 * 100, $"the rope was laid from only {pieces.Count} pieces");

        foreach (TubeSegment piece in pieces)
        {
            Assert.AreEqual(strand, piece.StartRadius, 1e-12, "a strand was not as thick as fits three");
            Assert.AreEqual(0.1 - strand, OffTheBend(piece.Start), 0.002, $"a strand at {piece.Start} stood off the rope");
        }

        // Down the straight run, a strand's place about the middle turns once in a lay: its angle about the run,
        // measured at every piece, goes round 2 pi in 0.6 of a fall.
        List<TubeSegment> straight = pieces.Where(piece => piece.Start.Y < -0.5 && piece.End.Y < -0.5).ToList();
        TubeSegment first = straight.OrderByDescending(piece => piece.Start.Y).First();
        double Angle(Point at) => Math.Atan2(at.Z, at.X - 3);
        double turned = Angle(first.End) - Angle(first.Start);

        turned = Math.Atan2(Math.Sin(turned), Math.Cos(turned));

        Assert.AreEqual(2 * Math.PI / 0.6 * (first.Start.Y - first.End.Y), Math.Abs(turned), 0.02,
            "a strand did not turn once in a lay");
    }

    /// <summary>
    /// A chain's links are its own size, hooked one through the next an inner length apart, each a quarter turned from
    /// the last -- along the bend and hanging straight down alike -- and all of them the one shared shape.
    /// </summary>
    [TestMethod]
    public void TestAChainsLinksHookThroughEachOther()
    {
        Chain chain = new () { Spline = Bend(), Link = 0.3, Wire = 0.05, Width = 0.18 };

        chain.PrepareForRendering();

        List<Instance> links = chain.Surfaces.OfType<Instance>().ToList();
        double length = Bend().Measure.Length;

        Assert.AreEqual((int) Math.Round(length / (0.3 - 2 * 0.05)), links.Count, "the chain held the wrong number of links");
        Assert.AreEqual(1, links.Select(link => link.Prototype).Distinct().Count(), "the links were not one shared shape");

        BoundingBox box = Surface.BoxAround(links[0].Prototype, true);

        Assert.AreEqual(0.3, box.Maximum.X - box.Minimum.X, 1e-3, "a link was not as long as it was told");
        Assert.AreEqual(0.18, box.Maximum.Y - box.Minimum.Y, 1e-3, "a link was not as wide as it was told");
        Assert.AreEqual(0.05, box.Maximum.Z - box.Minimum.Z, 1e-3, "a link's bar was not as thick as it was told");

        for (int index = 1; index < links.Count; index++)
        {
            Point here = links[index - 1].Transform * Point.Zero, next = links[index].Transform * Point.Zero;
            Vector across = (links[index - 1].Transform * new Vector(0, 1, 0)).Unit;
            Vector nextAcross = (links[index].Transform * new Vector(0, 1, 0)).Unit;

            Assert.AreEqual(length / links.Count, (next - here).Magnitude, 0.01, "two links stood the wrong way apart");
            Assert.AreEqual(0, across.Dot(nextAcross), 0.02, "a link was not a quarter turned from the last");
        }
    }

    /// <summary>
    /// The scene language: a catenary named and hung, a tube along it, a rope and a chain along it; a catenary hung in
    /// place of a spline, by a primitive, a tube and a sweep; and what cannot be refused as the scene is read.
    /// </summary>
    /// <summary>
    /// A strand along a spline runs along the very curve the spline is: a ray square to the strand's plane meets it
    /// within half its width of any place along a catenary, and misses it past that; a spline's lines and
    /// quadratics, raised to cubics, are the same curves still.
    /// </summary>
    [TestMethod]
    public void TestAStrandRunsAlongTheVeryCurveOfItsSpline()
    {
        Spline hung = Catenary.Hung(new Point(-2, 1, 0), new Point(2, 1, 0), 0.5, null);
        Spline bent = new () { Start = new Point(-2, 0, 0) };

        bent.Segments.Add(new SplineSegmentSpec { End = new Point(0, 0, 0) });
        bent.Segments.Add(new SplineSegmentSpec { Control1 = new Point(1, 0, 0), End = new Point(1, 1, 0) });

        // Hit, or missed, by a ray along +Z through a place a share of a width off the curve, square across it.
        static int Crossings(Strand strand, Point at, Vector heading, double off)
        {
            Vector across = new Vector(-heading.Y, heading.X, 0).Unit;
            Point from = at + across * (off * strand.Width);
            List<Intersection> hits = [];

            strand.AddIntersections(new Ray(new Point(from.X, from.Y, -5), new Vector(0, 0, 1)), hits);

            return hits.Count;
        }

        Strand cable = new () { Spline = hung, Width = 0.02 };
        Strand line = new () { Spline = bent, Width = 0.02 };

        cable.PrepareForRendering();
        line.PrepareForRendering();

        SplineMeasure measure = hung.Measure;

        for (int place = 1; place < 10; place++)
        {
            double along = measure.Length * place / 10;
            Point at = measure.PointAt(along);
            Vector heading = measure.HeadingAt(along);

            Assert.AreEqual(1, Crossings(cable, at, heading, 0), $"on the curve at {place}");
            Assert.AreEqual(1, Crossings(cable, at, heading, 0.4), $"inside half its width at {place}");
            Assert.AreEqual(0, Crossings(cable, at, heading, 0.6), $"past half its width at {place}");
        }

        // Halfway along the line, and the quadratic's middle, a quarter of each of its ends and half its control.
        Assert.AreEqual(1, Crossings(line, new Point(-1, 0, 0), new Vector(1, 0, 0), 0.4));
        Assert.AreEqual(0, Crossings(line, new Point(-1, 0, 0), new Vector(1, 0, 0), 0.6));
        Assert.AreEqual(1, Crossings(line, new Point(0.75, 0.25, 0), new Vector(1, 1, 0), 0.4));
        Assert.AreEqual(0, Crossings(line, new Point(0.75, 0.25, 0), new Vector(1, 1, 0), 0.6));
    }

    [TestMethod]
    public void TestTheSceneLanguageRigsThings()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rigging-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string Run(string body)
            {
                string scene = Path.Combine(directory, "scene.igl");
                StringWriter captured = new ();
                TextWriter was = Console.Out;

                File.WriteAllText(scene,
                    "camera { location [0, 1.5, -6]  look at [0, 1.2, 0] }\n" +
                    "point light { location [-4, 6, -6] }\n" + body);
                Console.SetOut(captured);

                try
                {
                    new LanguageParser(scene).Parse()?.Render(new RenderOptions
                    {
                        OutputFileName = Path.Combine(directory, "scene.png"), Width = 8, Height = 6
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
                Line = catenary { from [-2, 2, 0]  to [2, 2.2, 0]  sag 0.4 }
                Slack = catenary { from [-2, 1.5, 0]  to [2, 1.5, 0]  length 4.6 }
                tube { spline Line radius 0.01 }
                rope { spline Slack  radius 0.03  strands 4  lay 0.2  material { pigment [0.6, 0.5, 0.3] } }
                chain { spline Line  link 0.06  wire 0.01  width 0.03 }
                chain { spline { move to -2, 0.5, 0  line to 2, 0.5, 0 }  link 0.08 }
                strand { spline Line  width 0.004  material { metal 'iron'  roughness 0.5 } }
                strand { spline catenary { from [-2, 2.5, 0]  to [2, 2.5, 0]  sag 0.2 }  width 0.004 }
                """));
            Assert.IsNull(Run("""
                primitive Mooring(from, to, sag) -> rope {
                    return rope { spline catenary { from from  to to  sag sag }  radius 0.02 }
                }
                object Mooring([-2, 1, 0], [2, 1, 0], 0.3)
                tube { spline catenary { from [-2, 0.5, 0]  to [2, 0.6, 0]  length 4.2 } radius 0.01 }
                sweep {
                    profile { move to -0.02, -0.02  line to 0.02, -0.02  line to 0.02, 0.02  line to -0.02, 0.02  close }
                    spline catenary { from [-2, 0.3, 0]  to [2, 0.3, 0]  sag 0.1 }
                }
                """));
            StringAssert.Contains(Run("Line = catenary { from [-2, 2, 0]  to [2, 2, 0] }\ntube { spline Line radius 0.01 }"),
                "sag or by its length");
            StringAssert.Contains(Run("Line = catenary { from [-2, 2, 0]  to [2, 2, 0]  sag 0.2 }\ntube { spline Line }"),
                "radius");
            StringAssert.Contains(Run("Line = catenary { from [-2, 2, 0]  to [2, 2, 0]  sag 0.2 }\nrope { spline Line  strands 1 }"),
                "at least two strands");
            StringAssert.Contains(Run("Line = catenary { from [-2, 2, 0]  to [2, 2, 0]  sag 0.2 }\nchain { spline Line  link 0.2  wire 0.03  width 0.05 }"),
                "wider than two");
            StringAssert.Contains(Run("Line = catenary { from [-2, 2, 0]  to [2, 2, 0]  sag 0.2 }\nchain { spline Line  link 0.05  wire 0.03 }"),
                "no shorter than it is wide");
            StringAssert.Contains(Run("rope { radius 0.02 }"), "needs a spline");
            StringAssert.Contains(Run("Line = catenary { from [-2, 2, 0]  to [2, 2, 0]  sag 0.2 }\nstrand { spline Line }"),
                "width");
            StringAssert.Contains(
                Run("Line = catenary { from [-2, 2, 0]  to [2, 2, 0]  sag 0.2 }\n" +
                    "strand { width 0.01 at [-1, 0, 0]  width 0.01 at [1, 0, 0]  spline Line  width 0.01 }"),
                "not both");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
