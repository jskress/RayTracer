namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a reference to the metadata for all our known image file codecs.
/// </summary>
public static class AvailableCodecs
{
    /// <summary>
    /// This field holds the metadata for all our known image file codecs.
    /// </summary>
    private static readonly CodecMetaData[] MetaData =
    [
        new CodecMetaData
        {
            Name = "Portable Pixel Map",
            Extensions = [ "ppm" ],
            Formats = [
                new CodecFormatInfo
                {
                    Extensions = [ "p3" ],
                    Marker = new FileTypeMarker("P", "3", FileTypeMarker.Whitespace),
                    Creator = () => new Ppm3Codec()
                },
                new CodecFormatInfo
                {
                    Extensions = [ "p6" ],
                    Marker = new FileTypeMarker("P", "6", FileTypeMarker.Whitespace),
                    Creator = () => new Ppm6Codec()
                }
            ],
            DefaultFormatIndex = 0
        }
    ];

    /// <summary>
    /// This property makes available the largest number of bytes needed from a source file
    /// to check on image file type markers.
    /// </summary>
    internal static int LongestMarkerLength => LazyLongestMarkerLength.Value;

    private static readonly Lazy<int> LazyLongestMarkerLength = new (
        () => MetaData
            .SelectMany(metadata => metadata.Formats)
            .Select(format => format.Marker.Length)
            .Max());

    /// <summary>
    /// This method will find the appropriate codec format that claims to support the
    /// given image file marker.
    /// </summary>
    /// <param name="marker">The marker to find.</param>
    /// <returns>The appropriate codec format, or <c>null</c>, if we couldn't find one.</returns>
    internal static CodecFormatInfo FindByMarker(byte[] marker)
    {
        return MetaData
            .SelectMany(metadata => metadata.Formats)
            .FirstOrDefault(format => format.Marker.Matches(marker));
    }

    /// <summary>
    /// This method returns whether a given format has a codec that supports it.
    /// </summary>
    /// <param name="format">The format to look for.</param>
    /// <returns><c>true</c>, if there's a codec for the format, or <c>false</c>, if not.</returns>
    public static bool IsFormatSupported(string format)
    {
        if (format.StartsWith('.'))
            format = format[1..];

        return FindByExtension(format) != null;
    }

    /// <summary>
    /// This method will find the appropriate codec format based on file extension.  First,
    /// we try to find a specific format that supports the extension.  Failing that, we
    /// look for a match at the metadata level and, if we find one, return its default
    /// format.
    /// </summary>
    /// <param name="extension">The extension to look for.</param>
    /// <returns>The appropriate codec format, or <c>null</c>, if we couldn't find one.</returns>
    internal static CodecFormatInfo FindByExtension(string extension)
    {
        extension = extension.ToLower();

        return MetaData
                   // Look for a specific match first.
                   .SelectMany(metadata => metadata.Formats)
                   .FirstOrDefault(format => format.Extensions.Contains(extension)) ??
               MetaData
                   // Failing that, try the metadata default route.
                   .FirstOrDefault(md => md.Extensions.Contains(extension))?
                   .DefaultFormat;
    }
}
