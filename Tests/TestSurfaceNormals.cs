using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Patterns;

namespace Tests;

/// <summary>
/// These tests cover roughening a surface: tilting its normal from point to point by the slope of
/// a pattern, so that a wall reads as stucco without gaining a single triangle.
/// </summary>
[TestClass]
public class TestSurfaceNormals
{
    /// <summary>
    /// A pattern that climbs steadily along X and does nothing along Y or Z, so that its slope is
    /// known exactly and the arithmetic can be checked rather than eyeballed.
    /// </summary>
    private class RampPattern : Pattern
    {
        public override int DiscretePigmentsNeeded => 0;

        public override double Evaluate(Point point) => point.X;
    }

    /// <summary>
    /// Returns how far, in degrees, one vector was tilted from another.
    /// </summary>
    private static double DegreesBetween(Vector left, Vector right) =>
        Math.Acos(Math.Clamp(left.Unit.Dot(right.Unit), -1, 1)) * 180 / Math.PI;

    [TestMethod]
    public void TestAFlatPatternLeavesTheNormalAlone()
    {
        // Nothing to slope down, so nothing to tilt toward.
        SurfaceNormal normal = new () { Pattern = new PlanarPattern(), Depth = 1 };
        Vector was = new (0, 1, 0);

        Assert.AreEqual(0, DegreesBetween(was, normal.PerturbAt(was, new Point(0, 0, 0))), 1e-9);
    }

    [TestMethod]
    public void TestNoPatternLeavesTheNormalAlone()
    {
        SurfaceNormal normal = new () { Depth = 1 };
        Vector was = new (0, 1, 0);

        Assert.AreSame(was, normal.PerturbAt(was, new Point(0.3, 0.4, 0.5)));
    }

    [TestMethod]
    public void TestNoDepthLeavesTheNormalAlone()
    {
        SurfaceNormal normal = new () { Pattern = new RampPattern(), Depth = 0 };
        Vector was = new (0, 1, 0);

        Assert.AreSame(was, normal.PerturbAt(was, new Point(0.3, 0.4, 0.5)));
    }

    [TestMethod]
    public void TestTheNormalTiltsAgainstTheSlope()
    {
        // The pattern climbs along X, so the surface tilts back the other way, which is what makes
        // the near side of a rise catch the light and the far side lose it.
        SurfaceNormal normal = new () { Pattern = new RampPattern(), Depth = 1 };
        Vector tilted = normal.PerturbAt(new Vector(0, 1, 0), new Point(0.3, 0.4, 0.5));

        Assert.IsTrue(tilted.X < 0, "the normal should lean away from the climb");
        Assert.AreEqual(1, tilted.Magnitude, 1e-9, "the normal should still be a unit vector");
    }

    [TestMethod]
    public void TestDeeperTiltsFurther()
    {
        Vector was = new (0, 1, 0);
        Point point = new (0.3, 0.4, 0.5);
        double gentle = DegreesBetween(was, new SurfaceNormal
        {
            Pattern = new RampPattern(), Depth = 0.5
        }.PerturbAt(was, point));
        double steep = DegreesBetween(was, new SurfaceNormal
        {
            Pattern = new RampPattern(), Depth = 4
        }.PerturbAt(was, point));

        Assert.IsTrue(steep > gentle, $"depth 4 tilted {steep:F2} degrees, depth 0.5 tilted {gentle:F2}");
    }

    [TestMethod]
    public void TestASlopeStraightThroughTheSurfaceIsIgnored()
    {
        // The pattern climbs along X and the surface faces along X, so the whole slope points
        // through the skin rather than across it.  There is no tilt to be had from that.
        SurfaceNormal normal = new () { Pattern = new RampPattern(), Depth = 1 };
        Vector was = new (1, 0, 0);

        Assert.AreEqual(0, DegreesBetween(was, normal.PerturbAt(was, new Point(0.3, 0, 0))), 1e-9);
    }

    [TestMethod]
    public void TestScalingThePatternDoesNotChangeHowDeepItReads()
    {
        // This is POV-Ray's arrangement rather than a landscape's, and it is the one an author
        // wants: the scale says how fine the bumps are, and the depth says how deep, neither
        // disturbing the other.  Squeezing a real hill into half the room would double its slope.
        // How far any one point tilts says nothing on its own -- at each scale the pattern is being
        // read somewhere else entirely, so one point may sit on a slope at one scale and on the
        // flat at another.  What has to hold is the average over a surface's worth of points.
        Vector was = new (0, 1, 0);
        List<double> averages = [];

        foreach (double scale in new[] { 1.0, 0.5, 0.1 })
        {
            SurfaceNormal normal = new ()
            {
                Pattern = new GranitePattern(),
                Depth = 1,
                Transform = Transforms.Scale(scale)
            };
            List<double> tilts = [];

            for (int x = 0; x < 20; x++)
            {
                for (int z = 0; z < 20; z++)
                {
                    Point point = new (x * 0.11, 0, z * 0.13);

                    tilts.Add(DegreesBetween(was, normal.PerturbAt(was, point)));
                }
            }

            averages.Add(tilts.Average());
        }

        Assert.IsTrue(averages.Max() < averages.Min() * 1.5,
            "the roughening should hold its strength as the pattern is scaled, but averaged " +
            $"{string.Join(", ", averages.Select(tilt => $"{tilt:F1}"))} degrees");
    }

    [TestMethod]
    public void TestASurfaceIsRoughenedWhereItsMaterialSaysSo()
    {
        // The whole point, checked through the surface rather than the class: a sphere whose
        // material carries a roughening reports a different normal than one whose does not.
        Sphere plain = new ();
        Sphere rough = new ()
        {
            Material = new Material
            {
                SurfaceNormal = new SurfaceNormal { Pattern = new GranitePattern(), Depth = 3 }
            }
        };
        Point point = new Point(0, 0, -1);

        Assert.IsTrue(
            DegreesBetween(plain.NormaAt(point, null), rough.NormaAt(point, null)) > 1,
            "the material's roughening did not reach the surface");
    }
}
