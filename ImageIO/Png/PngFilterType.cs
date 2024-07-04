namespace RayTracer.ImageIO.Png;

/// <summary>
/// This enum notes the known scanline filter types supported in a PNG image file.
/// </summary>
internal enum PngFilterType : byte
{
    None = 0,
    SubtractLeft = 1,
    SubtractUp = 2,
    Average = 3,
    Paeth = 4
}

/// <summary>
/// This class provides extension methods for our filter type enum.
/// </summary>
internal static class PngFilterTypeExtensions
{
    /// <summary>
    /// This method is used to apply a filter to a scan line of byte data.
    /// </summary>
    /// <param name="filterType">The type of filter to apply.</param>
    /// <param name="current">The current scan line of pixel data to apply the filter to.</param>
    /// <param name="previous">The previous scan line of pixel data.</param>
    /// <param name="filtered">The array to write filtered data to.</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel.</param>
    internal static void ApplyFilter(
        this PngFilterType filterType, byte[] current, byte[] previous, byte[] filtered,
        int bytesPerPixel)
    {
        if (filterType == PngFilterType.None)
            Buffer.BlockCopy(current, 0, filtered, 0, current.Length);
        else
        {
            for (int x = 0; x < current.Length; x++)
                Apply(filterType, current, previous, filtered, bytesPerPixel, x);
        }
    }

    /// <summary>
    /// This method is used to undo a filter to get a scan line of byte data.
    /// </summary>
    /// <param name="filterType">The type of filter to undo.</param>
    /// <param name="filtered">The array to read filtered data from.</param>
    /// <param name="current">The current scan line of pixel data to write the unfiltered data to.</param>
    /// <param name="previous">The previous scan line of pixel data.</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel.</param>
    internal static void UndoFilter(
        this PngFilterType filterType, byte[] filtered, byte[] current, byte[] previous,
        int bytesPerPixel)
    {
        if (filterType == PngFilterType.None)
            Buffer.BlockCopy(filtered, 0, current, 0, current.Length);
        else
        {
            for (int x = 0; x < current.Length; x++)
                Undo(filterType, filtered, current, previous, bytesPerPixel, x);
        }
    }

    /// <summary>
    /// This method is used to apply the given filter to the byte at offset <c>x</c>.
    /// </summary>
    /// <param name="filterType">The type of filter to apply.</param>
    /// <param name="current">The current scan line of pixel data to apply the filter to.</param>
    /// <param name="previous">The previous scan line of pixel data.</param>
    /// <param name="filtered">The array to write filtered data to.</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel.</param>
    /// <param name="x">The index of the byte to set.</param>
    private static void Apply(
        PngFilterType filterType, byte[] current, byte[] previous, byte[] filtered,
        int bytesPerPixel, int x)
    {
        int value = filterType switch
        {
            PngFilterType.SubtractLeft =>
                current[x] - GetByte(current, x - bytesPerPixel),
            PngFilterType.SubtractUp =>
                current[x] - previous[x],
            PngFilterType.Average =>
                current[x] - (GetByte(current, x - bytesPerPixel) + previous[x]) / 2,
            PngFilterType.Paeth =>
                current[x] - PaethPredictor(
                    GetByte(current, x - bytesPerPixel),
                    previous[x],
                    GetByte(previous, x - bytesPerPixel)),
            PngFilterType.None => 0, // this won't happen
            _ => throw new Exception("Internal error: unknown filter type.")
        };

        filtered[x] = (byte) (value & 0x000000FF);
    }

    /// <summary>
    /// This method is used to undo the given filter on the byte at offset <c>x</c>.
    /// </summary>
    /// <param name="filterType">The type of filter to undo.</param>
    /// <param name="filtered">The array to write filtered data to.</param>
    /// <param name="current">The current scan line of pixel data to apply the filter to.</param>
    /// <param name="previous">The previous scan line of pixel data.</param>
    /// <param name="bytesPerPixel">The number of bytes per pixel.</param>
    /// <param name="x">The index of the byte to set.</param>
    private static void Undo(
        PngFilterType filterType, byte[] filtered, byte[] current, byte[] previous,
        int bytesPerPixel, int x)
    {
        int value = filterType switch
        {
            PngFilterType.SubtractLeft =>
                filtered[x] + GetByte(current, x - bytesPerPixel),
            PngFilterType.SubtractUp =>
                filtered[x] + previous[x],
            PngFilterType.Average =>
                filtered[x] + (GetByte(current, x - bytesPerPixel) + previous[x]) / 2,
            PngFilterType.Paeth =>
                filtered[x] + PaethPredictor(
                    GetByte(current, x - bytesPerPixel),
                    previous[x],
                    GetByte(previous, x - bytesPerPixel)),
            PngFilterType.None => 0, // this won't happen
            _ => throw new Exception("Internal error: unknown filter type.")
        };

        filtered[x] = (byte) (value & 0x000000FF);
    }

    /// <summary>
    /// This method is used to get a byte from an array.  If the index is less than 0,
    /// then 0 is returned.
    /// </summary>
    /// <param name="bytes">The array to read from.</param>
    /// <param name="index">The index of the desired byte.</param>
    /// <returns>the requested byte or 0.</returns>
    private static byte GetByte(byte[] bytes, int index)
    {
        return index < 0 ? (byte) 0 : bytes[index];
    }

    /// <summary>
    /// This method provides the implementation of the Paeth algorithm.
    /// </summary>
    /// <param name="left">The value to our left.</param>
    /// <param name="above">The value above us.</param>
    /// <param name="upperLeft">the value above and left of us.</param>
    /// <returns>The value to use for filtering.</returns>
    private static int PaethPredictor(byte left, byte above, byte upperLeft)
    {
        int estimate = left + above - upperLeft;
        int distanceLeft = Math.Abs(estimate - left);
        int distanceUp = Math.Abs(estimate - above);
        int distanceUpLeft = Math.Abs(estimate - upperLeft);

        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
            return distanceLeft;

        return distanceUp <= distanceUpLeft ? distanceUp : distanceUpLeft;
    }
}
