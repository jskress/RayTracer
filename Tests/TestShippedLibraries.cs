using System.Reflection;
using System.Text.RegularExpressions;
using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
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

    [TestMethod]
    public void TestEverySkyKeepsItsSunOnTheSideTheDocumentationSaysItIs()
    {
        // The documentation tells an author to face what they want lit toward +Z, because every sky
        // here has its sun on that side.  That is a promise about six numbers in one file, and a
        // number is exactly the sort of thing that gets nudged -- so it is checked rather than
        // trusted.  An author who followed the advice and got a silhouette would have no way of
        // knowing the advice had gone stale.
        MatchCollection suns = Regex.Matches(
            File.ReadAllText(Shipped.First(path => Path.GetFileName(path) == "daylight.igl")),
            @"(?m)^([A-Za-z]+) = pigment physical sky \{\s*sun elevation ([-\d.]+)\s+sun azimuth ([-\d.]+)");

        Assert.IsTrue(suns.Count >= 6, $"expected the physical skies, and found {suns.Count}");

        foreach (Match sun in suns)
        {
            PhysicalSkyPigment sky = new ()
            {
                SunElevation = double.Parse(sun.Groups[2].Value),
                SunAzimuth = double.Parse(sun.Groups[3].Value)
            };

            Assert.IsTrue(sky.TowardSun.Z >= 0,
                $"{sun.Groups[1].Value} has its sun toward {sky.TowardSun}, which the documentation " +
                "says no sky here does; either move it back or rewrite what libraries.md promises");
        }
    }

    [TestMethod]
    public void TestAnAzimuthPointsWhereTheDocumentationSaysItDoes()
    {
        // The table in libraries.md, held against the thing it describes.  A compass with -Z for north.
        foreach ((double azimuth, double x, double z) in new[]
                 {
                     (0.0, 0.0, -1.0), (90.0, 1.0, 0.0), (180.0, 0.0, 1.0), (270.0, -1.0, 0.0)
                 })
        {
            Vector toward = new PhysicalSkyPigment { SunElevation = 0, SunAzimuth = azimuth }.TowardSun;

            Assert.IsTrue(Math.Abs(toward.X - x) < 0.001 && Math.Abs(toward.Z - z) < 0.001,
                $"an azimuth of {azimuth} points {toward}, and the documentation says [{x}, 0, {z}]");
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
    private static readonly string[] Species = ["Elm", "Oak", "Birch", "Fir"];

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
            // A fir makes a different promise from the others and is held to that one.  It keeps its
            // needles, so spring, summer and autumn are the same tree -- and it takes snow, so winter
            // is not.  Both halves matter: a fir that differed in autumn would have lost its needles,
            // and one that did not differ in winter would have lost its snow.
            if (tree == "Fir")
            {
                Canvas evergreen = Picture(tree, "summer");

                foreach (string season in new[] { "autumn", "spring" })
                {
                    Assert.IsFalse(Differs(evergreen, Picture(tree, season)),
                        $"a fir keeps its needles and should look the same in {season}");
                }

                Assert.IsTrue(Differs(evergreen, Picture(tree, "winter")),
                    "a fir should carry snow in winter");

                continue;
            }

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
    private string Grow(string tree, string season, int? variant = 2)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        // A null leaves that argument off altogether, which is how the defaults get tested.  They are
        // positional, so leaving the season off leaves the variant off too.
        string call = $"{tree}(8" +
                      (season is null ? "" : $", \'{season}\'") +
                      (season is null || variant is null ? "" : $", {variant}") + ")";

        File.WriteAllText(scene, $$"""
            import 'trees' { {{tree}} }
            context { angles are degrees  no gamma }
            camera { location [10, 5, -16]  look at [0, 4, 0]  field of view 45 }
            point light { location [-10, 14, -12] }
            background [0.5, 0.6, 0.8]
            object {{call}}
            """);

        return Render(scene);
    }

    /// <summary>
    /// Grows one and hands back the picture of it.
    /// </summary>
    private Canvas Picture(string tree, string season, int? variant = 2)
    {
        Assert.IsNull(Grow(tree, season, variant), $"{tree} should grow in {season}");

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
    public void TestATreeAskedForWithNoSeasonGrowsInSummer()
    {
        // Every tree takes the season last and defaults it, so most scenes never write one -- which
        // makes the default the season most trees in most scenes are actually in, and it should be the
        // same one for all of them.  A single tree defaulting differently is invisible while nothing
        // depends on the season and becomes a snow-laden fir in a summer stand the moment something
        // does.  That is not hypothetical: the fir defaulted to winter, harmlessly, right up until
        // winter grew snow.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "trees.igl"),
            Path.Combine(_directory, "trees.igl"), true);

        foreach (string tree in Species)
        {
            Assert.IsFalse(Differs(Picture(tree, null), Picture(tree, "summer", null)),
                $"a {tree} asked for with no season should be the same as one in summer");
        }
    }

    /// <summary>
    /// What the undergrowth library holds out, written down here rather than read out of the file, for
    /// the same reason the tree species are: a test that learns the names from the thing it is testing
    /// cannot notice a rename.
    /// </summary>
    private static readonly string[] Plants =
        ["Grass", "GrassCircle", "Tuft", "Boxwood", "Bramble", "Lavender"];

    /// <summary>
    /// How big to ask for each, since these are not all measured in the same thing: the first number
    /// to <c>Grass</c> is how far across a patch reaches, and to everything else it is a height.
    /// </summary>
    private static string SizeOf(string plant) => plant switch
    {
        "Grass" => "2",
        "Tuft" => "0.5",
        _ => "1.1"
    };

    /// <summary>
    /// Grows one plant in one season, and hands back whatever stopped it.
    /// </summary>
    private string Sprout(string plant, string season)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string call = season is null
            ? $"{plant}({SizeOf(plant)})"
            : $"{plant}({SizeOf(plant)}, '{season}', 3)";

        File.WriteAllText(scene, $$"""
            import 'undergrowth' { {{plant}} }
            context { angles are degrees  no gamma }
            camera { location [1.4, 1.1, -2.4]  look at [0, 0.28, 0]  field of view 46 }
            point light { location [-4, 6, -5] }
            background [0.5, 0.6, 0.8]
            plane { material { pigment [0.3, 0.3, 0.3] } }
            object {{call}}
            """);

        return Render(scene, 90, 70);
    }

    /// <summary>
    /// Grows one and hands back the picture of it.
    /// </summary>
    private Canvas Sprouted(string plant, string season)
    {
        Assert.IsNull(Sprout(plant, season), $"{plant} should grow in {season}");

        return new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
    }

    [TestMethod]
    public void TestEveryPlantGrowsInEverySeason()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "undergrowth.igl"),
            Path.Combine(_directory, "undergrowth.igl"), true);

        foreach (string plant in Plants)
        {
            foreach (string season in new[] { "summer", "autumn", "fall", "winter", "spring" })
                Assert.IsNull(Sprout(plant, season), $"{plant} should grow in {season}");

            Assert.IsNull(Sprout(plant, null), $"{plant} should grow with no season named");
        }
    }

    [TestMethod]
    public void TestEachPlantsSeasonsLookLikeThemselves()
    {
        // The same rule the trees are held to, and it matters more here: what a season does differs by
        // plant, so there is no single change to look for.  Grass goes tawny and lies down, a bramble
        // fruits and then goes bare, lavender flowers and fades -- and every one of those has to be a
        // real difference rather than a word that fell through to the wrong arm.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "undergrowth.igl"),
            Path.Combine(_directory, "undergrowth.igl"), true);

        string[] seasons = ["summer", "autumn", "winter", "spring"];

        foreach (string plant in Plants)
        {
            // A boxwood is evergreen and so makes the promise the fir makes: three seasons alike, and
            // snow in the fourth.  Both halves are held, since one without the other is a boxwood that
            // has either lost its leaves or lost its snow.
            if (plant == "Boxwood")
            {
                Canvas evergreen = Sprouted(plant, "summer");

                foreach (string season in new[] { "autumn", "spring" })
                {
                    Assert.IsFalse(Differs(evergreen, Sprouted(plant, season)),
                        $"a boxwood is evergreen and should look the same in {season}");
                }

                Assert.IsTrue(Differs(evergreen, Sprouted(plant, "winter")),
                    "a boxwood should carry snow in winter");

                continue;
            }

            Canvas[] pictures = seasons.Select(season => Sprouted(plant, season)).ToArray();

            for (int one = 0; one < seasons.Length; one++)
            {
                for (int other = one + 1; other < seasons.Length; other++)
                {
                    Assert.IsTrue(Differs(pictures[one], pictures[other]),
                        $"a {plant} in {seasons[other]} looks exactly like one in {seasons[one]}");
                }
            }

            Assert.IsFalse(Differs(pictures[1], Sprouted(plant, "fall")),
                $"a {plant} in the fall should be the same as one in the autumn");
        }
    }

    [TestMethod]
    public void TestGrassTurnsTheColorTheSeasonSaysItDoes()
    {
        // Held apart from the test above because that one asks only whether the seasons *differ*, and
        // grass differs in two ways at once: it changes color and it lies down.  Take the color away
        // and the pictures still differ, by shape alone, so that test goes on passing while a summer
        // green August lawn stands in for a February one.  This asks what the library actually
        // promises: green while it is growing, and not green once it is not.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "undergrowth.igl"),
            Path.Combine(_directory, "undergrowth.igl"), true);

        foreach ((string season, bool green) in new[]
                 {
                     ("summer", true), ("spring", true), ("autumn", false), ("winter", false)
                 })
        {
            string scene = Path.Combine(_directory, "scene.igl");

            // Straight down at a patch, so nothing but grass and the gaps between it are in shot, and
            // the ground is a gray that leans neither way.
            File.WriteAllText(scene, $$"""
                import 'undergrowth' { Grass }
                context { angles are degrees  no gamma }
                camera { location [0, 2.4, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 50 }
                point light { location [-3, 6, -3] }
                background [0.5, 0.5, 0.5]
                plane { material { pigment [0.5, 0.5, 0.5] } }
                object Grass(4, '{{season}}', 1)
                """);

            Assert.IsNull(Render(scene, 110, 110), $"grass should render in {season}");

            Canvas image = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
            double lean = 0;

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Color pixel = image.GetPixel(x, y);

                    lean += pixel.Green - pixel.Red;
                }
            }

            Assert.AreEqual(green, lean > 0,
                $"grass in {season} leans {(lean > 0 ? "green" : "brown")}, and should not");
        }
    }

    [TestMethod]
    public void TestHowMuchGrassThereIsCanBeTurnedDown()
    {
        // The knob the documentation tells an author to reach for when a scene has become slow, so it
        // has to do something.  Fewer tufts is less of the picture covered, and the three settings must
        // give three different pictures rather than one picture and two claims about it.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "undergrowth.igl"),
            Path.Combine(_directory, "undergrowth.igl"), true);

        List<double> covered = [];

        foreach (string density in new[] { "0.4", "0.7", "1" })
        {
            string scene = Path.Combine(_directory, "scene.igl");

            File.WriteAllText(scene, $$"""
                import 'undergrowth' { Grass }
                context { angles are degrees  no gamma }
                camera { location [0, 1.6, -2.6]  look at [0, 0, 0]  field of view 50 }
                point light { location [-4, 6, -5] }
                background [0.5, 0.6, 0.8]
                plane { material { pigment [0.9, 0.1, 0.1] } }
                object Grass(3, 'summer', 1, 0.3, {{density}})
                """);

            Assert.IsNull(Render(scene, 120, 90), $"grass at a density of {density} should render");

            Canvas image = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
            int green = 0;

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Color pixel = image.GetPixel(x, y);

                    // The ground is red on purpose, so a pixel is either grass or a gap.
                    if (pixel.Green > pixel.Red)
                        green++;
                }
            }

            covered.Add(green);
        }

        Assert.IsTrue(covered[0] < covered[1] && covered[1] < covered[2],
            $"more density should cover more ground, and got {string.Join(", ", covered)}");
    }

    [TestMethod]
    public void TestTheUndergrowthKeepsItsOwnWorkings()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "undergrowth.igl"),
            Path.Combine(_directory, "undergrowth.igl"), true);

        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, """
            import 'undergrowth' { Boxwood }
            camera { location [0, 1, -4]  look at [0, 0.4, 0] }
            point light { location [-5, 5, -5] }
            object Boxwood(1)
            sphere { scale UnderBladeHeight('summer') }
            """);

        string error = Render(scene);

        Assert.IsNotNull(error, "the library's own workings should not have reached the scene");
        StringAssert.Contains(error, "UnderBladeHeight");
    }

    /// <summary>
    /// What the rocks library holds out, written down here rather than read out of the file.
    /// </summary>
    private static readonly string[] Stones = ["Boulder", "Cobble", "Scree"];

    /// <summary>
    /// Grows one stone in one season, and hands back whatever stopped it.
    /// </summary>
    private string Quarry(string stone, string season)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string size = stone == "Scree" ? "1.6" : "1";
        string call = season is null
            ? $"{stone}({size})"
            : $"{stone}({size}, '{season}', 3)";

        File.WriteAllText(scene, $$"""
            import 'rocks' { {{stone}} }
            context { angles are degrees  no gamma }
            camera { location [1.2, 1.1, -2.2]  look at [0, 0.16, 0]  field of view 46 }
            point light { location [-4, 6, -5] }
            background [0.5, 0.6, 0.8]
            plane { material { pigment [0.3, 0.3, 0.3] } }
            object {{call}}
            """);

        return Render(scene, 90, 70);
    }

    /// <summary>
    /// Makes one stone of one variant in one season and hands back the picture.
    /// </summary>
    private Canvas StoneIn(string stone, string season, int variant)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string size = stone == "Scree" ? "1.6" : "1";

        File.WriteAllText(scene, $$"""
            import 'rocks' { {{stone}} }
            context { angles are degrees  no gamma }
            camera { location [1.2, 1.1, -2.2]  look at [0, 0.16, 0]  field of view 46 }
            point light { location [-4, 6, -5] }
            background [0.5, 0.6, 0.8]
            plane { material { pigment [0.3, 0.3, 0.3] } }
            object {{stone}}({{size}}, '{{season}}', {{variant}})
            """);

        Assert.IsNull(Render(scene, 90, 70), $"{stone} {variant} should be made in {season}");

        return new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
    }

    [TestMethod]
    public void TestEveryStoneIsMadeInEverySeason()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "rocks.igl"),
            Path.Combine(_directory, "rocks.igl"), true);

        foreach (string stone in Stones)
        {
            foreach (string season in new[] { "summer", "autumn", "fall", "winter", "spring" })
                Assert.IsNull(Quarry(stone, season), $"{stone} should be made in {season}");

            Assert.IsNull(Quarry(stone, null), $"{stone} should be made with no season named");
        }
    }

    [TestMethod]
    public void TestAStoneTakesSnowInWinterAndInNoOtherSeason()
    {
        // The whole of what a season does to a rock, and it is worth holding to both halves.  A stone
        // is not deciduous, so three of the four must be the *same* stone -- a rock that changed in
        // autumn would be a rock pretending to be a leaf -- and the fourth must not be, or the word
        // has been taken and ignored, which is the fault the fir was pulled up for.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "rocks.igl"),
            Path.Combine(_directory, "rocks.igl"), true);

        foreach (string stone in Stones)
        {
            // Snow lies unevenly on purpose -- about a third of stones catch none, so that a scree
            // does not read as a field of eggs.  That makes "this stone differs in winter" the wrong
            // thing to assert: pick the wrong variant and it is false about a library working just
            // as intended.  So several are asked and what is held is that *most* take snow.
            //
            // Deliberately not tested: what that fraction is.  A first attempt asked that not all six
            // were snowy, and it could not fail reliably -- with only six samples the count lands on
            // five by chance often enough that the assertion passes while the rule it checks is gone.
            // Enough samples to make it sound would be enough renders to make the suite slow, and the
            // fraction is a tuning choice like how a birch branches.  The sweep guards that.
            int snowy = 0;

            for (int variant = 1; variant <= 6; variant++)
            {
                Canvas bare = StoneIn(stone, "summer", variant);

                foreach (string season in new[] { "autumn", "spring" })
                {
                    Assert.IsFalse(Differs(bare, StoneIn(stone, season, variant)),
                        $"a {stone} should be the same stone in {season} as in summer");
                }

                if (Differs(bare, StoneIn(stone, "winter", variant)))
                    snowy++;
            }

            Assert.IsTrue(snowy >= 3,
                $"only {snowy} of six {stone} variants took any snow in winter");

        }
    }

    [TestMethod]
    public void TestOneStoneIsNotAnother()
    {
        // The variant has to do something, or a scree of them is one stone repeated -- which is what a
        // field of identical rocks announces at a glance.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "rocks.igl"),
            Path.Combine(_directory, "rocks.igl"), true);

        foreach (string stone in new[] { "Boulder", "Cobble" })
        {
            List<Canvas> stones = [];

            foreach (int variant in new[] { 1, 2, 3 })
            {
                string scene = Path.Combine(_directory, "scene.igl");

                File.WriteAllText(scene, $$"""
                    import 'rocks' { {{stone}} }
                    context { angles are degrees  no gamma }
                    camera { location [1.2, 1.1, -2.2]  look at [0, 0.16, 0]  field of view 46 }
                    point light { location [-4, 6, -5] }
                    background [0.5, 0.6, 0.8]
                    plane { material { pigment [0.3, 0.3, 0.3] } }
                    object {{stone}}(1, 'summer', {{variant}})
                    """);

                Assert.IsNull(Render(scene, 90, 70), $"{stone} {variant} should render");

                stones.Add(new ImageFile(Path.Combine(_directory, "out.png")).Load()[0]);
            }

            for (int one = 0; one < stones.Count; one++)
            {
                for (int other = one + 1; other < stones.Count; other++)
                {
                    Assert.IsTrue(Differs(stones[one], stones[other]),
                        $"{stone} variants {one + 1} and {other + 1} came out the same stone");
                }
            }
        }
    }

    [TestMethod]
    public void TestTheRocksKeepTheirOwnWorkings()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "rocks.igl"),
            Path.Combine(_directory, "rocks.igl"), true);

        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, """
            import 'rocks' { Boulder }
            camera { location [0, 1, -4]  look at [0, 0.2, 0] }
            point light { location [-5, 5, -5] }
            object Boulder(1)
            object RockCap('winter', 1, 1, 1)
            """);

        string error = Render(scene);

        Assert.IsNotNull(error, "the library's own workings should not have reached the scene");
        StringAssert.Contains(error, "RockCap");
    }

    /// <summary>
    /// What the fire library holds out, written down rather than read out of the file.
    /// </summary>
    private static readonly string[] Fires = ["Flame", "Campfire", "Torch", "Embers"];

    /// <summary>
    /// Lights one fire and hands back the picture.  A fire is a medium, so the scene has to say how
    /// many places along a crossing to stop and ask; the library cannot say it.
    /// </summary>
    private Canvas Lit(string fire, double size, int variant)
    {
        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, $$"""
            import 'fire' { {{fire}} }
            context { angles are degrees  no gamma  medium samples 80 }
            camera { location [0, 0.55, -2.2]  look at [0, 0.5, 0]  field of view 44 }
            background Black
            object {{fire}}({{size}}, {{variant}})
            """);

        Assert.IsNull(Render(scene, 100, 100), $"{fire} should light");

        return new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
    }

    [TestMethod]
    public void TestEveryFireLights()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "fire.igl"),
            Path.Combine(_directory, "fire.igl"), true);

        foreach (string fire in Fires)
            Assert.IsNotNull(Lit(fire, 1, 1), $"{fire} should light");
    }

    [TestMethod]
    public void TestAFlameIsHotterAtItsFootThanAtItsTip()
    {
        // The whole reason `emission` was taught to take a pigment.  A flame is white at the heart and
        // red at the tip, and one flat color cannot say it -- so this is the claim that would quietly
        // stop being true if the pigment were dropped and a color put back.
        //
        // Read as *how blue* rather than how bright: the foot is white-hot and so carries blue, the tip
        // is red and carries almost none.  Brightness alone would not do, since a flame is thicker at
        // the foot and would be brighter there whatever color it was.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "fire.igl"),
            Path.Combine(_directory, "fire.igl"), true);

        Canvas flame = Lit("Flame", 1, 1);

        // Where the flame actually is, rather than where it is assumed to be.  Hard-coded rows are how
        // this test first failed: they sat below the flame entirely and compared nothing with nothing,
        // which reads exactly like the feature being broken.
        List<int> lit = [];

        for (int y = 0; y < flame.Height; y++)
        {
            double red = 0;

            for (int x = 0; x < flame.Width; x++)
                red += flame.GetPixel(x, y).Red;

            if (red > 1)
                lit.Add(y);
        }

        Assert.IsTrue(lit.Count >= 10, $"the flame covers only {lit.Count} rows, so there is little to read");

        double tip = Blueness(flame, lit[0], lit[lit.Count / 5]);
        double foot = Blueness(flame, lit[^(lit.Count / 5 + 1)], lit[^1]);

        Assert.IsTrue(foot > tip * 1.5,
            $"the foot should be the whiter, and got {foot:F3} against {tip:F3} at the tip");
    }

    /// <summary>
    /// How much blue a band of the picture carries next to its red, which is how white-hot it is.
    /// <para>
    /// Summed over the band and then divided, rather than divided per pixel and averaged: a flame's
    /// edge is nearly black, and a per-pixel ratio there is noise divided by noise.
    /// </para>
    /// </summary>
    private static double Blueness(Canvas image, int from, int to)
    {
        double red = 0;
        double blue = 0;

        for (int y = from; y <= to; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                red += image.GetPixel(x, y).Red;
                blue += image.GetPixel(x, y).Blue;
            }
        }

        return blue / (red + 0.0001);
    }

    [TestMethod]
    public void TestOneFireIsNotAnother()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "fire.igl"),
            Path.Combine(_directory, "fire.igl"), true);

        foreach (string fire in new[] { "Flame", "Campfire" })
        {
            Assert.IsTrue(Differs(Lit(fire, 1, 1), Lit(fire, 1, 2)),
                $"two {fire} variants came out the same fire");
        }
    }

    [TestMethod]
    public void TestTheFiresKeepTheirOwnWorkings()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "fire.igl"),
            Path.Combine(_directory, "fire.igl"), true);

        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, """
            import 'fire' { Flame }
            camera { location [0, 0.5, -3]  look at [0, 0.5, 0] }
            point light { location [-5, 5, -5] }
            object Flame(1)
            sphere { scale FireLobe(0, 0, 0, 0, 0, 1, 1) }
            """);

        string error = Render(scene);

        Assert.IsNotNull(error, "the library's own workings should not have reached the scene");
        StringAssert.Contains(error, "FireLobe");
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
    /// What the buildings library holds out, written down here rather than read out of the file, for
    /// the same reason the tree species are: a test that learns the names from the thing it is testing
    /// cannot notice a rename.
    /// </summary>
    private static readonly string[] Buildings = ["House", "Row", "Tower"];

    /// <summary>
    /// How big to ask for each, since the first number does not mean the same thing to all three: to a
    /// <c>Row</c> it is how many houses, and to the other two it is a height.
    /// </summary>
    private static string BuildingSizeOf(string building) => building switch
    {
        "Row" => "3",
        "Tower" => "9",
        _ => "4"
    };

    /// <summary>
    /// Raises one building, from in front of it unless asked otherwise, and hands back whatever
    /// stopped it.  The camera looks at the -Z face because that is the face these put their windows
    /// and doors on.
    /// </summary>
    private string Raise(string building, string season, int? variant = 2, bool fromBehind = false)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string call = season is null
            ? $"{building}({BuildingSizeOf(building)})"
            : variant is null
                ? $"{building}({BuildingSizeOf(building)}, '{season}')"
                : $"{building}({BuildingSizeOf(building)}, '{season}', {variant})";
        string where = fromBehind ? "[0, 9, 26]" : "[0, 9, -26]";

        File.WriteAllText(scene, $$"""
            import 'buildings' { {{building}} }
            context { angles are degrees  no gamma }
            camera { location {{where}}  look at [0, 4, 0]  field of view 46 }
            point light { location [-9, 20, -12] }
            background [0.5, 0.6, 0.8]
            plane { material { pigment [0.3, 0.3, 0.3] } }
            object {{call}}
            """);

        // Big enough to resolve a pane of glass.  At 110 by 84 a building is fifty pixels tall and a
        // window a couple, which was fine while a window was one dark sheet -- but the windows library
        // divides them with glazing bars, and six panes across two pixels average out to the color of
        // the frame.  The glass then counts as nothing and a test that looks for it reports a building
        // with no windows on either side.
        return Render(scene, 320, 240);
    }

    /// <summary>
    /// Raises one and hands back the picture of it.
    /// </summary>
    private Canvas Raised(string building, string season, int? variant = 2, bool fromBehind = false)
    {
        Assert.IsNull(Raise(building, season, variant, fromBehind),
            $"{building} should stand in {season ?? "no season at all"}");

        return new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
    }

    [TestMethod]
    public void TestEveryBuildingStandsInEverySeason()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "buildings.igl"),
            Path.Combine(_directory, "buildings.igl"), true);

        foreach (string building in Buildings)
        {
            foreach (string season in new[] { "summer", "autumn", "fall", "winter", "spring" })
                Assert.IsNull(Raise(building, season), $"{building} should stand in {season}");
        }
    }

    [TestMethod]
    public void TestWinterPutsSnowOnEveryBuilding()
    {
        // The season is the one word these take that has to *do* something, and it very nearly did
        // not: the snow was laid at the roof slope's own offset rather than pushed out along that
        // slope's normal, so every flake of it sat inside the roof and the only trace was a faint
        // change in the shadows.  A test that merely rendered winter would have passed throughout.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "buildings.igl"),
            Path.Combine(_directory, "buildings.igl"), true);

        foreach (string building in Buildings)
        {
            Assert.IsTrue(Differs(Raised(building, "summer"), Raised(building, "winter")),
                $"a {building} in winter should not look like one in summer");
        }
    }

    [TestMethod]
    public void TestABuildingAskedForWithNoSeasonStandsInSummer()
    {
        // These take the season last and default it, so most scenes never write one.  The default has
        // to be the season a scene that says nothing actually gets, and in the trees library one
        // species quietly defaulted to a different one from all the rest.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "buildings.igl"),
            Path.Combine(_directory, "buildings.igl"), true);

        foreach (string building in Buildings)
        {
            Assert.IsFalse(Differs(Raised(building, null), Raised(building, "summer", null)),
                $"a {building} asked for with no season should be the same as one in summer");
        }
    }

    /// <summary>
    /// Counts the glass in a picture.  The glass in this library is the one dark, cool thing in it --
    /// the walls and trim are all warm, the ground is neutral gray and the sky is bright -- so a pixel
    /// that is dark and bluer than it is red is a window.
    /// </summary>
    private static int Windows(Canvas canvas)
    {
        int found = 0;

        for (int x = 0; x < canvas.Width; x++)
        {
            for (int y = 0; y < canvas.Height; y++)
            {
                Color pixel = canvas.GetPixel(x, y);

                if (pixel.Blue > pixel.Red + 0.02 && pixel.Blue < 0.45)
                    found++;
            }
        }

        return found;
    }

    [TestMethod]
    public void TestABuildingPutsItsWindowsOnTheFaceASceneLooksAt()
    {
        // Every one of these is built to be looked at from -Z, and the windows, the door and the sills
        // all go on that face.  They were first written onto the far side, which renders perfectly
        // well and gives a scene a row of blank walls -- nothing errored, and nothing showed.
        //
        // This counts the glass rather than comparing the two pictures, and the difference matters: a
        // building is not symmetric -- the chimney stands off to one side and the whole thing is set a
        // few degrees off square -- so front and back differ *whatever* face the windows are on.  The
        // first version of this test compared the pictures, passed, and proved nothing.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "buildings.igl"),
            Path.Combine(_directory, "buildings.igl"), true);

        foreach (string building in Buildings)
        {
            int front = Windows(Raised(building, "summer"));
            int behind = Windows(Raised(building, "summer", 2, true));

            Assert.IsTrue(front > behind * 3,
                $"a {building} shows {front} pixels of glass from the front and {behind} from " +
                "behind; its windows are not on the face a scene looks at");
        }
    }

    [TestMethod]
    public void TestTheVariantChangesABuilding()
    {
        // A number a scene may pass has to be a number that does something; a knob wired to nothing is
        // worse than no knob, since a scene will pass it and believe it worked.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "buildings.igl"),
            Path.Combine(_directory, "buildings.igl"), true);

        foreach (string building in Buildings)
        {
            Assert.IsTrue(Differs(Raised(building, "summer", 1), Raised(building, "summer", 4)),
                $"two {building}s of different variants should not be the same building");
        }
    }

    [TestMethod]
    public void TestARowIsCountedRatherThanMeasured()
    {
        // The first number means something different here than everywhere else in these libraries, and
        // the documentation says so -- a terrace is counted.  So more houses has to mean a longer row.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "buildings.igl"),
            Path.Combine(_directory, "buildings.igl"), true);

        string scene = Path.Combine(_directory, "scene.igl");
        int[] widths = new int[2];

        foreach ((int houses, int index) in new[] { (2, 0), (5, 1) })
        {
            File.WriteAllText(scene, $$"""
                import 'buildings' { Row }
                context { angles are degrees  no gamma }
                camera { location [0, 6, -46]  look at [0, 3, 0]  field of view 46 }
                point light { location [-9, 14, -12] }
                background [0.2, 0.35, 0.9]
                object Row({{houses}}, 'summer', 2)
                """);

            Assert.IsNull(Render(scene, 180, 60), $"a row of {houses} should stand");

            Canvas canvas = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
            int left = canvas.Width;
            int right = -1;

            // The background is the only strongly blue thing in the picture, so anything else is the
            // terrace.  There is deliberately no ground plane here, for that reason.
            for (int x = 0; x < canvas.Width; x++)
            {
                for (int y = 0; y < canvas.Height; y++)
                {
                    Color pixel = canvas.GetPixel(x, y);

                    if (pixel.Blue - pixel.Red > 0.2)
                        continue;

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }

            widths[index] = right - left;
        }

        Assert.IsTrue(widths[1] > widths[0],
            $"a row of five ({widths[1]} across) should be wider than a row of two ({widths[0]})");
    }

    /// <summary>
    /// What the vehicles library holds out, written down here rather than read out of the file, for
    /// the same reason the tree species are: a test that learns the names from the thing it is testing
    /// cannot notice a rename.
    /// </summary>
    private static readonly string[] Vehicles = ["Car", "Van", "Truck"];

    /// <summary>
    /// How long to ask for each.  These are the one library measured by length rather than height, so
    /// the numbers are the lengths a vehicle of that sort actually is.
    /// </summary>
    private static string VehicleLengthOf(string vehicle) => vehicle switch
    {
        "Van" => "5.6",
        "Truck" => "8.5",
        _ => "4.4"
    };

    /// <summary>
    /// Drives one vehicle out, and hands back whatever stopped it.  The camera stands off the front
    /// quarter, which is where a scene puts one and where its glass has to be visible from.
    /// </summary>
    private string Drive(string vehicle, string season, int? variant = 2)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string call = season is null
            ? $"{vehicle}({VehicleLengthOf(vehicle)})"
            : variant is null
                ? $"{vehicle}({VehicleLengthOf(vehicle)}, '{season}')"
                : $"{vehicle}({VehicleLengthOf(vehicle)}, '{season}', {variant})";

        File.WriteAllText(scene, $$"""
            import 'vehicles' { {{vehicle}} }
            context { angles are degrees  no gamma }
            camera { location [7, 5.5, -13]  look at [0, 0.9, 0]  field of view 46 }
            point light { location [6, 12, -10] }
            background [0.35, 0.45, 0.75]
            plane { material { pigment [0.3, 0.3, 0.3] } }
            object {{call}}
            """);

        return Render(scene, 130, 100);
    }

    /// <summary>
    /// Drives one out and hands back the picture of it.
    /// </summary>
    private Canvas Driven(string vehicle, string season, int? variant = 2)
    {
        Assert.IsNull(Drive(vehicle, season, variant),
            $"{vehicle} should stand in {season ?? "no season at all"}");

        return new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
    }

    [TestMethod]
    public void TestEveryVehicleStandsInEverySeason()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "vehicles.igl"),
            Path.Combine(_directory, "vehicles.igl"), true);

        foreach (string vehicle in Vehicles)
        {
            foreach (string season in new[] { "summer", "autumn", "fall", "winter", "spring" })
                Assert.IsNull(Drive(vehicle, season), $"{vehicle} should stand in {season}");
        }
    }

    [TestMethod]
    public void TestWinterLaysSnowOnEveryVehicle()
    {
        // A vehicle takes the season so that a street whose houses are white-roofed does not have
        // bare-roofed cars parked along it.  A word taken and ignored is worse than a word not asked
        // for, so what winter does has to be visible from where a scene stands.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "vehicles.igl"),
            Path.Combine(_directory, "vehicles.igl"), true);

        foreach (string vehicle in Vehicles)
        {
            Assert.IsTrue(Differs(Driven(vehicle, "summer"), Driven(vehicle, "winter")),
                $"a {vehicle} in winter should not look like one in summer");
        }
    }

    [TestMethod]
    public void TestAVehicleAskedForWithNoSeasonIsInSummer()
    {
        // These take the season last and default it, so most scenes never write one.  The default has
        // to be the season a scene that says nothing actually gets.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "vehicles.igl"),
            Path.Combine(_directory, "vehicles.igl"), true);

        foreach (string vehicle in Vehicles)
        {
            Assert.IsFalse(Differs(Driven(vehicle, null), Driven(vehicle, "summer", null)),
                $"a {vehicle} asked for with no season should be the same as one in summer");
        }
    }

    [TestMethod]
    public void TestTheVariantChangesAVehicle()
    {
        // Six paints, so a street is not one car repeated.  A knob wired to nothing is worse than no
        // knob, since a scene will pass it and believe it worked.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "vehicles.igl"),
            Path.Combine(_directory, "vehicles.igl"), true);

        foreach (string vehicle in Vehicles)
        {
            Assert.IsTrue(Differs(Driven(vehicle, "summer", 1), Driven(vehicle, "summer", 4)),
                $"two {vehicle}s of different variants should not be the same vehicle");
        }
    }

    [TestMethod]
    public void TestEveryVehicleShowsItsGlass()
    {
        // The glass on a van and on a truck first sat *just inside* the body's width, where it renders
        // perfectly and cannot be seen from any angle a scene uses -- a blank painted wall where a
        // windscreen should be.  Nothing errored and nothing showed, which is the same fault the
        // buildings library's windows had on the far wall.
        //
        // The glass is the one thing here that is dark and clearly cooler than it is warm: the paint is
        // bright, the tires are near-black and neutral, and the ground is gray.  So counting the cool
        // dark pixels counts glass.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "vehicles.igl"),
            Path.Combine(_directory, "vehicles.igl"), true);

        foreach (string vehicle in Vehicles)
        {
            Canvas canvas = Driven(vehicle, "summer");
            int glass = 0;

            for (int x = 0; x < canvas.Width; x++)
            {
                for (int y = 0; y < canvas.Height; y++)
                {
                    Color pixel = canvas.GetPixel(x, y);

                    if (pixel.Blue > pixel.Red + 0.02 && pixel.Blue < 0.30)
                        glass++;
                }
            }

            Assert.IsTrue(glass > 12,
                $"a {vehicle} shows only {glass} pixels of glass; its windows are buried in its body");
        }
    }

    [TestMethod]
    public void TestTheFirstNumberIsALength()
    {
        // Every other library measures its things by height, and this one does not -- a vehicle is
        // described by how long it is.  The documentation says so, so it is worth holding to.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "vehicles.igl"),
            Path.Combine(_directory, "vehicles.igl"), true);

        string scene = Path.Combine(_directory, "scene.igl");
        int[] widths = new int[2];

        foreach ((string length, int index) in new[] { ("3", 0), ("7", 1) })
        {
            File.WriteAllText(scene, $$"""
                import 'vehicles' { Car }
                context { angles are degrees  no gamma }
                camera { location [0, 3, -22]  look at [0, 0.7, 0]  field of view 46 }
                point light { location [6, 12, -10] }
                background [0.2, 0.35, 0.9]
                object Car({{length}}, 'summer', 2)
                """);

            Assert.IsNull(Render(scene, 180, 70), $"a car of {length} should stand");

            Canvas canvas = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
            int left = canvas.Width;
            int right = -1;

            // The background is the only strongly blue thing in the picture -- there is deliberately
            // no ground plane here -- so anything else is the car.
            for (int x = 0; x < canvas.Width; x++)
            {
                for (int y = 0; y < canvas.Height; y++)
                {
                    Color pixel = canvas.GetPixel(x, y);

                    if (pixel.Blue - pixel.Red > 0.2)
                        continue;

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }

            widths[index] = right - left;
        }

        Assert.IsTrue(widths[1] > widths[0] * 1.6,
            $"a car of seven ({widths[1]} across) should be much longer than one of three " +
            $"({widths[0]}); the first number is a length");
    }

    /// <summary>
    /// Renders a scene small and fast, and hands back whatever stopped it.
    /// </summary>
    private string Render(string path, int wide = 40, int high = 30)
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
                OutputFileName = Path.Combine(_directory, "out.png"), Width = wide, Height = high
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
    /// <summary>
    /// What the trains library holds out, written down here rather than read out of the library, so
    /// that a renamed primitive fails a test rather than quietly stopping being offered.
    /// </summary>
    private static readonly string[] RollingStock = ["Locomotive", "Tender"];

    [TestMethod]
    public void TestEveryPieceOfRollingStockBuilds()
    {
        // A library's primitives are not covered by the sweep above, which finds names by looking for
        // assignments -- a primitive is declared, not assigned.  So each one is asked for by name and
        // built, which is the only way a mistake inside one of them shows up as a test failure rather
        // than as a locomotive nobody can render.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "trains.igl"),
            Path.Combine(_directory, "trains.igl"), true);

        foreach (string piece in RollingStock)
        foreach (string season in (string[]) ["summer", "winter"])
            Assert.IsNull(Run(piece, season), $"{piece} should build in {season}");
    }

    [TestMethod]
    public void TestATrackIsLaidToStandardGaugeWhateverLengthIsAskedFor()
    {
        // The gauge is the one number in this library that must not move with the size of anything,
        // because an engine dropped onto a length of line has to fit it.  Two very different lengths
        // of track are laid and the rails have to come out the same distance apart in both.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "trains.igl"),
            Path.Combine(_directory, "trains.igl"), true);

        foreach (int length in (int[]) [12, 90])
        {
            string scene = Path.Combine(_directory, "scene.igl");

            // Looking straight down the rails from above, so the two bright heads are the only thing
            // in the picture and their spacing can be measured off it.
            File.WriteAllText(scene, $$"""
                import 'trains' { Track }
                context { angles are degrees  no gamma }
                camera { location [0, 9, 0]  look at [0, 0, 0]  up [1, 0, 0]  field of view 30 }
                point light { location [0, 14, 0] }
                background [0, 0, 0]
                object Track({{length}})
                """);

            Assert.IsNull(Render(scene), $"a track of {length} should lay");

            Canvas picture = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];

            // Each column is summed down the whole picture rather than sampled on one row, and the
            // brightest column in each half is taken as that rail.  Sampling a single row and calling
            // everything above a threshold "rail" does not work: the ballast comes close enough to the
            // threshold to be caught by it, and where the sleepers fall depends on how many there are,
            // so the same gauge measured 11 pixels at one length and 26 at another.  A rail runs the
            // whole height of this picture and nothing else does, so a column total tells them apart
            // with room to spare.
            int half = picture.Width / 2;
            int left = BrightestColumn(picture, 0, half);
            int right = BrightestColumn(picture, half, picture.Width);

            int measured = right - left;
            int expected = _gauge ?? (_gauge = measured).Value;

            Assert.IsTrue(Math.Abs(measured - expected) <= 1,
                $"a track of {length} measured {measured} pixels between the rails where the one " +
                $"before it measured {expected} -- the gauge is moving with the length");
        }
    }

    private int? _gauge;

    /// <summary>
    /// Finds the brightest column of a picture over a range of it, by total rather than by any one
    /// pixel, so that what is being looked for has to be bright all the way down.
    /// </summary>
    private static int BrightestColumn(Canvas picture, int from, int to)
    {
        int best = from;
        double most = -1;

        for (int x = from; x < to; x++)
        {
            double total = 0;

            for (int y = 0; y < picture.Height; y++)
                total += picture.GetPixel(x, y).Red;

            if (total > most)
            {
                most = total;
                best = x;
            }
        }

        Assert.IsTrue(most > 0, "this picture has nothing in it at all");

        return best;
    }

    /// <summary>
    /// Builds one piece of rolling stock and hands back whatever stopped it.
    /// </summary>
    private string Run(string piece, string season)
    {
        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, $$"""
            import 'trains' { {{piece}}, Track }
            context { angles are degrees  no gamma }
            camera { location [14, 6, -18]  look at [0, 2, 0]  field of view 46 }
            point light { location [-12, 16, -20] }
            background [0.5, 0.6, 0.8]
            object Track(30)
            object {{piece}}(11.6, '{{season}}')
            """);

        return Render(scene);
    }

    /// <summary>
    /// What the roads library holds out, written down here rather than read out of the library.
    /// </summary>
    private static readonly string[] RoadPieces =
    [
        "Road", "Curb", "Pavement", "Junction", "CurbCorner", "Crossing",
        "Crossroads", "Roundabout", "CurbArc", "CurbRing", "ParkingLot"
    ];

    [TestMethod]
    public void TestEveryRoadPieceBuilds()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "roads.igl"),
            Path.Combine(_directory, "roads.igl"), true);

        foreach (string piece in RoadPieces)
        foreach (string season in (string[]) ["summer", "winter"])
        foreach (int variant in (int[]) [0, 1, 2])
        {
            Assert.IsNull(Lay(piece, season, variant),
                $"{piece} should build in {season} as variant {variant}");
        }
    }

    [TestMethod]
    public void TestTheThreeRoadSurfacesAreActuallyDifferent()
    {
        // A variant that changes nothing is a lie in the documentation, and this one promises a good
        // deal: new asphalt nearly black, worn asphalt grayed, concrete pale.  So the three are not
        // merely required to differ -- they are required to come out in that *order*, brightest last,
        // which is the part that would break quietly if a color map were nudged.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "roads.igl"),
            Path.Combine(_directory, "roads.igl"), true);

        List<double> brightness = [];

        foreach (int variant in (int[]) [0, 1, 2])
        {
            Assert.IsNull(Lay("Road", "summer", variant), $"variant {variant} should lay");

            Canvas picture = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
            double total = 0;
            int counted = 0;

            // The middle of the picture, which is carriageway and nothing else.
            for (int x = picture.Width / 3; x < picture.Width * 2 / 3; x++)
            for (int y = picture.Height / 2; y < picture.Height * 3 / 4; y++)
            {
                total += picture.GetPixel(x, y).Red;
                counted++;
            }

            brightness.Add(total / counted);
        }

        Assert.IsTrue(brightness[0] < brightness[1] - 0.01,
            $"new asphalt came out at {brightness[0]:F3} against worn at {brightness[1]:F3}, and new " +
            "asphalt is meant to be the darker of the two by a clear margin");
        Assert.IsTrue(brightness[1] < brightness[2] - 0.01,
            $"worn asphalt came out at {brightness[1]:F3} against concrete at {brightness[2]:F3}, and " +
            "concrete is meant to be the palest surface the library offers");
    }

    /// <summary>
    /// Lays one piece of road and hands back whatever stopped it.
    /// </summary>
    private string Lay(string piece, string season, int variant)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        // `Pavement` and `Road` take a width; `Curb` does not.
        // The pieces do not all take the same shape of call, and that is deliberate rather than untidy:
        // a curb has a length and no width, a corner has a radius, and a crossing has no variant of its
        // own because road paint is road paint.
        string call = piece switch
        {
            "Curb" => $"Curb(30, '{season}', {variant})",
            "CurbCorner" => $"CurbCorner(2.5, '{season}', {variant})",
            "Crossing" => $"Crossing(7, 3.5, '{season}')",
            "Junction" => $"Junction(7, 8, '{season}', {variant})",
            "Crossroads" => $"Crossroads(7, 6, 1.5, '{season}', {variant})",
            "Roundabout" => $"Roundabout(4.5, 6, '{season}', {variant})",
            "CurbArc" => $"CurbArc(6, 20, 50, 1, '{season}', {variant})",
            "CurbRing" => $"CurbRing(4.5, 0, '{season}', {variant})",
            "ParkingLot" => $"ParkingLot(13, 14, '{season}', {variant})",
            _ => $"{piece}(30, 7, '{season}', {variant})"
        };

        File.WriteAllText(scene, $$"""
            import 'roads' { {{piece}} }
            context { angles are degrees  no gamma }
            camera { location [0, 6, -13]  look at [0, 0, 0]  field of view 44 }
            point light { location [-10, 14, -14] }
            background [0.5, 0.6, 0.8]
            object {{call}}
            """);

        return Render(scene);
    }

    [TestMethod]
    public void TestACurbCornerIsExactlyTheNinetyDegreeCaseOfAnArc()
    {
        // The library says `CurbCorner` is the ninety-degree case of `CurbArc`, and says it to explain
        // why both exist.  A claim like that rots the moment one of them is touched and the other is not,
        // and nothing about a picture of a curb would make the drift obvious -- so the two are rendered
        // and held against each other pixel for pixel.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "roads.igl"),
            Path.Combine(_directory, "roads.igl"), true);

        Canvas corner = CurbPicture("CurbCorner(2.5)");
        Canvas arc = CurbPicture("CurbArc(2.5, 0, 90, 0)");

        Assert.AreEqual(corner.Width, arc.Width);
        Assert.AreEqual(corner.Height, arc.Height);

        for (int x = 0; x < corner.Width; x++)
        for (int y = 0; y < corner.Height; y++)
        {
            Assert.AreEqual(corner.GetPixel(x, y).Red, arc.GetPixel(x, y).Red, 1e-9,
                $"a corner and a ninety-degree arc differ at ({x},{y}); one of them has been changed " +
                "without the other, and the library claims they are the same thing");
        }
    }

    /// <summary>
    /// Renders one piece of curb from straight above and hands back the picture.
    /// </summary>
    private Canvas CurbPicture(string call)
    {
        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, $$"""
            import 'roads' { Curb, CurbCorner, CurbArc }
            context { angles are degrees  no gamma }
            camera { location [1.2, 7, 1.2]  look at [1.2, 0, 1.2]  up [1, 0, 0]
                     field of view 44 }
            point light { location [-6, 9, -8] }
            background [0, 0, 0]
            object {{call}}
            """);

        Assert.IsNull(Render(scene), $"{call} should build");

        return new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
    }


    /// <summary>
    /// What the windows and doors libraries hold out, written down rather than read out of them.
    /// </summary>
    private static readonly string[] Openings =
        ["Sash", "Casement", "Shutters", "Door", "GlazedDoor", "DoubleDoor", "Fanlight"];

    [TestMethod]
    public void TestEveryOpeningBuilds()
    {
        foreach (string name in (string[]) ["windows.igl", "doors.igl"])
        {
            File.Copy(
                Shipped.First(path => Path.GetFileName(path) == name),
                Path.Combine(_directory, name), true);
        }

        foreach (string opening in Openings)
        foreach (string season in (string[]) ["summer", "winter"])
        foreach (int variant in (int[]) [0, 1, 2, 3])
            Assert.IsNull(Fit(opening, season, variant), $"{opening} should fit in {season} as {variant}");
    }

    [TestMethod]
    public void TestASashsGlassShowsAndItsBarsDivideIt()
    {
        // The one fault these libraries are prone to, and it does not look like a fault -- it looks like
        // the geometry was never made.  The depths have to stack: glass deepest, bars in front of it,
        // frame in front of those.  Wrong one way and the glass hides the bars; wrong the other and the
        // wall hides the glass.
        //
        // **Two earlier versions of this test passed in both broken states**, which is worse than having
        // no test.  Counting dark pixels caught the shadow the sill throws; counting where a row crosses
        // the picture's midpoint caught the jambs and the sill's edge.  Scanning a whole picture for one
        // feature keeps finding everything except that feature.
        //
        // So the glass is *marked* instead.  The test owns its copy of the library, so it paints the
        // glass magenta and then looks at nothing else: magenta appearing at all means the wall is not
        // hiding it, and magenta appearing in three separate runs along a row means two glazing bars are
        // standing in front of it dividing it into three lights.
        string library = Path.Combine(_directory, "windows.igl");

        File.Copy(Shipped.First(path => Path.GetFileName(path) == "windows.igl"), library, true);

        // The *whole* material is replaced, not just its pigment.  Appending `ambient 1` after the
        // pigment does nothing, because the material's own `ambient 0.04` comes later in the block and
        // wins -- which rendered the marked glass almost black and had this test reporting no glass at
        // all when the geometry was perfectly correct.
        File.WriteAllText(library, File.ReadAllText(library).Replace(
            """
            WindowGlass = material {
                pigment [0.075, 0.095, 0.115]
                specular 0.58  shininess 150  reflective 0.22  ambient 0.04
            }
            """.Replace("\n            ", "\n").Trim(),
            "WindowGlass = material { pigment [1, 0, 1]  ambient 1  diffuse 0  specular 0 }"));

        Assert.DoesNotContain("0.075, 0.095, 0.115", File.ReadAllText(library),
            "the glass material was not replaced, so this test would prove nothing");

        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, """
            import 'windows' { Sash }
            context { angles are degrees  no gamma }
            camera { location [0, 1.05, -2.4]  look at [0, 1.05, 0]  field of view 40 }
            point light { location [-4, 5, -6] }
            background [0, 0, 0]
            cube { material { pigment [0.55, 0.5, 0.44] }  scale [1.2, 0.9, 0.14]  translate Y 1.0 }
            object Sash(0.30, 0.46, 0.14) { translate [0, 1.05, -0.14] }
            """);

        Assert.IsNull(Render(scene, 220, 170), "the sash should render");

        Canvas picture = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
        int magenta = 0;
        int mostRuns = 0;

        for (int y = 0; y < picture.Height; y++)
        {
            int runs = 0;
            bool inRun = false;

            for (int x = 0; x < picture.Width; x++)
            {
                Color pixel = picture.GetPixel(x, y);
                bool isGlass = pixel.Red > 0.6 && pixel.Blue > 0.6 && pixel.Green < 0.3;

                if (isGlass)
                    magenta++;

                if (isGlass && !inRun)
                    runs++;

                inRun = isGlass;
            }

            mostRuns = Math.Max(mostRuns, runs);
        }

        Assert.IsTrue(magenta > 100,
            $"only {magenta} pixels of glass are visible; the wall is hiding it, which is what happens " +
            "when the glass is placed at positive Z");
        Assert.IsTrue(mostRuns >= 3,
            $"the glass shows as {mostRuns} run(s) across its widest row; a sash has two glazing bars " +
            "standing in front of it, so it should show as three.  One run means the glass is in front " +
            "of the bars and hiding them");
    }

    /// <summary>
    /// Fits one opening into a plain wall and hands back whatever stopped it.
    /// </summary>
    private string Fit(string opening, string season, int variant)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string library = opening is "Sash" or "Casement" or "Shutters" ? "windows" : "doors";

        File.WriteAllText(scene, $$"""
            import '{{library}}' { {{opening}} }
            context { angles are degrees  no gamma }
            camera { location [0, 1.05, -3] look at [0, 1.05, 0]  field of view 44 }
            point light { location [-4, 5, -6] }
            background [0.5, 0.6, 0.8]
            cube { material { pigment [0.55, 0.5, 0.44] }  scale [1.2, 0.9, 0.14]  translate Y 1.0 }
            object {{opening}}(0.30, 0.46, 0.14, '{{season}}', {{variant}}) { translate [0, 1.05, -0.14] }
            """);

        return Render(scene);
    }

    /// <summary>
    /// What the outdoor lights library holds out, written down rather than read out of it.
    /// </summary>
    private static readonly string[] Lamps = ["StreetLamp", "WallLantern", "BollardLight"];

    [TestMethod]
    public void TestEveryLampStandsLitAndUnlit()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "outdoor-lights.igl"),
            Path.Combine(_directory, "outdoor-lights.igl"), true);

        foreach (string lamp in Lamps)
        foreach (string season in (string[]) ["summer", "winter"])
        foreach (int variant in (int[]) [0, 1])
        foreach (int lit in (int[]) [0, 1])
            Assert.IsNull(Erect(lamp, season, variant, lit), $"{lamp} should stand ({season}, {lit})");
    }

    [TestMethod]
    public void TestALitLampActuallyLightsTheGroundAndAnUnlitOneDoesNot()
    {
        // The whole promise of this library, and the one thing about it that could break silently.  A
        // lamp is emissive rather than a light -- an emitting medium inside a shell marked `gives
        // light` -- so there is nothing in the scene to fail loudly if that stops working.  The fitting
        // would go on rendering perfectly and simply stop lighting anything.
        //
        // So the same lamp is stood over the same ground in a scene with no light of its own, twice, and
        // the ground is required to be far brighter with it burning than with it out.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "outdoor-lights.igl"),
            Path.Combine(_directory, "outdoor-lights.igl"), true);

        double dark = GroundUnder(0);
        double lit = GroundUnder(1);

        // The numbers are small because the pool of light is a small part of the frame -- what matters
        // is that one of them is flatly zero and the other is not.
        Assert.IsTrue(dark < 0.005,
            $"the ground under an unlit lamp measured {dark:F4}; nothing should be lighting it");
        Assert.IsTrue(lit > 0.015,
            $"the ground under a lit lamp measured {lit:F4} against {dark:F4} unlit -- the lamp is not " +
            "lighting anything, which is what happens when its shell stops giving light or its lantern " +
            "closes over its own globe");
    }

    /// <summary>
    /// Stands one lamp over a plain floor in a scene with no light in it, and reports how bright the
    /// floor came out directly beneath it.
    /// </summary>
    private double GroundUnder(int lit)
    {
        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, $$"""
            import 'outdoor-lights' { StreetLamp }
            context { angles are degrees  no gamma  medium samples 24 }
            // Angled rather than straight down: a camera looking along its own up vector is degenerate,
            // and renders black, which reads exactly like a lamp that is not lighting anything.
            camera { location [4, 6, -5]  look at [0, 0.4, 0]  field of view 44 }
            background [0, 0, 0]
            plane { material { pigment [0.6, 0.6, 0.6]  ambient 0 } }
            object StreetLamp(5, 'summer', 1, {{lit}})
            """);

        Assert.IsNull(Render(scene, 120, 90), $"a lamp with lit = {lit} should render");

        Canvas picture = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
        double total = 0;

        // The whole picture: there is nothing in it but the floor and one lamp, so its mean is the
        // floor's brightness to within the small area the column covers -- and that area is the same
        // whether the lamp is burning or not, which is what makes the two readings comparable.
        for (int x = 0; x < picture.Width; x++)
        for (int y = 0; y < picture.Height; y++)
            total += picture.GetPixel(x, y).Red;

        return total / (picture.Width * picture.Height);
    }

    /// <summary>
    /// Erects one lamp and hands back whatever stopped it.
    /// </summary>
    private string Erect(string lamp, string season, int variant, int lit)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        double size = lamp == "StreetLamp" ? 5 : lamp == "BollardLight" ? 1 : 1.4;

        File.WriteAllText(scene, $$"""
            import 'outdoor-lights' { {{lamp}} }
            context { angles are degrees  no gamma  medium samples 12 }
            camera { location [6, 4, -8]  look at [0, 2, 0]  field of view 46 }
            point light { location [-6, 9, -9] }
            background [0.1, 0.1, 0.14]
            plane { material { pigment [0.3, 0.3, 0.3] } }
            object {{lamp}}({{size}}, '{{season}}', {{variant}}, {{lit}})
            """);

        return Render(scene, 60, 45);
    }

    /// <summary>
    /// What the indoor lights library holds out, written down rather than read out of it.
    /// </summary>
    private static readonly string[] Fittings = ["TableLamp", "FloorLamp", "Pendant", "Sconce"];

    [TestMethod]
    public void TestEveryIndoorFittingStandsLitAndUnlit()
    {
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "indoor-lights.igl"),
            Path.Combine(_directory, "indoor-lights.igl"), true);

        foreach (string fitting in Fittings)
        foreach (int variant in (int[]) [0, 1, 2])
        foreach (int lit in (int[]) [0, 1])
            Assert.IsNull(Fit(fitting, variant, lit), $"{fitting} should stand ({variant}, {lit})");
    }

    [TestMethod]
    public void TestALitFittingLightsTheRoomAndAnUnlitOneDoesNot()
    {
        // The same promise the street lamps make, and the same silent way of breaking it: a fitting is
        // emissive rather than a light, so nothing in the scene fails loudly when it stops lighting.
        // It would go on rendering perfectly and simply leave the room black.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "indoor-lights.igl"),
            Path.Combine(_directory, "indoor-lights.igl"), true);

        double dark = RoomUnder(0);
        double lit = RoomUnder(1);

        Assert.IsTrue(dark < 0.005,
            $"the floor under an unlit lamp measured {dark:F4}; nothing should be lighting it");
        Assert.IsTrue(lit > 0.02,
            $"the floor under a lit lamp measured {lit:F4} against {dark:F4} unlit -- the fitting is " +
            "not lighting anything, which is what happens when its shell stops giving light or its " +
            "shade closes over its own bulb");
    }

    [TestMethod]
    public void TestAShadeSendsFarMoreLightPastItsEndsThanThroughItsSide()
    {
        // This is the whole design of the library, and neither half of it is safe on its own.
        //
        // A shade is *supposed* to block: what makes a lamp read as a lamp is the cone it throws at the
        // ceiling and the pool it throws on the floor, with the wall beside it left comparatively dark.
        // Build the shade out of anything clear and that shape is gone -- the fitting becomes a bare
        // bulb in a room and lights everything evenly.
        //
        // But it must not block *everything* either.  Linen is not tin, and a shade that passes nothing
        // sideways leaves a hard-edged pool with black around it, which reads as a spotlight rather than
        // as a lamp.  So the side is required to be lit, and required to be much dimmer than the top.
        File.Copy(
            Shipped.First(path => Path.GetFileName(path) == "indoor-lights.igl"),
            Path.Combine(_directory, "indoor-lights.igl"), true);

        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, """
            import 'indoor-lights' { TableLamp }
            context { angles are degrees  no gamma  medium samples 20 }
            camera { location [0, 1.25, -2.6]  look at [0, 1.25, 0] }
            background [0, 0, 0]
            // The wall faces the camera.  Turned the other way it renders black however well the lamp
            // works, which looks exactly like a lamp that does not.
            plane {
                material { pigment [0.75, 0.75, 0.75]  specular 0  ambient 0 }
                rotate X -90  translate Z 0.35
            }
            object TableLamp(0.60) { translate [0, 0.95, 0] }
            """);

        Assert.IsNull(Render(scene, 300, 300), "the lamp should render");

        Canvas picture = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];
        double above = Patch(picture, 130, 170, 45, 75);
        double beside = Patch(picture, 40, 70, 140, 170);

        Assert.IsTrue(beside > 0.002,
            $"the wall beside the shade measured {beside:F4}; linen is not tin, and a shade that " +
            "passes nothing sideways leaves a spotlight rather than a lamp");
        Assert.IsTrue(above > beside * 3,
            $"the wall above the shade measured {above:F4} against {beside:F4} beside it -- a shade " +
            "has to block far more than it passes, or the fitting is only a bare bulb");
    }

    /// <summary>
    /// The mean brightness of one rectangle of a picture.
    /// </summary>
    private static double Patch(Canvas picture, int fromX, int toX, int fromY, int toY)
    {
        double total = 0;

        for (int x = fromX; x < toX; x++)
        for (int y = fromY; y < toY; y++)
            total += picture.GetPixel(x, y).Red;

        return total / ((toX - fromX) * (toY - fromY));
    }

    /// <summary>
    /// Stands one table lamp over a plain floor in a room with no light of its own, and reports how
    /// bright the floor came out.
    /// </summary>
    private double RoomUnder(int lit)
    {
        string scene = Path.Combine(_directory, "scene.igl");

        File.WriteAllText(scene, $$"""
            import 'indoor-lights' { TableLamp }
            context { angles are degrees  no gamma  medium samples 20 }
            camera { location [1.1, 1.2, -1.9]  look at [0, 0.4, 0]  field of view 50 }
            background [0, 0, 0]
            plane { material { pigment [0.6, 0.6, 0.6]  ambient 0 } }
            object TableLamp(0.60, 1, {{lit}}) { translate Y 0.02 }
            """);

        Assert.IsNull(Render(scene, 120, 90), $"a fitting with lit = {lit} should render");

        Canvas picture = new ImageFile(Path.Combine(_directory, "out.png")).Load()[0];

        return Patch(picture, 0, picture.Width, 0, picture.Height);
    }

    /// <summary>
    /// Stands one fitting in a room and hands back whatever stopped it.
    /// </summary>
    private string Fit(string fitting, int variant, int lit)
    {
        string scene = Path.Combine(_directory, "scene.igl");
        // A pendant hangs from where it is put; everything else stands there.
        double size = fitting == "FloorLamp" ? 1.5 : fitting == "Sconce" ? 0.34 : 0.6;
        string place = fitting == "Pendant"
            ? "translate Y 2.6"
            : fitting == "Sconce" ? "translate [0, 1.7, 1.19]" : "translate Y 0.02";

        File.WriteAllText(scene, $$"""
            import 'indoor-lights' { {{fitting}} }
            context { angles are degrees  no gamma  medium samples 12 }
            camera { location [1.6, 1.5, -3.2]  look at [0, 1.1, 0]  field of view 50 }
            background [0, 0, 0]
            plane { material { pigment [0.6, 0.6, 0.6]  ambient 0.05 } }
            plane { material { pigment [0.6, 0.6, 0.6]  ambient 0.05 }  rotate X -90  translate Z 1.2 }
            object {{fitting}}({{size}}, {{variant}}, {{lit}}) { {{place}} }
            """);

        return Render(scene, 60, 45);
    }

}
