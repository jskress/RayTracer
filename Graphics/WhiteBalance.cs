namespace RayTracer.Graphics;

/// <summary>
/// This class balances a finished picture the way a camera is balanced for the light it is shooting
/// under, so that a glow at a chosen temperature comes out white.
/// <para>
/// **Left alone, white is light of the same strength at every wavelength** -- about 5500 K, noon
/// sunlight, which is how a film balanced for daylight sees.  That is the right camera outdoors and the
/// wrong one indoors: a household bulb glows at about 2800 K, and seen on daylight film a room lit by one
/// is deeply orange, which is what such a photograph really looks like, and not what a room looks like
/// to someone standing in it, the eye adjusting to whatever it is lit by.  Balancing for the bulb is what
/// a photographer does, and what this does: the room comes out warm rather than orange, and daylight
/// through its windows comes out blue, exactly as it does in a photograph balanced the same way.
/// </para>
/// <para>
/// **The balance is a von Kries adaptation in Bradford's cone space**, the standard way of carrying
/// colors from one white to another: each color is taken to how three kinds of cone would answer it,
/// each answer scaled by how much stronger that cone's answer to the new white is than to the old, and
/// taken back.  Scaling red, green and blue directly instead would carry the whites across too, but
/// would pull saturated colors toward the wrong hues on the way.  The glow being balanced for stands at
/// the same brightness as white, so the balance moves colors and not exposure.
/// </para>
/// </summary>
public class WhiteBalance
{
    /// <summary>
    /// This property holds the temperature, in kelvin, of the glow that comes out white.
    /// </summary>
    public double Temperature { get; }

    // Linear sRGB to XYZ, and the Bradford matrix from XYZ to cone answers.
    private static readonly double[,] RgbToXyz =
    {
        { 0.4124564, 0.3575761, 0.1804375 },
        { 0.2126729, 0.7151522, 0.0721750 },
        { 0.0193339, 0.1191920, 0.9503041 }
    };

    private static readonly double[,] Bradford =
    {
        { 0.8951, 0.2664, -0.1614 },
        { -0.7502, 1.7135, 0.0367 },
        { 0.0389, -0.0685, 1.0296 }
    };

    private readonly double[,] _matrix;

    /// <summary>
    /// This constructor balances for a glow at the given temperature.
    /// </summary>
    /// <param name="temperature">The temperature, in kelvin, of the glow that should come out white.</param>
    public WhiteBalance(double temperature)
    {
        if (temperature <= 0)
            throw new ArgumentException("A temperature is in kelvin, and nothing glows at absolute zero or below.");

        Temperature = temperature;

        Color glow = SpectralColor.ToColor(SpectralColor.Blackbody(temperature));
        double[] from = Multiply(Bradford, Multiply(RgbToXyz, [glow.Red, glow.Green, glow.Blue]));
        double[] to = Multiply(Bradford, Multiply(RgbToXyz, [1, 1, 1]));
        double[,] scale = new double[3, 3];

        for (int index = 0; index < 3; index++)
            scale[index, index] = to[index] / from[index];

        _matrix = Multiply(
            Inverse(RgbToXyz), Multiply(Inverse(Bradford), Multiply(scale, Multiply(Bradford, RgbToXyz))));
    }

    /// <summary>
    /// This method balances one color, keeping how much of its pixel it covers.  A color carried outside
    /// what a screen can show -- a saturated one, toward the white being balanced away -- is brought back
    /// in the way the sky's colors are.
    /// </summary>
    /// <param name="color">The color, as rendered.</param>
    /// <returns>The color, balanced.</returns>
    public Color Apply(Color color)
    {
        Color balanced = SpectralColor.IntoGamut(new Color(
            _matrix[0, 0] * color.Red + _matrix[0, 1] * color.Green + _matrix[0, 2] * color.Blue,
            _matrix[1, 0] * color.Red + _matrix[1, 1] * color.Green + _matrix[1, 2] * color.Blue,
            _matrix[2, 0] * color.Red + _matrix[2, 1] * color.Green + _matrix[2, 2] * color.Blue));

        return color.Alpha == 1 ? balanced : balanced.WithAlpha(color.Alpha);
    }

    /// <summary>
    /// This method multiplies a three by three matrix into a vector.
    /// </summary>
    private static double[] Multiply(double[,] matrix, double[] vector)
    {
        double[] result = new double[3];

        for (int row = 0; row < 3; row++)
            result[row] = matrix[row, 0] * vector[0] + matrix[row, 1] * vector[1] + matrix[row, 2] * vector[2];

        return result;
    }

    /// <summary>
    /// This method multiplies two three by three matrices.
    /// </summary>
    private static double[,] Multiply(double[,] left, double[,] right)
    {
        double[,] result = new double[3, 3];

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                for (int index = 0; index < 3; index++)
                    result[row, column] += left[row, index] * right[index, column];
            }
        }

        return result;
    }

    /// <summary>
    /// This method inverts a three by three matrix, by its adjugate.
    /// </summary>
    private static double[,] Inverse(double[,] m)
    {
        double a = m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1];
        double b = m[1, 2] * m[2, 0] - m[1, 0] * m[2, 2];
        double c = m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0];
        double determinant = m[0, 0] * a + m[0, 1] * b + m[0, 2] * c;

        return new double[,]
        {
            {
                a / determinant, (m[0, 2] * m[2, 1] - m[0, 1] * m[2, 2]) / determinant,
                (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]) / determinant
            },
            {
                b / determinant, (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]) / determinant,
                (m[0, 2] * m[1, 0] - m[0, 0] * m[1, 2]) / determinant
            },
            {
                c / determinant, (m[0, 1] * m[2, 0] - m[0, 0] * m[2, 1]) / determinant,
                (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]) / determinant
            }
        };
    }
}
