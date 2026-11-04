using System.Globalization;
using System.Text;

namespace RayTracer.ImageIO;

/// <summary>
/// This class writes and reads pictures of three channels of light as Portable FloatMaps: the plainest file of
/// light there is, a short text header and then the numbers as they stand.  It is what Intel's denoiser reads
/// and writes, and is done by hand here rather than through Magick.NET, which turns colors it is handed by the
/// curve it takes them to be in.
/// <para>
/// A three-channel file says <c>PF</c>, its width and height, and a scale whose sign is the byte order -- less
/// than nought, little-endian, which is what is written and all that is read -- and then its rows of 32-bit
/// floats, red, green and blue, from the BOTTOM row up.
/// </para>
/// </summary>
public static class PortableFloatMap
{
    /// <summary>
    /// This method writes a picture of three channels to a file.
    /// </summary>
    /// <param name="fileName">The file to write.</param>
    /// <param name="width">How many pixels across the picture is.</param>
    /// <param name="height">How many pixels down it is.</param>
    /// <param name="values">Its numbers, three a pixel, row by row from the TOP, as the picture is read.</param>
    public static void Write(string fileName, int width, int height, float[] values)
    {
        if (values.Length != width * height * 3)
            throw new ArgumentException("A picture of three channels holds three numbers a pixel.");

        using FileStream stream = File.Create(fileName);
        using BinaryWriter writer = new (stream);

        writer.Write(Encoding.ASCII.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"PF\n{width} {height}\n-1.0\n")));

        byte[] row = new byte[width * 3 * sizeof(float)];

        for (int y = height - 1; y >= 0; y--)
        {
            Buffer.BlockCopy(values, y * width * 3 * sizeof(float), row, 0, row.Length);

            if (!BitConverter.IsLittleEndian)
                Reverse(row);

            writer.Write(row);
        }
    }

    /// <summary>
    /// This method reads a picture of three channels from a file written little-endian.
    /// </summary>
    /// <param name="fileName">The file to read.</param>
    /// <returns>Its width, its height, and its numbers, three a pixel, row by row from the top.</returns>
    public static (int Width, int Height, float[] Values) Read(string fileName)
    {
        byte[] bytes = File.ReadAllBytes(fileName);
        int at = 0;
        string kind = Word(bytes, ref at);

        if (kind != "PF")
            throw new InvalidDataException($"{fileName} is not a Portable FloatMap of three channels.");

        int width = int.Parse(Word(bytes, ref at), CultureInfo.InvariantCulture);
        int height = int.Parse(Word(bytes, ref at), CultureInfo.InvariantCulture);
        double scale = double.Parse(Word(bytes, ref at), CultureInfo.InvariantCulture);

        // One white space ends the header, and the numbers follow it at once.
        at++;

        if (scale >= 0)
            throw new InvalidDataException($"{fileName} is written big-endian, which is not read.");

        int size = width * 3 * sizeof(float);

        if (bytes.Length - at < size * height)
            throw new InvalidDataException($"{fileName} ends before its last row.");

        float[] values = new float[width * height * 3];
        byte[] row = new byte[size];

        for (int y = height - 1; y >= 0; y--, at += size)
        {
            Array.Copy(bytes, at, row, 0, size);

            if (!BitConverter.IsLittleEndian)
                Reverse(row);

            Buffer.BlockCopy(row, 0, values, y * size, size);
        }

        return (width, height, values);
    }

    /// <summary>
    /// This method reads the next word of a file's header, leaving the place at the space after it.
    /// </summary>
    private static string Word(byte[] bytes, ref int at)
    {
        while (at < bytes.Length && char.IsWhiteSpace((char) bytes[at]))
            at++;

        int start = at;

        while (at < bytes.Length && !char.IsWhiteSpace((char) bytes[at]))
            at++;

        return Encoding.ASCII.GetString(bytes, start, at - start);
    }

    /// <summary>
    /// This method turns each float's bytes about, for a machine that keeps them the other way round.
    /// </summary>
    private static void Reverse(byte[] row)
    {
        for (int index = 0; index < row.Length; index += sizeof(float))
            Array.Reverse(row, index, sizeof(float));
    }
}
