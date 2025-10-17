using RayTracer.Graphics;
using RayTracer.Utils;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a simplistic image cache so that we don't end up with multiple
/// copies of the same image in memory.
/// </summary>
public static class ImageCache
{
    private static readonly Dictionary<string, Canvas> Cache = [];

    /// <summary>
    /// This method returns an image known by the given path/URL.  If we've loaded it
    /// before, we return what we already loaded.  Otherwise, we load the image, cache it
    /// and return it.
    /// </summary>
    /// <param name="imageName">The path or URL to the image.</param>
    /// <param name="sourceDirectory">The directory in which to look for the image when it's
    /// local.</param>
    /// <param name="alwaysLoad">A flag that notes whether we should always load the image,
    /// bypassing the cache.</param>
    /// <returns>The canvas that represents the image.</returns>
    public static Canvas GetImage(string imageName, string sourceDirectory, bool alwaysLoad = false)
    {
        if (!HttpUtils.LooksLikeUrl(imageName))
            imageName = Path.GetFullPath(Path.Combine(sourceDirectory, imageName));

        if (alwaysLoad || !Cache.TryGetValue(imageName, out Canvas canvas))
        {
            ImageFile imageFile = new ImageFile(imageName);

            canvas = imageFile.Load()[0];

            if (!alwaysLoad)
                Cache[imageName] = canvas;
        }

        return canvas;
    }
}
