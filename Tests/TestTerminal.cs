using RayTracer;
using RayTracer.Fonts;

namespace Tests;

/// <summary>
/// These tests cover the tables the commands print: headings centered over their columns with a rule under them,
/// every other row in its column's alignment, the prefix before each row and the spacing between its cells.
/// </summary>
[TestClass]
public class TestTerminal
{
    private static string[] Printed(List<List<string>> lines, List<TextAlignment> alignments, string prefix = "  ",
        string spacing = "  ")
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            Terminal.Out(lines, prefix, spacing, true, alignments);
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString().Split(Environment.NewLine)[..^1];
    }

    [TestMethod]
    public void TestHeadingsAreCenteredAndRowsAligned()
    {
        string[] printed = Printed(
            [["Name", "Size"], ["Granite", "12"], ["Ash", "7"]],
            [TextAlignment.Left, TextAlignment.Right], "> ", " | ");

        Assert.AreEqual(string.Join("/", ">  Name   | Size", "> ------- | ----", "> Granite |   12", "> Ash     |    7"),
            string.Join("/", printed));
    }

    [TestMethod]
    public void TestColumnsWithNoAlignmentGivenAreLeftAligned()
    {
        string[] printed = Printed([["A", "B"], ["one", "two"], ["x", "y"]], [TextAlignment.Right]);

        Assert.AreEqual("    x  y  ", printed[3]);
    }
}
