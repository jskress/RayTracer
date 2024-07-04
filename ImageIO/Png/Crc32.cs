using System.Text;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class provides an implementation of the CRC32 algorithm used for the PNG file
/// format.
/// </summary>
public class Crc32
{
    private const uint Polynomial = 0xedb88320;
    private const uint AllOnes = 0xFFFFFFFF;

    /// <summary>
    /// This field holds our precomputed 8-bit messages.
    /// </summary>
    private static readonly uint[] CrcTable;

    static Crc32()
    {
        CrcTable = new uint[256];

        for (uint index = 0; index < CrcTable.Length; index++)
        {
            uint c = index;

            for (int inner = 0; inner < 8; inner++)
            {
                if ((c & 1) != 0)
                    c = Polynomial ^ (c >>> 1);
                else
                    c >>>= 1;
            }

            CrcTable[index] = c;
        }
    }

    /// <summary>
    /// This property reports the CRC32 value for all bytes accumulated so far.
    /// </summary>
    public uint Value => _register ^ AllOnes;

    private uint _register = AllOnes;

    /// <summary>
    /// This method is used to accumulate the CRC for a string.  If not specified, the
    /// encoding used will default to <c>ASCII</c>.
    /// </summary>
    /// <param name="text">The text to accumulate.</param>
    /// <param name="encoding">The encoding to use to convert the text to an array of bytes.</param>
    /// <returns>This object, for fluency.</returns>
    public Crc32 Append(string text, Encoding encoding = null)
    {
        encoding ??= Encoding.ASCII;

        return Append(encoding.GetBytes(text));
    }

    /// <summary>
    /// This method is used to accumulate the CRC for an array of bytes.
    /// </summary>
    /// <param name="buffer">The array of bytes to accumulate.</param>
    /// <returns>This object, for fluency.</returns>
    public Crc32 Append(byte[] buffer)
    {
        return Append(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// This method is used to accumulate the CRC for a range of bytes in an array.
    /// </summary>
    /// <param name="buffer">The array of bytes to accumulate.</param>
    /// <param name="offset">The offset into the array where accumulation should begin.</param>
    /// <param name="end">The offset into the array where we should stop.</param>
    /// <returns>This object, for fluency.</returns>
    public Crc32 Append(byte[] buffer, int offset, int end)
    {
        for (int index = offset; index < end; index++)
            _register = CrcTable[(_register ^ buffer[index]) & 0xff] ^ (_register >> 8);

        return this;
    }
}
