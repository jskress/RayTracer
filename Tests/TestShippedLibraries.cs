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
    private static readonly string[] Plants = ["Grass", "Tuft", "Boxwood", "Bramble", "Lavender"];

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
}
