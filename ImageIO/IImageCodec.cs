using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This interface defines the contract that a codec must implement to support image file
/// loading and saving.
/// </summary>
public interface IImageCodec
{
    /// <summary>
    /// This method is used to encode the given canvas to the specified stream.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to encode</param>
    void Encode(Canvas canvas, Stream stream);
}
