using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover what the air does to light passing through it.
/// <para>
/// The air is the rare part of a renderer where the right answers were measured long ago by people
/// with instruments, so almost nothing here is a matter of judgement.  That is the reason to lean on
/// it: a sky can be tuned until it looks nice from any number of wrong models, and these are the
/// numbers that say which model is actually the air.
/// </para>
/// </summary>
[TestClass]
public class TestAtmosphere
{
    [TestMethod]
    public void TestAVerticalColumnOfAirTakesOutWhatItIsMeasuredTo()
    {
        // The number the whole model stands on.  Looking straight up through the entire atmosphere,
        // green light at 550 nanometers is diminished by 0.0973 -- a measured quantity.  Nothing in
        // the model was fitted to it: it falls out of how strongly air polarizes, how much of it
        // there is, and how quickly it thins with height.  So this either agrees or the model is
        // wrong somewhere, and there is nowhere to hide.
        double measured = 0.0973;
        double derived = Atmosphere.VerticalAirDepthAt(550);

        Assert.AreEqual(measured, derived, measured * 0.02,
            $"a vertical column came to {derived} against a measured {measured}");
    }

    [TestMethod]
    public void TestBlueIsTurnedAsideFarMoreReadilyThanRed()
    {
        // Rayleigh's result, and the reason the sky is blue rather than some other color: scattering
        // runs as the inverse fourth power of the wavelength.  Red at 680 against blue at 440 is a
        // ratio of (680/440)^4, near enough 5.7.  The agreement will not be exact, and should not be,
        // since air bends blue a little more than red and that enters the answer as well.
        double blue = Atmosphere.AirScatteringAt(440);
        double red = Atmosphere.AirScatteringAt(680);
        double plainly = Math.Pow(680.0 / 440, 4);

        Assert.AreEqual(plainly, blue / red, plainly * 0.05,
            $"blue over red came to {blue / red} against a plain fourth power of {plainly}");

        // And the size of it.  These follow from the measured vertical depth above rather than being
        // independent of it, so they are here to catch a wavelength going astray rather than as a
        // second check of the physics.
        //
        // Worth knowing if this is ever compared against another renderer: the triple that circulates
        // in graphics -- 5.8, 13.5 and 33.1 millionths for red, green and blue -- is some fifteen
        // percent heavier than these, and is paired with a scale height of 8000 rather than 8435.  The
        // two errors do not cancel; that combination puts eleven percent more air in a vertical column
        // than has ever been measured there.  This model is the measurement's, not that convention's,
        // and a sky rendered from it is correspondingly a little less blue.
        Assert.AreEqual(28.7e-6, blue, 28.7e-6 * 0.03, $"440nm came to {blue}");
        Assert.AreEqual(4.85e-6, red, 4.85e-6 * 0.03, $"680nm came to {red}");
    }

    [TestMethod]
    public void TestAirBendsBlueMoreThanRed()
    {
        // Small, and it matters: this difference is squared on its way into the scattering.  Standard
        // air is about 1.000278 in the green, and blue is bent more than red.
        double blue = Atmosphere.RefractiveIndexOfAir(440);
        double green = Atmosphere.RefractiveIndexOfAir(550);
        double red = Atmosphere.RefractiveIndexOfAir(680);

        Assert.AreEqual(1.000278, green, 2e-6, $"green came to {green}");
        Assert.IsTrue(blue > green && green > red,
            $"the order is wrong: {blue}, {green}, {red}");
    }

    [TestMethod]
    public void TestTheAirAndTheHazeThinAtQuiteDifferentRates()
    {
        // Why a hazy day washes out the horizon and leaves the zenith blue.  One scale height up, both
        // are down to the same share of themselves -- but the haze gets there seven times sooner.
        Assert.AreEqual(Math.Exp(-1), Atmosphere.AirDensityAt(Atmosphere.AirScaleHeight), 1e-12);
        Assert.AreEqual(Math.Exp(-1), Atmosphere.HazeDensityAt(Atmosphere.HazeScaleHeight), 1e-12);

        // At two kilometres up most of the haze is already below you and most of the air is not.
        Assert.IsTrue(Atmosphere.HazeDensityAt(2000) < 0.2, "the haze should be nearly gone");
        Assert.IsTrue(Atmosphere.AirDensityAt(2000) > 0.75, "the air should barely have thinned");

        // Below the ground counts as the ground rather than as more air than there is.
        Assert.AreEqual(1, Atmosphere.AirDensityAt(-100));
    }

    [TestMethod]
    public void TestHazeRunsFromNothingAtATurbidityOfOne()
    {
        // A turbidity of one means air with nothing in it, so the haze must vanish there rather than
        // merely getting small -- otherwise there is no way to write a scene with none.
        Assert.AreEqual(0, new Atmosphere { Turbidity = 1 }.HazeScattering());
        Assert.AreEqual(0, new Atmosphere { Turbidity = 0.5 }.HazeScattering(),
            "and nonsense below one is still nothing rather than negative");

        Atmosphere clear = new () { Turbidity = 2.5 };
        Atmosphere murky = new () { Turbidity = 6 };

        Assert.IsTrue(murky.HazeScattering() > clear.HazeScattering() * 3,
            "thick haze should be plainly thicker");

        // Haze swallows a little of what it takes, so rather more is removed than is turned aside.
        Assert.IsTrue(clear.HazeExtinction() > clear.HazeScattering());
    }

    [TestMethod]
    public void TestHazeIsNearlyBlindToColorWhereTheAirIsNot()
    {
        // The other half of why haze reads as white and the sky as blue.  Haze is not merely nearly
        // the same at every wavelength in this model, it takes no wavelength at all -- droplets and
        // dust being large enough to turn every color much alike -- so there is nothing here to
        // assert about it that is not simply the shape of the method.  What can be checked is the
        // contrast: across the same range the air differs by several times over.
        double ratio = Atmosphere.AirScatteringAt(440) / Atmosphere.AirScatteringAt(680);

        Assert.IsTrue(ratio > 4,
            $"the air should be strongly colored in what it turns aside, and gave {ratio}");
    }

    [TestMethod]
    public void TestEveryBandGetsItsOwnScattering()
    {
        double[] perBand = Atmosphere.AirScatteringPerBand();

        Assert.AreEqual(SpectralColor.Bands, perBand.Length);

        // Shorter wavelengths are turned aside more, all the way along, with no band out of order.
        for (int band = 1; band < perBand.Length; band++)
        {
            Assert.IsTrue(perBand[band] < perBand[band - 1],
                $"band {band} at {SpectralColor.WavelengthOf(band)}nm broke the order");
        }
    }
}
