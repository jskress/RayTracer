using RayTracer.General;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents an unsupported chunk type.  We only have this to be able to
/// properly read past chunks we don't know or care about (which is pretty much all
/// ancillary types).
/// </summary>
public class UnknownPngChunk : PngChunk
{
    internal UnknownPngChunk(RenderContext context, string type) : base(context, type) {}

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.  We
    /// throw a "not implemented" exception because we will never emit chunks of an
    /// unknown type.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected override void WriteData(Stream stream)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// This method is used to deserialize our specific data from the given stream.  We
    /// don't do anything because we wouldn't know what to do with the data anyway,
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected override void ReadData(PngChunkReader reader, byte[] data, Stream stream)
    {
        // No-op.
    }
}
