namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class handles reading and writing image data by translating between the compressed
/// image data and the PNG data chunks they are stored in.  This is designed to be given
/// to a standard deflate stream as its compressed stream.
/// </summary>
public class PngImageStream : Stream
{
    private const int BufferSize = 16384;

    private PngChunkReader _reader;
    private PngChunkWriter _writer;
    private PngImageDataChunk _imageDataChunk;
    private int _cp;

    private PngImageStream(
        PngChunkReader reader, PngChunkWriter writer, PngImageDataChunk firstChunk)
    {
        _reader = reader;
        _writer = writer;
        _imageDataChunk = firstChunk;
        _cp = 0;
    }

    public PngImageStream(PngChunkReader reader)
        : this(reader, null, reader.GetImageDataChunk()) {}

    public PngImageStream(PngChunkWriter writer)
        : this(null, writer, new PngImageDataChunk
        {
            ImageData = new byte[BufferSize]
        }) {}

    // Reading support

    /// <summary>
    /// This property reports whether the stream can be read from.  This is true if we're
    /// configured for reading.
    /// </summary>
    public override bool CanRead => _reader != null;

    /// <summary>
    /// This method is used to read data from this stream.  We exhaust the current image
    /// chunk and get the next.  This repeats until we fill the buffer or run out of data.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="offset">The place in the buffer to start writing.</param>
    /// <param name="maxCount">The desired number of bytes to transfer.</param>
    /// <returns>The number of bytes we actually transferred.</returns>
    public override int Read(byte[] buffer, int offset, int maxCount)
    {
        int totalBytesRead = 0;

        while (maxCount > 0 && _imageDataChunk != null)
        {
            int count = Math.Min(_imageDataChunk.ImageData.Length - _cp, maxCount);

            Buffer.BlockCopy(_imageDataChunk.ImageData, _cp, buffer, offset, count);

            _cp += count;
            offset += count;
            totalBytesRead += count;
            maxCount -= count;

            // If we've exhausted the current chunk, go get the next one.
            if (_cp >= _imageDataChunk.ImageData.Length)
            {
                _imageDataChunk = _reader.GetImageDataChunk();
                _cp = 0;
            }
        }

        return totalBytesRead;
    }

    // Writing support

    /// <summary>
    /// This property reports whether the stream can be read from.  This is true if we're
    /// configured for writing.
    /// </summary>
    public override bool CanWrite => _writer != null;

    /// <summary>
    /// This method is used to write data to this stream.  We fill up the current image
    /// chunk and create a new one as needed.  This repeats until we have consumed the data
    /// we have been handed.
    /// </summary>
    /// <param name="buffer">The buffer to read from to.</param>
    /// <param name="offset">The place in the buffer to start reading.</param>
    /// <param name="maxCount">The desired number of bytes to transfer.</param>
    public override void Write(byte[] buffer, int offset, int maxCount)
    {
        while (maxCount > 0)
        {
            int count = Math.Min(_imageDataChunk.ImageData.Length - _cp, maxCount);

            Buffer.BlockCopy(buffer, offset, _imageDataChunk.ImageData, _cp, count);

            _cp += count;
            offset += count;
            maxCount -= count;

            // If we've exhausted the current chunk, go get the next one.
            if (_cp >= _imageDataChunk.ImageData.Length)
                Flush();
        }
    }

    /// <summary>
    /// This method is used to flush whatever we've accumulated so far.
    /// </summary>
    public override void Flush()
    {
        if (_cp > 0)
        {
            // If it's full, just write it out.
            if (_cp >= _imageDataChunk.ImageData.Length)
                _writer.WriteImageDataChunk(_imageDataChunk);
            // Otherwise, create a new chunk that carries an appropriately sized array.
            else
            {
                byte[] rest = new byte[_cp];
                
                Buffer.BlockCopy(_imageDataChunk.ImageData, 0, rest, 0, _cp);

                _writer.WriteImageDataChunk(new PngImageDataChunk
                {
                    ImageData = rest
                });
            }

            _cp = 0;
        }
    }

    // Seeking support

    /// <summary>
    /// This property reports whether the read/write point of the stream can be repositioned.
    /// This is always false.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// This property normally indicates the read/write position of the stream.  We don't
    /// support that.
    /// </summary>
    public override long Position
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    /// <summary>
    /// This method normally used to set the current read/write position, but we don't
    /// support doing that.
    /// </summary>
    /// <param name="offset">The amount to move the read/write position.</param>
    /// <param name="origin">How to interpret the offset.</param>
    /// <returns>The new read/write position of the stream.</returns>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

    /// <summary>
    /// This method reports the length of the stream.  We don't support doing this.
    /// </summary>
    public override long Length => throw new NotImplementedException();

    /// <summary>
    /// This would normally be used to set the length for the stream.  We don't support this.
    /// </summary>
    /// <param name="value">The new length of the stream</param>
    public override void SetLength(long value) => throw new NotImplementedException();

    /// <summary>
    /// This method makes sure we're all cleaned up.
    /// </summary>
    /// <param name="disposing">Whether we are being disposed.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (CanRead || CanWrite))
        {
            if (CanWrite && _cp > 0)
                Flush();

            _reader = null;
            _writer = null;
            _imageDataChunk = null;
        }

        base.Dispose(disposing);
    }
}
