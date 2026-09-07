using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the field: an area of the X/Z plane, given by a 2D outline, filled with copies
/// of one surface.
/// </summary>
[TestClass]
public class TestField
{
    /// <summary>
    /// A square two units on a side, centred on the origin.
    /// </summary>
    private static GeneralPath Square() => new GeneralPath()
        .MoveTo(-1, -1).LineTo(1, -1).LineTo(1, 1).LineTo(-1, 1).ClosePath();

    private static Field Filled(double spacing, double jitter = 0, bool copies = false)
    {
        Field field = new ()
        {
            Outline = Square(),
            Spacing = spacing,
            Jitter = jitter,
            Copies = copies,
            Prototype = _ => new Sphere()
        };

        field.PrepareForRendering();

        return field;
    }

    /// <summary>
    /// The outline is filled cell by cell, so a square two units across at a spacing of half a unit
    /// holds four rows of four.  The count is checked rather than merely that something was made:
    /// a fill that ran a row over the edge, or stopped a row short, gives 25 or 9 instead.
    /// </summary>
    [TestMethod]
    public void TestAnOutlineIsFilledCellByCell()
    {
        Assert.AreEqual(16, Filled(0.5).Surfaces.Count);
        Assert.AreEqual(4, Filled(1.0).Surfaces.Count);
        Assert.AreEqual(64, Filled(0.25).Surfaces.Count);
    }

    /// <summary>
    /// Nothing may land outside the outline, however far it is jittered -- which is the whole reason
    /// the test is made against where a copy has been *moved to* rather than where the grid put it.
    /// </summary>
    [TestMethod]
    public void TestNothingLandsOutsideTheOutline()
    {
        Field field = new ()
        {
            // A triangle, so that a good half of the grid's cells fall outside it.
            Outline = new GeneralPath().MoveTo(-1, -1).LineTo(1, -1).LineTo(0, 1).ClosePath(),
            Spacing = 0.12,
            Jitter = 1,
            Prototype = _ => new Sphere()
        };

        field.PrepareForRendering();

        Assert.IsTrue(field.Surfaces.Count > 40,
            $"Only {field.Surfaces.Count} were placed, which is too few to say much.");

        foreach (Surface surface in field.Surfaces)
        {
            Point where = surface.Transform * new Point(0, 0, 0);

            Assert.IsTrue(field.Outline.Contains(new TwoDPoint(where.X, where.Z)),
                $"A copy stands at ({where.X}, {where.Z}), which is outside the outline.");
        }
    }

    /// <summary>
    /// Without jitter the copies stand on the grid exactly; with it they do not.  Both halves are
    /// checked, since a jitter that did nothing would pass the first on its own.
    /// </summary>
    [TestMethod]
    public void TestJitterMovesThingsOffTheGrid()
    {
        bool AllOnTheGrid(Field field)
        {
            return field.Surfaces.All(surface =>
            {
                Point where = surface.Transform * new Point(0, 0, 0);
                double offset = (where.X + 1) / 0.5;

                return Math.Abs(offset - Math.Round(offset - 0.5) - 0.5) < 1e-9;
            });
        }

        Assert.IsTrue(AllOnTheGrid(Filled(0.5)), "Without jitter the copies should be on the grid.");
        Assert.IsFalse(AllOnTheGrid(Filled(0.5, 0.9)), "With jitter they should not be.");
    }

    /// <summary>
    /// The copies are instances of one shape by default, and separate shapes when the scene asks.
    /// </summary>
    [TestMethod]
    public void TestInstancesByDefaultAndCopiesOnRequest()
    {
        Field instanced = Filled(0.5);
        Field copied = Filled(0.5, copies: true);

        Assert.IsTrue(instanced.Surfaces.All(surface => surface is Instance),
            "By default a field should stand instances of one shape.");
        Assert.AreEqual(1, instanced.Surfaces
            .Cast<Instance>()
            .Select(instance => instance.Prototype)
            .Distinct()
            .Count(), "Every instance should share the one prototype.");

        Assert.IsTrue(copied.Surfaces.All(surface => surface is Sphere),
            "Asked for copies, a field should stand the shapes themselves.");
        Assert.AreEqual(copied.Surfaces.Count, copied.Surfaces.Distinct().Count(),
            "Every copy should be its own surface.");
    }

    /// <summary>
    /// Asked for copies, a field numbers them -- and that number is the whole of what makes copies
    /// worth asking for.  Without something that differs per copy they are the same geometry built
    /// over and over, which is strictly worse than an instance, so the numbers are checked to run
    /// from nought without gaps or repeats.
    /// </summary>
    [TestMethod]
    public void TestCopiesAreNumbered()
    {
        List<int> asked = [];
        Field field = new ()
        {
            Outline = Square(),
            Spacing = 0.5,
            Copies = true,
            Prototype = number =>
            {
                asked.Add(number);

                return new Sphere();
            }
        };

        field.PrepareForRendering();

        Assert.AreEqual(16, field.Surfaces.Count);
        CollectionAssert.AreEqual(Enumerable.Range(0, 16).ToArray(), asked,
            $"The copies were numbered [{string.Join(", ", asked)}].");
    }

    /// <summary>
    /// A field standing instances asks for the shape once and no more, however many places it fills.
    /// That is the whole saving, so it is worth keeping rather than assuming.
    /// </summary>
    [TestMethod]
    public void TestInstancesAreBuiltOnce()
    {
        int built = 0;
        Field field = new ()
        {
            Outline = Square(),
            Spacing = 0.25,
            Prototype = _ =>
            {
                built++;

                return new Sphere();
            }
        };

        field.PrepareForRendering();

        Assert.AreEqual(64, field.Surfaces.Count);
        Assert.AreEqual(1, built, $"The shape was built {built} times, and should have been built once.");
    }

    /// <summary>
    /// The jitter is repeatable: the same seed gives the same field, and a different one does not.
    /// </summary>
    [TestMethod]
    public void TestTheSeedDecidesTheJitter()
    {
        double[] Places(int? seed)
        {
            Field field = new ()
            {
                Outline = Square(), Spacing = 0.5, Jitter = 0.9, Seed = seed,
                Prototype = _ => new Sphere()
            };

            field.PrepareForRendering();

            return field.Surfaces
                .Select(surface => (surface.Transform * new Point(0, 0, 0)).X)
                .ToArray();
        }

        Assert.AreEqual(Places(7).Length, Places(7).Length);
        CollectionAssert.AreEqual(Places(7), Places(7), "The same seed should give the same field.");
        CollectionAssert.AreNotEqual(Places(7), Places(8), "A different seed should give a different one.");
    }

    /// <summary>
    /// The scene's side of it, and the point of the whole exercise: an outline may be written out in
    /// a field, or given a name, or handed to a primitive as an argument -- which is what lets a
    /// library offer an area of any shape rather than of the two it happens to have built in.
    /// </summary>
    [TestMethod]
    public void TestASceneMayNameAnOutlineAndHandItAbout()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"field-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string written = """
                             Blade = ribbon { width 0.05 at [0, 0, 0]  width 0.01 at [0, 0.3, 0] }
                             field {
                                 of Blade
                                 within { move to -1, -1  line to 1, -1  line to 0, 1  close }
                                 spacing 0.3
                             }
                             """;
            string named = """
                           Blade = ribbon { width 0.05 at [0, 0, 0]  width 0.01 at [0, 0.3, 0] }
                           Outline = path { move to -1, -1  line to 1, -1  line to 0, 1  close }
                           field { of Blade  within Outline  spacing 0.3 }
                           """;
            string given = """
                           Blade = ribbon { width 0.05 at [0, 0, 0]  width 0.01 at [0, 0.3, 0] }
                           Outline = path { move to -1, -1  line to 1, -1  line to 0, 1  close }
                           primitive Sward(shape, step) -> field {
                               return field { of Blade  within shape  spacing step }
                           }
                           object Sward(Outline, 0.3)
                           """;

            foreach ((string what, string body) in new[]
                     { ("written out", written), ("named", named), ("handed to a primitive", given) })
            {
                string scene = Path.Combine(directory, "scene.igl");

                File.WriteAllText(scene,
                    "camera { location [0, 3, -5]  look at [0, 0, 0] }\n" +
                    "point light { location [-4, 6, -6] }\n" + body + "\n");

                StringWriter captured = new ();
                TextWriter was = Console.Out;

                Console.SetOut(captured);

                try
                {
                    ImageRenderer renderer = new LanguageParser(scene).Parse();

                    renderer?.Render(new RenderOptions
                    {
                        OutputFileName = Path.ChangeExtension(scene, ".png"), Width = 8, Height = 6
                    });
                }
                finally
                {
                    Console.SetOut(was);
                }

                Assert.IsFalse(captured.ToString().Contains("Error"),
                    $"An outline {what} did not work: {captured}");
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
