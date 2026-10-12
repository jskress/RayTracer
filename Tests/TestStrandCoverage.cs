using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// Strands finer than a ray is wide: widened to the ray's width where it meets them and seen in proportion
/// to how much of it they fill, the rest of the ray going on to whatever lies beyond -- for the eye and for
/// a light alike.
/// </summary>
[TestClass]
public class TestStrandCoverage
{
    /// <summary>
    /// A ray carried into a surface's own space keeps its width: a space twice as fine measures the cone
    /// twice as wide, at every distance along it.
    /// </summary>
    [TestMethod]
    public void TestARayKeepsItsWidthInASurfacesSpace()
    {
        Ray ray = new (new Point(0, 0, -5), new Vector(0, 0, 1), 0, 0.01, 3, 0.002);
        Ray local = Transforms.Scale(0.5).Invert().Transform(ray);

        foreach (double distance in new[] { 0.0, 1, 7 })
            Assert.AreEqual(2 * ray.RadiusAt(distance), local.RadiusAt(distance), 1e-12);

        Ray line = Transforms.Scale(0.5).Invert().Transform(new Ray(Point.Zero, new Vector(1, 0, 0)));

        Assert.AreEqual(0, line.RadiusAt(5));
    }

    /// <summary>
    /// A strand finer than the ray is wide is met wherever the ray's width reaches it, and covers only its
    /// share of it: its width over the ray's.  A ray with no width meets it only along its true width.
    /// </summary>
    [TestMethod]
    public void TestAFineStrandIsWidenedAndCoversItsShare()
    {
        Strand strand = Horizontal(0.01);

        // Five units out, a ray spreading a hundredth of a radian is a twentieth wide: a radius of 0.025.
        HairIntersection widened = Hits(strand, 0.02, 0.01).OfType<HairIntersection>().Single();

        Assert.AreEqual(0.005 / 0.025, widened.Coverage, 1e-6);
        Assert.AreEqual(0.8, Math.Abs(widened.Across), 1e-6);
        Assert.AreEqual(0, Hits(strand, 0.03, 0.01).Count, "a ray met a strand beyond its width");
        Assert.AreEqual(0, Hits(strand, 0.02, 0).Count, "a ray with no width met a strand beyond the strand's own");

        HairIntersection wide = Hits(Horizontal(0.2), 0.02, 0.01).OfType<HairIntersection>().Single();

        Assert.AreEqual(1, wide.Coverage, "a strand wider than the ray was faded");
    }

    /// <summary>
    /// The tree over a great many strands passes over none that a wide ray would widen: it finds what each
    /// strand finds alone.
    /// </summary>
    [TestMethod]
    public void TestATreeFindsEveryStrandAWideRayMeets()
    {
        Random random = new (11);
        List<List<Point>> paths = [];
        StrandSet all = new ();
        Strand owner = new ();

        for (int index = 0; index < 300; index++)
        {
            Point root = new (random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2);
            List<Point> path = [root, root + new Vector(random.NextDouble() - 0.5, random.NextDouble(), random.NextDouble() - 0.5)];

            paths.Add(path);
            all.Add(path, [0.004, 0.001]);
        }

        all.Seal();

        int found = 0;

        for (int ray = 0; ray < 300; ray++)
        {
            Ray probe = new (
                new Point(random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2, -6),
                new Vector(random.NextDouble() * 0.4 - 0.2, random.NextDouble() * 0.4 - 0.2, 1), 0, 0.02);
            List<Intersection> together = [];
            List<Intersection> alone = [];

            all.Intersect(probe, owner, together);

            foreach (List<Point> path in paths)
            {
                StrandSet one = new ();

                one.Add(path, [0.004, 0.001]);
                one.Seal();
                one.Intersect(probe, owner, alone);
            }

            CollectionAssert.AreEqual(
                alone.Select(hit => hit.Distance).Order().ToList(), together.Select(hit => hit.Distance).Order().ToList(),
                $"ray {ray}");
            found += together.Count;
        }

        Assert.IsTrue(found > 50, "too few strands met to judge by");
    }

    /// <summary>
    /// What lies beyond a fine strand shows in the rest of the ray: a dark strand covering a fifth of the
    /// ray before a bright wall leaves four fifths of the wall's light.
    /// </summary>
    [TestMethod]
    public void TestWhatLiesBeyondAFineStrandShowsInTheRest()
    {
        Scene scene = new ();
        Plane wall = new ()
        {
            Material = new Material { Pigment = new SolidPigment(new Color(1, 1, 1)), Ambient = 1, Diffuse = 0, Specular = 0 },
            Transform = Transforms.Translate(0, 0, 2) * Transforms.RotateAroundX(-Math.PI / 2, true)
        };

        wall.PrepareForRendering();
        scene.Lights.Add(new PointLight { Location = new Point(0, 0, -50), Color = new Color(1, 1, 1) });
        scene.Surfaces.Add(wall);
        scene.Surfaces.Add(Horizontal(0.01, new Material { Melanin = Melanin.Shades["black"], Ambient = 0 }));
        scene.ArrangeSurfaces();

        Ray ray = new (new Point(0, 0.02, -5), new Vector(0, 0, 1), 0, 0.01);
        HairIntersection strand = scene.Intersect(ray).OfType<HairIntersection>().Single();
        Color seen = scene.GetColorFor(ray);

        Assert.AreEqual(0.2, strand.Coverage, 1e-6);
        Assert.AreEqual(0.8, seen.Red, 0.03, $"came out {seen}");
    }

    /// <summary>
    /// Against nothing at all, a fine strand leaves the picture as transparent as the share of the ray it
    /// does not cover.
    /// </summary>
    [TestMethod]
    public void TestAFineStrandCoversItsShareOfAnEmptySky()
    {
        Scene scene = new ();

        scene.Lights.Add(new PointLight { Location = new Point(0, 0, -50) });
        scene.Surfaces.Add(Horizontal(0.01, new Material { Melanin = Melanin.Shades["blond"], Ambient = 0 }));
        scene.ArrangeSurfaces();

        Color seen = scene.GetColorFor(new Ray(new Point(0, 0.02, -5), new Vector(0, 0, 1), 0, 0.01));

        Assert.AreEqual(0.2, seen.Alpha, 1e-6);
    }

    /// <summary>
    /// A shadow ray from a point the eye saw is as wide as the eye's ray was there, and a fine strand in its
    /// way blocks only its share of the light: all of that share, for another strand, and of a surface
    /// beneath, what it does not send onward.
    /// </summary>
    [TestMethod]
    public void TestAFineStrandsShadowIsItsShare()
    {
        Scene scene = new ();
        Strand strand = new () { Material = new Material { Melanin = Melanin.Shades["black"] } };

        strand.Points.Add(new RibbonControlPoint { Position = new Point(0, 0.5, -1), Width = 0.01 });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(0, 0.5, 1), Width = 0.01 });
        strand.PrepareForRendering();
        scene.Surfaces.Add(strand);
        scene.ArrangeSurfaces();

        RgbSpectrum line = scene.GetLightReaching<RgbSpectrum>(Point.Zero, new Vector(0, 1, 0), 10, 0, null, true, 0);
        RgbSpectrum wide = scene.GetLightReaching<RgbSpectrum>(Point.Zero, new Vector(0, 1, 0), 10, 0, null, true, 0.05);
        RgbSpectrum beneath = scene.GetLightReaching<RgbSpectrum>(Point.Zero, new Vector(0, 1, 0), 10, 0, null, false, 0.05);

        Assert.AreEqual(0, line[0], 1e-12, "a line through the strand got past it");
        Assert.AreEqual(0.9, wide[0], 1e-6, "a wide ray lost more than the strand's share");
        Assert.IsTrue(beneath[0] > 0.9 && beneath[0] < 0.92, $"a surface beneath kept {beneath[0]}");
    }

    /// <summary>
    /// This method returns a strand two long along X, through the origin, of the given width.
    /// </summary>
    private static Strand Horizontal(double width, Material material = null)
    {
        Strand strand = new () { Material = material };

        strand.Points.Add(new RibbonControlPoint { Position = new Point(-1, 0, 0), Width = width });
        strand.Points.Add(new RibbonControlPoint { Position = new Point(1, 0, 0), Width = width });
        strand.PrepareForRendering();

        return strand;
    }

    /// <summary>
    /// This method returns the crossings of a ray looking along +Z from z = -5 at the given height, spreading
    /// as given.
    /// </summary>
    private static List<Intersection> Hits(Surface surface, double y, double spread)
    {
        List<Intersection> hits = [];

        surface.Intersect(new Ray(new Point(0, y, -5), new Vector(0, 0, 1), 0, spread), hits);

        return hits;
    }
}
