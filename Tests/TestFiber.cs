using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// How a strand of hair scatters light (Chiang et al. 2016): that it keeps its energy, puts its lobes
/// where a glassy cylinder would, and turns colors into absorption and back.
/// </summary>
[TestClass]
public class TestFiber
{
    /// <summary>
    /// A strand that absorbs nothing sends on all of the light it meets: lit evenly from every
    /// direction by light of one, it sends one back toward any eye, wherever across it the eye looks
    /// and however rough it is.  PBRT holds its hair to the same test.
    /// </summary>
    [TestMethod]
    public void TestAStrandThatAbsorbsNothingLosesNothing()
    {
        Random random = new (5);

        foreach (double roughness in new[] { 0.2, 0.3, 0.5, 0.8 })
        {
            Fiber fiber = Fiber.For(roughness);

            for (int trial = 0; trial < 4; trial++)
            {
                double sinThetaO = random.NextDouble() * 1.6 - 0.8;
                double h = random.NextDouble() * 1.8 - 0.9;
                double total = Total(fiber, sinThetaO, h, RgbSpectrum.Black);

                Assert.AreEqual(1, total, 0.03, $"roughness {roughness}, sin θ {sinThetaO:F2}, h {h:F2}");
            }
        }
    }

    /// <summary>
    /// A strand that absorbs sends on less, and the more it absorbs the less.
    /// </summary>
    [TestMethod]
    public void TestAStrandThatAbsorbsSendsOnLess()
    {
        Fiber fiber = Fiber.For(0.3);
        double clear = Total(fiber, 0.1, 0.3, RgbSpectrum.Black);
        double blond = Total(fiber, 0.1, 0.3, RgbSpectrum.FromUnbounded(Fiber.AbsorptionFor(Melanin.Shades["blond"])));
        double black = Total(fiber, 0.1, 0.3, RgbSpectrum.FromUnbounded(Fiber.AbsorptionFor(Melanin.Shades["black"])));

        Assert.IsTrue(clear > blond && blond > black, $"{clear} {blond} {black}");
        Assert.IsTrue(black > 0.03, "black hair lost even its white highlight");
    }

    /// <summary>
    /// The highlight off the outside of a strand is where a mirror on a cylinder would put it: an eye
    /// whose ray passes the strand's middle at an offset sees a lamp mirrored at twice the angle round
    /// the strand that the offset makes, on the side the offset is toward.
    /// </summary>
    [TestMethod]
    public void TestTheHighlightIsWhereACylinderMirrorsTheLamp()
    {
        Fiber fiber = Fiber.For(0.2);
        // Black, so that only what is mirrored off the outside comes back at all.
        RgbSpectrum absorbing = RgbSpectrum.FromUnbounded(new Color(50, 50, 50));

        foreach (double h in new[] { 0.3, -0.3, 0.6 })
        {
            Fiber.Seen<RgbSpectrum> seen = fiber.Seeing(0, Math.PI / 2, h, absorbing);
            double gamma = Math.Asin(h);
            double mirrored = fiber.Scattered(seen, 0, Math.PI / 2 - 2 * gamma)[0];
            double opposite = fiber.Scattered(seen, 0, Math.PI / 2 + 2 * gamma)[0];

            Assert.IsTrue(mirrored > 5 * opposite, $"h {h}: mirrored {mirrored}, opposite {opposite}");
        }
    }

    /// <summary>
    /// Light passes through a clear strand nearly whole, through blond hair partly, and through black
    /// hair not at all; and more of it is mirrored away near the edge than through the middle.
    /// </summary>
    [TestMethod]
    public void TestLightPassesThroughAsTheHairAllows()
    {
        double Passed(Melanin melanin, double h) =>
            Fiber.PassedThrough(0, h, RgbSpectrum.FromUnbounded(Fiber.AbsorptionFor(melanin)))[0];

        Assert.IsTrue(Passed(new Melanin(0, 0), 0) > 0.9);
        Assert.IsTrue(Passed(Melanin.Shades["blond"], 0) is > 0.5 and < 0.95);
        Assert.IsTrue(Passed(Melanin.Shades["black"], 0) < 0.005);
        Assert.IsTrue(Passed(new Melanin(0, 0), 0.95) < Passed(new Melanin(0, 0), 0));
    }

    /// <summary>
    /// The color a head of hair is to come out is turned into absorption and back unchanged, and a
    /// darker color absorbs more.
    /// </summary>
    [TestMethod]
    public void TestAColorIsMatchedByItsAbsorption()
    {
        foreach (double channel in new[] { 0.02, 0.2, 0.5, 0.9 })
            Assert.AreEqual(channel, Fiber.ColorFor(Fiber.AbsorptionFor(channel, 0.3), 0.3), 1e-9);

        Assert.IsTrue(Fiber.AbsorptionFor(0.1, 0.3) > Fiber.AbsorptionFor(0.6, 0.3));
        Assert.AreEqual(0, Fiber.AbsorptionFor(1, 0.3), 1e-12);
    }

    /// <summary>
    /// The named shades run from dark to light as their names say, and red hair is red.
    /// </summary>
    [TestMethod]
    public void TestTheNamedShadesLookLikeTheirNames()
    {
        Color Of(string shade) => Fiber.ColorFor(Fiber.AbsorptionFor(Melanin.Shades[shade]), 0.3);

        string[] darkToLight = ["black", "brown", "auburn", "blond", "platinum"];

        for (int index = 1; index < darkToLight.Length; index++)
        {
            Assert.IsTrue(Of(darkToLight[index]).Red > Of(darkToLight[index - 1]).Red,
                $"{darkToLight[index]} is not lighter than {darkToLight[index - 1]}");
        }

        Color ginger = Of("ginger");

        Assert.IsTrue(ginger.Red > 2 * ginger.Green && ginger.Green > 2 * ginger.Blue, $"ginger came out {ginger}");
    }

    /// <summary>
    /// This method sums what a strand sends toward one eye, lit evenly from every direction by light of
    /// one: the integral of its scattering over the sphere, worked out on a grid even in the sine of the
    /// angle along the strand and in the angle around it, which is even in solid angle.
    /// </summary>
    private static double Total(Fiber fiber, double sinThetaO, double h, RgbSpectrum absorption)
    {
        const int steps = 600;
        Fiber.Seen<RgbSpectrum> seen = fiber.Seeing(sinThetaO, Math.PI / 2, h, absorption);
        double sum = 0;

        for (int along = 0; along < steps; along++)
        {
            double sinThetaI = -1 + 2 * (along + 0.5) / steps;

            for (int around = 0; around < steps; around++)
            {
                double phiI = -Math.PI + 2 * Math.PI * (around + 0.5) / steps;

                sum += fiber.Scattered(seen, sinThetaI, phiI)[0];
            }
        }

        return sum * (2.0 / steps) * (2 * Math.PI / steps);
    }
}
