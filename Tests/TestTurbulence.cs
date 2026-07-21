using RayTracer.Basics;
using RayTracer.Patterns;

namespace Tests;

/// <summary>
/// These tests cover turbulence as a stirring of the points a pattern is asked about, rather than
/// as something each pattern has to know how to do for itself.  That is how POV-Ray has always
/// worked, and it is what lets a library texture say "granite turbulence 0.4" and be understood.
/// </summary>
[TestClass]
public class TestTurbulence
{
    private static readonly Point Somewhere = new (0.3, 0.7, 0.4);

    [TestMethod]
    public void TestAPatternWithNoTurbulenceIsAskedAboutThePointItWasGiven()
    {
        // The compatibility guarantee: with nothing named, a pattern sees exactly the point the
        // caller had in mind, so every pattern behaves as it always did.
        GranitePattern pattern = new ();

        Assert.AreEqual(pattern.Evaluate(Somewhere), pattern.ValueFor(Somewhere), 1e-12);
    }

    [TestMethod]
    public void TestTurbulenceStirsAPatternThatDoesNotStirItsOwnPoints()
    {
        // The capability this exists for.  Granite has no idea turbulence exists -- it never did,
        // and it still does not -- yet it comes out stirred, because the point it is handed has
        // been pushed around before it ever sees it.
        GranitePattern pattern = new ()
        {
            Turbulence = new Turbulence { Octaves = 3, Seed = 5 }
        };

        Assert.AreNotEqual(pattern.Evaluate(Somewhere), pattern.ValueFor(Somewhere));
    }

    [TestMethod]
    public void TestMarbleAndWoodAreLeftToStirTheirOwnPoints()
    {
        // These two fold turbulence into their own arithmetic, so stirring the point as well would
        // apply it twice.  POV-Ray carves out exactly the same two exceptions, and for the same
        // reason: what they want stirred is the one coordinate they are built from.
        Turbulence turbulence = new () { Octaves = 3, Seed = 5 };
        MarblePattern marble = new () { Turbulence = turbulence };
        WoodPattern wood = new () { Turbulence = turbulence };

        Assert.AreEqual(marble.Evaluate(Somewhere), marble.ValueFor(Somewhere), 1e-12,
            "marble's points were stirred on top of the turbulence it applies itself");
        Assert.AreEqual(wood.Evaluate(Somewhere), wood.ValueFor(Somewhere), 1e-12,
            "wood's points were stirred on top of the turbulence it applies itself");
    }

    [TestMethod]
    public void TestAmplitudeSaysHowFarAPointMayBePushed()
    {
        // An amplitude of nothing leaves the point where it was, however many layers are summed,
        // which is what makes it the natural "off" setting.
        Turbulence still = new () { Octaves = 3, Seed = 5, Amplitude = new Vector(0, 0, 0) };
        Point stirred = still.Warp(Somewhere);

        Assert.AreEqual(Somewhere.X, stirred.X, 1e-12);
        Assert.AreEqual(Somewhere.Y, stirred.Y, 1e-12);
        Assert.AreEqual(Somewhere.Z, stirred.Z, 1e-12);
    }

    [TestMethod]
    public void TestAmplitudeMayDifferPerAxis()
    {
        // The case the wood textures need: stirred across the grain but left nearly alone along it,
        // which is written in POV as something like "turbulence <0.04, 0.04, 0.1>".
        Turbulence sideways = new () { Octaves = 3, Seed = 5, Amplitude = new Vector(1, 0, 0) };
        Point stirred = sideways.Warp(Somewhere);

        Assert.AreNotEqual(Somewhere.X, stirred.X);
        Assert.AreEqual(Somewhere.Y, stirred.Y, 1e-12, "Y was stirred despite an amplitude of zero");
        Assert.AreEqual(Somewhere.Z, stirred.Z, 1e-12, "Z was stirred despite an amplitude of zero");
    }

    [TestMethod]
    public void TestFinerAndFainterChangeTheLayersTheyGovern()
    {
        // These were constants baked into the loop before -- two and a half, which are the values
        // POV-Ray defaults to.  Keeping those as the defaults is what makes exposing them a change
        // nothing already written can notice.
        Turbulence standard = new () { Octaves = 4, Seed = 5 };

        Assert.AreEqual(2, standard.Finer, 1e-12);
        Assert.AreEqual(0.5, standard.Fainter, 1e-12);

        Turbulence tighter = new () { Octaves = 4, Seed = 5, Finer = 3 };
        Turbulence flatter = new () { Octaves = 4, Seed = 5, Fainter = 0.9 };

        Assert.AreNotEqual(standard.Generate(Somewhere), tighter.Generate(Somewhere));
        Assert.AreNotEqual(standard.Generate(Somewhere), flatter.Generate(Somewhere));
    }

    [TestMethod]
    public void TestOneLayerIsUnaffectedByFainter()
    {
        // A sanity check on which knob does what: "fainter" only ever weakens the *second* layer
        // and those after it, so with a single layer it can have nothing to say.
        Turbulence half = new () { Octaves = 1, Seed = 5, Fainter = 0.5 };
        Turbulence most = new () { Octaves = 1, Seed = 5, Fainter = 0.9 };

        Assert.AreEqual(half.Generate(Somewhere), most.Generate(Somewhere), 1e-12);
    }

    /// <summary>
    /// A pattern that hands back whatever X it was given, so a test can see exactly what the
    /// shaping did to it without a real pattern's arithmetic in the way.
    /// </summary>
    private class PassThroughPattern : Pattern
    {
        public override int DiscretePigmentsNeeded => 0;

        public override double Evaluate(Point point) => point.X;
    }

    private static double ShapedAt(Pattern pattern, double x) =>
        pattern.ValueFor(new Point(x, 0, 0));

    [TestMethod]
    public void TestAPatternWithNoShapingAskedForIsLeftAlone()
    {
        // This has to be exact rather than merely close, and it matters more than it looks: several
        // patterns deliberately hand back values outside [0, 1] -- marble's is a coordinate the
        // colour map is meant to wrap for itself -- so shaping that wrapped unbidden would quietly
        // break them.  POV-Ray wraps whenever its frequency is non-zero; we wrap only when asked.
        PassThroughPattern pattern = new ();

        Assert.AreEqual(3.7, ShapedAt(pattern, 3.7), 1e-12);
        Assert.AreEqual(-2.5, ShapedAt(pattern, -2.5), 1e-12);
    }

    [TestMethod]
    public void TestFrequencyMakesAPatternRepeat()
    {
        // Raising the frequency multiplies before the wrap, so the pattern runs through its whole
        // range several times over rather than being stretched across it once.
        PassThroughPattern pattern = new () { Frequency = 3 };

        Assert.AreEqual(0.0, ShapedAt(pattern, 0), 1e-12);
        Assert.AreEqual(0.9, ShapedAt(pattern, 0.3), 1e-12);
        // A third of the way along, the pattern has already come full circle and started again.
        Assert.AreEqual(0.0, ShapedAt(pattern, 1.0 / 3), 1e-12);
    }

    [TestMethod]
    public void TestPhaseSlidesAPatternAlong()
    {
        // Phase adds, so the whole pattern shifts without changing shape -- which is how a band is
        // nudged off a seam it happened to land on.
        PassThroughPattern pattern = new () { Phase = 0.25 };

        Assert.AreEqual(0.35, ShapedAt(pattern, 0.1), 1e-12);
        // And it wraps rather than running off the end.
        Assert.AreEqual(0.15, ShapedAt(pattern, 0.9), 1e-12);
    }

    [TestMethod]
    public void TestTheWrapKeepsNegativesInRange()
    {
        // A negative frequency runs the pattern backwards, which can leave the value below zero;
        // it has to come back into range or the colour map has nothing to look up.
        PassThroughPattern pattern = new () { Frequency = -1 };

        foreach (double x in new[] { 0.25, 0.5, 0.75, 1.5, 3.3 })
        {
            double shaped = ShapedAt(pattern, x);

            Assert.IsTrue(shaped >= 0 && shaped <= 1, $"{x} shaped to {shaped}, outside [0, 1]");
        }
    }

    [TestMethod]
    public void TestEachWaveShapesTheValueItsOwnWay()
    {
        // Rather than pin every curve, this pins that each is a different curve -- and the two
        // anchors worth stating outright: a ramp changes nothing, and a sine peaks in the middle.
        Assert.AreEqual(0.5, ShapedAt(new PassThroughPattern { Wave = WaveType.Ramp }, 0.5), 1e-12);
        Assert.AreEqual(1.0, ShapedAt(new PassThroughPattern { Wave = WaveType.Sine }, 0.25), 1e-9);
        Assert.AreEqual(1.0, ShapedAt(new PassThroughPattern { Wave = WaveType.Triangle }, 0.5), 1e-12);

        double[] shaped =
        [
            ShapedAt(new PassThroughPattern { Wave = WaveType.Ramp }, 0.3),
            ShapedAt(new PassThroughPattern { Wave = WaveType.Sine }, 0.3),
            ShapedAt(new PassThroughPattern { Wave = WaveType.Triangle }, 0.3),
            ShapedAt(new PassThroughPattern { Wave = WaveType.Scallop }, 0.3),
            ShapedAt(new PassThroughPattern { Wave = WaveType.Cubic }, 0.3)
        ];

        Assert.AreEqual(shaped.Length, shaped.Distinct().Count(), "two waves shaped 0.3 alike");
    }

    [TestMethod]
    public void TestAPolynomialWaveUsesItsExponent()
    {
        // The one wave that takes a number of its own, and the exponent has to reach it: squaring
        // pushes the value down, and an exponent of one leaves it alone.
        Assert.AreEqual(0.25, ShapedAt(new PassThroughPattern
        {
            Wave = WaveType.Poly, Exponent = 2
        }, 0.5), 1e-12);

        Assert.AreEqual(0.5, ShapedAt(new PassThroughPattern
        {
            Wave = WaveType.Poly, Exponent = 1
        }, 0.5), 1e-12);
    }

    [TestMethod]
    public void TestShapingHappensAfterTurbulenceHasStirredThePoint()
    {
        // The order the two run in: turbulence moves the point, then the pattern is asked about it,
        // and only then is the answer shaped.  If shaping came first it would be shaping a value
        // taken from the wrong place entirely.
        PassThroughPattern plain = new ()
        {
            Turbulence = new Turbulence { Octaves = 3, Seed = 5 }
        };
        PassThroughPattern shaped = new ()
        {
            Turbulence = new Turbulence { Octaves = 3, Seed = 5 },
            Wave = WaveType.Sine
        };

        double stirred = plain.ValueFor(Somewhere);

        Assert.AreNotEqual(Somewhere.X, stirred, "the point was not stirred");
        Assert.AreEqual((1 + Math.Sin(stirred * 2 * Math.PI)) * 0.5, shaped.ValueFor(Somewhere), 1e-9);
    }
}
