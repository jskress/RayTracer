using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Terms;

/// <summary>
/// This class declares the DSL's mathematical functions.  Each is a plain static method carrying
/// the name a scene calls it by; <see cref="FunctionCatalog"/> gathers them from here.
/// <para>
/// The methods are thin wrappers over the math they perform rather than the underlying methods
/// themselves.  That is deliberate: it gives the catalog a method whose shape we control (reflecting
/// over an overload set such as <see cref="Math"/>'s own would mean choosing among forms the DSL has
/// no use for), it keeps the DSL's name for a function separate from the implementation's, and it
/// leaves each function a place of its own to hang the derivative and interval rules that compiled
/// field functions will want.
/// </para>
/// <para>
/// Scalars and vectors only, for now: the DSL's operators do arithmetic on colors and matrices, but
/// there is no use for a square root of a color yet, and holding off costs nothing -- adding one
/// later is another method here and nothing else.  The vector functions work in three components,
/// leaving W at zero, as the rest of the geometry does.
/// </para>
/// <para>
/// <b>The angles here are radians</b>, whichever way a scene has set <c>angles are</c> -- that
/// setting turns the numbers a scene writes in its <i>clauses</i>, and degrees are its default, so
/// <c>sin(90)</c> is emphatically not 1.  Write <c>sin(π / 2)</c>, or <c>sin(90°)</c> once the
/// postfix angle operators are in.  The alternative would be a function whose meaning depends on a
/// setting made somewhere else in the file, which is a poor bargain in a scene and an untenable one
/// in a field function that has to compile to arithmetic and nothing else.  POV-Ray draws the line
/// in the same place.
/// </para>
/// </summary>
public static class MathFunctions
{
    /// <summary>
    /// This method returns the square root of a number.
    /// </summary>
    /// <param name="value">The number to take the square root of.</param>
    /// <returns>The square root of the number.</returns>
    [Function("sqrt")]
    public static double SquareRoot(double value)
    {
        return Math.Sqrt(value);
    }

    /// <summary>
    /// This method returns the cube root of a number.  This is the one the <c>∛</c> operator will
    /// stand for, and unlike a power of one third it is defined for negative numbers.
    /// </summary>
    /// <param name="value">The number to take the cube root of.</param>
    /// <returns>The cube root of the number.</returns>
    [Function("cbrt")]
    public static double CubeRoot(double value)
    {
        return Math.Cbrt(value);
    }

    /// <summary>
    /// This method returns a number raised to a power.
    /// </summary>
    /// <param name="value">The number to raise.</param>
    /// <param name="exponent">The power to raise it to.</param>
    /// <returns>The number raised to the power.</returns>
    [Function("pow")]
    public static double Power(double value, double exponent)
    {
        return Math.Pow(value, exponent);
    }

    /// <summary>
    /// This method returns e raised to a power.
    /// </summary>
    /// <param name="value">The power to raise e to.</param>
    /// <returns>e raised to the power.</returns>
    [Function("exp")]
    public static double Exponential(double value)
    {
        return Math.Exp(value);
    }

    /// <summary>
    /// This method returns the natural logarithm of a number.
    /// </summary>
    /// <param name="value">The number to take the logarithm of.</param>
    /// <returns>The natural logarithm of the number.</returns>
    [Function("log")]
    public static double Logarithm(double value)
    {
        return Math.Log(value);
    }

    /// <summary>
    /// This method returns the base 10 logarithm of a number.
    /// </summary>
    /// <param name="value">The number to take the logarithm of.</param>
    /// <returns>The base 10 logarithm of the number.</returns>
    [Function("log10")]
    public static double Logarithm10(double value)
    {
        return Math.Log10(value);
    }

    /// <summary>
    /// This method returns the absolute value of a number.
    /// </summary>
    /// <param name="value">The number to take the absolute value of.</param>
    /// <returns>The absolute value of the number.</returns>
    [Function("abs")]
    public static double AbsoluteValue(double value)
    {
        return Math.Abs(value);
    }

    /// <summary>
    /// This method returns a vector whose components are the absolute values of the given one's.
    /// </summary>
    /// <param name="vector">The vector to take the absolute values of.</param>
    /// <returns>The vector of absolute values.</returns>
    [Function("abs")]
    public static Vector AbsoluteValue(Vector vector)
    {
        return new Vector(Math.Abs(vector.X), Math.Abs(vector.Y), Math.Abs(vector.Z));
    }

    /// <summary>
    /// This method returns -1, 0 or 1, according to the sign of a number.
    /// </summary>
    /// <param name="value">The number to take the sign of.</param>
    /// <returns>The sign of the number.</returns>
    [Function("sign")]
    public static double Sign(double value)
    {
        return Math.Sign(value);
    }

    /// <summary>
    /// This method returns the largest whole number no greater than the given one.
    /// </summary>
    /// <param name="value">The number to round down.</param>
    /// <returns>The number, rounded down.</returns>
    [Function("floor")]
    public static double Floor(double value)
    {
        return Math.Floor(value);
    }

    /// <summary>
    /// This method returns the smallest whole number no less than the given one.
    /// </summary>
    /// <param name="value">The number to round up.</param>
    /// <returns>The number, rounded up.</returns>
    [Function("ceil")]
    public static double Ceiling(double value)
    {
        return Math.Ceiling(value);
    }

    /// <summary>
    /// This method returns the given number rounded to the nearest whole one, with a half going to
    /// the nearer even number, as arithmetic rounding does.
    /// </summary>
    /// <param name="value">The number to round.</param>
    /// <returns>The number, rounded.</returns>
    [Function("round")]
    public static double Round(double value)
    {
        return Math.Round(value);
    }

    /// <summary>
    /// This method returns the given number with any fraction discarded, which for a negative
    /// number means rounding toward zero rather than down.
    /// </summary>
    /// <param name="value">The number to truncate.</param>
    /// <returns>The whole part of the number.</returns>
    [Function("trunc")]
    public static double Truncate(double value)
    {
        return Math.Truncate(value);
    }

    /// <summary>
    /// This method returns what is left of a number after taking out whole multiples of a divisor.
    /// <para>
    /// This is not the same as the <c>%</c> operator, and the difference is the whole reason it is
    /// here: <c>%</c> takes its sign from the number being divided, so <c>-1 % 4</c> is -1, while
    /// this counts down from the divisor, making <c>mod(-1, 4)</c> 3.  That is what tiles: a field
    /// that repeats every so far along an axis wants the same pattern to either side of the origin,
    /// which the operator's sign flip would mirror instead.
    /// </para>
    /// </summary>
    /// <param name="value">The number to divide.</param>
    /// <param name="divisor">The divisor to take multiples of.</param>
    /// <returns>The remainder, with the sign of the divisor.</returns>
    [Function("mod")]
    public static double Modulo(double value, double divisor)
    {
        return value - divisor * Math.Floor(value / divisor);
    }

    /// <summary>
    /// This method returns the smaller of two numbers.
    /// </summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns>The smaller of the two numbers.</returns>
    [Function("min")]
    public static double Minimum(double left, double right)
    {
        return Math.Min(left, right);
    }

    /// <summary>
    /// This method returns the vector of the smaller of each pair of components.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The component-wise smaller of the two vectors.</returns>
    [Function("min")]
    public static Vector Minimum(Vector left, Vector right)
    {
        return new Vector(
            Math.Min(left.X, right.X), Math.Min(left.Y, right.Y), Math.Min(left.Z, right.Z));
    }

    /// <summary>
    /// This method returns the larger of two numbers.
    /// </summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns>The larger of the two numbers.</returns>
    [Function("max")]
    public static double Maximum(double left, double right)
    {
        return Math.Max(left, right);
    }

    /// <summary>
    /// This method returns the vector of the larger of each pair of components.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The component-wise larger of the two vectors.</returns>
    [Function("max")]
    public static Vector Maximum(Vector left, Vector right)
    {
        return new Vector(
            Math.Max(left.X, right.X), Math.Max(left.Y, right.Y), Math.Max(left.Z, right.Z));
    }

    /// <summary>
    /// This method returns the smallest of a vector's three components.
    /// </summary>
    /// <param name="vector">The vector to take the smallest component of.</param>
    /// <returns>The vector's smallest component.</returns>
    [Function("min")]
    public static double Minimum(Vector vector)
    {
        return Math.Min(vector.X, Math.Min(vector.Y, vector.Z));
    }

    /// <summary>
    /// This method returns the largest of a vector's three components.
    /// </summary>
    /// <param name="vector">The vector to take the largest component of.</param>
    /// <returns>The vector's largest component.</returns>
    [Function("max")]
    public static double Maximum(Vector vector)
    {
        return Math.Max(vector.X, Math.Max(vector.Y, vector.Z));
    }

    /// <summary>
    /// This method returns a number held to a range.
    /// </summary>
    /// <param name="value">The number to hold.</param>
    /// <param name="low">The lowest the number may be.</param>
    /// <param name="high">The highest the number may be.</param>
    /// <returns>The number, held to the range.</returns>
    [Function("clamp")]
    public static double Clamp(double value, double low, double high)
    {
        return Math.Clamp(value, low, high);
    }

    /// <summary>
    /// This method returns a vector whose components are each held to their own range.
    /// </summary>
    /// <param name="vector">The vector to hold.</param>
    /// <param name="low">The lowest each component may be.</param>
    /// <param name="high">The highest each component may be.</param>
    /// <returns>The vector, held to the range.</returns>
    [Function("clamp")]
    public static Vector Clamp(Vector vector, Vector low, Vector high)
    {
        return new Vector(
            Math.Clamp(vector.X, low.X, high.X),
            Math.Clamp(vector.Y, low.Y, high.Y),
            Math.Clamp(vector.Z, low.Z, high.Z));
    }

    /// <summary>
    /// This method returns the number a given fraction of the way from one number to another.  A
    /// fraction outside 0 to 1 carries on past the ends rather than stopping at them.
    /// </summary>
    /// <param name="from">The number at a fraction of 0.</param>
    /// <param name="to">The number at a fraction of 1.</param>
    /// <param name="fraction">How far along to go.</param>
    /// <returns>The number that far along.</returns>
    [Function("lerp")]
    public static double Lerp(double from, double to, double fraction)
    {
        return from + (to - from) * fraction;
    }

    /// <summary>
    /// This method returns the vector a given fraction of the way from one vector to another.
    /// </summary>
    /// <param name="from">The vector at a fraction of 0.</param>
    /// <param name="to">The vector at a fraction of 1.</param>
    /// <param name="fraction">How far along to go.</param>
    /// <returns>The vector that far along.</returns>
    [Function("lerp")]
    public static Vector Lerp(Vector from, Vector to, double fraction)
    {
        return from + (to - from) * fraction;
    }

    /// <summary>
    /// This method returns a smooth climb from 0 to 1 as a number crosses from one edge to the
    /// other, flat at both ends and steepest in the middle.  Below the first edge it is 0 and above
    /// the second it is 1.
    /// </summary>
    /// <param name="from">The edge at which the climb begins.</param>
    /// <param name="to">The edge at which it finishes.</param>
    /// <param name="value">The number to place between them.</param>
    /// <returns>How far up the climb the number falls.</returns>
    [Function("smoothstep")]
    public static double SmoothStep(double from, double to, double value)
    {
        // With no distance between the edges there is no climb to be part way up, so the answer can
        // only be one end or the other; the division below would make it neither.
        if (from.Near(to))
            return value < from ? 0 : 1;

        double fraction = Math.Clamp((value - from) / (to - from), 0, 1);

        return fraction * fraction * (3 - 2 * fraction);
    }

    /// <summary>
    /// This method returns the sine of an angle in radians.
    /// </summary>
    /// <param name="angle">The angle, in radians.</param>
    /// <returns>The sine of the angle.</returns>
    [Function("sin")]
    public static double Sine(double angle)
    {
        return Math.Sin(angle);
    }

    /// <summary>
    /// This method returns the cosine of an angle in radians.
    /// </summary>
    /// <param name="angle">The angle, in radians.</param>
    /// <returns>The cosine of the angle.</returns>
    [Function("cos")]
    public static double Cosine(double angle)
    {
        return Math.Cos(angle);
    }

    /// <summary>
    /// This method returns the tangent of an angle in radians.
    /// </summary>
    /// <param name="angle">The angle, in radians.</param>
    /// <returns>The tangent of the angle.</returns>
    [Function("tan")]
    public static double Tangent(double angle)
    {
        return Math.Tan(angle);
    }

    /// <summary>
    /// This method returns the angle, in radians, whose sine is the given number.
    /// </summary>
    /// <param name="value">The sine to take the angle of.</param>
    /// <returns>The angle, in radians.</returns>
    [Function("asin")]
    public static double ArcSine(double value)
    {
        return Math.Asin(value);
    }

    /// <summary>
    /// This method returns the angle, in radians, whose cosine is the given number.
    /// </summary>
    /// <param name="value">The cosine to take the angle of.</param>
    /// <returns>The angle, in radians.</returns>
    [Function("acos")]
    public static double ArcCosine(double value)
    {
        return Math.Acos(value);
    }

    /// <summary>
    /// This method returns the angle, in radians, whose tangent is the given number.
    /// </summary>
    /// <param name="value">The tangent to take the angle of.</param>
    /// <returns>The angle, in radians.</returns>
    [Function("atan")]
    public static double ArcTangent(double value)
    {
        return Math.Atan(value);
    }

    /// <summary>
    /// This method returns the angle, in radians, of the direction from the origin to a point, taking
    /// the quadrant from the signs of both numbers, which a lone tangent cannot.
    /// </summary>
    /// <param name="y">How far along Y the direction goes.</param>
    /// <param name="x">How far along X the direction goes.</param>
    /// <returns>The angle, in radians, between -π and π.</returns>
    [Function("atan2")]
    public static double ArcTangent(double y, double x)
    {
        return Math.Atan2(y, x);
    }

    /// <summary>
    /// This method returns the hyperbolic sine of a number.
    /// </summary>
    /// <param name="value">The number to take the hyperbolic sine of.</param>
    /// <returns>The hyperbolic sine of the number.</returns>
    [Function("sinh")]
    public static double HyperbolicSine(double value)
    {
        return Math.Sinh(value);
    }

    /// <summary>
    /// This method returns the hyperbolic cosine of a number.
    /// </summary>
    /// <param name="value">The number to take the hyperbolic cosine of.</param>
    /// <returns>The hyperbolic cosine of the number.</returns>
    [Function("cosh")]
    public static double HyperbolicCosine(double value)
    {
        return Math.Cosh(value);
    }

    /// <summary>
    /// This method returns the hyperbolic tangent of a number.
    /// </summary>
    /// <param name="value">The number to take the hyperbolic tangent of.</param>
    /// <returns>The hyperbolic tangent of the number.</returns>
    [Function("tanh")]
    public static double HyperbolicTangent(double value)
    {
        return Math.Tanh(value);
    }

    /// <summary>
    /// This method returns an angle given in radians as degrees.  It is the conversion worth having
    /// as a function: an angle arrived at by arithmetic is in radians, while a clause that turns
    /// something reads degrees, so this is what carries the one to the other.
    /// <para>
    /// There is deliberately no function going the other way.  Entering an angle in degrees is what
    /// the postfix <c>degrees</c> and <c>°</c> operators are for -- <c>sin(90°)</c> -- and one
    /// conversion spelled two ways, with the same word meaning opposite things depending on which
    /// side of its value it sat, would be a poor trade for the saving.
    /// </para>
    /// </summary>
    /// <param name="radians">The angle, in radians.</param>
    /// <returns>The angle, in degrees.</returns>
    [Function("toDegrees")]
    public static double ToDegrees(double radians)
    {
        return radians.ToDegrees();
    }

    /// <summary>
    /// This method returns the length of a vector.
    /// </summary>
    /// <param name="vector">The vector to measure.</param>
    /// <returns>The length of the vector.</returns>
    [Function("length")]
    [Function("magnitude")]
    public static double Length(Vector vector)
    {
        return vector.Magnitude;
    }

    /// <summary>
    /// This method returns the dot product of two vectors.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The dot product of the two vectors.</returns>
    [Function("dot")]
    public static double Dot(Vector left, Vector right)
    {
        return left.Dot(right);
    }

    /// <summary>
    /// This method returns the cross product of two vectors.
    /// </summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The cross product of the two vectors.</returns>
    [Function("cross")]
    public static Vector Cross(Vector left, Vector right)
    {
        return left.Cross(right);
    }

    /// <summary>
    /// This method returns a vector of length 1 pointing the same way as the given one.
    /// </summary>
    /// <param name="vector">The vector to normalize.</param>
    /// <returns>The vector, normalized.</returns>
    [Function("normalize")]
    public static Vector Normalize(Vector vector)
    {
        return vector.Unit;
    }

    /// <summary>
    /// This method returns how far apart two points are.
    /// </summary>
    /// <param name="from">The first point.</param>
    /// <param name="to">The second point.</param>
    /// <returns>The distance between the two points.</returns>
    [Function("distance")]
    public static double Distance(Point from, Point to)
    {
        return (from - to).Magnitude;
    }

    /// <summary>
    /// This method returns the noise at a point: a smooth, repeatable value between 0 and 1,
    /// averaging about a half, which is the same field every pattern in the library draws on and the
    /// same contract POV-Ray's own <c>Noise()</c> honors.  The same point always gives the same
    /// value, so a scene renders the same way twice.
    /// <para>
    /// One layer of it, deliberately.  Layered noise -- the sum of octaves that gives rock its grain
    /// and bark its ridges -- is this written a few times over, each finer and fainter than the
    /// last, and a scene can now say that for itself: <c>noise(p) + noise(p * 2) / 2 +
    /// noise(p * 4) / 4</c>.  Wiring the octave count in here would fix the shape of that sum for
    /// everyone, and the point of having functions at all is that it need not be fixed.
    /// </para>
    /// </summary>
    /// <param name="point">The point to take the noise at.</param>
    /// <returns>The noise at that point, between 0 and 1.</returns>
    [Function("noise")]
    public static double Noise(Point point)
    {
        return NoiseGenerator.ForSeed().Noise(point);
    }
}
