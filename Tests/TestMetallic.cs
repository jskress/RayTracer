using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

[TestClass]
public class TestMetallic
{
    private static Material Gold(double metallic)
    {
        return new Material
        {
            Pigment = new SolidPigment(new Color(1, 0.8, 0.2)),
            Metallic = metallic
        };
    }

    [TestMethod]
    public void TestNoMetallicLeavesTheLightsColourAlone()
    {
        // A dielectric reflects the light's own colour, so the tint has to be a no-op.
        Color tint = Gold(0).GetMetallicTint(new Color(1, 0.8, 0.2), 1);

        Assert.IsTrue(tint.Matches(Colors.White), tint.ToString());
    }

    [TestMethod]
    public void TestFullMetallicTakesTheSurfaceColourHeadOn()
    {
        // Meeting the surface head on, the empirical Fresnel term is ~0, so a fully metallic
        // surface tints all the way to its own colour.
        Color surface = new (1, 0.8, 0.2);
        Color tint = Gold(1).GetMetallicTint(surface, 1);

        Assert.IsTrue(tint.Matches(surface), tint.ToString());
    }

    [TestMethod]
    public void TestTheTintFallsAwayAtGrazingAngles()
    {
        // At grazing incidence everything becomes a colourless mirror, metal included, so the
        // tint has to return to white however metallic the surface is.
        Color tint = Gold(1).GetMetallicTint(new Color(1, 0.8, 0.2), 0);

        Assert.IsTrue(tint.Matches(Colors.White), tint.ToString());
    }

    [TestMethod]
    public void TestTheTintIsStillNearlyFullWellOffTheNormal()
    {
        // The falloff is not linear in angle: the fitted curve stays close to zero across most of
        // the surface and only climbs near the silhouette.  At 45 degrees the tint should still be
        // most of the way to the surface's colour, not halfway.
        Color surface = new (1, 0, 0);
        Color tint = Gold(1).GetMetallicTint(surface, Math.Cos(Math.PI / 4));

        Assert.IsTrue(tint.Green < 0.05, $"Expected a nearly full tint, got {tint}.");
    }

    [TestMethod]
    public void TestAMetallicHighlightTakesTheSurfaceColour()
    {
        // The visible payoff: a white light on a gold sphere makes a gold highlight, where the
        // same light on a dielectric makes a white one.
        Sphere plain = new () { Material = Gold(0) };
        Sphere metal = new () { Material = Gold(1) };
        PointLight light = new () { Location = new Point(0, 0, -10) };
        Point point = new (0, 0, -1);
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);

        Color plainColor = light.ApplyPhong(point, eye, normal, plain, Colors.White);
        Color metalColor = light.ApplyPhong(point, eye, normal, metal, Colors.White);

        // Both are lit the same; only the highlight differs, and it drags the blue channel down
        // toward the gold's own small blue component.
        Assert.IsTrue(metalColor.Blue < plainColor.Blue,
            $"Expected the metallic highlight to be less blue: {metalColor} vs {plainColor}.");
        Assert.IsTrue(metalColor.Red.Near(plainColor.Red),
            $"Red is already full in the gold, so it should not change: {metalColor} vs {plainColor}.");
    }

    [TestMethod]
    public void TestMetallicIsIgnoredWithoutAHighlight()
    {
        // With no specular term there is no highlight to tint, so metallic must make no
        // difference at all -- the same rule POV-Ray applies.
        Material plain = Gold(0);
        Material metal = Gold(1);

        plain.Specular = metal.Specular = 0;

        Sphere plainSphere = new () { Material = plain };
        Sphere metalSphere = new () { Material = metal };
        PointLight light = new () { Location = new Point(0, 0, -10) };
        Point point = new (0, 0, -1);
        Vector eye = new (0, 0, -1);
        Vector normal = new (0, 0, -1);

        Color plainColor = light.ApplyPhong(point, eye, normal, plainSphere, Colors.White);
        Color metalColor = light.ApplyPhong(point, eye, normal, metalSphere, Colors.White);

        Assert.IsTrue(plainColor.Matches(metalColor), $"{plainColor} vs {metalColor}");
    }
}
