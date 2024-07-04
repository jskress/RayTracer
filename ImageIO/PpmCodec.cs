using System.Text;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides the base class for all variants of the PPM file format.
/// </summary>
public abstract class PpmCodec : BaseCodec
{
    /// <summary>
    /// This property is reported by subclasses to indicate the specific value of the magic
    /// number to put, or expect, in the image file's marker.
    /// </summary>
    protected abstract int MagicNumber { get; }

    /// <summary>
    /// This method is used to encode the given canvas to the specified stream.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="info">Metadata about the image.</param>
    public override void Encode(Canvas canvas, Stream stream, ImageInformation info)
    {
        WriteHeader(canvas, stream, info);
        WritePixels(canvas, stream);
    }

    /// <summary>
    /// This method is used to write an appropriate header for the given canvas.
    /// </summary>
    /// <param name="canvas">The canvas which holds the image we are writing out.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="info">Metadata about the image.</param>
    private void WriteHeader(Canvas canvas, Stream stream, ImageInformation info)
    {
        StringBuilder builder = new StringBuilder()
            .Append($"P{MagicNumber}\n");

        if (info != null)
        {
            AddInfoField(builder, PredefinedTextKeywords.Title, info.Title);
            AddInfoField(builder, PredefinedTextKeywords.Author, info.Author);
            AddInfoField(builder, PredefinedTextKeywords.Description, info.Description);
            AddInfoField(builder, PredefinedTextKeywords.Copyright, info.Copyright);
            AddInfoField(builder, PredefinedTextKeywords.CreationTime, info.CreationTime.ToString("r"));
            AddInfoField(builder, PredefinedTextKeywords.Software, info.Software);
            AddInfoField(builder, PredefinedTextKeywords.Disclaimer, info.Disclaimer);
            AddInfoField(builder, PredefinedTextKeywords.Warning, info.Warning);
            AddInfoField(builder, PredefinedTextKeywords.Source, info.Source);
            AddInfoField(builder, PredefinedTextKeywords.Comment, info.Comment);
        }

        builder.Append($"{canvas.Width} {canvas.Height}\n" +
                       $"{ProgramOptions.Instance.MaxColorChannelValue}\n");

        ImageFileIo.WriteText(stream, builder.ToString());
    }

    /// <summary>
    /// This is a helper method for conditionally adding a field to a string builder.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="label">The label for the field.</param>
    /// <param name="value">The value of the field.</param>
    private static void AddInfoField(StringBuilder builder, string label, string value)
    {
        if (value != null && value.Trim().Length > 0)
            builder.Append($"# {label}: {value.Trim()}\n");
    }

    /// <summary>
    /// This method is used to write the appropriate pixel data in PPM format to the
    /// file.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to write to.</param>
    protected abstract void WritePixels(Canvas canvas, Stream stream);

    /// <summary>
    /// This method is used to read the header of a PPM file.  It returns a properly sized
    /// canvas and the maximum value for a color channel.  If we've hit the end of the
    /// stream, then the canvas returned will be <c>null</c>.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>A tuple containing the canvas and max value.</returns>
    protected (Canvas, int) ReadHeader(Stream stream)
    {
        (string marker, string[] words) = NextWord(stream);

        if (marker == null)
            return (null, 0);

        if (marker != $"P{MagicNumber}")
            throw new Exception("File does not look like a PPM file or it is corrupted.");

        (string widthText, words) = NextWord(stream, words);
        (string heightText, words) = NextWord(stream, words);
        (string maxValueText, words) = NextWord(stream, words);
        int width = ToSafeInt(widthText);
        int height = ToSafeInt(heightText);
        int maxValue = ToSafeInt(maxValueText);

        if (width < 1 || height < 1 || maxValue < 1 || words.Length != 0)
            throw new Exception("File does not look like a PPM file or it is corrupted.");

        return (new Canvas(width, height), maxValue);
    }

    /// <summary>
    /// This is a helper method for getting the next text word from a word array which is,
    /// as necessary, populated by reading lines from the given stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="words">The current list of words.</param>
    /// <returns>A tuple containing the next word found and any words found but not yet
    /// consumed.</returns>
    protected static (string, string[]) NextWord(Stream stream, string[] words = null)
    {
        if (words == null || words.Length == 0)
        {
            do
            {
                string line = ImageFileIo.ReadLine(stream);

                if (line == null)
                    return (null, null);

                int p = line.IndexOf('#');

                if (p >= 0)
                    line = line[..p].Trim();

                words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
            while (words.Length == 0);
        }

        return (words[0], words[1..]);
    }

    /// <summary>
    /// This is a helper method for parsing a word into an integer.  If the parse fails,
    /// then <c>-1</c> is returned.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The converted text.</returns>
    protected static int ToSafeInt(string text)
    {
        return text == null ? -1 : int.TryParse(text, out int result) ? result : -1;
    }

    /// <summary>
    /// This is a helper method for converting a color to its appropriate RGB values.
    /// </summary>
    /// <param name="color">The colors to convert.</param>
    /// <returns>The color as its separate channel values.</returns>
    protected static (int, int, int) ToChannelValues(Color color)
    {
        if (ProgramOptions.Instance.Grayscale)
        {
            (int gray, _) = color.ToGrayValue();

            return (gray, gray, gray);
        }

        (int red, int green, int blue, _) = color.ToChannelValues();

        return (red, green, blue);
    }
}
