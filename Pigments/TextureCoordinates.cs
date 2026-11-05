namespace RayTracer.Pigments;

/// <summary>
/// This record holds how a model's texture coordinates are moved and stretched before a picture is read at them, as a
/// material library's `-s` and `-o` say: a coordinate u is read at u x ScaleU + OffsetU, and v alike.
/// </summary>
/// <param name="ScaleU">How much across the picture is stretched by.</param>
/// <param name="ScaleV">How much up it.</param>
/// <param name="OffsetU">How far across it is moved.</param>
/// <param name="OffsetV">How far up it.</param>
public sealed record TextureCoordinates(double ScaleU, double ScaleV, double OffsetU, double OffsetV)
{
    /// <summary>
    /// This method moves and stretches a place in a picture laid by a model's coordinates -- across and down, as
    /// shares of the picture, down running from the top -- and its rates with it.
    /// </summary>
    internal (double S, double T, Basics.Vector Across, Basics.Vector Down) Applied(
        double s, double t, Basics.Vector across, Basics.Vector down)
    {
        // Down is one less the model's up: v = 1 - t, read at v x ScaleV + OffsetV, so down is one less that.
        return (s * ScaleU + OffsetU, 1 - ((1 - t) * ScaleV + OffsetV), across * ScaleU, down * ScaleV);
    }
}
