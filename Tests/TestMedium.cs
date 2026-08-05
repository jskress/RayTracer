using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover what a medium does to a ray crossing it.  They are arithmetic rather than
/// pictures, because with the density even throughout there is an exact answer to check against:
/// what survives a crossing is <c>exp(-σd)</c>, and what the medium adds along the way is the
/// integral of its own light dimmed by however much medium still lay ahead of it.
/// </summary>
[TestClass]
public class TestMedium
{
    /// <summary>
    /// Integrates what a medium of the given coefficients gives off over the given distance, the
    /// slow and obvious way, as something to hold the closed form to.
    /// </summary>
    private static double EmissionByBruteForce(
        double absorption, double emission, double density, double distance, int steps = 2_000_000)
    {
        double step = distance / steps;
        double total = 0;

        for (int index = 0; index < steps; index++)
        {
            double at = (index + 0.5) * step;

            total += emission * density * Math.Exp(-absorption * density * at) * step;
        }

        return total;
    }

    [TestMethod]
    public void TestWhatSurvivesACrossingFollowsBeersLaw()
    {
        Medium medium = new () { Absorption = new Color(0.1, 0.2, 0.4) };

        Color through = medium.GetTransmittanceOver(3);

        Assert.AreEqual(Math.Exp(-0.1 * 3), through.Red, 1e-12);
        Assert.AreEqual(Math.Exp(-0.2 * 3), through.Green, 1e-12);
        Assert.AreEqual(Math.Exp(-0.4 * 3), through.Blue, 1e-12);
    }

    [TestMethod]
    public void TestDensityMultipliesTheAbsorption()
    {
        // Twice as much of the stuff absorbs as strongly as twice the distance of it does, which is
        // what having the two as separate numbers is worth: how much there is may be tuned without
        // touching what it does.
        Medium thin = new () { Absorption = new Color(0.3, 0.3, 0.3), Density = 1 };
        Medium thick = new () { Absorption = new Color(0.3, 0.3, 0.3), Density = 2 };

        Assert.AreEqual(
            thin.GetTransmittanceOver(4).Red, thick.GetTransmittanceOver(2).Red, 1e-12);
    }

    [TestMethod]
    public void TestWhatTheMediumAddsMatchesTheIntegral()
    {
        // The closed form against the sum it stands for.  A medium's own light is dimmed by the
        // medium still in front of it, so what a long span adds is nothing like its length times
        // its brightness -- the far end of it barely shows.
        const double absorption = 0.35;
        const double emission = 0.8;
        const double density = 1.4;
        const double distance = 6;

        Medium medium = new ()
        {
            Absorption = new Color(absorption, absorption, absorption),
            Emission = new Color(emission, emission, emission),
            Density = density
        };
        Color seen = medium.ApplyOver(Colors.Black, distance);
        double expected = EmissionByBruteForce(absorption, emission, density, distance);

        Assert.AreEqual(expected, seen.Red, 1e-6, $"the medium added {seen.Red}, not {expected}");
    }

    [TestMethod]
    public void TestWhatIsBehindArrivesDimmed()
    {
        Medium medium = new () { Absorption = new Color(0.5, 0.5, 0.5) };
        Color seen = medium.ApplyOver(new Color(1, 1, 1), 2);

        Assert.AreEqual(Math.Exp(-1), seen.Red, 1e-12);
    }

    [TestMethod]
    public void TestAnEndlessCrossingSettlesAtTheMediumsOwnColor()
    {
        // The one that decides what a ray which strikes nothing comes back with.  Let the span run
        // on forever and what lies beyond cannot matter at all, so what is left is the color the
        // medium's own numbers imply: what it gives off, divided by what it takes out.
        Medium medium = new ()
        {
            Absorption = new Color(0.2, 0.4, 0.5),
            Emission = new Color(0.1, 0.3, 0.5)
        };
        Color endless = medium.ApplyOver(new Color(1, 1, 1), double.PositiveInfinity);

        Assert.AreEqual(0.1 / 0.2, endless.Red, 1e-12);
        Assert.AreEqual(0.3 / 0.4, endless.Green, 1e-12);
        Assert.AreEqual(0.5 / 0.5, endless.Blue, 1e-12);

        // And it does not matter in the least what was behind it.
        Color otherSide = medium.ApplyOver(Colors.Black, double.PositiveInfinity);

        Assert.IsTrue(endless.Matches(otherSide), $"{endless} against {otherSide}");
    }

    [TestMethod]
    public void TestAMediumThatAbsorbsNothingStillAddsItsOwnLightEvenly()
    {
        // The general form for what a medium adds is a nought over a nought when it absorbs nothing,
        // so this is the limit rather than the formula: with nothing taking light out, what is put
        // in simply piles up with the distance.
        Medium medium = new () { Emission = new Color(0.25, 0.25, 0.25) };

        Assert.AreEqual(0.5, medium.ApplyOver(Colors.Black, 2).Red, 1e-12);
        Assert.AreEqual(1.0, medium.ApplyOver(Colors.Black, 4).Red, 1e-12);

        // And it must arrive at that limit smoothly rather than leaping to it, so a scene tuning its
        // absorption toward nothing sees no step change.
        Medium nearly = new ()
        {
            Absorption = new Color(1e-9, 1e-9, 1e-9), Emission = new Color(0.25, 0.25, 0.25)
        };

        Assert.AreEqual(0.5, nearly.ApplyOver(Colors.Black, 2).Red, 1e-8);
    }

    [TestMethod]
    public void TestTheTwoFormsAgreeWhereTheyMeet()
    {
        // What a medium adds is worked out two ways -- the plain form, and a series for when the
        // plain form would be all cancellation -- so the seam between them is worth standing on.
        // A visible step here would show up as a band in any scene whose absorption crossed it.
        Medium medium = new () { Emission = new Color(0.7, 0.7, 0.7) };

        foreach (double crossings in new[] { 1e-4, 1e-4 + 1e-12, 1e-4 - 1e-12 })
        {
            medium.Absorption = new Color(crossings / 2, crossings / 2, crossings / 2);

            double added = medium.ApplyOver(Colors.Black, 2).Red;
            double expected = 0.7 / (crossings / 2) * (1 - Math.Exp(-crossings));

            Assert.AreEqual(expected, added, 1e-10, $"at {crossings} crossings");
        }
    }

    [TestMethod]
    public void TestTheSurroundingsDimALampAsWellAsAView()
    {
        // A fog stands between a lamp and what it lights as surely as it stands between the eye and
        // what it looks at, so the light itself must be charged for its trip.  What the medium gives
        // off has no part in this: a shadow ray asks after the lamp's light and nothing else.
        Scene scene = new ()
        {
            Environment = new SceneEnvironment
            {
                Medium = new Medium
                {
                    Absorption = new Color(0.25, 0.25, 0.25),
                    Emission = new Color(0.5, 0.5, 0.5)
                }
            }
        };
        Color reaching = scene.GetLightReaching(
            Point.Zero, new Vector(0, 0, 1), 4);

        Assert.AreEqual(Math.Exp(-1), reaching.Red, 1e-12, $"the lamp arrived at {reaching.Red}");

        // And with nothing in the way at all, the lamp arrives whole.
        Assert.IsTrue(Colors.White.Matches(
            new Scene().GetLightReaching(Point.Zero, new Vector(0, 0, 1), 4)));
    }

    [TestMethod]
    public void TestAMediumWithNothingToSayChangesNothing()
    {
        Color color = new (0.3, 0.6, 0.9, 0.5);

        Assert.IsTrue(color.Matches(new Medium().ApplyOver(color, 10)));
        Assert.IsTrue(color.Matches(
            new Medium { Absorption = new Color(1, 1, 1), Density = 0 }.ApplyOver(color, 10)));

        // Nor does any medium have anything to say about a crossing of no length at all.
        Assert.IsTrue(color.Matches(
            new Medium { Absorption = new Color(1, 1, 1) }.ApplyOver(color, 0)));
    }

    [TestMethod]
    public void TestAMediumCoversWhatItHides()
    {
        // A fog is a thing that is there, so it stands in front of whatever is behind it -- which
        // for an image rendered over nothing at all means the pixels it fills come out opaque
        // rather than empty.
        Medium medium = new () { Absorption = new Color(0.5, 0.5, 0.5) };
        Color overNothing = medium.ApplyOver(Colors.Transparent, 20);

        Assert.IsTrue(overNothing.Alpha > 0.99, $"the fog covered only {overNothing.Alpha}");

        // A thin one covers only a little.
        Color barely = medium.ApplyOver(Colors.Transparent, 0.01);

        Assert.IsTrue(barely.Alpha < 0.01, $"a whisper of fog covered {barely.Alpha}");
    }

    [TestMethod]
    public void TestAMediumKnowsWhetherItNeedsAFarSide()
    {
        // Light given off where none is absorbed has nothing to settle it, so over an endless span
        // such a medium is infinitely bright.  It is only a fair description of something bounded.
        Assert.IsFalse(new Medium().MustBeBounded);
        Assert.IsFalse(new Medium
        {
            Absorption = new Color(0.1, 0.1, 0.1), Emission = new Color(0.1, 0.1, 0.1)
        }.MustBeBounded);
        Assert.IsTrue(new Medium { Emission = new Color(0.1, 0.1, 0.1) }.MustBeBounded);

        // One color is enough to want a far side, since the others cannot make up for it.
        Assert.IsTrue(new Medium
        {
            Absorption = new Color(0.1, 0.1, 0), Emission = new Color(0.1, 0.1, 0.1)
        }.MustBeBounded);
    }
}
