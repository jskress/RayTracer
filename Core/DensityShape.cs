using RayTracer.Basics;
using RayTracer.Patterns;

namespace RayTracer.Core;

/// <summary>
/// This class shapes a medium's density with one of the pattern library's patterns, so that how much
/// stuff there is from place to place may be written the same way a marble's veins or a granite's
/// grain is.
/// <para>
/// It is the third thing the pattern library has been pointed at, after coloring a surface and
/// roughening one, and it arrives for the same reason both of those did: a pattern says how far
/// through its range a point lies, and what that number is taken to <i>mean</i> is the caller's
/// business.  A pigment reads it as a place in a color map, a normal reads it as a slope, and this
/// reads it as how much of the medium is there.  Everything the patterns already offer -- turbulence,
/// the waves, the frequency and phase -- therefore comes along for nothing.
/// </para>
/// <para>
/// The transform is what makes it usable rather than merely possible.  A pattern is written at the
/// scale of the space it sits in, and a cloud is a couple of units across, so a granite left at its
/// own footing gives one blob and nothing else.  The transform gives the pattern its own footing
/// inside the container, exactly as a pigment's does.
/// </para>
/// </summary>
public class DensityShape
{
    /// <summary>
    /// This property holds the pattern the density is shaped by.
    /// </summary>
    public Pattern Pattern { get; set; }

    /// <summary>
    /// This property holds the transform applied to a point before the pattern is asked about it,
    /// which is how a scene scales and turns the shaping without disturbing what contains it.
    /// </summary>
    public Matrix Transform
    {
        get => field;
        set
        {
            field = value;

            _inverseTransform = new Lazy<Matrix>(() => Transform.Invert());
        }
    } = Matrix.Identity;

    private Lazy<Matrix> _inverseTransform = new (() => Matrix.Identity);

    /// <summary>
    /// This method reports how much of the medium there is at the given point, as a fraction that
    /// the medium's own density is then multiplied by.
    /// <para>
    /// A pattern built for a color map hands back a whole number naming which pigment to use rather
    /// than a fraction, and those are spread back across the range here.  A checker gives nothing or
    /// all, which is a medium in alternating blocks; a hexagon gives nothing, a half, or all.  That
    /// is the reading that keeps every pattern in the library usable here and keeps all of them
    /// inside the same range, rather than a six-way pattern quietly meaning six times the density.
    /// </para>
    /// </summary>
    /// <param name="point">Where to ask, in the space of the surface the medium fills.</param>
    /// <returns>How much of the medium is there, from nothing up to all of it.</returns>
    public double ValueAt(Point point)
    {
        double value = Pattern.ValueFor(_inverseTransform.Value * point);
        int bands = Pattern.DiscretePigmentsNeeded;

        return bands > 1 ? value / (bands - 1) : value;
    }

    /// <summary>
    /// This method reports whether this shaping is the same as another.
    /// </summary>
    /// <param name="other">The shaping to compare to.</param>
    /// <returns><c>true</c>, if the two are the same, or <c>false</c>, if not.</returns>
    public bool Matches(DensityShape other)
    {
        if (other is null || !Transform.Matches(other.Transform))
            return false;

        return Pattern is null ? other.Pattern is null : Pattern.Matches(other.Pattern);
    }
}
