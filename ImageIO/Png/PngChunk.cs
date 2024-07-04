namespace RayTracer.ImageIO.Png;

/// <summary>
/// This is the base class for all PNG chunk types that we need to deal with.
/// </summary>
public abstract class PngChunk
{
    /// <summary>
    /// This property holds the type of the chunk.
    /// </summary>
    internal string Type { get; }

    /// <summary>
    /// This is a convenience property that tells us whether the chunk is a critical one.
    /// </summary>
    public bool IsCritical => char.IsUpper(Type[0]);

    /// <summary>
    /// This is a convenience property that tells us whether the chunk is an ancillary one.
    /// </summary>
    public bool IsAncillary => char.IsLower(Type[0]);

    protected PngChunk(string type)
    {
        Type = type;
    }

    /// <summary>
    /// This method is used to write out this chunk to the given stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    public void Write(Stream stream)
    {
        byte[] data = GetData();
        uint calculatedCrc = new Crc32()
            .Append(Type)
            .Append(data)
            .Value;

        ImageFileIo.WriteInt(stream, data.Length, 4);
        ImageFileIo.WriteText(stream, Type);
        ImageFileIo.WriteBytes(stream, data);
        ImageFileIo.WriteUInt(stream, calculatedCrc, 4);
    }

    /// <summary>
    /// This method must be provided by subclasses to serialize their specific data into a
    /// byte array.
    /// </summary>
    /// <returns>The array of bytes that represents this chunk's payload.</returns>
    private byte[] GetData()
    {
        using MemoryStream stream = new MemoryStream();
        
        WriteData(stream);

        return stream.ToArray();
    }

    /// <summary>
    /// This method must be provided by subclasses to serialize their specific data into
    /// the given stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected abstract void WriteData(Stream stream);

    /// <summary>
    /// This method is used to populate this chunk from the array of raw data provided.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    internal void SetData(PngChunkReader reader, byte[] data)
    {
        using MemoryStream stream = new MemoryStream(data);

        ReadData(reader, data, stream);
    }

    /// <summary>
    /// This method must be provided by subclasses to deserialize their specific data from
    /// the given stream.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected abstract void ReadData(PngChunkReader reader, byte[] data, Stream stream);
}
