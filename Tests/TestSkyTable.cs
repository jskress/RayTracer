using System.Diagnostics;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover the sky that has already been worked out and kept.
/// <para>
/// A table is only ever as good as its agreement with the thing it stands in for, so that is what is
/// checked here rather than anything about the sky itself: read back, it must give what marching
/// through the air would have given.
/// </para>
/// </summary>
[TestClass]
public class TestSkyTable
{
    private static double Brightness(Color color) =>
        0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue;

    [TestMethod]
    public void TestTheTableAgreesWithMarchingThroughTheAir()
    {
        // The whole justification for keeping one.  Directions are chosen off the rows and columns on
        // purpose, so that what is measured is the mixing between entries rather than the entries.
        Atmosphere air = new ();
        Vector sun = new Vector(0.5, 0.6, -0.3).Unit;
        SkyTable table = new (air, sun, 0);
        double worst = 0;
        double worstAt = 0;

        for (int up = -8; up <= 88; up += 7)
        {
            for (int around = 3; around < 360; around += 37)
            {
                double climb = up * Math.PI / 180;
                double turn = around * Math.PI / 180;
                Vector view = new Vector(
                    Math.Cos(climb) * Math.Sin(turn),
                    Math.Sin(climb),
                    -Math.Cos(climb) * Math.Cos(turn)).Unit;
                Color marched = SpectralColor.ToColor(air.RadianceToward(view, sun, 0));
                Color read = table.Toward(view);
                double apart = Math.Abs(Brightness(marched) - Brightness(read));

                if (apart > worst)
                {
                    worst = apart;
                    worstAt = up;
                }
            }
        }

        Assert.IsTrue(worst < 0.004,
            $"the table was off by {worst} at {worstAt} degrees up, which is too much to read back");
    }

    [TestMethod]
    public void TestTheSkyIsTheSameOnEitherSideOfTheSun()
    {
        // A real saving rather than an assumption: the table keeps only half a turn because nothing in
        // the air can tell one side of the sun from the other.  If this ever failed, the table would be
        // silently mirroring something that is not actually symmetric.
        Atmosphere air = new ();
        Vector sun = new Vector(0, 0.5, -1).Unit;
        SkyTable table = new (air, sun, 0);

        for (int around = 10; around < 180; around += 30)
        {
            double turn = around * Math.PI / 180;
            Vector oneSide = new Vector(Math.Sin(turn) * 0.7, 0.5, -Math.Cos(turn) * 0.7).Unit;
            Vector other = new Vector(-oneSide.X, oneSide.Y, oneSide.Z).Unit;

            Assert.AreEqual(Brightness(table.Toward(oneSide)), Brightness(table.Toward(other)), 1e-9,
                $"the two sides differed {around} degrees round");
        }
    }

    [TestMethod]
    public void TestTheSunGoesWhereItIsPutt()
    {
        // Straight up is straight up, and the way round is measured so that nothing puts the sun in
        // front of a camera looking the way cameras here look by default.
        Vector overhead = new PhysicalSkyPigment { SunElevation = 90 }.TowardSun;

        Assert.AreEqual(1, overhead.Y, 1e-9);

        Vector ahead = new PhysicalSkyPigment { SunElevation = 0, SunAzimuth = 0 }.TowardSun;

        Assert.AreEqual(-1, ahead.Z, 1e-9, $"a bearing of nothing gave {ahead}");

        Vector toTheRight = new PhysicalSkyPigment { SunElevation = 0, SunAzimuth = 90 }.TowardSun;

        Assert.AreEqual(1, toTheRight.X, 1e-9, $"a quarter turn gave {toTheRight}");
    }

    [TestMethod]
    public void TestAnEvenNumberOfRowsIsRaisedRatherThanOverrunning()
    {
        // A row has to fall exactly on the horizon, so an even count is raised by one.  That rounding
        // once outran the table it had already allocated -- every render with the default settings
        // died on it, and no test noticed, because every test until this one passed an odd count.
        foreach (int rows in new[] { 2, 16, 17, 96, 97 })
        {
            SkyTable table = new (new Atmosphere(), new Vector(0.4, 0.7, -0.5).Unit, 0, rows, 8);

            foreach (double up in new[] { 90.0, 45, 0.5, -0.5, -45, -90 })
            {
                double angle = up * Math.PI / 180;
                Color seen = table.Toward(new Vector(Math.Cos(angle), Math.Sin(angle), 0));

                Assert.IsTrue(seen.Red >= 0, $"{rows} rows gave {seen} looking {up} degrees up");
            }
        }
    }

    [TestMethod]
    public void TestWorkingTheWholeSkyOutIsQuickEnoughToDoEveryRender()
    {
        // It happens once per render, so a second would be tolerable and ten would not.  This is here
        // to catch the steps or the table growing until the cost stops being tolerable.
        Stopwatch watch = Stopwatch.StartNew();

        _ = new SkyTable(new Atmosphere(), new Vector(0.5, 0.6, -0.3).Unit, 0);

        watch.Stop();

        Assert.IsTrue(watch.ElapsedMilliseconds < 3000,
            $"working the sky out took {watch.ElapsedMilliseconds}ms");

        Console.WriteLine($"the whole sky took {watch.ElapsedMilliseconds}ms");
    }
}
