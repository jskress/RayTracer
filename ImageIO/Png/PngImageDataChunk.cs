namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents the image data chunk in a PNG file.
/// </summary>
public class PngImageDataChunk : PngChunk
{
    /// <summary>
    /// This property holds the raw, compressed image data specified in the chunk.
    /// </summary>
    public byte[] ImageData { get; set; }

    public PngImageDataChunk() : base(ChunkTypes.ImageDataChunk) {}

    /// <summary>
    /// This method returns the chunk's data as a byte array.  We override it because it's
    /// more efficient to bypass the serialization step.
    /// </summary>
    /// <returns>The array of bytes that represents this chunk's payload.</returns>
    protected override byte[] GetData()
    {
        return ImageData;
    }

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected override void WriteData(Stream stream)
    {
        // No-op; this method will never be called since we override the GetData() method.
    }

    /// <summary>
    /// This method is used to deserialize our specific data from the given stream.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected override void ReadData(PngChunkReader reader, byte[] data, Stream stream)
    {
        ImageData = data;
    }
}
