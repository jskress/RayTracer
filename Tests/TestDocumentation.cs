using System.Reflection;
using System.Text.RegularExpressions;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;
using RayTracer.Terms;

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
    public void TestEveryLinkBetweenTheDocsLandsSomewhere()
    {
        // The pages cross-reference each other constantly, and a link to a section that has been
        // renamed -- or was never named that in the first place -- looks perfectly ordinary on the
        // page and simply drops the reader at the top of the file.  A heading's anchor is its text,
        // lower-cased, with the spaces turned to dashes and the punctuation dropped.
        List<string> broken = [];
        Regex links = new (@"\[[^\]]*\]\(([^)]+)\)");
        Dictionary<string, HashSet<string>> anchors = [];

        foreach (string file in MarkdownFiles)
        {
            string directory = Path.GetDirectoryName(file)!;

            foreach (Match match in links.Matches(File.ReadAllText(file)))
            {
                string reference = match.Groups[1].Value;

                // Only links between our own pages are ours to check.
                if (reference.StartsWith("http") || reference.StartsWith("mailto:"))
                    continue;

                string[] parts = reference.Split('#');
                string path = parts[0].Length == 0
                    ? file
                    : Path.GetFullPath(Path.Combine(directory, parts[0]));

                // A link may name a folder rather than a page -- the examples are pointed at that way.
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    broken.Add($"{Path.GetFileName(file)} -> {reference} (no such file)");

                    continue;
                }

                if (parts.Length < 2 || !path.EndsWith(".md"))
                    continue;

                if (!anchors.TryGetValue(path, out HashSet<string> headings))
                    anchors[path] = headings = HeadingAnchorsIn(path);

                if (!headings.Contains(parts[1]))
                    broken.Add($"{Path.GetFileName(file)} -> {reference} (no such section)");
            }
        }

        Assert.AreEqual(0, broken.Count,
            $"the docs link to sections that are not there:\n  {string.Join("\n  ", broken)}");
    }

    /// <summary>
    /// Collects the anchor GitHub gives every heading in the given markdown file.
    /// </summary>
    private static HashSet<string> HeadingAnchorsIn(string file)
    {
        HashSet<string> anchors = [];

        foreach (string line in File.ReadLines(file).Where(line => line.StartsWith('#')))
        {
            string text = line.TrimStart('#').Trim().ToLowerInvariant();

            anchors.Add(new string(text
                .Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-')
                .Select(character => character == ' ' ? '-' : character)
                .ToArray()));
        }

        return anchors;
    }

    [TestMethod]
    public void TestEveryGallerySceneNamedInProseIsThere()
    {
        // The guide points at gallery scenes by name, in prose rather than as links, so nothing about
        // a chapter looks wrong when one is renamed or moved -- the sentence still reads perfectly and
        // simply sends the reader nowhere.  This is the one check that catches that.
        Regex named = new (@"gallery/[A-Za-z0-9._/-]+");
        List<string> missing = [];

        foreach (string file in MarkdownFiles)
        {
            foreach (Match match in named.Matches(File.ReadAllText(file)))
            {
                // A trailing dot belongs to the sentence rather than to the path.
                string path = match.Value.TrimEnd('.', ',');

                if (!File.Exists(Path.Combine(RepositoryRoot, path)) &&
                    !Directory.Exists(Path.Combine(RepositoryRoot, path)))
                    missing.Add($"{Path.GetFileName(file)} -> {path}");
            }
        }

        Assert.AreEqual(0, missing.Count,
            $"the guide names gallery scenes that are not there:\n  {string.Join("\n  ", missing)}");
    }

    [TestMethod]
    public void TestTheGalleryIndexPointsAtRealFiles()
    {
        // The gallery's own index is a hand-written table of a couple of hundred links, with no
        // generator behind it and nothing else watching it.  Moving a scene without minding it leaves
        // broken thumbnails, which is the sort of thing nobody notices for a year.
        string index = Path.Combine(RepositoryRoot, "gallery", "README.md");

        Assert.IsTrue(File.Exists(index), "the gallery has no index at all");

        Regex pointed = new (@"(?:href|src)=""([^""]+)""");
        List<string> missing = [];

        foreach (Match match in pointed.Matches(File.ReadAllText(index)))
        {
            string pointedAt = match.Groups[1].Value;

            if (pointedAt.StartsWith("http"))
                continue;

            if (!File.Exists(Path.Combine(RepositoryRoot, "gallery", pointedAt)))
                missing.Add(pointedAt);
        }

        Assert.AreEqual(0, missing.Count,
            $"the gallery index points at files that are not there:\n  {string.Join("\n  ", missing)}");
    }

    [TestMethod]
    public void TestTheGalleryIndexNamesEveryScene()
    {
        // The other way round from the test above, and the one that actually bites.  A scene added to
        // the gallery and left out of the index is invisible: nothing is broken, nothing complains, and
        // the picture simply never appears on the page.  Four scenes went in over one stretch of work
        // and the only thing standing between them and that fate was remembering.
        //
        // The rule every .igl in the gallery must meet is one of two things.  Either it is a scene, in
        // which case it has a rendered image beside it and a row in the index; or it is a piece meant
        // to be pulled into other scenes, in which case it has no image and something includes it.
        // Anything that is neither has been forgotten about.
        string gallery = Path.Combine(RepositoryRoot, "gallery");
        string index = File.ReadAllText(Path.Combine(gallery, "README.md"));
        string[] scenes = Directory
            .EnumerateFiles(gallery, "*.igl", SearchOption.AllDirectories)
            .Order()
            .ToArray();
        HashSet<string> included = [];

        foreach (string scene in scenes)
        {
            // Resolved against the file doing the including, as the parser itself resolves it.  Two
            // directories here hold a "default-context.igl", so a bare file name is not an answer.
            foreach (Match match in Regex.Matches(File.ReadAllText(scene), @"include\s+'([^']+)'"))
                included.Add(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(scene)!, match.Groups[1].Value)));
        }

        List<string> forgotten = [];

        foreach (string scene in scenes)
        {
            string relative = Path.GetRelativePath(gallery, scene).Replace(Path.DirectorySeparatorChar, '/');

            if (File.Exists(Path.ChangeExtension(scene, ".png")))
            {
                if (!index.Contains(relative))
                    forgotten.Add($"{relative} (rendered, but not in the index)");
            }
            else if (!included.Contains(Path.GetFullPath(scene)))
                forgotten.Add($"{relative} (no image, and nothing includes it)");
        }

        Assert.IsEmpty(forgotten,
            "these gallery scenes have been forgotten about:\n  " + string.Join("\n  ", forgotten));
    }

    [TestMethod]
    public void TestTheGalleryIndexPutsEverySceneUnderTheRightHeading()
    {
        // The index is in three parts, one per directory the gallery is kept in, and a row added under
        // the wrong heading is worse than one left out: it is there, it works, its picture shows, and
        // it is quietly filed as somebody else's work.  Ten had drifted before this test existed --
        // each one appended after the last, and the last had been wrong for some time.
        Dictionary<string, string> headings = new ()
        {
            { "Ray Tracer Challenge Book", "challenge-book/" },
            { "Stuff Invented Here", "Local/" },
            { "Ported from POV-Ray", "POVRay/" }
        };
        string[] lines = File.ReadAllLines(Path.Combine(RepositoryRoot, "gallery", "README.md"));
        List<string> misfiled = [];
        string under = null;

        foreach (string line in lines)
        {
            if (line.StartsWith("### "))
            {
                under = line[4..].Trim();

                Assert.IsTrue(headings.ContainsKey(under),
                    $"the gallery index has a heading nothing knows about: {under}");

                continue;
            }

            // Every path in the row, not only the scene it links to: a row whose thumbnail is drawn
            // from another section is just as wrong and rather harder to notice.
            foreach (Match points in Regex.Matches(line, @"(?:href|src)=""([^""]+)"""))
            {
                string pointedAt = points.Groups[1].Value;

                if (under is null || pointedAt.StartsWith("http") ||
                    pointedAt.StartsWith(headings[under]))
                    continue;

                misfiled.Add($"{pointedAt} is filed under \"{under}\"");
            }
        }

        Assert.IsEmpty(misfiled,
            "these gallery scenes are under the wrong heading:\n  " + string.Join("\n  ", misfiled));
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

    // -------- The reference chapter's tables must match the thing they list. --------

    private static string ReferencePath => Path.Combine(DocsDirectory, "reference.md");

    /// <summary>
    /// Pulls the back-ticked names out of the table rows (lines beginning with "|") that sit under
    /// the given heading, up to the next heading of any level.  When <paramref name="firstCellOnly"/>
    /// is set, only the first cell of each row is read -- the name column of a table whose other
    /// columns also hold back-ticked words.
    /// </summary>
    private static HashSet<string> BacktickedNamesUnder(string heading, bool firstCellOnly)
    {
        HashSet<string> names = [];
        bool inSection = false;

        foreach (string raw in File.ReadAllLines(ReferencePath))
        {
            string line = raw.Trim();

            if (line.StartsWith('#'))
            {
                inSection = line.TrimStart('#', ' ') == heading;

                continue;
            }

            if (!inSection || !line.StartsWith('|'))
                continue;

            string scan = firstCellOnly
                ? line.Split('|').ElementAtOrDefault(1) ?? string.Empty
                : line;

            foreach (Match match in Regex.Matches(scan, "`([^`]+)`"))
                names.Add(match.Groups[1].Value);
        }

        return names;
    }

    private static void AssertSameNames(HashSet<string> expected, HashSet<string> documented, string what)
    {
        List<string> missing = expected.Except(documented).Order().ToList();
        List<string> extra = documented.Except(expected).Order().ToList();

        Assert.IsTrue(missing.Count == 0 && extra.Count == 0,
            $"the reference's {what} table is out of step with the source.\n" +
            $"  in the source but not the table: {string.Join(", ", missing)}\n" +
            $"  in the table but not the source: {string.Join(", ", extra)}");
    }

    /// <summary>
    /// The names of the public static fields of the given type whose value is of type
    /// <typeparamref name="T"/> -- the same reflection the renderer uses to publish them.
    /// </summary>
    private static HashSet<string> PublicStaticNamesOfType<T>(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(T))
        .Select(field => field.Name)
        .ToHashSet();

    [TestMethod]
    public void TestTheKeywordIndexListsExactlyTheGrammarsKeywords()
    {
        AssertSameNames(
            GrammarKeywords().ToHashSet(),
            BacktickedNamesUnder("Keyword index", firstCellOnly: true),
            "keyword index");
    }

    [TestMethod]
    public void TestTheFunctionTableListsExactlyTheCatalogsFunctions()
    {
        // The catalog is about to grow, and a function nobody can find is as good as one that was
        // never added -- so the table is held to what the catalog actually holds.
        AssertSameNames(
            FunctionCatalog.Instance.Names.ToHashSet(),
            BacktickedNamesUnder("Functions", firstCellOnly: false),
            "function");
    }

    [TestMethod]
    public void TestTheColorTableListsExactlyTheNamedColors()
    {
        AssertSameNames(
            PublicStaticNamesOfType<Color>(typeof(Colors)),
            BacktickedNamesUnder("Colors", firstCellOnly: false),
            "color");
    }

    [TestMethod]
    public void TestTheIndexOfRefractionTableListsExactlyTheNamedValues()
    {
        AssertSameNames(
            typeof(IndicesOfRefraction)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(double))
                .Select(field => field.Name)
                .ToHashSet(),
            BacktickedNamesUnder("Indices of refraction", firstCellOnly: false),
            "index of refraction");
    }

    [TestMethod]
    public void TestTheDirectionTableListsExactlyTheNamedVectors()
    {
        AssertSameNames(
            PublicStaticNamesOfType<Vector>(typeof(Directions)),
            BacktickedNamesUnder("Direction vectors", firstCellOnly: false),
            "direction vector");
    }

    [TestMethod]
    public void TestTheGlobalConstantsTableListsExactlyThoseTheRendererSets()
    {
        // The renderer sets these one by one in its constructor; read them from that source the
        // way the keyword list is read from the grammar, rather than reaching into a private pool.
        string source = File.ReadAllText(
            Path.Combine(RepositoryRoot, "Renderer", "ImageRenderer.cs"));
        HashSet<string> set = Regex.Matches(source, @"_globals\.SetValue\(""([^""]+)""")
            .Select(match => match.Groups[1].Value)
            .ToHashSet();

        Assert.IsTrue(set.Count > 0, "could not find the global SetValue calls in ImageRenderer");

        AssertSameNames(set, BacktickedNamesUnder("Global constants", firstCellOnly: true),
            "global constants");
    }
}
