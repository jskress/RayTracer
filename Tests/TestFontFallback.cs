using RayTracer.Fonts;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using Typography.OpenFont;

namespace Tests;

/// <summary>
/// These tests cover naming a list of fonts, as CSS does, so that a face not everyone can have may be
/// named first and one anyone can fetch named after it: the first that can be had is the one used.
/// </summary>
[TestClass]
public class TestFontFallback
{
    /// <summary>
    /// A family name no font has, different every time it is asked for, since the manager remembers
    /// for the rest of the run which faces it could not find.
    /// </summary>
    private static string Missing() => $"No Such Font {Guid.NewGuid():N}";

    private static Typeface Installed(string family)
    {
        return FontManager.Instance.GetExistingTypeFace(new FaceIdentifier
        {
            FamilyName = family, Weight = (int) FontWeight.Regular, Italic = false
        });
    }

    [TestMethod]
    public void TestTheFirstFontThatCanBeHadIsUsed()
    {
        // Whether a face cannot be had because it is not there or because fetching it failed, the
        // next is tried; and once one is found, nothing after it is asked about at all.
        string absent = Missing();
        string unfetchable = Missing();
        Typeface merriweather = Installed("Merriweather");
        List<string> asked = [];
        Typeface found = FontManager.Instance.GetFirstAvailableTypeFace(
            [absent, unfetchable, "Merriweather", "Tangerine"], FontWeight.Regular, false, id =>
            {
                asked.Add(id.FamilyName);

                return id.FamilyName == unfetchable
                    ? throw new Exception("Google does not have it.")
                    : id.FamilyName == "Merriweather" ? merriweather : null;
            });

        Assert.AreSame(merriweather, found);
        CollectionAssert.AreEqual(new[] { absent, unfetchable, "Merriweather" }, asked);
    }

    [TestMethod]
    public void TestAFontThatCouldNotBeHadIsNotAskedForAgain()
    {
        // A scene that sets a dozen texts in a font it cannot have would otherwise go to the network
        // a dozen times before falling back.
        string missing = Missing();
        Typeface merriweather = Installed("Merriweather");
        int asked = 0;

        for (int time = 0; time < 3; time++)
        {
            FontManager.Instance.GetFirstAvailableTypeFace([missing, "Merriweather"], FontWeight.Regular, false, id =>
            {
                if (id.FamilyName == missing)
                {
                    asked++;

                    throw new Exception("Google does not have it.");
                }

                return merriweather;
            });
        }

        Assert.AreEqual(1, asked, "a font already found missing was asked for again");
    }

    [TestMethod]
    public void TestWhenNoFontCanBeHadEachIsNamed()
    {
        string first = Missing();
        string second = Missing();
        Exception all = Assert.ThrowsExactly<Exception>(() => FontManager.Instance.GetFirstAvailableTypeFace(
            [first, second], FontWeight.Regular, false, _ => throw new Exception("Google does not have it.")));

        StringAssert.Contains(all.Message, "None of the fonts asked for could be found.");
        StringAssert.Contains(all.Message, first);
        StringAssert.Contains(all.Message, second);
        StringAssert.Contains(all.Message, "Google does not have it.");

        // A single font fails as it always has, with its own message.
        Exception one = Assert.ThrowsExactly<Exception>(() => FontManager.Instance.GetFirstAvailableTypeFace(
            [Missing()], FontWeight.Regular, false, _ => throw new Exception("Google does not have it.")));

        Assert.AreEqual("Google does not have it.", one.Message);
    }

    [TestMethod]
    public void TestAListOfFontsIsWrittenWithCommas()
    {
        // Set in the first of a list, a word looks exactly as it does set in that font alone, and not
        // as it does in the second; with the list turned round, it is the other way about.  A weight
        // written after the list belongs to it, and changes nothing here, regular being the default.
        // Both fonts are installed, so nothing here goes to the network; what happens when a font
        // cannot be had is checked above, without it.
        Canvas tangerine = Rendered("'Tangerine'");
        Canvas merriweather = Rendered("'Merriweather'");

        Assert.IsFalse(Same(tangerine, merriweather), "the two fonts should look different, or this proves nothing");
        Assert.IsTrue(Same(tangerine, Rendered("'Tangerine', 'Merriweather'")), "the first of the list was not used");
        Assert.IsTrue(Same(merriweather, Rendered("'Merriweather', 'Tangerine'")), "the first of the list was not used");
        Assert.IsTrue(Same(tangerine, Rendered("'Tangerine', 'Merriweather' regular")),
            "a weight after a list was not read");

        // And a font that cannot be had is passed over for the next.  This one is made known to be
        // missing first -- by asking for it once with a finder that cannot find it -- so that the
        // scene passes it over from the manager's memory rather than by going to the network.
        string missing = Missing();

        FontManager.Instance.GetFirstAvailableTypeFace(
            [missing, "Merriweather"], FontWeight.Regular, false,
            id => id.FamilyName == missing ? null : Installed(id.FamilyName));

        Assert.IsTrue(Same(merriweather, Rendered($"'{missing}', 'Merriweather'")),
            "a font that could not be had was not passed over");
    }

    /// <summary>
    /// Renders a word painted on a floor, seen from above, set in the given font list.
    /// </summary>
    private static Canvas Rendered(string fonts)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"font-fallback-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string scene = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "scene.png");

            File.WriteAllText(scene, $$"""
                context { no gamma }
                camera { location [0, 10, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 30 }
                point light { location [0, 20, 0] }
                plane {
                    material {
                        pigment color White  ambient 1  diffuse 0  specular 0
                        decal {
                            path { text { text 'Hamburg'  font {{fonts}}
                                          layout { horizontal position center  vertical position center } } }
                            color Black
                            planar  min Y -1  max Y 1
                        }
                    }
                }
                """);

            new LanguageParser(scene).Parse().Render(new RenderOptions
            {
                OutputFileName = output, Width = 80, Height = 40
            });

            return new ImageFile(output).Load()[0];
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static bool Same(Canvas left, Canvas right)
    {
        for (int y = 0; y < left.Height; y++)
        for (int x = 0; x < left.Width; x++)
        {
            if (!left.GetPixel(x, y).Matches(right.GetPixel(x, y)))
                return false;
        }

        return true;
    }
}
