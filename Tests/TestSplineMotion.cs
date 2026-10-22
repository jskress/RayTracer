using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions.Transforms;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests cover following a spline: measuring it by distance, putting a thing along it -- upright, banked, or
/// riding on a wheelbase -- the functions that ask where along it a share lies, and the scene language for them.
/// </summary>
[TestClass]
public class TestSplineMotion
{
    /// <summary>
    /// This class is a term worked out from the variables, standing in for an expression.
    /// </summary>
    private sealed class Given(Func<Variables, object> value) : Term(null)
    {
        protected override object Evaluate(Variables variables, params Type[] targetTypes) => value(variables);
    }

    private static Spline Line(Point from, Point to) => new ()
    {
        Start = from, Segments = { new SplineSegmentSpec { End = to } }
    };

    // A level circle about the origin, drawn as four quarter-circle cubics, starting on -Z and running toward +X.
    private static Spline Circle(double radius)
    {
        double arm = radius * 0.5523;

        return new Spline
        {
            Start = new Point(0, 0, -radius),
            Segments =
            {
                new SplineSegmentSpec
                {
                    Control1 = new Point(arm, 0, -radius), Control2 = new Point(radius, 0, -arm), End = new Point(radius, 0, 0)
                },
                new SplineSegmentSpec
                {
                    Control1 = new Point(radius, 0, arm), Control2 = new Point(arm, 0, radius), End = new Point(0, 0, radius)
                },
                new SplineSegmentSpec
                {
                    Control1 = new Point(-arm, 0, radius), Control2 = new Point(-radius, 0, arm), End = new Point(-radius, 0, 0)
                },
                new SplineSegmentSpec
                {
                    Control1 = new Point(-radius, 0, -arm), Control2 = new Point(-arm, 0, -radius), End = new Point(0, 0, -radius)
                }
            }
        };
    }

    private static Matrix Along(Spline spline, double amount, bool byDistance = false, bool banked = false,
        double? wheelbase = null, double? speed = null)
    {
        Variables variables = new ();

        variables.SetValue("time", 1.0);

        AlongCreator creator = new ()
        {
            SplineTerm = new Given(_ => spline),
            AmountTerm = speed is null
                ? new Given(_ => amount)
                : new Given(scope => (double) scope.GetValue("time", typeof(double)) * speed.Value),
            ByDistance = byDistance,
            Banked = banked,
            WheelbaseTerm = wheelbase is null ? null : new Given(_ => wheelbase.Value)
        };

        return creator.Resolve(new RenderContext(), variables);
    }

    private static void AreNear(NumberTuple expected, NumberTuple actual, double within, string what)
    {
        Assert.AreEqual(expected.X, actual.X, within, $"{what}'s X");
        Assert.AreEqual(expected.Y, actual.Y, within, $"{what}'s Y");
        Assert.AreEqual(expected.Z, actual.Z, within, $"{what}'s Z");
    }

    /// <summary>
    /// A spline is measured by distance: a straight one is its length, a place a share along it is that share of the
    /// way, and a circle drawn as cubics is its circumference near enough, its places evenly spaced round it.
    /// </summary>
    [TestMethod]
    public void TestASplineIsMeasuredByDistance()
    {
        SplineMeasure line = Line(new Point(0, 0, 0), new Point(10, 0, 0)).Measure;

        Assert.AreEqual(10, line.Length, 1e-9);
        AreNear(new Point(2.5, 0, 0), line.PointAt(2.5), 1e-9, "a quarter along");
        AreNear(new Vector(1, 0, 0), line.HeadingAt(2.5), 1e-9, "the heading");
        AreNear(new Vector(0, 0, 0), line.BendAt(2.5), 1e-9, "a straight's bend");

        SplineMeasure circle = Circle(20).Measure;

        Assert.AreEqual(2 * Math.PI * 20, circle.Length, 2 * Math.PI * 20 * 1e-3, "a circle's length");

        for (int step = 0; step < 16; step++)
        {
            double from = step * circle.Length / 16;
            double chord = (circle.PointAt(from + circle.Length / 16) - circle.PointAt(from)).Magnitude;

            Assert.AreEqual(2 * 20 * Math.Sin(Math.PI / 16), chord, 0.01, $"the step from {from}");
            Assert.AreEqual(20, (circle.PointAt(from) - Point.Zero).Magnitude, 0.01, $"the place at {from}");
        }

        // Round a bend, the heading turns by the curvature, toward the middle.
        Vector bend = circle.BendAt(circle.Length / 8);
        Point place = circle.PointAt(circle.Length / 8);

        Assert.AreEqual(1 / 20.0, bend.Magnitude, 0.001, "the curvature");
        Assert.IsTrue(bend.Unit.Dot((Point.Zero - place).Unit) > 0.999, "the bend should turn toward the middle");
    }

    /// <summary>
    /// A spline that ends where it began is a loop, and a distance past its end begins round it again, as does one
    /// before its start; any other holds at its ends.
    /// </summary>
    [TestMethod]
    public void TestALoopBeginsAgainAndAnOpenSplineHolds()
    {
        SplineMeasure circle = Circle(5).Measure;
        SplineMeasure line = Line(new Point(0, 0, 0), new Point(4, 0, 0)).Measure;

        Assert.IsTrue(circle.Loops);
        Assert.IsFalse(line.Loops);
        AreNear(circle.PointAt(1), circle.PointAt(circle.Length + 1), 1e-9, "once round and a little");
        AreNear(circle.PointAt(circle.Length - 1), circle.PointAt(-1), 1e-9, "a little before the start");
        AreNear(new Point(0, 0, 0), line.PointAt(-3), 1e-9, "before the start");
        AreNear(new Point(4, 0, 0), line.PointAt(9), 1e-9, "past the end");
    }

    /// <summary>
    /// A thing along a spline stands with its origin on it, its +X the way the spline runs and its +Y as near the
    /// world's up as the spline lets it -- climbing a slope, it pitches with it but does not roll -- and placed by a
    /// share or by a distance, it stands in the same place.
    /// </summary>
    [TestMethod]
    public void TestAThingAlongASplineStandsOnItFacingItsWay()
    {
        Spline slope = Line(new Point(0, 0, 0), new Point(10, 10, 0));
        Matrix halfway = Along(slope, 0.5);
        double root = Math.Sqrt(0.5);

        AreNear(new Point(5, 5, 0), halfway * Point.Zero, 1e-9, "the origin");
        AreNear(new Vector(root, root, 0), halfway * new Vector(1, 0, 0), 1e-9, "+X");
        AreNear(new Vector(-root, root, 0), halfway * new Vector(0, 1, 0), 1e-9, "+Y");
        AreNear(new Vector(0, 0, 1), halfway * new Vector(0, 0, 1), 1e-9, "+Z");
        Assert.IsTrue(Along(slope, Math.Sqrt(200) / 2, byDistance: true).Matches(halfway), "by distance");
        Assert.IsTrue(Along(Line(new Point(0, 0, 0), new Point(10, 0, 0)), 0.5)
            .Matches(RayTracer.Basics.Transforms.Translate(5, 0, 0)), "along a level line is a move");
    }

    /// <summary>
    /// A banked thing leans into a bend by as much as a rider going round it at its speed would feel: on a level
    /// circle of radius twenty at fourteen meters a second, by the arctangent of v squared over r g, forty-five
    /// degrees, toward the middle; standing still, or upright, not at all.
    /// </summary>
    [TestMethod]
    public void TestABankedThingLeansIntoTheBendAsFastAsItGoes()
    {
        Spline circle = Circle(20);
        Matrix banked = Along(circle, 0, byDistance: true, banked: true, speed: 14);
        Vector up = banked * new Vector(0, 1, 0);
        Point place = banked * Point.Zero;
        double expected = Math.Atan(14 * 14 / (20 * AlongCreator.Gravity)) * 180 / Math.PI;

        Assert.AreEqual(expected, Math.Acos(up.Y) * 180 / Math.PI, 1.5, "the lean");
        Assert.IsTrue(up.Dot(Point.Zero - place) > 0, "it should lean toward the middle of the bend");

        foreach (Matrix level in new[] { Along(circle, 0.3, banked: true), Along(circle, 0, byDistance: true, speed: 14) })
            Assert.AreEqual(1, (level * new Vector(0, 1, 0)).Y, 1e-9, "standing still or upright, no lean");
    }

    /// <summary>
    /// With a wheelbase, both ends of a thing ride on the spline: round a circle, its two ends each lie on the
    /// circle, where without one its ends swing off it.
    /// </summary>
    [TestMethod]
    public void TestAWheelbaseKeepsBothEndsOnTheSpline()
    {
        Spline circle = Circle(20);
        Matrix riding = Along(circle, 0.1, wheelbase: 7);
        Matrix standing = Along(circle, 0.1);

        foreach (double end in new[] { -3.5, 3.5 })
        {
            Assert.AreEqual(20, (riding * new Point(end, 0, 0) - Point.Zero).Magnitude, 0.02, $"the end at {end}");
            Assert.AreEqual(Math.Sqrt(20 * 20 + 3.5 * 3.5), (standing * new Point(end, 0, 0) - Point.Zero).Magnitude,
                0.02, $"the unbased end at {end}");
        }
    }

    /// <summary>
    /// Renders a small scene and hands back its picture, or fails with what stopped it.
    /// </summary>
    private static (Canvas Picture, string Error) Rendered(string directory, string body)
    {
        string scene = Path.Combine(directory, "scene.igl");
        string output = Path.Combine(directory, "scene.png");
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(scene,
            "context { angles are degrees  no gamma }\n" +
            "camera { location [5, 6, -9]  look at [5, 0, 0] }\n" +
            "point light { location [-4, 9, -9] }\n" +
            "Line = spline { move to 0, 0, 0  line to 10, 0, 0 }\n" + body);
        Console.SetOut(captured);

        try
        {
            new LanguageParser(scene).Parse()?.Render(new RenderOptions { OutputFileName = output, Width = 24, Height = 18 });
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString().Contains("Error")
            ? (null, captured.ToString())
            : (new ImageFile(output).Load()[0], null);
    }

    private static bool Same(Canvas one, Canvas other)
    {
        for (int x = 0; x < one.Width; x++)
        {
            for (int y = 0; y < one.Height; y++)
            {
                if (!one.GetPixel(x, y).Matches(other.GetPixel(x, y)))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The scene language: a spline named and followed -- a thing along it by a share or by a distance, upright,
    /// banked or on a wheelbase, placed by where `pointAlong` says, a sweep carried along it by its name -- and what
    /// cannot be followed refused.
    /// </summary>
    [TestMethod]
    public void TestTheSceneLanguageFollowsASpline()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"spline-motion-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            (Canvas moved, string error) = Rendered(directory, "sphere { translate [5, 0, 0] }");

            Assert.IsNull(error);

            foreach (string placed in new[]
                     {
                         "sphere { along Line at 0.5 }", "sphere { along Line by 5 upright }",
                         "sphere { translate pointAlong(Line, 0.5) }",
                         "sphere { along Line at 0.5 banked  wheelbase 2 }",
                         "sphere { translate [length(Line) / 2, 0, 0] }"
                     })
            {
                (Canvas picture, string problem) = Rendered(directory, placed);

                Assert.IsNull(problem, placed);
                Assert.IsTrue(Same(moved, picture), $"{placed} should stand where translate [5, 0, 0] does");
            }

            (Canvas swept, string sweptError) = Rendered(directory, """
                sweep {
                    profile { move to -0.2, -0.2  line to 0.2, -0.2  line to 0.2, 0.2  line to -0.2, 0.2  close }
                    spline Line
                }
                """);
            (Canvas written, string writtenError) = Rendered(directory, """
                sweep {
                    profile { move to -0.2, -0.2  line to 0.2, -0.2  line to 0.2, 0.2  line to -0.2, 0.2  close }
                    spline { move to 0, 0, 0  line to 10, 0, 0 }
                }
                """);

            Assert.IsNull(sweptError);
            Assert.IsNull(writtenError);
            Assert.IsTrue(Same(swept, written), "a sweep along a spline by name is the sweep along it written out");

            StringAssert.Contains(Rendered(directory, "sphere { motion { along Line at 0.5 } }").Error, "has no share");
            StringAssert.Contains(Rendered(directory, "Number = 3\nsphere { along Number at 0.5 }").Error, "must be a spline");
            StringAssert.Contains(Rendered(directory, "sphere { translate headingAlong(Line) }").Error, "headingAlong");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
