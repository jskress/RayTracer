using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover how deep a ray from the eye is followed through mirrors and glass.
/// </summary>
[TestClass]
public class TestTraceDepth
{
    /// <summary>
    /// This method makes three panes of glass stacked over a floor under an overhead sun, and returns what
    /// the eye sees of the floor looking straight down through them, followed as deep as the scene says.
    /// </summary>
    private static double ThroughThreePanes(int depth, double bailout)
    {
        Scene scene = new () { TraceDepth = depth, Bailout = bailout };
        List<Surface> surfaces =
        [
            new Plane { Material = new Material { Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0 } }
        ];

        for (int pane = 0; pane < 3; pane++)
        {
            surfaces.Add(new Cube
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
                    Transparency = 1, Reflective = 1, Interior = new Interior { IndexOfRefraction = 1.5 }
                },
                Transform = Transforms.Translate(0, 1 + pane * 0.3, 0) * Transforms.Scale(5, 0.05, 5)
            });
        }

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(new DistantLight { Direction = new Vector(0, -1, 0) });

        return scene.GetColorFor(new Ray(new Point(0.1, 3, 0.2), new Vector(0, -1, 0)), scene.TraceDepth).Red;
    }

    /// <summary>
    /// This tests that a ray from the eye is followed as deep as the scene says.  Looking down through three
    /// panes is six surfaces deep: followed through four, as every ray always was, it never reaches the floor;
    /// followed through twelve, it does, and sees the floor lit through the panes by the sun.
    /// </summary>
    [TestMethod]
    public void TestARayIsFollowedAsDeepAsTheSceneSays()
    {
        Assert.IsLessThan(0.01, ThroughThreePanes(4, 0), "four deep, the floor is never reached");
        Assert.IsGreaterThan(0.6, ThroughThreePanes(12, 0), "twelve deep, it is");
    }

    /// <summary>
    /// This tests that ways worth next to nothing are left unfollowed without changing what is seen: three panes
    /// followed twelve deep, every way, and then leaving any way worth less than a 1024th of the pixel, come to
    /// the same to within a few of those 1024ths.
    /// </summary>
    [TestMethod]
    public void TestWaysWorthNextToNothingAreLeftAlone()
    {
        Assert.AreEqual(ThroughThreePanes(12, 0), ThroughThreePanes(12, RenderContext.Bailout), 4 * RenderContext.Bailout);
    }

    /// <summary>
    /// This tests the words a scene says it with, and that a scene that says nothing is traced as it always
    /// was: four deep, every way followed.
    /// </summary>
    [TestMethod]
    public void TestAScenesSaysHowDeepToTrace()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"depth-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // Looking straight down through three panes at a floor under an overhead sun.
            double Middle(string context)
            {
                File.WriteAllText(path,
                    $"context {{ no gamma  {context} }}\n" +
                    "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 1 }\n" +
                    "distant light { direction [0, -1, 0] }\n" +
                    "plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }\n" +
                    "Pane = material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1  reflective 1  interior { ior 1.5 } }\n" +
                    "cube { material Pane  scale [5, 0.05, 5]  translate Y 1 }\n" +
                    "cube { material Pane  scale [5, 0.05, 5]  translate Y 1.3 }\n" +
                    "cube { material Pane  scale [5, 0.05, 5]  translate Y 1.6 }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{context}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2).Red;
            }

            Assert.AreEqual(ThroughThreePanes(4, 0), Middle(""), 0.001, "unsaid, four deep");
            Assert.AreEqual(ThroughThreePanes(12, RenderContext.Bailout), Middle("trace depth 12"), 0.001, "twelve deep");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
