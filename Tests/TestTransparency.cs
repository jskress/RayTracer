using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests ask one question from several angles: does putting a perfectly clear surface in
/// front of something change how that something looks?  It should not.  Anything that darkens,
/// tints, or dims what is seen through clear glass is the renderer losing light it was handed.
/// </summary>
[TestClass]
public class TestTransparency
{
    /// <summary>
    /// A surface that is perfectly clear: no ambient, diffuse or specular of its own, nothing
    /// reflected, fully transmitting, and the same index of refraction as the air around it, so
    /// it does not even bend what passes through.  Looking through it should be the same as
    /// looking through nothing at all.
    /// </summary>
    private static Material PerfectlyClear(double indexOfRefraction = 1)
    {
        return new Material
        {
            Pigment = new SolidPigment(Colors.White),
            Ambient = 0,
            Diffuse = 0,
            Specular = 0,
            Reflective = 0,
            Transparency = 1,
            Interior = new Interior { IndexOfRefraction = indexOfRefraction }
        };
    }

    private static Scene SceneShowing(params Surface[] surfaces)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Red) };

        scene.Lights.Add(new PointLight { Location = new Point(0, 0, -10) });

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        return scene;
    }

    private static Color LookAlongZ(Scene scene, int remaining = 4)
    {
        return scene.GetColorFor(new Ray(new Point(0, 0, -5), new Vector(0, 0, 1)), remaining);
    }

    [TestMethod]
    public void TestAnEmptyViewSeesTheBackground()
    {
        // The reference the rest of these are measured against.
        Assert.IsTrue(Colors.Red.Matches(LookAlongZ(SceneShowing())));
    }

    [TestMethod]
    public void TestClearGlassDoesNotChangeWhatIsBehindIt()
    {
        // The heart of it: one perfectly clear sphere between the eye and the background must
        // leave the background exactly as it was.
        Color throughGlass = LookAlongZ(SceneShowing(new Sphere { Material = PerfectlyClear() }));

        Assert.IsTrue(Colors.Red.Matches(throughGlass), throughGlass.ToString());
    }

    [TestMethod]
    public void TestClearGlassDoesNotDarkenWhatIsBehindIt()
    {
        // Stated as the failure we actually care about, so a regression reads plainly: whatever
        // else may drift, the colour must not lose brightness.
        Color throughGlass = LookAlongZ(SceneShowing(new Sphere { Material = PerfectlyClear() }));

        Assert.IsTrue(throughGlass.Red >= Colors.Red.Red - 1e-9,
            $"Clear glass dimmed the red channel: {throughGlass}");
    }

    [TestMethod]
    public void TestStackedClearGlassStillDoesNotChangeAnything()
    {
        // Each clear surface costs two levels of recursion, one entering and one leaving, so a
        // stack of them is where a recursion budget shows up as darkness.
        Scene scene = SceneShowing(
            new Sphere { Material = PerfectlyClear(), Transform = Transforms.Scale(0.5) },
            new Sphere { Material = PerfectlyClear() });
        Color throughGlass = LookAlongZ(scene, 16);

        Assert.IsTrue(Colors.Red.Matches(throughGlass), throughGlass.ToString());
    }

    [TestMethod]
    public void TestRunningOutOfRecursionTurnsClearGlassBlack()
    {
        // Documents the real limit rather than hiding it: when the budget runs out mid-glass the
        // refracted ray gives up and reports black, which reads as a dark surface rather than a
        // clear one.  This is why deep stacks of glass need a bigger budget.
        Scene scene = SceneShowing(new Sphere { Material = PerfectlyClear() });

        Assert.IsTrue(Colors.Red.Matches(LookAlongZ(scene, 4)));
        Assert.IsTrue(Colors.Black.Matches(LookAlongZ(scene, 0)));
    }

    [TestMethod]
    public void TestTransparencyIsPerSurfaceCrossedNotPerObject()
    {
        // Worth pinning down, because it surprises: "transparency" is how much light one surface
        // lets through, not how much of an object's far side shows.  A pane is a single surface
        // and passes its 0.7; a sphere is two, one entering and one leaving, so it passes
        // 0.7 x 0.7.  A solid at 0.7 therefore looks dimmer than the number suggests -- correctly
        // so, and the same way a real pair of glass interfaces would behave.
        Material pane = PerfectlyClear();
        Material ball = PerfectlyClear();

        pane.Transparency = ball.Transparency = 0.7;

        Plane sheet = new () { Material = pane, Transform = Transforms.RotateAroundX(90) };
        Color throughPane = LookAlongZ(SceneShowing(sheet), 8);
        Color throughBall = LookAlongZ(SceneShowing(new Sphere { Material = ball }), 8);

        Assert.AreEqual(0.7, throughPane.Red, 1e-9, throughPane.ToString());
        Assert.AreEqual(0.7 * 0.7, throughBall.Red, 1e-9, throughBall.ToString());
    }

    /// <summary>
    /// Looks through a single pane of glass of the given colour and filtering at a white
    /// background, which is the plainest way to see what a filter does to the light.
    /// </summary>
    private static Color ThroughAPaneOf(Color colour, double filter)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.White) };

        scene.Lights.Add(new PointLight { Location = new Point(0, 0, -10) });

        Material glass = PerfectlyClear();

        glass.Pigment = new SolidPigment(colour);
        glass.Interior.Filter = filter;

        Plane pane = new () { Material = glass, Transform = Transforms.RotateAroundX(90) };

        pane.PrepareForRendering();
        scene.Surfaces.Add(pane);

        return scene.GetColorFor(new Ray(new Point(0, 0, -5), new Vector(0, 0, 1)), 8);
    }

    [TestMethod]
    public void TestFilteringTintsWhatPassesThrough()
    {
        // The capability this whole change exists for: red glass makes what is behind it red.
        // Before the interior existed, transparency could only dim, never colour, so a red pane
        // over white left the white stubbornly white.
        Color seen = ThroughAPaneOf(Colors.Red, 1);

        Assert.IsTrue(Colors.Red.Matches(seen), seen.ToString());
    }

    [TestMethod]
    public void TestNoFilteringStillPassesColourThroughUntouched()
    {
        // The old behaviour is still the default, so nothing already written changes meaning:
        // without filtering, a red pane dims but does not redden.
        Color seen = ThroughAPaneOf(Colors.Red, 0);

        Assert.IsTrue(Colors.White.Matches(seen), seen.ToString());
    }

    [TestMethod]
    public void TestPartialFilteringLandsBetweenTheTwo()
    {
        // Filtering rises smoothly from "not at all" to "entirely", so a scene can be dialled to
        // faintly tinted glass rather than only stained glass.
        double none = ThroughAPaneOf(Colors.Red, 0).Green;
        double some = ThroughAPaneOf(Colors.Red, 0.5).Green;
        double all = ThroughAPaneOf(Colors.Red, 1).Green;

        Assert.IsTrue(none > some && some > all, $"expected {none} > {some} > {all}");
    }

    [TestMethod]
    public void TestFilteringIsChargedPerSurfaceCrossed()
    {
        // Filtering follows transparency's rule: it is charged at each boundary, so a solid tints
        // twice -- going in and coming out -- and its colour deepens accordingly.  That is also
        // what keeps thicker-looking glass from looking identical to a pane.
        Scene scene = new () { Background = new SolidPigment(Colors.White) };

        scene.Lights.Add(new PointLight { Location = new Point(0, 0, -10) });

        Material glass = PerfectlyClear();

        glass.Pigment = new SolidPigment(new Color(1, 0.5, 0.5));
        glass.Interior.Filter = 1;

        Sphere ball = new () { Material = glass };

        ball.PrepareForRendering();
        scene.Surfaces.Add(ball);

        Color throughBall = scene.GetColorFor(new Ray(new Point(0, 0, -5), new Vector(0, 0, 1)), 8);
        Color throughPane = ThroughAPaneOf(new Color(1, 0.5, 0.5), 1);

        Assert.AreEqual(0.5, throughPane.Green, 1e-9, throughPane.ToString());
        Assert.AreEqual(0.25, throughBall.Green, 1e-9, throughBall.ToString());
    }

    [TestMethod]
    public void TestFilteringNeverBrightensWhatPassesThrough()
    {
        // A filter takes light out; it must never put any in.  Worth stating outright, since the
        // tint is a multiply and a badly chosen one could push a channel past what arrived.
        foreach (double filter in new[] { 0, 0.25, 0.5, 0.75, 1 })
        {
            Color seen = ThroughAPaneOf(Colors.Red, filter);

            Assert.IsTrue(seen.Red <= 1 + 1e-9 && seen.Green <= 1 + 1e-9 && seen.Blue <= 1 + 1e-9,
                $"filter {filter} brightened something: {seen}");
        }
    }

    [TestMethod]
    public void TestFilteringFollowsAPatternedPigment()
    {
        // The tint is taken from the pigment where the ray actually crossed, not from one colour
        // averaged over the whole surface, so patterned glass filters as a pattern -- which is
        // what stained glass is.
        PatternPigment checks = new ()
        {
            Pattern = new CheckerPattern(),
            PigmentSet = new PigmentSet()
        };

        checks.PigmentSet.AddEntry(new SolidPigment(Colors.White));
        checks.PigmentSet.AddEntry(new SolidPigment(Colors.Red), 1);

        Scene scene = new () { Background = new SolidPigment(Colors.White) };

        scene.Lights.Add(new PointLight { Location = new Point(0, 0, -10) });

        Material glass = PerfectlyClear();

        glass.Pigment = checks;
        glass.Interior.Filter = 1;

        Plane pane = new () { Material = glass, Transform = Transforms.RotateAroundX(90) };

        pane.PrepareForRendering();
        scene.Surfaces.Add(pane);

        // Two rays a full check apart cross the pane on differently coloured squares.
        Color first = scene.GetColorFor(new Ray(new Point(0.5, 0, -5), new Vector(0, 0, 1)), 8);
        Color second = scene.GetColorFor(new Ray(new Point(1.5, 0, -5), new Vector(0, 0, 1)), 8);

        Assert.IsFalse(first.Matches(second), $"the pattern did not carry through: {first} vs {second}");
    }

    [TestMethod]
    public void TestTransparencyIgnoresHowFarLightTravelsThroughIt()
    {
        // Absorption does not accumulate with distance: a ten-fold thicker piece of the same
        // glass takes out exactly as much light, because transparency is charged per surface
        // crossed rather than per unit travelled.  POV-Ray's interior fade_distance/fade_power
        // is what models the other behaviour, and has no equivalent here.
        Material thin = PerfectlyClear();
        Material thick = PerfectlyClear();

        thin.Transparency = thick.Transparency = 0.7;

        Color throughThin = LookAlongZ(SceneShowing(
            new Sphere { Material = thin, Transform = Transforms.Scale(0.2) }), 8);
        Color throughThick = LookAlongZ(SceneShowing(
            new Sphere { Material = thick, Transform = Transforms.Scale(2.0) }), 8);

        Assert.IsTrue(throughThin.Matches(throughThick), $"{throughThin} vs {throughThick}");
    }

    [TestMethod]
    public void TestClearGlassWithARealIndexStillPassesLightThrough()
    {
        // Glass that actually refracts bends the path, so what is seen through it may come from
        // somewhere else -- but against a background of one flat colour there is nowhere else to
        // look, and it must still come through undimmed.
        Color throughGlass = LookAlongZ(SceneShowing(new Sphere { Material = PerfectlyClear(1.5) }), 8);

        Assert.IsTrue(Colors.Red.Matches(throughGlass), throughGlass.ToString());
    }
}
