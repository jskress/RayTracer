using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover the color the air actually comes out, which is where the model stops being
/// arithmetic and starts being a sky.
/// </summary>
[TestClass]
public class TestSkyRadiance
{
    private static Color LookingAt(Vector view, Vector sun, double turbidity = 2.5)
    {
        Atmosphere air = new () { Turbidity = turbidity };

        return SpectralColor.ToColor(air.RadianceToward(view.Unit, sun.Unit, 0));
    }

    [TestMethod]
    public void TestTheSkyOverheadIsBlue()
    {
        Color zenith = LookingAt(new Vector(0, 1, 0), new Vector(0.3, 0.8, 0));

        Assert.IsTrue(zenith.Blue > zenith.Green && zenith.Green > zenith.Red,
            $"the zenith came out {zenith}");
        Assert.IsTrue(zenith.Blue > 0, $"the zenith came out {zenith}");
    }

    [TestMethod]
    public void TestTheHorizonIsPalerThanTheZenith()
    {
        Vector sun = new (0.3, 0.8, 0);
        Color zenith = LookingAt(new Vector(0, 1, 0), sun);
        Color horizon = LookingAt(new Vector(0, 0.02, 1), sun);
        double zenithSpread = zenith.Blue / zenith.Red;
        double horizonSpread = horizon.Blue / horizon.Red;

        Assert.IsTrue(horizonSpread < zenithSpread,
            $"the horizon should be the paler: {horizonSpread} against {zenithSpread}");
    }

    [TestMethod]
    public void TestALowSunGoesRed()
    {
        Atmosphere air = new ();
        Color high = SpectralColor.ToColor(air.SunlightAfterAir(new Vector(0, 1, 0).Unit, 0));
        Color low = SpectralColor.ToColor(air.SunlightAfterAir(new Vector(0, 0.03, 1).Unit, 0));

        // Stated as an ordering rather than as a ratio, because a sunset this deep is a color a
        // screen cannot actually make: converted honestly it wants a negative amount of blue, and a
        // ratio against that would say nothing.  What can be said is that the light is overwhelmingly
        // red, that almost no blue is left, and that it has dimmed hard on the way in.
        Assert.IsTrue(low.Red > low.Green && low.Green > low.Blue,
            $"a low sun should be strongly red: {low}");
        Assert.IsTrue(low.Blue / low.Red < 0.2 && high.Blue / high.Red > 0.7,
            $"the blue should be nearly gone: {low} against {high}");
        Assert.IsTrue(low.Red < high.Red * 0.5, $"and dim as well: {low} against {high}");
    }

    [TestMethod]
    public void TestTheSunDimsByWhatTheAirTakes()
    {
        // Straight overhead, sunlight should lose about what a vertical column takes out -- which is
        // the measured number the coefficients were checked against, now arriving through the march.
        Atmosphere air = new () { Turbidity = 1 };
        double[] above = Atmosphere.SunlightPerBand();
        double[] below = air.SunlightAfterAir(new Vector(0, 1, 0), 0);
        int green = 0;

        for (int band = 1; band < SpectralColor.Bands; band++)
        {
            if (Math.Abs(SpectralColor.WavelengthOf(band) - 550) <
                Math.Abs(SpectralColor.WavelengthOf(green) - 550))
                green = band;
        }

        double survived = below[green] / above[green];
        double expected = Math.Exp(-Atmosphere.VerticalAirDepthAt(SpectralColor.WavelengthOf(green)));

        Assert.AreEqual(expected, survived, 0.005,
            $"the march lost {1 - survived} where the column says {1 - expected}");
    }

    [TestMethod]
    public void TestHazeWashesTheColorOut()
    {
        Vector view = new (0, 0.15, 1);
        Vector sun = new (0.3, 0.8, 0);
        Color clear = LookingAt(view, sun, 2);
        Color murky = LookingAt(view, sun, 8);

        Assert.IsTrue(murky.Blue / murky.Red < clear.Blue / clear.Red,
            $"haze should wash out the blue: {murky} against {clear}");
    }

    [TestMethod]
    public void TestTheGroundIsInTheWayLookingDown()
    {
        // From sea level, every downward look meets the ground at once, so there is no air in front
        // of it at all and the answer is exactly nothing rather than merely a little.
        //
        // This test used to allow "a little", and that let a real fault through: a ray starting exactly
        // on the ground and pointing down was reckoned to miss the planet, and marched twelve thousand
        // kilometres through it.  What made the answer look reasonable anyway was that the rock counted
        // as sea level air, which swallowed the light almost at once.  A test that asks for the right
        // answer rather than a plausible one would have caught it on the first run.
        Vector sun = new (0.3, 0.8, 0);

        foreach (double down in new[] { -90.0, -45, -10, -1 })
        {
            double angle = down * Math.PI / 180;
            Vector view = new (Math.Cos(angle), Math.Sin(angle), 0);
            Color seen = LookingAt(view, sun);

            Assert.AreEqual(0, seen.Red, 1e-9, $"looking {down} degrees down gave {seen}");
            Assert.AreEqual(0, seen.Green, 1e-9, $"looking {down} degrees down gave {seen}");
            Assert.AreEqual(0, seen.Blue, 1e-9, $"looking {down} degrees down gave {seen}");
        }

        // And looking level or up, there is air ahead and the sky is not nothing.
        Assert.IsTrue(LookingAt(new Vector(1, 0.001, 0), sun).Blue > 0,
            "a level look should still find air ahead of it");
    }
}
