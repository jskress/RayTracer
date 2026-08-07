using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover light that has been turned aside more than once before reaching the eye.
/// <para>
/// The thing worth testing here is not that it makes the sky brighter -- adding a positive quantity to
/// another will do that -- but that it does the things only multiply turned light can do, and by about
/// the amount it should.  Chief among those is reaching places the sun cannot see.
/// </para>
/// </summary>
[TestClass]
public class TestMultipleScattering
{
    private static double Brightness(Color color) =>
        0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue;

    private static readonly MultipleScattering Bounced = new (2.5, 16, 16, 16);

    [TestMethod]
    public void TestItReachesWhereTheSunCannotSee()
    {
        // The one thing single scattering cannot do at all, and the real reason to have this quite
        // apart from any brightening.
        //
        // Where that line falls is worth knowing, and it is not where it first seems.  With the sun
        // just below the horizon the sky is still lit without any of this, because a point twenty
        // miles up has a lower horizon than you do and can still see the sun -- which is what
        // twilight is.  Only when the sun is far enough down that *nothing* along the view can see it
        // does the once-turned light stop entirely, and there it stops dead rather than fading.  Ten
        // degrees under is past that line.
        Vector view = new Vector(0.5, 0.3, 0).Unit;
        Atmosphere single = new ();
        Atmosphere bounced = new () { Bounced = Bounced };

        Vector wellDown = new Vector(0.985, -0.174, 0).Unit;
        double nothing = Brightness(SpectralColor.ToColor(single.RadianceToward(view, wellDown, 0)));
        double glow = Brightness(SpectralColor.ToColor(bounced.RadianceToward(view, wellDown, 0)));

        Assert.AreEqual(0, nothing, 1e-12,
            "with the sun ten degrees down nothing along the view can see it, so the once-turned " +
            $"light must be exactly nothing, and gave {nothing}");
        Assert.IsTrue(glow > 0, $"and the later turns should still leave a glow, but gave {glow}");

        // Just under the horizon, where the upper air is still lit, both are lit and the later turns
        // add to what is there rather than being all of it.
        Vector justDown = new Vector(0.9998, -0.0175, 0).Unit;
        double early = Brightness(SpectralColor.ToColor(single.RadianceToward(view, justDown, 0)));
        double later = Brightness(SpectralColor.ToColor(bounced.RadianceToward(view, justDown, 0)));

        Assert.IsTrue(early > 0, "just under the horizon the upper air is still in sunlight");
        Assert.IsTrue(later > early * 1.1, $"and the later turns add to it: {later} against {early}");
    }

    [TestMethod]
    public void TestItAddsLightEverywhereRatherThanTakingItAway()
    {
        Vector sun = new Vector(0.5, 0.6, 0).Unit;
        Atmosphere single = new ();
        Atmosphere bounced = new () { Bounced = Bounced };

        foreach (double up in new[] { 88.0, 60, 30, 5 })
        {
            double angle = up * Math.PI / 180;
            Vector view = new Vector(Math.Cos(angle), Math.Sin(angle), 0).Unit;
            double once = Brightness(SpectralColor.ToColor(single.RadianceToward(view, sun, 0)));
            double again = Brightness(SpectralColor.ToColor(bounced.RadianceToward(view, sun, 0)));

            Assert.IsTrue(again > once, $"looking {up} degrees up gave {again} against {once}");
        }
    }

    [TestMethod]
    public void TestItIsASmallCorrectionRatherThanTheBulkOfTheLight()
    {
        // Worth pinning, because it was once assumed to be the bulk of it and it is not.  In air this
        // thin -- a vertical column takes out about a tenth of what crosses it -- the second turn can
        // only be a small share of the first, and a model where it were not would be wrong.  If this
        // ever grows past a half, something has run away.
        Vector sun = new Vector(0.5, 0.6, 0).Unit;
        Vector view = new Vector(0.3, 0.9, 0).Unit;
        double once = Brightness(SpectralColor.ToColor(new Atmosphere().RadianceToward(view, sun, 0)));
        double again = Brightness(SpectralColor.ToColor(
            new Atmosphere { Bounced = Bounced }.RadianceToward(view, sun, 0)));
        double added = (again - once) / once;

        Assert.IsTrue(added is > 0.02 and < 0.5,
            $"the later turns added {100 * added:F0}%, which is not the size this should be");
    }

    [TestMethod]
    public void TestThickerAirBouncesMoreOfIt()
    {
        // More air to turn the light means more of it turned again, and the *amount* added does grow
        // with haze.  The *share* it adds does not, and that is worth recording rather than asserting
        // the other way round as I first did: haze throws light hard forward, so it swells the
        // once-turned light near the sun far more than it swells the evenly spread light that has been
        // turned again -- and haze swallows a tenth of what it touches besides.
        Vector sun = new Vector(0.5, 0.6, 0).Unit;
        Vector view = new Vector(0.3, 0.9, 0).Unit;

        double AddedAt(double turbidity)
        {
            MultipleScattering bounced = new (turbidity, 16, 16, 16);
            double once = Brightness(SpectralColor.ToColor(
                new Atmosphere { Turbidity = turbidity }.RadianceToward(view, sun, 0)));
            double again = Brightness(SpectralColor.ToColor(
                new Atmosphere { Turbidity = turbidity, Bounced = bounced }
                    .RadianceToward(view, sun, 0)));

            return again - once;
        }

        Assert.IsTrue(AddedAt(6) > AddedAt(1.5),
            "thicker air should send more light round a second time");
    }

    [TestMethod]
    public void TestTheSunIsHandedOverAsBrightnessRatherThanAsFallingLight()
    {
        // The correction that mattered more than any of the physics above, and the one most easily got
        // wrong again, so it is pinned here.
        //
        // A sun's color is how much light falls on a surface; a sky's is how bright it looks.  A
        // surface facing light of strength E does not glow at E -- it spreads what it caught over
        // every direction and glows at E over pi -- and this renderer has no such division in its
        // shading.  So the sun must be handed over already divided, or it stands pi times too bright
        // against its own sky, which is exactly what it did.
        PhysicalSkyPigment sky = new () { SunElevation = 45 };
        Color asALight = sky.SunAsALight().Color;
        Color falling = SpectralColor.ToColor(
            new Atmosphere { Turbidity = sky.Turbidity }.SunlightAfterAir(sky.TowardSun, 0));

        Assert.AreEqual(falling.Red / Math.PI, asALight.Red, 1e-9,
            $"the sun was handed over as {asALight} where {falling} falls");

        // And the exposure carries through it, so that moving one moves both together.
        PhysicalSkyPigment brighter = new () { SunElevation = 45, Brightness = 3 };

        Assert.AreEqual(asALight.Red * 3, brighter.SunAsALight().Color.Red, 1e-9);
    }

    [TestMethod]
    public void TestTheDiffuseShareOfDaylightIsAboutWhatIsMeasured()
    {
        // What the whole model is finally worth, said as the quantity meteorologists actually record:
        // of all the light falling on a level surface outdoors, how much of it arrives from the sky
        // rather than straight from the sun.  It is the right check because it is a ratio of two things
        // the model computes independently, so it cannot be satisfied by scaling anything, and because
        // it has been measured everywhere for a century.  A clear day runs about a tenth with the sun
        // high, and the share climbs as the sun drops and the direct beam has more air to cross.
        //
        // It replaced a check against a remembered figure for how bright a zenith is against a sunlit
        // white card.  That figure turned out to be shakier than the model it was judging, which is a
        // poor way round for a test to be.
        double previous = 0;

        foreach (double elevation in new[] { 75.0, 45, 20 })
        {
            double up = elevation * Math.PI / 180;
            Vector sun = new Vector(Math.Cos(up), Math.Sin(up), 0).Unit;
            Atmosphere air = new () { Bounced = Bounced };
            double diffuse = 0;
            const int rings = 12, spokes = 24;

            for (int ring = 0; ring < rings; ring++)
            {
                // Even steps in the sine, so every sample stands for the same patch of sky.
                double sine = (ring + 0.5) / rings;
                double cosine = Math.Sqrt(1 - sine * sine);

                for (int spoke = 0; spoke < spokes; spoke++)
                {
                    double around = 2 * Math.PI * (spoke + 0.5) / spokes;
                    Vector view = new Vector(
                        cosine * Math.Cos(around), sine, cosine * Math.Sin(around)).Unit;

                    // Weighed by how squarely it meets the ground, which is what makes this the light
                    // actually falling rather than merely the light in the sky.
                    diffuse += Brightness(SpectralColor.ToColor(air.RadianceToward(view, sun, 0))) * sine;
                }
            }

            diffuse *= 2 * Math.PI / (rings * spokes);

            double direct = Brightness(SpectralColor.ToColor(air.SunlightAfterAir(sun, 0))) * Math.Sin(up);
            double share = diffuse / (diffuse + direct);

            Assert.IsTrue(share is > 0.04 and < 0.30,
                $"with the sun at {elevation} degrees the diffuse share came to {share:P1}");
            Assert.IsTrue(share > previous,
                $"the diffuse share should climb as the sun drops, and went {previous:P1} to {share:P1}");

            previous = share;
        }
    }
}
