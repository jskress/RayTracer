using RayTracer.General;

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

    /// <summary>
    /// This property holds the current rendering context.
    /// </summary>
    protected readonly RenderContext Context;

    protected PngChunk(RenderContext context, string type)
    {
        Context = context;
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

        Dump(data.Length);
    }

    /// <summary>
    /// This method returns the chunk's data as a byte array.  By default, we create a
    /// memory stream and delegate to the <see cref="WriteData"/> method.  If the chunk
    /// can provide the byte array, it may override this method.
    /// </summary>
    /// <returns>The array of bytes that represents this chunk's payload.</returns>
    protected virtual byte[] GetData()
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
        Dump(data.Length);
    }

    /// <summary>
    /// This method must be provided by subclasses to deserialize their specific data from
    /// the given stream.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected abstract void ReadData(PngChunkReader reader, byte[] data, Stream stream);

    /// <summary>
    /// This method is used to print out details about this chunk.  Output level must be
    /// higher than normal to see anything.
    /// </summary>
    /// <param name="length">The length of the raw data.</param>
    private void Dump(int length)
    {
        if (Terminal.OutputLevel < OutputLevel.Chatty)
            return;

        string name = GetType().Name;

        if (name.StartsWith("Png"))
            name = name[3..];

        Terminal.Out($"--> {Type}: {name} ({length})", OutputLevel.Chatty);

        if (Terminal.OutputLevel > OutputLevel.Chatty)
            DumpDetails();
    }

    /// <summary>
    /// This may be overridden by subclasses that know out to write out details about
    /// themselves.  <see cref="Terminal"/>'s <c>Out()</c> method must be called with the
    /// <c>Verbose</c> level.
    /// </summary>
    protected virtual void DumpDetails()
    {
        // No-op.
    }
}
