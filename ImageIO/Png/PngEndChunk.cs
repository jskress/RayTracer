using RayTracer.General;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents the end chunk in a PNG file.
/// </summary>
public class PngEndChunk : PngChunk
{
    public PngEndChunk(RenderContext context) : base(context, ChunkTypes.EndChunk) {}

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.  We have
    /// none, so we don't do anything.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected override void WriteData(Stream stream)
    {
        // No-op.
    }

    /// <summary>
    /// This method is used to deserialize our specific data from the given stream.  We
    /// have none, so we don't do anything.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected override void ReadData(PngChunkReader reader, byte[] data, Stream stream)
    {
        // No-op.
    }
}
