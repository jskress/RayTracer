namespace RayTracer.Basics;

/// <summary>
/// This class represents a 4-part tuple of numbers
/// </summary>
public class NumberTuple
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public double W { get; protected set; }

    protected NumberTuple(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>
    /// This method returns whether the given tuple matches this one.  This will be true
    /// if both tuples are of the same class and all members are equitable within a small
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
}
