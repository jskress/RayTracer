using System.Reflection;
using System.Text.RegularExpressions;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the libraries that ship with the ray tracer.
/// <para>
/// A shipped library is documentation that runs.  Every other kind of example can go stale quietly --
/// a snippet in a page is only read by people -- but this one is imported by scenes, so a name that
/// stops resolving is a scene that stops rendering.  What is checked here is therefore not that the
/// skies look nice, which no test can say, but that every name the library holds out is a name a scene
/// can actually take, and that the file travels inside the assembly so that installing it works
/// wherever the ray tracer was installed to.
/// </para>
/// </summary>
[TestClass]
public class TestShippedLibraries
{
    private string _directory;

    [TestInitialize]
    public void CreateWorkingDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"library-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveWorkingDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Walks up from wherever the tests are running to find the root of the repository.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            DirectoryInfo directory = new (AppContext.BaseDirectory);

            while (directory is not null &&
                   !File.Exists(Path.Combine(directory.FullName, "RayTracer.csproj")))
                directory = directory.Parent;

            Assert.IsNotNull(directory, "could not find the repository root from the test's location");

            return directory.FullName;
        }
    }

    /// <summary>
    /// The libraries as they sit in the repository, which is where they are written and read.
    /// </summary>
    private static string[] Shipped => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "Libraries"), "*.igl")
        .Order()
        .ToArray();

    [TestMethod]
    public void TestEveryShippedLibraryTravelsInsideTheAssembly()
    {
        // They are embedded rather than copied beside the program, so that installing them does not
        // depend on where the ray tracer was put or what directory it was run from.  A library added
        // to the folder and left out of the project file would install as nothing at all.
        Assembly assembly = typeof(LibraryLocator).Assembly;
        string prefix = $"{assembly.GetName().Name}.Libraries.";
        HashSet<string> carried = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix))
            .Select(name => name[prefix.Length..])
            .ToHashSet();

        foreach (string library in Shipped)
        {
            Assert.IsTrue(carried.Contains(Path.GetFileName(library)),
                $"{Path.GetFileName(library)} is in the Libraries folder but not in the assembly");
        }

        Assert.IsNotEmpty(Shipped, "there are no shipped libraries at all");
    }

    [TestMethod]
    public void TestEveryNameAShippedLibraryHoldsCanBeImported()
    {
        // The whole promise of a library, checked by keeping it: every name it defines is asked for by
        // name, all at once, and the scene has to render.  A definition that was renamed, or that
        // leans on something the library forgot to define, fails here rather than in somebody's scene.
        foreach (string library in Shipped)
        {
            string[] names = Regex
                .Matches(File.ReadAllText(library), @"(?m)^([A-Za-z_][A-Za-z0-9_]*)\s*=")
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .Order()
                .ToArray();

            Assert.IsNotEmpty(names, $"{Path.GetFileName(library)} defines nothing");

            string scene = Path.Combine(_directory, "scene.igl");

            File.Copy(library, Path.Combine(_directory, Path.GetFileName(library)), true);
            File.WriteAllText(scene,
                $"import '{Path.GetFileNameWithoutExtension(library)}' {{ {string.Join(", ", names)} }}\n" +
                "camera { location [0, 1, -5]  look at [0, 0, 0] }\n" +
                "point light { location [-5, 5, -5] }\n" +
                "sphere { material { pigment Red } }\n");

            string error = Render(scene);

            Assert.IsNull(error, $"{Path.GetFileName(library)}: {error}");
        }
    }

    [TestMethod]
    public void TestEverySkyInTheDaylightLibraryLightsAScene()
    {
        // A sky and the light that goes with it are two halves of one thing, and the pairing is by
        // name -- "ClearMorning" and "ClearMorningLight".  A sky whose light was misnamed would import
        // perfectly well and then leave the scene lit by nothing, so the pairs are rendered rather
        // than merely read.
        string library = Shipped.First(path => Path.GetFileName(path) == "daylight.igl");
        string[] skies = Regex
            .Matches(File.ReadAllText(library), @"(?m)^([A-Za-z]+) = pigment ")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        Assert.IsTrue(skies.Length >= 6, $"expected a spread of skies, and found {skies.Length}");

        File.Copy(library, Path.Combine(_directory, "daylight.igl"), true);

        foreach (string sky in skies)
        {
            string scene = Path.Combine(_directory, "scene.igl");

            File.WriteAllText(scene, $$"""
                import 'daylight' { {{sky}}, {{sky}}Light }
                context { angles are degrees }
                camera { location [0, 2, -8]  look at [0, 1, 0]  field of view 45 }
                background {{sky}}
                light {{sky}}Light
                plane { material { pigment [0.45, 0.45, 0.42] } }
                sphere { material { pigment [0.8, 0.3, 0.2] }  translate Y 1 }
                """);

            Assert.IsNull(Render(scene), $"{sky} and {sky}Light should make a scene together");
        }
    }

    /// <summary>
    /// The trees the library holds out, written down rather than read out of the library.
    /// <para>
    /// Naming them here is the point.  A test that learns the species from the file it is testing
    /// cannot notice a rename -- it simply tests whatever it finds -- and these names are a promise to
    /// every scene that imports them, so changing one should be a failure and not a shrug.
    /// </para>
    /// </summary>
    private static readonly string[] Species = ["Elm", "Oak", "Birch"];

    [TestMethod]
    public void TestEveryTreeGrowsInEverySeason()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "trees.igl"),
            Path.Combine(_directory, "trees.igl"), true);

        foreach (string tree in Species)
        {
            foreach (string season in new[] { "summer", "autumn", "fall", "winter", "spring" })
                Assert.IsNull(Grow(tree, season), $"{tree} should grow in {season}");
        }
    }

    [TestMethod]
    public void TestEachSeasonLooksLikeItselfAndNoOther()
    {
        // That a tree renders in winter says nothing.  A season whose name stopped matching falls
        // through to the default and grows a spring tree instead, which renders perfectly well and is
        // wrong -- and comparing it against summer would not notice, since spring does not look like
        // summer either.  So every season is held against every other, which is the only comparison
        // that catches one quietly becoming another.
        //
        // "fall" is the exception and is held the other way: it names the same arm as "autumn", so the
        // two must come out *identical*.  Drop one of the two words and this is what says so.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "trees.igl"),
            Path.Combine(_directory, "trees.igl"), true);

        string[] seasons = ["summer", "autumn", "winter", "spring"];

        foreach (string tree in Species)
        {
            Canvas[] pictures = seasons.Select(season => Picture(tree, season)).ToArray();

            for (int one = 0; one < seasons.Length; one++)
            {
                for (int other = one + 1; other < seasons.Length; other++)
                {
                    Assert.IsTrue(Differs(pictures[one], pictures[other]),
                        $"a {tree} in {seasons[other]} looks exactly like one in {seasons[one]}");
                }
            }

            Assert.IsFalse(Differs(pictures[1], Picture(tree, "fall")),
                $"a {tree} in the fall should be the same as one in the autumn");
        }
    }

    /// <summary>
    /// Grows one tree of one species in one season, and hands back whatever stopped it.
    /// </summary>
    private string Grow(string tree, string season)
    {
        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, $$"""
            import 'trees' { {{tree}} }
            context { angles are degrees  no gamma }
            camera { location [10, 5, -16]  look at [0, 4, 0]  field of view 45 }
            point light { location [-10, 14, -12] }
            background [0.5, 0.6, 0.8]
            object {{tree}}(8, '{{season}}', 2)
            """);

        return Render(scene);
    }

    /// <summary>
    /// Grows one and hands back the picture of it.
    /// </summary>
    private Canvas Picture(string tree, string season)
    {
        Assert.IsNull(Grow(tree, season), $"{tree} should grow in {season}");

        return new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
    }

    /// <summary>
    /// Reports whether two pictures differ anywhere worth noticing.
    /// </summary>
    private static bool Differs(Canvas first, Canvas second)
    {
        for (int x = 0; x < first.Width; x++)
        {
            for (int y = 0; y < first.Height; y++)
            {
                Color one = first.GetPixel(x, y);
                Color other = second.GetPixel(x, y);

                if (Math.Abs(one.Red - other.Red) + Math.Abs(one.Green - other.Green) +
                    Math.Abs(one.Blue - other.Blue) > 0.05)
                    return true;
            }
        }

        return false;
    }

    [TestMethod]
    public void TestATreeKeepsItsOwnWorkings()
    {
        // The reason the library could not be written before the scoping went in.  A tree is a limb and
        // a foliage and a wilt and a sway, and a scene that asked for an elm used to get all of them.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "trees.igl"),
            Path.Combine(_directory, "trees.igl"), true);

        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, """
            import 'trees' { Elm }
            camera { location [12, 6, -18]  look at [0, 4, 0] }
            point light { location [-10, 14, -12] }
            object Elm(8)
            sphere { scale TreeShrink('elm') }
            """);

        string error = Render(scene);

        Assert.IsNotNull(error, "the library's own workings should not have reached the scene");
        StringAssert.Contains(error, "TreeShrink");
    }

    /// <summary>
    /// Renders a scene small and fast, and hands back whatever stopped it.
    /// </summary>
    private string Render(string path)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            ImageRenderer renderer = new LanguageParser(path).Parse();

            if (renderer is null)
                return captured.ToString();

            renderer.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "out.png"), Width = 40, Height = 30
            });

            return captured.ToString().Contains("Error") ? captured.ToString() : null;
        }
        catch (Exception exception)
        {
            return exception.ToString();
        }
        finally
        {
            Console.SetOut(was);
        }
    }
}
