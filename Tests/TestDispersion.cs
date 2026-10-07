using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// Glass that bends each color by its own amount: the glasses' data, the two ways of saying it in a
/// scene, and the split it makes in a spectral render -- and makes nowhere else.
/// </summary>
[TestClass]
public class TestDispersion
{
    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"dispersion-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// Each glass, read at the catalog's lines, comes back with the catalog's index and Abbe number.
    /// The numbers are Schott's; the tables are measurements at twenty-odd wavelengths read between in
    /// straight lines, which is close enough to give the index to a ten-thousandth and the Abbe number
    /// to within one.
    /// </summary>
    [TestMethod]
    public void TestTheGlassesAreTheCatalogs()
    {
        (string Name, double Index, double Abbe)[] catalog =
        [
            ("BK7", 1.51680, 64.17), ("BAF10", 1.67003, 47.11), ("FK51A", 1.48656, 84.47),
            ("LASF9", 1.85025, 32.17), ("SF5", 1.67270, 32.21), ("SF10", 1.72825, 28.41),
            ("SF11", 1.78472, 25.68)
        ];

        CollectionAssert.AreEquivalent(catalog.Select(glass => glass.Name).ToList(), Glasses.Names.ToList());

        foreach ((string name, double index, double abbe) in catalog)
        {
            double atD = Glasses.IndexAt(name, Glasses.ReferenceWavelength);
            double spread = Glasses.IndexAt(name, Glasses.BlueWavelength) -
                            Glasses.IndexAt(name, Glasses.RedWavelength);

            Assert.AreEqual(index, atD, 2e-4, $"{name}'s index");
            Assert.AreEqual(abbe, (atD - 1) / spread, 1, $"{name}'s Abbe number");
        }
    }

    /// <summary>
    /// Every glass bends blue more than red, all the way across the visible range, and is not fussy
    /// about how its name is written.
    /// </summary>
    [TestMethod]
    public void TestEveryGlassBendsBlueMoreThanRed()
    {
        foreach (string name in Glasses.Names)
        {
            for (double wavelength = 380; wavelength < 780; wavelength += 5)
            {
                Assert.IsTrue(Glasses.IndexAt(name, wavelength) > Glasses.IndexAt(name, wavelength + 5),
                    $"{name} at {wavelength}");
            }
        }

        Assert.IsTrue(Glasses.IsKnown("bk7"));
        Assert.IsFalse(Glasses.IsKnown("BK8"));
    }

    /// <summary>
    /// An index and an Abbe number make a curve that passes through the index at the reference line
    /// and spreads the blue and red lines by exactly the amount the Abbe number says.
    /// </summary>
    [TestMethod]
    public void TestAnAbbeNumberMakesTheCurveItDescribes()
    {
        foreach ((double index, double abbe) in new[] { (1.5, 60.0), (2.417, 55.0), (1.75, 25.0) })
        {
            double atD = Glasses.CauchyIndexAt(index, abbe, Glasses.ReferenceWavelength);
            double spread = Glasses.CauchyIndexAt(index, abbe, Glasses.BlueWavelength) -
                            Glasses.CauchyIndexAt(index, abbe, Glasses.RedWavelength);

            Assert.AreEqual(index, atD, 1e-12);
            Assert.AreEqual(abbe, (index - 1) / spread, 1e-9);
        }
    }

    /// <summary>
    /// An interior that names a glass takes its index from it, and every interior answers by
    /// wavelength -- with its one index, for light of every wavelength at once or a substance that
    /// spreads nothing.
    /// </summary>
    [TestMethod]
    public void TestAnInteriorAnswersByWavelength()
    {
        Interior plain = new () { IndexOfRefraction = 1.5 };
        Interior abbe = new () { IndexOfRefraction = 1.5, AbbeNumber = 30 };
        Interior glass = new () { IndexOfRefraction = 1.2, Glass = "SF10" };

        Assert.IsFalse(plain.Disperses);
        Assert.AreEqual(1.5, plain.IndexOfRefractionAt(450));
        Assert.IsTrue(abbe.Disperses);
        Assert.AreEqual(1.5, abbe.IndexOfRefractionAt(double.NaN));
        Assert.IsTrue(abbe.IndexOfRefractionAt(450) > abbe.IndexOfRefractionAt(650));

        Assert.IsTrue(glass.Disperses);
        Assert.AreEqual(Glasses.IndexAt("SF10", Glasses.ReferenceWavelength), glass.IndexOfRefraction,
            "a glass brings its own index, over any written before it");
        Assert.AreEqual(Glasses.IndexAt("SF10", 500), glass.IndexOfRefractionAt(500));

        Assert.ThrowsExactly<ArgumentException>(() => new Interior { Glass = "Pyrex" });
    }

    /// <summary>
    /// The split is the only thing dispersion changes, and only a spectral render splits.  So in red,
    /// green and blue a glass that spreads colors renders exactly as one of the same index that does
    /// not; and in a spectral render the same two differ, the one breaking black and white stripes
    /// into colors and the other leaving them black and white.
    /// </summary>
    [TestMethod]
    public void TestOnlyASpectralRenderSpreadsColors()
    {
        string index = Glasses.IndexAt("SF10", Glasses.ReferenceWavelength).ToString("R");
        string dispersive = Rendered("", "glass 'SF10'");
        string plain = Rendered("", $"ior {index}");

        CollectionAssert.AreEqual(File.ReadAllBytes(plain), File.ReadAllBytes(dispersive),
            "in red, green and blue, a glass's dispersion should change nothing");

        Assert.IsTrue(Colorful(Rendered("spectral", "glass 'SF10'")) > 20,
            "seen through dispersive glass in a spectral render, stripes should break into colors");
        Assert.AreEqual(0, Colorful(Rendered("spectral", $"ior {index}")),
            "seen through glass that spreads nothing, stripes should stay black and white");
    }

    /// <summary>
    /// Asking for a glass that does not exist, or a dispersion of nothing or less, says what is wrong.
    /// </summary>
    [TestMethod]
    public void TestABadGlassOrDispersionIsReported()
    {
        StringAssert.Contains(Complaint("glass 'Pyrex'"), "There is no glass named 'Pyrex'");
        StringAssert.Contains(Complaint("ior 1.5  dispersion 0"), "An Abbe number must be more than nothing");
    }

    /// <summary>
    /// This method renders a sphere of the given interior against black and white stripes, small, and
    /// returns the image's path.
    /// </summary>
    private string Rendered(string context, string interior)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(path,
            $"context {{ angles are degrees  {context} }}\n" +
            "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 30 }\n" +
            "background linear stripes { White, Black  scale 0.03 }\n" +
            "sphere { material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1  " +
            $"interior {{ {interior} }} }} }}");

        new LanguageParser(path).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 48, Height = 48, ProgressStyleText = "none"
        });

        return output;
    }

    /// <summary>
    /// This method counts the pixels of an image that are plainly colored rather than gray.
    /// </summary>
    private static int Colorful(string path)
    {
        Canvas canvas = new ImageFile(path).Load()[0];
        int count = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                Color color = canvas.GetPixel(x, y);
                double most = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
                double least = Math.Min(color.Red, Math.Min(color.Green, color.Blue));

                if (most - least > 0.1)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// This method renders a sphere of the given interior and returns whatever the render said, which
    /// for a term that fails its check is the complaint, raised when the term is resolved.
    /// </summary>
    private string Complaint(string interior)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            Rendered("", interior);
        }
        catch (Exception exception)
        {
            Console.Write(exception);
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }
}
