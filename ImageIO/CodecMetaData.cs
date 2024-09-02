using System.Diagnostics.CodeAnalysis;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a representation of the metadata about an image file codec.
/// </summary>
internal class CodecMetaData
{
    /// <summary>
    /// This property provides the name of the codec, suitable for display.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    internal string Name { get; init; }

    /// <summary>
    /// This property holds an array of general file extensions for files this codec can
    /// handle.
    /// </summary>
    internal string[] Extensions { get; init; }

    /// <summary>
    /// This property provides the collection of concrete formats that make up this codec.
    /// </summary>
    internal CodecFormatInfo[] Formats { get; init; }

    /// <summary>
    /// This property holds the index within <see cref="Formats"/> for the format that is
    /// the default one to use.
    /// </summary>
    internal int DefaultFormatIndex { get; init; }

    /// <summary>
    /// This property provides access to the default format to use for this codec.
    /// </summary>
    internal CodecFormatInfo DefaultFormat => Formats[DefaultFormatIndex];
}
