using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
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
    /// shape's to refuse rather than the author's to get right.  A thing that moves is one: an
    /// instance would have to carry the shape's motion as well as its own place.
    /// <para>
    /// (This used to be the ordinary case: a primitive that gives back a group hands out a group
    /// holding an instance, and a shared shape could not hold one.  Now it can -- see
    /// <c>TestASharedShapeMayHoldAnotherSharedShape</c> -- so a field of a group primitive shares it.)
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAShapeThatMayNotBeSharedIsCopiedAnyway()
    {
        Field field = new ()
        {
            Outline = Square(),
            Spacing = 0.5,
            Prototype = _ => new Group().Add(Moving())
        };

        field.PrepareForRendering();

        Assert.AreEqual(16, field.Surfaces.Count);
        Assert.IsFalse(field.Surfaces.Any(surface => surface is Instance),
            "A shape that may not be shared should have been copied, not instanced.");
    }

    /// <summary>
    /// A field shares a shape that holds shared shapes of its own -- which is what every group primitive
    /// gives back -- rather than building it again at every place.
    /// </summary>
    [TestMethod]
    public void TestAShapeHoldingSharedShapesIsSharedToo()
    {
        Sphere leaf = new ();
        int built = 0;
        Field field = new ()
        {
            Outline = Square(),
            Spacing = 0.5,
            Prototype = _ =>
            {
                built++;

                return new Group().Add(new Instance { Prototype = leaf });
            }
        };

        field.PrepareForRendering();

        Assert.AreEqual(16, field.Surfaces.Count);
        Assert.AreEqual(1, built, "the shape was built again for each place");
        Assert.IsTrue(field.Surfaces.All(surface => surface is Instance), "a copy was not an instance");
    }

    /// <summary>
    /// A sphere that moves while the shutter is open, which may not be shared.
    /// </summary>
    private static Sphere Moving() => new () { MotionAt = _ => Transforms.Translate(0.1, 0, 0) };

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

                return new Group().Add(Moving());
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
    /// The ground the drop tests stand things on: an ellipsoid ten across and three high, named
    /// 'hill', whose height and slope at any place are known exactly.
    /// </summary>
    private static Sphere Hill() => new ()
    {
        Name = "hill", Transform = Transforms.Scale(10, 3, 10)
    };

    private static double HillHeight(double x, double z) => 3 * Math.Sqrt(1 - (x * x + z * z) / 100);

    private static Vector HillUp(double x, double z) =>
        new Vector(x / 100, HillHeight(x, z) / 9, z / 100).Unit;

    /// <summary>
    /// This method stands a field beside a hill in a group, readies the group, and hands back the field.
    /// </summary>
    private static Field DroppedOnAHill(Field field, Matrix group = null, params Surface[] more)
    {
        Group holder = new () { Transform = group ?? Transforms.Scale(1) };

        field.Over ??= "hill";
        holder.Add(Hill()).Add(field);

        foreach (Surface surface in more)
            holder.Add(surface);

        holder.PrepareForRendering();

        return field;
    }

    /// <summary>
    /// A field dropped over a hill stands each copy on the hill where it lands -- at the hill's own
    /// height there -- and stands nothing where no hill lies below, however generous its outline.
    /// </summary>
    [TestMethod]
    public void TestADroppedFieldStandsOnTheGroundAndNowhereElse()
    {
        Field field = DroppedOnAHill(new Field
        {
            Outline = new GeneralPath().MoveTo(-14, -14).LineTo(14, -14).LineTo(14, 14).LineTo(-14, 14).ClosePath(),
            Spacing = 1, Jitter = 0.8, Prototype = _ => new Sphere()
        });

        Assert.IsTrue(field.Surfaces.Count > 250, $"only {field.Surfaces.Count} copies stood on the hill");

        foreach (Surface copy in field.Surfaces)
        {
            Point at = Point.Zero * copy.Transform;

            Assert.IsTrue(at.X * at.X + at.Z * at.Z < 100, $"a copy stood off the hill at {at}");
            Assert.AreEqual(HillHeight(at.X, at.Z), at.Y, 1e-6, $"a copy stood at the wrong height at {at}");
        }
    }

    /// <summary>
    /// A dropped copy falls onto the ground it was told of and nothing else: a roof over it does not
    /// catch it.
    /// </summary>
    [TestMethod]
    public void TestADroppedFieldFallsPastEverythingButItsGround()
    {
        Cube roof = new () { Transform = Transforms.Translate(0, 6, 0) * Transforms.Scale(20, 0.1, 20) };
        Field field = DroppedOnAHill(new Field { Outline = Square(), Spacing = 0.5, Prototype = _ => new Sphere() },
            more: roof);

        Assert.AreEqual(16, field.Surfaces.Count);
        Assert.IsTrue(field.Surfaces.All(copy => (Point.Zero * copy.Transform).Y < 3.01), "a copy was caught by the roof");
    }

    /// <summary>
    /// An upright copy keeps its own up whatever the slope; a tilted one leans with the ground, its up
    /// along the ground's own.
    /// </summary>
    [TestMethod]
    public void TestCopiesStandUprightUnlessTilted()
    {
        Field Dropped(bool tilted) => DroppedOnAHill(new Field
        {
            Outline = new GeneralPath().MoveTo(-8, -8).LineTo(8, -8).LineTo(8, 8).LineTo(-8, 8).ClosePath(),
            Spacing = 1.5, Jitter = 0.5, Tilted = tilted, Prototype = _ => new Sphere()
        });

        foreach (Surface copy in Dropped(false).Surfaces)
        {
            Vector up = (new Vector(0, 1, 0) * copy.Transform).Unit;

            Assert.AreEqual(1, up.Y, 1e-12, "an upright copy leaned");
        }

        int steep = 0;

        // The slope is read from the ground a little way either side of a copy, so it is judged where a
        // copy could stand at all -- up to sixty degrees -- rather than at the hill's very rim, where the
        // ground turns over faster than that little way can follow.
        foreach (Surface copy in Dropped(true).Surfaces)
        {
            Point at = Point.Zero * copy.Transform;
            Vector up = (new Vector(0, 1, 0) * copy.Transform).Unit;
            Vector expected = HillUp(at.X, at.Z);

            if (expected.Y < 0.5)
                continue;

            Assert.AreEqual(1, up.Dot(expected), 2e-4, $"a tilted copy at {at} did not lean with the ground");

            if (expected.Y < 0.9)
                steep++;
        }

        Assert.IsTrue(steep > 5, "too few copies stood on a slope to judge by");
    }

    /// <summary>
    /// Ground too steep, too low or too high stands nothing, and a copy sunk into the ground stands that
    /// much lower along its own up.
    /// </summary>
    [TestMethod]
    public void TestSteepLowAndHighGroundStandNothing()
    {
        GeneralPath wide = new GeneralPath().MoveTo(-12, -12).LineTo(12, -12).LineTo(12, 12).LineTo(-12, 12).ClosePath();
        Field all = DroppedOnAHill(new Field { Outline = wide, Spacing = 0.7, Prototype = _ => new Sphere() });
        Field gentle = DroppedOnAHill(new Field
        {
            Outline = wide, Spacing = 0.7, Steepest = 30.0.ToRadians(), Prototype = _ => new Sphere()
        });
        Field band = DroppedOnAHill(new Field
        {
            Outline = wide, Spacing = 0.7, Above = 1, Below = 2.5, Prototype = _ => new Sphere()
        });
        Field sunk = DroppedOnAHill(new Field
        {
            Outline = Square(), Spacing = 0.5, Sink = 0.25, Prototype = _ => new Sphere()
        });

        Assert.IsTrue(gentle.Surfaces.Count < all.Surfaces.Count * 0.9, "steep ground was not left bare");
        Assert.IsTrue(gentle.Surfaces.Count > all.Surfaces.Count * 0.3, "gentle ground was left bare too");

        foreach (Surface copy in gentle.Surfaces)
        {
            Point at = Point.Zero * copy.Transform;

            Assert.IsTrue(HillUp(at.X, at.Z).Y >= Math.Cos(30.0.ToRadians()) - 2e-3, $"a copy stood on steep ground at {at}");
        }

        Assert.IsTrue(band.Surfaces.Count is > 20, "the band of heights stood too little");
        Assert.IsTrue(band.Surfaces.All(copy => (Point.Zero * copy.Transform).Y is >= 1 and <= 2.5), "a copy stood out of its band");

        foreach (Surface copy in sunk.Surfaces)
        {
            Point at = Point.Zero * copy.Transform;

            Assert.AreEqual(HillHeight(at.X, at.Z) - 0.25, at.Y, 1e-6, "a sunk copy stood at the wrong depth");
        }
    }

    /// <summary>
    /// A field moved and turned on its own, beside its ground in a group that is moved, turned and scaled
    /// as well, still stands its copies on the ground where they fall in the space the two share -- and
    /// rays across the group find the copies where they fell, the field having sorted them and boxed
    /// itself again.
    /// </summary>
    [TestMethod]
    public void TestADroppedFieldWorksInTheSharedSpaceAndIsFoundWhereItFell()
    {
        Field field = new ()
        {
            Outline = Square(), Spacing = 0.5, Over = "hill",
            Transform = Transforms.Translate(2, 0, -1) * Transforms.RotateAroundY(30),
            Prototype = _ => new Sphere { Transform = Transforms.Scale(0.1) }
        };
        Group holder = new ()
        {
            Transform = Transforms.Translate(3, 1, -2) * Transforms.RotateAroundY(35) * Transforms.Scale(0.5)
        };

        holder.Add(Hill()).Add(field);
        holder.PrepareForRendering();

        Assert.AreEqual(16, field.Surfaces.Count);

        double[] grid = [-0.75, -0.25, 0.25, 0.75];

        foreach (Surface copy in field.Surfaces)
        {
            Point own = Point.Zero * copy.Transform;
            Point shared = Point.Zero * (field.Transform * copy.Transform);

            // Where the field's grid put it, in the field's own space; only its height has changed.
            Assert.IsTrue(grid.Any(x => Math.Abs(own.X - x) < 1e-9) && grid.Any(z => Math.Abs(own.Z - z) < 1e-9),
                $"a copy fell away from its place on the grid, to {own}");
            Assert.AreEqual(HillHeight(shared.X, shared.Z), shared.Y, 1e-6, $"a copy stood at the wrong height at {shared}");

            // Across the group, level with the copy's middle, in the group's own space.
            List<Intersection> hits = [];

            holder.AddIntersections(new Ray(new Point(shared.X - 20, shared.Y, shared.Z), new Vector(1, 0, 0)), hits);

            Assert.IsTrue(hits.Any(hit => Math.Abs(hit.Distance - (20 - 0.1)) < 1e-6),
                $"a ray across the group did not find the copy standing at {shared}");
        }
    }

    /// <summary>
    /// A copy stands on the ground's skin, not on the fur the ground grows.
    /// </summary>
    [TestMethod]
    public void TestADroppedCopyStandsOnTheSkinNotTheFur()
    {
        Sphere furry = new ()
        {
            Name = "hill", Transform = Transforms.Scale(10, 3, 10),
            // Lying nearly flat, so that every drop crosses fur on its way to the skin.
            HairGrowth = new HairGrowth { Count = 20000, Length = 0.6, Thickness = 0.12, Lean = 1.4 }
        };
        Group holder = new ();
        Field field = new () { Outline = Square(), Spacing = 0.5, Over = "hill", Prototype = _ => new Sphere() };

        holder.Add(furry).Add(field);
        holder.PrepareForRendering();

        Assert.AreEqual(16, field.Surfaces.Count);
        Assert.IsTrue(field.Surfaces.All(copy => Math.Abs((Point.Zero * copy.Transform).Y - HillHeight(
            (Point.Zero * copy.Transform).X, (Point.Zero * copy.Transform).Z)) < 1e-6), "a copy stood on the fur");
    }

    /// <summary>
    /// The scene's side: every clause reads, a field from a primitive may be dropped by the block on its
    /// call, and naming nothing beside the field is refused with the names that are there.
    /// </summary>
    [TestMethod]
    public void TestASceneMayDropAFieldOntoTheGround()
    {
        const string ground = "sphere { scale [3, 1, 3]  named 'hill' }";

        int dropped = Lit($$"""
                            {{Ball}}
                            {{ground}}
                            field { of Ball(0)  {{Outline}}  spacing 0.5  over 'hill' tilted  above 0.1  below 5  steepest 60  sink 0.02 }
                            """);

        Assert.IsTrue(dropped > 0, "the dropped field showed nothing");
        Assert.IsNull(ErrorFrom($$"""
                                  {{Ball}}
                                  {{ground}}
                                  primitive Patch(outline) -> field {
                                      return field { of Ball(0)  within outline  spacing 0.5 }
                                  }
                                  Square = path { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close }
                                  object Patch(Square) { over 'hill' tilted }
                                  """), "a field from a primitive should drop by the block on its call");

        string missing = ErrorFrom($$"""
                                     {{Ball}}
                                     {{ground}}
                                     field { of Ball(0)  {{Outline}}  spacing 0.5  over 'mountain' }
                                     """);

        Assert.IsNotNull(missing, "dropping onto nothing should have been refused");
        Assert.IsTrue(missing.Contains("'mountain'") && missing.Contains("'hill'"), $"the complaint was: {missing}");
    }

    // ---------------------------------------------------------------------------------------------
    // Along a path.
    // ---------------------------------------------------------------------------------------------

    // The four cubics that make a circle as nearly as four can.
    private const double Kappa = 0.5522847498;

    /// <summary>
    /// A circle of the given radius about the origin, drawn anticlockwise from its rightmost point and
    /// ending where it starts -- but never closed by name.
    /// </summary>
    private static GeneralPath Circle(double radius)
    {
        double k = radius * Kappa;

        return new GeneralPath()
            .MoveTo(radius, 0)
            .CubicTo(radius, k, k, radius, 0, radius)
            .CubicTo(-k, radius, -radius, k, -radius, 0)
            .CubicTo(-radius, -k, -k, -radius, 0, -radius)
            .CubicTo(k, -radius, radius, -k, radius, 0);
    }

    private static Field Laid(GeneralPath path, double spacing, Action<Field> more = null)
    {
        Field field = new () { Along = path, Spacing = spacing, Prototype = _ => new Sphere() };

        more?.Invoke(field);
        field.PrepareForRendering();

        return field;
    }

    private static List<Point> WhereEach(Field field) =>
        field.Surfaces.Select(copy => Point.Zero * copy.Transform).ToList();

    /// <summary>
    /// An open path is laid from end to end: a whole number of gaps, each the spacing asked or a little
    /// more, so there is a copy at both ends -- the posts of a fence.
    /// </summary>
    [TestMethod]
    public void TestAnOpenPathIsLaidEndToEndAtTheSpacingOrALittleMore()
    {
        GeneralPath line = new GeneralPath().MoveTo(0, 0).LineTo(10, 0);

        void AssertStops(double spacing, params double[] expected)
        {
            List<Point> stops = WhereEach(Laid(line, spacing));

            Assert.HasCount(expected.Length, stops, $"at a spacing of {spacing}");

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.AreEqual(expected[index], stops[index].X, 1e-9, $"stop {index} at a spacing of {spacing}");
                Assert.AreEqual(0, stops[index].Z, 1e-9, $"stop {index} left the path at a spacing of {spacing}");
            }
        }

        AssertStops(2.5, 0, 2.5, 5, 7.5, 10);
        AssertStops(3, 0, 10 / 3.0, 20 / 3.0, 10);
        AssertStops(20, 0, 10);
    }

    /// <summary>
    /// A closed path goes round without two copies at its seam, whether it was closed by name or merely
    /// ends where it starts.
    /// </summary>
    [TestMethod]
    public void TestAClosedPathGoesRoundWithoutDoublingItsSeam()
    {
        GeneralPath square = new GeneralPath().MoveTo(0, 0).LineTo(4, 0).LineTo(4, 4).LineTo(0, 4).ClosePath();
        List<Point> round = WhereEach(Laid(square, 2));

        Assert.HasCount(8, round, "a square sixteen round at a spacing of two");
        Assert.AreEqual(new Point(0, 0, 0), round[0]);
        Assert.AreEqual(new Point(4, 0, 2), round[3]);
        Assert.AreEqual(new Point(0, 0, 2), round[7]);

        List<Point> ring = WhereEach(Laid(Circle(5), 1));

        Assert.HasCount((int) Math.Floor(10 * Math.PI), ring, "a circle that ends where it starts is closed");

        for (int index = 0; index < ring.Count; index++)
        {
            Point next = ring[(index + 1) % ring.Count];

            // Four cubics stray a little from a true circle: some 0.0013 at this size.
            Assert.AreEqual(5, new Vector(ring[index].X, 0, ring[index].Z).Magnitude, 2e-3, $"stop {index} left the circle");
            Assert.AreEqual(10 * Math.PI / ring.Count, (next - ring[index]).Magnitude, 2e-3,
                $"stops {index} and the next are not the even share of the way round apart");
        }
    }

    /// <summary>
    /// Each copy is turned so its X runs the way the path does where it stands, which puts what faces
    /// -Z as made on the path's right as it is walked.
    /// </summary>
    [TestMethod]
    public void TestEachCopyRunsWithThePath()
    {
        // Walking +Z, the right is +X.
        foreach (Surface copy in Laid(new GeneralPath().MoveTo(0, 0).LineTo(0, 10), 2).Surfaces)
        {
            Assert.IsTrue((new Vector(1, 0, 0) * copy.Transform).Matches(new Vector(0, 0, 1)), "a copy's X did not run with the path");
            Assert.IsTrue((new Vector(0, 0, -1) * copy.Transform).Matches(new Vector(1, 0, 0)), "a copy's front did not face the path's right");
        }

        // Round an anticlockwise circle, each runs along the circle, anticlockwise.
        double worst = 0;
        Point where = null;

        foreach (Surface copy in Laid(Circle(5), 1).Surfaces)
        {
            Point at = Point.Zero * copy.Transform;
            double off = (new Vector(1, 0, 0) * copy.Transform - new Vector(-at.Z, 0, at.X).Unit).Magnitude;

            if (off > worst)
                (worst, where) = (off, at);
        }

        // Four cubics run a little off a true circle's way as well as off its radius: up to 0.0012.
        Assert.IsTrue(worst < 2e-3, $"the copy at {where} did not run round the circle: {worst}");
    }

    /// <summary>
    /// A field's turns spread about the way the path runs, rather than about the way the thing was made.
    /// </summary>
    [TestMethod]
    public void TestTurnsAlongAPathSpreadAboutTheWayItRuns()
    {
        Field field = Laid(new GeneralPath().MoveTo(0, 0).LineTo(0, 20), 0.2, field => field.Turn = 40.0.ToRadians());
        double facing = Math.Atan2(-1, 0);
        List<double> offs = field.Surfaces
            .Select(copy => TurnAndSizeOf(copy.Transform).Turn - facing)
            .ToList();

        Assert.IsTrue(offs.All(off => Math.Abs(off) <= 20.0.ToRadians() + 1e-9), "a copy turned too far from the path");
        Assert.IsTrue(offs.Max() - offs.Min() > 36.0.ToRadians(), "the turns did not spread over the range");
    }

    /// <summary>
    /// Rows lie side by side, centred on the path, and each is spaced along its own length: round a
    /// circle, the row outside holds more than the row inside.  An offset moves them all to one side.
    /// </summary>
    [TestMethod]
    public void TestRowsLieSideBySideEachSpacedAlongItsOwnLength()
    {
        // Anticlockwise, the path's left is the inside of the circle.
        List<Point> three = WhereEach(Laid(Circle(5), 1, field =>
        {
            field.Rows = 3;
            field.Apart = 1;
        }));
        Dictionary<int, int> byRadius = three
            .GroupBy(at => (int) Math.Round(new Vector(at.X, 0, at.Z).Magnitude * 10))
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.HasCount(3, byRadius, $"the rows stood at radii {string.Join(", ", byRadius.Keys)}");
        Assert.AreEqual((int) Math.Floor(8 * Math.PI), byRadius[40], "the inside row");
        Assert.AreEqual((int) Math.Floor(10 * Math.PI), byRadius[50], "the middle row");
        Assert.AreEqual((int) Math.Floor(12 * Math.PI), byRadius[60], "the outside row");

        GeneralPath line = new GeneralPath().MoveTo(0, 0).LineTo(10, 0);

        // Walking +X, the left is +Z.
        Assert.IsTrue(WhereEach(Laid(line, 2, field => field.Offset = 1.5)).All(at => Math.Abs(at.Z - 1.5) < 1e-9),
            "an offset did not set the row to the path's left");
        Assert.IsTrue(WhereEach(Laid(line, 2, field => field.Offset = -1.5)).All(at => Math.Abs(at.Z + 1.5) < 1e-9),
            "a negative offset did not set the row to the path's right");

        List<double> sides = WhereEach(Laid(line, 2, field =>
            {
                field.Rows = 2;
                field.Apart = 3;
                field.Offset = 4;
            }))
            .Select(at => at.Z)
            .Distinct()
            .Order()
            .ToList();

        CollectionAssert.AreEqual(new List<double> { 2.5, 5.5 }, sides, "two rows offset to one side");
    }

    /// <summary>
    /// Each run of a path is laid by itself, with copies at both of its own ends.
    /// </summary>
    [TestMethod]
    public void TestEachRunOfAPathIsLaidByItself()
    {
        GeneralPath two = new GeneralPath()
            .MoveTo(0, 0).LineTo(4, 0)
            .MoveTo(0, 5).LineTo(0, 9);
        List<Point> stops = WhereEach(Laid(two, 2));

        CollectionAssert.AreEqual(
            new List<Point>
            {
                new (0, 0, 0), new (2, 0, 0), new (4, 0, 0),
                new (0, 0, 5), new (0, 0, 7), new (0, 0, 9)
            },
            stops);
    }

    /// <summary>
    /// Jitter along a path moves each copy along it and across it, by no more than its share of the
    /// spacing -- and it is drawn from the place, so the same field stands the same way twice.
    /// </summary>
    [TestMethod]
    public void TestJitterAlongAPathStaysWithinItsShare()
    {
        GeneralPath line = new GeneralPath().MoveTo(0, 0).LineTo(40, 0);
        List<Point> plain = WhereEach(Laid(line, 2));
        List<Point> jittered = WhereEach(Laid(line, 2, field => field.Jitter = 0.5));

        Assert.HasCount(plain.Count, jittered);
        Assert.IsTrue(plain.Zip(jittered).All(pair =>
            Math.Abs(pair.First.X - pair.Second.X) <= 0.5 + 1e-9 && Math.Abs(pair.Second.Z) <= 0.5 + 1e-9),
            "a copy strayed further than its share");
        Assert.IsTrue(jittered.Count(at => Math.Abs(at.Z) > 0.1) > plain.Count / 3, "the copies were hardly moved across");
        CollectionAssert.AreEqual(jittered, WhereEach(Laid(line, 2, field => field.Jitter = 0.5)), "the jitter is not the same twice");
    }

    /// <summary>
    /// A field laid along a path may be dropped onto the ground like any other.
    /// </summary>
    [TestMethod]
    public void TestAFieldAlongAPathDropsOntoTheGround()
    {
        Field field = DroppedOnAHill(new Field
        {
            Along = new GeneralPath().MoveTo(-12, 1).LineTo(12, 1), Spacing = 1, Prototype = _ => new Sphere()
        });
        List<Point> stops = WhereEach(field);

        // Twenty-five stops along the line, of which those within the hill's ten either side of it stand.
        Assert.HasCount(19, stops);

        foreach ((Point at, Surface copy) in stops.Zip(field.Surfaces))
        {
            Assert.AreEqual(HillHeight(at.X, at.Z), at.Y, 1e-6, $"a copy stood at the wrong height at {at}");
            Assert.IsTrue((new Vector(1, 0, 0) * copy.Transform).Matches(new Vector(1, 0, 0)), "a dropped copy did not run with the path");
        }
    }

    /// <summary>
    /// A field fills an outline or follows a path, and says so if it is told both or neither, or given
    /// rows it cannot lay.
    /// </summary>
    [TestMethod]
    public void TestAFieldFillsOrFollowsButNotBoth()
    {
        GeneralPath line = new GeneralPath().MoveTo(0, 0).LineTo(10, 0);

        void AssertRefused(Field field, string expected)
        {
            field.Prototype = _ => new Sphere();
            field.Spacing = 1;

            Exception thrown = Assert.ThrowsExactly<Exception>(() => field.PrepareForRendering());

            StringAssert.Contains(thrown.Message, expected);
        }

        AssertRefused(new Field(), "an outline to fill or a path to follow");
        AssertRefused(new Field { Outline = Square(), Along = line }, "not both");
        AssertRefused(new Field { Outline = Square(), Rows = 2, Apart = 1 }, "rows and offset are for one that follows a path");
        AssertRefused(new Field { Along = line, Rows = 0 }, "at least one row");
        AssertRefused(new Field { Along = line, Rows = 2 }, "some distance apart");
        AssertRefused(new Field { Along = new GeneralPath() }, "has to have something in it");
    }

    /// <summary>
    /// A scene lays a field along a path written out or named, in rows and to one side.
    /// </summary>
    [TestMethod]
    public void TestASceneMayLayAFieldAlongAPath()
    {
        int written = Lit($$"""
                            {{Ball}}
                            field { of Ball(0)  along { move to -1, 0  line to 1, 0 }  spacing 0.5  rows 2 apart 0.6  offset 0.2 }
                            """);
        int named = Lit($$"""
                          {{Ball}}
                          Line = path { move to -1, 0  line to 1, 0 }
                          field { of Ball(0)  along Line  spacing 0.5  rows 2 apart 0.6  offset 0.2 }
                          """);

        Assert.IsTrue(written > 0, "the field along a path showed nothing");
        Assert.AreEqual(written, named, "a named path did not lay the same field as one written out");

        string unspaced = ErrorFrom($$"""
                                      {{Ball}}
                                      field { of Ball(0)  along { move to -1, 0  line to 1, 0 }  spacing 0.5  rows 2 }
                                      """);

        Assert.IsNotNull(unspaced, "rows without how far apart should have been refused");
        StringAssert.Contains(unspaced, "apart");
    }

    // ---------------------------------------------------------------------------------------------
    // Patterns.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// A pattern rising across the square from one side to the other, never wrapping inside it.
    /// </summary>
    private static DensityShape Across() => new ()
    {
        Pattern = new GradientPattern { BandType = BandType.LinearX },
        Transform = Transforms.Translate(-1.01, 0, 0) * Transforms.Scale(2.03, 1, 1)
    };

    /// <summary>
    /// The same, but with its values crowded toward nought, as a noise pattern's crowd toward its middle:
    /// banded by their raw value, three-quarters of the places would fall below a half.
    /// </summary>
    private static DensityShape Crowded() => new ()
    {
        Pattern = new GradientPattern { BandType = BandType.LinearX, Wave = WaveType.Poly, Exponent = 4 },
        Transform = Transforms.Translate(-1.01, 0, 0) * Transforms.Scale(2.03, 1, 1)
    };

    private static Field Patterned(Action<Field> more)
    {
        Field field = new () { Outline = Square(), Spacing = 0.05, Jitter = 0.5, Prototype = _ => new Sphere() };

        more(field);
        field.PrepareForRendering();

        return field;
    }

    private static double ValueOf(DensityShape shape, Surface copy)
    {
        Point at = Point.Zero * copy.Transform;

        return shape.ValueAt(new Point(at.X, 0, at.Z));
    }

    private static HashSet<(long, long)> PlacesOf(Field field) => field.Surfaces
        .Select(copy => Point.Zero * copy.Transform)
        .Select(at => ((long) Math.Round(at.X * 1e6), (long) Math.Round(at.Z * 1e6)))
        .ToHashSet();

    /// <summary>
    /// A field's patches keep a place as often as the pattern's value there says: in each tenth of the
    /// pattern's range, about as many of the places as the middle of that tenth.
    /// </summary>
    [TestMethod]
    public void TestPatchesKeepAPlaceAsOftenAsThePatternSays()
    {
        DensityShape across = Across();
        Field everywhere = Patterned(_ => { });
        Field kept = Patterned(field => field.Patches = across);

        int[] all = new int[10];
        int[] stood = new int[10];

        foreach (Surface copy in everywhere.Surfaces)
            all[Math.Min(9, (int) (ValueOf(across, copy) * 10))]++;

        foreach (Surface copy in kept.Surfaces)
            stood[Math.Min(9, (int) (ValueOf(across, copy) * 10))]++;

        for (int tenth = 0; tenth < 10; tenth++)
        {
            Assert.IsTrue(all[tenth] > 100, $"only {all[tenth]} places fell in tenth {tenth}");
            Assert.AreEqual((tenth + 0.5) / 10, stood[tenth] / (double) all[tenth], 0.1,
                $"tenth {tenth} kept {stood[tenth]} of {all[tenth]}");
        }
    }

    /// <summary>
    /// An edge with no width cuts clean; a wider one thins between its two ends; and turned about, an
    /// edge keeps exactly the places the edge the right way round leaves.
    /// </summary>
    [TestMethod]
    public void TestAnEdgeCutsCleanAndTurnedAboutFillsWhatItCut()
    {
        DensityShape across = Across();
        Field everywhere = Patterned(_ => { });
        Field cut = Patterned(field =>
        {
            field.Patches = across;
            field.PatchesFrom = field.PatchesTo = 0.4;
        });

        Assert.AreEqual(
            everywhere.Surfaces.Count(copy => ValueOf(across, copy) >= 0.4), cut.Surfaces.Count,
            "a clean edge did not keep exactly the places at or above it");
        Assert.IsTrue(cut.Surfaces.All(copy => ValueOf(across, copy) >= 0.4), "a clean edge kept a place below it");

        Field soft = Patterned(field =>
        {
            field.Patches = across;
            field.PatchesFrom = 0.3;
            field.PatchesTo = 0.5;
        });
        List<double> values = soft.Surfaces.Select(copy => ValueOf(across, copy)).ToList();

        Assert.IsTrue(values.All(value => value >= 0.3), "a soft edge kept a place below where it begins");
        Assert.AreEqual(everywhere.Surfaces.Count(copy => ValueOf(across, copy) > 0.5), values.Count(value => value > 0.5),
            "a soft edge did not keep every place beyond where it ends");

        int between = everywhere.Surfaces.Count(copy => ValueOf(across, copy) is >= 0.3 and <= 0.5);
        int keptBetween = values.Count(value => value <= 0.5);

        Assert.IsTrue(keptBetween > between / 4 && keptBetween < between * 3 / 4,
            $"a soft edge kept {keptBetween} of the {between} places between its ends");

        Field reversed = Patterned(field =>
        {
            field.Patches = across;
            field.PatchesFrom = 0.5;
            field.PatchesTo = 0.3;
        });
        HashSet<(long, long)> one = PlacesOf(soft);
        HashSet<(long, long)> other = PlacesOf(reversed);

        Assert.IsFalse(one.Overlaps(other), "an edge turned about kept a place the other kept too");
        Assert.IsTrue(PlacesOf(everywhere).SetEquals(one.Union(other)), "between them the two edges left places empty");
    }

    /// <summary>
    /// A pattern picks the first thing where it is lowest and the last where it is highest, each as large
    /// a share of the places as its weight -- however the pattern's own values happen to fall -- and the
    /// groves mingle only near where they meet.
    /// </summary>
    [TestMethod]
    public void TestAPatternPicksEachThingWhereItRanksAndAsMuchAsItWeighs()
    {
        DensityShape across = Crowded();
        Field field = Patterned(field =>
        {
            field.PickedBy = across;
            field.Choices.Add(new FieldChoice(_ => new Sphere(), 1));
            field.Choices.Add(new FieldChoice(_ => new Cube(), 1));
            field.Choices.Add(new FieldChoice(_ => new Cylinder(), 2));
        });
        List<(double Rank, Type Kind)> ranked = field.Surfaces
            .Select(copy => (Value: ValueOf(across, copy), Kind: ((Instance) copy).Prototype.GetType()))
            .OrderBy(pair => pair.Value)
            .Select((pair, index) => ((index + 0.5) / field.Surfaces.Count, pair.Kind))
            .ToList();

        Assert.AreEqual(0.25, ranked.Count(pair => pair.Kind == typeof(Sphere)) / (double) ranked.Count, 0.03, "the first thing's share");
        Assert.AreEqual(0.25, ranked.Count(pair => pair.Kind == typeof(Cube)) / (double) ranked.Count, 0.03, "the second thing's share");
        Assert.AreEqual(0.50, ranked.Count(pair => pair.Kind == typeof(Cylinder)) / (double) ranked.Count, 0.03, "the third thing's share");

        Assert.IsTrue(ranked.Where(pair => pair.Rank < 0.15).All(pair => pair.Kind == typeof(Sphere)),
            "something else stood where the pattern is lowest");
        Assert.IsTrue(ranked.Where(pair => pair.Rank > 0.6).All(pair => pair.Kind == typeof(Cylinder)),
            "something else stood where the pattern is highest");

        // Mingled: each crosses a little way into the other's share, rather than the two meeting in a line.
        Assert.IsTrue(ranked.Any(pair => pair.Rank < 0.24 && pair.Kind == typeof(Cube)) &&
                      ranked.Any(pair => pair.Rank > 0.26 && pair.Kind == typeof(Sphere)),
            "the first two did not mingle where they meet");
    }

    /// <summary>
    /// A pattern sets each size from the smallest where it is lowest to the largest where it is highest,
    /// spanning the whole range asked for.
    /// </summary>
    [TestMethod]
    public void TestAPatternSetsSizesFromTheSmallestToTheLargest()
    {
        DensityShape across = Crowded();
        Field field = Patterned(field =>
        {
            field.SizedBy = across;
            field.SmallestSize = 0.5;
            field.LargestSize = 1.5;
        });
        List<(double Value, double Size)> pairs = field.Surfaces
            .Select(copy => (ValueOf(across, copy), TurnAndSizeOf(copy.Transform).Size))
            .ToList();

        Assert.IsTrue(pairs.All(pair => pair.Size is >= 0.5 - 1e-12 and <= 1.5 + 1e-12), "a size fell outside the range");

        // Evenly over the range, however the pattern's own values crowd.
        for (int fifth = 0; fifth < 5; fifth++)
        {
            int count = pairs.Count(pair => Math.Min(4, (int) ((pair.Size - 0.5) * 5)) == fifth);

            Assert.AreEqual(0.2, count / (double) pairs.Count, 0.05, $"fifth {fifth} of the sizes held {count} of {pairs.Count}");
        }

        // And in the pattern's own order: ranked by value and by size, the two orders nearly agree.
        double[] byValue = RanksIn(pairs.Select(pair => pair.Value).ToList());
        double[] bySize = RanksIn(pairs.Select(pair => pair.Size).ToList());
        double agreement = 1 - 6 * byValue.Zip(bySize).Sum(pair => (pair.First - pair.Second) * (pair.First - pair.Second)) /
                           (pairs.Count * ((double) pairs.Count * pairs.Count - 1));

        Assert.IsTrue(agreement > 0.95, $"the sizes did not follow the pattern: {agreement}");
    }

    private static double[] RanksIn(List<double> values)
    {
        double[] ranks = new double[values.Count];
        int position = 0;

        foreach (int index in Enumerable.Range(0, values.Count).OrderBy(index => values[index]))
            ranks[index] = position++;

        return ranks;
    }

    /// <summary>
    /// A field takes its things in turn or picks them by a pattern, and a scene may say all of it.
    /// </summary>
    [TestMethod]
    public void TestASceneMayPatternAField()
    {
        Exception thrown = Assert.ThrowsExactly<Exception>(() => Patterned(field =>
        {
            field.Cycle = true;
            field.PickedBy = Across();
            field.Choices.Add(new FieldChoice(_ => new Sphere()));
            field.Choices.Add(new FieldChoice(_ => new Cube()));
        }));

        StringAssert.Contains(thrown.Message, "in turn or picks them by a pattern");

        int lit = Lit($$"""
                        {{Ball}}
                        field {
                            of Ball(0), Ball(2)  picked by bozo { scale 0.5 }
                            {{Outline}}  spacing 0.1  jitter 0.8
                            patches bozo { scale 0.4 } from 0.3 to 0.4
                            sized 0.5 to 1.5 by bozo { scale 0.3 }
                        }
                        """);
        int softly = Lit($$"""
                           {{Ball}}
                           field { of Ball(0)  {{Outline}}  spacing 0.1  patches bozo { scale 0.4 } }
                           """);

        Assert.IsTrue(lit > 0 && softly > 0, "a patterned field showed nothing");

        string unfinished = ErrorFrom($$"""
                                        {{Ball}}
                                        field { of Ball(0)  {{Outline}}  spacing 0.1  patches bozo { scale 0.4 } from 0.3 }
                                        """);

        Assert.IsNotNull(unfinished, "an edge with no end should have been refused");
        StringAssert.Contains(unfinished, "to");

        // A pattern sets a size between two, so one size alone would leave it nothing to do.
        string alone = ErrorFrom($$"""
                                   {{Ball}}
                                   field { of Ball(0)  {{Outline}}  spacing 0.1  sized 2 by bozo { scale 0.3 } }
                                   """);

        Assert.IsNotNull(alone, "a pattern setting one size should have been refused");
        StringAssert.Contains(alone, "between two");
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
