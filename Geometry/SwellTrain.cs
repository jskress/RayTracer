using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents one train of waves running across a body of water: how tall it is, how far
/// apart its crests are, which way it travels and where in its cycle it starts.
/// <para>
/// A body of water is a handful of these summed.  One alone is too regular to read as water at any
/// size; three or four crossing at angles is what gives a sea its unrepeating look.
/// </para>
/// </summary>
public class SwellTrain
{
    /// <summary>
    /// This property holds how far the water rises above its rest level at a crest, and falls below it
    /// in a trough.
    /// </summary>
    public double Amplitude { get; set; } = 0.5;

    /// <summary>
    /// This property holds the distance from one crest to the next.
    /// </summary>
    public double Wavelength { get; set; } = 10;

    /// <summary>
    /// This property holds the direction the train travels, in the X/Z plane.  It need not be a unit
    /// vector; only where it points matters.
    /// </summary>
    public Vector Direction { get; set; } = new (1, 0, 0);

    /// <summary>
    /// This property holds where in its cycle the train stands at the origin, in radians.  Two bodies
    /// of water given different phases are not the same water twice.
    /// </summary>
    public double Phase { get; set; }

    /// <summary>
    /// This property reports how steep the train is: its amplitude against how tightly its crests are
    /// spaced, as <c>amplitude * 2 * pi / wavelength</c>.
    /// <para>
    /// **This one number decides the shape.**  At nought the train is a plain sine.  As it rises the
    /// crests draw up and narrow while the troughs flatten and broaden, which is what tells water from
    /// a rumpled sheet.  At **one** the crest closes to a cusp -- the wave breaking -- and beyond it
    /// the surface would fold over itself and stop being something a height can be found for.  Real
    /// water breaks at about 0.45, so the whole useful range sits well inside it.
    /// </para>
    /// </summary>
    public double Steepness => Amplitude * WaveNumber;

    /// <summary>
    /// This property reports how many radians of the wave there are per unit of distance.
    /// </summary>
    public double WaveNumber => 2 * Math.PI / Wavelength;
}
