using RayTracer.Basics;
using RayTracer.Extensions;
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
        foreach (Pigment pigment in _pigments)
            pigment.SetSeed(seed);
    }

    /// <summary>
    /// This method is used to resolve a given index number to the proper corresponding color.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="index">The index of the desired color.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, int index)
    {
        (_, Pigment pigment) = _pigments.GetByIndex(index);

        return pigment.GetTransformedColorFor(point);
    }

    /// <summary>
    /// This method is used to resolve a given number to the proper corresponding color.
    /// </summary>
    /// <param name="point">The point to get the color for.</param>
    /// <param name="value">The value to get the color for.</param>
    /// <returns>The appropriate color for the value.</returns>
    public Color GetColorFor(Point point, double value)
    {
        // If we have no pigments, just go with black.
        if (_pigments.IsEmpty)
            return Colors.Black;

        (double start, Pigment firstPigment) = _pigments.GetByValue(value);
        (double end, Pigment secondPigment) = _pigments.GetValueFollowing(firstPigment);
        Color firstColor = firstPigment.GetTransformedColorFor(point);

        // If we're banded or on the last entry, then we have our color.
        if (Banded || double.IsNaN(end))
            return firstColor;

        double fraction = (end - start) * value;
        Color secondColor = secondPigment.GetTransformedColorFor(point);
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
            (double theirBreakValue, Pigment theirPigment) = _pigments.GetByIndex(index);

            if (!ourBreakValue.Near(theirBreakValue) ||
                !ourPigment.Matches(theirPigment))
                return false;
        }

        return true;
    }
}
