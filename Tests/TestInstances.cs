using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover a shape built once and stood in several places at once.
/// <para>
/// **Every one of them is a pixel-for-pixel comparison, and it has to be.**  Sharing geometry is
/// worth nothing if it changes what is drawn, and the ways it can go wrong are all quiet ones: a
/// pattern read in the wrong space still paints a pattern, and a normal carried out through the
/// wrong chain still lights the surface.  The only answer that means anything here is "the same
/// picture".
/// </para>
/// </summary>
[TestClass]
public class TestInstances
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"instance-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A checker rather than a plain color, because a solid pigment is read at no point at all and
    /// would go on looking right however badly the spaces were confused.  The same goes for the
    /// scale: a pattern on an unscaled shape is the one case where getting the transform chain
    /// wrong still lands on the right answer.
    /// </summary>
    private static Material Checkered()
    {
        PigmentSet colors = new ();

        colors.AddEntry(new SolidPigment(new Color(0.85, 0.30, 0.25)));
        colors.AddEntry(new SolidPigment(new Color(0.20, 0.35, 0.70)));

        return new Material
        {
            Pigment = new PatternPigment
            {
                Pattern = new CheckerPattern(),
                PigmentSet = colors,
                Transform = Transforms.Scale(0.34)
            },
            Ambient = 0.15,
            Specular = 0.4
        };
    }

    /// <summary>
    /// Glass, so that a ray goes through the shape rather than stopping at it -- which is what makes
    /// the refraction test able to tell one ball from two.
    /// </summary>
    private static Material Glass()
    {
        return new Material
        {
            Pigment = new SolidPigment(new Color(0.05, 0.08, 0.06)),
            Transparency = 0.92,
            Reflective = 0.15,
            Ambient = 0.05,
            Interior = new Interior { IndexOfRefraction = 1.52 }
        };
    }

    /// <summary>
    /// Renders whatever the given filling puts into a scene, under the same camera and light as the
    /// rest of these.
    /// </summary>
    /// <param name="fill">What to put in the scene.</param>
    /// <returns>The picture.</returns>
    private static Canvas Rendered(Action<Scene> fill, Point where = null)
    {
        Scene scene = new ();

        scene.Lights.Add(new PointLight { Location = new Point(-6, 7, -8) });

        fill(scene);

        Camera camera = new ()
        {
            Location = where ?? new Point(2.6, 2.0, -4.5),
            LookAt = new Point(0, 0, 0),
            FieldOfView = Math.PI / 3
        };

        return camera.Render(new RenderContext { Width = 140, Height = 105 }, scene);
    }

    /// <summary>
    /// Builds the same picture twice over: once with the shape standing in the scene itself, and once
    /// with it shared and stood in place by an instance.  The two must come out identical.
    /// </summary>
    /// <param name="shared">Whether to put the shape in through an instance.</param>
    /// <param name="placings">Where to stand it, as many times as wanted.</param>
    /// <returns>The picture.</returns>
    private static Canvas Rendered(bool shared, params Matrix[] placings)
    {
        return Rendered(shared, Checkered, placings);
    }

    private static Canvas Rendered(bool shared, Func<Material> dress, params Matrix[] placings)
    {
        Scene scene = new ();

        scene.Lights.Add(new PointLight { Location = new Point(-6, 7, -8) });

        foreach (Matrix placing in placings)
        {
            // The shape carries a turn and a squash of its own, so that the instance's transform and
            // the shape's have to compose in the right order for the two pictures to agree.
            Matrix mine = Transforms.RotateAroundY(25) *
                          Transforms.Scale(0.8, 0.55, 0.8);

            if (shared)
            {
                Sphere shape = new () { Material = dress(), Transform = mine };

                scene.Surfaces.Add(new Instance { Prototype = shape, Transform = placing });
            }
            else
            {
                scene.Surfaces.Add(new Sphere
                {
                    Material = dress(), Transform = placing * mine
                });
            }
        }

        Camera camera = new ()
        {
            Location = new Point(2.6, 2.0, -4.5),
            LookAt = new Point(0, 0, 0),
            FieldOfView = Math.PI / 3
        };

        return camera.Render(
            new RenderContext { Width = 140, Height = 105 }, scene);
    }

    [TestMethod]
    public void TestAnInstanceStandsWhereTheShapeItselfWould()
    {
        // The plainest case, and the one that proves the transforms compose the right way round: the
        // shape's own turn and squash have to happen before the instance's placing, exactly as they
        // would if the two were written as one chain.
        AssertSame(
            Rendered(true, Transforms.Translate(0.6, 0.2, 0)),
            Rendered(false, Transforms.Translate(0.6, 0.2, 0)),
            "a shape stood by an instance did not land where the shape itself would");
    }

    [TestMethod]
    public void TestOneShapeStandsInSeveralPlacesAtOnce()
    {
        // The point of the thing.  Three instances of one shape, each placed and scaled differently,
        // against three shapes built separately -- and it is the *third* that matters most, since a
        // chain confused between instances would still get the first one right.
        Matrix[] placings =
        [
            Transforms.Translate(-1.3, 0, 0.4),
            Transforms.Translate(0.2, 0.5, 0) * Transforms.Scale(0.7),
            Transforms.Translate(1.4, -0.2, -0.5) * Transforms.RotateAroundZ(40)
        ];

        AssertSame(
            Rendered(true, placings), Rendered(false, placings),
            "three instances of one shape did not look like three shapes");
    }

    [TestMethod]
    public void TestAPatternOnASharedShapeIsReadInTheRightSpace()
    {
        // **This is the test the whole portal exists for.**  A pattern is worked out in the space of
        // the surface the ray met, which means carrying the point in from the world through every
        // transform between -- and a shared shape has no parent to walk, because it stands in more
        // than one place.  Get it wrong and the checker still paints a checker; it is simply the
        // wrong checker, and nothing says so.
        //
        // The instance is scaled as well as moved, so the pattern's squares must come out a different
        // size on it than on the shape at rest.  Two instances at different scales, so a chain that
        // ignored the instance entirely would paint both the same and be caught.
        Matrix[] placings =
        [
            Transforms.Translate(-1.0, 0, 0) * Transforms.Scale(1.5),
            Transforms.Translate(1.2, 0, 0) * Transforms.Scale(0.6)
        ];

        AssertSame(
            Rendered(true, placings), Rendered(false, placings),
            "a checker on a shared shape came out differently from the same checker on a shape of " +
            "its own; the point is being carried in through the wrong chain of transforms");
    }

    [TestMethod]
    public void TestANormalOnASharedShapeIsCarriedOutThroughTheInstance()
    {
        // **A shear, and it has to be a shear.**  A normal is carried out to the world by the
        // *inverse transpose* of the transform, and a translation leaves directions alone while a
        // diagonal matrix -- any scale -- is its own transpose.  So neither can tell an inverse from
        // an inverse transpose, and a test built from them passes on a normal path that ignores the
        // instance completely.  Only something off-diagonal separates the two.
        //
        // This was not a guess: dropping the instance from the normal path leaves the translate and
        // scale cases above passing and is caught by exactly one of them, the one that happens to
        // turn its third shape.
        Matrix[] placings =
        [
            Transforms.Translate(-0.9, 0, 0) * Transforms.Shear(0.45, 0, 0, 0.3, 0, 0),
            Transforms.Translate(1.1, 0, 0) * Transforms.Shear(0, 0.5, 0.35, 0, 0, 0)
        ];

        AssertSame(
            Rendered(true, placings), Rendered(false, placings),
            "a sheared instance is lit differently from a sheared shape of its own; the normal is " +
            "not being carried out through the instance's own transform");
    }

    [TestMethod]
    public void TestARayThroughTwoInstancesOfOneGlassShapeRefractsTwice()
    {
        // **A shape and the place it stands in are two different things, and refraction is where that
        // bites hardest.**  Which surfaces a ray is currently inside is tracked by identity, and a
        // shared shape hands back the *same* surface for every instance of it -- so a ray entering one
        // glass ball and then another reads as entering and then *leaving* the one ball, and comes out
        // the far side unrefracted.
        //
        // **The two balls stand one behind the other, and that is the whole test.**  Side by side they
        // prove nothing at all: no ray goes through both, the containment list never holds two, and
        // the confusion never arises.  The first version of this had them side by side and passed
        // against the very fault it was written for.
        Canvas Balls(bool shared) => Rendered(scene =>
        {
            Sphere shape = new () { Material = Glass(), Transform = Transforms.Scale(0.55) };

            // **They overlap, and that is the whole test.**  Two balls standing clear of each other
            // are entered and left one at a time, so the containment list never holds two and the
            // confusion never arises -- the second version of this test had them clear of each other
            // and passed against the very fault it was written for.  Overlapping, the ray is inside
            // both at once: enter, enter, leave, leave.
            foreach (double z in new[] { -0.3, 0.3 })
            {
                Matrix placing = Transforms.Translate(0, 0, z);

                scene.Surfaces.Add(shared
                    ? new Instance { Prototype = shape, Transform = placing }
                    : new Sphere { Material = Glass(), Transform = placing * Transforms.Scale(0.55) });
            }

            // Something behind them worth seeing through the glass, so that getting the refraction
            // wrong changes the picture rather than the emptiness.
            scene.Surfaces.Add(new Cube
            {
                Material = Checkered(), Transform = Transforms.Translate(0, 0, 4) *
                                                     Transforms.Scale(3, 3, 0.1)
            });
        }, new Point(0, 0, -5));

        AssertSame(Balls(true), Balls(false),
            "two instances of one glass shape do not refract like two glass shapes; which surfaces " +
            "a ray is inside is being tracked by the shape alone");
    }

    [TestMethod]
    public void TestACombinationMayHoldAnInstance()
    {
        // A combination decides whether a crossing belongs to its left or its right by looking for
        // that surface in its own tree -- and a shared shape lives *outside* the tree, behind the
        // instance.  Every crossing therefore looked like the right-hand side, and the difference came
        // out as the whole cube.
        Canvas shared = Rendered(scene =>
        {
            Cube bite = new () { Material = Checkered() };

            // **One shape on both sides, and that is the whole test.**  A combination decides which
            // side a crossing came from by looking for its surface in each side's tree -- and if the
            // same shared shape stands on both sides, the shape alone cannot say which.  Putting an
            // instance on one side only is got right by accident, since the walk now reaches through
            // an instance anyway; it is the *same* shape twice that has no answer.
            scene.Surfaces.Add(new CsgSurface
            {
                Operation = CsgOperation.Difference,
                Left = new Group().Add(new Instance
                {
                    Prototype = bite, Transform = Transforms.Scale(0.7)
                }),
                Right = new Group().Add(new Instance
                {
                    Prototype = bite, Transform = Transforms.Translate(0.45, 0.45, -0.5) *
                                                  Transforms.Scale(0.5)
                })
            });
        });
        Canvas direct = Rendered(scene => scene.Surfaces.Add(new CsgSurface
        {
            Operation = CsgOperation.Difference,
            Left = new Group().Add(new Cube
            {
                Material = Checkered(), Transform = Transforms.Scale(0.7)
            }),
            Right = new Group().Add(new Cube
            {
                Material = Checkered(), Transform = Transforms.Translate(0.45, 0.45, -0.5) *
                                                    Transforms.Scale(0.5)
            })
        }));

        AssertSame(shared, direct,
            "a bite taken out of a cube by an instance is not the bite the shape itself takes; the " +
            "combination cannot tell which of its sides the crossing came from");
    }

    [TestMethod]
    public void TestASharedShapeIsSettledBeforeRenderingLikeAnyOther()
    {
        // **The fault that actually changed three gallery scenes, and none of the tests above saw
        // it.**  Everything a surface needs settling before rendering -- the material it falls back
        // on, the scene's ambient scaling, the seed its pigment is sown with -- is done by walking the
        // scene, and that walk went into groups and combinations but not through an instance.  A
        // shape standing behind one was reached by none of it and came out unseeded and undressed.
        //
        // A scene calling a primitive twice has to look like the same contents written out twice.
        // That is the invariant the whole of the sharing rests on, and it is checked here at the only
        // level where the settling happens at all.
        //
        // **The scene scales its ambient, and that is what makes this bite.**  Ambient scaling is
        // applied by that same walk, to every material it reaches -- so a shape it never reached kept
        // its full ambient and came out plainly brighter.  Written with a pattern alone the test
        // passes either way, since a pattern carries its own seed and needs no settling: that version
        // of this test proved nothing.
        string twice = Rendered("""
            primitive marker() -> group {
                return group {
                    sphere { scale 0.42  material { pigment agate { [0, Red, 1, Yellow] } } }
                }
            }
            object marker() { translate X -0.7 }
            object marker() { translate X 0.7 }
            """);
        string longhand = Rendered("""
            group { sphere { scale 0.42  material { pigment agate { [0, Red, 1, Yellow] } } }
                    translate X -0.7 }
            group { sphere { scale 0.42  material { pigment agate { [0, Red, 1, Yellow] } } }
                    translate X 0.7 }
            """);

        Assert.AreEqual(longhand, twice,
            "a primitive called twice does not look like its contents written out twice");
    }

    [TestMethod]
    public void TestASharedShapeWearsWhatEachPlaceDressesItIn()
    {
        // A shape left undressed takes the material of whatever holds it, and a shared one is held in
        // several places at once.  Handed down when the scene was settled, the first holder's material
        // went into the one shape and every place wore it: two calls dressed red and blue came out both
        // red.  So a shared shape's surfaces wear, at each hit, what the instances it was met through
        // give that place -- as weather is settled.  Both ways of dressing a call are tried: on the call
        // itself, and by the group it stands in.
        string twice = Rendered("""
            primitive ball() -> group {
                return group { sphere { scale 0.42 } }
            }
            object ball() { material { pigment Red }  translate X -0.7 }
            object ball() { material { pigment Blue }  translate X 0.7 }
            group { object ball() { translate X -0.7 }  material { pigment Green }  translate Y -0.9 }
            group { object ball() { translate X 0.7 }  material { pigment Yellow }  translate Y -0.9 }
            """);
        string longhand = Rendered("""
            group { sphere { scale 0.42 }  material { pigment Red }  translate X -0.7 }
            group { sphere { scale 0.42 }  material { pigment Blue }  translate X 0.7 }
            group { group { sphere { scale 0.42 }  translate X -0.7 }  material { pigment Green }  translate Y -0.9 }
            group { group { sphere { scale 0.42 }  translate X 0.7 }  material { pigment Yellow }  translate Y -0.9 }
            """);

        Assert.AreEqual(longhand, twice, "a shared shape does not wear what each place dresses it in");
    }

    [TestMethod]
    public void TestAnImageOnWhatHoldsASharedShapeIsMappedForTheShape()
    {
        // An image takes its mapping from the shape it is put on when none is said: a ball's is
        // spherical.  Dressing a shared ball from the group holding it puts the material on the
        // instance, which has no mapping of its own; the material must be readied for the ball.
        string image = Path.Combine(_directory, "two-color.png");
        Canvas canvas = new (2, 2);

        canvas.SetColor(Colors.Red, 0, 0);
        canvas.SetColor(Colors.Red, 1, 0);
        canvas.SetColor(Colors.Blue, 0, 1);
        canvas.SetColor(Colors.Blue, 1, 1);
        new ImageFile(image).Save(canvas, new RenderContext { ApplyGamma = false });

        string shared = Rendered($$"""
            primitive ball() -> group {
                return group { sphere { scale 0.42 } }
            }
            group { object ball()  material { pigment image '{{image}}' } }
            """);
        string longhand = Rendered($$"""
            group { group { sphere { scale 0.42 } }  material { pigment image '{{image}}' } }
            """);

        Assert.AreEqual(longhand, shared, "an image on what holds a shared shape is not mapped for the shape");
    }

    [TestMethod]
    public void TestHairOnASharedShapeWearsWhatEachPlaceDressesItIn()
    {
        // Hair with no material of its own wears its skin's, taken when the scene is settled -- and the skin of a
        // shared shape left undressed wears, in each place, what holds it there.  So must its hair: dressed once,
        // the fur on both balls was the plain white of a surface given nothing (a half's mean 0.05 red, 0.027
        // green, against 0.09 and 0.0015).  The strands fall differently shared and written out, so it is each
        // half's color that is compared, not each pixel.
        Canvas shared = Picture("""
            primitive furry() -> group {
                return group { sphere { scale 0.42  hair { count 20000  length 0.35 } } }
            }
            group { object furry() { translate X -0.7 }  material { pigment Red } }
            group { object furry() { translate X 0.7 }  material { pigment Blue } }
            """);
        Canvas longhand = Picture("""
            group { group { sphere { scale 0.42  hair { count 20000  length 0.35 } }  translate X -0.7 }
                    material { pigment Red } }
            group { group { sphere { scale 0.42  hair { count 20000  length 0.35 } }  translate X 0.7 }
                    material { pigment Blue } }
            """);

        foreach ((int from, int to) in new[] { (0, 45), (45, 90) })
        {
            Color mine = MeanOver(shared, from, to);
            Color written = MeanOver(longhand, from, to);

            Assert.IsTrue(Math.Abs(mine.Red - written.Red) < 0.005 && Math.Abs(mine.Green - written.Green) < 0.005 &&
                          Math.Abs(mine.Blue - written.Blue) < 0.005,
                $"hair on a shared shape is {mine} where written out it is {written}");
        }
    }

    private static Color MeanOver(Canvas picture, int from, int to)
    {
        Color sum = Colors.Black;
        int count = 0;

        for (int y = 0; y < picture.Height; y++)
        for (int x = from; x < to; x++, count++)
            sum += picture.GetPixel(x, y);

        return sum * (1.0 / count);
    }

    [TestMethod]
    public void TestAnEmptyGroupIsNotSharedSoWhatHoldsItMayBe()
    {
        // **A fir's bough carries the snow lying on it, and in summer that is an empty group.**  Shared
        // like any other, it made each bough a shape holding a shared shape -- which may not itself be
        // shared -- so every fir built its sixty-six boughs again, needles and all: some 140 MB a tree,
        // and a field of four hundred firs swapped for minutes.  There is nothing in an empty group to
        // share, so it is handed back bare and the bough holding it is shared instead.
        string report = StatisticsOf("""
            primitive snow(on) -> group {
                if (on > 0) { return group { sphere { scale 0.2 } } }
                else { return group { } }
            }
            primitive bough(on) -> group {
                return group { sphere { scale 0.3 }  object snow(on)  object snow(on - 1) }
            }
            object bough(0) { translate X -1 }
            object bough(0)
            object bough(0) { translate X 1 }
            """);

        StringAssert.Contains(report, "1 shape, shown 3 times",
            "the bough was not shared; the report said:\n" + report);
    }

    [TestMethod]
    public void TestAPrimitiveHoldingSharedShapesIsSharedItself()
    {
        // **The tree is the point.**  A primitive whose body calls another hands back a shape holding an
        // instance, which could not be shared -- so a tree called twice was built twice, leaves and all,
        // and only its leaves were shared.  Now the tree is shared as well, and the census counts what
        // is shown: two trees, and three leaves in each of them, six in all.
        string report = StatisticsOf("""
            primitive leaf() -> group {
                return group { sphere { scale 0.1 } }
            }
            primitive tree(n) -> group {
                return group {
                    cylinder { min Y 0  max Y 1  scale 0.05 }
                    object leaf() { translate [0.2, 1, 0] }
                    object leaf() { translate [-0.2, 1, 0] }
                    object leaf() { translate [0, 1.2, 0] }
                }
            }
            object tree(1) { translate X -1 }
            object tree(1) { translate X 1 }
            """);

        StringAssert.Contains(report, "2 shapes, shown 8 times",
            "the tree was not shared, or its leaves were not counted in each; the report said:\n" + report);
    }

    [TestMethod]
    public void TestACallDressedByItsOwnBlockIsShared()
    {
        // A material written on the call dresses the shape where that call stands (TestASharedShapeWearsWhatEachPlace-
        // DressesItIn holds the picture to it), so the call no longer needs a shape of its own.
        string report = StatisticsOf("""
            primitive ball() -> group {
                return group { sphere { scale 0.42 } }
            }
            object ball() { material { pigment Red }  translate X -0.7 }
            object ball() { material { pigment Blue }  translate X 0.7 }
            """);

        StringAssert.Contains(report, "1 shape, shown 2 times",
            "a call dressed by its own block was not shared; the report said:\n" + report);
    }

    [TestMethod]
    public void TestACallWhoseMaterialHoldsAMediumOrThatGlowsIsNotShared()
    {
        // What is inside a thing is not shared, and a light made of a glow is made once for each surface that glows,
        // not once for each place it stands: such a call keeps a shape of its own.
        foreach (string block in new[]
                 {
                     "material { pigment White  transparency 1  interior { medium { absorption [0.5, 0.5, 0.5] } } }",
                     "material { pigment White  transparency 1  interior { medium { emission [2, 1.5, 1] } } }  gives light"
                 })
        {
            string report = StatisticsOf($$"""
                primitive ball() -> group {
                    return group { sphere { scale 0.42 } }
                }
                object ball() { {{block}}  translate X -0.7 }
                object ball() { {{block}}  translate X 0.7 }
                """);

            Assert.IsFalse(report.Contains("Shared"),
                $"a call dressed with `{block}` was shared; the report said:\n" + report);
        }
    }

    [TestMethod]
    public void TestDecalsOnASharedCallLieOverWhatHoldsIt()
    {
        // A call whose block only adds decals (`material inherited`) wears them over whatever dresses it, as the same
        // parts written out would: over the default when nothing does, and over the material of a shared shape it
        // stands in when that is dressed.  Walking out through the instances, a call adding only decals was passed
        // over as undressed, and its decals were lost.  (Held by a dressed group the call is handed a whole material
        // before any walk, so that is not the case to try.)
        const string decal =
            "decal { path { text { text 'R'  font 'Merriweather' } }  color Yellow  planar  min Y -1  max Y 1  " +
            "scale 0.6  translate [-0.2, 0, -0.3] }";
        string shared = Rendered($$"""
            primitive ball() -> group {
                return group { sphere { scale 0.42 } }
            }
            primitive held() -> group {
                return group { object ball() { material inherited { {{decal}} } } }
            }
            object ball() { material inherited { {{decal}} }  translate X -0.7 }
            object held() { material { pigment Red }  translate X 0.7 }
            """);
        string longhand = Rendered($$"""
            group { group { group { sphere { scale 0.42 } }  material inherited { {{decal}} } }  translate X -0.7 }
            group {
                group { group { group { sphere { scale 0.42 } }  material inherited { {{decal}} } } }
                material { pigment Red }
                translate X 0.7
            }
            """);

        Assert.AreEqual(longhand, shared, "decals on a shared call did not lie over what dresses it");
    }

    [TestMethod]
    public void TestASharedShapeDressedInGlassByItsHolderCastsItsCaustic()
    {
        // Photons are aimed at what turns light, found by walking every place a shared shape stands -- but only into
        // shapes that could hold any, as a wood holding no glass is billions of places to walk for nothing.  A part
        // left undressed may be dressed in glass by its holder, so it must count as one that could.
        string glass = "material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1  interior { ior 1.5 } }";
        string shared = Rendered($$"""
            context { caustics photons 20000 }
            primitive ball() -> group {
                return group { sphere { scale 0.42 } }
            }
            plane { translate Y -0.6  material { pigment White } }
            object ball() { {{glass}} }
            """);
        string longhand = Rendered($$"""
            context { caustics photons 20000 }
            plane { translate Y -0.6  material { pigment White } }
            group { group { sphere { scale 0.42 } }  {{glass}} }
            """);

        Assert.AreEqual(longhand, shared, "a shared shape dressed in glass by its holder cast no caustic");
    }

    /// <summary>
    /// Renders a scene written in the language and hands back the statistics reported for it.
    /// </summary>
    /// <param name="body">The scene to render.</param>
    /// <returns>What the render reported.</returns>
    private string StatisticsOf(string body)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");

        File.WriteAllText(path,
            "context { width 16  height 12 }\n" +
            "camera { location [0, 0.6, -4]  look at [0, 0, 0] }\n" +
            "point light { location [-4, 6, -5] }\n" + body);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            Assert.IsNotNull(renderer, "the scene did not parse");

            renderer.Render(new RenderOptions
            {
                OutputFileName = Path.ChangeExtension(path, ".png"), ReportStatistics = true
            });
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }

    /// <summary>
    /// Renders a scene written in the language and hands back the picture as text, so that two of
    /// them can be compared exactly.
    /// </summary>
    /// <param name="body">The scene to render.</param>
    /// <returns>The picture, as a string of pixels.</returns>
    private string Rendered(string body)
    {
        Canvas picture = Picture(body);
        System.Text.StringBuilder pixels = new ();

        for (int y = 0; y < picture.Height; y++)
        for (int x = 0; x < picture.Width; x++)
            pixels.Append(picture.GetPixel(x, y)).Append(';');

        return pixels.ToString();
    }

    private Canvas Picture(string body)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");
        string output = Path.ChangeExtension(path, ".png");

        File.WriteAllText(path,
            "context { no gamma  width 90  height 70  scale ambient by 0.35 }\n" +
            "camera { location [0, 0.6, -4]  look at [0, 0, 0]  field of view 55 }\n" +
            "point light { location [-4, 6, -5] }\n" + body);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        Assert.IsNotNull(renderer, "the scene did not parse");

        renderer.Render(new RenderOptions { OutputFileName = output });

        return new ImageFile(output).Load()[0];
    }

    /// <summary>
    /// Builds a shape shared at several depths -- a sphere inside a group shared by two instances,
    /// that group inside another shared by two more, and so on -- or the same spheres written out, one
    /// for every way down through the instances.  The two must come out identical.
    /// </summary>
    /// <param name="shared">Whether to share the shapes rather than write them out.</param>
    /// <param name="depth">How many levels of sharing to stand the sphere inside.</param>
    /// <param name="dress">What to make the sphere of.</param>
    /// <returns>The surfaces to put in a scene.</returns>
    private static List<Surface> Nested(bool shared, int depth, Func<Material> dress)
    {
        // The sphere's own turn and squash, and at every level two placings, one of them sheared, and
        // a transform on the group itself -- so that every link of the chain has to compose the right
        // way round, and a normal carried out through the wrong one is lit wrongly.
        Matrix mine = Transforms.RotateAroundY(25) * Transforms.Scale(0.5, 0.35, 0.5);
        Matrix[][] placings =
        [
            [Transforms.Translate(-0.45, 0, 0.1), Transforms.Translate(0.5, 0.15, 0) * Transforms.Shear(0.4, 0, 0, 0.3, 0, 0)],
            [Transforms.Translate(0, -0.5, 0.3) * Transforms.Scale(0.8), Transforms.Translate(0.2, 0.55, -0.2) * Transforms.RotateAroundZ(35)],
            [Transforms.Translate(-0.6, 0, 0) * Transforms.Scale(0.7), Transforms.Translate(0.7, 0.1, 0.2) * Transforms.Shear(0, 0.3, 0.25, 0, 0, 0)]
        ];
        Matrix[] groups = [Transforms.RotateAroundX(10), Transforms.Scale(1.1, 0.9, 1), Transforms.RotateAroundY(-20)];

        // Level nought is the innermost: the sphere, placed twice in a group; each level after it places
        // the one before twice in a group of its own.
        if (!shared)
        {
            List<Matrix> ways = [Matrix.Identity];

            for (int level = 0; level < depth; level++)
            {
                int at = level;

                ways = ways.SelectMany(way => placings[at].Select(placing => groups[at] * placing * way)).ToList();
            }

            return ways.Select(way => (Surface) new Sphere { Material = dress(), Transform = way * mine }).ToList();
        }

        Surface shape = new Sphere { Material = dress(), Transform = mine };

        for (int level = 0; level < depth; level++)
        {
            Group holder = new () { Transform = groups[level] };

            foreach (Matrix placing in placings[level])
                holder.Add(new Instance { Prototype = shape, Transform = placing });

            shape = holder;
        }

        return [shape];
    }

    [TestMethod]
    public void TestASharedShapeMayHoldAnotherSharedShape()
    {
        // **An oak holds its leaf clusters, and a wood holds its oaks.**  A crossing found through both
        // has two places to come out through, in order, and a walk that took only one would read the
        // checker in the wrong space and light the sheared ball by the wrong normal.  Two levels and
        // three, against the same balls written out: four of them, and eight.
        foreach (int depth in (int[]) [2, 3])
        {
            AssertSame(
                Rendered(scene => scene.Surfaces.AddRange(Nested(true, depth, Checkered))),
                Rendered(scene => scene.Surfaces.AddRange(Nested(false, depth, Checkered))),
                $"shapes shared {depth} deep did not look like the same shapes written out");
        }
    }

    [TestMethod]
    public void TestARayThroughNestedGlassTellsItsPlacesApart()
    {
        // **The same inner instance, in two outer places.**  The two balls here differ only by the outer
        // instance that places their group: the instance inside the group, and the ball inside that, are
        // the very same objects for both.  So which surfaces a ray is inside must be told by the whole
        // chain of instances -- told by the inner one alone, entering the second ball reads as leaving
        // the first.  They overlap, so a ray is inside both at once.
        Canvas Balls(bool shared) => Rendered(scene =>
        {
            Sphere shape = new () { Material = Glass(), Transform = Transforms.Scale(0.55) };
            Group pair = new Group().Add(new Instance { Prototype = shape, Transform = Transforms.Translate(0, 0.05, 0) });

            foreach (double z in new[] { -0.3, 0.3 })
            {
                Matrix placing = Transforms.Translate(0, 0, z);

                scene.Surfaces.Add(shared
                    ? new Instance { Prototype = pair, Transform = placing }
                    : new Sphere
                    {
                        Material = Glass(),
                        Transform = placing * Transforms.Translate(0, 0.05, 0) * Transforms.Scale(0.55)
                    });
            }

            scene.Surfaces.Add(new Cube
            {
                Material = Checkered(), Transform = Transforms.Translate(0, 0, 4) * Transforms.Scale(3, 3, 0.1)
            });
        }, new Point(0, 0, -5));

        AssertSame(Balls(true), Balls(false),
            "two nested instances of one glass shape do not refract like two glass shapes; the inside of " +
            "a shape is being told by its inner instance alone");
    }

    [TestMethod]
    public void TestACombinationMayHoldNestedInstances()
    {
        // **Both sides share the same inner instance**, so only the outer one can say which side a
        // crossing came from -- the outermost stamped by the time the combination asks.  And a
        // combination may stand inside a shared shape too, when the instances it asks about are the
        // inner ones, since the outer have yet to stamp.
        Cube bite = new () { Material = Checkered() };
        Group held = new Group().Add(new Instance { Prototype = bite, Transform = Transforms.Translate(0.05, 0, 0) });
        CsgSurface inside = new ()
        {
            Operation = CsgOperation.Difference,
            Left = new Group().Add(new Instance { Prototype = bite, Transform = Transforms.Scale(0.4) }),
            Right = new Group().Add(new Instance
            {
                Prototype = bite, Transform = Transforms.Translate(0.25, 0.25, -0.3) * Transforms.Scale(0.3)
            })
        };

        Canvas shared = Rendered(scene =>
        {
            scene.Surfaces.Add(new CsgSurface
            {
                Operation = CsgOperation.Difference,
                Left = new Group().Add(new Instance { Prototype = held, Transform = Transforms.Translate(-0.5, 0, 0) * Transforms.Scale(0.6) }),
                Right = new Group().Add(new Instance
                {
                    Prototype = held, Transform = Transforms.Translate(-0.2, 0.3, -0.4) * Transforms.Scale(0.4)
                })
            });

            foreach (double y in new[] { -0.7, 0.6 })
                scene.Surfaces.Add(new Instance { Prototype = inside, Transform = Transforms.Translate(0.9, y, 0) });
        });
        Canvas direct = Rendered(scene =>
        {
            scene.Surfaces.Add(new CsgSurface
            {
                Operation = CsgOperation.Difference,
                Left = new Group().Add(new Cube
                {
                    Material = Checkered(),
                    Transform = Transforms.Translate(-0.5, 0, 0) * Transforms.Scale(0.6) * Transforms.Translate(0.05, 0, 0)
                }),
                Right = new Group().Add(new Cube
                {
                    Material = Checkered(),
                    Transform = Transforms.Translate(-0.2, 0.3, -0.4) * Transforms.Scale(0.4) * Transforms.Translate(0.05, 0, 0)
                })
            });

            foreach (double y in new[] { -0.7, 0.6 })
            {
                scene.Surfaces.Add(new CsgSurface
                {
                    Operation = CsgOperation.Difference,
                    Transform = Transforms.Translate(0.9, y, 0),
                    Left = new Group().Add(new Cube { Material = Checkered(), Transform = Transforms.Scale(0.4) }),
                    Right = new Group().Add(new Cube
                    {
                        Material = Checkered(),
                        Transform = Transforms.Translate(0.25, 0.25, -0.3) * Transforms.Scale(0.3)
                    })
                });
            }
        });

        AssertSame(shared, direct,
            "a combination of nested instances did not cut as the same shapes written out do; it cannot " +
            "tell which of its sides a crossing came from");
    }

    [TestMethod]
    public void TestAnInstanceThatCastsNoShadowDoesSoForWhatItHolds()
    {
        // `no shadow` on an outer instance covers everything found through it, however deep.
        Canvas Shadowed(bool shared) => Rendered(scene =>
        {
            scene.Surfaces.Add(new Plane { Material = Checkered(), Transform = Transforms.Translate(0, -0.9, 0) });

            if (shared)
            {
                Sphere ball = new () { Material = Checkered(), Transform = Transforms.Scale(0.5) };
                Group held = new Group().Add(new Instance { Prototype = ball });

                scene.Surfaces.Add(new Instance { Prototype = held, NoShadow = true });
            }
            else
                scene.Surfaces.Add(new Sphere { Material = Checkered(), Transform = Transforms.Scale(0.5), NoShadow = true });
        });

        AssertSame(Shadowed(true), Shadowed(false),
            "a shape found through an outer instance that casts no shadow cast one anyway");
    }

    [TestMethod]
    public void TestAPointIsCarriedBothWaysThroughNestedInstances()
    {
        // Out to the world and back in again, through two instances and the groups between: what lights
        // made of a shape's insides and hair lit from the world both carry points this way.
        Sphere ball = new () { Transform = Transforms.Scale(0.5, 0.4, 0.6) };
        Instance inner = new () { Prototype = ball, Transform = Transforms.Translate(0.3, 0, 0) * Transforms.RotateAroundZ(20) };
        Group middle = new () { Transform = Transforms.RotateAroundY(30) };
        Instance outer = new () { Prototype = middle, Transform = Transforms.Translate(0, 1, 0) * Transforms.Shear(0.3, 0, 0, 0, 0.2, 0) };
        Group top = new () { Transform = Transforms.Translate(-1, 0, 2) };

        middle.Add(inner);
        top.Add(outer);

        Portals portals = new Portals(inner).And(outer);
        Point there = new (0.4, 0.2, 0.1);
        Matrix whole = top.Transform * outer.Transform * middle.Transform * inner.Transform * ball.Transform;
        Point world = ball.SurfaceToWorld(there, 0, portals);

        Assert.IsTrue((whole * there).Matches(world), "a point was carried out to the world through the wrong chain");
        Assert.IsTrue(ball.WorldToSurface(world, 0, portals).Matches(there), "a point was carried back in through the wrong chain");

        Footprint patch = ball.WorldToSurface(new Footprint(new Vector(0.01, 0, 0), new Vector(0, 0.01, 0)), 0, portals);
        Footprint expected = new Footprint(new Vector(0.01, 0, 0), new Vector(0, 0.01, 0)).TransformedBy(whole.Invert());

        Assert.IsTrue(patch.Across.Matches(expected.Across) && patch.Along.Matches(expected.Along),
            "a footprint was carried in through the wrong chain");
    }

    [TestMethod]
    public void TestAMaterialsAnchorIsReachedThroughOnlyTheInstancesBeyondIt()
    {
        // A material anchored on a surface inside the innermost shared shape is read through every
        // instance; one anchored on the shape that holds the inner instance, through the outer alone;
        // one anchored above them all, through none.
        Sphere ball = new () { Transform = Transforms.Scale(0.5) };
        Instance inner = new () { Prototype = ball, Transform = Transforms.Translate(0.3, 0, 0) };
        Group middle = new () { Transform = Transforms.RotateAroundY(30) };
        Instance outer = new () { Prototype = middle, Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(2) };
        Group top = new () { Transform = Transforms.Translate(-1, 0, 0) };

        middle.Add(inner);
        top.Add(outer);

        Portals portals = new Portals(inner).And(outer);
        Point there = new (0.4, 0.2, 0.1);
        Point world = top.Transform * outer.Transform * middle.Transform * inner.Transform * ball.Transform * there;

        Assert.IsTrue(new Material { Anchor = ball }.WorldToAnchor(ball, world, portals).Matches(there),
            "an anchor inside the innermost shared shape was not reached through every instance");
        Assert.IsTrue(new Material { Anchor = middle }.WorldToAnchor(ball, world, portals)
                .Matches(inner.Transform * ball.Transform * there),
            "an anchor holding the inner instance was not reached through the outer one alone");
        Assert.IsTrue(new Material { Anchor = top }.WorldToAnchor(ball, world, portals)
                .Matches(outer.Transform * middle.Transform * inner.Transform * ball.Transform * there),
            "an anchor above every instance was reached through one");
    }

    private static void AssertSame(Canvas left, Canvas right, string what)
    {
        Assert.AreEqual(left.Width, right.Width, what);
        Assert.AreEqual(left.Height, right.Height, what);

        for (int y = 0; y < left.Height; y++)
        {
            for (int x = 0; x < left.Width; x++)
            {
                Color one = left.GetPixel(x, y);
                Color other = right.GetPixel(x, y);

                Assert.AreEqual(one.Red, other.Red, 1e-9, $"{what} (they differ at {x}, {y})");
                Assert.AreEqual(one.Green, other.Green, 1e-9, $"{what} (they differ at {x}, {y})");
                Assert.AreEqual(one.Blue, other.Blue, 1e-9, $"{what} (they differ at {x}, {y})");
            }
        }
    }

    /// <summary>
    /// The rule that decides whether one shape may stand in many places.  It has two callers now --
    /// a primitive's own sharing and a field's -- so it is worth asking in its own terms rather than
    /// only through what those two do with the answer.
    /// <para>
    /// **Every shape here is one that has not been prepared**, because that is when both callers
    /// ask: they have just built the thing and are deciding what to do with it.  That is why motion
    /// is looked for at <c>MotionAt</c> rather than only at <c>Moves</c>, which is not settled until
    /// a surface is readied -- asking <c>Moves</c> alone was a guard that could never fire.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestWhatMayNotBeShared()
    {
        Assert.IsTrue(Instance.MayBeShared(new Sphere()),
            "An ordinary shape may stand in many places.");
        Assert.IsTrue(Instance.MayBeShared(new Group().Add(new Sphere())),
            "So may a group of ordinary shapes.");

        Assert.IsFalse(Instance.MayBeShared(null),
            "Nothing at all cannot be shared.");
        Assert.IsTrue(Instance.MayBeShared(new Instance { Prototype = new Sphere() }),
            "An instance may stand in a shared shape; a crossing remembers every instance it came through.");
        Assert.IsTrue(Instance.MayBeShared(
                new Group().Add(new Instance { Prototype = new Sphere() })),
            "So may a group holding one, which is exactly what a group primitive gives back.");
        Assert.IsFalse(Instance.MayBeShared(new Sphere { GivesLightSamples = 4 }),
            "A shape that is a light has to be somewhere.");
        Assert.IsFalse(Instance.MayBeShared(
                new Sphere { MotionAt = _ => Transforms.Translate(1, 0, 0) }),
            "A shape that moves stands somewhere different at each instant.");
        Assert.IsFalse(Instance.MayBeShared(new Sphere
            {
                Material = new Material { Interior = new Interior { Medium = new Medium() } }
            }),
            "The stuff inside a shape is found by carrying a point into what contains it.");
    }
}
