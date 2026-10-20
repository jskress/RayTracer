namespace RayTracer.ImageIO;

/// <summary>
/// This class writes a PNG file's word on the gamma its values were written with: a gAMA chunk, set just
/// after the header where the format wants it.
/// <para>
/// **A PNG that says nothing of its gamma is read as sRGB**, by every viewer and by this renderer, so a file
/// written with a plain power must say so or be shown wrongly in its darks.  Magick.NET will not be told an
/// image's gamma -- the property is read only -- so the chunk is put in after the file is written.
/// </para>
/// </summary>
internal static class PngGamma
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] Table = MakeTable();

    /// <summary>
    /// This method gives the PNG file at the given path a gAMA chunk declaring the gamma its values were
    /// written with, replacing any it had.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="gamma">The gamma, as PNG says it: what light was raised to, 0.4545 for a 2.2 power.</param>
    public static void Declare(string path, double gamma)
    {
        byte[] file = File.ReadAllBytes(path);

        if (file.Length < 33 || !file.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException($"{path} is not a PNG file.");

        using MemoryStream written = new ();

        written.Write(file, 0, 8);

        int at = 8;

        while (at + 12 <= file.Length)
        {
            int length = (int) BigEndian(file, at);
            string type = System.Text.Encoding.ASCII.GetString(file, at + 4, 4);
            int next = at + 12 + length;

            if (type != "gAMA")
                written.Write(file, at, next - at);

            if (type == "IHDR")
                WriteChunk(written, "gAMA", BigEndianBytes((uint) Math.Round(gamma * 100_000)));

            at = next;
        }

        File.WriteAllBytes(path, written.ToArray());
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] typed = [.. System.Text.Encoding.ASCII.GetBytes(type), .. data];

        stream.Write(BigEndianBytes((uint) data.Length));
        stream.Write(typed);
        stream.Write(BigEndianBytes(Crc(typed)));
    }

    private static uint BigEndian(byte[] bytes, int at) =>
        (uint) (bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3]);

    private static byte[] BigEndianBytes(uint value) =>
        [(byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value];

    // The CRC-32 PNG checks each chunk's type and data by.
    private static uint Crc(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte value in bytes)
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFF;
    }

    private static uint[] MakeTable()
    {
        uint[] table = new uint[256];

        for (uint n = 0; n < 256; n++)
        {
            uint c = n;

            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;

            table[n] = c;
        }

        return table;
    }
}
