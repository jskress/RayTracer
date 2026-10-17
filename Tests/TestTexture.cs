using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestTexture
{
    private static Texture Make(
        int width, int height, Func<int, int, Color> colorAt, TransferFunction encoding = null)
    {
        Canvas canvas = new (width, height) { Encoding = encoding ?? TransferFunction.Linear };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                canvas.SetColor(colorAt(x, y), x, y);
        }

        return Texture.From(canvas);
    }

    private static Color Gray(double value) => new (value, value, value);

    /// <summary>
    /// This tests the sRGB curve against its published values: the straight run through the darks, the
    /// power above it, and the mid-gray of an eight-bit file, which is a fifth of white's light and not
    /// half of it.
    /// </summary>
    [TestMethod]
    public void TestAnSrgbValueIsTurnedBackIntoItsLight()
    {
        Assert.AreEqual(0, TransferFunction.Srgb.ToLinear(0), 1e-12);
        Assert.AreEqual(1, TransferFunction.Srgb.ToLinear(1), 1e-12);
        Assert.AreEqual(0.02 / 12.92, TransferFunction.Srgb.ToLinear(0.02), 1e-12);
        Assert.AreEqual(0.21586, TransferFunction.Srgb.ToLinear(128 / 255.0), 1e-5);
        Assert.AreEqual(0.5, TransferFunction.Linear.ToLinear(0.5), 1e-12);
        Assert.AreEqual(Math.Pow(0.5, 2.2), TransferFunction.Power(1 / 2.2).ToLinear(0.5), 1e-12);
        Assert.AreSame(TransferFunction.Linear, TransferFunction.Power(1));
    }

    /// <summary>
    /// This tests that a texture holds the light its file's values stand for.  Read as they stood, an
    /// eight-bit mid-gray of 128 came back as half of white's light, and rendered as 186.
    /// </summary>
    [TestMethod]
    public void TestATextureHoldsLightNotItsFilesValues()
    {
        Texture fromSrgb = Make(1, 1, (_, _) => Gray(128 / 255.0), TransferFunction.Srgb);
        Texture fromLight = Make(1, 1, (_, _) => Gray(128 / 255.0));

        Assert.AreEqual(0.21586, fromSrgb.Bilinear(0.5, 0.5, TextureWrap.Clamp, TextureWrap.Clamp).Red, 1e-5);
        Assert.AreEqual(128 / 255.0, fromLight.Bilinear(0.5, 0.5, TextureWrap.Clamp, TextureWrap.Clamp).Red, 1e-6);
    }

    /// <summary>
    /// This tests that a point between two texels is read between them, and that what is blended is
    /// their light: halfway from a black texel to a white one of an sRGB file is half of white's light.
    /// Blending the file's values instead gives a fifth of it, which is what darkens every edge.
    /// </summary>
    [TestMethod]
    public void TestHalfwayBetweenTwoTexelsIsHalfTheirLight()
    {
        Texture texture = Make(2, 1, (x, _) => Gray(x), TransferFunction.Srgb);

        Assert.AreEqual(0.5, texture.Bilinear(1, 0.5, TextureWrap.Clamp, TextureWrap.Clamp).Red, 1e-9);
        Assert.AreEqual(0.25, texture.Bilinear(0.75, 0.5, TextureWrap.Clamp, TextureWrap.Clamp).Red, 1e-9);
        Assert.AreEqual(0, texture.Bilinear(0.5, 0.5, TextureWrap.Clamp, TextureWrap.Clamp).Red, 1e-9);
    }

    /// <summary>
    /// This tests that a clear texel lends its neighbor none of its color.  A clear texel's color is
    /// nothing anyone sees -- a clear red, here -- and averaging it in as though it were would tint or dim
    /// the edge of every decal.
    /// </summary>
    [TestMethod]
    public void TestAClearTexelLendsNoColorToItsNeighbor()
    {
        Texture texture = Make(2, 1, (x, _) => x == 0 ? Colors.White : new Color(1, 0, 0, 0));
        Color edge = texture.Bilinear(1, 0.5, TextureWrap.Clamp, TextureWrap.Clamp);

        Assert.AreEqual(0.5, edge.Alpha, 1e-9);
        Assert.AreEqual(1, edge.Red, 1e-6);
        Assert.AreEqual(1, edge.Green, 1e-6);
        Assert.IsFalse(texture.IsOpaque);
        Assert.IsTrue(Make(1, 1, (_, _) => Colors.White).IsOpaque);
    }

    /// <summary>
    /// This tests the three things a texture may hold past its edges: itself again, its edge forever,
    /// or nothing.
    /// </summary>
    [TestMethod]
    public void TestWhatLiesPastAnImagesEdge()
    {
        Texture texture = Make(4, 1, (x, _) => Gray(x / 3.0));

        Color inside = texture.Bilinear(1.5, 0.5, TextureWrap.Repeat, TextureWrap.Repeat);
        Color tiled = texture.Bilinear(1.5 + 4 * 1000, 0.5 - 7, TextureWrap.Repeat, TextureWrap.Repeat);
        Color held = texture.Bilinear(-50, 0.5, TextureWrap.Clamp, TextureWrap.Clamp);
        Color past = texture.Bilinear(-50, 0.5, TextureWrap.Border, TextureWrap.Border);

        Assert.AreEqual(inside.Red, tiled.Red, 1e-9);
        Assert.AreEqual(0, held.Red, 1e-9);
        Assert.AreEqual(1, held.Alpha, 1e-9);
        Assert.AreEqual(0, past.Alpha, 1e-9);

        // An image drawn once fades out over its last half texel rather than stopping on it.
        Color atTheEdge = Make(1, 1, (_, _) => Colors.White).Bilinear(1, 0.5, TextureWrap.Border, TextureWrap.Border);

        Assert.AreEqual(0.5, atTheEdge.Alpha, 1e-9);
        Assert.AreEqual(1, atTheEdge.Red, 1e-9);
    }

    /// <summary>
    /// This tests that each smaller copy of an image is the exact average of the part of the one above
    /// it that it covers, odd sizes included, so that the smallest is the whole image's average.
    /// </summary>
    [TestMethod]
    public void TestTheSmallestCopyIsTheWholeImagesAverage()
    {
        double[] values = [0.1, 0.9, 0.2, 0.6, 0.35];
        Texture texture = Make(5, 3, (x, y) => Gray(values[x] * (y + 1) / 3));
        double expected = values.Average() * (1 + 2 + 3) / 9.0;

        // Five by three halves to three by two, then two by one, then one.
        Assert.AreEqual(4, texture.LevelCount);

        Color whole = texture.Filtered(2.5, 1.5, 1e6, 0, 0, 1e6, TextureWrap.Clamp, TextureWrap.Clamp);

        Assert.AreEqual(expected, whole.Red, 1e-6);
    }

    /// <summary>
    /// This tests that a patch covering many texels is their average.  Point-sampled, a checker of
    /// single texels comes back black or white at random, which across a floor is moiré; filtered, it
    /// is gray, square on or at a glance and turned any way.
    /// </summary>
    [TestMethod]
    public void TestAPatchOfManyTexelsIsTheirAverage()
    {
        Texture texture = Make(64, 64, (x, y) => Gray((x + y) % 2));
        (double, double, double, double)[] patches =
        [
            (8, 0, 0, 8),
            (20 * Math.Cos(0.5), 20 * Math.Sin(0.5), -2 * Math.Sin(0.5), 2 * Math.Cos(0.5)),
            (3, 4, -40, 30),
            (40, 0, 0, 0)
        ];

        foreach ((double du0, double dv0, double du1, double dv1) in patches)
        {
            for (int place = 0; place < 5; place++)
            {
                double u = 7.3 + place * 9.1, v = 31.7 - place * 5.3;
                Color color = texture.Filtered(u, v, du0, dv0, du1, dv1, TextureWrap.Repeat, TextureWrap.Repeat);

                Assert.AreEqual(0.5, color.Red, 0.03, $"patch ({du0}, {dv0}), ({du1}, {dv1}) at ({u}, {v})");
            }
        }
    }

    /// <summary>
    /// This tests that the patch is averaged along its own axes and not some others.  In an image of
    /// stripes eight texels wide running down it, a patch long down the image and narrow across it stays
    /// on its black stripe; turned the other way it reaches across into the white ones either side, whose
    /// even average over it is half.
    /// </summary>
    [TestMethod]
    public void TestThePatchIsAveragedAlongItsOwnAxes()
    {
        Texture texture = Make(64, 64, (x, _) => Gray(x / 8 % 2));

        Color along = texture.Filtered(4, 30, 0.5, 0, 0, 16, TextureWrap.Repeat, TextureWrap.Repeat);
        Color across = texture.Filtered(4, 30, 16, 0, 0, 0.5, TextureWrap.Repeat, TextureWrap.Repeat);

        Assert.IsLessThan(0.02, along.Red, $"along the stripe: {along}");
        Assert.IsGreaterThan(0.3, across.Red, $"across the stripes: {across}");
    }

    /// <summary>
    /// This tests the size of the average against the box a pixel really covers.  A patch half a stripe
    /// wide centered on a stripe's edge must come back half and half; one a quarter wide on the white
    /// side of the edge mostly white -- by as much as the even average over it, give or take the blur of
    /// reading between texels.  An average a third too wide, as PBRT's is, misses that by a tenth.
    /// </summary>
    [TestMethod]
    public void TestThePatchIsAsWideAsThePixel()
    {
        Texture texture = Make(256, 4, (x, _) => Gray(x / 64 % 2));

        // The edge between the first black stripe and the first white one lies at 64.  Over a patch
        // forty texels across that starts there and reaches back, the even average is a quarter white.
        Color quarter = texture.Filtered(54, 2, 40, 0, 0, 40, TextureWrap.Repeat, TextureWrap.Repeat);
        Color half = texture.Filtered(64, 2, 40, 0, 0, 40, TextureWrap.Repeat, TextureWrap.Repeat);

        Assert.AreEqual(0.5, half.Red, 0.02);
        Assert.AreEqual(0.25, quarter.Red, 0.04);
    }
}
