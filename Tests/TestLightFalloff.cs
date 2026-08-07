using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover light thinning with the distance it has travelled.
/// <para>
/// It is the one thing a candle is made of, and until now nothing here did it: a light of one made a
/// white surface come back at one whether it stood a foot off or a hundred.  What matters most in
/// these is the arithmetic being the world's rather than a pleasing curve, and that a scene which
/// never mentions it is left exactly as it was.
/// </para>
/// </summary>
[TestClass]
public class TestLightFalloff
{
    [TestMethod]
    public void TestALightSaysNothingUntilItIsAsked()
    {
        // The whole of the backward compatibility, in one line: with no fading distance set, every
        // distance is worth the same, which is what every scene written before this expects.
        PointLight plain = new ();

        Assert.IsNull(plain.FadeDistance);

        foreach (double distance in new[] { 0.001, 1, 10, 1000, double.PositiveInfinity })
            Assert.AreEqual(1, plain.FadingOver(distance), $"at {distance}");
    }

    [TestMethod]
    public void TestPastTheStatedDistanceItThinsAsTheWorldDoes()
    {
        // Light spreads over a sphere that grows as it goes, so what falls on a patch at twice the
        // distance is a quarter as much.  That is the default and it is not a taste.
        PointLight candle = new () { FadeDistance = 2 };

        Assert.AreEqual(1, candle.FadingOver(2), 1e-12, "at the stated distance it is worth its word");
        Assert.AreEqual(0.25, candle.FadingOver(4), 1e-12, "twice as far is a quarter as much");
        Assert.AreEqual(1.0 / 9, candle.FadingOver(6), 1e-12, "three times as far, a ninth");
        Assert.AreEqual(0.01, candle.FadingOver(20), 1e-12, "ten times as far, a hundredth");
    }

    [TestMethod]
    public void TestNearerThanThatItIsLeftAlone()
    {
        // The true law runs to infinity at no distance at all, and a real flame is not a point in any
        // case.  So the stated distance names where the light is worth its word, and nearer than that
        // it is simply left there rather than allowed to grow without bound.
        PointLight candle = new () { FadeDistance = 2 };

        Assert.AreEqual(1, candle.FadingOver(2));
        Assert.AreEqual(1, candle.FadingOver(0.5));
        Assert.AreEqual(1, candle.FadingOver(0));
    }

    [TestMethod]
    public void TestTheRateMayBeChangedForALookRatherThanTheTruth()
    {
        PointLight gentle = new () { FadeDistance = 2, FadePower = 1 };

        Assert.AreEqual(0.5, gentle.FadingOver(4), 1e-12, "dimming by distance rather than its square");

        PointLight undimmed = new () { FadeDistance = 2, FadePower = 0 };

        Assert.AreEqual(1, undimmed.FadingOver(400), 1e-12, "and no power at all leaves it undimmed");
    }

    [TestMethod]
    public void TestNothingInfinitelyFarOffEverFades()
    {
        // A sun and a sky are so far away that nothing in a scene is meaningfully nearer to them than
        // anything else, so there is nothing for a distance to measure against.  Their samples come
        // back at an infinite distance, and that must be left alone rather than falling to nothing --
        // which is what would happen if the arithmetic were simply applied.
        DistantLight sun = new () { FadeDistance = 2 };
        SkyLight sky = new () { FadeDistance = 2 };

        Assert.AreEqual(1, sun.FadingOver(double.PositiveInfinity));
        Assert.AreEqual(1, sky.FadingOver(double.PositiveInfinity));
    }

    [TestMethod]
    public void TestASurfaceFurtherOffIsReallyDarker()
    {
        // The arithmetic above arriving where it is meant to: the same ball at two distances from the
        // same lamp, shaded through the renderer proper rather than by asking the light directly.
        // The lamp stands on the camera's side of it, so that the face being measured is the lit one.
        PointLight lamp = new ()
        {
            Location = new Point(0, 0, 10), Color = Colors.White, FadeDistance = 2
        };

        double Lit(double ballCentre)
        {
            Sphere ball = new ()
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Specular = 0
                },
                Transform = Transforms.Translate(0, 0, ballCentre)
            };

            ball.PrepareForRendering();

            Scene scene = new () { Background = new SolidPigment(Colors.Black) };

            scene.Lights.Add(lamp);
            scene.Surfaces.Add(ball);

            Ray ray = new (new Point(0, 0, 12), new Vector(0, 0, -1));
            List<Intersection> hits = scene.Intersect(ray);
            Intersection hit = hits.Hit();

            Assert.IsNotNull(hit, $"the ray missed the ball at {ballCentre}");

            hit.PrepareUsing(ray, hits);

            return scene.GetHitColor(hit, 1).Red;
        }

        // The near ball's lit face stands 9 from the lamp and the far one's 18, so the far one must
        // come back a quarter as bright -- both squarely lit and otherwise identical.
        double near = Lit(0);
        double far = Lit(-9);

        Assert.IsTrue(near > 0, $"the near ball should be lit, and gave {near}");
        Assert.AreEqual(0.25, far / near, 0.02,
            $"twice as far should be a quarter as bright: {far} against {near}");
    }
}
