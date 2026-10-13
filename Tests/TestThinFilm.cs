using Complex = System.Numerics.Complex;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover thin films: a skin thinner than light's wavelength -- a soap bubble, oil on a
/// puddle, a lens's coating -- which colors what a surface mirrors by interference.
/// </summary>
[TestClass]
public class TestThinFilm
{
    private const int Bands = SpectralColor.Bands;

    // The band whose middle is nearest 550 nm, and its wavelength.
    private const int Middle = 13;

    private static readonly double MiddleWavelength = SpectralColor.WavelengthOf(Middle);

    private string _directory;

    [TestInitialize]
    public void MakeDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"film-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        Directory.Delete(_directory, true);
    }

    private static double[] Reflectance(
        double thickness, double film, double cos, double outside, double beneath)
    {
        double[] perBand = new double[Bands];

        new ThinFilm { IndexOfRefraction = film }.ReflectanceAt(thickness, cos, outside, beneath, perBand);

        return perBand;
    }

    /// <summary>
    /// Fresnel's equations for one face, exactly, for both polarizations averaged.
    /// </summary>
    private static double Fresnel(double cos, double near, double far)
    {
        double sin = Math.Sqrt(1 - cos * cos) * near / far;
        double cosFar = Math.Sqrt(1 - sin * sin);
        double s = (near * cos - far * cosFar) / (near * cos + far * cosFar);
        double p = (far * cos - near * cosFar) / (far * cos + near * cosFar);

        return (s * s + p * p) / 2;
    }

    /// <summary>
    /// A film of no thickness is no film at all: what is mirrored is what the bare face between the two
    /// sides would mirror, by Fresnel's equations exactly, at every wavelength and every angle.
    /// </summary>
    [TestMethod]
    public void TestAFilmOfNoThicknessIsTheBareFace()
    {
        foreach (double cos in new[] { 1, 0.8, 0.5, 0.1 })
        {
            double[] perBand = Reflectance(0, 1.38, cos, 1, 1.5);
            double expected = Fresnel(cos, 1, 1.5);

            foreach (double share in perBand)
                Assert.AreEqual(expected, share, 1e-12, $"at a cosine of {cos}");
        }

        Assert.AreEqual(0.04, Reflectance(0, 1.38, 1, 1, 1.5)[0], 1e-12, "glass mirrors four percent square-on");
    }

    /// <summary>
    /// **A lens's coating.**  A film whose index lies halfway between the air's and the glass's, by
    /// their product, and a quarter of a wave thick, mirrors nothing at that wavelength square-on: the
    /// two reflections are equal and half a wave apart.  Either side of it, it mirrors a little.
    /// </summary>
    [TestMethod]
    public void TestAQuarterWaveCoatingMirrorsNothing()
    {
        double film = Math.Sqrt(1.5);
        double[] perBand = Reflectance(MiddleWavelength / (4 * film), film, 1, 1, 1.5);

        Assert.AreEqual(0, perBand[Middle], 1e-12, "a quarter-wave coating should mirror nothing at its wavelength");
        Assert.IsTrue(perBand[0] > 0.003 && perBand[Bands - 1] > 0.003,
            $"either end of the spectrum it should mirror a little: {perBand[0]}, {perBand[Bands - 1]}");
        Assert.IsTrue(perBand.All(share => share < 0.04), "a coating should mirror less than bare glass everywhere");
    }

    /// <summary>
    /// **A soap film in air.**  One face mirrors with a half-wave's turn and the other without, so the
    /// film mirrors nothing where twice its optical thickness is a whole number of waves, and most
    /// where it is a whole number and a half -- as much as two faces mirroring in step can.
    /// </summary>
    [TestMethod]
    public void TestASoapFilmCancelsAndReinforcesAsItsThicknessSays()
    {
        const double soap = 1.33;

        double[] cancelled = Reflectance(MiddleWavelength / (2 * soap), soap, 1, 1, 1);
        double[] reinforced = Reflectance(0.75 * MiddleWavelength / soap, soap, 1, 1, 1);
        double face = (soap - 1) / (soap + 1);
        double most = 2 * face / (1 + face * face);

        Assert.AreEqual(0, cancelled[Middle], 1e-12, "a film a whole wave thick, there and back, mirrors nothing");
        Assert.AreEqual(most * most, reinforced[Middle], 1e-12, "a film a wave and a half thick mirrors the most");
        Assert.IsTrue(reinforced.All(share => share <= most * most + 1e-12), "nothing should mirror more than that");
    }

    /// <summary>
    /// **Seen at a slant, a film's colors move toward the blue**: light crosses it at a slant too, and
    /// the slanting crossing is shorter than the film is thick, by the cosine of the angle inside it.
    /// </summary>
    [TestMethod]
    public void TestASlantMovesTheColorsTowardTheBlue()
    {
        const double soap = 1.33;
        double inside = Math.Sqrt(1 - 0.64 / (soap * soap));
        double thickness = MiddleWavelength / (2 * soap * inside);

        Assert.AreEqual(0, Reflectance(thickness, soap, 0.6, 1, 1)[Middle], 1e-12,
            "at the slant, the film should mirror nothing in the middle of the spectrum");
        Assert.IsTrue(Reflectance(thickness, soap, 1, 1, 1)[Middle] > 0.03,
            "square-on, the same film should mirror plenty there");

        double[] square = Reflectance(thickness, soap, 1, 1, 1);
        int darkest = Array.IndexOf(square, square.Min());

        Assert.IsTrue(darkest > Middle + 5, $"square-on the darkest band should be well toward the red, not {darkest}");
    }

    /// <summary>
    /// **Light that cannot leave is all mirrored.**  From inside glass, past the angle at which light can
    /// get out into the air, a film of soap on the glass mirrors every color whole, however thick.
    /// </summary>
    [TestMethod]
    public void TestLightThatCannotLeaveIsAllMirrored()
    {
        foreach (double thickness in new[] { 0, 50, 300, 1200 })
        {
            foreach (double share in Reflectance(thickness, 1.33, 0.5, 1.5, 1))
                Assert.AreEqual(1, share, 1e-9, $"at {thickness} nm");
        }
    }

    /// <summary>
    /// **A gap too thin to stop light.**  Two blocks of glass with a gap of air between them, met past
    /// the angle at which light can cross into air: a thick gap mirrors everything, but light reaches a
    /// little way past a face it cannot cross, so across a gap a few nanometers wide most of it gets
    /// through.  This is the arithmetic in complex numbers having its say.
    /// </summary>
    [TestMethod]
    public void TestLightTunnelsAcrossAThinGap()
    {
        double[] thin = Reflectance(5, 1, 0.5, 1.5, 1.5);
        double[] thick = Reflectance(2000, 1, 0.5, 1.5, 1.5);

        Assert.IsTrue(thin.All(share => share is >= 0 and < 0.2), $"across a thin gap most should get through: {thin[Middle]}");
        Assert.IsTrue(thick.All(share => share > 0.999999), $"across a thick one none should: {thick[Middle]}");
        Assert.IsTrue(thin[0] > thin[Bands - 1], "the blue, whose waves are shorter, should tunnel less");
    }

    /// <summary>
    /// **A film mirrors as much from either side**, the angles on the two sides being those a ray
    /// crossing it would have -- which a film that took no light for itself must.
    /// </summary>
    [TestMethod]
    public void TestAFilmMirrorsAlikeFromEitherSide()
    {
        double far = Math.Sqrt(1 - 0.36 / (1.5 * 1.5));

        foreach (double thickness in new[] { 120, 400, 900 })
        {
            double[] from = Reflectance(thickness, 1.38, 0.8, 1, 1.5);
            double[] back = Reflectance(thickness, 1.38, far, 1.5, 1);

            for (int band = 0; band < Bands; band++)
                Assert.AreEqual(from[band], back[band], 1e-12, $"band {band} at {thickness} nm");
        }
    }

    /// <summary>
    /// **Checked against another way of working it out.**  Optics also works a film out as a matrix
    /// carrying the light's field across it, which never takes a square root's sign and so cannot choose
    /// it wrongly; the two must agree, whatever the film, the sides and the angle -- including light
    /// tunneling across a gap into denser glass beyond, and light that cannot leave at all.
    /// <para>
    /// The sign itself turns out not to matter for a film that takes no light for itself: where light
    /// cannot enter it, both its faces mirror wholly, and then a reflection growing across the film
    /// comes to what one dying away does.  So the agreement checks the polarizations and the faces.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestTheFilmAgreesWithTheMatrixMethod()
    {
        (double Thickness, double Film, double Cos, double Outside, double Beneath)[] cases =
        [
            (120, 1.38, 0.8, 1, 1.5), (450, 1.33, 0.3, 1, 1), (60, 1, 0.5, 1.5, 1.7),
            (150, 1, 0.5, 1.5, 1.7), (80, 1.2, 0.3, 1.5, 1.33), (300, 2.2, 0.95, 1.33, 1.5),
            (40, 1, 0.4, 1.6, 1.2)
        ];

        foreach ((double thickness, double film, double cos, double outside, double beneath) in cases)
        {
            double[] perBand = Reflectance(thickness, film, cos, outside, beneath);

            foreach (int band in new[] { 0, Middle, Bands - 1 })
            {
                double expected = ByMatrix(
                    thickness, film, cos, outside, beneath, SpectralColor.WavelengthOf(band));

                Assert.AreEqual(expected, perBand[band], 1e-12,
                    $"{thickness} nm of {film} between {outside} and {beneath} at a cosine of {cos}, band {band}");
            }
        }
    }

    /// <summary>
    /// The characteristic matrix of a film, for both polarizations, averaged.
    /// </summary>
    private static double ByMatrix(
        double thickness, double film, double cos, double outside, double beneath, double wavelength)
    {
        double sinSquared = 1 - cos * cos;
        Complex inside = Complex.Sqrt(1 - outside * outside / (film * film) * sinSquared);
        Complex under = Complex.Sqrt(1 - outside * outside / (beneath * beneath) * sinSquared);
        Complex phase = 2 * Math.PI * film * thickness * inside / wavelength;
        double sum = 0;

        foreach (bool across in new[] { true, false })
        {
            Complex Offered(double index, Complex angle) => across ? index * angle : index / angle;

            Complex near = Offered(outside, cos);
            Complex middle = Offered(film, inside);
            Complex far = Offered(beneath, under);
            Complex b = Complex.Cos(phase) - Complex.ImaginaryOne * Complex.Sin(phase) / middle * far;
            Complex c = -Complex.ImaginaryOne * middle * Complex.Sin(phase) + Complex.Cos(phase) * far;
            double magnitude = ((near * b - c) / (near * b + c)).Magnitude;

            sum += magnitude * magnitude;
        }

        return sum / 2;
    }

    /// <summary>
    /// **A thick film shows no colors**: its swings through the spectrum come so close together that
    /// each band holds several, and the band mirrors their average -- near enough white.  Sampled once
    /// a band, it would show colors at random instead.
    /// </summary>
    [TestMethod]
    public void TestAThickFilmShowsNoColors()
    {
        ThinFilm film = new () { IndexOfRefraction = 1.33 };
        double[] perBand = new double[Bands];
        double average = film.ReflectanceAt(20000, 1, 1, 1, perBand);

        Assert.IsTrue(perBand.All(share => Math.Abs(share / average - 1) < 0.3),
            $"each band should mirror near the average, {average}: from {perBand.Min()} to {perBand.Max()}");

        Color color = film.SharesAt<RgbSpectrum>(20000, 1, 1, 1).Mirrored.ToColor();
        double most = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        double least = Math.Min(color.Red, Math.Min(color.Green, color.Blue));

        Assert.IsTrue(most - least < 0.1 * most, $"a thick film should mirror nearly gray, not {color}");
    }

    /// <summary>
    /// What a film mirrors on average over every thickness is what it reports as its average, which is
    /// what a film too thick to show colors mirrors.  A highlight is colored against it.
    /// </summary>
    [TestMethod]
    public void TestTheAverageIsTheAverageOverThickness()
    {
        ThinFilm film = new () { IndexOfRefraction = 1.33 };
        double[] perBand = new double[Bands];
        double sum = 0;
        double reported = 0;
        const int steps = 4000;

        // Four whole swings of the middle band, sampled evenly: each is a crossing there and back
        // longer by a wave, at the slant the light crosses the film at.  The film is kept thin enough
        // that the band is sampled once, at its middle.
        double inside = Math.Sqrt(1 - 0.51 / (1.33 * 1.33));
        double swing = MiddleWavelength / (2 * 1.33 * inside);

        for (int step = 0; step < steps; step++)
        {
            reported = film.ReflectanceAt(100 + 4 * swing * step / steps, 0.7, 1, 1, perBand);
            sum += perBand[Middle];
        }

        Assert.AreEqual(reported, sum / steps, 1e-6);
    }

    /// <summary>
    /// The shares a film hands the shading are what it mirrors, the rest, and the highlight's color --
    /// band by band in a spectral render, and as the color those bands make in one carrying three.
    /// </summary>
    [TestMethod]
    public void TestTheSharesAreWhatIsMirroredAndTheRest()
    {
        ThinFilm film = new () { IndexOfRefraction = 1.33 };
        double[] perBand = new double[Bands];
        double average = film.ReflectanceAt(350, 0.9, 1, 1.5, perBand);
        FilmShares<BandSpectrum> bands = film.SharesAt<BandSpectrum>(350, 0.9, 1, 1.5);
        FilmShares<RgbSpectrum> rgb = film.SharesAt<RgbSpectrum>(350, 0.9, 1, 1.5);

        for (int band = 0; band < Bands; band++)
        {
            Assert.AreEqual(perBand[band], bands.Mirrored[band], 1e-15);
            Assert.AreEqual(1 - perBand[band], bands.Passed[band], 1e-15);
            Assert.AreEqual(perBand[band] / average, bands.Highlight[band], 1e-12);
        }

        Assert.IsTrue(SpectralColor.ToColor(perBand).Matches(rgb.Mirrored.ToColor()));
        Assert.IsTrue((Colors.White - SpectralColor.ToColor(perBand)).Matches(rgb.Passed.ToColor()));
    }

    /// <summary>
    /// A pattern sets a film's thickness from its thinnest, where the pattern's value is nought, to its
    /// thickest, where it is one, read in the surface's own space.
    /// </summary>
    [TestMethod]
    public void TestAPatternSetsTheThickness()
    {
        ThinFilm film = new ()
        {
            Thickness = 200, Thickest = 600, Pattern = new DensityShape { Pattern = new GradientPattern() }
        };

        Assert.AreEqual(200, film.ThicknessAt(new Point(0, 0, 0)), 1e-9);
        Assert.AreEqual(300, film.ThicknessAt(new Point(0.25, 0, 0)), 1e-9);
        Assert.AreEqual(500, film.ThicknessAt(new Point(0.75, 3, 1)), 1e-9);
        Assert.AreEqual(450, new ThinFilm { Thickness = 450 }.ThicknessAt(null));
    }

    private const string Soapy = "film { thickness 380  ior 1.33 }";

    private static Color Mirrored(double thickness, double cos, double outside, double beneath)
    {
        return new ThinFilm { IndexOfRefraction = 1.33 }
            .SharesAt<RgbSpectrum>(thickness, cos, outside, beneath).Mirrored.ToColor();
    }

    /// <summary>
    /// **What an opaque surface mirrors, a film colors.**  A black surface with a film, seen square-on
    /// against white, shows the film's color and nothing else -- with no reflective said, the film
    /// mirroring for itself.  It lies on the material's own index beneath.
    /// </summary>
    [TestMethod]
    public void TestAFilmColorsWhatASurfaceMirrors()
    {
        Color seen = Center($$"""
            background White
            camera { location [0, 5, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 2 }
            plane {
                material {
                    pigment Black  ambient 0  diffuse 0  specular 0
                    interior { ior 1.5 }
                    {{Soapy}}
                }
            }
            """);
        Color expected = Mirrored(380, 1, 1, 1.5);

        Assert.IsTrue(Near(expected, seen), $"expected {expected}, saw {seen}");
        Assert.IsTrue(Colorful(expected), $"the film's color should be a color: {expected}");
    }

    /// <summary>
    /// A film works out what it mirrors for itself, in place of what reflective and fresnel would say,
    /// so saying them changes nothing -- neither what is mirrored nor what the surface shows of itself.
    /// </summary>
    [TestMethod]
    public void TestAFilmTakesThePlaceOfReflectiveAndFresnel()
    {
        string Scene(string finish) => $$"""
            background White
            camera { location [0, 5, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 2 }
            point light { location [0, 5, 0] }
            plane {
                material {
                    pigment Gray50  ambient 0  diffuse 1  specular 0  {{finish}}
                    interior { ior 1.5 }
                    {{Soapy}}
                }
            }
            """;
        Color plain = Center(Scene(""));
        Color finished = Center(Scene("reflective 0.7  fresnel"));

        Assert.IsTrue(Near(plain, finished, 1e-9), $"without {plain}, with {finished}");
    }

    /// <summary>
    /// The same in a spectral render, which works the film out band by band and converts once, at the
    /// end -- and comes to the same color under white light.
    /// </summary>
    [TestMethod]
    public void TestASpectralRenderAgrees()
    {
        string scene = $$"""
            background White
            camera { location [0, 5, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 2 }
            plane {
                material {
                    pigment Black  ambient 0  diffuse 0  specular 0
                    interior { ior 1.5 }
                    {{Soapy}}
                }
            }
            """;
        Color rgb = Center(scene);
        Color spectral = Center(scene, "spectral");

        Assert.IsTrue(Near(rgb, spectral, 0.02), $"in three bands {rgb}, in all of them {spectral}");
    }

    /// <summary>
    /// **What a film does not mirror goes on.**  A clear sheet with a film, seen square-on with white
    /// beyond it and black behind the eye, shows the complement of the film's color -- what it lets
    /// through -- and against white on both sides it vanishes, which is light neither made nor lost.
    /// </summary>
    [TestMethod]
    public void TestWhatIsNotMirroredGoesThrough()
    {
        const string sheet = """
            plane {
                material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1  FILM }
                rotate X -90
            }
            """;
        string behind = """
            plane { material { pigment Black  ambient 0  diffuse 0  specular 0 }  rotate X -90  translate Z -10 }
            """;
        const string camera = "camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 2 }";

        Color through = Center($"background White\n{camera}\n{behind}\n{sheet.Replace("FILM", Soapy)}");
        Color expected = Colors.White - Mirrored(380, 1, 1, 1);

        Assert.IsTrue(Near(expected, through), $"expected {expected}, saw {through}");
        Assert.IsTrue(Near(Colors.White, Center($"background White\n{camera}\n{sheet.Replace("FILM", Soapy)}")),
            "against white everywhere a film should vanish");
    }

    /// <summary>
    /// What an opaque surface shows of its own is only what its film lets on.  A white surface lit
    /// square-on by a lamp at the eye, with nothing to mirror, shows the complement of the film.
    /// </summary>
    [TestMethod]
    public void TestASurfaceShowsOnlyWhatItsFilmLetsOn()
    {
        Color seen = Center($$"""
            camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 2 }
            point light { location [0, 0, -5] }
            plane {
                material { pigment White  ambient 0  diffuse 1  specular 0  {{Soapy}} }
                rotate X -90
            }
            """);
        Color expected = Colors.White - Mirrored(380, 1, 1, 1);

        Assert.IsTrue(Near(expected, seen), $"expected {expected}, saw {seen}");
    }

    /// <summary>
    /// **A bubble's highlight.**  Clear glass shows no highlight, its own color being all let through; a
    /// film mirrors its highlight as it mirrors anything else, so a bubble that stops nothing still
    /// shows one -- in the film's color.
    /// </summary>
    [TestMethod]
    public void TestABubbleShowsAHighlight()
    {
        Color Brightest(string film)
        {
            Canvas picture = Rendered($$"""
                camera { location [0, 0, -5]  look at [0, 0, 0]  field of view 30 }
                point light { location [0, 0, -50] }
                sphere {
                    material { pigment White  ambient 0  diffuse 0  specular 1  transparency 1  {{film}} }
                }
                """, 31, 31);

            return picture.GetPixel(15, 15);
        }

        Color plain = Brightest("");
        Color bubble = Brightest(Soapy);

        Assert.IsTrue(plain.Red + plain.Green + plain.Blue < 0.01, $"clear glass showed a highlight: {plain}");
        Assert.IsTrue(bubble.Red + bubble.Green + bubble.Blue > 0.3, $"the bubble showed no highlight: {bubble}");
        Assert.IsTrue(Colorful(bubble), $"the bubble's highlight should be colored: {bubble}");
    }

    /// <summary>
    /// **A light a ray can meet gives a film no highlight.**  A film always mirrors, so the sky already
    /// shows in what it mirrors, and a highlight from each of the sky's samples besides would scatter
    /// dots over every bubble under it.  A lamp, which no ray meets, gives its highlight as ever.
    /// </summary>
    [TestMethod]
    public void TestALightARayCanMeetGivesAFilmNoHighlight()
    {
        Material material = new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 1
        };
        Sphere ball = new () { Material = material };
        Vector up = new (0, 1, 0);
        Point top = new (0, 1, 0);
        LightSample overhead = new (up, double.PositiveInfinity, 1);
        FilmShares<RgbSpectrum> shares =
            new ThinFilm { IndexOfRefraction = 1.33 }.SharesAt<RgbSpectrum>(380, 1, 1, 1);

        Color Lit(Light light) => light.ApplyPhong(
            top, up, up, ball, overhead, RgbSpectrum.White, material: material, film: shares).ToColor();

        Color lamp = Lit(new PointLight { Location = new Point(0, 10, 0) });
        Color sky = Lit(new SkyLight { Pigment = new SolidPigment(Colors.White) });

        Assert.IsTrue(Near(shares.Highlight.ToColor(), lamp, 1e-9),
            $"the lamp's highlight should be the film's color: {lamp}");
        Assert.IsTrue(Near(Colors.Black, sky, 1e-12), $"the sky gave the film a highlight: {sky}");
    }

    /// <summary>
    /// Light crossing a film on its way to make a shadow loses what the film mirrors, so a clear pane
    /// with a film over a white floor shades it the complement of the film's color.
    /// </summary>
    [TestMethod]
    public void TestAShadowThroughAFilmIsItsComplement()
    {
        Color floor = Center($$"""
            camera { location [0, 0.5, -0.01]  look at [0, 0, 0]  up [0, 0, 1]  field of view 2 }
            point light { location [0, 20, 0] }
            plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }
            plane {
                material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1  {{Soapy}} }
                translate Y 1
            }
            """);
        Color expected = Colors.White - Mirrored(380, 1, 1, 1);

        Assert.IsTrue(Near(expected, floor), $"expected {expected}, saw {floor}");
    }

    /// <summary>
    /// A shadow ray crossing coated glass goes straight on, unbent, so it meets the far face at the
    /// slant it met the near one -- past the angle at which light inside glass can leave.  Bent light
    /// would not, so each face is charged as though met from the surroundings, and a slab of coated
    /// glass lit at a slant passes what its film lets through twice over rather than nothing.
    /// </summary>
    [TestMethod]
    public void TestAShadowThroughCoatedGlassIsChargedFromOutside()
    {
        Color floor = Center($$"""
            camera { location [0, 0.5, -0.01]  look at [0, 0, 0]  up [0, 0, 1]  field of view 2 }
            point light { location [40, 30, 0] }
            plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }
            cube {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0  transparency 1
                    interior { ior 1.5 }
                    {{Soapy}}
                }
                scale [20, 0.05, 20]
                translate Y 1
            }
            """);
        Color passed = Colors.White - Mirrored(380, 0.6, 1, 1.5);
        Color expected = passed * passed * 0.6;

        Assert.IsTrue(Near(expected, floor), $"expected {expected}, saw {floor}");
    }

    /// <summary>
    /// A film's thickness may run between two by a pattern, which colors the surface in bands, and it
    /// may follow the word metallic as any other entry may.
    /// </summary>
    [TestMethod]
    public void TestAPatternedFilmIsBanded()
    {
        Canvas picture = Rendered("""
            background White
            camera { location [0, 5, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 40 }
            plane {
                material {
                    pigment White  ambient 0  diffuse 0  specular 0
                    metallic film { thickness 100 to 900 by linear X gradient { scale 4  translate -2 }  ior 2.2 }
                    interior { ior 1.5 }
                }
            }
            """, 40, 5);
        HashSet<(int, int, int)> colors = [];

        for (int x = 0; x < picture.Width; x++)
        {
            Color color = picture.GetPixel(x, 2);

            colors.Add(((int) (color.Red * 10), (int) (color.Green * 10), (int) (color.Blue * 10)));
        }

        Assert.IsTrue(colors.Count > 8, $"a film thickening across the picture should band it, not {colors.Count} colors");
    }

    /// <summary>
    /// A film needs a thickness; a thickness running between two needs the pattern that says where it
    /// is which; neither may be less than nought, nor its index; and a film is smooth by nature.
    /// </summary>
    [TestMethod]
    public void TestABadFilmIsReported()
    {
        StringAssert.Contains(Complaint("film { ior 1.33 }"), "A film needs a thickness");
        StringAssert.Contains(Complaint("film { thickness 200 to 800 }"), "the pattern that sets the film's thickness");
        StringAssert.Contains(Complaint("film { thickness -1 }"), "may not be less than nought");
        StringAssert.Contains(Complaint("film { thickness 300  ior 0 }"), "must be more than nought");
        StringAssert.Contains(Complaint("roughness 0.2  film { thickness 300 }"), "may not also be rough");
    }

    private static bool Colorful(Color color)
    {
        double most = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        double least = Math.Min(color.Red, Math.Min(color.Green, color.Blue));

        return most > 0.005 && most - least > 0.3 * most;
    }

    private static bool Near(Color expected, Color seen, double tolerance = 2.5 / 255)
    {
        return Math.Abs(expected.Red - seen.Red) <= tolerance &&
               Math.Abs(expected.Green - seen.Green) <= tolerance &&
               Math.Abs(expected.Blue - seen.Blue) <= tolerance;
    }

    private Color Center(string body, string context = "")
    {
        return Rendered(body, 5, 5, context).GetPixel(2, 2);
    }

    private Canvas Rendered(string body, int width, int height, string context = "")
    {
        string scene = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(scene, $"context {{ no gamma  angles are degrees  {context} }}\n" + body + "\n");

        ImageRenderer renderer = new LanguageParser(scene).Parse();

        Assert.IsNotNull(renderer, "the scene did not parse");

        renderer.Render(new RenderOptions
        {
            OutputFileName = output, Width = width, Height = height, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0];
    }

    /// <summary>
    /// This method renders a sphere of the given material entries and returns whatever was said, which
    /// for a bad one is the complaint.
    /// </summary>
    private string Complaint(string entries)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            string scene = Path.Combine(_directory, "bad.igl");

            File.WriteAllText(scene,
                "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
                $"sphere {{ material {{ {entries} }} }}\n");

            new LanguageParser(scene).Parse()?.Render(new RenderOptions
            {
                OutputFileName = Path.Combine(_directory, "bad.png"), Width = 4, Height = 4,
                ProgressStyleText = "none"
            });
        }
        catch (Exception exception)
        {
            return exception.Message + captured;
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }
}
