using System.Text.RegularExpressions;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests keep the documentation under <c>docs/</c> from drifting away from the thing it
/// describes.
/// <para>
/// Prose cannot be checked by a machine, but three things about it can: that every picture it
/// points at is really there, that every syntax diagram describes words the grammar actually has,
/// and that every example scene it offers still renders.  Each of those has gone wrong at least
/// once while the documentation was being written, and each is silent when it does -- a broken
/// image link renders as a broken image, and an example that no longer parses looks perfectly
/// convincing sitting on the page.
/// </para>
/// </summary>
[TestClass]
public class TestDocumentation
{
    /// <summary>
    /// Walks up from wherever the tests are running to find the root of the repository.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            DirectoryInfo directory = new (AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RayTracer.csproj")))
                directory = directory.Parent;

            Assert.IsNotNull(directory, "could not find the repository root from the test's location");

            return directory.FullName;
        }
    }

    private static string DocsDirectory => Path.Combine(RepositoryRoot, "docs");

    private static List<string> MarkdownFiles => Directory
        .EnumerateFiles(DocsDirectory, "*.md", SearchOption.AllDirectories)
        .ToList();

    [TestMethod]
    public void TestEveryPictureTheDocsPointAtIsThere()
    {
        // Both the plain markdown form and the <picture> form the light/dark diagrams use.
        Regex references = new (@"(?:src|srcset)=""([^""]+)""|!\[[^\]]*\]\(([^)]+)\)");
        List<string> missing = [];

        foreach (string file in MarkdownFiles)
        {
            string directory = Path.GetDirectoryName(file)!;

            foreach (Match match in references.Matches(File.ReadAllText(file)))
            {
                string reference = match.Groups[1].Success
                    ? match.Groups[1].Value
                    : match.Groups[2].Value;

                // Only local files are ours to check.
                if (reference.StartsWith("http") || reference.EndsWith(".md") ||
                    reference.Contains(".md#"))
                    continue;

                if (!File.Exists(Path.Combine(directory, reference)))
                    missing.Add($"{Path.GetFileName(file)} -> {reference}");
            }
        }

        Assert.AreEqual(0, missing.Count,
            $"the docs point at pictures that are not there:\n  {string.Join("\n  ", missing)}");
    }

    [TestMethod]
    public void TestEveryDiagramSourceHasBeenGenerated()
    {
        // A diagram that was written but never run through generate-diagrams.sh leaves a hole in
        // the page rather than an error, so it is worth being told.
        string diagrams = Path.Combine(DocsDirectory, "diagrams");

        if (!Directory.Exists(diagrams))
            return;

        List<string> ungenerated = [];

        foreach (string source in Directory.EnumerateFiles(diagrams, "*.mmd", SearchOption.AllDirectories))
        {
            string group = Path.GetFileName(Path.GetDirectoryName(source)!);
            string name = Path.GetFileNameWithoutExtension(source);
            string images = Path.Combine(DocsDirectory, "images", group);

            foreach (string wanted in new[] { $"{name}.svg", $"{name}-dark.svg" })
            {
                if (!File.Exists(Path.Combine(images, wanted)))
                    ungenerated.Add($"{group}/{wanted}");
            }
        }

        Assert.AreEqual(0, ungenerated.Count,
            "some diagrams have not been generated; run docs/generate-diagrams.sh:\n  " +
            string.Join("\n  ", ungenerated));
    }

    [TestMethod]
    public void TestEveryWordADiagramSpellsOutIsARealKeyword()
    {
        // The railroad diagrams quote the actual words a scene writes.  If one of those is
        // renamed or dropped, the diagram carries on claiming syntax that no longer exists, and
        // nothing about the picture gives that away.
        List<string> keywords = GrammarKeywords();
        List<string> unknown = [];
        Regex terminals = new ("\"([A-Za-z][A-Za-z]*)\"");
        string diagrams = Path.Combine(DocsDirectory, "diagrams");

        if (!Directory.Exists(diagrams))
            return;

        foreach (string source in Directory.EnumerateFiles(diagrams, "*.mmd", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(source);

            foreach (Match match in terminals.Matches(text))
            {
                string word = match.Groups[1].Value;

                // The title line is prose, not syntax.
                if (text.Contains($"title \"{word}") || keywords.Contains(word))
                    continue;

                unknown.Add($"{Path.GetFileName(source)}: \"{word}\"");
            }
        }

        Assert.AreEqual(0, unknown.Count,
            "diagrams spell out words the grammar does not have:\n  " +
            string.Join("\n  ", unknown.Distinct()));
    }

    [TestMethod]
    public void TestEveryExampleSceneStillRenders()
    {
        // The examples are the part of the documentation that can actually be run, so they are
        // the part worth running.  An example that has quietly stopped parsing still reads
        // perfectly well on the page, which is exactly the problem.
        string examples = Path.Combine(DocsDirectory, "examples");

        if (!Directory.Exists(examples))
            return;

        List<string> broken = [];
        string output = Path.Combine(Path.GetTempPath(), $"doc-examples-{Guid.NewGuid():N}");

        Directory.CreateDirectory(output);

        try
        {
            foreach (string scene in Directory.EnumerateFiles(examples, "*.igl", SearchOption.AllDirectories))
            {
                // Some examples are meant to be included by others rather than rendered alone.
                if (Path.GetFileName(scene) == "stage.igl")
                    continue;

                StringWriter captured = new ();
                TextWriter was = Console.Out;

                Console.SetOut(captured);

                try
                {
                    ImageRenderer renderer = new LanguageParser(scene).Parse();

                    if (renderer is null)
                    {
                        broken.Add($"{Path.GetFileName(scene)}: {captured}");

                        continue;
                    }

                    // Small, since this is about whether it works rather than what it looks like.
                    renderer.Render(new RenderOptions
                    {
                        OutputFileName = Path.Combine(output, $"{Path.GetFileNameWithoutExtension(scene)}.png"),
                        Width = 40,
                        Height = 30
                    });

                    if (captured.ToString().Contains("Error"))
                        broken.Add($"{Path.GetFileName(scene)}: {captured}");
                }
                catch (Exception exception)
                {
                    broken.Add($"{Path.GetFileName(scene)}: {exception.Message}");
                }
                finally
                {
                    Console.SetOut(was);
                }
            }
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, true);
        }

        Assert.AreEqual(0, broken.Count,
            $"example scenes in the docs no longer render:\n  {string.Join("\n  ", broken)}");
    }

    /// <summary>
    /// Reads the keyword list out of the grammar specification.  The specification is a private
    /// constant, so this reads the source it lives in rather than reaching into the parser.
    /// </summary>
    private static List<string> GrammarKeywords()
    {
        string source = File.ReadAllText(
            Path.Combine(RepositoryRoot, "Parser", "LanguageParser.DSL.cs"));
        int start = source.IndexOf("_keywords:", StringComparison.Ordinal);

        Assert.IsTrue(start > 0, "could not find the keyword list in the grammar specification");

        int end = source.IndexOf("_expressions:", start, StringComparison.Ordinal);
        string list = source[start..end];

        return Regex.Matches(list, "'([A-Za-z]+)'")
            .Select(match => match.Groups[1].Value)
            .ToList();
    }
}
