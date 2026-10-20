using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover how a camera's opening spreads light: the glare round a bright point, and the rays its
/// blades throw out.
/// </summary>
[TestClass]
public class TestDiffraction
{
    private const int Size = 256;
    private const int Middle = Size / 2;

    /// <summary>
    /// This method spreads a single bright point in the middle of a dark picture and returns the picture.
    /// </summary>
    private static Canvas Spread(double share, int blades = 0, double turn = 0, int width = Size, int height = Size)
    {
        Canvas canvas = new (width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                canvas.SetColor(new Color(0, 0, 0), x, y);
        }

        canvas.SetColor(new Color(100, 100, 100), width / 2, height / 2);
        new Diffraction(share, blades, turn).Spread(canvas);

        return canvas;
    }

    private static double Brightness(Canvas canvas, double x, double y)
    {
        Color color = canvas.GetPixel((int) Math.Round(x), (int) Math.Round(y));

        return color.Red + color.Green + color.Blue;
    }

    /// <summary>
    /// This tests that spreading neither adds light nor loses it, and that the share not spread stays where it
    /// fell.
    /// </summary>
    [TestMethod]
    public void TestSpreadingKeepsTheLight()
    {
        Canvas canvas = Spread(0.3, 6);
        double red = 0, green = 0, blue = 0;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Color color = canvas.GetPixel(x, y);

                red += color.Red;
                green += color.Green;
                blue += color.Blue;
            }
        }

        Assert.AreEqual(100, red, 1e-6);
        Assert.AreEqual(100, green, 1e-6);
        Assert.AreEqual(100, blue, 1e-6);
        Assert.IsGreaterThan(70, canvas.GetPixel(Middle, Middle).Green, "what is not spread stays");
    }

    /// <summary>
    /// This tests that the share says how much of the light is spread: spread twice the share, twice as much
    /// lands off the point, and the point keeps what is not spread.
    /// </summary>
    [TestMethod]
    public void TestTheShareSaysHowMuchIsSpread()
    {
        Canvas some = Spread(0.3, 6), more = Spread(0.6, 6);
        double near = some.GetPixel(Middle + 3, Middle).Green, nearer = more.GetPixel(Middle + 3, Middle).Green;

        Assert.AreEqual(2 * near, nearer, 1e-9 * nearer, "twice the share, twice the spread");
        Assert.AreEqual(
            100 - 2 * (100 - some.GetPixel(Middle, Middle).Green), more.GetPixel(Middle, Middle).Green, 1e-9,
            "the point keeps what is not spread");
    }

    /// <summary>
    /// This tests that an opening of blades throws a ray out square to each of its edges: six blades, six
    /// rays, each much brighter than the light between them at the same distance.
    /// </summary>
    [TestMethod]
    public void TestBladesThrowRays()
    {
        const int blades = 6;
        Canvas canvas = Spread(0.5, blades);

        for (int ray = 0; ray < blades; ray++)
        {
            double along = (2 * ray + 1) * Math.PI / blades;
            double between = along + Math.PI / blades;
            double onTheRay = Brightness(canvas, Middle + 24 * Math.Cos(along), Middle + 24 * Math.Sin(along));
            double offIt = Brightness(canvas, Middle + 24 * Math.Cos(between), Middle + 24 * Math.Sin(between));

            Assert.IsGreaterThan(5 * offIt, onTheRay, $"ray {ray}: {onTheRay} on it, {offIt} beside it");
        }
    }

    /// <summary>
    /// This tests that a round opening spreads light alike every way round.
    /// </summary>
    [TestMethod]
    public void TestARoundOpeningThrowsNoRays()
    {
        Canvas canvas = Spread(0.5);
        double level = Brightness(canvas, Middle + 12, Middle);

        foreach (double angle in new[] { 0.4, 0.8, 1.2, 2.0, 3.0 })
        {
            double there = Brightness(canvas, Middle + 12 * Math.Cos(angle), Middle + 12 * Math.Sin(angle));

            Assert.AreEqual(level, there, 0.35 * level, $"at {angle} radians");
        }
    }

    /// <summary>
    /// This tests that the pattern grows with the wavelength: more of the red is spread past the bright
    /// middle than of the blue, so what is left round a white point is faintly colored.
    /// </summary>
    [TestMethod]
    public void TestRedSpreadsFurtherThanBlue()
    {
        Canvas canvas = Spread(1);
        double red = 0, blue = 0;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (Math.Abs(x - Middle) + Math.Abs(y - Middle) <= 1)
                    continue;

                red += canvas.GetPixel(x, y).Red;
                blue += canvas.GetPixel(x, y).Blue;
            }
        }

        Assert.IsGreaterThan(blue * 1.1, red, $"{red} of the red and {blue} of the blue spread past the middle");
    }

    /// <summary>
    /// This tests that the light is spread about the point it came from, not beside it: what was spread is
    /// centered on the point to a fiftieth of a pixel, whichever way the blades are turned.  The picture is
    /// wide, since the pattern goes with the picture's width: in a narrow one it is smaller than a pixel, and a
    /// pattern set off center by a fraction of that would still leave its light in the point's own pixel.
    /// </summary>
    [TestMethod]
    public void TestThePatternIsCenteredOnThePoint()
    {
        const int width = 2048, height = 64;

        foreach ((int blades, double turn) in new[] { (0, 0.0), (5, 0.4), (6, 0.0) })
        {
            Canvas canvas = Spread(1, blades, turn, width, height);
            double total = 0, x = 0, y = 0;

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    double light = canvas.GetPixel(column, row).Green;

                    total += light;
                    x += light * column;
                    y += light * row;
                }
            }

            Assert.AreEqual(width / 2, x / total, 0.02, $"across, {blades} blades");
            Assert.AreEqual(height / 2, y / total, 0.02, $"down, {blades} blades");
        }
    }

    /// <summary>
    /// This tests that a camera with no glare leaves its picture alone.
    /// </summary>
    [TestMethod]
    public void TestNoGlareSpreadsNothing()
    {
        Canvas canvas = Spread(0);

        Assert.AreEqual(100, canvas.GetPixel(Middle, Middle).Red);
        Assert.AreEqual(0, canvas.GetPixel(Middle + 1, Middle).Red);
    }
}
