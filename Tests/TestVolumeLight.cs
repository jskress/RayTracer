using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover the light a volume of glowing stuff casts on what is around it.
/// <para>
/// The energy here was derived rather than tuned, so what these check is the derivation.  The one that
/// matters is the first: a small glowing ball and a lamp carrying the same power must light a distant
/// surface to the same brightness.  Get the π wrong, or the density integral, and that comparison is
/// off by a clean factor -- which is exactly how a missing π was caught once before in the sky work.
/// </para>
/// </summary>
[TestClass]
public class TestVolumeLight
{
    /// <summary>
    /// Builds a small sphere filled with glowing stuff of the given emission, and the volume light
    /// that goes with it.
    /// </summary>
    private static (VolumeLight Light, double Stuff) GlowingBall(double radius, Color emission)
    {
        Medium medium = new () { Emission = emission };
        Sphere ball = new () { Transform = Transforms.Scale(radius) };

        ball.PrepareForRendering();

        VolumeLight light = new (ball, medium) { Samples = 400 };

        // What the derivation calls "the stuff": the density integral in world units.  For a uniform
        // density of one it is the volume of the bounding box, which for a sphere of this radius is
        // the cube around it rather than the ball inside it.
        return (light, 8 * radius * radius * radius);
    }

    [TestMethod]
    public void TestAGlowingVolumeCarriesTheEnergyTheMathsSays()
    {
        // A volume of emission j and size V, seen from far enough away that it is effectively a point,
        // delivers an irradiance of j·V/r².  This renderer's shading is pigment × color × cosine, and a
        // Lambertian surface under irradiance E gives back E/π -- so the color a sample carries must be
        // j·V/(π r²).  That is what is checked, against the arithmetic written out here rather than
        // against the code's own formula.
        const double Radius = 0.1;
        const double Distance = 20;

        (VolumeLight light, double stuff) = GlowingBall(Radius, new Color(2, 2, 2));
        Point far = new (0, 0, -Distance);
        Color total = Colors.Black;

        for (int index = 0; index < light.SampleCount; index++)
            total += light.ColorFor(light.SampleToward(far, index));

        // The samples are averaged by the scene, so the average is what one of them is worth.
        Color average = total * (1.0 / light.SampleCount);
        double expected = 2 * stuff / (Math.PI * Distance * Distance);

        Assert.AreEqual(expected, average.Red, expected * 0.02,
            $"a volume of emission 2 and size {stuff:F4} at {Distance} away should carry " +
            $"{expected:F8}, and carried {average.Red:F8}");
    }

    /// <summary>
    /// A glow's light is the stuff there is in the world: the same ball held in a group scaled to half carries an
    /// eighth of the light, as the glow it shows does, not the full-size light its own transform alone would say.
    /// </summary>
    [TestMethod]
    public void TestAGlowInAScaledGroupGivesItsScaledLight()
    {
        Medium medium = new () { Emission = new Color(2, 2, 2) };
        Sphere loose = new () { Transform = Transforms.Scale(0.1) };
        Sphere held = new () { Transform = Transforms.Scale(0.1) };
        Group half = new () { Transform = Transforms.Scale(0.5) };

        half.Add(held);
        loose.PrepareForRendering();
        half.PrepareForRendering();

        Point far = new (0, 0, -20);
        VolumeLight whole = new (loose, medium) { Samples = 400 };
        VolumeLight halved = new (held, medium) { Samples = 400 };
        double Total(VolumeLight light) => Enumerable.Range(0, light.SampleCount)
            .Sum(index => light.ColorFor(light.SampleToward(far, index)).Red);

        Assert.AreEqual(1.0 / 8, Total(halved) / Total(whole), 0.01, "a ball half the size gives an eighth of the light");
    }

    /// <summary>
    /// A smooth surface's highlight from a glow is the glow's own light: the color it glows, fading with distance as
    /// the rest of its light does.  Taken from the light's color as a whole, it was white whatever the glow, and as
    /// bright from twenty away as from ten -- a fire put a gray sheen on a polished floor across the room.
    /// </summary>
    [TestMethod]
    public void TestAGlowsHighlightIsTheGlowsLight()
    {
        (VolumeLight light, _) = GlowingBall(0.1, new Color(2, 0, 0));
        Plane floor = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 1, Shininess = 10
            }
        };
        Vector up = new (0, 1, 0);

        floor.PrepareForRendering();

        // Straight below the glow, the eye straight above: the glow's mirror image square in the eye.
        Color Highlight(double below) => light.ApplyPhong(new Point(0, -below, 0), up, up, floor, Colors.White);

        Color near = Highlight(10);
        Color far = Highlight(20);

        Assert.IsTrue(near.Red > 0, "a glow should put a highlight on a smooth surface");
        Assert.AreEqual(0, near.Green, 1e-12, $"a red glow's highlight should be red, and was {near}");
        Assert.AreEqual(4, near.Red / far.Red, 0.05, "a glow's highlight should fade with the square of the distance");
    }

    [TestMethod]
    public void TestTwiceTheEmissionIsTwiceTheLight()
    {
        (VolumeLight dim, _) = GlowingBall(0.1, new Color(1, 1, 1));
        (VolumeLight bright, _) = GlowingBall(0.1, new Color(2, 2, 2));
        Point far = new (0, 0, -20);

        double one = dim.ColorFor(dim.SampleToward(far, 0)).Red;
        double two = bright.ColorFor(bright.SampleToward(far, 0)).Red;

        Assert.AreEqual(2, two / one, 0.001, "doubling the emission should double the light");
    }

    [TestMethod]
    public void TestTwiceAsFarIsAQuarterTheLight()
    {
        // The inverse square, which lives in this light and nowhere else -- which is why one of these
        // must never also be given a fade distance.
        //
        // Measured against the distances the samples actually report rather than against the two the
        // points were placed at.  The sample sits somewhere *inside* the ball, so it is not exactly ten
        // and twenty away, and a first version of this asked for a ratio of exactly four and failed on
        // the ball's own radius -- a test wrong about the geometry, not a renderer wrong about light.
        (VolumeLight light, _) = GlowingBall(0.1, new Color(2, 2, 2));

        LightSample near = light.SampleToward(new Point(0, 0, -10), 0);
        LightSample far = light.SampleToward(new Point(0, 0, -20), 0);

        double ratio = light.ColorFor(near).Red / light.ColorFor(far).Red;
        double expected = far.Distance * far.Distance / (near.Distance * near.Distance);

        Assert.AreEqual(expected, ratio, expected * 0.001,
            $"light should fall as the square of the distance, and went {ratio:F4} against {expected:F4}");
    }

    /// <summary>
    /// What a glow's samples weigh against a rough surface's looking, in sharing the glow between them, does not
    /// depend on the unit a scene is written in: the same glow ten times the size, seen from ten times as far, weighs
    /// the same.  (The odds of picking a place, per unit of volume, were weighed against the odds of a direction with
    /// no length between them, so the split moved with the unit.)
    /// </summary>
    [TestMethod]
    public void TestAGlowsWeightDoesNotDependOnTheUnit()
    {
        Medium glowing = new () { Emission = new Color(2, 2, 2) };
        Sphere small = new ();
        Sphere large = new () { Transform = Transforms.Scale(10, 10, 10) };

        small.PrepareForRendering();
        large.PrepareForRendering();

        VolumeLight near = new (small, glowing);
        VolumeLight far = new (large, glowing);
        double weight = near.WeightOf(near.OddsAt(new Point(0.1, 0.2, 0.3)), 3);
        double scaled = far.WeightOf(far.OddsAt(new Point(1, 2, 3)), 30);

        Assert.IsTrue(weight > 0);
        Assert.AreEqual(weight, scaled, weight * 1e-9, "the same glow ten times the size weighed differently");
    }

    [TestMethod]
    public void TestStuffThatGlowsNowhereIsNotALight()
    {
        // A medium whose density comes out at nought everywhere has nothing to light with, and saying
        // so is what keeps it from being added to a scene as a light that does nothing but cost.
        Medium empty = new () { Emission = new Color(2, 2, 2), Density = 0 };
        Sphere ball = new ();

        ball.PrepareForRendering();

        Assert.IsFalse(new VolumeLight(ball, empty).Lights);
    }
}
