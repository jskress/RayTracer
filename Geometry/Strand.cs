namespace RayTracer.Geometry;

/// <summary>
/// This class represents a single strand: a fiber of a given width running smoothly through a series of
/// points -- a whisker, an antenna, a hair, a thread.
/// <para>
/// It is the one-strand case of what <c>hair</c> grows by the thousand, and is hit and shaded the same
/// way: as a flat ribbon that always faces the ray, shaded as though it were round.  So unlike a
/// <see cref="Ribbon"/>, which keeps one face and can be seen edge-on, a strand looks the same from every
/// side; and unlike a <see cref="Tube"/>, which really is round, it costs no more than a sheet.
/// </para>
/// </summary>
public class Strand : StrandSurface
{
    /// <summary>
    /// This property holds the points the strand runs through, root first, each with its width there.
    /// </summary>
    public List<RibbonControlPoint> Points { get; } = [];

    /// <summary>
    /// This method is called once prior to rendering to lay the strand through its points.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Points.Count < 2)
            throw new Exception("A strand needs at least two points to run between.");

        StrandSet strands = new ();

        strands.Add(
            Points.Select(point => point.Position).ToList(),
            Points.Select(point => point.Width).ToList());
        strands.Seal();

        Strands = strands;
    }
}
