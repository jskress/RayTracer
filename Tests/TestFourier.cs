using System.Numerics;
using RayTracer.Basics;

namespace Tests;

/// <summary>
/// These tests cover the fast Fourier transform.
/// </summary>
[TestClass]
public class TestFourier
{
    /// <summary>
    /// This tests that a grid taken forward and back is the grid it was.
    /// </summary>
    [TestMethod]
    public void TestAGridComesBackAsItWas()
    {
        Random random = new (3);
        Complex[] grid = Enumerable.Range(0, 32 * 8)
            .Select(_ => new Complex(random.NextDouble(), random.NextDouble())).ToArray();
        Complex[] was = (Complex[]) grid.Clone();

        Fourier.Transform(grid, 32, 8);
        Fourier.Transform(grid, 32, 8, back: true);

        for (int index = 0; index < grid.Length; index++)
            Assert.AreEqual(0, (grid[index] - was[index]).Magnitude, 1e-12);
    }

    /// <summary>
    /// This tests the transform against what it is: a single value at the corner transforms to that value
    /// everywhere, and a wave of one cycle across the grid to a single value at the first place along.
    /// </summary>
    [TestMethod]
    public void TestTheTransformIsTheFourierTransform()
    {
        Complex[] corner = new Complex[16 * 16];

        corner[0] = 2;
        Fourier.Transform(corner, 16, 16);

        foreach (Complex value in corner)
            Assert.AreEqual(0, (value - 2).Magnitude, 1e-12);

        Complex[] wave = new Complex[16 * 4];

        for (int row = 0; row < 4; row++)
        {
            for (int x = 0; x < 16; x++)
                wave[row * 16 + x] = Complex.FromPolarCoordinates(1, 2 * Math.PI * x / 16);
        }

        Fourier.Transform(wave, 16, 4);

        for (int index = 0; index < wave.Length; index++)
            Assert.AreEqual(index == 1 ? 64 : 0, wave[index].Magnitude, 1e-9, $"at {index}");
    }

    /// <summary>
    /// This tests that a grid whose sides are not powers of two is refused.
    /// </summary>
    [TestMethod]
    public void TestOnlyPowersOfTwoAreTransformed()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Fourier.Transform(new Complex[12 * 8], 12, 8));
    }
}
