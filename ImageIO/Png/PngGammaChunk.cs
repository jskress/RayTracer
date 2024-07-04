namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents the ancillary gamma chunk in a PNG file.
/// </summary>
public class PngGammaChunk : PngChunk
{
    public PngGammaChunk() : base(ChunkTypes.GammaChunk) {}

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.  We have
    /// none, so we don't do anything.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected override void WriteData(Stream stream)
    {
        int value = (int) Math.Round(1 / ProgramOptions.Instance.Gamma * 100_000);

        ImageFileIo.WriteInt(stream, value, 4);
    }

    /// <summary>
    /// This method is used to deserialize our specific data from the given stream.  We
    /// don't use this chunk on the read side, so we don't do anything.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected override void ReadData(PngChunkReader reader, byte[] data, Stream stream)
    {
        // No-op.
    }
}
