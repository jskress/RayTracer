using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class represents a file that contains an image, either for loading from an
/// existing file or writing an image to the file.
/// </summary>
public class ImageFile
{
    private readonly string _fileName;
    private readonly string _outputImageFormat;

    public ImageFile(string name, string outputImageFormat = null)
    {
        _fileName = name;
        _outputImageFormat = outputImageFormat;
    }

    /// <summary>
    /// This method is used to load images from the image file.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <returns>An array of the images found in the image file.</returns>
    public Canvas[] Load(RenderContext context)
    {
        IImageCodec codec = DetermineCodec(_fileName, false, null);

        using Stream stream = File.OpenRead(_fileName);

        return codec.Decode(context, stream);
    }

    /// <summary>
    /// This method is used to save the given image to the file, in the format
    /// indicated by its extension.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <param name="canvas">The image to save.</param>
    /// <param name="extension">An optional concrete extension to use.</param>
    /// <param name="info">Metadata about the image.</param>
    public void Save(RenderContext context, Canvas canvas, string extension = null, ImageInformation info = null)
    {
        extension ??= _outputImageFormat;

        IImageCodec codec = DetermineCodec(_fileName, true, extension);

        using Stream stream = File.OpenWrite(_fileName);

        codec.Encode(context, canvas, stream, info);
    }

    /// <summary>
    /// This method is used to resolve our file's extension into an appropriate
    /// image codec.
    /// </summary>
    /// <param name="name">The file name to start with.</param>
    /// <param name="forOutput">A flag noting whether we need a codec for input or output.</param>
    /// <param name="extension">An optional concrete extension to use.</param>
    /// <returns>A properly constructed image codec.</returns>
    private static IImageCodec DetermineCodec(string name, bool forOutput, string extension)
    {
        CodecFormatInfo format = null;

        // If we're reading the file, then try to resolve the codec by marker first.
        if (!forOutput && File.Exists(name))
        {
            format = AvailableCodecs.FindByMarker(
                ReadFileMarker(name, AvailableCodecs.LongestMarkerLength));
        }

        extension ??= Path.GetExtension(name);

        if (format == null)
        {
            if (extension == null || extension.Trim() == string.Empty)
                throw new ArgumentException($"Cannot determine image format from '{name}'.");

            if (extension.StartsWith('.'))
                extension = extension[1..];

            format = AvailableCodecs.FindByExtension(extension);
        }

        if (format == null)
            throw new ArgumentException($"The format, '{extension}', is not supported.");

        return format.Creator();
    }

    /// <summary>
    /// This is a helper method for reading the first <c>size</c> bytes of the given file.
    /// </summary>
    /// <param name="fileName">The name of the file to read.</param>
    /// <param name="size">The largest marker size.</param>
    /// <returns>A byte array containing the first bytes of the file.  The array will never
    /// be larger than <c>size</c>.</returns>
    private static byte[] ReadFileMarker(string fileName, int size)
    {
        using FileStream stream = new FileStream(fileName, FileMode.Open);
        byte[] buffer = new byte[size];
        int count = stream.Read(buffer, 0, size);

        return count < size ? buffer[..count] : buffer;
    }
}
