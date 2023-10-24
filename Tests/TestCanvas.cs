using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestCanvas
{
    [TestMethod]
    public void TestConstruction()
    {
        Canvas canvas = new (10, 20);

        Assert.AreEqual(10, canvas.Width);
        Assert.AreEqual(20, canvas.Height);

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
                Assert.AreSame(Colors.Transparent, canvas.GetPixel(x, y));
        }
    }

    [TestMethod]
    public void TestSetPixel()
    {
        Canvas canvas = new (10, 20);
        Color red = new (1, 0, 0);

        canvas.SetColor(red, 2, 3);

        Assert.AreSame(red, canvas.GetPixel(2, 3));
    }
}
