using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Instructions.Surfaces;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover models read from Wavefront files with what their material libraries say: pictures laid by the
/// models' own texture coordinates, the right way round; materials read from a library onto the faces that ask for
/// them, and put back by a scene's; and what a library says turned into this engine's materials.
/// </summary>
[TestClass]
public class TestModels
{
    private string _directory;

    [TestInitialize]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"models-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    // A square in X and Y facing +Z, as a right-handed tool makes one, its texture coordinates its corners.
    private const string Square = """
        v -1 -1 0
        v 1 -1 0
        v 1 1 0
        v -1 1 0
        vt 0 0
        vt 1 0
        vt 1 1
        vt 0 1
        vn 0 0 1
        """;

    /// <summary>
    /// This helper writes a picture file holding the given colors, and returns its name.
    /// </summary>
    private string Picture(int width, int height, Func<int, int, Color> colorAt, string name = null)
    {
        name ??= $"{Guid.NewGuid():N}.png";

        Canvas canvas = new (width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = colorAt(x, y);

                canvas.SetColor(
                    new Color(color.Red * color.Alpha, color.Green * color.Alpha, color.Blue * color.Alpha, color.Alpha),
                    x, y);
            }
        }

        new ImageFile(Path.Combine(_directory, name)).Save(canvas, new RenderContext { ApplyGamma = false });

        return name;
    }

    // A picture of four quarters: red at the top left, green at the top right, blue at the bottom left, white beyond.
    private string Quarters() => Picture(32, 32, (x, y) => (x < 16, y < 16) switch
    {
        (true, true) => new Color(1, 0, 0),
        (false, true) => new Color(0, 1, 0),
        (true, false) => new Color(0, 0, 1),
        _ => new Color(1, 1, 1)
    });

    /// <summary>
    /// This helper writes a model and its library, renders it square on from the usual camera, unlit but for its own
    /// colors, and returns the picture.
    /// </summary>
    private Canvas Rendered(string model, string library, string extra = "", string light = null)
    {
        File.WriteAllText(Path.Combine(_directory, "model.mtl"), library);
        File.WriteAllText(Path.Combine(_directory, "model.obj"), "mtllib model.mtl\n" + model);

        string scene = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, $"out-{Guid.NewGuid():N}.png");

        File.WriteAllText(scene, $$"""
            context { no gamma }
            camera { location [0, 0, -4]  look at [0, 0, 0]  field of view 50 }
            {{light ?? "point light { location [0, 0, -10] }"}}
            background [0.5, 0.5, 0.5]
            object file { source 'model.obj'  {{extra}} }
            """);
        new LanguageParser(scene).Parse().Render(new RenderOptions
        {
            OutputFileName = output, Width = 40, Height = 40, ProgressStyleText = "none"
        });

        return new ImageFile(output).Load()[0];
    }

    // The color in a picture at a share of the way across and down it.
    private static Color At(Canvas canvas, double across, double down) =>
        canvas.GetPixel((int) (across * canvas.Width), (int) (down * canvas.Height));

    /// <summary>
    /// A model and its picture read as they were made: a pictured square made at a model's front, toward +Z, with a gray
    /// one behind it, stands in front here, toward the usual camera, its picture the right way round -- the top left
    /// quarter at the top left.  Read as it stood, a right-handed model is its own mirror image in a left-handed space:
    /// the camera met the gray square, and lettering on the far one would have read backward from behind it.
    /// </summary>
    [TestMethod]
    public void TestAModelAndItsPictureReadAsTheyWereMade()
    {
        string picture = Quarters();
        string model = "v -1 -1 0.5\nv 1 -1 0.5\nv 1 1 0.5\nv -1 1 0.5\n" +
                       "v -1 -1 -0.5\nv 1 -1 -0.5\nv 1 1 -0.5\nv -1 1 -0.5\n" +
                       "vt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nvn 0 0 1\n" +
                       "usemtl Shown\nf 1/1/1 2/2/1 3/3/1 4/4/1\nusemtl Hidden\nf 8 7 6 5\n";
        Canvas canvas = Rendered(model,
            $"newmtl Shown\nillum 0\nmap_Kd {picture}\nnewmtl Hidden\nillum 0\nKd 0.3 0.3 0.3\n");

        Assert.IsGreaterThan(0.8, At(canvas, 0.3, 0.3).Red, "red at the top left");
        Assert.IsGreaterThan(0.8, At(canvas, 0.7, 0.3).Green, "green at the top right");
        Assert.IsGreaterThan(0.8, At(canvas, 0.3, 0.7).Blue, "blue at the bottom left");
        Assert.IsGreaterThan(0.8, At(canvas, 0.7, 0.7).Red + At(canvas, 0.7, 0.7).Green - 1, "white at the bottom right");

        ObjectFileParser parser = new (content: Square + "\nv 1 2 3\nf 1 2 3\n");
        Triangle triangle = parser.Parse().Surfaces.OfType<Triangle>().Single();

        Assert.IsTrue(new Vector(0, 0, -1).Matches(triangle.SurfaceNormalAt(new Point(0, 0, 0), null)),
            "a face made toward +Z should face the usual camera, at -Z");
        Assert.IsTrue(new Point(1, 2, -3).Matches(parser.Vertices[^1]), "a vertex's Z is negated");
    }

    /// <summary>
    /// A material library's `-s` and `-o` stretch and move where the picture falls: stretched twice, the quarters
    /// come round twice each way, and moved by a half across, the picture slides over by half its width.
    /// </summary>
    [TestMethod]
    public void TestAPicturesCoordinatesAreMovedAndStretched()
    {
        string picture = Quarters();
        string face = Square + "\nusemtl Shown\nf 1/1/1 2/2/1 3/3/1 4/4/1\n";
        Canvas stretched = Rendered(face, $"newmtl Shown\nillum 0\nmap_Kd -s 2 2 {picture}\n");
        Canvas moved = Rendered(face, $"newmtl Shown\nillum 0\nmap_Kd -s 2 2 -o 0.5 0 {picture}\n");

        // The place at three tenths across and seven tenths up the square is red, laid once; stretched twice it lies
        // at 0.6 and 1.4, in the white quarter; moved by a half across as well, at 1.1, in the blue.
        Point place = new (-0.4, 0.4, 0);
        double across = 0.5 + place.X / (2 * 4 * Math.Tan(25 * Math.PI / 180));
        double down = 0.5 - place.Y / (2 * 4 * Math.Tan(25 * Math.PI / 180));
        Color laidOnce = At(Rendered(face, $"newmtl Shown\nillum 0\nmap_Kd {picture}\n"), across, down);
        Color white = At(stretched, across, down), blue = At(moved, across, down);

        Assert.IsTrue(laidOnce.Red > 0.8 && laidOnce.Green < 0.2, $"laid once, the place should be red, not {laidOnce}");
        Assert.IsTrue(white.Red > 0.8 && white.Green > 0.8 && white.Blue > 0.8, $"stretched, it should be white, not {white}");
        Assert.IsTrue(blue.Blue > 0.8 && blue.Red < 0.2, $"moved as well, it should be blue, not {blue}");
    }

    /// <summary>
    /// A picture's clear places are cut out of the model: the background shows through the right half.
    /// </summary>
    [TestMethod]
    public void TestAPicturesClearPlacesAreCutOut()
    {
        string picture = Picture(16, 16, (x, _) => x < 8 ? new Color(1, 0, 0) : new Color(1, 0, 0, 0));
        Canvas canvas = Rendered(Square + "\nusemtl Shown\nf 1/1/1 2/2/1 3/3/1 4/4/1\n",
            $"newmtl Shown\nillum 0\nmap_Kd {picture}\n");
        Color left = At(canvas, 0.3, 0.5), right = At(canvas, 0.7, 0.5);

        Assert.IsGreaterThan(0.8, left.Red, "the opaque half");
        Assert.AreEqual(0.5, right.Red, 0.05, "the background through the clear half");
        Assert.AreEqual(0.5, right.Green, 0.05, "the background through the clear half");
    }

    /// <summary>
    /// A normal map leans the surface the way its picture says, in the frame of the model's own coordinates: a map
    /// leaning every place toward the picture's right lights the square more from a lamp on the right than from one on
    /// the left, and its plain twin alike from both.
    /// </summary>
    [TestMethod]
    public void TestANormalMapLeansTheWayItsPictureSays()
    {
        string normals = Picture(8, 8, (_, _) => new Color(0.85, 0.5, 0.86));
        string face = Square + "\nusemtl Shown\nf 1/1/1 2/2/1 3/3/1 4/4/1\n";
        string library = $"newmtl Shown\nKd 0.8 0.8 0.8\nKs 0 0 0\nnorm {normals}\n";
        double fromRight = At(Rendered(face, library, light: "point light { location [6, 0, -2] }"), 0.5, 0.5).Red;
        double fromLeft = At(Rendered(face, library, light: "point light { location [-6, 0, -2] }"), 0.5, 0.5).Red;

        Assert.IsGreaterThan(fromLeft * 1.3, fromRight, $"lit {fromRight:F3} from the right against {fromLeft:F3} from the left");

        string plain = "newmtl Shown\nKd 0.8 0.8 0.8\nKs 0 0 0\n";
        double plainRight = At(Rendered(face, plain, light: "point light { location [6, 0, -2] }"), 0.5, 0.5).Red;
        double plainLeft = At(Rendered(face, plain, light: "point light { location [-6, 0, -2] }"), 0.5, 0.5).Red;

        Assert.AreEqual(plainLeft, plainRight, 0.02, "the plain square should be lit alike from either side");
    }

    /// <summary>
    /// Faces wear the material their `usemtl` names; a face that names none, or one the library lacks, wears what the
    /// scene gives the model; and a scene's `replace` puts its own in place of the library's by name.
    /// </summary>
    [TestMethod]
    public void TestFacesWearTheMaterialsTheyAreGiven()
    {
        // Four squares in a row along X, each its own material.
        string model = """
            v -2 -0.5 0
            v -1 -0.5 0
            v 0 -0.5 0
            v 1 -0.5 0
            v 2 -0.5 0
            v -2 0.5 0
            v -1 0.5 0
            v 0 0.5 0
            v 1 0.5 0
            v 2 0.5 0
            f 1 2 7 6
            usemtl Red
            f 2 3 8 7
            usemtl Missing
            f 3 4 9 8
            usemtl Blue
            f 4 5 10 9
            """;
        string library = "newmtl Red\nillum 0\nKd 1 0 0\nnewmtl Blue\nillum 0\nKd 0 0 1\n";
        Canvas canvas = Rendered(model, library, "material { pigment [0, 1, 0]  ambient 1  diffuse 0  specular 0 }");
        double first = 0.5 - 1.5 / (2 * 4 * Math.Tan(25 * Math.PI / 180)), step = 1 / (2 * 4 * Math.Tan(25 * Math.PI / 180));

        Assert.IsGreaterThan(0.8, At(canvas, first, 0.5).Green, "a face naming no material wears the scene's");
        Assert.IsGreaterThan(0.8, At(canvas, first + step, 0.5).Red, "a face naming Red wears Red");
        Assert.IsGreaterThan(0.8, At(canvas, first + 2 * step, 0.5).Green, "a face naming a missing one wears the scene's");
        Assert.IsGreaterThan(0.8, At(canvas, first + 3 * step, 0.5).Blue, "a face naming Blue wears Blue");

        Canvas replaced = Rendered(model, library,
            "material { pigment [0, 1, 0]  ambient 1  diffuse 0  specular 0 }  replace 'Red' with material { pigment [1, 1, 0]  ambient 1  diffuse 0  specular 0 }");
        Color yellow = At(replaced, first + step, 0.5);

        Assert.IsTrue(yellow.Red > 0.8 && yellow.Green > 0.8 && yellow.Blue < 0.2, $"Red replaced should be yellow, not {yellow}");

        string refused = Refusal(model, library, "replace 'Gold' with material { pigment Yellow }");

        StringAssert.Contains(refused, "no material named 'Gold'");
        StringAssert.Contains(refused, "'Red', 'Blue'");
    }

    private string Refusal(string model, string library, string extra)
    {
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        Console.SetOut(captured);

        try
        {
            Rendered(model, library, extra);
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString();
    }

    /// <summary>
    /// A face's corners may be counted back from the end, may carry texture coordinates without normals or normals
    /// without texture coordinates, and an index naming nothing is taken as none; a line ending in a backslash runs
    /// on, and a remark is dropped.
    /// </summary>
    [TestMethod]
    public void TestFacesAreReadHoweverTheirCornersAreWritten()
    {
        ObjectFileParser parser = new (content: """
            v 0 0 0
            v 1 0 0
            v 0 1 0  # a remark
            vt 0 0
            vt 1 0
            vt 0 1
            vn 0 0 1
            f -3/-3 -2/-2 -1/-1
            f 1//1 2//1 \
              3//1
            f 1/9 2/9 3/9
            """);
        List<Triangle> triangles = parser.Parse().Surfaces.OfType<Triangle>().ToList();

        Assert.HasCount(3, triangles);
        Assert.IsNotNull(triangles[0].TextureCoordinates, "texture coordinates counted back from the end");
        Assert.IsNotInstanceOfType<SmoothTriangle>(triangles[0]);
        Assert.IsInstanceOfType<SmoothTriangle>(triangles[1], "normals with no texture coordinates, run on a line");
        Assert.IsNull(triangles[1].TextureCoordinates);
        Assert.IsNull(triangles[2].TextureCoordinates, "an index naming nothing is none");
        Assert.AreSame(parser.Vertices[0], triangles[0].Point1);
    }

    /// <summary>
    /// What a material library says is read into this engine's materials as nearly as it can be said, and what is not
    /// read is noted.
    /// </summary>
    [TestMethod]
    public void TestALibrarySaysWhatItCan()
    {
        string heights = Picture(8, 8, (x, _) => new Color(x / 8.0, x / 8.0, x / 8.0), "heights.png");
        string normals = Picture(8, 8, (_, _) => new Color(0.5, 0.5, 1), "normals.png");
        MaterialLibrary library = new ($$"""
            newmtl Plastic
            Kd 0.2 0.4 0.6
            Ks 0.5 0.3 0.1
            Ns 50
            map_Ks shine.png
            newmtl Flat
            illum 0
            Kd 1 1 0
            newmtl Matte
            illum 1
            Ks 1 1 1
            newmtl Mirror
            illum 3
            Ks 0.7 0.7 0.7
            newmtl Glass
            illum 7
            Ni 1.5
            Tf 0.9 0.9 0.8
            newmtl Smoke
            d 0.25
            Ni 1.2
            newmtl Haze
            Tr 0.4
            newmtl Lamp
            Ke 1 0.5 0
            newmtl Gold
            Pr 0.3
            Pm 0.8
            Pc 1
            Pcr 0.2
            newmtl Cloth
            Ps 0.5
            newmtl Painted
            map_Kd -s 2 3 -o 0.5 0.25 paint.png
            map_Pr -imfchan g rough.png
            map_Pm metal.png
            newmtl Bumpy
            bump -bm 0.3 {{heights}}
            newmtl Normal
            map_Bump {{normals}}
            """.Split('\n'), _directory);
        Dictionary<string, Material> m = library.Materials;

        Assert.IsTrue(m["Plastic"].Pigment.GetColorFor(new Point(0, 0, 0)).Matches(new Color(0.2, 0.4, 0.6)));
        Assert.AreEqual(0.5, m["Plastic"].Specular, 1e-9);
        Assert.AreEqual(50, m["Plastic"].Shininess, 1e-9);
        Assert.AreEqual((1.0, 0.0, 0.0), (m["Flat"].Ambient ?? 0, m["Flat"].Diffuse, m["Flat"].Specular));
        Assert.AreEqual(0, m["Matte"].Specular, 1e-9);
        Assert.AreEqual(0.7, m["Mirror"].Reflective, 1e-9);
        Assert.IsTrue(m["Glass"].Fresnel);
        Assert.AreEqual(1, m["Glass"].Transparency, 1e-9);
        Assert.AreEqual(1.5, m["Glass"].Interior.IndexOfRefraction, 1e-9);
        Assert.AreEqual(0.9, m["Glass"].Interior.Filter, 1e-9);
        Assert.AreEqual(0.75, m["Smoke"].Transparency, 1e-9);
        Assert.AreEqual(1.2, m["Smoke"].Interior.IndexOfRefraction, 1e-9);
        Assert.AreEqual(0.4, m["Haze"].Transparency, 1e-9);
        Assert.IsTrue(m["Lamp"].Emission.Matches(new Color(1, 0.5, 0)));
        Assert.AreEqual((0.3, 0.8), (m["Gold"].Roughness, m["Gold"].Metallic));
        Assert.AreEqual(0.2, m["Gold"].Coat.Roughness, 1e-9);
        Assert.IsTrue(m["Cloth"].Sheen.Color.Matches(new Color(0.5, 0.5, 0.5)));

        ImagePigment painted = (ImagePigment) m["Painted"].Pigment;

        Assert.AreEqual("paint.png", painted.ImageReference.ImageName);
        Assert.AreEqual(new TextureCoordinates(2, 3, 0.5, 0.25), painted.Coordinates);
        Assert.AreEqual(ImageChannel.Green, ((ImagePattern) m["Painted"].RoughnessBy.Pattern).Channel);
        Assert.AreEqual("metal.png", ((ImagePattern) m["Painted"].MetallicBy.Pattern).ImageReference.ImageName);

        Assert.IsNotNull(m["Bumpy"].SurfaceNormal.Pattern, "a picture of heights is a bump");
        Assert.AreEqual(0.3, m["Bumpy"].SurfaceNormal.Depth, 1e-9);
        Assert.IsNotNull(m["Normal"].SurfaceNormal.NormalMap, "a picture of normals named as a bump is a normal map");
        CollectionAssert.Contains(library.Unread, "map_Ks");
    }
}
