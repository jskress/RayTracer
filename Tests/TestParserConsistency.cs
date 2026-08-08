using System.Text.RegularExpressions;

namespace Tests;

/// <summary>
/// These tests keep the parser's several lists of surface kinds saying the same thing.
/// <para>
/// The renderer names its surface kinds in a surprising number of places, and none of them can be
/// derived from the others.  The grammar says what may stand inside a group, inside a CSG and at the
/// top of a scene.  A handler in C# then decides what to do with each of those.  Reusing a named
/// surface has a list, and calling a primitive of one's own has three more -- how to read what it
/// gives back, what its call's block may say, and which grammar rule opens it.
/// </para>
/// <para>
/// Every one of those has to grow when a new kind of surface is added, and nothing makes them.  Miss
/// one and the failure is quiet and specific: a shape that works everywhere except inside a CSG, or
/// one a primitive cannot give back for no reason anybody could guess.  That is exactly how the
/// primitive work shipped supporting eight kinds out of twenty-six -- a boundary that was an accident
/// rather than a rule, and which nothing complained about.
/// </para>
/// <para>
/// So these read the source and insist the lists agree.  Reading source is a blunt way to test, and it
/// is the right one here: the thing being checked is a fact about the text, and there is nowhere at
/// run time it could be asked about instead.
/// </para>
/// </summary>
[TestClass]
public class TestParserConsistency
{
    private static readonly string Root = FindProjectRoot();

    /// <summary>
    /// These are the words that stand in the same lists as the surface kinds without being one.
    /// </summary>
    private static readonly HashSet<string> NotSurfaces =
    [
        "interval", "surface", "object", "call", "background", "camera", "environment",
        "environmentBlock", "light", "name"
    ];

    [TestMethod]
    public void TestAGroupACsgAndASceneTakeTheSameSurfaces()
    {
        // Anything that may stand in one of these may stand in all of them.  Nothing enforces that but
        // this: the three lists are written out separately, one after another, in the grammar.
        HashSet<string> group = SurfacesIn(GrammarList("groupEntryClause"));
        HashSet<string> csg = SurfacesIn(GrammarList("csgEntryClause"));
        HashSet<string> scene = SurfacesIn(GrammarList("sceneEntryClause"));

        AssertSame(group, csg, "a group and a CSG");
        AssertSame(group, scene, "a group and a scene");
    }

    [TestMethod]
    public void TestEachHandlerKnowsEverythingItsGrammarAdmits()
    {
        // The grammar lets a thing be written and the handler decides what to do with it, so a kind
        // in one and not the other is a scene that parses and then falls over -- or worse, is quietly
        // ignored.
        foreach ((string list, string file, string method) in new[]
        {
            ("groupEntryClause", "Parser/LanguageParser.Groups.cs", "HandleGroupEntryClause"),
            ("csgEntryClause", "Parser/LanguageParser.CSG.cs", "HandleCsgEntryClause"),
            ("sceneEntryClause", "Parser/LanguageParser.Scenes.cs", "HandleSceneEntryClause")
        })
        {
            HashSet<string> admitted = GrammarList(list);
            HashSet<string> handled = CaseLabelsIn(file, method);

            AssertSame(admitted, handled, $"the {list} grammar and {method}");
        }
    }

    [TestMethod]
    public void TestAPrimitiveCanGiveBackEverySurfaceThatMayStandInAGroup()
    {
        // The one this was written for.  A primitive names the kind it gives back, and three separate
        // tables then have to know that kind: which grammar rule opens it, how to read it, and what
        // its call's block may say.  Any kind missing from any of them is a shape a scene may use
        // everywhere except from a primitive of its own.
        HashSet<string> surfaces = Normalize(SurfacesIn(GrammarList("groupEntryClause")));
        string primitives = Read("Parser/LanguageParser.Primitives.cs");

        // A primitive gives back more than surfaces.  These are the others, and they are listed here
        // rather than found, so that adding one is a deliberate act with a test to update.
        HashSet<string> everything = [..surfaces, "pigment", "material", "interior"];

        AssertSame(everything, Normalize(GrammarList("primitiveKindClause", asWords: true)),
            "what a primitive may say it gives back and what it may give back");

        // A pigment is read before the surfaces are looked for, having no rule of its own to open it,
        // so it is not among the arms that table wears.
        HashSet<string> readable = Normalize(KindsIn(primitives, "ParseThingOfKind"));

        readable.UnionWith(new[] { "pigment", "material", "interior" });

        AssertSame(everything, readable, "what a primitive may give back and what can be read back");

        // Only a surface has clauses that could be laid over one already made, and only a surface is
        // opened by a rule, so these two know the surfaces alone.
        HashSet<string> blocks = Normalize(KindsIn(primitives, "ParseCallBlock"));

        // That table names the others only to turn them away, having no block to read for them.
        blocks.ExceptWith(new[] { "pigment", "material", "interior" });

        AssertSame(surfaces, blocks,
            "the surfaces a primitive may give back and what a call's block understands");
        AssertSame(surfaces, Normalize(KindsIn(primitives, "StartClauseFor")),
            "the surfaces a primitive may give back and what has a grammar rule");
    }

    [TestMethod]
    public void TestEverySurfaceMayBeGivenAName()
    {
        // A fifth list, and the one that found the fault this test was written after: an isosurface
        // could not be given a name at all, so it could not be reused either.  It was added to every
        // list that lets a surface be *written* and missed on the one that lets it be *kept*.
        HashSet<string> expected = SurfacesIn(GrammarList("groupEntryClause"));
        string naming = GrammarClause("setThingToVariable");
        HashSet<string> named = [];

        foreach (string kind in expected)
        {
            string rule = "start" + char.ToUpperInvariant(kind[0]) + kind[1..] + "Clause";

            if (naming.Contains(rule, StringComparison.OrdinalIgnoreCase))
                named.Add(kind);
        }

        AssertSame(expected, named, "what may stand in a group and what may be given a name");
    }

    [TestMethod]
    public void TestNamingAThingKnowsEverySurfaceToo()
    {
        // A sixth list, and it was short by two: an isosurface and a heightfield could be written but
        // not kept.  The grammar admitted both; the switch that decides what to do with them did not,
        // and a scene naming either was told it had written something unsupported.
        HashSet<string> expected = Normalize(SurfacesIn(GrammarList("groupEntryClause")));
        HashSet<string> handled = Normalize(SwitchArmsIn(
            Read("Parser/LanguageParser.Variables.cs"), "HandleSetThingToVariableClause"));

        // That switch also names things that are not surfaces at all.
        handled.ExceptWith(new[] { "pigment", "material", "interior", "transform" });

        // Its two-word kinds are written as a first word and a guard on the second, so both halves
        // turn up separately and neither is the kind.
        handled.UnionWith(new[] { "smoothtriangle", "genericshape", "objectfile" });
        handled.ExceptWith(new[] { "smooth", "generic", "object", "triangle", "file", "shape" });
        handled.Add("triangle");

        AssertSame(expected, handled, "what may stand in a group and what may be named");
    }

    [TestMethod]
    public void TestReusingANamedSurfaceKnowsThemAllToo()
    {
        // `object` has its own list, written as resolver types rather than words, so it is counted
        // rather than compared name by name.  A kind added everywhere else and forgotten here is one
        // that cannot be given a name and used again.
        int kinds = SurfacesIn(GrammarList("groupEntryClause")).Count;
        int handled = Regex.Matches(
            Read("Parser/LanguageParser.Objects.cs"), @"case \w+Resolver:").Count;

        Assert.AreEqual(kinds, handled,
            $"reusing a named surface knows {handled} kinds where a group admits {kinds}");
    }

    /// <summary>
    /// Reads the tags an entry list in the grammar admits, or the words a kind clause offers.
    /// </summary>
    private static HashSet<string> GrammarList(string name, bool asWords = false)
    {
        string source = Read("Parser/LanguageParser.DSL.cs");
        int start = source.IndexOf($"        {name}:", StringComparison.Ordinal);

        Assert.IsTrue(start >= 0, $"the grammar has no {name}");

        string body = source[start..];

        if (!asWords)
        {
            body = body[..body.IndexOf("\n        ]", StringComparison.Ordinal)];

            return [..Regex.Matches(body, @"=> '([a-zA-Z]+)'").Select(match => match.Groups[1].Value)];
        }

        // A kind clause lists bare words, some of them in pairs, rather than tagged rules, and its
        // choices sit in brackets that have to be matched rather than searched for -- the clause does
        // not end at the close of them.
        string choices = ChoicesIn(body);
        HashSet<string> words = [];

        foreach (string option in choices.Split('|'))
        {
            string[] parts = Regex.Matches(option, @"[a-zA-Z]+")
                .Select(match => match.Value)
                .ToArray();

            if (parts.Length > 0)
                words.Add(string.Join(' ', parts));
        }

        return words;
    }

    /// <summary>
    /// Reads one clause of the grammar whole, for when what is wanted is the text rather than a list.
    /// </summary>
    /// <param name="name">The clause wanted.</param>
    /// <returns>The clause, as written.</returns>
    private static string GrammarClause(string name)
    {
        string source = Read("Parser/LanguageParser.DSL.cs");
        int start = source.IndexOf($"        {name}:", StringComparison.Ordinal);

        Assert.IsTrue(start >= 0, $"the grammar has no {name}");

        string body = source[start..];

        return body[..body.IndexOf("\n        }", StringComparison.Ordinal)];
    }

    /// <summary>
    /// Pulls out what stands between a clause's brackets, matching them rather than looking for the
    /// first close -- a kind clause carries an error message and more grammar after its choices.
    /// </summary>
    /// <param name="body">The clause, from its name onward.</param>
    /// <returns>What stands between its brackets.</returns>
    private static string ChoicesIn(string body)
    {
        int open = body.IndexOf('[');
        int depth = 0;

        for (int at = open; at < body.Length; at++)
        {
            if (body[at] == '[')
                depth++;
            else if (body[at] == ']' && --depth == 0)
                return body[(open + 1)..at];
        }

        Assert.Fail("the kind clause's brackets are unbalanced");

        return null;
    }

    /// <summary>
    /// Reads the case labels of one switch in one method.
    /// </summary>
    private static HashSet<string> CaseLabelsIn(string file, string method)
    {
        string source = Read(file);
        int start = source.IndexOf(method, StringComparison.Ordinal);

        Assert.IsTrue(start >= 0, $"{file} has no {method}");

        return [..Regex.Matches(source[start..], @"case ""([a-zA-Z]+)""")
            .TakeWhile(match => match.Index < 8000)
            .Select(match => match.Groups[1].Value)];
    }

    /// <summary>
    /// Reads the kinds one of the primitive tables knows about.
    /// </summary>
    private static HashSet<string> KindsIn(string source, string method)
    {
        return SwitchArmsIn(source, method);
    }

    /// <summary>
    /// Reads the labels of a switch written as expression arms rather than as cases.  Both shapes
    /// are in use here, and which one a switch wears says nothing about what it is for.
    /// </summary>
    /// <param name="source">The file to read.</param>
    /// <param name="method">The method holding the switch.</param>
    /// <returns>The labels it knows.</returns>
    private static HashSet<string> SwitchArmsIn(string source, string method)
    {
        // The definition rather than the first mention of it, which is generally a call.
        Match found = Regex.Match(source, "private[^\n]*\\b" + method + "\\(");

        Assert.IsTrue(found.Success, $"there is no {method} to read");

        string body = source[found.Index..];
        int ends = body.IndexOf("\n    }", StringComparison.Ordinal);

        Assert.IsTrue(ends > 0, $"could not find the end of {method}");

        return [..Regex.Matches(body[..ends], "\"([a-zA-Z ]+)\"\\s*(?:or\\s|=>|when\\b)")
            .Select(match => match.Groups[1].Value)];
    }

    /// <summary>
    /// Drops the words that keep company with the surface kinds without being one.
    /// </summary>
    private static HashSet<string> SurfacesIn(HashSet<string> tags)
    {
        return [..tags.Where(tag => !NotSurfaces.Contains(tag))];
    }

    /// <summary>
    /// Puts kinds into one form so the lists may be compared: case and spaces do not matter, and the
    /// three words for cutting one surface against another all mean the same kind of thing.
    /// </summary>
    private static HashSet<string> Normalize(HashSet<string> kinds)
    {
        HashSet<string> same = [];

        foreach (string kind in kinds)
        {
            string plain = kind.Replace(" ", "").ToLowerInvariant();

            same.Add(plain is "union" or "difference" or "intersection" ? "csg" : plain);
        }

        return same;
    }

    /// <summary>
    /// Complains in a way that says which list is short and of what.
    /// </summary>
    private static void AssertSame(HashSet<string> expected, HashSet<string> found, string what)
    {
        List<string> missing = [..expected.Except(found).Order()];
        List<string> extra = [..found.Except(expected).Order()];

        Assert.IsTrue(missing.Count == 0 && extra.Count == 0,
            $"{what} disagree.\n  missing: {Say(missing)}\n  unexpected: {Say(extra)}");
    }

    private static string Say(List<string> names)
    {
        return names.Count == 0 ? "nothing" : string.Join(", ", names);
    }

    private static string Read(string relative)
    {
        return File.ReadAllText(Path.Combine(Root, relative));
    }

    /// <summary>
    /// Finds the project root by climbing until the parser is in sight.
    /// </summary>
    private static string FindProjectRoot()
    {
        DirectoryInfo directory = new (AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Parser")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "could not find the project root");

        return directory.FullName;
    }
}
