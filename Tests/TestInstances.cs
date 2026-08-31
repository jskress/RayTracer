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

    /// <summary>
    /// Renders a scene written in the language and hands back the picture as text, so that two of
    /// them can be compared exactly.
    /// </summary>
    /// <param name="body">The scene to render.</param>
    /// <returns>The picture, as a string of pixels.</returns>
    private string Rendered(string body)
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

        Canvas picture = new ImageFile(output).Load()[0];
        System.Text.StringBuilder pixels = new ();

        for (int y = 0; y < picture.Height; y++)
        for (int x = 0; x < picture.Width; x++)
            pixels.Append(picture.GetPixel(x, y)).Append(';');

        return pixels.ToString();
    }

    [TestMethod]
    public void TestASharedShapeCannotHoldAnotherSharedShape()
    {
        // Refused rather than quietly got wrong: a hit would have to remember both instances, in
        // order, and it remembers one.
        Sphere inner = new () { Material = Checkered() };
        Group holder = new ();

        holder.Add(new Instance { Prototype = inner, Transform = Transforms.Translate(1, 0, 0) });

        Instance outer = new () { Prototype = holder };

        Exception thrown = Assert.ThrowsExactly<Exception>(() => outer.PrepareForRendering(null));

        StringAssert.Contains(thrown.Message, "cannot itself hold a shared shape");
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
}
