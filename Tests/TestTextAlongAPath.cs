using RayTracer.Basics;
using RayTracer.Fonts;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using Typography.OpenFont;

namespace Tests;

/// <summary>
/// These tests cover laying text along a path rather than in straight lines: the path being walked by
/// distance, each glyph landing on it by its middle and turned to run with it, and the text's
/// vertical position saying where the path runs through it -- and so which side of it the text is on.
/// </summary>
[TestClass]
public class TestTextAlongAPath
{
    private const string Font = "Merriweather";

    // The four cubics that make a circle as nearly as four can.
    private const double Kappa = 0.5522847498;

    /// <summary>
    /// Lays text out as given and hands back every glyph's outline, sampled finely enough that the
    /// curves are represented by points on them rather than by their control points.
    /// </summary>
    private static List<TwoDPoint> Laid(
        string text, GeneralPath guide = null, VerticalPosition vertical = VerticalPosition.Baseline,
        HorizontalPosition horizontal = HorizontalPosition.Left)
    {
        TextLayoutSettings settings = new ()
        {
            Guide = guide,
            VerticalPosition = vertical,
            HorizontalPosition = horizontal
        };

        return TextOutline.Glyphs(Font, FontWeight.Regular, false, settings, null, text)
            .SelectMany(glyph => glyph.Sample(8))
            .ToList();
    }

    private static GeneralPath Line(double x0, double y0, double x1, double y1)
    {
        return new GeneralPath().MoveTo(x0, y0).LineTo(x1, y1);
    }

    private static void AssertSamePoints(List<TwoDPoint> expected, List<TwoDPoint> actual, string what)
    {
        Assert.HasCount(expected.Count, actual, what);

        for (int index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].X, actual[index].X, 1e-9, $"{what}: point {index}");
            Assert.AreEqual(expected[index].Y, actual[index].Y, 1e-9, $"{what}: point {index}");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The guide.
    // ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void TestAGuideIsWalkedByDistance()
    {
        PathGuide corner = new (new GeneralPath().MoveTo(0, 0).LineTo(10, 0).LineTo(10, 5));

        Assert.AreEqual(15, corner.Length, 1e-12);

        (TwoDPoint point, TwoDVector way) = corner.At(12);

        Assert.AreEqual(new TwoDPoint(10, 2), point);
        Assert.AreEqual(0, way.X, 1e-9);
        Assert.AreEqual(1, way.Y, 1e-9);

        // Before its start and past its end, it carries straight on.
        Assert.AreEqual(-1, corner.At(-1).Point.X, 1e-9);
        Assert.AreEqual(6, corner.At(16).Point.Y, 1e-9);
    }

    [TestMethod]
    public void TestDistanceIsEvenWhereACurvesParameterIsNot()
    {
        // A straight cubic whose control points are bunched up at its far end, so that halfway
        // through its parameter it has run nine tenths of the way.  Halfway by distance is still the
        // middle.
        PathGuide bunched = new (new GeneralPath().MoveTo(0, 0).CubicTo(9, 0, 9.9, 0, 10, 0));

        Assert.AreEqual(10, bunched.Length, 1e-6);
        Assert.AreEqual(5, bunched.At(5).Point.X, 1e-3);
    }

    [TestMethod]
    public void TestAGuideFollowsACurve()
    {
        GeneralPath circle = new GeneralPath()
            .MoveTo(2, 0)
            .CubicTo(2, 2 * Kappa, 2 * Kappa, 2, 0, 2)
            .CubicTo(-2 * Kappa, 2, -2, 2 * Kappa, -2, 0)
            .CubicTo(-2, -2 * Kappa, -2 * Kappa, -2, 0, -2)
            .CubicTo(2 * Kappa, -2, 2, -2 * Kappa, 2, 0);
        PathGuide guide = new (circle);

        Assert.AreEqual(4 * Math.PI, guide.Length, 2e-3);

        (TwoDPoint point, TwoDVector way) = guide.At(guide.Length / 4);

        Assert.AreEqual(0, point.X, 1e-3);
        Assert.AreEqual(2, point.Y, 1e-3);
        Assert.AreEqual(-1, way.X, 1e-3);
        Assert.AreEqual(0, way.Y, 1e-3);
    }

    [TestMethod]
    public void TestAGuideLandsOnTheCurveAndRunsWithIt()
    {
        // The guide's straight pieces are only for measuring.  Where a distance lands must be on the
        // curve itself, and the way it runs there the curve's own: a piece's chord strays from a
        // circle by a degree or so, which turns each letter by a little jolt as it crosses from one
        // piece to the next.
        const double radius = 2;
        double k = Kappa * radius;
        GeneralPath path = new GeneralPath().MoveTo(radius, 0).CubicTo(radius, k, k, radius, 0, radius);
        IPathSegment quarter = path.Segments[0];
        PathGuide guide = new (path);

        for (int step = 1; step < 50; step++)
        {
            double distance = guide.Length * step / 50.3;
            (TwoDPoint point, TwoDVector way) = guide.At(distance);
            double t = NearestParameter(quarter, point);
            TwoDPoint onCurve = quarter.GetPoint(t);
            TwoDVector tangent = (quarter.GetPoint(Math.Min(1, t + 1e-7)) -
                                  quarter.GetPoint(Math.Max(0, t - 1e-7))).Unit;

            Assert.AreEqual(0, (point - onCurve).Magnitude, 1e-9, $"at {distance}, off the curve");
            Assert.AreEqual(1, way.Dot(tangent), 1e-9, $"at {distance}, not running with the curve");
        }

        return;

        // Where on the curve is nearest a point: a coarse look along it, then a fine one.
        static double NearestParameter(IPathSegment curve, TwoDPoint point)
        {
            double best = 0;
            double distance = double.MaxValue;

            for (int index = 0; index <= 1000; index++)
            {
                double t = index / 1000.0;
                double away = (curve.GetPoint(t) - point).Magnitude;

                if (away < distance)
                {
                    distance = away;
                    best = t;
                }
            }

            double low = Math.Max(0, best - 0.001);
            double high = Math.Min(1, best + 0.001);

            for (int round = 0; round < 200; round++)
            {
                double a = low + (high - low) / 3;
                double b = high - (high - low) / 3;

                if ((curve.GetPoint(a) - point).Magnitude < (curve.GetPoint(b) - point).Magnitude)
                    high = b;
                else
                    low = a;
            }

            return (low + high) / 2;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // The text.
    // ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void TestTextAlongAStraightGuideIsTheTextLaidFlat()
    {
        // A guide that is the line the text sits on anyway must change nothing at all.
        AssertSamePoints(Laid("Hello"), Laid("Hello", Line(0, 0, 100, 0)), "along the X axis");
    }

    [TestMethod]
    public void TestTextAlongATurnedGuideIsTurnedWithIt()
    {
        // Up the Y axis, the text runs up and stands to the left: the flat text, turned a quarter.
        List<TwoDPoint> turned = Laid("Hello")
            .Select(point => new TwoDPoint(-point.Y, point.X))
            .ToList();

        AssertSamePoints(turned, Laid("Hello", Line(0, 0, 0, 100)), "up the Y axis");
    }

    [TestMethod]
    public void TestTheHorizontalPositionSaysWhereAlongThePathTheTextGoes()
    {
        // Centered, the text's middle is the path's; right, its end is the path's end.  On a
        // straight guide that is the flat text, centered or set right, moved along by half the path
        // or all of it.
        GeneralPath guide = Line(0, 0, 10, 0);

        AssertSamePoints(
            Laid("Hello", horizontal: HorizontalPosition.Center).Select(point => point + new TwoDPoint(5, 0)).ToList(),
            Laid("Hello", guide, horizontal: HorizontalPosition.Center), "centered");
        AssertSamePoints(
            Laid("Hello", horizontal: HorizontalPosition.Right).Select(point => point + new TwoDPoint(10, 0)).ToList(),
            Laid("Hello", guide, horizontal: HorizontalPosition.Right), "set right");
    }

    [TestMethod]
    public void TestTheVerticalPositionSaysWhichSideOfThePathTheTextIsOn()
    {
        // A word with an ascender, a capital and three descenders, along the X axis.  With the path
        // through its tops, all of it hangs below; through its bottoms, all of it stands above;
        // through its baseline, the descenders cross; through its middle, it straddles.
        GeneralPath guide = Line(0, 0, 100, 0);
        const string word = "Typography";

        Assert.IsTrue(Laid(word, guide, VerticalPosition.Top).All(point => point.Y <= 1e-9),
            "something rose above a path through the tops");
        Assert.IsTrue(Laid(word, guide, VerticalPosition.Bottom).All(point => point.Y >= -1e-9),
            "something sank below a path through the bottoms");

        List<TwoDPoint> baseline = Laid(word, guide);
        List<TwoDPoint> center = Laid(word, guide, VerticalPosition.Center);

        Assert.IsTrue(baseline.Min(point => point.Y) < -0.1 && baseline.Max(point => point.Y) > 0.5,
            "the descenders did not cross the baseline");
        Assert.IsTrue(center.Min(point => point.Y) < -0.3 && center.Max(point => point.Y) > 0.3,
            "the text did not straddle the path");

        // Which side is "above" is the path's left, so drawn the other way the text is on the other
        // side -- and upside down, since it still reads along the path.
        Assert.IsTrue(Laid(word, Line(100, 0, 0, 0), VerticalPosition.Top).All(point => point.Y >= -1e-9),
            "the path's direction did not decide which side is which");
    }

    /// <summary>
    /// How far the font's tallest glyphs reach above the baseline, and its deepest below it, in ems;
    /// the second is negative.
    /// </summary>
    private static (double Ascender, double Descender) Metrics()
    {
        Typeface typeface = FontManager.Instance.GetTypeFace(new FaceIdentifier
        {
            FamilyName = Font, Weight = (int) FontWeight.Regular, Italic = false
        });

        return (typeface.Ascender / (double) typeface.UnitsPerEm,
            typeface.Descender / (double) typeface.UnitsPerEm);
    }

    [TestMethod]
    public void TestTopAndBottomAreTheFontsOwn()
    {
        // The path through the tops sits on the font's ascender line, so a capital's foot, which
        // stands on the baseline, comes to rest the ascender's height below it; through the bottoms,
        // the descender line, so the foot stands the descender's depth above.
        (double ascender, double descender) = Metrics();

        Assert.AreEqual(0, Laid("T").Min(point => point.Y), 1e-9, "a T does not stand on its baseline");
        Assert.AreEqual(-ascender, Laid("T", vertical: VerticalPosition.Top).Min(point => point.Y), 1e-9);
        Assert.AreEqual(-descender, Laid("T", vertical: VerticalPosition.Bottom).Min(point => point.Y), 1e-9);
    }

    [TestMethod]
    public void TestEachGlyphIsLaidOnThePathByItsMiddle()
    {
        // One glyph, centered on an arc over the top of a circle: its middle lands on the crest,
        // where the arc runs level, so it sits exactly as it would flat, lifted by the radius.  Laid
        // by any other point of it -- its left edge, say -- it would land off the crest, turned.
        const double radius = 6;
        double k = Kappa * radius;
        GeneralPath over = new GeneralPath()
            .MoveTo(-radius, 0)
            .CubicTo(-radius, k, -k, radius, 0, radius)
            .CubicTo(k, radius, radius, k, radius, 0);
        List<TwoDPoint> flat = Laid("H", horizontal: HorizontalPosition.Center);
        List<TwoDPoint> crest = Laid("H", over, horizontal: HorizontalPosition.Center);

        Assert.HasCount(flat.Count, crest);

        for (int index = 0; index < flat.Count; index++)
        {
            Assert.AreEqual(flat[index].X, crest[index].X, 1e-6, $"point {index}");
            Assert.AreEqual(flat[index].Y + radius, crest[index].Y, 1e-6, $"point {index}");
        }
    }

    [TestMethod]
    public void TestTextStandsOnACircleOrHangsFromIt()
    {
        // The classic seal: over the top, drawn left to right, the path's left is outward, so text
        // standing on it by its bottoms is outside the circle; under the bottom, drawn left to right,
        // its right is outward, so text hanging from it by its tops is outside too.  Each glyph is
        // laid whole, by its middle, so its corners dip inside the circle by a hair -- an eighth of
        // its width squared over the radius -- which is all the slack allowed.
        //
        // The bottoms are the font's descender line, not the baseline, so capitals, which have no
        // descenders, stand that far off the circle: just what keeps a word that has them from
        // crossing it.  Capitals meant to sit right on the circle want the baseline.
        (double ascender, double descender) = Metrics();
        const double radius = 6;
        double k = Kappa * radius;
        GeneralPath over = new GeneralPath()
            .MoveTo(-radius, 0)
            .CubicTo(-radius, k, -k, radius, 0, radius)
            .CubicTo(k, radius, radius, k, radius, 0);
        GeneralPath under = new GeneralPath()
            .MoveTo(-radius, 0)
            .CubicTo(-radius, -k, -k, -radius, 0, -radius)
            .CubicTo(k, -radius, radius, -k, radius, 0);

        foreach ((string name, List<TwoDPoint> points) in new[]
        {
            ("standing over the top", Laid("FEDERATION", over, VerticalPosition.Bottom, HorizontalPosition.Center)),
            ("hanging under the bottom", Laid("PLANETS", under, VerticalPosition.Top, HorizontalPosition.Center))
        })
        {
            double nearest = points.Min(point => Math.Sqrt(point.X * point.X + point.Y * point.Y));

            Assert.IsTrue(nearest > radius - 0.02, $"{name}: a glyph came {radius - nearest} inside the circle");
            Assert.IsTrue(nearest < radius + ascender, $"{name}: the text stood {nearest - radius} off the circle");
        }

        List<TwoDPoint> standing = Laid("FEDERATION", over, VerticalPosition.Bottom, HorizontalPosition.Center);
        double foot = standing.Min(point => Math.Sqrt(point.X * point.X + point.Y * point.Y));

        Assert.AreEqual(radius - descender, foot, 0.03, "capitals did not stand a descender's depth off the circle");

        List<TwoDPoint> onIt = Laid("FEDERATION", over, VerticalPosition.Baseline, HorizontalPosition.Center);

        Assert.AreEqual(radius, onIt.Min(point => Math.Sqrt(point.X * point.X + point.Y * point.Y)), 0.03,
            "capitals on the baseline did not sit on the circle");
    }

    // ---------------------------------------------------------------------------------------------
    // The language.
    // ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void TestAPathToLayTextAlongIsWrittenInALayout()
    {
        // A word painted on a floor seen from above, laid along a path that runs up the picture
        // rather than across it -- written out in place, and named -- so the paint must be taller
        // than it is wide.
        foreach (string along in new[] { "along path { move to 0, -10  line to 0, 10 }", "along path upward" })
        {
            Canvas picture = FloorWithAWord(along);
            List<(int X, int Y)> painted = [];

            for (int y = 0; y < picture.Height; y++)
            for (int x = 0; x < picture.Width; x++)
            {
                if (picture.GetPixel(x, y).Green < 0.5)
                    painted.Add((x, y));
            }

            Assert.IsTrue(painted.Count > 20, $"{along}: only {painted.Count} pixels were painted");

            int wide = painted.Max(pixel => pixel.X) - painted.Min(pixel => pixel.X);
            int tall = painted.Max(pixel => pixel.Y) - painted.Min(pixel => pixel.Y);

            Assert.IsTrue(tall > 2 * wide, $"{along}: the word was {wide} wide and {tall} tall");
        }
    }

    [TestMethod]
    public void TestAlongMustBeFollowedByAPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"text-along-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string scene = Path.Combine(directory, "scene.igl");

            File.WriteAllText(scene, """
                camera { location [0, 10, 0]  look at [0, 0, 0]  up [0, 0, 1] }
                point light { location [0, 20, 0] }
                extrusion { path { text { text 'A'  font 'Merriweather'  layout { along { } } } } }
                """);

            StringWriter captured = new ();
            TextWriter was = Console.Out;

            Console.SetOut(captured);

            try
            {
                Assert.IsNull(new LanguageParser(scene).Parse(), "a layout took \"along\" without a path");
            }
            finally
            {
                Console.SetOut(was);
            }

            StringAssert.Contains(captured.ToString(), "Expecting \"path\" to follow \"along\" here.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Renders a white floor seen from straight above, with a red word painted on it laid out as the
    /// given layout clause says, and hands back the picture.
    /// </summary>
    private static Canvas FloorWithAWord(string along)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"text-along-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string scene = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "scene.png");

            File.WriteAllText(scene, $$"""
                context { no gamma }
                camera { location [0, 10, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 60 }
                point light { location [0, 20, 0] }
                upward = path { move to 0, -10  line to 0, 10 }
                plane {
                    material {
                        pigment color White  ambient 1  diffuse 0  specular 0
                        decal {
                            path { text { text 'WORDS'  font 'Merriweather'
                                          layout { {{along}}  horizontal position center
                                                   vertical position center } } }
                            color Red
                            planar  min Y -1  max Y 1
                        }
                    }
                }
                """);

            new LanguageParser(scene).Parse().Render(new RenderOptions
            {
                OutputFileName = output, Width = 60, Height = 60
            });

            return new ImageFile(output).Load()[0];
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
