using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover putting one surface where another one is, rather than where a number says.
/// <para>
/// Every one of them compares a placed scene against the same scene with the position worked out by
/// hand and written as a translation.  That is deliberate: a placement either lands exactly where the
/// arithmetic says or it does not, and the two pictures are then either identical or they are not.
/// Measuring a picture for "about right" would pass a placement that is a whisker out -- and a whisker
/// is precisely how the first version of this was wrong.
/// </para>
/// </summary>
[TestClass]
public class TestPlacement
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"placement-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// The thing being placed is a cube of half-size one standing at the origin, so the box it
    /// occupies runs from -1 to 1 in every direction and every answer below is a whole number.
    /// </summary>
    private const string Anchor =
        "cube { named 'anchor'  material { pigment [0.6, 0.4, 0.25] } }\n";

    [TestMethod]
    public void TestEveryRelationPutsAThingWhereItSays()
    {
        // Each relation settles one direction by contact and centers the other two on what it named.
        // The probe is a cube of half-size a quarter, so a relation that touches puts its middle
        // 1.25 from the origin along that direction, and nought along the others.
        (string Relation, string Where)[] cases =
        [
            ("on 'anchor'", "[0, 1.25, 0]"),
            ("under 'anchor'", "[0, -1.25, 0]"),
            ("left of 'anchor'", "[-1.25, 0, 0]"),
            ("right of 'anchor'", "[1.25, 0, 0]"),
            ("front of 'anchor'", "[0, 0, -1.25]"),
            ("behind 'anchor'", "[0, 0, 1.25]")
        ];

        foreach ((string relation, string where) in cases)
        {
            Canvas placed = Rendered(Anchor + $"cube {{ scale 0.25  {relation}  {Probe} }}\n");
            Canvas byHand = Rendered(Anchor + $"cube {{ scale 0.25  translate {where}  {Probe} }}\n");

            AssertSame(placed, byHand, $"a thing placed {relation} did not land at {where}");
        }
    }

    [TestMethod]
    public void TestEverySideLinesUpWithTheMatchingSide()
    {
        // An alignment matches an edge to the *matching* edge rather than to the opposite one, which
        // is the whole of what makes it a different thing from a contact.  Lined up left with the
        // anchor, the probe's own left edge lands on the anchor's -- so its middle sits a quarter
        // inside it, not a quarter outside.
        //
        // **The probe is a slab rather than a cube, and that is not decoration.**  A lone alignment
        // centers the other two directions, so a small probe ends up buried inside the anchor or
        // hidden behind it, and the picture is the same whatever the arithmetic did.  The first
        // version of this test was exactly that, and it passed against an align-left wired to the
        // wrong edge.  Made long across the two directions it does not claim, the probe stands proud
        // of the anchor on all four sides and any error in the one direction it does claim shows.
        (string Relation, string Scale, string Where)[] cases =
        [
            ("align left with 'anchor'", "[0.25, 1.4, 1.4]", "[-0.75, 0, 0]"),
            ("align right with 'anchor'", "[0.25, 1.4, 1.4]", "[0.75, 0, 0]"),
            ("align top with 'anchor'", "[1.4, 0.25, 1.4]", "[0, 0.75, 0]"),
            ("align bottom with 'anchor'", "[1.4, 0.25, 1.4]", "[0, -0.75, 0]"),
            ("align front with 'anchor'", "[1.4, 1.4, 0.25]", "[0, 0, -0.75]"),
            ("align back with 'anchor'", "[1.4, 1.4, 0.25]", "[0, 0, 0.75]")
        ];

        foreach ((string relation, string scale, string where) in cases)
        {
            Canvas placed = Rendered(Anchor + $"cube {{ scale {scale}  {relation}  {Probe} }}\n");
            Canvas byHand = Rendered(
                Anchor + $"cube {{ scale {scale}  translate {where}  {Probe} }}\n");

            AssertSame(placed, byHand, $"a thing {relation} did not land at {where}");
        }
    }

    [TestMethod]
    public void TestAnAlignmentAndAContactComposeIntoATableLeg()
    {
        // **The case that asked for alignment in the first place.**  A leg goes under the table top
        // and lined up with its end -- and no arrangement of the contact relations will say that,
        // because `left of` means *beside*, which puts the leg off the edge and in mid-air.  One
        // relation says which side of the top it is on; the other says lined up how.
        Canvas placed = Rendered(
            "cube { scale [1.5, 0.1, 1.0]  translate Y 1  named 'top' }\n" +
            "cube { scale [0.1, 0.45, 0.1]  under 'top'  align left with 'top'  " + Probe + " }\n");

        // The top's underside is at 0.9 and its left edge at -1.5, so the leg's middle goes at
        // -1.4 across and 0.45 down from there.
        Canvas byHand = Rendered(
            "cube { scale [1.5, 0.1, 1.0]  translate Y 1 }\n" +
            "cube { scale [0.1, 0.45, 0.1]  translate [-1.4, 0.45, 0]  " + Probe + " }\n");

        AssertSame(placed, byHand, "a leg lined up under the end of a table did not land there");
    }

    [TestMethod]
    public void TestAnAlignmentAndAContactCannotClaimOneDirection()
    {
        // Lining a thing up left with something *and* putting it beside that same thing are two
        // answers to one question, so this is the same contradiction as two contacts.
        StringAssert.Contains(
            WhatWentWrong(Anchor + "sphere { left of 'anchor'  align left with 'anchor' }\n"),
            "along the same axis");
    }

    [TestMethod]
    public void TestADistanceMeansAGapAfterAContactAndAnInsetAfterAnAlignment()
    {
        // **One word, two senses, and both are the one a person means.**  After a contact, `by` opens
        // a *gap*: a shelf slung `under 'top' by 0.3` hangs clear of the top rather than being buried
        // in it, so the distance goes away from the thing you are against.  After an alignment it is
        // an *inset*: a leg `align left with 'top' by 0.1` stands in from the corner rather than out
        // past it, so the distance goes toward the middle of the thing you lined up with.
        //
        // Written as one rule -- "always along the positive axis" -- half of these would go the wrong
        // way, and `under ... by` would drive the shelf up into the table.
        (string Relation, string Where)[] cases =
        [
            ("on 'anchor' by 0.5", "[0, 1.75, 0]"),
            ("under 'anchor' by 0.5", "[0, -1.75, 0]"),
            ("left of 'anchor' by 0.5", "[-1.75, 0, 0]"),
            ("right of 'anchor' by 0.5", "[1.75, 0, 0]"),
            ("front of 'anchor' by 0.5", "[0, 0, -1.75]"),
            ("behind 'anchor' by 0.5", "[0, 0, 1.75]"),
            ("align left with 'anchor' by 0.3", "[-0.45, 0, 0]"),
            ("align right with 'anchor' by 0.3", "[0.45, 0, 0]"),
            ("align top with 'anchor' by 0.3", "[0, 0.45, 0]"),
            ("align bottom with 'anchor' by 0.3", "[0, -0.45, 0]"),
            ("align front with 'anchor' by 0.3", "[0, 0, -0.45]"),
            ("align back with 'anchor' by 0.3", "[0, 0, 0.45]")
        ];

        foreach ((string relation, string where) in cases)
        {
            Canvas placed = Rendered(Anchor + $"cube {{ scale 0.25  {relation}  {Probe} }}\n");
            Canvas byHand = Rendered(Anchor + $"cube {{ scale 0.25  translate {where}  {Probe} }}\n");

            AssertSame(placed, byHand, $"a thing placed {relation} did not land at {where}");
        }
    }

    [TestMethod]
    public void TestWhatTheOtherDirectionsAreCenteredOnCanBeChosen()
    {
        // A relation settles one direction and the rest are centered on whatever was named first,
        // which is right nearly always and impossible to override -- so a shelf could be lined up
        // vertically with the legs and there was no way to say it should sit over the middle of the
        // *table*.  `centered on` settles no direction of its own; it only says which thing the rest
        // are measured from.
        const string Two =
            "cube { named 'anchor'  material { pigment [0.6, 0.4, 0.25] } }\n" +
            "cube { scale 0.5  translate [3, 0, 2]  named 'other' }\n";

        (string Relation, string Where)[] cases =
        [
            ("on 'anchor'", "[0, 1.25, 0]"),
            ("on 'anchor'  centered on 'other'", "[3, 1.25, 2]"),
            ("centered on 'other'", "[3, 0, 2]")
        ];

        foreach ((string relation, string where) in cases)
        {
            Canvas placed = Rendered(Two + $"cube {{ scale 0.25  {relation}  {Probe} }}\n", Wider);
            Canvas byHand = Rendered(
                Two + $"cube {{ scale 0.25  translate {where}  {Probe} }}\n", Wider);

            AssertSame(placed, byHand, $"a thing placed {relation} did not land at {where}");
        }
    }

    [TestMethod]
    public void TestAThingCanOnlyBeCenteredOnOneThing()
    {
        StringAssert.Contains(
            WhatWentWrong(
                Anchor + "cube { scale 0.5  translate X 3  named 'other' }\n" +
                "sphere { centered on 'anchor'  centered on 'other' }\n"),
            "there can only be one thing it is middled on");
    }

    [TestMethod]
    public void TestBeingCenteredOnSomethingTakesNoDistance()
    {
        // Every other relation points somewhere, so a distance past it means something.  This one
        // does not, and taking the number quietly would be worse than refusing it.
        StringAssert.Contains(
            WhatWentWrong(Anchor + "sphere { centered on 'anchor' by 0.5 }\n"),
            "there is no direction for it to go in");
    }

    [TestMethod]
    public void TestAChainSettlesWhateverOrderItIsWrittenIn()
    {
        // **A chain is the ordinary case and the order it is written in must not matter.**  Here the
        // ball is placed against a table that has not yet been put on the rug, so settling these as
        // they are written would measure the table where it was first drawn rather than where it ends
        // up -- and nothing would fail.  The ball would simply hang in the air, which is the kind of
        // wrong that is easy to look at and not see.
        Canvas placed = Rendered(
            "sphere { scale 0.4  on 'table'  material { pigment [0.2, 0.5, 0.8] } }\n" +
            "cube { scale [1.2, 0.5, 0.9]  on 'rug'  named 'table' }\n" +
            "cube { scale [2.4, 0.1, 1.8]  translate Y 0.1  named 'rug' }\n");

        // The rug's top is at 0.2, so the table's middle is at 0.7 and its top at 1.2.
        Canvas byHand = Rendered(
            "sphere { scale 0.4  translate Y 1.6  material { pigment [0.2, 0.5, 0.8] } }\n" +
            "cube { scale [1.2, 0.5, 0.9]  translate Y 0.7 }\n" +
            "cube { scale [2.4, 0.1, 1.8]  translate Y 0.1 }\n");

        AssertSame(placed, byHand, "a chain of placements did not settle in dependency order");
    }

    [TestMethod]
    public void TestASecondRelationTakesOverItsOwnDirection()
    {
        // Two relations, each settling its own direction, and the one direction neither of them
        // claimed centered on the first thing named.
        Canvas placed = Rendered(
            Anchor +
            "sphere { scale 0.3  on 'anchor'  named 'lamp' }\n" +
            "cube { scale 0.25  on 'anchor'  right of 'lamp'  " + Probe + " }\n");
        Canvas byHand = Rendered(
            Anchor +
            "sphere { scale 0.3  translate [0, 1.3, 0]  named 'lamp' }\n" +
            "cube { scale 0.25  translate [0.55, 1.25, 0]  " + Probe + " }\n");

        AssertSame(placed, byHand, "a second relation did not take over its own direction");
    }

    [TestMethod]
    public void TestAPlacementIsMadeInTheGroupsOwnSpace()
    {
        // A placement is between things in one group, and the whole reason for saying so is this: the
        // answer has to go on meaning the same thing after the group is turned and moved.  If it were
        // worked out in the world instead, this pair would come apart the moment the group moved.
        const string Moved = "  rotate Y 35  translate [0.4, 0, 0.6]\n}\n";

        Canvas placed = Rendered(
            "group {\n" + Anchor + "  sphere { scale 0.4  on 'anchor'  " + Probe + " }\n" + Moved);
        Canvas byHand = Rendered(
            "group {\n" + Anchor + "  sphere { scale 0.4  translate Y 1.4  " + Probe + " }\n" + Moved);

        AssertSame(placed, byHand, "a placement did not survive its group being turned and moved");
    }

    [TestMethod]
    public void TestAPlacementTouchesAtAnyScale()
    {
        // **This is the test that catches the fault the first version shipped with, and it is worth
        // saying exactly what that was.**  Every bounding box is grown by a whisker once it is worked
        // out, so that a ray grazing a surface is not turned away by arithmetic.  Placement read those
        // padded boxes, so two things told to touch did not quite -- by a ten-thousandth, which no eye
        // will ever find at the scale the first tests used.
        //
        // But the padding goes on in the surface's own space and is then carried through its
        // transform.  Scale a thing by ten thousand and its whisker is scaled too: the gap becomes a
        // whole unit.  So this stands a ball of radius one on a cube scaled by ten thousand and looks
        // closely at the join, where being a unit out is being a whole ball out.
        //
        // The general form of this trap is recorded in the absolute-epsilon work: a tolerance that is
        // right at one scale is wrong at another, and the only safe answer is to take the padding off
        // in the space it went on in.
        Canvas placed = Rendered(
            "cube { scale 10000  named 'ground' }\n" +
            "sphere { on 'ground'  " + Probe + " }\n",
            HighUp);
        Canvas byHand = Rendered(
            "cube { scale 10000  named 'ground' }\n" +
            "sphere { translate Y 10001  " + Probe + " }\n",
            HighUp);

        AssertSame(placed, byHand,
            "a placement on a hugely scaled surface did not touch it; the box padding is leaking " +
            "into the measurement, and it is scaled by the surface's own transform");
    }

    [TestMethod]
    public void TestAPlacementAgainstSomethingBuiltOfOthersTouchesAtAnyScale()
    {
        // **A second helping of the same fault, and it survived the first fix.**  Taking the padding
        // back off a surface's own box does nothing for a group or a CSG, because their boxes are
        // gathered from children that were *already* padded -- and each child's whisker has been
        // stretched by whatever transform carried it up. Unpadding the outside leaves all of that
        // inside.
        //
        // So both of these stand a ball on something ten thousand times life size and look at the
        // join, where the leak is a whole unit rather than a ten-thousandth.
        foreach (string ground in new[]
                 {
                     "group { cube { scale 10000 }  named 'ground' }",
                     "union { cube { scale 10000 }  cube { scale 9000 }  named 'ground' }"
                 })
        {
            Canvas placed = Rendered(
                ground + "\nsphere { on 'ground'  " + Probe + " }\n", HighUp);
            Canvas byHand = Rendered(
                ground + "\nsphere { translate Y 10001  " + Probe + " }\n", HighUp);

            AssertSame(placed, byHand,
                "a placement against something built out of other surfaces did not touch it; the " +
                "padding on its children is leaking through the box it gathers from them");
        }
    }

    [TestMethod]
    public void TestAPlacementNamingNothingSaysWhatThereIs()
    {
        StringAssert.Contains(
            WhatWentWrong(Anchor + "sphere { on 'ancho' }\n"),
            "the names here are 'anchor'");
    }

    [TestMethod]
    public void TestPlacementsRunningInACircleAreCalledOut()
    {
        // Nothing fails on its own here -- each one is simply waiting for the next -- so this has to
        // be looked for rather than fallen over, and the message names the ring.
        StringAssert.Contains(
            WhatWentWrong(
                "cube { on 'b'  named 'a' }\n" +
                "cube { on 'a'  named 'b' }\n"),
            "run in a circle");
    }

    [TestMethod]
    public void TestTwoThingsOfOneNameAreRefused()
    {
        StringAssert.Contains(
            WhatWentWrong(
                "cube { named 'a' }\n" +
                "cube { translate X 3  named 'a' }\n" +
                "sphere { on 'a' }\n"),
            "cannot say which was meant");
    }

    [TestMethod]
    public void TestOneDirectionMayOnlyBeToldOneThing()
    {
        // Two relations for the same direction is a contradiction rather than a race, and settling it
        // by letting the last one win would leave the first silently doing nothing.
        StringAssert.Contains(
            WhatWentWrong(
                Anchor +
                "cube { translate X 4  named 'other' }\n" +
                "sphere { on 'anchor'  under 'other' }\n"),
            "along the same axis");
    }

    [TestMethod]
    public void TestSomethingWithNoBoundsCannotBePlacedAgainst()
    {
        // A plane goes on forever, so there is no region to put anything against -- and guessing at
        // one would put the thing somewhere arbitrary and say nothing about it.
        StringAssert.Contains(
            WhatWentWrong("plane { named 'floor' }\nsphere { on 'floor' }\n"),
            "occupies no region");
    }

    /// <summary>
    /// This tests that something with nothing in it is simply not placed, rather than refused.
    /// <para>
    /// **An empty box is not the same as no box at all.**  Every library in this project writes a
    /// snow cap as a primitive that gives back an empty group out of season, so if placing one were
    /// refused, `on` could only ever be used for things that are there all year -- which is most of
    /// what a scene wants to place.  A plane, which has *no* box rather than an empty one, is a real
    /// mistake and stays refused; the test below says so.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestAnEmptyThingIsSimplyNotPlaced()
    {
        const string Body = """
                            primitive Cap(season) -> group {
                                switch (season) {
                                    case 'winter' { return group { sphere { scale 0.3 } } }
                                    default { return group { } }
                                }
                            }
                            group {
                                cube { scale 0.5  material { pigment [0.8, 0.5, 0.3] }  named 'post' }
                                object Cap('summer') { on 'post' }
                            }

                            """;

        // It renders at all, which is the whole point -- and it draws the same picture as the scene
        // without the empty thing in it, so nothing was moved on its account either.
        Canvas withEmpty = Rendered(Body);
        Canvas without = Rendered("""
                                  group {
                                      cube { scale 0.5  material { pigment [0.8, 0.5, 0.3] }  named 'post' }
                                  }

                                  """);

        Assert.IsNotNull(withEmpty, "an empty thing being placed should not stop the render");

        for (int y = 0; y < withEmpty.Height; y++)
        {
            for (int x = 0; x < withEmpty.Width; x++)
            {
                Assert.IsTrue(withEmpty.GetPixel(x, y).Matches(without.GetPixel(x, y)),
                    $"placing nothing changed the picture at ({x}, {y})");
            }
        }
    }

    /// <summary>
    /// This tests that something endless is still refused when it is the thing being *placed*, which
    /// is the other side of the test above: an empty thing is nothing to place, but a plane is a
    /// region nobody can put anywhere, and quietly doing nothing with it would hide a real mistake.
    /// </summary>
    [TestMethod]
    public void TestSomethingWithNoBoundsCannotItselfBePlaced()
    {
        StringAssert.Contains(
            WhatWentWrong("cube { scale 0.5  named 'post' }\nplane { on 'post' }\n"),
            "cannot be placed this way");
    }

    /// <summary>
    /// A material that shows a probe clearly against the anchor it is placed against.
    /// </summary>
    private const string Probe = "material { pigment [0.2, 0.5, 0.8] }";

    /// <summary>
    /// A camera far enough back to hold two anchors standing well apart.
    /// </summary>
    private const string Wider =
        "camera { location [7, 5, -8]  look at [1.5, 0.5, 1]  field of view 55 }\n";

    /// <summary>
    /// A camera up at the top of something ten thousand units across, close enough to the join that
    /// a gap of one unit is a gap of most of a ball.
    /// </summary>
    private const string HighUp =
        "camera { location [0, 10004, -7]  look at [0, 10000.6, 0]  field of view 45 }\n";

    /// <summary>
    /// Renders a scene body and hands back the picture.
    /// </summary>
    private Canvas Rendered(string body, string camera = null)
    {
        string path = Path.Combine(_directory, $"scene-{Guid.NewGuid():N}.igl");
        string output = Path.ChangeExtension(path, ".png");

        File.WriteAllText(path,
            "context { no gamma  width 120  height 90 }\n" +
            (camera ?? "camera { location [4, 3, -6]  look at [0, 0, 0]  field of view 55 }\n") +
            "point light { location [-5, 8, -6] }\n" + body);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        Assert.IsNotNull(renderer, "the scene did not parse");

        renderer.Render(new RenderOptions { OutputFileName = output });

        return new ImageFile(output).Load()[0];
    }

    /// <summary>
    /// Renders a scene that should not render, and hands back what was said about it.
    /// </summary>
    private string WhatWentWrong(string body)
    {
        string path = Path.Combine(_directory, $"bad-{Guid.NewGuid():N}.igl");

        File.WriteAllText(path,
            "camera { location [4, 3, -6]  look at [0, 0, 0] }\n" +
            "point light { location [-5, 8, -6] }\n" + body);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            renderer?.Render(new RenderOptions
            {
                OutputFileName = Path.ChangeExtension(path, ".png")
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

        return captured.ToString();
    }

    private static void AssertSame(Canvas left, Canvas right, string what)
    {
        Assert.AreEqual(left.Width, right.Width, what);
        Assert.AreEqual(left.Height, right.Height, what);

        for (int y = 0; y < left.Height; y++)
        {
            for (int x = 0; x < left.Width; x++)
            {
                Assert.AreEqual(left.GetPixel(x, y).Red, right.GetPixel(x, y).Red, 1e-9,
                    $"{what} (they differ at {x}, {y})");
            }
        }
    }
}
