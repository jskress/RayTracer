using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides the means by which an image may be specified in a scene file.
/// </summary>
public class ImageReference
{
    /// <summary>
    /// This property holds the file name or URL of the image.
    /// </summary>
    public string ImageName { get; set; }

    /// <summary>
    /// This property holds the directory that contains the directory to look in for the
    /// image when it is not specified by a URL.
    /// </summary>
    public string SourceDirectory { get; set; }

    /// <summary>
    /// This property notes whether the image we load should be cached or always loaded.
    /// </summary>
    public bool AlwaysLoad { get; set; }

    /// <summary>
    /// This property produces a canvas by loading the image we refer to.
    /// </summary>
    public Canvas Canvas => ImageCache.GetImage(ImageName, SourceDirectory, AlwaysLoad);

    /// <summary>
    /// This method returns whether the given image reference matches this one.
    /// </summary>
    /// <param name="other">The image reference to compare to.</param>
    /// <returns><c>true</c>, if the two image references match, or <c>false</c>, if not.</returns>
    public bool Matches(ImageReference other)
    {
        return ImageName == other.ImageName &&
               SourceDirectory == other.SourceDirectory &&
               AlwaysLoad == other.AlwaysLoad;
    }
}
