using RayTracer;
using RayTracer.Graphics;
using RayTracer.ImageIO;

namespace Tests;

[TestClass]
public class TestPngCodec
{
    private static readonly Color[] ColorSet =
    [
        Colors.Aqua, Colors.Black, Colors.Gold, Colors.Green, Colors.Indigo
    ];

    [TestMethod]
    public void TestRoundTrip()
    {
        // For testing, it's important to make sure we don't do gamma correction since
        // that will throw off roundtrip color matching.
        _ = new ProgramOptions { Gamma = 1 };

        PngCodec codec = new PngCodec();
        Canvas source = CreateCanvas();

        using MemoryStream stream = new MemoryStream();

        codec.Encode(source, stream, null);

        stream.Seek(0, SeekOrigin.Begin);

        Canvas[] targets = codec.Decode(stream);
        
        Assert.AreEqual(1, targets.Length);
        Assert.AreEqual(6, targets[0].Width);
        Assert.AreEqual(5, targets[0].Height);

        for (int y = 0; y < targets[0].Height; y++)
            AssertLineIsColor(targets[0], y);
    }

    private static Canvas CreateCanvas()
    {
        Canvas canvas = new Canvas(6, 5);

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                canvas.SetColor(ColorSet[y], x, y);
            }
        }

        return canvas;
    }

    private static void AssertLineIsColor(Canvas canvas, int y)
    {
        Color expected = ColorSet[y];

        for (int x = 0; x < canvas.Width; x++)
        {
            Color color = canvas.GetPixel(x, y);
            
            Assert.IsTrue(expected.Matches(color), $"Expected {expected} != actual {color}.");
        }
    }
}
