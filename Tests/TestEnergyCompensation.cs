using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// What a rough surface's facets mirror among themselves, which a single mirroring leaves out, given back: checked
/// in an even white light, where a surface whose facets mirror all of it must give all of it back, and against the
/// arithmetic the table is built from.
/// </summary>
[TestClass]
public class TestEnergyCompensation
{
    /// <summary>
    /// The albedo is one for a mirror, and for facets leaning as far as they can, seen square on, it is exactly
    /// 1 - ln 2: GGX's distribution is then even, 1/pi, and the masking of the light 2 cos / (1 + cos).
    /// </summary>
    [TestMethod]
    public void TestTheAlbedoIsExactWhereItCanBeWorkedOut()
    {
        Assert.AreEqual(1, Microfacets.Albedo(0.5, 0));
        Assert.AreEqual(1 - Math.Log(2), Microfacets.Albedo(1, 1), 1e-3 * (1 - Math.Log(2)));
        Assert.AreEqual(1, Microfacets.Compensation(0.5, 0, 1));
        Assert.AreEqual(RgbSpectrum.White, Microfacets.Compensation(0.5, 0, RgbSpectrum.White * 0.7));
    }

    /// <summary>
    /// The table, read between its steps, agrees with the albedo worked out another way: the highlight of a lamp of
    /// unit color, from every direction over the sky, added up -- the highlight being pi times what the facets give
    /// back toward the eye times the cosine the lamp's light arrives at.  The table is built by picking the facets
    /// the eye sees, so the two share only the facets themselves.
    /// </summary>
    [TestMethod]
    public void TestTheTableIsTheFacetsOwn()
    {
        foreach (double cos in new[] { 0.93, 0.61, 0.27 })
        {
            foreach (double roughness in new[] { 0.43, 0.71, 0.97 })
            {
                double alpha = roughness * roughness;
                double expected = OverTheSky(light => Microfacets.Highlight(Up, EyeAt(cos), light, alpha, _ => 1)) /
                                  Math.PI;

                Assert.AreEqual(
                    expected, Microfacets.Albedo(cos, alpha), 0.004 * expected,
                    $"at a cosine of {cos} and roughness {roughness}");
            }
        }
    }

    /// <summary>
    /// A rough mirror under an even white sky gives all of it back, at every roughness and seen from every angle,
    /// through its blurred reflection.  A single mirroring gave back 0.69 of it at roughness 0.7, seen square on,
    /// and 0.31 at roughness 1.
    /// </summary>
    [TestMethod]
    public void TestARoughMirrorKeepsAllOfAnEvenLight()
    {
        foreach (double roughness in new[] { 0.3, 0.6, 0.8, 1.0 })
        {
            foreach (double cos in new[] { 1, 0.7, 0.3 })
            {
                Material mirror = new () { Reflective = 1, Roughness = roughness, ReflectionSamples = 4096 };
                Scene scene = new () { Background = new SolidPigment(Colors.White) };
                Plane floor = new () { Material = mirror };

                scene.Surfaces.Add(floor);

                double seen = scene.GetReflectionColor(Hit(floor, cos), 2).Red;

                Assert.AreEqual(1, seen, 0.01, $"roughness {roughness}, seen at a cosine of {cos}");
            }
        }
    }

    /// <summary>
    /// A rough mirror's highlight of a lamp, the lamp put in every direction over the sky in turn, adds up to all
    /// of the lamp's light, as the sky's did: alike every way, and brushed, looked at across the grooves and along
    /// them.  Brushed facets made up for as though they leaned their mean every way kept 0.71 of it at roughness 1.
    /// </summary>
    [TestMethod]
    public void TestALampFromEveryWayIsKeptWhole()
    {
        foreach (double roughness in new[] { 0.4, 0.7, 1.0 })
        {
            foreach (double cos in new[] { 0.9, 0.5 })
            {
                double alpha = roughness * roughness;
                double compensation = Microfacets.Compensation(cos, alpha, 1);
                double whole = OverTheSky(light => Microfacets.Highlight(Up, EyeAt(cos), light, alpha, _ => 1)) /
                               Math.PI * compensation;

                Assert.AreEqual(1, whole, 0.005, $"alike every way, roughness {roughness}, at a cosine of {cos}");

                (double along, double across) = Facets.AlphasFor(roughness, 0.8);
                Facets brushed = new (Up, new Vector(1, 0, 0), along, across);

                foreach ((Vector eye, string way) in new[]
                         {
                             (EyeAt(cos), "across the grooves"),
                             (new Vector(-Math.Sqrt(1 - cos * cos), cos, 0), "along the grooves")
                         })
                {
                    double brushedWhole = OverTheSky(light => brushed.Highlight(eye, light, _ => 1)) / Math.PI *
                                          Microfacets.CompensationFor(brushed.AlbedoFor(brushed.ToLocal(eye)), 1);

                    Assert.AreEqual(
                        1, brushedWhole, 0.02, $"brushed, roughness {roughness}, at a cosine of {cos}, {way}");
                }
            }
        }
    }

    /// <summary>
    /// A rough metal gives back more of every band than a single mirroring did, and more of the bands it mirrors more
    /// of: light that goes round its facets is colored by the metal again at each one, so a rough gold is a deeper
    /// gold.  Under an even white sky, gold rough enough to be matte is redder against its blue than polished gold.
    /// </summary>
    [TestMethod]
    public void TestARoughMetalComesOutDeeper()
    {
        Metal gold = Metal.Named("gold");
        RgbSpectrum made = Microfacets.Compensation(1, 0.81, gold.ShareFromAround<RgbSpectrum>(1));
        Color factor = made.ToColor();

        Assert.IsTrue(factor.Red > factor.Green && factor.Green > factor.Blue && factor.Blue > 1, $"{factor}");

        Color polished = GoldUnderAWhiteSky(0.1);
        Color matte = GoldUnderAWhiteSky(0.9);

        Assert.IsGreaterThan(polished.Red / polished.Blue * 1.3, matte.Red / matte.Blue, $"{polished} then {matte}");
        Assert.AreEqual(polished.Red, matte.Red, 0.03, $"{polished} then {matte}");
    }

    // A gold floor of the given roughness under an even white sky, seen square on.
    private static Color GoldUnderAWhiteSky(double roughness)
    {
        Scene scene = new () { Background = new SolidPigment(Colors.White) };
        Plane floor = new ()
        {
            Material = new Material
            {
                Metal = Metal.Named("gold"), Metallic = 1, Roughness = roughness, Reflective = 0.04,
                ReflectionSamples = 4096
            }
        };

        scene.Surfaces.Add(floor);

        return scene.GetReflectionColor(Hit(floor, 1), 2);
    }

    private static readonly Vector Up = new (0, 1, 0);

    // The direction toward an eye whose ray comes down at the angle whose cosine is given.
    private static Vector EyeAt(double cos) => new (0, cos, -Math.Sqrt(1 - cos * cos));

    // The integral of a function of direction over the sky above the floor, on a grid fine enough for a lobe of
    // roughness 0.4: crowded toward the horizon, where the masking falls away fastest.
    private static double OverTheSky(Func<Vector, double> function)
    {
        const int rings = 768;
        const int spokes = 768;
        double total = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            double theta = (ring + 0.5) / rings * Math.PI / 2;
            double sin = Math.Sin(theta);
            double cos = Math.Cos(theta);

            for (int spoke = 0; spoke < spokes; spoke++)
            {
                double phi = (spoke + 0.5) / spokes * 2 * Math.PI;

                total += function(new Vector(sin * Math.Cos(phi), cos, sin * Math.Sin(phi))) * sin;
            }
        }

        return total * (Math.PI / 2 / rings) * (2 * Math.PI / spokes);
    }

    // Where a ray meets the floor's middle, coming down at the angle whose cosine is given, prepared for shading.
    private static Intersection Hit(Plane floor, double cos)
    {
        double sin = Math.Sqrt(1 - cos * cos);
        Ray ray = new (new Point(0, cos, -sin), new Vector(0, -cos, sin));
        Intersection intersection = new (floor, 1);

        intersection.PrepareUsing(ray, [intersection]);

        return intersection;
    }
}
