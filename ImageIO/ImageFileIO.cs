using System.Text;
using RayTracer.Extensions;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides some low-level support for reading and writing image files.
/// </summary>
public static class ImageFileIo
{
    /// <summary>
    /// This is a helper method for writing the given string to the specified stream.  The
    /// text is converted to bytes by applying ASCII encoding.
    /// </summary>
    /// <param name="stream">The stream to write the text to.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="encoding">The encoding to use.  This defaults to ASCII encoding.</param>
    public static void WriteText(Stream stream, string text, Encoding encoding = null)
    {
        encoding ??= Encoding.ASCII;

        byte[] bytes = encoding.GetBytes(text);

        WriteBytes(stream, bytes);
    }

    /// <summary>
    /// This is a helper method for writing the given string to the specified stream.  The
    /// text is converted to bytes by applying Latin1 encoding, ended, if indicated by a
    /// null byte.
    /// </summary>
    /// <param name="stream">The stream to write the text to.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="includeNullByte">Whether a string-ending null byte should be emitted.</param>
    /// <param name="encoding">The encoding to use.  This defaults to ASCII encoding.</param>
    public static void WriteText(
        Stream stream, string text, bool includeNullByte, Encoding encoding = null)
    {
        encoding ??= Encoding.Latin1;

        byte[] bytes = encoding.GetBytes(text);

        WriteBytes(stream, bytes);
        
        if (includeNullByte)
            WriteByte(stream, 0);
    }

    /// <summary>
    /// THis is a helper method for writing a binary integer to the given stream.  Though
    /// it is not enforced, it is expected that <c>byteCount</c> will be between 1 and 4.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="number">The number to write out.</param>
    /// <param name="byteCount">The number of bytes to write.</param>
    /// <param name="bigEndian">Whether the number should be written in big- or little-endian
    /// format.</param>
    public static void WriteInt(Stream stream, int number, int byteCount, bool bigEndian = true)
    {
        byte[] bytes = new byte[byteCount];

        for (int index = 0; index < byteCount; index++)
        {
            bytes[index] = (byte) (number & 0x000000FF);

            number >>>= 8;
        }

        if (bigEndian)
            bytes = bytes.Reverse().ToArray();

        WriteBytes(stream, bytes);
    }

    /// <summary>
    /// THis is a helper method for writing an unsigned binary integer to the given stream.
    /// Though it is not enforced, it is expected that <c>byteCount</c> will be between 1
    /// and 4.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="number">The number to write out.</param>
    /// <param name="byteCount">The number of bytes to write.</param>
    /// <param name="bigEndian">Whether the number should be written in big- or little-endian
    /// format.</param>
    public static void WriteUInt(Stream stream, uint number, int byteCount, bool bigEndian = true)
    {
        byte[] bytes = new byte[byteCount];

        for (int index = 0; index < byteCount; index++)
        {
            bytes[index] = (byte) (number & 0x000000FF);

            number >>>= 8;
        }

        if (bigEndian)
            bytes = bytes.Reverse().ToArray();

        WriteBytes(stream, bytes);
    }

    /// <summary>
    /// This method is used to write the given byte to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="data">The data byte to write out.</param>
    public static void WriteByte(Stream stream, byte data)
    {
        stream.WriteByte(data);
    }

    /// <summary>
    /// This method is used to write the given array of bytes to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="bytes">The bytes to write out.</param>
    public static void WriteBytes(Stream stream, byte[] bytes)
    {
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// This is a helper method for reading a binary integer from the stream.  Though it is
    /// not enforced, it is expected that <c>byteCount</c> will be between 1 and 4.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="isRequired">Notes whether the number is required.</param>
    /// <returns>The integral number from the stream.</returns>
    public static byte? ReadByte(Stream stream, bool isRequired = true)
    {
        return ReadBytes(stream, 1, isRequired)?[0];
    }

    /// <summary>
    /// This is a helper method for reading a binary integer from the stream.  Though it is
    /// not enforced, it is expected that <c>byteCount</c> will be between 1 and 4.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="byteCount">The number of bytes the integer occupies.</param>
    /// <param name="bigEndian">Whether the number is in big- or little-endian format.
    /// This is irrelevant when <c>byteCount</c> is 1.</param>
    /// <param name="isRequired">Notes whether the number is required.</param>
    /// <returns>The integral number from the stream.</returns>
    public static int? ReadInt(
        Stream stream, int byteCount, bool bigEndian = true, bool isRequired = true)
    {
        // First, read the required number of bytes.
        byte[] bytes = ReadBytes(stream, byteCount, isRequired);

        if (bytes == null)
            return null;

        // If the number is little-endian, reverse the byte order.
        if (!bigEndian && byteCount > 1)
            bytes = bytes.Reverse().ToArray();

        int result = 0;

        // Finally, merge the bytes into the proper integer.
        for (int i = 0; i < byteCount; i++)
            result = result << 8 | bytes[i];

        return result;
    }

    /// <summary>
    /// This is a helper method for reading an unsigned binary integer from the stream.
    /// Though it is not enforced, it is expected that <c>byteCount</c> will be between
    /// 1 and 4.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="byteCount">The number of bytes the integer occupies.</param>
    /// <param name="bigEndian">Whether the number is in big- or little-endian format.
    /// This is irrelevant when <c>byteCount</c> is 1.</param>
    /// <param name="isRequired">Notes whether the number is required.</param>
    /// <returns>The integral number from the stream.</returns>
    public static uint? ReadUInt(
        Stream stream, int byteCount, bool bigEndian = true, bool isRequired = true)
    {
        // First, read the required number of bytes.
        byte[] bytes = ReadBytes(stream, byteCount, isRequired);

        // If the number is little-endian, reverse the byte order.
        if (!bigEndian && byteCount > 1)
            bytes = bytes.Reverse().ToArray();

        uint result = 0;

        // Finally, merge the bytes into the proper integer.
        for (int i = 0; i < byteCount; i++)
            result = result << 8 | (uint) (bytes[i] & 0x000000FF);

        return result;
    }

    /// <summary>
    /// This method is used to read a string of text from the given stream.  The string
    /// will be ended by either a null byte or the end of the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="tillNull">A flag noting whether the string should be ended by a null
    /// byte or the end of the stream.</param>
    /// <param name="encoding">The encoding to use.  This defaults to Latin1 encoding.</param>
    /// <returns>The string that was read.</returns>
    public static string ReadText(Stream stream, bool tillNull = true, Encoding encoding = null)
    {
        encoding ??= Encoding.Latin1;

        using MemoryStream buffer = new MemoryStream();
        byte? data = ReadByte(stream, tillNull);

        while (data.HasValue && data.Value != 0)
        {
            buffer.WriteByte(data.Value);

            data = ReadByte(stream, tillNull);
        }

        if ((!data.HasValue && tillNull) || (data is 0 && !tillNull))
            throw new Exception("PNG Image file is corrupted.  End of file unexpectedly reached.");

        return encoding.GetString(buffer.ToArray());
    }

    /// <summary>
    /// This method is used to read a string of text from the given stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="byteCount">The number of characters to read.</param>
    /// <param name="encoding">The encoding to use.  This defaults to ASCII encoding.</param>
    /// <param name="isRequired">Whether the field is required.</param>
    /// <returns>The string that was read.</returns>
    public static string ReadText(
        Stream stream, int byteCount, Encoding encoding = null, bool isRequired = true)
    {
        encoding ??= Encoding.ASCII;

        byte[] bytes = ReadBytes(stream, byteCount, isRequired);

        return bytes == null ? null : encoding.GetString(bytes);
    }

    /// <summary>
    /// This is a helper method for reading a fixed number of bytes from the given stream.
    /// There must be at least <c>byteCount</c> bytes to read or none, if the result is
    /// not required.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="byteCount">The number of bytes to read.</param>
    /// <param name="isRequired">Whether the field is required.</param>
    /// <returns>The requested array of bytes, or <c>null</c>, if the bytes are not required
    /// and the end of the stream has been reached.</returns>
    public static byte[] ReadBytes(Stream stream, int byteCount, bool isRequired = true)
    {
        byte[] bytes = new byte[byteCount];
        int read = 0;

        if (byteCount > 0)
            read = ReadBytes(stream, bytes, isRequired);

        return read == 0 ? null : bytes;
    }

    /// <summary>
    /// This is a helper method for reading a fixed number of bytes from the given stream
    /// into the given byte array.  There must be enough bytes to read or none, if the
    /// result is not required.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="bytes">The byte buffer to read into.</param>
    /// <param name="isRequired">Whether the field is required.</param>
    /// <returns>The requested array of bytes, or <c>null</c>, if the bytes are not required
    /// and the end of the stream has been reached.</returns>
    public static int ReadBytes(Stream stream, byte[] bytes, bool isRequired = true)
    {
        int offset = 0;
        int bytesToRead = bytes.Length;

        while (bytesToRead > 0)
        {
            int read = stream.Read(bytes, offset, bytesToRead);

            bytesToRead -= read;
            offset += read;

            if (read == 0)
                break;
        }

        if (bytesToRead > 0 && isRequired)
            throw new Exception("PNG Image file is corrupted.  End of file unexpectedly reached.");

        return bytesToRead > 0 ? 0 : bytes.Length;
    }

    /// <summary>
    /// This method is used to read a line of text from the given stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="encoding">The encoding to use.  This defaults to ASCII encoding.</param>
    /// <returns>The string that was read.</returns>
    public static string ReadLine(Stream stream, Encoding encoding = null)
    {
        encoding ??= Encoding.ASCII;

        List<byte> bytes = [];
        
        int data = stream.ReadByte();

        while (data >= 0 && data != '\n')
        {
            bytes.Add((byte) data);

            data = stream.ReadByte();
        }

        return data < 0 && bytes.IsEmpty() ? null : encoding.GetString(bytes.ToArray());
    }
}
