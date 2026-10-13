using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the flop pattern: a color that shifts with the angle a surface is seen at, as some
/// car paints do -- deep blue from one side, dark green from the other.
/// </summary>
[TestClass]
public class TestFlop
{
    private static readonly double Root = Math.Sqrt(0.5);

    private static double Flop(Vector eye, Vector light, Vector normal = null) =>
        new FlopPattern().ValueFor(Point.Zero, Footprint.None, new Viewing(eye, normal ?? new Vector(0, 1, 0), light));

    /// <summary>
    /// The value is the angle between the way to the eye and the way the light mirrors off the surface,
    /// as a share of a half turn: nought along the reflection, one looking straight back at the light.
    /// With no light in particular, the light is taken to be at the eye.
    /// </summary>
    [TestMethod]
    public void TestTheValueRunsFromTheReflectionBackToTheLight()
    {
        // Along the reflection: the light at 45 degrees from one side, the eye at 45 from the other.
        Assert.AreEqual(0, Flop(new Vector(Root, Root, 0), new Vector(-Root, Root, 0)), 1e-12);

        // The eye beside the light: square to the reflection.
        Assert.AreEqual(0.5, Flop(new Vector(Root, Root, 0), new Vector(Root, Root, 0)), 1e-12);

        // Straight back toward a light skimming the surface.
        Assert.AreEqual(1, Flop(new Vector(1, 0, 0), new Vector(1, 0, 0)), 1e-12);

        // No light: twice the eye's own angle from the surface -- here thirty degrees, so a third.
        Assert.AreEqual(1.0 / 3, Flop(new Vector(0.5, Math.Sqrt(0.75), 0), null), 1e-12);

        // And asked with no directions at all, nought.
        Assert.AreEqual(0, new FlopPattern().ValueFor(Point.Zero), 1e-12);
    }

    private static PatternPigment Paint() => new ()
    {
        Pattern = new FlopPattern(),
        PigmentSet = Map(new Color(0, 0, 1), new Color(0, 1, 0))
    };

    private static PigmentSet Map(Color first, Color second)
    {
        PigmentSet set = new ();

        set.AddEntry(new SolidPigment(first));
        set.AddEntry(new SolidPigment(second), 1);

        return set;
    }

    /// <summary>
    /// Only a pigment holding a flop pattern, however deep, is told the directions; every other pigment is
    /// asked exactly as it always was.
    /// </summary>
    [TestMethod]
    public void TestOnlyAPigmentHoldingAFlopSeesTheViewer()
    {
        Assert.IsTrue(Paint().SeesTheViewer);
        Assert.IsFalse(new PatternPigment { Pattern = new CheckerPattern(), PigmentSet = Map(Colors.Red, Colors.White) }.SeesTheViewer);
        Assert.IsFalse(new SolidPigment(Colors.Red).SeesTheViewer);

        PigmentSet nested = new ();

        nested.AddEntry(new SolidPigment(Colors.Red));
        nested.AddEntry(Paint(), 1);

        Assert.IsTrue(new PatternPigment { Pattern = new CheckerPattern(), PigmentSet = nested }.SeesTheViewer,
            "a flop inside a color map was not seen");
        Assert.IsTrue(new BlendedPigment { Pigments = [new SolidPigment(Colors.Red), Paint()] }.SeesTheViewer,
            "a flop inside a blend was not seen");
        Assert.IsFalse(new BlendedPigment { Pigments = [new SolidPigment(Colors.Red)] }.SeesTheViewer);
    }

    /// <summary>
    /// A flop pigment's color runs along its color map with the angle: the first color along the
    /// reflection, the last looking back at the light, and between them in proportion.
    /// </summary>
    [TestMethod]
    public void TestAFlopPigmentsColorFollowsTheAngle()
    {
        Material material = new () { Pigment = Paint() };
        Sphere ball = new () { Material = material };
        Vector up = new (0, 1, 0);
        Point top = new (0, 1, 0);

        Color Seen(Vector eye, Vector light) =>
            material.GetColorFor(ball, top, up, viewing: new Viewing(eye, up, light));

        Assert.IsTrue(new Color(0, 0, 1).Matches(Seen(new Vector(Root, Root, 0), new Vector(-Root, Root, 0))),
            "along the reflection the first color should show");
        Assert.IsTrue(new Color(0, 0.5, 0.5).Matches(Seen(new Vector(Root, Root, 0), new Vector(Root, Root, 0))),
            "square to the reflection the colors should be half and half");
        Assert.IsTrue(new Color(0, 1, 0).Matches(Seen(new Vector(1, 0, 0), new Vector(1, 0, 0))),
            "looking back at the light the last color should show");
        Assert.IsTrue(new Color(0, 0, 1).Matches(material.GetColorFor(ball, top, up)),
            "asked with no directions at all, the first color should show");

        // Inside another pigment -- here both squares of a checker -- it shifts just the same.
        PigmentSet squares = new ();

        squares.AddEntry(Paint());
        squares.AddEntry(Paint(), 1);
        material.Pigment = new PatternPigment { Pattern = new CheckerPattern(), PigmentSet = squares };

        Assert.IsTrue(new Color(0, 1, 0).Matches(Seen(new Vector(1, 0, 0), new Vector(1, 0, 0))),
            "a flop inside a checker did not shift with the angle");
        Assert.IsTrue(new Color(0, 0, 1).Matches(Seen(new Vector(Root, Root, 0), new Vector(-Root, Root, 0))),
            "a flop inside a checker did not shift back");
    }

    /// <summary>
    /// **The two oblique sides of a panel.**  Seen from either side at the same angle, a panel looks the
    /// same to anything that only asks how the eye meets it.  Lit from one side, the eye on the far side
    /// looks along the light's reflection and the eye on the near side does not -- which is what makes
    /// such a paint blue from one side and green from the other.  Rendered, from both sides.
    /// </summary>
    [TestMethod]
    public void TestThePanelsTwoSidesFlopDifferently()
    {
        Color Center(double side)
        {
            Canvas picture = Rendered($$"""
                camera { location [{{side * 4}}, 4, 0]  look at [0, 0, 0] up [0, 0, 1]  field of view 20 }
                point light { location [-8, 8, 0] }
                plane {
                    material {
                        pigment flop { [0, [0, 0, 1], 1, [0, 1, 0]] }
                        ambient 0  diffuse 1  specular 0
                    }
                }
                """, 21, 21);

            return picture.GetPixel(10, 10);
        }

        Color far = Center(1);
        Color near = Center(-1);

        // Far from the light the eye looks along its reflection: all blue.  Beside the light, square to
        // it: half and half.  Measured by eye alone the two would be the same.
        Assert.IsTrue(far.Blue > 0.1 && far.Green < far.Blue * 0.05, $"the far side came out {far}");
        Assert.AreEqual(1, near.Green / near.Blue, 0.05, $"the near side came out {near}");
    }

    /// <summary>
    /// Light crossing a flop pane on its way to make a shadow is tinted as the pane looks from along the
    /// light's own path: straight down through it, square-on; slanting through it, further round its map.
    /// </summary>
    [TestMethod]
    public void TestAShadowThroughAFlopPaneIsTintedAsSeenAlongIt()
    {
        Color Floor(string light)
        {
            Canvas picture = Rendered($$"""
                camera { location [0, 0.5, -0.01]  look at [0, 0, 0]  up [0, 0, 1]  field of view 10 }
                point light { location {{light}} }
                plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }
                cube {
                    material {
                        pigment flop { [0, [1, 0, 0], 1, [0, 0, 1]] }
                        ambient 0  diffuse 0  specular 0
                        transparency 0.95
                        interior { filter 1 }
                    }
                    scale [3, 0.01, 3]
                    translate Y 1
                }
                """, 11, 11);

            return picture.GetPixel(5, 5);
        }

        Color overhead = Floor("[0, 20, 0]");
        Color slanting = Floor("[14, 20, 0]");

        Assert.IsTrue(overhead.Red > 0.05 && overhead.Blue < overhead.Red * 0.02, $"straight down the shadow came out {overhead}");
        Assert.IsTrue(slanting.Blue / slanting.Red > overhead.Blue / overhead.Red + 0.2,
            $"slanting, the shadow came out {slanting} against {overhead} straight down");
    }

    /// <summary>
    /// A scene writes it as any other pattern, and it may stand inside other pigments.
    /// </summary>
    [TestMethod]
    public void TestASceneMayPaintWithFlop()
    {
        Canvas plain = Rendered("""
            camera { location [0, 1.2, -5]  look at [0, 0.8, 0] }
            point light { location [-6, 8, -6] }
            sphere { material { pigment flop { [0, Blue, 1, Green] } } translate Y 1 }
            """, 40, 30);
        Canvas nested = Rendered("""
            camera { location [0, 1.2, -5]  look at [0, 0.8, 0] }
            point light { location [-6, 8, -6] }
            sphere { material { pigment checker { flop { [0, Blue, 1, Green] }, flop { [0, Red, 1, Yellow] } } } translate Y 1 }
            """, 40, 30);

        Assert.IsTrue(Lit(plain) > 100 && Lit(nested) > 100, "a flop paint showed nothing");
    }

    private static int Lit(Canvas picture)
    {
        int count = 0;

        for (int y = 0; y < picture.Height; y++)
        for (int x = 0; x < picture.Width; x++)
        {
            Color color = picture.GetPixel(x, y);

            if (color.Red + color.Green + color.Blue > 0.05)
                count++;
        }

        return count;
    }

    private static Canvas Rendered(string body, int width, int height)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"flop-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string scene = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            File.WriteAllText(scene, "context { no gamma  angles are degrees }\n" + body + "\n");

            ImageRenderer renderer = new LanguageParser(scene).Parse();

            Assert.IsNotNull(renderer, "the scene did not parse");

            renderer.Render(new RenderOptions { OutputFileName = output, Width = width, Height = height });

            return new ImageFile(output).Load()[0];
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
