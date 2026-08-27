using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class represents a pigment set that carries a list of pigments, mapped by a number
/// from 0 to 1.  The set may be used as a simple list that returns the color for the nth
/// pigment in the set.  Alternatively, it may take a value and find the interval containing
/// that value and return color for that band or a gradient color for the value, based on
/// where it falls in its interval.
/// </summary>
public class PigmentSet
{
    /// <summary>
    /// This flag notes whether we will produce bands or gradients.
    /// </summary>
    public bool Banded { get; set; }

    /// <summary>
    /// This property reports whether any pigment in this set might let light through.
    /// </summary>
    public bool MayTransmit => _pigments.Any(pigment => pigment.MayTransmit);

    private readonly Spectrum<Pigment> _pigments = new (); 

    /// <summary>
    /// This method is used to add an entry to the pigment map.
    /// </summary>
    /// <param name="pigment">The pigment to start using at the given break value.</param>
    /// <param name="breakValue">The break value that indicates where in the [0. 1] range
    /// that the new color takes effect.</param>
    public void AddEntry(Pigment pigment, double breakValue = 0)
    {
        _pigments.AddEntry(pigment, breakValue);
    }

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public void SetSeed(int seed)
    {
        foreach (Pigment pigment in _pigments.Where(pigment => !pigment.Seed.HasValue))
            pigment.SetSeed(seed);
    }

    /// <summary>
    /// This method gives every pigment in the set its chance to get ready for rendering, which is
    /// where a pigment that reads an image loads it.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surface">The surface the pigment holding this set is set on.</param>
    public void PrepareForRendering(RenderContext context, Surface surface)
    {
        foreach (Pigment pigment in _pigments)
            pigment.RenderingIsAboutToStart(context, surface);
    }

    /// <summary>
    /// This method is used to resolve a given index number to the proper corresponding color.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="index">The index of the desired color.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, int index)
    {
        return GetColorFor(point, index, null);
    }

    /// <summary>
    /// This method is the same, carrying the patch down to whichever pigment is chosen.
    /// <para>
    /// It matters because a pigment in a set may be a pattern in its own right -- a brick whose
    /// brick is granite -- and without this the filtering would stop one level down, leaving the
    /// inner pattern to shimmer inside a wall that had been perfectly settled around it.
    /// </para>
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="index">The index of the desired color.</param>
    /// <param name="footprint">The patch around the point.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, int index, Footprint footprint)
    {
        (_, Pigment pigment) = _pigments.GetByIndex(index);

        return footprint is null || footprint.IsEmpty
            ? pigment.GetTransformedColorFor(point)
            : pigment.GetTransformedColorFor(point, footprint);
    }

    /// <summary>
    /// This method resolves an index that may fall *between* two of the discrete pigments, mixing
    /// them in that proportion.
    /// <para>
    /// A lattice pattern normally answers with a whole number -- brick, or mortar -- and when it
    /// does, this hands back exactly what asking for that index would have handed back, down to the
    /// arithmetic.  It answers with a fraction only when it has been asked about a patch of surface
    /// wide enough to hold some of each, and then the fraction is how much of the patch is the
    /// second one.  Which is the honest color for that patch: a wall too far off to show its courses
    /// is not brick and it is not mortar, it is the two of them mixed in the proportion they cover.
    /// </para>
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="value">The index, which may lie between two pigments.</param>
    /// <returns>The appropriate color.</returns>
    public Color GetBlendedColorFor(Point point, double value)
    {
        return GetBlendedColorFor(point, value, null);
    }

    /// <summary>
    /// This method is the same, carrying the patch down to the pigments it mixes.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="value">The index, which may lie between two pigments.</param>
    /// <param name="footprint">The patch around the point.</param>
    /// <returns>The appropriate color.</returns>
    public Color GetBlendedColorFor(Point point, double value, Footprint footprint)
    {
        int index = (int) value;
        double fraction = value - index;

        // The whole-number case is the one nearly every ray takes, and it must come out bit for bit
        // as it did before any of this: no second lookup, no arithmetic on the color.
        if (fraction <= 0)
            return GetColorFor(point, index, footprint);

        Color firstColor = GetColorFor(point, index, footprint);
        Color secondColor = GetColorFor(point, index + 1, footprint);
        double alpha = firstColor.Alpha + (secondColor.Alpha - firstColor.Alpha) * fraction;

        return (firstColor + (secondColor - firstColor) * fraction).WithAlpha(alpha);
    }

    /// <summary>
    /// This method is used to resolve a given number to the proper corresponding color.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="value">The value to get the color for.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, double value)
    {
        return GetColorFor(point, value, null);
    }

    /// <summary>
    /// This method is the same, carrying the patch down to the pigments it interpolates between.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="value">The value to get the color for.</param>
    /// <param name="footprint">The patch around the point.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, double value, Footprint footprint)
    {
        // If we have no pigments, just go with black.
        if (_pigments.IsEmpty)
            return Colors.Black;

        // Note we track where we are by index rather than by pigment: the same pigment may
        // legitimately appear at several break values, and looking the next one up by pigment
        // instead would find the first of those every time, pairing our break value with one
        // belonging to a different stop entirely.
        int index = _pigments.GetIndexByValue(value);
        (double start, Pigment firstPigment) = _pigments.GetByIndex(index);
        Color firstColor = footprint is null || footprint.IsEmpty
            ? firstPigment.GetTransformedColorFor(point)
            : firstPigment.GetTransformedColorFor(point, footprint);

        // If we're banded or on the last entry, then we have our color.
        if (Banded || index >= _pigments.Count - 1)
            return firstColor;

        (double end, Pigment secondPigment) = _pigments.GetByIndex(index + 1);

        // The break values live in [0, 1), so the value has to be brought into that same
        // interval before it's measured against them.
        double fraction = (Spectrum<Pigment>.Normalize(value) - start) / (end - start);
        Color secondColor = footprint is null || footprint.IsEmpty
            ? secondPigment.GetTransformedColorFor(point)
            : secondPigment.GetTransformedColorFor(point, footprint);
        double alpha = firstColor.Alpha + (secondColor.Alpha - firstColor.Alpha) * fraction;

        return (firstColor + (secondColor - firstColor) * fraction).WithAlpha(alpha);
    }

    /// <summary>
    /// This method returns whether the given pigment set matches this one.
    /// </summary>
    /// <param name="other">The pigment set to compare to.</param>
    /// <returns><c>true</c>, if the two pigment sets match, or <c>false</c>, if not.</returns>
    public bool Matches(PigmentSet other)
    {
        if (Banded != other.Banded || _pigments.Count != other._pigments.Count)
            return false;

        for (int index = 0; index < _pigments.Count; index++)
        {
            (double ourBreakValue, Pigment ourPigment) = _pigments.GetByIndex(index);
            (double theirBreakValue, Pigment theirPigment) = other._pigments.GetByIndex(index);

            if (!ourBreakValue.Near(theirBreakValue) ||
                !ourPigment.Matches(theirPigment))
                return false;
        }

        return true;
    }
}
