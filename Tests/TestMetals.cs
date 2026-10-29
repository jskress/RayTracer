using Complex = System.Numerics.Complex;
using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// Real metals: the catalog against what each metal is known to mirror, Fresnel's equations for a conductor, a
/// metal given by its color, and a metal's light at a surface -- mirrored by its own share of each color, with
/// nothing of its own shown, under paint, grime, a film and a coat.
/// </summary>
[TestClass]
public class TestMetals
{
    /// <summary>
    /// Square-on, each metal in the catalog mirrors what it is known to, in red, green and blue: the values
    /// published in Real-Time Rendering (4th ed., table 9.2), to within the spread between one measurement and
    /// another -- but titanium, whose measurements the catalog takes give back a few hundredths more than those
    /// values say.
    /// </summary>
    [TestMethod]
    public void TestTheMetalsMirrorWhatTheyAreKnownTo()
    {
        (string Name, double Red, double Green, double Blue, double Within)[] known =
        [
            ("silver", 0.972, 0.960, 0.915, 0.025), ("aluminum", 0.913, 0.922, 0.924, 0.025),
            ("gold", 1.000, 0.782, 0.344, 0.025), ("copper", 0.955, 0.638, 0.538, 0.025),
            ("brass", 0.910, 0.778, 0.423, 0.025), ("chromium", 0.549, 0.556, 0.554, 0.025),
            ("iron", 0.562, 0.565, 0.578, 0.025), ("nickel", 0.660, 0.609, 0.526, 0.025),
            ("platinum", 0.673, 0.637, 0.585, 0.025), ("titanium", 0.542, 0.497, 0.449, 0.1)
        ];

        CollectionAssert.AreEquivalent(known.Select(metal => metal.Name).ToList(), Metals.Names.ToList());

        foreach ((string name, double red, double green, double blue, double within) in known)
        {
            RgbSpectrum mirrored = Metal.Named(name).ShareAt<RgbSpectrum>(1, 1);

            Assert.AreEqual(red, mirrored.Red, within, $"{name}'s red");
            Assert.AreEqual(green, mirrored.Green, within, $"{name}'s green");
            Assert.AreEqual(blue, mirrored.Blue, within, $"{name}'s blue");
        }

        Assert.IsTrue(Metals.IsKnown("Gold"), "a name is not fussy about how it is written");
        Assert.IsFalse(Metals.IsKnown("zinc"));
    }

    /// <summary>
    /// Fresnel's equations for a conductor: square-on, the share every textbook gives; at a graze, all of it; and
    /// for an index with nothing swallowed, exactly a dielectric's, polarizations averaged, at every angle.
    /// </summary>
    [TestMethod]
    public void TestAConductorFollowsFresnelsEquations()
    {
        foreach (Complex index in new[] { new Complex(0.2, 3.1), new Complex(2.9, 3.3), new Complex(1, 8.7) })
        {
            double n = index.Real;
            double k = index.Imaginary;

            Assert.AreEqual(
                ((n - 1) * (n - 1) + k * k) / ((n + 1) * (n + 1) + k * k), Metal.Reflectance(1, index), 1e-12,
                $"square-on, {index}");
            Assert.AreEqual(1, Metal.Reflectance(0, index), 1e-12, $"at a graze, {index}");
        }

        foreach (double n in new[] { 1.33, 1.5, 2.4 })
        {
            for (double cos = 0.05; cos <= 1; cos += 0.05)
            {
                double sin = Math.Sqrt(1 - cos * cos);
                double bent = Math.Sqrt(1 - sin * sin / (n * n));
                double across = (cos - n * bent) / (cos + n * bent);
                double along = (n * cos - bent) / (n * cos + bent);

                Assert.AreEqual(
                    (across * across + along * along) / 2, Metal.Reflectance(cos, new Complex(n, 0)), 1e-12,
                    $"glass of {n} at a cosine of {cos}");
            }
        }
    }

    /// <summary>
    /// What a metal mirrors is read from its table as the equations give it: in its own bands, in one band alone,
    /// at any angle, and against whatever lies outside it.
    /// </summary>
    [TestMethod]
    public void TestTheTableIsTheEquations()
    {
        Metal gold = Metal.Named("gold");
        Random random = new (7);

        foreach (double outside in new[] { 1, 1.33 })
        {
            for (int index = 0; index < 50; index++)
            {
                double cos = random.NextDouble();
                BandSpectrum bands = gold.ShareAt<BandSpectrum>(cos, outside);

                for (int band = 0; band < SpectralColor.Bands; band++)
                {
                    double exact = Metal.Reflectance(cos, gold.IndexIn(band) / outside);

                    Assert.AreEqual(exact, bands[band], 1e-4, $"band {band} at a cosine of {cos} under {outside}");
                }
            }
        }

        (int band, double wavelength) was = SingleBand.Carry(20, SpectralColor.WavelengthOf(20) + 3);

        try
        {
            Assert.AreEqual(
                Metal.Reflectance(0.4, gold.IndexIn(20)), gold.ShareAt<SingleBand>(0.4, 1).Amount, 1e-4,
                "a ray carrying one band");
        }
        finally
        {
            SingleBand.Carry(was.band, was.wavelength);
        }
    }

    /// <summary>
    /// What a metal mirrors in red, green and blue is what its bands come to, so a spectral render of a metal
    /// under white light matches one in red, green and blue.
    /// </summary>
    [TestMethod]
    public void TestRedGreenAndBlueAreWhatTheBandsComeTo()
    {
        foreach (string name in Metals.Names)
        {
            Metal metal = Metal.Named(name);

            foreach (double cos in new[] { 1, 0.7, 0.3, 0.05 })
            {
                BandSpectrum bands = metal.ShareAt<BandSpectrum>(cos, 1);
                Color seen = SpectralColor.ToColor(Enumerable.Range(0, SpectralColor.Bands).Select(band => bands[band]).ToArray());
                RgbSpectrum rgb = metal.ShareAt<RgbSpectrum>(cos, 1);

                Assert.AreEqual(Math.Min(seen.Red, 1), rgb.Red, 1e-4, $"{name}'s red at {cos}");
                Assert.AreEqual(Math.Min(seen.Green, 1), rgb.Green, 1e-4, $"{name}'s green at {cos}");
                Assert.AreEqual(Math.Min(seen.Blue, 1), rgb.Blue, 1e-4, $"{name}'s blue at {cos}");
            }

            Assert.AreEqual(
                Enumerable.Range(0, SpectralColor.Bands).Average(band => metal.ShareAt<BandSpectrum>(0.6, 1)[band]),
                metal.AverageAt(0.6, 1), 1e-9, $"{name}'s average");
        }
    }

    /// <summary>
    /// A metal holds what it mirrors square-on most of the way to a graze -- aluminum dipping a little before it
    /// climbs, as it was measured to -- and whitens at the very edge: gold mirrors a third of the blue square-on
    /// and nearly all of it at a graze.
    /// </summary>
    [TestMethod]
    public void TestAMetalWhitensAtTheEdge()
    {
        RgbSpectrum gold = Metal.Named("gold").ShareAt<RgbSpectrum>(1, 1);
        RgbSpectrum grazed = Metal.Named("gold").ShareAt<RgbSpectrum>(0.02, 1);
        Metal aluminum = Metal.Named("aluminum");

        Assert.IsTrue(gold.Blue < 0.4 && gold.Red > 0.95, $"gold square-on was {gold}");
        Assert.IsTrue(grazed.Blue > 0.9 && grazed.Green > 0.95, $"gold at a graze was {grazed}");
        Assert.IsTrue(
            aluminum.AverageAt(Math.Cos(80 * Math.PI / 180), 1) < aluminum.AverageAt(1, 1) - 0.04,
            "aluminum did not dip toward a graze");
        Assert.IsTrue(aluminum.AverageAt(0.01, 1) > 0.97, "aluminum did not climb at the edge");
    }

    /// <summary>
    /// A metal given by its color mirrors that color square-on: exactly, band by band, for Gulbrandsen's index
    /// and for pbrt's; and in red, green and blue, to within how nearly a color can be made a spectrum.  The edge
    /// decides how each color fares at a slant: where the edge is bright it climbs, where dark it dips first.
    /// </summary>
    [TestMethod]
    public void TestAMetalGivenByItsColorMirrorsIt()
    {
        foreach (double face in new[] { 0.05, 0.34, 0.64, 0.95 })
        {
            Assert.AreEqual(face, Metal.Reflectance(1, Metal.IndexFor(face)), 1e-9, $"pbrt's, {face}");

            foreach (double edge in new[] { 0, 0.3, 0.7, 1 })
            {
                Assert.AreEqual(
                    face, Metal.Reflectance(1, Metal.IndexFor(face, edge)), 1e-9, $"Gulbrandsen's, {face}, {edge}");
            }
        }

        Color copper = new (0.955, 0.638, 0.538);

        foreach (Color edge in new[] { null, new Color(1, 0.8, 0.6) })
        {
            RgbSpectrum seen = Metal.FromColor(copper, edge).ShareAt<RgbSpectrum>(1, 1);

            Assert.AreEqual(copper.Red, seen.Red, 0.01, $"red, edge {edge}");
            Assert.AreEqual(copper.Green, seen.Green, 0.01, $"green, edge {edge}");
            Assert.AreEqual(copper.Blue, seen.Blue, 0.01, $"blue, edge {edge}");
        }

        // Gulbrandsen's index is the one between glass and the least that can mirror the color: an edge of nought is
        // glass of the index that mirrors it, nothing swallowed, and an edge of one the least bending part there is.
        foreach (double face in new[] { 0.2, 0.64, 0.9 })
        {
            double root = Math.Sqrt(face);

            Assert.AreEqual((1 + root) / (1 - root), Metal.IndexFor(face, 0).Real, 1e-9, $"glass, {face}");
            // The root of what rounding leaves of nought.
            Assert.AreEqual(0, Metal.IndexFor(face, 0).Imaginary, 1e-5, $"glass swallows nothing, {face}");
            Assert.AreEqual((1 - face) / (1 + face), Metal.IndexFor(face, 1).Real, 1e-9, $"the least, {face}");
        }

        double slant = Math.Cos(80 * Math.PI / 180);

        Assert.IsTrue(Metal.Reflectance(slant, Metal.IndexFor(0.64, 1)) > 0.85, "a bright edge did not climb");
        Assert.IsTrue(Metal.Reflectance(slant, Metal.IndexFor(0.64, 0)) < 0.55, "a dark edge did not dip");
        Assert.AreSame(Metal.FromColor(copper), Metal.FromColor(new Color(0.955, 0.638, 0.538)), "made twice");
    }

    /// <summary>
    /// A metal under water, or under a coat, mirrors against what lies outside it, and so less square-on than in
    /// air; under a coat, at the angle the light is bent to inside the coat.
    /// </summary>
    [TestMethod]
    public void TestWhatLiesOutsideAMetalMatters()
    {
        Metal gold = Metal.Named("gold");
        RgbSpectrum inAir = gold.ShareAt<RgbSpectrum>(1, 1);
        RgbSpectrum inWater = gold.ShareAt<RgbSpectrum>(1, 1.33);
        Coat coat = new ();
        RgbSpectrum coated = new BareMetal(gold, 1, 1, coat).Mirrors<RgbSpectrum>(0.3, 0);
        double inside = Math.Sqrt(1 - (1 - 0.09) / (1.5 * 1.5));

        Assert.IsTrue(inWater.Red < inAir.Red && inWater.Green < inAir.Green && inWater.Blue < inAir.Blue,
            $"in water {inWater}, in air {inAir}");
        Assert.AreEqual(gold.ShareAt<RgbSpectrum>(inside, 1.5).Green, coated.Green, 1e-12, "under a coat");
    }

    /// <summary>
    /// Where the surface is part metal, part something else, what it mirrors is each one's share of what that one
    /// mirrors; and a mixture of two metals mirrors each one's share of each.
    /// </summary>
    [TestMethod]
    public void TestPartOfASurfaceMayBeMetal()
    {
        Metal gold = Metal.Named("gold");
        Metal silver = Metal.Named("silver");
        RgbSpectrum half = new BareMetal(gold, 0.5, 1).Mirrors<RgbSpectrum>(0.8, 0.04);
        RgbSpectrum mixed = Metal.Mixed(gold, silver, 0.25).ShareAt<RgbSpectrum>(0.8, 1);
        RgbSpectrum goldAt = gold.ShareAt<RgbSpectrum>(0.8, 1);
        RgbSpectrum silverAt = silver.ShareAt<RgbSpectrum>(0.8, 1);

        Assert.AreEqual(goldAt.Blue * 0.5 + 0.02, half.Blue, 1e-12, "half bare");
        Assert.AreEqual(goldAt.Blue * 0.75 + silverAt.Blue * 0.25, mixed.Blue, 1e-12, "gold and silver");
        Assert.AreSame(gold, Metal.Mixed(gold, gold, 0.5), "a metal mixed with itself");
    }

    /// <summary>
    /// A material blend between a metal and grime keeps the metal where it is bare and the grime's own numbers
    /// where it is not: half covered is half metal, its color, light and gloss wholly the grime's.
    /// </summary>
    [TestMethod]
    public void TestABlendKeepsWhatIsNotMetalWhole()
    {
        Material metal = new () { Metal = Metal.Named("brass"), Metallic = 1, Diffuse = 0.9, Reflective = 0 };
        Material grime = new () { Pigment = new SolidPigment(Colors.Brown), Diffuse = 0.6, Reflective = 0.04 };
        Material half = MaterialBlend.Mixed(metal, grime, 0.5);
        Material gold = new () { Metal = Metal.Named("gold"), Metallic = 1 };
        Material both = MaterialBlend.Mixed(metal, gold, 0.5);

        Assert.AreSame(metal.Metal, half.Metal);
        Assert.AreEqual(0.5, half.Metallic, 1e-12, "how much is bare");
        Assert.AreEqual(0.6, half.Diffuse, 1e-12, "the grime's diffuse");
        Assert.AreEqual(0.04, half.Reflective, 1e-12, "the grime's gloss");
        Assert.AreEqual(1, both.Metallic, 1e-12, "two metals are all metal");
        Assert.AreEqual(
            (Metal.Named("brass").AverageAt(1, 1) + Metal.Named("gold").AverageAt(1, 1)) / 2,
            both.Metal.AverageAt(1, 1), 1e-12, "two metals mixed");
    }

    /// <summary>
    /// Bare metal mirrors the sky by its own share of each color and shows nothing of its own, however bright
    /// its pigment and however much ambient light it would take: looked at straight down, a gold floor under a
    /// white sky is gold's square-on share, and looked across, its share there.
    /// </summary>
    [TestMethod]
    public void TestBareMetalShowsOnlyWhatItMirrors()
    {
        Scene scene = new () { Background = new SolidPigment(Colors.White) };
        Plane floor = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0.5, Diffuse = 0.9, Metal = Metal.Named("gold"),
                Metallic = 1
            }
        };

        scene.Lights.Add(new PointLight { Location = new Point(0, 10, -10) });
        scene.Surfaces.Add(floor);

        foreach (double cos in new[] { 1, 0.2 })
        {
            RgbSpectrum gold = Metal.Named("gold").ShareAt<RgbSpectrum>(cos, 1);
            Color seen = scene.GetHitColor(Hit(floor, cos), 1);

            Assert.AreEqual(gold.Red, seen.Red, 1e-9, $"red at {cos}");
            Assert.AreEqual(gold.Green, seen.Green, 1e-9, $"green at {cos}");
            Assert.AreEqual(gold.Blue, seen.Blue, 1e-9, $"blue at {cos}");
        }
    }

    /// <summary>
    /// A metal's highlight is the lamp's light in the metal's own color: a smooth one's Phong highlight is the
    /// metal's share where the lamp is mirrored, and a rough one's the facets' highlight in the metal's share at
    /// the angle the eye meets them.
    /// </summary>
    [TestMethod]
    public void TestAMetalsHighlightIsItsOwnColor()
    {
        Metal copper = Metal.Named("copper");
        double cos = Math.Cos(Math.PI / 6);

        foreach (double roughness in new[] { 0, 0.3 })
        {
            Scene scene = new () { Background = new SolidPigment(Colors.Black) };
            Plane floor = new ()
            {
                Material = new Material
                {
                    Ambient = 0, Specular = 0.9, Shininess = 50, Metal = copper, Metallic = 1, Roughness = roughness,
                    Reflective = roughness > 0 ? 0.04 : 0
                }
            };

            // The lamp where the eye's ray, coming down at thirty degrees, is mirrored.
            scene.Lights.Add(new PointLight { Location = new Point(0, 10 * cos, 10 * Math.Sin(Math.PI / 6)) });
            scene.Surfaces.Add(floor);

            Color seen = scene.GetHitColor(Hit(floor, cos), 1);
            RgbSpectrum share = copper.ShareAt<RgbSpectrum>(cos, 1);

            Assert.IsTrue(seen.Red > 0.1, $"there was no highlight at roughness {roughness}: {seen}");
            Assert.AreEqual(share.Green / share.Red, seen.Green / seen.Red, 1e-6, $"green, roughness {roughness}");
            Assert.AreEqual(share.Blue / share.Red, seen.Blue / seen.Red, 1e-6, $"blue, roughness {roughness}");
        }
    }

    /// <summary>
    /// A rough metal under an even white sky mirrors close to its own share -- a little less, for the facets that
    /// hide one another -- in its own color.
    /// </summary>
    [TestMethod]
    public void TestARoughMetalMirrorsItsOwnColor()
    {
        Scene scene = new () { Background = new SolidPigment(Colors.White) };
        Plane floor = new ()
        {
            Material = new Material { Metal = Metal.Named("gold"), Metallic = 1, Roughness = 0.3, Reflective = 0.04 }
        };

        scene.Surfaces.Add(floor);

        Color seen = scene.GetReflectionColor(Hit(floor, 1), 1);
        RgbSpectrum gold = Metal.Named("gold").ShareAt<RgbSpectrum>(1, 1);

        Assert.AreEqual(gold.Red, seen.Red, 0.05, "red");
        Assert.AreEqual(gold.Green, seen.Green, 0.05, "green");
        Assert.AreEqual(gold.Blue, seen.Blue, 0.05, "blue");
        Assert.IsTrue(seen.Red <= gold.Red + 1e-9, "the facets mirrored more than the metal");
    }

    /// <summary>
    /// A film on a metal lies on the metal's own index: a film with the index of what lies outside it is no film,
    /// and mirrors exactly what the bare metal does; an oxide a few dozen nanometers thick colors titanium as
    /// anodizing does, where bare titanium is a gray.
    /// </summary>
    [TestMethod]
    public void TestAFilmLiesOnTheMetal()
    {
        Metal titanium = Metal.Named("titanium");
        Span<double> perBand = stackalloc double[SpectralColor.Bands];

        new ThinFilm { Thickness = 120, IndexOfRefraction = 1 }.ReflectanceAt(120, 0.7, 1, 1.5, perBand, titanium);

        for (int band = 0; band < SpectralColor.Bands; band++)
            Assert.AreEqual(Metal.Reflectance(0.7, titanium.IndexIn(band)), perBand[band], 1e-12, $"band {band}");

        new ThinFilm { Thickness = 60, IndexOfRefraction = 2.3 }.ReflectanceAt(60, 1, 1, 1.5, perBand, titanium);

        double spread = perBand.ToArray().Max() - perBand.ToArray().Min();
        double bare = Enumerable.Range(0, SpectralColor.Bands).Max(band => Metal.Reflectance(1, titanium.IndexIn(band))) -
                      Enumerable.Range(0, SpectralColor.Bands).Min(band => Metal.Reflectance(1, titanium.IndexIn(band)));

        Assert.IsTrue(spread > 0.3, $"the oxide colored titanium by only {spread}");
        Assert.IsTrue(bare < 0.15, $"bare titanium spread {bare}");
    }

    /// <summary>
    /// A scene says a surface is a metal by name or by color: what it mirrors under a white sky, looked at straight
    /// down, is the metal's, in red, green and blue and in a spectral render alike; under a coat, the coat's own
    /// share and the metal's beneath it.  An unknown metal, an edge on a named one, a color past what a surface
    /// can mirror and a metal that lets light through are refused.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysASurfaceIsAMetal()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"metal-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            Color Seen(string finish, string context = "")
            {
                File.WriteAllText(path,
                    $"context {{ no gamma  {context} }}\n" +
                    "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 1 }\n" +
                    "background White\n" +
                    "plane { material { specular 0  " + finish + " } }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{finish}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2);
            }

            RgbSpectrum gold = Metal.Named("gold").ShareAt<RgbSpectrum>(1, 1);
            Color named = Seen("metal 'gold'");
            Color spectral = Seen("metal 'gold'", "spectral");
            Color colored = Seen("metal [0.9, 0.6, 0.5]  edge [1, 0.8, 0.6]");

            Assert.AreEqual(gold.Red, named.Red, 2e-4, "gold's red");
            Assert.AreEqual(gold.Green, named.Green, 2e-4, "gold's green");
            Assert.AreEqual(gold.Blue, named.Blue, 2e-4, "gold's blue");
            Assert.AreEqual(named.Red, spectral.Red, 2.0 / 255, "spectral, red");
            Assert.AreEqual(named.Green, spectral.Green, 2.0 / 255, "spectral, green");
            Assert.AreEqual(named.Blue, spectral.Blue, 2.0 / 255, "spectral, blue");
            Assert.AreEqual(0.9, colored.Red, 0.01, "a color's red");
            Assert.AreEqual(0.6, colored.Green, 0.01, "a color's green");
            Assert.AreEqual(0.5, colored.Blue, 0.01, "a color's blue");
            Assert.IsTrue(Seen("metallic metal 'silver'").Green > 0.9, "after metallic");

            // A film on a metal: the oxide on titanium, which is what the film works out over the metal's own index.
            Color anodized = Seen("metal 'titanium'  film { thickness 60  ior 2.3 }");
            Color film = new ThinFilm { Thickness = 60, IndexOfRefraction = 2.3 }
                .SharesAt<RgbSpectrum>(60, 1, 1, 1, Metal.Named("titanium")).Mirrored.ToColor();

            Assert.AreEqual(film.Red, anodized.Red, 2e-4, "anodized, red");
            Assert.AreEqual(film.Green, anodized.Green, 2e-4, "anodized, green");
            Assert.AreEqual(film.Blue, anodized.Blue, 2e-4, "anodized, blue");

            // Under a coat: the coat's four percent, and the gold beneath against the coat's index, crossing it
            // both ways.
            Color coated = Seen("metal 'gold'  coat { }");
            double beneath = Metal.Named("gold").ShareAt<RgbSpectrum>(1, 1.5).Blue;

            Assert.AreEqual(0.04 + beneath * 0.96 * 0.96, coated.Blue, 2e-4, "under a coat");

            Assert.ThrowsExactly<TokenException>(() => Seen("metal 'unobtainium'"), "an unknown metal");
            Assert.ThrowsExactly<TokenException>(() => Seen("metal 'gold' edge White"), "an edge on a named metal");
            Assert.ThrowsExactly<TokenException>(() => Seen("metal [1.2, 0.5, 0.5]"), "more than all of red");

            File.WriteAllText(path,
                "camera { location [0, 3, 0]  look at [0, 0, 0]  up [0, 0, 1] }\n" +
                "plane { material { metal 'gold'  transparency 0.5 } }\n");

            TextWriter was = Console.Out;

            Console.SetOut(new StringWriter());

            try
            {
                Assert.IsNull(new LanguageParser(path).Parse(), "a metal letting light through");
            }
            finally
            {
                Console.SetOut(was);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// This method returns where a ray meets the floor's middle, coming down at the angle whose cosine against
    /// the floor's normal is given, prepared for shading.
    /// </summary>
    private static Intersection Hit(Plane floor, double cos)
    {
        double sin = Math.Sqrt(1 - cos * cos);
        Ray ray = new (new Point(0, cos, -sin), new Vector(0, -cos, sin));
        Intersection intersection = new (floor, 1);

        intersection.PrepareUsing(ray, [intersection]);

        return intersection;
    }
}
