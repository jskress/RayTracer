using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover choosing which scene and camera to render from the command line, which a
/// file with more than one of either otherwise selects with a <c>render</c> command.  A command
/// line name takes precedence over the file's.
/// </summary>
[TestClass]
public class TestRenderSelection
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"render-selection-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Renders the given scene text with the given command line scene/camera names, and reports
    /// the error that stopped it, if any, or <c>null</c> when it rendered.
    /// </summary>
    private string ErrorFrom(string sceneText, string sceneName = null, string cameraName = null)
    {
        string path = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(path, sceneText);

        StringWriter output = new ();
        TextWriter was = Console.Out;

        Console.SetOut(output);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return output.ToString();

            renderer.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "out.png"),
                Width = 8, Height = 8,
                SceneName = sceneName, CameraName = cameraName
            });

            return output.ToString().Contains("Error") ? output.ToString() : null;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }
    }

    private const string TwoScenes =
        "context { no gamma }\n" +
        "scene {\n" +
        "  named 'day'\n" +
        "  camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
        "  point light { location [-4, 6, -8] }\n" +
        "  sphere { }\n" +
        "}\n" +
        "scene {\n" +
        "  named 'night'\n" +
        "  camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
        "  point light { location [-4, 6, -8] }\n" +
        "  sphere { }\n" +
        "}\n";

    private const string TwoCameras =
        "context { no gamma }\n" +
        "camera { named 'wide'   location [0, 0, -5]  look at [0, 0, 0]  field of view 70 }\n" +
        "camera { named 'close'  location [0, 0, -3]  look at [0, 0, 0]  field of view 40 }\n" +
        "point light { location [-4, 6, -8] }\n" +
        "sphere { }\n";

    [TestMethod]
    public void TestTheSceneToRenderMayBeNamedOnTheCommandLine()
    {
        // With two scenes and no render command, the file cannot say which to draw...
        Assert.IsNotNull(ErrorFrom(TwoScenes), "two scenes with no choice made should refuse to render");

        // ... but the command line can.
        Assert.IsNull(ErrorFrom(TwoScenes, sceneName: "night"));
    }

    [TestMethod]
    public void TestTheCameraToRenderMayBeNamedOnTheCommandLine()
    {
        Assert.IsNotNull(ErrorFrom(TwoCameras), "two cameras with no choice made should refuse to render");

        Assert.IsNull(ErrorFrom(TwoCameras, cameraName: "wide"));
    }

    [TestMethod]
    public void TestAnUnknownCommandLineNameIsReported()
    {
        Assert.IsTrue(ErrorFrom(TwoScenes, sceneName: "dusk")!.Contains("dusk"),
            "naming a scene that does not exist should say so");
        Assert.IsTrue(ErrorFrom(TwoCameras, cameraName: "telephoto")!.Contains("telephoto"),
            "naming a camera that does not exist should say so");
    }

    [TestMethod]
    public void TestTheCommandLineSceneOverridesTheRenderCommand()
    {
        // 'a' has a camera and 'b' does not; the render command chooses 'a', so with no override
        // the file renders.  Naming 'b' on the command line must win -- and 'b', having no camera,
        // then fails at the camera, which is how we know the override took effect.
        string text =
            "context { no gamma }\n" +
            "scene {\n" +
            "  named 'a'\n" +
            "  camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
            "  point light { location [-4, 6, -8] }\n" +
            "  sphere { }\n" +
            "}\n" +
            "scene {\n" +
            "  named 'b'\n" +
            "  point light { location [-4, 6, -8] }\n" +
            "  sphere { }\n" +
            "}\n" +
            "render scene 'a'\n";

        Assert.IsNull(ErrorFrom(text), "the render command alone should draw scene 'a'");

        string overridden = ErrorFrom(text, sceneName: "b");

        Assert.IsNotNull(overridden, "overriding to scene 'b' should have taken effect");
        Assert.IsTrue(overridden.Contains("camera", StringComparison.OrdinalIgnoreCase),
            "scene 'b' has no camera, so the override should fail there");
    }

    [TestMethod]
    public void TestTheCommandLineCameraOverridesTheRenderCommand()
    {
        string text =
            TwoCameras + "render with camera 'wide'\n";

        Assert.IsNull(ErrorFrom(text), "the render command alone should draw with camera 'wide'");

        // The render command names 'wide'; overriding to a name that does not exist must win, and
        // so must be reported against that name.
        Assert.IsTrue(ErrorFrom(text, cameraName: "telephoto")!.Contains("telephoto"),
            "the command line camera name should override the render command's");
    }
}
