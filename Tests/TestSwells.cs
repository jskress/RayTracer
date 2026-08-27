using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

/// <summary>
/// These test the water as a surface: that a ray finds it where the shape says it is, that the box
/// holds it, and that a ray starting under it can still find its way out -- which is what CSG and
/// looking up from below both depend on.
/// </summary>
[TestClass]
public class TestSwells
{
    private static Swells Water(double amplitude = 0.8, double wavelength = 20) 
    {
        Swells water = new () { Across = 200, Along = 200 };

        water.Trains.Add(new SwellTrain
        {
            Amplitude = amplitude, Wavelength = wavelength, Direction = new Vector(1, 0, 0)
        });

        water.PrepareForRendering();

        return water;
    }

    /// <summary>
    /// A ray dropped straight down must meet the water at exactly the height the shape says stands
    /// there.  This is the whole contract of the surface in one assertion.
    /// </summary>
    [TestMethod]
    public void TestARayFindsTheWaterWhereTheShapeSaysItIs()
    {
        Swells water = Water();
        Random random = new (11);

        for (int trial = 0; trial < 300; trial++)
        {
            double x = random.NextDouble() * 80 - 40;
            double z = random.NextDouble() * 80 - 40;
            List<Intersection> found = [];

            water.Intersect(new Ray(new Point(x, 40, z), new Vector(0, -1, 0)), found);

            Assert.AreEqual(1, found.Count, $"a ray straight down at ({x:F3}, {z:F3}) found nothing");

            double height = 40 - found[0].Distance;

            Assert.AreEqual(water.Field.HeightAt(x, z), height, 1e-3,
                $"the crossing at ({x:F3}, {z:F3}) is not where the water is");
        }
    }

    /// <summary>
    /// **A ray starting under the water must find its way out.**  Refraction through the surface and
    /// any CSG that holds it both walk the crossings from inside, and a surface that only ever looks
    /// forward from outside would leave them with nothing to walk.
    /// </summary>
    [TestMethod]
    public void TestARayUnderTheWaterFindsTheSurfaceAbove()
    {
        Swells water = Water();
        Random random = new (13);
        int found = 0;

        for (int trial = 0; trial < 200; trial++)
        {
            double x = random.NextDouble() * 60 - 30;
            double z = random.NextDouble() * 60 - 30;
            double under = water.Field.HeightAt(x, z) - 0.05;
            List<Intersection> crossings = [];

            water.Intersect(new Ray(new Point(x, under, z), new Vector(0, 1, 0)), crossings);

            Assert.AreEqual(1, crossings.Count,
                $"a ray at ({x:F3}, {z:F3}) starting under the water found no way out");

            Assert.AreEqual(water.Field.HeightAt(x, z), under + crossings[0].Distance, 1e-3);

            found++;
        }

        Assert.AreEqual(200, found);
    }

    /// <summary>
    /// The box has to hold the water: the same rule every surface is held to, since a box that falls
    /// short makes the surface vanish in patches at whatever angle it falls short in.
    /// </summary>
    [TestMethod]
    public void TestTheWaterIsInsideItsBox()
    {
        Swells water = Water();
        BoundingBox box = water.BoundingBox;

        Assert.IsNotNull(box);

        Random random = new (17);

        for (int trial = 0; trial < 20000; trial++)
        {
            double x = random.NextDouble() * 200 - 100;
            double z = random.NextDouble() * 200 - 100;
            double height = water.Field.HeightAt(x, z);

            Assert.IsTrue(height >= box.Minimum.Y && height <= box.Maximum.Y,
                $"the water stands at {height:F6} over ({x:F3}, {z:F3}), which is outside the box's " +
                $"{box.Minimum.Y:F6} to {box.Maximum.Y:F6}");
        }
    }

    /// <summary>
    /// A ray passing well above the water must find nothing, and one passing well below it likewise.
    /// This is what the bound is for, and getting it wrong shows up here as a crossing out of nowhere.
    /// </summary>
    [TestMethod]
    public void TestARayThatMissesTheWaterFindsNothing()
    {
        Swells water = Water();
        List<Intersection> above = [];
        List<Intersection> below = [];

        water.Intersect(new Ray(new Point(-50, 5, 0), new Vector(1, 0, 0)), above);
        water.Intersect(new Ray(new Point(-50, -5, 0), new Vector(1, 0, 0)), below);

        Assert.IsEmpty(above, "a ray well above the water crossed it");
        Assert.IsEmpty(below, "a ray well below the water crossed it");
    }

    /// <summary>
    /// The normal the surface reports must be the one the shape has there.
    /// </summary>
    [TestMethod]
    public void TestTheSurfaceReportsTheShapesOwnNormal()
    {
        Swells water = Water();
        List<Intersection> found = [];
        Ray ray = new (new Point(3.3, 40, -1.7), new Vector(0, -1, 0));

        water.Intersect(ray, found);

        Assert.AreEqual(1, found.Count);

        Point where = ray.At(found[0].Distance);
        Vector normal = water.SurfaceNormaAt(where, found[0]);

        Assert.IsTrue(normal.Matches(water.Field.NormalAt(where.X, where.Z)));
        Assert.AreEqual(1, normal.Magnitude, 1e-9);
    }
}
