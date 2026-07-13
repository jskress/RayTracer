using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class represents a 4-part tuple of numbers
/// </summary>
public class NumberTuple : IEquatable<NumberTuple>
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public double W { get; protected set; }

    public NumberTuple(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>
    /// This method returns whether the given tuple matches this one.  This will be true
    /// if both tuples are of the same class and all members are equal within a small
    /// tolerance.
    /// </summary>
    /// <param name="other">The tuple to compare to.</param>
    /// <returns><c>true</c>, if the two tuples match, or <c>false</c>, if not.</returns>
    public bool Matches(NumberTuple other)
    {
        return GetType() == other.GetType() &&
               X.Near(other.X) && Y.Near(other.Y) &&
               Z.Near(other.Z) && W.Near(other.W);
    }

    /// <summary>
    /// This method produces a string representation for the tuple.  It is intended for use
    /// in debugging so is very simplistic.
    /// </summary>
    /// <returns>A descriptive string that represents this color.</returns>
    public override string ToString()
    {
        return $"[{X}, {Y}, {Z}, {W}]";
    }

    /// <summary>
    /// This method tests whether the given tuple is the same as this one.
    /// </summary>
    /// <param name="other">The tuple to compare to.</param>
    /// <returns><c>true</c>, if the tuples have the same coordinates, or<c>false</c>,
    /// if not.</returns>
    public bool Equals(NumberTuple other)
    {
        return other is not null && (ReferenceEquals(this, other) ||
               (X.Equals(other.X) && Y.Equals(other.Y) &&
                Z.Equals(other.Z) && W.Equals(other.W)));
    }

    /// <summary>
    /// This method tests whether the given object is the same as this one.
    /// </summary>
    /// <param name="other">The object to compare to.</param>
    /// <returns><c>true</c>, if the given object it s a tuple, and it has the same coordinates
    /// as this one, or<c>false</c>, if not.</returns>
    public override bool Equals(object other)
    {
        return other is not null && (ReferenceEquals(this, other) ||
               (other.GetType() == GetType() && Equals((NumberTuple) other)));
    }

    /// <summary>
    /// This method returns the hash code for this object.
    /// </summary>
    /// <returns>This object's hash code.</returns>
    public override int GetHashCode()
    {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return HashCode.Combine(X, Y, Z, W);
    }
}
