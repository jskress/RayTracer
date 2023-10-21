using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class represents a file that contains an image, either for loading from an
/// existing file or writing an image to the file.
/// </summary>
public class ImageFile
{
    private static readonly Dictionary<string, Func<IImageCodec>> ImageCodecs = new ()
    {
        { ".ppm", () => new PpmCodec() }
    };

    private readonly string _fileName;
    private readonly IImageCodec _codec;

    public ImageFile(string name)
    {
        _fileName = name;
        _codec = DetermineCodec(_fileName);
    }

    /// <summary>
    /// This method is used to save the given image to the file, in the format
    /// indicated by its extension.
    /// </summary>
    /// <param name="canvas">The image to save.</param>
    public void Save(Canvas canvas)
    {
        using Stream stream = new FileStream(_fileName, FileMode.Create);

        _codec.Encode(canvas, stream);
    }

    /// <summary>
    /// This method is used to resolve our file's extension into an appropriate
    /// image codec.
    /// </summary>
    /// <param name="name">The file name to start with.</param>
    /// <returns>A properly constructed image codec.</returns>
    /// <exception cref="ArgumentException">if a codec cannot be created.</exception>
    private static IImageCodec DetermineCodec(string name)
    {
        string extension = Path.GetExtension(name);

        if (extension == null || extension.Trim() == string.Empty)
            throw new ArgumentException($"Cannot determine image format from '{name}'.");

        if (ImageCodecs.TryGetValue(extension.ToLower(), out Func<IImageCodec>? codecCreator))
            return codecCreator.Invoke();

        throw new ArgumentException($"The extension, '{extension}', is not supported.");
    }
}
