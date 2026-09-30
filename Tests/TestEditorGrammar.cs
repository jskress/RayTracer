using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace Tests;

/// <summary>
/// These tests keep the editor grammar under <c>editors/igl</c> from drifting away from the language
/// it highlights.  The grammar is written by <c>editors/igl/generate-grammar.py</c>, which reads its
/// word lists from the engine's source; these hold what it wrote to the engine itself -- the parser's
/// keywords, and the names the renderer publishes -- so a grammar left behind by a change to the
/// language fails here rather than quietly coloring the wrong words.  When one of these fails, run
/// the generator.
/// </summary>
[TestClass]
public class TestEditorGrammar
{
    private const string Regenerate = "run \"python3 editors/igl/generate-grammar.py\" to bring it up to date";

    /// <summary>
    /// The scopes a keyword may be colored as.  Every keyword must be in exactly one of them.
    /// </summary>
    private static readonly string[] KeywordScopes =
    [
        "keyword.control.igl", "keyword.control.import.igl", "keyword.operator.word.igl",
        "storage.type.igl", "support.type.igl", "support.type.pattern.igl", "keyword.other.igl",
        "constant.language.igl"
    ];

    // A list of whole words, as the generator writes one: the words between edges that know about
    // Greek letters, optionally with a lookahead after them (a function is only one before a "(").
    private static readonly Regex WordList = new (@"^\(\?<!\[[^\]]+\]\)\(\?:([^()]+)\)\(\?!\[[^\]]+\]\)(?:\(\?=[^)]*\))?$");

    // A single word caught in a group, as the "include" and "import" rules write theirs.
    private static readonly Regex CapturedWord = new (@"^\(\?<!\[[^\]]+\]\)\(([A-Za-z_]+)\)\(\?!\[[^\]]+\]\)$");

    private static readonly Lazy<Dictionary<string, HashSet<string>>> LazyWords = new (ReadWords);

    /// <summary>
    /// Reads every word list out of the grammar, keyed by the scope it colors its words as.
    /// </summary>
    private static Dictionary<string, HashSet<string>> ReadWords()
    {
        string path = Path.Combine(TestDocumentation.RepositoryRoot, "editors", "igl", "syntaxes", "igl.tmLanguage.json");
        using JsonDocument grammar = JsonDocument.Parse(File.ReadAllText(path));
        Dictionary<string, HashSet<string>> words = [];

        void Add(string scope, IEnumerable<string> found)
        {
            if (!words.TryGetValue(scope, out HashSet<string> set))
                words[scope] = set = [];

            set.UnionWith(found);
        }

        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                    Visit(item);

                return;
            }

            if (element.ValueKind != JsonValueKind.Object)
                return;

            string pattern = element.TryGetProperty("match", out JsonElement match) ? match.GetString()
                : element.TryGetProperty("begin", out JsonElement begin) ? begin.GetString() : null;

            if (pattern is not null)
            {
                Match list = WordList.Match(pattern);
                Match single = CapturedWord.Match(pattern);

                if (list.Success && element.TryGetProperty("name", out JsonElement name))
                    Add(name.GetString(), list.Groups[1].Value.Split('|'));
                else if (single.Success)
                {
                    string captures = element.TryGetProperty("captures", out _) ? "captures" : "beginCaptures";

                    if (element.TryGetProperty(captures, out JsonElement groups) &&
                        groups.TryGetProperty("1", out JsonElement first) &&
                        first.TryGetProperty("name", out JsonElement scope))
                        Add(scope.GetString(), [single.Groups[1].Value]);
                }
            }

            foreach (JsonProperty property in element.EnumerateObject())
                Visit(property.Value);
        }

        Visit(grammar.RootElement);

        return words;
    }

    private static HashSet<string> WordsColoredAs(string scope) =>
        LazyWords.Value.TryGetValue(scope, out HashSet<string> set) ? set : [];

    private static void AssertSameNames(HashSet<string> engine, string scope, string what)
    {
        HashSet<string> grammar = WordsColoredAs(scope);
        List<string> missing = engine.Except(grammar).Order().ToList();
        List<string> extra = grammar.Except(engine).Order().ToList();

        Assert.IsTrue(engine.Count > 0, $"found no {what} in the engine to hold the grammar to");
        Assert.IsTrue(missing.Count == 0 && extra.Count == 0,
            $"the editor grammar's {what} are out of step with the engine; {Regenerate}.\n" +
            $"  in the engine but not the grammar: {string.Join(", ", missing)}\n" +
            $"  in the grammar but not the engine: {string.Join(", ", extra)}");
    }

    [TestMethod]
    public void TestEveryKeywordIsColoredExactlyOnce()
    {
        HashSet<string> keywords = TestDocumentation.GrammarKeywords().ToHashSet();
        List<string> uncolored = [];
        List<string> twice = [];

        foreach (string keyword in keywords.Order())
        {
            int count = KeywordScopes.Count(scope => WordsColoredAs(scope).Contains(keyword));

            if (count == 0)
                uncolored.Add(keyword);
            else if (count > 1)
                twice.Add(keyword);
        }

        List<string> strangers = KeywordScopes
            .SelectMany(WordsColoredAs)
            .Where(word => !keywords.Contains(word))
            .Distinct()
            .Order()
            .ToList();

        Assert.IsTrue(uncolored.Count == 0 && twice.Count == 0 && strangers.Count == 0,
            $"the editor grammar's keywords are out of step with the language; {Regenerate}.\n" +
            $"  keywords it does not color: {string.Join(", ", uncolored)}\n" +
            $"  keywords it colors more than one way: {string.Join(", ", twice)}\n" +
            $"  words it colors as keywords that are not: {string.Join(", ", strangers)}");
    }

    [TestMethod]
    public void TestTheGrammarNamesExactlyTheNamedColors()
    {
        AssertSameNames(
            TestDocumentation.PublicStaticNamesOfType<Color>(typeof(Colors)),
            "support.constant.color.igl", "named colors");
    }

    [TestMethod]
    public void TestTheGrammarNamesExactlyTheIndicesOfRefraction()
    {
        AssertSameNames(
            typeof(IndicesOfRefraction)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(double))
                .Select(field => field.Name)
                .ToHashSet(),
            "support.constant.ior.igl", "indices of refraction");
    }

    [TestMethod]
    public void TestTheGrammarNamesExactlyTheDirections()
    {
        AssertSameNames(
            TestDocumentation.PublicStaticNamesOfType<Vector>(typeof(Directions)),
            "support.constant.direction.igl", "directions");
    }

    [TestMethod]
    public void TestTheGrammarNamesExactlyTheCatalogsFunctions()
    {
        AssertSameNames(
            FunctionCatalog.Instance.Names.ToHashSet(),
            "support.function.builtin.igl", "built-in functions");
    }

    [TestMethod]
    public void TestTheGrammarNamesExactlyTheRenderersGlobals()
    {
        // The renderer sets these one by one in its constructor, so they are read from that source,
        // as the documentation's table of them is.
        string source = File.ReadAllText(
            Path.Combine(TestDocumentation.RepositoryRoot, "Renderer", "ImageRenderer.cs"));

        AssertSameNames(
            Regex.Matches(source, @"_globals\.SetValue\(""([^""]+)""")
                .Select(match => match.Groups[1].Value)
                .ToHashSet(),
            "support.constant.igl", "global constants");
    }
}
