using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the index of refraction of the space between a scene's objects.
/// <para>
/// It used to be hard-coded to one, a vacuum, which is very nearly right for a scene set in air and
/// quite wrong for one set in water: what bends light at a surface is the ratio between the two sides
/// of it, so what surrounds a solid counts as much as the solid does.
/// </para>
/// </summary>
[TestClass]
public class TestSceneEnvironment
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"environment-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// This tests that a ray outside everything takes the surrounding index rather than a vacuum, on
    /// both sides of a crossing -- the one entering a solid, and the one leaving it again.
    /// </summary>
    [TestMethod]
    public void TestTheSurroundingIndexIsUsedOutsideEverything()
    {
        Sphere sphere = new ()
        {
            Material = new Material { Interior = new Interior { IndexOfRefraction = 1.5 } }
        };
        Ray ray = new (new Point(0, 0, -5), Directions.In);
        List<Intersection> hits = [];

        sphere.Intersect(ray, hits);
        hits.Sort();

        // Entering: from the surroundings into the glass.
        (double n1, double n2) = hits.FindIndicesOfRefraction(hits[0], IndicesOfRefraction.Water);

        Assert.IsTrue(IndicesOfRefraction.Water.Near(n1), $"entering, n1 should be the water: {n1}");
        Assert.IsTrue(1.5.Near(n2), $"entering, n2 should be the glass: {n2}");

        // Leaving: from the glass back into the surroundings.
        (n1, n2) = hits.FindIndicesOfRefraction(hits[1], IndicesOfRefraction.Water);

        Assert.IsTrue(1.5.Near(n1), $"leaving, n1 should be the glass: {n1}");
        Assert.IsTrue(IndicesOfRefraction.Water.Near(n2), $"leaving, n2 should be the water: {n2}");
    }

    /// <summary>
    /// This tests that the surroundings are charged in a shadow as well.  A shadow ray asks a boundary
    /// how much light it mirrors away rather than lets through, and that is a question of the ratio
    /// between the two sides just as bending is: glass in water turns away far less light than glass in
    /// a vacuum, so a glass ball set in water casts a fainter shadow ring.
    /// </summary>
    [TestMethod]
    public void TestTheSurroundingsAreChargedInAShadowToo()
    {
        Interior glass = new () { IndexOfRefraction = IndicesOfRefraction.Glass };
        double inAVacuum = glass.GetReflectanceAt(1);
        double inWater = glass.GetReflectanceAt(1, IndicesOfRefraction.Water);

        Assert.IsTrue(inWater < inAVacuum,
            $"glass in water should mirror away less than glass in a vacuum: {inWater} against " +
            $"{inAVacuum}");
        Assert.IsTrue(inWater > 0, $"there is still a boundary there: {inWater}");

        // And matched indices are no boundary at all, which is the case the reflectance has to be
        // stopped short for -- the angle term alone would otherwise turn light away at a surface that
        // is not there.
        Assert.AreEqual(0, glass.GetReflectanceAt(0.5, IndicesOfRefraction.Glass), 1e-12);
    }

    /// <summary>
    /// This tests that a scene which says nothing about its surroundings is in a vacuum, since every
    /// scene written before there was a way to say otherwise assumed exactly that.
    /// </summary>
    [TestMethod]
    public void TestTheDefaultIsAVacuum()
    {
        Assert.AreEqual(1, new Scene().Environment.IndexOfRefraction);
        Assert.AreEqual(1, new SceneEnvironment().IndexOfRefraction);
    }

    /// <summary>
    /// Renders a glass sphere over a checkered floor, in surroundings of the given index, and hands
    /// back the image.
    /// </summary>
    private const string TheRest = """
        camera { location [0, 1.6, -4]  look at [0, 0.4, 0]  field of view 40 }
        point light { location [-5, 7, -6]  color White }
        plane { material { pigment checker { color White, color Black scale 0.5 } } }
        sphere {
            material {
                pigment color White  ambient 0  diffuse 0.05  transparency 1
                interior { ior Glass }
            }
            translate [0, 0.7, 0]  scale 0.7
        }
        """;

    private Canvas Render(string environment)
    {
        return RenderScene(environment + "\n" + TheRest);
    }

    private Canvas RenderScene(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path, "context { angles are degrees  no gamma }\n" + scene);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            Assert.IsNotNull(renderer, $"the scene did not parse: {captured}");

            renderer.Render(new RenderOptions
            {
                OutputFileName = output, Width = 60, Height = 50
            });

            Assert.DoesNotContain("Error", captured.ToString());

            return new ImageFile(output).Load()[0];
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    /// <summary>
    /// Counts how many pixels differ between two images.
    /// </summary>
    private static int DifferencesBetween(Canvas first, Canvas second)
    {
        int count = 0;

        for (int x = 0; x < first.Width; x++)
        for (int y = 0; y < first.Height; y++)
        {
            if (!first.GetPixel(x, y).Matches(second.GetPixel(x, y)))
                count++;
        }

        return count;
    }

    /// <summary>
    /// This tests that saying what surrounds a scene actually changes the picture, and by about as much
    /// as it ought to: air is very nearly a vacuum and should barely show, while water is not and
    /// should show plainly.  A glass marble in water bends light far less than one in air, since 1.5
    /// against 1.333 is a far gentler ratio than 1.5 against 1.
    /// </summary>
    [TestMethod]
    public void TestTheSurroundingsChangeThePicture()
    {
        Canvas vacuum = Render("");
        Canvas air = Render("environment ior Air");
        Canvas water = Render("environment ior Water");

        int airDifferences = DifferencesBetween(vacuum, air);
        int waterDifferences = DifferencesBetween(vacuum, water);

        Assert.AreEqual(0, DifferencesBetween(vacuum, Render("environment ior Vacuum")),
            "saying the surroundings are a vacuum must be the same as not saying anything");
        Assert.IsTrue(waterDifferences > airDifferences * 3,
            $"water should change far more than air does, and changed {waterDifferences} pixels " +
            $"against air's {airDifferences}");
        Assert.IsTrue(waterDifferences > 100,
            $"water should change the picture plainly, and changed {waterDifferences} pixels");
    }

    /// <summary>
    /// This tests that the index may be spelled out in full as well as abbreviated, since that is the
    /// choice an <c>interior</c> block gives and the two should not disagree.
    /// </summary>
    [TestMethod]
    public void TestTheIndexMayBeSpelledOutInFull()
    {
        Assert.AreEqual(0, DifferencesBetween(
            Render("environment ior Water"),
            Render("environment index of refraction Water")),
            "the short and long spellings should mean the same thing");
    }

    /// <summary>
    /// This tests that the surroundings may be given inside a scene block as well as at the top level,
    /// so that two scenes in one file may sit in different places.
    /// </summary>
    [TestMethod]
    public void TestTheSurroundingsMayBeGivenInASceneBlock()
    {
        Canvas fromABlock = RenderScene("""
            scene {
                environment ior Water
                camera { location [0, 1.6, -4]  look at [0, 0.4, 0]  field of view 40 }
                point light { location [-5, 7, -6]  color White }
                plane { material { pigment checker { color White, color Black scale 0.5 } } }
                sphere {
                    material {
                        pigment color White  ambient 0  diffuse 0.05  transparency 1
                        interior { ior Glass }
                    }
                    translate [0, 0.7, 0]  scale 0.7
                }
            }
            """);
        Canvas fromTheTopLevel = Render("environment ior Water");

        Assert.AreEqual(0, DifferencesBetween(fromABlock, fromTheTopLevel),
            "a scene block and the top level should mean the same thing");
    }
}
