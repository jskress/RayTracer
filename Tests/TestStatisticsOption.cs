using System.Text.RegularExpressions;
using CommandLine;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;
using Parser = CommandLine.Parser;

namespace Tests;

/// <summary>
/// `--stats` asks for a report, laid out to read, of what a render cost and what its scene held: its
/// surfaces by kind, the combinations it wrote, the shapes it shared and its lights.  The tool style of
/// progress keeps the one line of key/value counts it has always given, for the program reading it.
/// </summary>
[TestClass]
public class TestStatisticsOption
{
    private const string Camera = "camera { location [0, 2, -5]  look at [0, 0, 0] }\n";
    private const string Light = "point light { location [-4, 6, -6] }\n";

    private static readonly Regex KindRow = new (@"^      (.+?)\s{2,}([\d,]+)(?:  \(([\d,]+) pieces?\))?$");

    private string _directory;
    private string _scene;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"stats-{Guid.NewGuid():N}");
        _scene = Path.Combine(_directory, "scene.igl");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// The command line knows the option by name, and a render that does not give it does not have
    /// it.
    /// </summary>
    [TestMethod]
    public void TestTheOptionIsReadFromTheCommandLine()
    {
        File.WriteAllText(_scene, Camera + Light + "sphere { }\n");

        Assert.IsTrue(Parsed("-i", _scene, "--stats").ReportStatistics);
        Assert.IsFalse(Parsed("-i", _scene).ReportStatistics);
    }

    /// <summary>
    /// Asked for, the report comes once, whatever the progress style.
    /// </summary>
    [TestMethod]
    public void TestAskingForStatisticsGivesTheReportOnce()
    {
        foreach (string style in new[] { "bar", "none", "tool" })
        {
            string said = Rendered(Camera + Light + "sphere { }\n", style, true);

            Assert.AreEqual(1, Lines(said).Count(line => line == "Statistics for scene.igl"), style);
        }
    }

    /// <summary>
    /// The tool style gives its line of counts whether the report is asked for or not, and nothing
    /// else does; not asked for, there is no report.
    /// </summary>
    [TestMethod]
    public void TestOnlyTheToolStyleGivesItsLineOfCounts()
    {
        const string scene = Camera + Light + "sphere { }\n";

        Assert.AreEqual(1, ToolLines(Rendered(scene, "tool", false)));
        Assert.AreEqual(1, ToolLines(Rendered(scene, "tool", true)));
        Assert.AreEqual(0, ToolLines(Rendered(scene, "bar", true)));
        Assert.AreEqual(0, ToolLines(Rendered(scene, "none", true)));
        Assert.IsFalse(Rendered(scene, "none", false).Contains("Statistics for"));
    }

    /// <summary>
    /// The report counts what the render did: a 4 by 4 picture with no anti-aliasing is 16 pixels of
    /// one sample each.
    /// </summary>
    [TestMethod]
    public void TestTheReportCountsTheRender()
    {
        List<string> lines = Lines(Rendered(Camera + Light + "sphere { }\n", "none", true));

        CollectionAssert.Contains(lines, "  Image       4 x 4, 16 pixels");
        CollectionAssert.Contains(lines, "  Samples     16, 1.00 a pixel");
    }

    /// <summary>
    /// A group only keeps things together, so it is not counted; what it holds is, however deep.
    /// </summary>
    [TestMethod]
    public void TestGroupsAreNotCountedButWhatTheyHoldIs()
    {
        Dictionary<string, (long Count, long Pieces)> kinds = Kinds(Report(
            "group { sphere { }  group { sphere { translate [2, 0, 0] }  cube { translate [-2, 0, 0] } } }"));

        Assert.AreEqual(2, kinds.Count);
        Assert.AreEqual(2, kinds["sphere"].Count);
        Assert.AreEqual(1, kinds["cube"].Count);
    }

    /// <summary>
    /// `union { a b c }` is built as two unions, one inside the other, but it is one union as written.
    /// </summary>
    [TestMethod]
    public void TestACombinationCountsAsWritten()
    {
        string report = Report(
            "union { sphere { }  sphere { translate [1, 0, 0] }  sphere { translate [2, 0, 0] } }\n" +
            "difference { cube { translate [-3, 0, 0] }  sphere { translate [-3, 0, -1] } }");

        Assert.AreEqual(4, Kinds(report)["sphere"].Count);
        CollectionAssert.Contains(Lines(report), "  Combined    1 union and 1 difference");
    }

    /// <summary>
    /// A kind built from pieces of its own counts once, as itself, with its pieces kept alongside: a
    /// tube through three points is two segments, and a height field is a mesh of triangles.
    /// </summary>
    [TestMethod]
    public void TestAKindBuiltFromPiecesCountsOnce()
    {
        Dictionary<string, (long Count, long Pieces)> kinds = Kinds(Report(
            "tube { radius 0.2 at [-1, 0, 0]  radius 0.2 at [0, 0, 0]  radius 0.2 at [1, 0, 0] }\n" +
            "heightfield { function { 0.1 }  samples 8 }"));

        Assert.AreEqual(2, kinds.Count);
        Assert.AreEqual((1, 2), kinds["tube"]);
        Assert.AreEqual(1, kinds["height field"].Count);
        Assert.IsTrue(kinds["height field"].Pieces > 1, "a height field's triangles are its pieces");
    }

    /// <summary>
    /// A field counts once, and its copies are counted too, since they are the scene's own surface
    /// set out many times over.
    /// </summary>
    [TestMethod]
    public void TestAFieldCountsItsCopies()
    {
        Dictionary<string, (long Count, long Pieces)> kinds = Kinds(Report(
            "Ball = sphere { scale 0.05 }\n" +
            "field { of Ball  spacing 0.5  within { move to -1, -1  line to 1, -1  line to 1, 1  line to -1, 1  close } }"));

        Assert.AreEqual(1, kinds["field"].Count);
        Assert.IsTrue(kinds["sphere"].Count >= 4, $"the field's copies should be counted, not {kinds["sphere"].Count}");
    }

    /// <summary>
    /// A shape shared between instances counts wherever it is shown, and the report says which shapes
    /// were shown more than once -- which a primitive called a single time, though it is an instance
    /// too, was not.
    /// </summary>
    [TestMethod]
    public void TestASharedShapeCountsWhereverItIsShown()
    {
        const string ball =
            "primitive Ball(size = 1) -> group {\n" +
            "    return group { sphere { scale (0.5 * size) } }\n" +
            "}\n";
        string thrice = Report(ball +
            "object Ball(1) { translate [-1, 0, 0] }\n" +
            "object Ball(1)\n" +
            "object Ball(1) { translate [1, 0, 0] }\n");
        string once = Report(ball + "object Ball(1)\n");

        Assert.AreEqual(3, Kinds(thrice)["sphere"].Count);
        CollectionAssert.Contains(Lines(thrice), "  Shared      1 shape, shown 3 times");
        Assert.IsFalse(once.Contains("Shared"), "a shape shown once is not shared");
    }

    /// <summary>
    /// The lights are counted by kind.
    /// </summary>
    [TestMethod]
    public void TestLightsAreCountedByKind()
    {
        string report = Report(
            "sphere { }\n" +
            "point light { location [4, 6, -6] }\n" +
            "distant light { direction [1, -1.2, 0.5] }", withLight: true);

        CollectionAssert.Contains(Lines(report), "  Lights      3: 2 point lights and 1 distant light");
    }

    /// <summary>
    /// This method parses a command line the way the program does, and hands back the render's
    /// options.
    /// </summary>
    private static RenderOptions Parsed(params string[] arguments)
    {
        using Parser parser = new (settings => settings.HelpWriter = null);
        RenderOptions options = null;

        parser.ParseArguments<RenderOptions, FontsOptions, LibrariesOptions>(arguments)
            .WithParsed<RenderOptions>(parsed => options = parsed);

        Assert.IsNotNull(options, "the command line should parse as a render");

        return options;
    }

    /// <summary>
    /// This method renders a scene of the given surfaces, with a camera and a light, and hands back
    /// the report it gave.
    /// </summary>
    private string Report(string surfaces, bool withLight = true)
    {
        return Rendered(Camera + (withLight ? Light : "") + surfaces + "\n", "none", true);
    }

    /// <summary>
    /// This method reads the surface kinds out of a report: each kind's count, and its pieces if it
    /// gave any.
    /// </summary>
    private static Dictionary<string, (long Count, long Pieces)> Kinds(string report)
    {
        Dictionary<string, (long Count, long Pieces)> kinds = [];

        foreach (string line in Lines(report))
        {
            Match match = KindRow.Match(line);

            if (match.Success)
            {
                kinds[match.Groups[1].Value] = (
                    long.Parse(match.Groups[2].Value.Replace(",", "")),
                    match.Groups[3].Success ? long.Parse(match.Groups[3].Value.Replace(",", "")) : 0);
            }
        }

        return kinds;
    }

    /// <summary>
    /// This method counts the tool style's lines of counts in what a render wrote.
    /// </summary>
    private static int ToolLines(string said)
    {
        return Lines(said).Count(line => line.StartsWith("statistics "));
    }

    /// <summary>
    /// This method splits what a render wrote into its lines.
    /// </summary>
    private static List<string> Lines(string said)
    {
        return said.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
    }

    /// <summary>
    /// This method writes the scene out, renders it 4 pixels square, and hands back everything the
    /// render wrote.
    /// </summary>
    private string Rendered(string scene, string style, bool reportStatistics)
    {
        File.WriteAllText(_scene, scene);

        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            new LanguageParser(_scene).Parse().Render(new RenderOptions
            {
                InputFileName = _scene,
                OutputFileName = Path.Combine(_directory, "scene.png"),
                Width = 4,
                Height = 4,
                ProgressStyleText = style,
                ReportStatistics = reportStatistics
            });
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }
}
