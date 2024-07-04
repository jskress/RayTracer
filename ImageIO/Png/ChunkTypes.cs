namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class defines the known PNG chunk types that we need to deal with.
/// </summary>
internal static class ChunkTypes
{
    // Critical chunk types.
    internal const string HeaderChunk = "IHDR";
    internal const string PaletteChunk = "PLTE";
    internal const string ImageDataChunk = "IDAT";
    internal const string EndChunk = "IEND";

    // Ancillary chunk types.
    internal const string TransparencyChunk = "tRNS";
    internal const string GammaChunk = "gAMA";
    internal const string ChromaticitiesChunk = "cHRM";
    internal const string StandardRgbChunk = "sRGB";
    internal const string EmbeddedIccProfileChunk = "iCCP";
    internal const string TextChunk = "tEXt";
    internal const string CompressedTextChunk = "zTXt";
    internal const string InternationalTextChunk = "iTXt";
    internal const string BackgroundChunk = "bKGD";
    internal const string PhysicalPixelChunk = "pHYs";
    internal const string SignificantBitsChunk = "sBIT";
    internal const string SuggestedPaletteChunk = "sPLT";
    internal const string PaletteHistogramChunk = "hIST";
    internal const string LastModifiedTimeChunk = "tIME";
}
