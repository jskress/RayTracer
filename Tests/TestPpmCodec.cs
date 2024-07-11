using RayTracer;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO;

namespace Tests;

[TestClass]
public class TestPpmCodec
{
    private readonly Ppm3Codec _codec = new();

    [TestMethod]
    public void TestHeader()
    {
        Canvas canvas = new (5, 3);
        string[] text = Encode(canvas).Split(Environment.NewLine);

        Assert.AreEqual("P3", text[0]);
        Assert.AreEqual("5 3", text[1]);
        Assert.AreEqual("255", text[2]);
    }

    [TestMethod]
    public void TestPixelData()
    {
        Canvas canvas = new (5, 3);
        Color c1 = new Color(1.5, 0, 0);
        Color c2 = new Color(0, 0.5, 0);
        Color c3 = new Color(-0.5, 0, 1);

        canvas.SetColor(c1, 0, 0);
        canvas.SetColor(c2, 2, 1);
        canvas.SetColor(c3, 4, 2);

        string[] text = Encode(canvas).Split(Environment.NewLine);

        // y = 0...
        Assert.AreEqual("255 0 0 0 0 0 0 0 0 0 0 0 0 0 0", text[3]);

        // y = 1... (Note the 0.5 becomes 186 instead of 128 due to gamma correction.
        Assert.AreEqual("0 0 0 0 0 0 0 186 0 0 0 0 0 0 0", text[4]);

        // y = 2...
        Assert.AreEqual("0 0 0 0 0 0 0 0 0 0 0 0 0 0 255", text[5]);
    }

    private string Encode(Canvas canvas)
    {
        using MemoryStream streamToWrite = new ();

        _ = new ProgramOptions();

        _codec.Encode(new RenderContext(), canvas, streamToWrite, null);

        using MemoryStream streamToRead = new (streamToWrite.GetBuffer());
        using StreamReader reader = new (streamToRead);

        return reader.ReadToEnd();
    }
}
