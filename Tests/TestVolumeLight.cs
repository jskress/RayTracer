using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;

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
