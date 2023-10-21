using RayTracer.Graphics;
using RayTracer.ImageIO;

namespace Tests;

[TestClass]
public class TestPpmCodec
{
    private readonly PpmCodec _codec = new();

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
        Assert.AreEqual("255 0 0", text[3]);
        Assert.AreEqual("0 0 0", text[4]);
        Assert.AreEqual("0 0 0", text[5]);
        Assert.AreEqual("0 0 0", text[6]);
        Assert.AreEqual("0 0 0", text[7]);

        // y = 1...
        Assert.AreEqual("0 0 0", text[8]);
        Assert.AreEqual("0 0 0", text[9]);
        Assert.AreEqual("0 128 0", text[10]);
        Assert.AreEqual("0 0 0", text[11]);
        Assert.AreEqual("0 0 0", text[12]);

        // y = 2...
        Assert.AreEqual("0 0 0", text[13]);
        Assert.AreEqual("0 0 0", text[14]);
        Assert.AreEqual("0 0 0", text[15]);
        Assert.AreEqual("0 0 0", text[16]);
        Assert.AreEqual("0 0 255", text[17]);
    }

    private string Encode(Canvas canvas)
    {
        using MemoryStream streamToWrite = new ();

        _codec.Encode(canvas, streamToWrite);

        using MemoryStream streamToRead = new (streamToWrite.GetBuffer());
        using StreamReader reader = new (streamToRead);

        return reader.ReadToEnd();
    }
}
