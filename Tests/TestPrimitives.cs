using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the things a scene teaches itself to make.
/// <para>
/// A primitive is a function's twin, and nearly everything about it follows from the one way they
/// differ: a function gives back a value, which can be worked out wherever an expression stands,
/// while this gives back a <i>thing</i>, which in this renderer is never a value but a recipe run at
/// render time.  So the tests worth having are about what that difference costs and buys -- that a
/// call is read as the kind it was promised, that what one call adds belongs to that call, and that a
/// body sees where it was written.
/// </para>
/// </summary>
[TestClass]
public class TestPrimitives
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"primitive-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders the given scene and hands back the picture, or whatever stopped it.
    /// </summary>
    private (Canvas Image, string Error) Render(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path, "context { angles are degrees  no gamma }\n" + scene);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return (null, captured.ToString());

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = 40, Height = 30
            });

            return captured.ToString().Contains("Error")
                ? (null, captured.ToString())
                : (new ImageFile(output).Load()[0], null);
        }
        catch (Exception exception)
        {
            return (null, exception.ToString());
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    private const string Staging = """
        camera { location [0, 0, -14]  look at [0, 0, 0]  field of view 40 }
        point light { location [-4, 4, -6] }
        """;

    [TestMethod]
    public void TestASceneMayTeachItselfToMakeSomething()
    {
        // Values, a fallback, a working on the way, and three calls that differ only in what they were
        // told -- which is the whole of what this is for.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive post(height, thickness = 0.2) -> group {
                cap = thickness * 1.6
                return group {
                    cube { material { pigment Red }  scale [thickness, height, thickness] }
                    sphere { material { pigment Blue }  scale cap  translate Y height }
                }
            }
            object post(1.2) { translate X -2 }
            object post(1.8) { }
            object post(0.9, 0.35) { translate X 2 }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestWhatOneCallAddsBelongsToThatCallAlone()
    {
        // A call takes a copy of the recipe, so a block on one call cannot reach another.  If they
        // shared, all three of these would end up wherever the last block put them, and the picture
        // would show one post rather than three.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive post() -> group {
                return group { cube { material { pigment Red }  scale [0.3, 1, 0.3] } }
            }
            object post() { translate X -3 }
            object post() { }
            object post() { translate X 3 }
            """);

        Assert.IsNull(error, error);

        // Red in three separated columns, which one shared recipe could not produce.
        HashSet<int> columns = [];

        for (int x = 0; x < 40; x++)
        {
            for (int y = 0; y < 30; y++)
            {
                Color pixel = image.GetPixel(x, y);

                if (pixel.Red > 0.25 && pixel.Blue < 0.15)
                    columns.Add(x / 5);
            }
        }

        Assert.IsTrue(columns.Count >= 3,
            $"three calls should stand in three places, and covered {columns.Count} bands");
    }

    [TestMethod]
    public void TestACallsBlockTakesTheDeclaredKindsOwnClauses()
    {
        // The reason the kind is declared rather than merely "a surface".  `max Y` is a cylinder's own
        // clause, and a call is read long before anything is built -- so the parser can only know to
        // accept it because the primitive said what it gives back.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive rod(thickness) -> cylinder {
                return cylinder {
                    min Y 0  max Y 1
                    material { pigment Red }
                    scale [thickness, 1, thickness]
                }
            }
            object rod(0.3) { max Y 2.5  translate X -1 }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAPrimitiveMayHoldASmallerOneOfItsOwn()
    {
        // A fence knows how to make a post, and nobody else needs to.  The smaller one is bound to the
        // call's scope, so it sees what the fence was told.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive fence(count, spacing = 0.8) -> group {
                primitive post(lean) -> group {
                    return group {
                        cube { material { pigment Red }  scale [0.07, 0.7, 0.07]  rotate Z lean }
                    }
                }
                return group {
                    for step in [0, 4] {
                        object post(step * 1.5) { translate X step * spacing }
                    }
                }
            }
            object fence(5) { translate [-1.5, 0, 0] }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestASmallerPrimitiveIsNotVisibleOutside()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive fence() -> group {
                primitive post() -> group {
                    return group { cube { material { pigment Red } } }
                }
                return group { object post() }
            }
            object post()
            """);

        Assert.IsNull(image, "the smaller one should not be reachable from outside");
        Assert.IsTrue(error.Contains("post"), $"and should be named: {error}");
    }

    [TestMethod]
    public void TestACallsBlockSeesWhereTheCallWasWritten()
    {
        // The subtlest thing here, and the one that was wrong at first.  A call's block belongs to the
        // caller, so it is resolved among the caller's names -- which is what lets a loop place a row
        // of them by saying `translate X step`.  Folded into the primitive's body instead, as it was
        // to begin with, `step` was nowhere to be found and the whole shape of the feature was wrong.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive blob() -> group {
                return group { sphere { material { pigment Red }  scale 0.35 } }
            }
            group {
                for step in [0, 3] {
                    object blob() { translate X step * 1.4 - 2 }
                }
            }
            """);

        Assert.IsNull(error, error);

        // Four of them across the frame, which could not happen if the block could not see `step`.
        HashSet<int> bands = [];

        for (int x = 0; x < 40; x++)
        {
            for (int y = 0; y < 30; y++)
            {
                if (image.GetPixel(x, y).Red > 0.25 && image.GetPixel(x, y).Blue < 0.15)
                    bands.Add(x / 4);
            }
        }

        Assert.IsTrue(bands.Count >= 4, $"they should stand apart, and covered {bands.Count} bands");
    }

    [TestMethod]
    public void TestEveryKindOfSurfaceMayBeGivenBack()
    {
        // The boundary this once had was an accident rather than a rule -- eight kinds worked and the
        // rest did not, for no reason an author could have guessed.  These are three that used to be
        // out of reach, each with its own clauses both in the body and in the call's block.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive ring(thickness) -> torus {
                return torus { radii 0.7, thickness  material { pigment Red } }
            }
            primitive pill(squareness) -> superellipsoid {
                return superellipsoid { east squareness  north squareness  material { pigment Blue } }
            }
            primitive lens(size) -> disc {
                return disc { center [0, 0, 0]  normal [0, 1, 0]  radius size
                              material { pigment Green } }
            }
            object ring(0.22) { radii 0.9, 0.18  translate X -2.2 }
            object pill(0.4)
            object lens(0.8)  { inner radius 0.3  rotate X -80  translate X 2.2 }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestACallsBlockNeedNotRepeatWhatTheBodyAlreadySaid()
    {
        // A block is laid over something already made, so it holds only what that call wished to
        // change.  Asking it for the properties a whole one must have would refuse every block that
        // did not repeat them -- which is what happened at first, a superellipsoid's call being told
        // it had no east or north when its body plainly had.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive pill(squareness) -> superellipsoid {
                return superellipsoid { east squareness  north squareness  material { pigment Red } }
            }
            object pill(0.4) { translate X 1 }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAPrimitiveMayGiveBackAPigment()
    {
        // The other half of what a primitive is for.  A pigment is named through an expression rather
        // than a clause of its own, so a call of one arrives by a quite different road than a
        // surface's -- and has to come back as the makings of a pigment rather than a pigment, an
        // expression having no sight of the render's context to finish one with.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive banded(width, warm = 0.8) -> pigment {
                pale = [warm, warm * 0.9, warm * 0.7]
                return linear stripes { pale, [0.2, 0.22, 0.3]  scale width }
            }
            sphere { material { pigment banded(0.4) }  translate X -1.2 }
            sphere { material { pigment banded(0.15, 1.0) }  translate X 1.2 }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAPigmentPrimitiveCannotStandWhereANumberIsWanted()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive banded(width) -> pigment { return linear stripes { White, Black  scale width } }
            sphere { scale banded(0.5) }
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("banded"), $"the complaint should name it: {error}");
    }

    [TestMethod]
    public void TestANameHoldingATupleAnswersWhereAColorIsWanted()
    {
        // Not about primitives at all, but found through one and worth keeping.  A name was looked up
        // by the type asked for and reported as nothing when none matched -- so a tuple, which
        // converts to a color readily enough, could not be used as one.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            pale = [0.8, 0.3, 0.2]
            sphere { material { pigment pale } }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAPrimitiveMayGiveBackAMaterialOrAnInterior()
    {
        // Both are named the way a surface is -- the word, then the name -- so a call of either is
        // told apart by what follows: a parenthesis rather than a brace or nothing.  And both may be
        // adjusted where they stand, the block being laid over what the recipe made.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive glazed(hue, gloss = 0.5) -> material {
                return material { pigment hue  specular gloss  shininess 40 + gloss * 260 }
            }
            primitive misty(thickness) -> interior {
                return interior { ior 1.4  medium { absorption thickness } }
            }
            sphere { material glazed(Red)  translate X -1.5 }
            sphere { material glazed(Blue, 0.9) { shininess 5 } }
            sphere {
                material {
                    pigment White  transparency 1  diffuse 0.1
                    interior misty(0.4) { ior 1.9 }
                }
                translate X 1.5
            }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestAMediumMayBeNamedAndMadeToOrder()
    {
        // A medium could not be given a name at all before this, so there was nothing a call of one
        // could have looked like.  Both halves are checked here: one named and used, and one made to
        // order with a block laid over it.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            haze = medium { absorption [0.05, 0.06, 0.08] }
            primitive smoke(thickness) -> medium {
                return medium { scattering thickness  absorption thickness * 0.1 }
            }
            environment { ior Air  medium haze }
            sphere {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { medium smoke(1.2) { anisotropy 0.4 } }
                }
            }
            """);

        Assert.IsNull(error, error);
        Assert.IsNotNull(image);
    }

    [TestMethod]
    public void TestWhatAMediumMayBeDependsOnWhereItIsUsed()
    {
        // The check belongs to the use rather than to the medium: the surroundings have no far side,
        // so a medium that gives off light without swallowing any has no answer there, though it is
        // perfectly good inside something bounded.  A named medium therefore has to be checked where
        // it is used, not where it was written.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            glow = medium { emission [0.4, 0.3, 0.1] }
            environment { ior Air  medium glow }
            sphere { material { pigment Red } }
            """);

        Assert.IsNull(image, "an endless span of that has no answer");
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TestACallIsCheckedAgainstWhatItTakes()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive post(height, thickness = 0.2) -> group {
                return group { cube { scale [thickness, height, thickness] } }
            }
            object post()
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("post") && error.Contains("takes"),
            $"the complaint should say what it takes: {error}");
    }

    [TestMethod]
    public void TestCallingSomethingThatIsNotAPrimitiveIsRefused()
    {
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            object nosuchthing(2)
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("nosuchthing"), $"the complaint should name it: {error}");
    }

    [TestMethod]
    public void TestWhatComesBackMustBeTheKindThatWasPromised()
    {
        // Caught while reading rather than left to be discovered when a picture looks wrong.
        (Canvas image, string error) = Render($$"""
            {{Staging}}
            primitive post() -> group {
                return sphere { material { pigment Red } }
            }
            object post()
            """);

        Assert.IsNull(image);
        Assert.IsTrue(error.Contains("group"), $"the complaint should say what was promised: {error}");
    }

    /// <summary>
    /// A closer view than <see cref="Staging"/>, for the tests that compare a call against the same
    /// thing written out where it stands.  It has to show something small clearly.
    /// </summary>
    private const string CloseUp = """
        camera { location [0, 0.8, -3.0]  look at [0, 0.2, 0]  field of view 45 }
        point light { location [-5, 7, -6] }
        background [0.6, 0.7, 0.85]
        plane { material { pigment [0.4, 0.4, 0.35] } }
        Rock = material { pigment [0.55, 0.53, 0.5] }
        """;

    /// <summary>
    /// Reports whether two pictures differ anywhere worth noticing.
    /// </summary>
    private static bool Differs(Canvas first, Canvas second)
    {
        for (int x = 0; x < first.Width; x++)
        {
            for (int y = 0; y < first.Height; y++)
            {
                Color one = first.GetPixel(x, y);
                Color other = second.GetPixel(x, y);

                if (Math.Abs(one.Red - other.Red) + Math.Abs(one.Green - other.Green) +
                    Math.Abs(one.Blue - other.Blue) > 0.02)
                    return true;
            }
        }

        return false;
    }

    [TestMethod]
    public void TestWhatACallAddsGoesOutsideWhatTheBodyAlreadyDid()
    {
        // The body's transform is written in the primitive's own frame and the call's block is written
        // in the caller's, so the two compose, the call's outermost.  Assigning over it is what used to
        // happen, and it was invisible for as long as it was because every primitive written so far
        // hands back a *group* with no transform of its own, keeping its transforms on the things
        // inside.  The first primitive to scale the thing it gives back -- a stone sized to order --
        // had that size thrown away by any call that added a `translate` to place it.
        //
        // The check is an equivalence rather than a measurement: a call must come out as the same
        // picture as the thing it stands for, written out longhand.
        (Canvas made, string first) = Render($$"""
            {{CloseUp}}
            primitive Stone(size) -> sphere { return sphere { material Rock  scale size } }
            object Stone(0.25) { translate Y 0.25 }
            """);
        (Canvas written, string second) = Render($$"""
            {{CloseUp}}
            sphere { material Rock  scale 0.25  translate Y 0.25 }
            """);

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);
        Assert.IsFalse(Differs(made, written),
            "a call that places what it made should keep the size the body gave it");
    }

    [TestMethod]
    public void TestACallMayPlaceSomethingBuiltOutOfACombination()
    {
        // A primitive giving back a CSG used to throw outright the moment a call put a block after it,
        // and it threw in the one place a block is for -- a loop scattering a run of them, each needing
        // its own place.  The block names no children and carries no operation, so there was nothing
        // for either to do, and doing them anyway rebuilt the children from an empty list.
        (Canvas made, string first) = Render($$"""
            {{CloseUp}}
            primitive Chip(size) -> intersection {
                return intersection {
                    sphere { }
                    cube { scale 0.86  rotate Y 30 }
                    material Rock
                    scale size
                }
            }
            object Chip(0.25) { translate Y 0.25 }
            """);
        (Canvas written, string second) = Render($$"""
            {{CloseUp}}
            intersection {
                sphere { }
                cube { scale 0.86  rotate Y 30 }
                material Rock
                scale 0.25
                translate Y 0.25
            }
            """);

        Assert.IsNull(first, first);
        Assert.IsNull(second, second);
        Assert.IsFalse(Differs(made, written),
            "a combination placed by a call should be the same as one written out where it stands");
    }

}
