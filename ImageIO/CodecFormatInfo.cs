namespace RayTracer.ImageIO;

/// <summary>
/// This class represents information about a specific file format for a codec type.
/// </summary>
internal class CodecFormatInfo
{
    /// <summary>
    /// This property holds an array of file extensions for files expected to be in this
    /// format.
    /// </summary>
    internal string[] Extensions { get; init; }

    /// <summary>
    /// This property holds the markers for the file format.
    /// </summary>
    internal FileTypeMarker[] Markers { get; init; }

    /// <summary>
    /// This property holds a lambda that creates an instance of the supporting codec
    /// implementation.
    /// </summary>
    internal Func<IImageCodec> Creator { get; init; }
}
