using RayTracer.Commands;
using RayTracer.Options;

namespace Tests;

[TestClass]
public class TestExitCodes
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [TestMethod]
    public void TestARenderThatWorkedReportsSuccess()
    {
        Assert.AreEqual(0, ExitCodeFor("""
            context { no gamma }
            camera { location [0, 0, -4]  look at [0, 0, 0] }
            point light { location [2, 4, -4] }
            sphere { material { pigment Red } }
            """));
    }

    [TestMethod]
    public void TestASceneThatWillNotParseReportsFailure()
    {
        // The parser prints its own complaint and hands back nothing, so there is no picture and
        // nothing threw.  That silence is what used to be reported as success.
        Assert.AreEqual(1, ExitCodeFor("""
            context { no gamma }
            camera { location [0, 0, -4]  look at [0, 0, 0] }
            this is not a scene
            """));
    }

    [TestMethod]
    public void TestARenderThatThrewReportsFailure()
    {
        // A scene that parses and then falls over on its way to a picture -- here an image that is
        // not there, which is the same shape as the fetch that failed and started all this.
        Assert.AreEqual(1, ExitCodeFor("""
            context { no gamma }
            camera { location [0, 0, -4]  look at [0, 0, 0] }
            point light { location [2, 4, -4] }
            sphere { material { pigment image 'no-such-file-anywhere.png' } }
            """));
    }

    /// <summary>
    /// Renders the given scene the way the command line would, and reports what the process would
    /// have exited with.
    /// <para>
    /// **The exit code is put back afterwards, and that is not tidiness.**  This runs inside the test
    /// host, so a failure code left standing is the code the whole test run reports -- every test
    /// passing and the run still failing, which is a genuinely baffling thing to be handed.
    /// </para>
    /// </summary>
    private int ExitCodeFor(string scene)
    {
        string path = Path.Combine(_directory, "scene.igl");
        int was = Environment.ExitCode;
        StringWriter captured = new ();
        TextWriter previously = Console.Out;

        File.WriteAllText(path, scene);
        Console.SetOut(captured);

        try
        {
            Environment.ExitCode = 0;

            RenderCommand.Render(new RenderOptions
            {
                InputFileName = path,
                OutputFileName = Path.Combine(_directory, "out.png"),
                Width = 40,
                Height = 30
            });

            return Environment.ExitCode;
        }
        finally
        {
            Console.SetOut(previously);

            Environment.ExitCode = was;
        }
    }
}
