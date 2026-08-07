using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover turning light described by wavelength into the three numbers the renderer works
/// in.
/// <para>
/// Nearly everything here has an answer that is known in advance rather than one to be judged by eye,
/// which is the reason to check it at all: the sky that will be built on top of this is meant to be
/// held to measurements, and it can only be held to them if what carries its colors is right first.
/// </para>
/// </summary>
[TestClass]
public class TestSpectralColor
{
    /// <summary>
    /// Builds a spectrum holding the same amount of light at every wavelength.
    /// </summary>
    private static double[] Even(double amount = 1)
    {
        double[] bands = new double[SpectralColor.Bands];

        Array.Fill(bands, amount);

        return bands;
    }

    [TestMethod]
    public void TestEvenLightComesBackAtTheStrengthItWentIn()
    {
        // The number that ties this to the rest of the renderer: a light of one must stay a light of
        // one, or every spectrum computed here would arrive at some scale of its own.
        Color seen = SpectralColor.ToColor(Even());

        Assert.AreEqual(1, seen.Red, 0.02, $"even light gave {seen}");
        Assert.AreEqual(1, seen.Green, 0.02, $"even light gave {seen}");
        Assert.AreEqual(1, seen.Blue, 0.02, $"even light gave {seen}");

        // And twice the light is twice the color, there being nothing in here that bends.
        Color twice = SpectralColor.ToColor(Even(2));

        Assert.AreEqual(2 * seen.Red, twice.Red, 1e-12);
        Assert.AreEqual(2 * seen.Green, twice.Green, 1e-12);
        Assert.AreEqual(2 * seen.Blue, twice.Blue, 1e-12);
    }

    [TestMethod]
    public void TestTheMatchingFunctionsWeighTheThreeAnswersEqually()
    {
        // The sharpest check there is on the fitted curves, and the reason it works is that the CIE
        // scaled its three functions so that each sums to the same amount.  That is what puts light of
        // the same strength at every wavelength at dead center of the chromaticity chart, and it is a
        // published property rather than one of mine.  An error in any one curve leans these totals
        // apart, and no amount of later scaling could hide it -- whereas a test of the final color
        // would happily pass with all three curves wrong by the same factor.
        (double red, double green, double blue) = SpectralColor.MatchingTotals;

        Assert.AreEqual(green, red, green * 0.02, $"the totals came to {red}, {green}, {blue}");
        Assert.AreEqual(green, blue, green * 0.02, $"the totals came to {red}, {green}, {blue}");
    }

    [TestMethod]
    public void TestEvenLightIsWarmBeforeItIsBalanced()
    {
        // The strongest check in here, because it pins the matching functions and the matrix that
        // carries them to a screen's primaries at the same time, against a figure neither of them
        // could have been fitted to: an even spectrum is the equal energy illuminant, and its
        // published reading in these primaries is (1.2048, 0.9484, 0.9087).
        //
        // It is also what the balancing is for, stated as a number so that changing it has to be
        // deliberate.  A screen's white stands for daylight, which is bluer than an even spectrum, so
        // an even spectrum really is warm; the balance is a choice and not the correction of an error.
        Color raw = SpectralColor.ToUnbalancedColor(Even());

        Assert.AreEqual(1.2048, raw.Red, 0.01, $"an even spectrum came back {raw}");
        Assert.AreEqual(0.9484, raw.Green, 0.01, $"an even spectrum came back {raw}");
        Assert.AreEqual(0.9087, raw.Blue, 0.01, $"an even spectrum came back {raw}");
    }

    [TestMethod]
    public void TestOneBandOfLightLooksLikeThatWavelength()
    {
        // Light at one wavelength alone must come back the color that wavelength is.  This is what
        // would catch the matching functions being handed to the wrong primaries -- a mistake that
        // leaves even light perfectly white and turns every real spectrum inside out.
        Color blue = OneBandNear(450);
        Color green = OneBandNear(550);
        Color red = OneBandNear(650);

        Assert.IsTrue(blue.Blue > blue.Red && blue.Blue > blue.Green, $"450nm came back {blue}");
        Assert.IsTrue(green.Green > green.Red && green.Green > green.Blue, $"550nm came back {green}");
        Assert.IsTrue(red.Red > red.Green && red.Red > red.Blue, $"650nm came back {red}");
    }

    [TestMethod]
    public void TestTheEyeIsBrightestInTheGreen()
    {
        // The middle matching function peaks at 555 nanometers, which is why a green laser looks so
        // much brighter than a red one of the same power.  Since brightness here is that same
        // function, one band of light must be at its brightest there.
        double brightest = 0;
        double whereBrightest = 0;

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            Color seen = OneBand(band);
            double brightness =
                0.2126 * seen.Red + 0.7152 * seen.Green + 0.0722 * seen.Blue;

            if (brightness > brightest)
            {
                brightest = brightness;
                whereBrightest = SpectralColor.WavelengthOf(band);
            }
        }

        // Within half a band of 555, which is as close as banded light can land on it.
        Assert.AreEqual(555, whereBrightest,
            (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / SpectralColor.Bands,
            $"the brightest band was at {whereBrightest}nm");
    }

    [TestMethod]
    public void TestTheBandsCoverTheRangeWithoutGapsOrOverlap()
    {
        double width = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / SpectralColor.Bands;

        Assert.AreEqual(SpectralColor.ShortestWavelength + width / 2, SpectralColor.WavelengthOf(0), 1e-12);
        Assert.AreEqual(SpectralColor.LongestWavelength - width / 2,
            SpectralColor.WavelengthOf(SpectralColor.Bands - 1), 1e-12);

        // Every step is the same size as every other, so no part of the range is weighed twice.
        for (int band = 1; band < SpectralColor.Bands; band++)
        {
            Assert.AreEqual(width,
                SpectralColor.WavelengthOf(band) - SpectralColor.WavelengthOf(band - 1), 1e-12);
        }
    }

    [TestMethod]
    public void TestABadlySizedSpectrumIsRefused()
    {
        Assert.ThrowsExactly<ArgumentException>(() => SpectralColor.ToColor(new double[4]));
    }

    /// <summary>
    /// Builds a spectrum with light in one band only.
    /// </summary>
    private static Color OneBand(int band)
    {
        double[] bands = new double[SpectralColor.Bands];

        bands[band] = 1;

        return SpectralColor.ToColor(bands);
    }

    /// <summary>
    /// Builds a spectrum with light only in the band nearest the given wavelength.
    /// </summary>
    private static Color OneBandNear(double wavelength)
    {
        int closest = 0;

        for (int band = 1; band < SpectralColor.Bands; band++)
        {
            if (Math.Abs(SpectralColor.WavelengthOf(band) - wavelength) <
                Math.Abs(SpectralColor.WavelengthOf(closest) - wavelength))
                closest = band;
        }

        return OneBand(closest);
    }
}
