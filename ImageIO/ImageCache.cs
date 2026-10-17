using System.Collections.Concurrent;
using RayTracer.Graphics;
using RayTracer.Utils;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a simplistic image cache so that we don't end up with multiple
/// copies of the same image in memory.
/// </summary>
public static class ImageCache
{
    private static readonly ConcurrentDictionary<string, Canvas> Cache = new ();
    private static readonly ConcurrentDictionary<string, Texture> Textures = new ();

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

    /// <summary>
    /// This method returns an image known by the given path/URL made ready to be read as colors: its
    /// values turned into light, or taken as they stand, and its pyramid of smaller copies built.  It is
    /// kept apart from the image itself, which is read afresh to make it, so that a height field taking
    /// its heights from the same file -- and turning that file gray where it lies -- reads the file's own
    /// values while a pigment reads its colors.
    /// </summary>
    /// <param name="imageName">The path or URL to the image.</param>
    /// <param name="sourceDirectory">The directory in which to look for the image when it's
    /// local.</param>
    /// <param name="asLight">Whether the image's values are to be turned into light.</param>
    /// <param name="alwaysLoad">A flag that notes whether we should always load the image,
    /// bypassing the cache.</param>
    /// <returns>The texture made from the image.</returns>
    public static Texture GetTexture(
        string imageName, string sourceDirectory, bool asLight, bool alwaysLoad = false)
    {
        if (!HttpUtils.LooksLikeUrl(imageName))
            imageName = Path.GetFullPath(Path.Combine(sourceDirectory, imageName));

        string key = asLight ? imageName : $"as it stands|{imageName}";

        if (alwaysLoad || !Textures.TryGetValue(key, out Texture texture))
        {
            texture = Texture.From(new ImageFile(imageName).Load()[0], asLight);

            if (!alwaysLoad)
                Textures[key] = texture;
        }

        return texture;
    }
}
