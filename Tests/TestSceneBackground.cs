using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
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
/// These tests cover what a ray that hits nothing at all comes back with.
/// <para>
/// The background is a pigment, so it may be patterned, but a pigment must be asked about a point and
/// there is no point of intersection to ask about.  It used to be asked about where the ray started,
/// which made a patterned background nonsense: every ray a camera casts starts at the same place, so
/// the whole sky came back one flat color, while reflected rays start wherever they bounced and so
/// showed a pattern the sky itself did not have.  It is asked about where the ray is heading instead,
/// which is to say the pigment is painted on a sphere infinitely far off.
/// </para>
/// </summary>
[TestClass]
public class TestSceneBackground
{
    /// <summary>
    /// A sky of checks, so that where it is asked about plainly matters.  A check is a unit across and
    /// the sphere the sky is painted on has a radius of one, so the eight octants of the sky alternate
    /// between the two colors with no scaling needed.
    /// </summary>
    private static Scene SkyOfChecks()
    {
        PatternPigment checks = new ()
        {
            Pattern = new CheckerPattern(),
            PigmentSet = new PigmentSet()
        };

        checks.PigmentSet.AddEntry(new SolidPigment(Colors.White));
        checks.PigmentSet.AddEntry(new SolidPigment(Colors.Red), 1);

        return new Scene { Background = checks };
    }

    /// <summary>
    /// This tests the heart of it: the sky depends on which way a ray is pointed and not at all on
    /// where it set out from.
    /// </summary>
    [TestMethod]
    public void TestTheSkyIsAskedByHeadingAndNotByPosition()
    {
        Scene scene = SkyOfChecks();
        Vector heading = new (0.3, 0.8, 0.5);
        Color fromTheOrigin = scene.GetColorFor(new Ray(new Point(0, 0, 0), heading), 4);
        Color fromMilesOff = scene.GetColorFor(new Ray(new Point(37, -12, 8), heading), 4);

        Assert.IsTrue(fromTheOrigin.Matches(fromMilesOff),
            $"the same heading from two places should see the same sky: {fromTheOrigin} vs " +
            $"{fromMilesOff}");

        // And the other half of the same claim: two headings should not see the same sky, or the
        // pattern would not be showing at all.
        Color theOtherWay = scene.GetColorFor(new Ray(new Point(0, 0, 0), new Vector(0.3, -0.8, 0.5)), 4);

        Assert.IsFalse(fromTheOrigin.Matches(theOtherWay),
            $"two headings should see different parts of the sky: {fromTheOrigin} vs {theOtherWay}");
    }

    /// <summary>
    /// This tests that only the direction a ray points matters and not how long its direction vector
    /// happens to be, since nothing promises rays carry a unit direction.
    /// </summary>
    [TestMethod]
    public void TestTheSkyDoesNotCareHowLongTheDirectionIs()
    {
        Scene scene = SkyOfChecks();
        Color unit = scene.GetColorFor(new Ray(Point.Zero, new Vector(0.3, 0.8, 0.5)), 4);
        Color stretched = scene.GetColorFor(new Ray(Point.Zero, new Vector(2.4, 6.4, 4)), 4);

        Assert.IsTrue(unit.Matches(stretched),
            $"the same direction, eight times as long, should see the same sky: {unit} vs {stretched}");
    }

    /// <summary>
    /// This tests that a mirror shows the sky that is really over it.  This is the pay-off, and the
    /// thing that could not be true before: what a reflection shows must be the sky in the direction
    /// the reflected ray went, not the sky in the direction the eye was looking.
    /// </summary>
    [TestMethod]
    public void TestAMirrorShowsTheSkyItIsPointedAt()
    {
        Scene scene = SkyOfChecks();
        Plane mirror = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.Black),
                Ambient = 0,
                Diffuse = 0,
                Specular = 0,
                Reflective = 1
            }
        };

        mirror.PrepareForRendering();
        scene.Surfaces.Add(mirror);

        // Down onto the mirror at the origin, and so back up again with the Y of the heading flipped.
        // The sky itself is asked of a scene with no mirror in it, since a ray leaving the origin in
        // this one would only bounce off the mirror it started on.
        Vector down = new (0.3, -0.8, 0.5);
        Vector up = new (0.3, 0.8, 0.5);
        Scene openSky = SkyOfChecks();
        Color inTheMirror = scene.GetColorFor(new Ray(new Point(-0.3, 0.8, -0.5), down), 4);
        Color skyAbove = openSky.GetColorFor(new Ray(Point.Zero, up), 4);
        Color skyBelow = openSky.GetColorFor(new Ray(Point.Zero, down), 4);

        Assert.IsTrue(inTheMirror.Matches(skyAbove),
            $"the mirror should show the sky it reflects: {inTheMirror} vs {skyAbove}");
        Assert.IsFalse(inTheMirror.Matches(skyBelow),
            "the mirror should not show the sky the eye was looking toward");
    }

    /// <summary>
    /// This tests that a sky may be read from an image, which is what being asked by direction makes
    /// worth doing: an image wrapped around that sphere is an environment map.  It also covers the
    /// preparation a background pigment needs, since nothing else in a scene owns it -- without that,
    /// the image is never loaded and the render dies on the first ray that misses.
    /// <para>
    /// The scene names the image by a plain file name and ends on the line that does so, both
    /// deliberately.  A relative name is resolved against the directory the scene file sits in, and the
    /// parser lets go of the file it is reading the moment that file runs out of tokens -- which is
    /// while the last clause in it is still being handled.  So an image named in a file's last clause
    /// used to be handed no directory at all and stopped the render with <i>"Value cannot be null.
    /// (Parameter 'path1')"</i>.
    /// </para>
    /// </summary>
    [TestMethod]
    public void TestASkyMayBeReadFromAnImage()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"sky-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            // A picture in four quarters, so which part of it the sky shows can be told apart.
            string image = Path.Combine(directory, "sky.png");
            Canvas picture = new (2, 2);

            picture.SetColor(new Color(1, 0, 0), 0, 0);
            picture.SetColor(new Color(0, 1, 0), 1, 0);
            picture.SetColor(new Color(0, 0, 1), 0, 1);
            picture.SetColor(new Color(1, 1, 0), 1, 1);

            new ImageFile(image).Save(picture, new RenderContext { ApplyGamma = false });

            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // No mapping is named, since a sky is a sphere and should not have to be told so.
            File.WriteAllText(path,
                "context { angles are degrees  no gamma  width 40  height 40 }\n" +
                "camera { location [0, 0, 0]  look at [0, 0, 1]  field of view 90 }\n" +
                $"background image '{Path.GetFileName(image)}'\n");

            StringWriter captured = new ();
            TextWriter was = Console.Out;

            Console.SetOut(captured);

            try
            {
                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene did not parse: {captured}");

                renderer.Render(new RenderOptions { OutputFileName = output });

                Assert.DoesNotContain("Error", captured.ToString());
            }
            finally
            {
                Console.SetOut(was);
            }

            // The camera looks along Z with a wide view, so the corners of the frame reach into
            // different quarters of the picture and the sky cannot come back all one color.
            Canvas sky = new ImageFile(output).Load()[0];
            HashSet<string> colors =
            [
                sky.GetPixel(4, 4).ToString(), sky.GetPixel(35, 4).ToString(),
                sky.GetPixel(4, 35).ToString(), sky.GetPixel(35, 35).ToString()
            ];

            Assert.IsTrue(colors.Count > 1, $"the whole sky came back {colors.First()}");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// This tests that a solid background is still the same in every direction, which is what keeps
    /// every scene that ever named a plain color for its background rendering exactly as it did.
    /// </summary>
    [TestMethod]
    public void TestASolidSkyIsTheSameEverywhere()
    {
        Scene scene = new () { Background = new SolidPigment(Colors.Red) };
        Vector[] headings =
        [
            Directions.In, Directions.Out, Directions.Up, Directions.Down,
            Directions.Left, Directions.Right, new Vector(0.3, -0.8, 0.5)
        ];

        foreach (Vector heading in headings)
        {
            Color seen = scene.GetColorFor(new Ray(new Point(4, -9, 2), heading), 4);

            Assert.IsTrue(Colors.Red.Matches(seen), $"looking {heading} saw {seen}");
        }
    }
}
