using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class parts a finished picture's red from its blue, as a lens that bends each color its own amount does:
/// lateral chromatic aberration, the red and blue fringes along bright edges toward a photograph's corners.
/// <para>
/// **A lens's glass bends blue further than red**, so a simple lens draws the red picture a little larger than the
/// blue one.  Nothing differs at the middle, and toward the corners the two have drawn apart, so a bright edge there
/// shows a red fringe on one side and a blue one on the other.  Here the red picture is drawn larger by half the
/// amount and the blue smaller by half, about the picture's middle, green standing between them: so at a corner the
/// two are the amount apart, as a share of the picture's half-diagonal, whatever its size.
/// </para>
/// <para>
/// It is done to the finished light rather than to each ray, since a ray in an ordinary render carries all three
/// colors at once and cannot be bent three ways.  It belongs before the glare (see <see cref="Diffraction"/>), whose
/// pattern each color draws about the place the lens has already put that color.
/// </para>
/// </summary>
public sealed class ChromaticAberration
{
    private readonly double _amount;

    /// <summary>
    /// This constructor makes the aberration of a lens that parts red from blue by the given share of a picture's
    /// half-diagonal at its corners.
    /// </summary>
    /// <param name="amount">How far apart the red and blue pictures are at a corner.</param>
    public ChromaticAberration(double amount)
    {
        _amount = amount;
    }

    /// <summary>
    /// This method parts the red and blue in the given picture of light, in place.
    /// </summary>
    /// <param name="canvas">The picture.</param>
    public void Part(Canvas canvas)
    {
        if (_amount <= 0)
            return;

        int width = canvas.Width, height = canvas.Height;
        float[] red = new float[width * height];
        float[] blue = new float[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = canvas.GetPixel(x, y);

                red[y * width + x] = (float) color.Red;
                blue[y * width + x] = (float) color.Blue;
            }
        }

        double middleX = width / 2.0, middleY = height / 2.0;
        double redScale = 1 / (1 + _amount / 2);
        double blueScale = 1 / (1 - _amount / 2);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double awayX = x + 0.5 - middleX, awayY = y + 0.5 - middleY;
                Color color = canvas.GetPixel(x, y);
                double newRed = Read(red, width, height, middleX + awayX * redScale, middleY + awayY * redScale);
                double newBlue = Read(blue, width, height, middleX + awayX * blueScale, middleY + awayY * blueScale);

                canvas.SetColor(new Color(newRed, color.Green, newBlue, color.Alpha), x, y);
            }
        }
    }

    /// <summary>
    /// This method reads one channel of the picture between the four pixels nearest a place, the edge going on past
    /// the picture's sides.
    /// </summary>
    /// <param name="channel">The channel's values, row by row.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    /// <param name="u">How far across, in pixels, a pixel's middle half a pixel in.</param>
    /// <param name="v">How far down, likewise.</param>
    /// <returns>The channel's value there.</returns>
    private static double Read(float[] channel, int width, int height, double u, double v)
    {
        double x = u - 0.5, y = v - 0.5;
        int x0 = (int) Math.Floor(x), y0 = (int) Math.Floor(y);
        double fx = x - x0, fy = y - y0;

        return At(x0, y0) * (1 - fx) * (1 - fy) + At(x0 + 1, y0) * fx * (1 - fy) +
               At(x0, y0 + 1) * (1 - fx) * fy + At(x0 + 1, y0 + 1) * fx * fy;

        double At(int column, int row) =>
            channel[Math.Clamp(row, 0, height - 1) * width + Math.Clamp(column, 0, width - 1)];
    }
}
