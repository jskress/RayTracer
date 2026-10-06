using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// The ways of saying that something casts no shadow, tried by rendering.  A white floor is seen
/// from straight above and lit from off to one side, with the subject floating over the middle, so
/// that its shadow falls well clear of it, toward -X; only the floor away from the middle is
/// counted, so the subject's own unlit side is never mistaken for a shadow.
/// </summary>
[TestClass]
public class TestShadowSuppression
{
    private const int Size = 100;
    private const string Ball = "sphere { scale 0.5  translate [0, 2, 0] }";

    private string _directory;

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"shadows-{Guid.NewGuid():N}");

        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void RemoveDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    /// <summary>
    /// The command line's `--no-shadows` reaches the render's context only just before it renders,
    /// after every surface has been made.  It was read as each surface was made, so it came too late
    /// for all of them and changed nothing at all.
    /// </summary>
    [TestMethod]
    public void TestTheNoShadowsOptionTakesEffect()
    {
        Assert.IsTrue(Shadowed(Ball) > 20, "the ball should cast a shadow to begin with");
        Assert.AreEqual(0, Shadowed(Ball, noShadows: true));
    }

    /// <summary>
    /// A tube's crossings are on segments it builds as it is readied, which no `no shadows` read
    /// while the scene was being made could reach.
    /// </summary>
    [TestMethod]
    public void TestNoShadowsReachesATube()
    {
        const string tube = "tube { radius 0.4 at [0, 2, -0.5]  radius 0.4 at [0, 2, 0.5] }";

        Assert.IsTrue(Shadowed(tube) > 20, "the tube should cast a shadow to begin with");
        Assert.AreEqual(0, Shadowed("context { no shadows }\n" + tube));
        Assert.AreEqual(0, Shadowed(tube, noShadows: true));
    }

    /// <summary>
    /// A scene's `no shadows` says the whole scene casts none, so where in the file it is written
    /// cannot matter; it used to miss everything made ahead of it.
    /// </summary>
    [TestMethod]
    public void TestNoShadowsCoversWhatComesBeforeIt()
    {
        Assert.AreEqual(0, Shadowed(Ball + "\ncontext { no shadows }"));
    }

    /// <summary>
    /// A shadow ray asks the surface it crossed, which is always a leaf -- a sphere inside a union,
    /// a segment inside a tube -- so `no shadow` written on anything made of other surfaces was
    /// never seen.  It covers everything they hold.
    /// </summary>
    [TestMethod]
    public void TestNoShadowCoversWhatACompositeHolds()
    {
        const string pair = "sphere { scale 0.4  translate [0, 2, -0.3] }  sphere { scale 0.4  translate [0, 2, 0.3] }";

        Assert.IsTrue(Shadowed($"union {{ {pair} }}") > 20, "the pair should cast a shadow to begin with");
        Assert.AreEqual(0, Shadowed($"union {{ {pair}  no shadow }}"));
        Assert.AreEqual(0, Shadowed($"group {{ {pair}  no shadow }}"));
        Assert.AreEqual(0, Shadowed("tube { radius 0.4 at [0, 2, -0.5]  radius 0.4 at [0, 2, 0.5]  no shadow }"));
    }

    /// <summary>
    /// Two calls of a primitive that makes a group share the one shape behind two instances of it,
    /// so `no shadow` said of one call must not reach the shape both of them show.  The copy at
    /// +Z casts no shadow; the one at -Z still does.
    /// </summary>
    [TestMethod]
    public void TestNoShadowOnOneSharedCopyLeavesTheOther()
    {
        const string body =
            "primitive Ball(size = 1) -> group {\n" +
            "    return group { sphere { scale (0.5 * size)  translate [0, 2, 0] } }\n" +
            "}\n" +
            "object Ball(1) { translate [0, 0, 2]  no shadow }\n" +
            "object Ball(1) { translate [0, 0, -2] }\n";
        (int ahead, int behind) = ShadowedByHalf(body);

        Assert.AreEqual(0, ahead, "the copy marked no shadow cast one");
        Assert.IsTrue(behind > 20, "the other copy should still cast its shadow");
    }

    /// <summary>
    /// Renders the floor with the given scene body over it and counts the shadowed floor pixels.
    /// </summary>
    private int Shadowed(string body, bool noShadows = false)
    {
        (int ahead, int behind) = ShadowedByHalf(body, noShadows);

        return ahead + behind;
    }

    /// <summary>
    /// Renders the floor with the given scene body over it and counts the shadowed floor pixels in
    /// the top half of the picture (+Z) and the bottom half (-Z), leaving out a band down the
    /// middle, where the subject itself is seen.
    /// </summary>
    private (int Ahead, int Behind) ShadowedByHalf(string body, bool noShadows = false)
    {
        string path = Path.Combine(_directory, "scene.igl");
        string output = Path.Combine(_directory, "out.png");

        File.WriteAllText(path,
            "context { no gamma }\n" +
            "camera { location [0, 10, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 60 }\n" +
            "point light { location [10, 10, 0] }\n" +
            "plane { material { pigment White } }\n" + body);

        ImageRenderer renderer = new LanguageParser(path).Parse();

        renderer.Render(new RenderOptions
        {
            OutputFileName = output, Width = Size, Height = Size, NoShadows = noShadows
        });

        Canvas canvas = new ImageFile(output).Load()[0];
        int band = (int) Math.Ceiling(1.2 * Size / (2 * 10 * Math.Tan(Math.PI / 6)));
        int ahead = 0;
        int behind = 0;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                if (Math.Abs(x - Size / 2) <= band || canvas.GetPixel(x, y).Red >= 0.2)
                    continue;

                if (y < Size / 2)
                    ahead++;
                else
                    behind++;
            }
        }

        return (ahead, behind);
    }
}
