using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This interface defines the contract that a codec must implement to support image file
/// loading and saving.
/// </summary>
public interface IImageCodec
{
    /// <summary>
    /// This method is used to set the output stream when writing an image
    /// </summary>
    /// <param name="stream"></param>
    void SetStream(Stream stream);

    /// <summary>
    /// This method is used to emit the appropriate header for the given image to the
    /// stream previously set.
    /// </summary>
    /// <param name="image">The image being encoded and written.</param>
    void WriteHeader(Image image);

    /// <summary>
    /// This method is used to emit the appropriate pixel data for the given
    /// image to the stream previously set.
    /// </summary>
    /// <param name="image">The image being encoded and written.</param>
    void WritePixels(Image image);

    /// <summary>
    /// This method is used to emit the appropriate footer for the given image to the
    /// stream previously set.
    /// </summary>
    /// <param name="image">The image being encoded and written.</param>
    void WriteFooter(Image image);
}

