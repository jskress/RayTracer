using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
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

    /// <summary>
    /// A field asked for instances still copies a shape that may not be shared, since that is the
    /// shape's to refuse rather than the author's to get right.
    /// <para>
    /// **This is the ordinary case rather than a corner of one.**  A primitive that gives back a
    /// group hands out a group holding an instance, and an instance of *that* cannot say which of
    /// the two a point lies in -- so a field of any group primitive used to stop the render with a
    /// complaint about shapes holding shapes, which is true of the internals and no use at all to
    /// whoever wrote the scene.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAShapeThatMayNotBeSharedIsCopiedAnyway()
    {
        Field field = new ()
        {
            Outline = Square(),
            Spacing = 0.5,
            Prototype = _ => new Group().Add(new Instance { Prototype = new Sphere() })
        };

        field.PrepareForRendering();

        Assert.AreEqual(16, field.Surfaces.Count);
        Assert.IsFalse(field.Surfaces.Any(surface => surface is Instance),
            "A shape that may not be shared should have been copied, not instanced.");
    }

    /// <summary>
    /// The shape built to be shared is kept as the first copy when sharing is refused, rather than
    /// thrown away and built again.  The numbers are what show it: an extra build would put a second
    /// nought at the front of them.
    /// </summary>
    [TestMethod]
    public void TestTheRefusedShapeBecomesTheFirstCopy()
    {
        List<int> asked = [];
        Field field = new ()
        {
            Outline = Square(),
            Spacing = 0.5,
            Prototype = number =>
            {
                asked.Add(number);

                return new Group().Add(new Instance { Prototype = new Sphere() });
            }
        };

        field.PrepareForRendering();

        Assert.AreEqual(16, field.Surfaces.Count);
        CollectionAssert.AreEqual(Enumerable.Range(0, 16).ToArray(), asked,
            $"The copies were built as [{string.Join(", ", asked)}].");
    }

    /// <summary>
    /// The clause that asks for copies, from the scene's side.  `index` on its own asks for them;
    /// `index in n` asks for them and says what each one's number is called.
    /// <para>
    /// The case that must *not* pass is the one that proves where the name comes from: a prototype
    /// reaching for `n` in a field that never wrote `index in n` has nothing to reach for, and the
    /// scene should say so.  Without that, a clause that quietly bound nothing would pass every
    /// other test here.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestASceneMayNameTheCopyNumber()
    {
        Assert.IsNull(ErrorFrom($$"""
                                  {{Ball}}
                                  field { of Ball(n)  {{Outline}}  spacing 0.5  index in n }
                                  """), "A field should be able to name its copy number.");

        string bare = ErrorFrom($$"""
                                  {{Ball}}
                                  field { of Ball(0)  {{Outline}}  spacing 0.5  index }
                                  """);

        Assert.IsNotNull(bare,
            "`index` naming nothing is the clause without its point, and should have been " +
            "refused.");
        Assert.IsTrue(bare.Contains("\"in\""),
            $"The complaint should say what is missing, but said: {bare}");

        Assert.IsNull(ErrorFrom($$"""
                                  primitive Lump(n) -> group {
                                      return group { sphere { scale 0.1 } }
                                  }
                                  field { of Lump(0)  {{Outline}}  spacing 0.5 }
                                  """),
            "A field of a group primitive should just work; it used to stop the render.");

        string missing = ErrorFrom($$"""
                                     {{Ball}}
                                     field { of Ball(n)  {{Outline}}  spacing 0.5 }
                                     """);

        Assert.IsNotNull(missing,
            "With no `index in n` there is nothing named n, and the scene should have said so.");
        Assert.IsTrue(missing.Contains("'n'"),
            $"The complaint should name n, but said: {missing}");
    }

    /// <summary>
    /// Naming the number is not enough; it has to *vary*.  A clause that bound n to nought for every
    /// copy would raise no complaint and make no difference, so the two fields here differ in one
    /// thing only -- whether the ball's size is read from the copy number or fixed at the number
    /// nought -- and the numbered one must cover more of the picture.
    /// </summary>
    [TestMethod]
    public void TestTheNamedNumberVariesFromCopyToCopy()
    {
        int numbered = Lit($$"""
                             {{Ball}}
                             field { of Ball(n)  {{Outline}}  spacing 0.5  index in n }
                             """);
        int uniform = Lit($$"""
                            {{Ball}}
                            field { of Ball(0)  {{Outline}}  spacing 0.5 }
                            """);

        Assert.IsTrue(uniform > 0, "The uniform field covered nothing at all.");
        Assert.IsTrue(numbered > uniform * 1.5,
            $"Numbered copies covered {numbered} pixels against {uniform} for copies all built " +
            "from nought, which is not enough of a difference to say the number varied.");
    }

    /// <summary>
    /// A field of several things picks among them as often as their weights say, and builds each once
    /// however many places it stands at.
    /// </summary>
    [TestMethod]
    public void TestSeveralThingsArePickedByWeightAndBuiltOnce()
    {
        int ballsBuilt = 0, boxesBuilt = 0;
        Field field = new ()
        {
            Outline = new GeneralPath().MoveTo(-10, -10).LineTo(10, -10).LineTo(10, 10).LineTo(-10, 10).ClosePath(),
            Spacing = 0.5
        };

        field.Choices.Add(new FieldChoice(_ => { ballsBuilt++; return new Sphere(); }));
        field.Choices.Add(new FieldChoice(_ => { boxesBuilt++; return new Cube(); }, 3));
        field.PrepareForRendering();

        int balls = field.Surfaces.Cast<Instance>().Count(instance => instance.Prototype is Sphere);
        int boxes = field.Surfaces.Cast<Instance>().Count(instance => instance.Prototype is Cube);

        Assert.AreEqual(1600, balls + boxes);
        Assert.AreEqual(3, (double) boxes / balls, 0.3, $"{boxes} boxes to {balls} balls");
        Assert.AreEqual(1, ballsBuilt, "the ball was built more than once");
        Assert.AreEqual(1, boxesBuilt, "the box was built more than once");
    }

    /// <summary>
    /// Taken in turn, the things a field stands run in diagonal stripes over the grid, each as many
    /// stripes running as its weight.
    /// </summary>
    [TestMethod]
    public void TestThingsTakenInTurnRunInDiagonalStripes()
    {
        Sphere ball = new ();
        Cube box = new ();
        Field field = new () { Outline = Square(), Spacing = 0.25, Cycle = true };

        field.Choices.Add(new FieldChoice(_ => ball, 2));
        field.Choices.Add(new FieldChoice(_ => box));
        field.PrepareForRendering();

        foreach (Instance instance in field.Surfaces.Cast<Instance>())
        {
            Point at = Point.Zero * instance.Transform;
            int column = (int) Math.Floor((at.X + 1) / 0.25);
            int row = (int) Math.Floor((at.Z + 1) / 0.25);
            Surface expected = (row + column) % 3 < 2 ? ball : box;

            Assert.AreSame(expected, instance.Prototype, $"at row {row}, column {column}");
        }
    }

    /// <summary>
    /// A field's turn is spread evenly about the way a thing faces as made, so a turn of twenty degrees
    /// faces each copy within ten of it either way; and a field asked for no turn and no size stands
    /// each copy exactly as it always has, moved and nothing else.
    /// </summary>
    [TestMethod]
    public void TestTurnsSpreadAboutTheWayAThingFaces()
    {
        Field turned = new ()
        {
            Outline = Square(), Spacing = 0.1, Turn = 20.0.ToRadians(), Prototype = _ => new Sphere()
        };

        turned.PrepareForRendering();

        List<double> angles = turned.Surfaces.Select(copy => TurnAndSizeOf(copy.Transform).Turn).ToList();

        Assert.IsTrue(angles.All(angle => Math.Abs(angle) <= 10.0.ToRadians() + 1e-12), "a copy turned too far");
        Assert.IsTrue(angles.Max() - angles.Min() > 18.0.ToRadians(), "the turns did not spread over the range");
        Assert.AreEqual(0, angles.Average(), 1.0.ToRadians(), "the turns did not center on the way the thing faces");

        Field plain = Filled(0.5, 0.9);

        foreach (Surface copy in plain.Surfaces)
        {
            Point at = Point.Zero * copy.Transform;

            Assert.IsTrue(Transforms.Translate(at.X, 0, at.Z).Matches(copy.Transform), "a plain copy was more than moved");
        }
    }

    /// <summary>
    /// A field's sizes fall between its smallest and its largest, spread over the range.
    /// </summary>
    [TestMethod]
    public void TestSizesFallBetweenTheSmallestAndTheLargest()
    {
        Field field = new ()
        {
            Outline = Square(), Spacing = 0.1, SmallestSize = 0.5, LargestSize = 2, Prototype = _ => new Sphere()
        };

        field.PrepareForRendering();

        List<double> sizes = field.Surfaces.Select(copy => TurnAndSizeOf(copy.Transform).Size).ToList();

        Assert.IsTrue(sizes.All(size => size is >= 0.5 - 1e-12 and <= 2 + 1e-12), "a copy was sized out of range");
        Assert.AreEqual(1.25, sizes.Average(), 0.06, "the sizes did not spread evenly over the range");
        Assert.IsTrue(sizes.All(size => Math.Abs(TurnAndSizeOf(Transforms.Scale(size)).Turn) < 1e-12));
    }

    /// <summary>
    /// What stands at a place, which way it is turned and how big it is, belong to the place: cutting a
    /// hole in the outline changes nothing around the hole.
    /// </summary>
    [TestMethod]
    public void TestAPlaceKeepsWhatStandsThereWhenAHoleIsCut()
    {
        Dictionary<(double, double), (Surface, double, double)> Stood(GeneralPath outline)
        {
            Field field = new ()
            {
                Outline = outline, Spacing = 0.2, Jitter = 0.8, Turn = Math.PI * 2, SmallestSize = 0.5, LargestSize = 1.5
            };

            field.Choices.Add(new FieldChoice(_ => new Sphere()));
            field.Choices.Add(new FieldChoice(_ => new Cube()));
            field.PrepareForRendering();

            return field.Surfaces.Cast<Instance>().ToDictionary(
                copy => (Math.Round((Point.Zero * copy.Transform).X, 9), Math.Round((Point.Zero * copy.Transform).Z, 9)),
                copy => ((Surface) (copy.Prototype is Sphere ? null : copy.Prototype), TurnAndSizeOf(copy.Transform).Turn,
                    TurnAndSizeOf(copy.Transform).Size));
        }

        Dictionary<(double, double), (Surface Box, double Turn, double Size)> whole = Stood(Square());
        Dictionary<(double, double), (Surface Box, double Turn, double Size)> holed = Stood(Square()
            .MoveTo(-0.4, -0.4).LineTo(-0.4, 0.4).LineTo(0.4, 0.4).LineTo(0.4, -0.4).ClosePath());

        Assert.IsTrue(holed.Count < whole.Count - 10, "the hole cut nothing away");

        foreach (((double, double) at, (Surface Box, double Turn, double Size) there) in holed)
        {
            Assert.IsTrue(whole.TryGetValue(at, out (Surface Box, double Turn, double Size) before), $"nothing stood at {at} before");
            Assert.AreEqual(before.Box is null, there.Box is null, $"what stood at {at} changed");
            Assert.AreEqual(before.Turn, there.Turn, 1e-12, $"the turn at {at} changed");
            Assert.AreEqual(before.Size, there.Size, 1e-12, $"the size at {at} changed");
        }
    }

    /// <summary>
    /// The scene's side: several things after commas, each with a weight, the heavier one picked more
    /// often; `turned` and `sized`; and weights and sizes that make no sense are refused.
    /// </summary>
    [TestMethod]
    public void TestASceneMayFillAFieldWithSeveralThings()
    {
        int moreBig = Lit($$"""
                            {{Ball}}
                            field { of Ball(0), Ball(5) weight 4  {{Outline}}  spacing 0.5  turned 360  sized 0.8 to 1.2 }
                            """);
        int moreSmall = Lit($$"""
                              {{Ball}}
                              field { of Ball(0) weight 4, Ball(5)  {{Outline}}  spacing 0.5  turned 360  sized 0.8 to 1.2 }
                              """);
        int doubled = Lit($$"""
                            {{Ball}}
                            field { of Ball(0)  {{Outline}}  spacing 0.5  sized 2 }
                            """);
        int single = Lit($$"""
                           {{Ball}}
                           field { of Ball(0)  {{Outline}}  spacing 0.5 }
                           """);

        Assert.IsTrue(moreBig > moreSmall * 1.3, $"weighted to the big ball covered {moreBig}, to the small {moreSmall}");
        Assert.AreEqual(4, (double) doubled / single, 1, $"twice the size covered {doubled} against {single}");
        Assert.IsNull(ErrorFrom($$"""
                                  {{Ball}}
                                  field { of Ball(0), Ball(1), Ball(2) weight 2  {{Outline}}  spacing 0.5  cycle }
                                  """), "a field taking things in turn should just work");

        foreach (string wrong in new[] { "of Ball(0), Ball(1) weight 0", "of Ball(0)  sized 0.5 to 0.25", "of Ball(0)  sized -1" })
        {
            Assert.IsNotNull(ErrorFrom($$"""
                                         {{Ball}}
                                         field { {{wrong}}  {{Outline}}  spacing 0.5 }
                                         """), $"`{wrong}` should have been refused");
        }
    }

    /// <summary>
    /// This method returns how far a transform turns about the up axis and how much it scales,
    /// for one made of a move, a turn about the up axis and an even scale.
    /// </summary>
    private static (double Turn, double Size) TurnAndSizeOf(Matrix transform)
    {
        Vector across = new Vector(1, 0, 0) * transform;

        return (Math.Atan2(-across.Z, across.X), across.Magnitude);
    }

    /// <summary>
    /// A ball whose size is read from whatever it is handed, so that a copy's number shows up as
    /// something a picture can be measured for.
    /// </summary>
    private const string Ball = """
                                primitive Ball(n) -> sphere {
                                    return sphere {
                                        scale 0.05 + n * 0.011
                                        material { pigment [1, 0.6, 0.3]  ambient 1  diffuse 0  specular 0 }
                                    }
                                }
                                """;

    private const string Outline =
        "within { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close }";

    /// <summary>
    /// Renders a small scene lit flat and counts how many pixels its surfaces cover.
    /// </summary>
    private int Lit(string body)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"field-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string scene = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            File.WriteAllText(scene,
                "context { no gamma }\n" +
                "camera { location [0, 3, -5]  look at [0, 0, 0]  field of view 40 }\n" +
                "point light { location [-4, 6, -6] }\n" + body + "\n");

            new LanguageParser(scene).Parse().Render(new RenderOptions
            {
                OutputFileName = output, Width = 100, Height = 100
            });

            Canvas canvas = new ImageFile(output).Load()[0];
            int count = 0;

            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    if (canvas.GetPixel(x, y).Red > 0.15)
                        count++;
                }
            }

            return count;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// Renders a small scene and hands back the error that stopped it, or <c>null</c>.
    /// </summary>
    private string ErrorFrom(string body)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"field-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
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
            catch (Exception exception)
            {
                // A scene the parser accepts may still come apart when it is run, and that arrives
                // as an exception rather than as a printed complaint.  Reaching for a name nothing
                // has set is exactly that sort, so both have to be caught here.
                return exception.Message;
            }
            finally
            {
                Console.SetOut(was);
            }

            string said = captured.ToString();

            return said.Contains("Error") ? said.Trim() : null;
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
