using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// Smooth surfaces met by a ray a rough surface has already spread: shaded as rough as what spread it, so
/// that a lantern seen in them is shared rather than found by chance -- and nothing else touched.
/// </summary>
[TestClass]
public class TestRegularization
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"regularization-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// A mirror the camera sees only in a rough floor is shaded exactly as the same mirror made as rough
    /// as the floor: the picture is the same to the last bit.  Made rougher than the floor, it is not --
    /// so the roughness it borrows is the floor's, not some other.
    /// </summary>
    [TestMethod]
    public void TestAMirrorSeenInARoughSurfaceIsAsRoughAsIt()
    {
        foreach (double floor in new[] { 0.15, 0.35 })
        {
            double[] smooth = RenderedMirror(floor, "");
            double[] asRough = RenderedMirror(floor, $"roughness {floor}");
            double[] rougher = RenderedMirror(floor, $"roughness {floor * 2}");

            AssertSame(asRough, smooth, $"a mirror seen in a floor of roughness {floor}");
            Assert.IsFalse(smooth.SequenceEqual(rougher),
                $"a mirror seen in a floor of roughness {floor} came out as rough as twice that");
        }
    }

    /// <summary>
    /// A coated wall the camera sees only in a rough floor is shaded exactly as the same wall under a coat
    /// as rough as the floor: its coat mirrors as a mirror does, and is made rough as one is, while the
    /// paint beneath, which mirrors nothing, is left as it is.
    /// </summary>
    [TestMethod]
    public void TestACoatSeenInARoughSurfaceIsAsRoughAsIt()
    {
        double[] smooth = RenderedCoat(0.25, "");
        double[] asRough = RenderedCoat(0.25, "roughness 0.25");
        double[] rougher = RenderedCoat(0.25, "roughness 0.5");

        AssertSame(asRough, smooth, "a coat seen in a rough floor");
        Assert.IsFalse(smooth.SequenceEqual(rougher), "a coat seen in a rough floor came out as rough as twice that");
    }

    /// <summary>
    /// Clear glass the camera sees only in a rough floor is frosted exactly as rough as the floor, as a
    /// mirror is.
    /// </summary>
    [TestMethod]
    public void TestClearGlassSeenInARoughSurfaceIsFrostedAsMuch()
    {
        double[] clear = RenderedPane(0.25, "");
        double[] frosted = RenderedPane(0.25, "roughness 0.25");

        AssertSame(frosted, clear, "a pane seen in a rough floor");
    }

    /// <summary>
    /// The camera's own view of a mirror is a mirror: shading a hit the camera found leaves the surface's
    /// own material on it, even in a scene with rough things in it.
    /// </summary>
    [TestMethod]
    public void TestTheCameraSeesAMirrorAsAMirror()
    {
        Material mirror = new () { Reflective = 0.8 };
        Plane plane = new () { Material = mirror };
        Scene scene = new () { Background = new SolidPigment(Colors.White) };

        scene.Surfaces.Add(plane);
        scene.Surfaces.Add(new Sphere
        {
            Material = new Material { Reflective = 0.5, Roughness = 0.3 },
            Transform = Transforms.Translate(0, 3, 0)
        });

        Ray ray = new (new Point(0, 1, -1), new Vector(0, -1, 1).Unit);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.First(found => found.Surface == plane);

        hit.PrepareUsing(ray, hits);
        scene.GetHitColor(hit, 3);

        Assert.AreSame(mirror, hit.Material, "the camera's view of a mirror was shaded as something else");
    }

    /// <summary>
    /// A lantern's shell has the surroundings' own index, so it bends nothing and is not frosted when a
    /// rough surface's ray meets it.  Frosted, its rays would set off on a run of their own, along which
    /// the lantern's share could not be reckoned, and the lantern would lose the rays' share of its light.
    /// Shared, a lantern seen in a rough wall comes to what the wall's rays alone find, looking long
    /// enough -- a shell ever so slightly off the surroundings' index being how they are asked alone.
    /// </summary>
    [TestMethod]
    public void TestALanternsShellIsNotFrosted()
    {
        double shared = RenderedLantern(1, 64).Sum();
        double rays = RenderedLantern(1.0001, 1024).Sum();

        Assert.IsTrue(rays > 0.5, $"too little of the lantern was seen to compare: {rays}");
        Assert.AreEqual(1, shared / rays, 0.03, $"shared {shared}, rays alone {rays}");
    }

    /// <summary>
    /// This method renders a rough floor mirroring a wall, with the camera looking down at the floor alone,
    /// and returns every pixel's red.  The wall wears the given finish over a mirror.
    /// </summary>
    private double[] RenderedMirror(double floor, string finish)
    {
        return Red(RenderedCanvas("reflection samples 4",
            "camera { location [0, 2, -4]  look at [0, 0, 0]  field of view 20 }\n" +
            "background linear Y gradient { [0, [0.9, 0.85, 0.7], 1, [0.2, 0.3, 0.6]] }\n" +
            "point light { location [-3, 5, -6] }\n" +
            $"plane {{ material {{ pigment [0.3, 0.3, 0.3]  reflective 0.5  roughness {floor} }} }}\n" +
            "cube { scale [10, 4, 0.1]  translate [0, 4, 4]\n" +
            $"    material {{ pigment [0.1, 0.1, 0.12]  diffuse 0.1  reflective 0.8  fresnel  {finish} }} }}\n" +
            "sphere { scale 0.5  translate [-1, 1, -3]  material { pigment [0.8, 0.2, 0.1] } }\n",
            24, 24));
    }

    /// <summary>
    /// This method renders a rough floor mirroring a coated wall, the coat as given, and returns every
    /// pixel's red.
    /// </summary>
    private double[] RenderedCoat(double floor, string coat)
    {
        return Red(RenderedCanvas("reflection samples 4",
            "camera { location [0, 2, -4]  look at [0, 0, 0]  field of view 20 }\n" +
            "background linear Y gradient { [0, [0.9, 0.85, 0.7], 1, [0.2, 0.3, 0.6]] }\n" +
            "point light { location [-3, 5, -6] }\n" +
            $"plane {{ material {{ pigment [0.3, 0.3, 0.3]  reflective 0.5  roughness {floor} }} }}\n" +
            "cube { scale [10, 4, 0.1]  translate [0, 4, 4]\n" +
            $"    material {{ pigment [0.5, 0.05, 0.05]  coat {{ {coat} }} }} }}\n" +
            "sphere { scale 0.5  translate [-1, 1, -3]  material { pigment [0.8, 0.2, 0.1] } }\n",
            24, 24));
    }

    /// <summary>
    /// This method renders a rough floor seen past a pane of glass with the given finish to a checkered wall
    /// beyond, the camera looking down at the floor alone, and returns every pixel's red.
    /// </summary>
    private double[] RenderedPane(double floor, string finish)
    {
        return Red(RenderedCanvas("reflection samples 4",
            "camera { location [0, 2, -4]  look at [0, 0, 0]  field of view 20 }\n" +
            "background [0.3, 0.4, 0.6]\n" +
            "point light { location [-3, 5, -6] }\n" +
            $"plane {{ material {{ pigment [0.3, 0.3, 0.3]  reflective 0.5  roughness {floor} }} }}\n" +
            "cube { scale [3, 2, 0.05]  translate [0, 2, 4]\n" +
            $"    material {{ pigment White  ambient 0  diffuse 0  transparency 1  reflective 1  {finish}\n" +
            "        interior { ior 1.5 } } }\n" +
            "plane { rotate X -90  translate Z 7\n" +
            "    material { pigment checker { White, Black  scale 0.4 }  ambient 1  diffuse 0  specular 0 } }\n",
            24, 24));
    }

    /// <summary>
    /// This method renders a small lantern mirrored in a rough wall, read at sixteen bits, and returns every
    /// pixel's red.  The lantern's shell has the given index.
    /// </summary>
    private double[] RenderedLantern(double index, int samples)
    {
        return Red(RenderedCanvas($"color depth 16  reflection samples {samples}",
            "camera { location [0.6, 1.4, -4.5]  look at [0, 1.3, 4]  field of view 20 }\n" +
            "background Black\n" +
            "cube { scale [10, 4, 0.1]  translate [0, 4, 4]\n" +
            "    material { pigment [0.1, 0.1, 0.12]  diffuse 0.1  reflective 0.8  roughness 0.08 } }\n" +
            "sphere {\n" +
            "    material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1\n" +
            $"        interior {{ ior {index}  medium {{ emission [0.3, 0.27, 0.21]\n" +
            "            density function { max(0, 1 - sqrt(x * x + y * y + z * z)) * 40 } } } }\n" +
            "    no shadow  gives light samples 24  scale 0.3  translate [0, 1.2, -7]\n" +
            "}\n",
            32, 32));
    }

    /// <summary>
    /// This method returns the red of every pixel of a picture.
    /// </summary>
    private static double[] Red(Canvas canvas)
    {
        List<double> red = [];

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
                red.Add(canvas.GetPixel(x, y).Red);
        }

        return red.ToArray();
    }

    /// <summary>
    /// This method asserts that two renders are the same pixel for pixel.
    /// </summary>
    private static void AssertSame(double[] expected, double[] actual, string what)
    {
        Assert.AreEqual(expected.Length, actual.Length, what);

        for (int index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], actual[index], $"{what}: pixel {index}");
    }

    /// <summary>
    /// This method renders the given scene with the given context at the given size, with no gamma.
    /// </summary>
    private Canvas RenderedCanvas(string context, string scene, int width, int height)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path, $"context {{ no gamma  {context} }}\n{scene}");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = width, Height = height, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0];
    }
}
