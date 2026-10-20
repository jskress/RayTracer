using System.Numerics;
using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class spreads a picture's light the way a camera's opening spreads it: the glare round a lamp, and the
/// rays a bright point throws out when the opening is a polygon of blades.
/// <para>
/// **Light through an opening does not keep to a point.**  It spreads into the pattern the opening's own shape
/// makes -- a round opening's bright disc and its faint rings, Airy's pattern, and a polygon's the same with a
/// ray thrown out square to each of its edges, which is the star round a street lamp in a photograph.  The
/// pattern is the square of the opening's Fourier transform, and it is worked out here exactly so, once, on a
/// grid.  Its size goes with the wavelength, so it is spread to each of the bands light is carried in -- red
/// furthest -- and weighed into red, green and blue by how each answers to them; what is left at the edges of
/// a bright point is faintly colored, as it is in a lens.
/// </para>
/// <para>
/// **The share the pattern is given is the scene's to say.**  A real lens spreads only a little of its light
/// this way, and a picture shows it only round things thousands of times brighter than white, which few
/// scenes have; so the glare is given as the share of all the light that is spread, and the rest is left
/// where it fell.  Every pixel's light is spread by the same pattern, so nothing is added or lost but what
/// spreads past the picture's edge.  The pattern's bright middle is a pixel and a half across in a picture
/// eight hundred wide, and the same share of any other width, so a picture rendered larger shows the same
/// glare.
/// </para>
/// </summary>
public sealed class Diffraction
{
    // The opening's pattern is worked out on a grid this many cells a side, the opening this share of it across.
    private const int Grid = 1024;
    private const double Opening = 0.24;

    // How far out the pattern's first dark ring is at 550 nanometers, as a share of the picture's width.
    private const double CoreShare = 1.5 / 800;
    private const double Reference = 550;

    /// <summary>
    /// This property holds the share of the light spread by the pattern; the rest is left where it fell.
    /// </summary>
    public double Share { get; }

    /// <summary>
    /// This property holds how many blades the opening is made of, or nought for a round one.
    /// </summary>
    public int Blades { get; }

    /// <summary>
    /// This property holds how far the blades are turned, in radians.
    /// </summary>
    public double Turn { get; }

    public Diffraction(double share, int blades = 0, double turn = 0)
    {
        Share = share;
        Blades = blades;
        Turn = turn;
    }

    /// <summary>
    /// This method spreads the light of the given picture by the opening's pattern, in place.  How much of each
    /// pixel the picture covers is left alone.
    /// </summary>
    /// <param name="canvas">The picture, holding light.</param>
    public void Spread(Canvas canvas)
    {
        if (Share <= 0)
            return;

        int width = canvas.Width, height = canvas.Height;
        int across = Fourier.PowerOfTwoFrom(2 * width), down = Fourier.PowerOfTwoFrom(2 * height);
        Complex[][] patterns = PatternsFor(width, across, down);

        for (int channel = 0; channel < 3; channel++)
        {
            Complex[] light = new Complex[across * down];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    light[y * across + x] = Real(ChannelOf(canvas.GetPixel(x, y), channel));
            }

            Fourier.Transform(light, across, down);

            Complex[] pattern = patterns[channel];

            for (int index = 0; index < light.Length; index++)
                light[index] *= (1 - Share) + Share * pattern[index];

            Fourier.Transform(light, across, down, back: true);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color was = canvas.GetPixel(x, y);
                    double spread = Math.Max(light[y * across + x].Real, 0);

                    canvas.SetColor(WithChannel(was, channel, spread), x, y);
                }
            }
        }
    }

    /// <summary>
    /// This method works out, for each of red, green and blue, the pattern a point of light spreads into over
    /// the picture's pixels, transformed ready to spread a picture by.  Each holds the whole of the point's
    /// light.
    /// </summary>
    private Complex[][] PatternsFor(int width, int across, int down)
    {
        double[] table = SummedPattern();
        double cellsPerPixel = 1.22 / Opening / (CoreShare * width);
        (double[] Scale, double[][] Weight) bands = Bands(cellsPerPixel);
        double least = bands.Scale.Min();
        int reachX = Math.Min(across / 2 - 1, (int) Math.Ceiling(Grid / 2.0 / least) + 1);
        int reachY = Math.Min(down / 2 - 1, (int) Math.Ceiling(Grid / 2.0 / least) + 1);
        Complex[][] patterns = [new Complex[across * down], new Complex[across * down], new Complex[across * down]];

        // The pattern's middle is the middle of the cell its brightest place was moved to.
        const double middle = Grid / 2 + 0.5;

        Parallel.For(-reachY, reachY + 1, dy =>
        {
            for (int dx = -reachX; dx <= reachX; dx++)
            {
                double red = 0, green = 0, blue = 0;

                for (int band = 0; band < bands.Scale.Length; band++)
                {
                    double scale = bands.Scale[band];
                    double share = Within(
                        table, middle + (dx - 0.5) * scale, middle + (dx + 0.5) * scale,
                        middle + (dy - 0.5) * scale, middle + (dy + 0.5) * scale);

                    red += bands.Weight[0][band] * share;
                    green += bands.Weight[1][band] * share;
                    blue += bands.Weight[2][band] * share;
                }

                int at = (dy + down) % down * across + (dx + across) % across;

                patterns[0][at] = red;
                patterns[1][at] = green;
                patterns[2][at] = blue;
            }
        });

        // Each band's pattern adds to one over the pixels, the whole of the table being looked over, and the
        // bands' weights add to one; so each channel's pattern holds the whole of a point's light.
        foreach (Complex[] pattern in patterns)
            Fourier.Transform(pattern, across, down);

        return patterns;
    }

    /// <summary>
    /// This method works out the opening's pattern at 550 nanometers on the grid, the whole of it adding to
    /// one, and returns it summed from its corner, so that what falls within any box of the grid can be read
    /// off in four looks.
    /// </summary>
    private double[] SummedPattern()
    {
        Complex[] grid = new Complex[Grid * Grid];
        double radius = Opening * Grid / 2;
        const int fine = 4;

        // The opening, its edges softened by looking at each cell in sixteen places.
        Parallel.For(0, Grid, y =>
        {
            for (int x = 0; x < Grid; x++)
            {
                int inside = 0;

                for (int i = 0; i < fine; i++)
                {
                    for (int j = 0; j < fine; j++)
                    {
                        if (IsOpen(x - Grid / 2 + (i + 0.5) / fine, y - Grid / 2 + (j + 0.5) / fine, radius))
                            inside++;
                    }
                }

                grid[y * Grid + x] = inside / (double) (fine * fine);
            }
        });

        Fourier.Transform(grid, Grid, Grid);

        // The pattern is the transform's strength squared, its middle moved from the corner to the middle.
        double[] pattern = new double[Grid * Grid];
        double total = 0;

        for (int y = 0; y < Grid; y++)
        {
            for (int x = 0; x < Grid; x++)
            {
                double strength = grid[(y + Grid / 2) % Grid * Grid + (x + Grid / 2) % Grid].Magnitude;

                pattern[y * Grid + x] = strength * strength;
                total += strength * strength;
            }
        }

        double[] summed = new double[(Grid + 1) * (Grid + 1)];

        for (int y = 0; y < Grid; y++)
        {
            double row = 0;

            for (int x = 0; x < Grid; x++)
            {
                row += pattern[y * Grid + x] / total;
                summed[(y + 1) * (Grid + 1) + x + 1] = summed[y * (Grid + 1) + x + 1] + row;
            }
        }

        return summed;
    }

    /// <summary>
    /// This method notes whether a place, measured from the opening's middle, is in it.
    /// </summary>
    private bool IsOpen(double x, double y, double radius)
    {
        if (Blades < 3)
            return x * x + y * y <= radius * radius;

        // Inside every blade's edge: each edge is as far from the middle as the cosine of half its angle.
        double edge = radius * Math.Cos(Math.PI / Blades);

        for (int blade = 0; blade < Blades; blade++)
        {
            double facing = Turn + (2 * blade + 1) * Math.PI / Blades;

            if (x * Math.Cos(facing) + y * Math.Sin(facing) > edge)
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method returns how many of the grid's cells each band's pattern spreads over a pixel, and how much
    /// of each band red, green and blue each take: what each answers to of light in that band, kept to what
    /// a screen can make, and weighed so that each adds to one.  An even spread of light is left white.
    /// </summary>
    private static (double[] Scale, double[][] Weight) Bands(double cellsPerPixel)
    {
        int count = SpectralColor.Bands;
        double[] scale = new double[count];
        double[][] weight = [new double[count], new double[count], new double[count]];
        double[] single = new double[count];

        for (int band = 0; band < count; band++)
        {
            single[band] = 1;

            Color answer = SpectralColor.ToUnbalancedColor(single);

            single[band] = 0;
            scale[band] = cellsPerPixel * Reference / SpectralColor.WavelengthOf(band);
            weight[0][band] = Math.Max(answer.Red, 0);
            weight[1][band] = Math.Max(answer.Green, 0);
            weight[2][band] = Math.Max(answer.Blue, 0);
        }

        foreach (double[] channel in weight)
        {
            double total = channel.Sum();

            for (int band = 0; band < count; band++)
                channel[band] /= total;
        }

        return (scale, weight);
    }

    /// <summary>
    /// This method returns how much of the pattern falls within a box of the grid, its sides in cells.
    /// </summary>
    private static double Within(double[] summed, double left, double right, double top, double bottom)
    {
        return Summed(summed, right, bottom) - Summed(summed, left, bottom) -
               Summed(summed, right, top) + Summed(summed, left, top);
    }

    // The pattern summed up to a place on the grid, between the cells' corners.
    private static double Summed(double[] summed, double x, double y)
    {
        x = Math.Clamp(x, 0, Grid);
        y = Math.Clamp(y, 0, Grid);

        int x0 = Math.Min((int) x, Grid - 1), y0 = Math.Min((int) y, Grid - 1);
        double fx = x - x0, fy = y - y0;
        int width = Grid + 1;
        double top = summed[y0 * width + x0] * (1 - fx) + summed[y0 * width + x0 + 1] * fx;
        double bottom = summed[(y0 + 1) * width + x0] * (1 - fx) + summed[(y0 + 1) * width + x0 + 1] * fx;

        return top * (1 - fy) + bottom * fy;
    }

    private static double Real(double value) => double.IsNaN(value) ? 0 : Math.Max(value, 0);

    private static double ChannelOf(Color color, int channel) =>
        channel switch { 0 => color.Red, 1 => color.Green, _ => color.Blue };

    private static Color WithChannel(Color color, int channel, double value) => channel switch
    {
        0 => new Color(value, color.Green, color.Blue, color.Alpha),
        1 => new Color(color.Red, value, color.Blue, color.Alpha),
        _ => new Color(color.Red, color.Green, value, color.Alpha)
    };
}
