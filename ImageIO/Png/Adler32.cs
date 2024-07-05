namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class is used to support generating an Alder32 checksum.
/// </summary>
internal class Adler32
{
    private const int AdlerModulus = 65521;

    /// <summary>
    /// This property reports the current checksum value.
    /// </summary>
    internal int Checksum => _sum2 * 65536 + _sum1;

    private int _sum1;
    private int _sum2;

    /// <summary>
    /// This method is used to add the given byte to the sum.
    /// </summary>
    /// <param name="data">The byte to add to the sum.</param>
    internal void Add(byte data)
    {
        _sum1 = (_sum1 + data) % AdlerModulus;
        _sum2 = (_sum1 + _sum2) % AdlerModulus;
    }

    /// <summary>
    /// This method is used to add all the given bytes to our checksum.
    /// </summary>
    /// <param name="bytes">The bytes to add to the sum.</param>
    internal void Add(byte[] bytes)
    {
        foreach (byte data in bytes)
            Add(data);
    }
}
