using RayTracer.Basics;

namespace RayTracer.Fields;

/// <summary>
/// This class holds the range each function a field may call could produce, given ranges for what it
/// is called with.  <see cref="FieldCall"/> asks here when it is bounding itself.
/// <para>
/// A rule must never claim a range narrower than the truth: a marcher skips whatever a range rules
/// out, so a bound that is too tight makes a surface disappear in patches, while one that is too loose
/// only makes the marcher work harder.  Every rule here therefore errs outward, and any function
/// whose range would have to be guessed at gets <see cref="FieldRange.Anywhere"/> instead -- which
/// rules nothing out and so cannot be wrong.
/// </para>
/// <para>
/// These rules and the slopes in <see cref="FieldDerivatives"/> are independent of one another, and a
/// function may well have one and not the other.  <c>smoothstep</c> is the plain example: nothing here
/// knows how to differentiate it, and yet its range is known exactly, since it cannot leave nought and
/// one whatever it is given.
/// </para>
/// </summary>
public static class FieldBounds
{
    private static readonly Dictionary<string, Func<FieldRange[], FieldRange>> Rules = new ()
    {
        // The ones that only ever climb, over the whole of their domain.
        { "exp", u => Climbing(u[0], Math.Exp) },
        { "sinh", u => Climbing(u[0], Math.Sinh) },
        { "tanh", u => Climbing(u[0], Math.Tanh) },
        { "atan", u => Climbing(u[0], Math.Atan) },
        { "cbrt", u => Climbing(u[0], Math.Cbrt) },
        { "toDegrees", u => Climbing(u[0], value => value * 180 / Math.PI) },

        // The steps climb too, in their own way: never downward, so the ends still bound them.
        { "floor", u => Climbing(u[0], Math.Floor) },
        { "ceil", u => Climbing(u[0], Math.Ceiling) },
        { "round", u => Climbing(u[0], Math.Round) },
        { "trunc", u => Climbing(u[0], Math.Truncate) },
        { "sign", u => Climbing(u[0], value => Math.Sign(value)) },

        // The ones that climb only where they are defined at all.  A range reaching outside that is
        // not narrowed to fit: the function would be asked for a number it has none of, and what the
        // field does there is the scene's business rather than something to paper over here.
        { "sqrt", u => u[0].Low < 0 ? FieldRange.Anywhere : Climbing(u[0], Math.Sqrt) },
        { "log", u => u[0].Low <= 0 ? FieldRange.Anywhere : Climbing(u[0], Math.Log) },
        { "log10", u => u[0].Low <= 0 ? FieldRange.Anywhere : Climbing(u[0], Math.Log10) },
        { "asin", u => Within(u[0], -1, 1) ? Climbing(u[0], Math.Asin) : FieldRange.Anywhere },
        { "acos", u => Within(u[0], -1, 1) ? Falling(u[0], Math.Acos) : FieldRange.Anywhere },

        // The ones shaped like a V, whose lowest point is in the middle rather than at an end.
        { "abs", u => Valley(u[0], 0, Math.Abs) },
        { "cosh", u => Valley(u[0], 0, Math.Cosh) },

        // The waves.  Wide enough and they cover everything they can reach; narrower, it depends on
        // whether the range takes in a crest or a trough.
        { "sin", u => Wave(u[0], Math.Sin, Math.PI / 2, 3 * Math.PI / 2) },
        { "cos", u => Wave(u[0], Math.Cos, 0, Math.PI) },

        // A tangent climbs, but only between the places it runs off to infinity.
        { "tan", u => CrossesAPole(u[0]) ? FieldRange.Anywhere : Climbing(u[0], Math.Tan) },

        // These follow their operands one for one.
        { "min", u => new FieldRange(
            Math.Min(u[0].Low, u[1].Low), Math.Min(u[0].High, u[1].High)) },
        { "max", u => new FieldRange(
            Math.Max(u[0].Low, u[1].Low), Math.Max(u[0].High, u[1].High)) },
        { "clamp", u => new FieldRange(
            Math.Clamp(u[0].Low, u[1].Low, u[2].Low), Math.Clamp(u[0].High, u[1].High, u[2].High)) },

        // lerp(a, b, t) = a + (b - a)t, so its range is that arithmetic on ranges.
        { "lerp", u => u[0] + (u[1] - u[0]) * u[2] },

        // Whatever it is given, a smooth step cannot leave nought and one.
        { "smoothstep", _ => new FieldRange(0, 1) },

        { "noise", Noise },

        // An angle is an angle.
        { "atan2", _ => new FieldRange(-Math.PI, Math.PI) },

        // What is left after taking out whole multiples of a fixed divisor lies between nought and
        // that divisor.  A divisor that is itself a range is not worth unpicking.
        { "mod", u => u[1].IsExact && u[1].Low > 0
            ? new FieldRange(0, u[1].Low)
            : FieldRange.Anywhere },

        { "pow", Power }
    };

    /// <summary>
    /// This method returns the range the named function could produce, or "anywhere" if there is no
    /// rule for it.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <param name="arguments">The ranges of what it is being called with.</param>
    /// <returns>The range it could produce.</returns>
    public static FieldRange RangeFor(string name, FieldRange[] arguments)
    {
        if (arguments.Any(argument => argument.IsAnywhere))
            return FieldRange.Anywhere;

        return Rules.TryGetValue(name, out Func<FieldRange[], FieldRange> rule)
            ? rule(arguments)
            : FieldRange.Anywhere;
    }

    /// <summary>
    /// The steepest the noise field is taken to get anywhere, in noise per unit of distance.  Measured
    /// at 1.41 over four hundred thousand points, and set well above that on purpose.
    /// <para>
    /// This is the one rule here arrived at by measurement rather than by reasoning, because a bound
    /// reasoned out from the interpolation is hopelessly loose -- eight corner gradients, each able to
    /// point any way, give something above twenty, which would rule nothing out and defeat the purpose.
    /// The margin is what makes the measurement safe to lean on, and <c>TestTheBoundOfNoiseHolds</c>
    /// leans on it hard.  Being too generous here costs only time; being too mean would lose patches of
    /// a surface, so if in doubt, raise it.
    /// </para>
    /// </summary>
    private const double NoiseSlope = 2.5;

    /// <summary>
    /// The range noise could take over a box: what it is in the middle, give or take as much as it could
    /// possibly change across the box, and never outside the nought to one it is defined to keep to.
    /// <para>
    /// Bounding it by nought and one alone would be perfectly safe and nearly useless.  It would never
    /// tighten as a box shrank, so a marcher narrowing in on a surface would keep every span alive to the
    /// finest step it was allowed rather than ruling any of them out -- which in a scene of a rock came to
    /// two and a half minutes against two seconds.  Sampling the middle once and allowing for the slope
    /// costs one noise value per box and tightens as it should.
    /// </para>
    /// </summary>
    /// <param name="arguments">The ranges of the three coordinates noise is being asked at.</param>
    /// <returns>The range noise could take over that box.</returns>
    private static FieldRange Noise(FieldRange[] arguments)
    {
        double radius = 0.5 * Math.Sqrt(
            arguments[0].Width * arguments[0].Width +
            arguments[1].Width * arguments[1].Width +
            arguments[2].Width * arguments[2].Width);
        double middle = NoiseGenerator.ForSeed().Noise(
            arguments[0].Middle, arguments[1].Middle, arguments[2].Middle);
        double slack = NoiseSlope * radius;

        return new FieldRange(Math.Max(0, middle - slack), Math.Min(1, middle + slack));
    }

    /// <summary>
    /// This method reports whether there is a rule for the named function.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    /// <returns><c>true</c>, if the function's range can be worked out.</returns>
    public static bool HasRuleFor(string name)
    {
        return Rules.ContainsKey(name);
    }

    /// <summary>
    /// A function that never goes down is bounded by what it gives at the two ends.
    /// </summary>
    private static FieldRange Climbing(FieldRange range, Func<double, double> function)
    {
        return FieldRange.Covering(function(range.Low), function(range.High));
    }

    /// <summary>
    /// A function that never goes up is likewise bounded by its ends, the other way about.
    /// </summary>
    private static FieldRange Falling(FieldRange range, Func<double, double> function)
    {
        return FieldRange.Covering(function(range.High), function(range.Low));
    }

    /// <summary>
    /// A function that falls to a lowest point and climbs away again is bounded by its ends, unless
    /// the range takes in that lowest point, in which case that is the bottom of it.
    /// </summary>
    private static FieldRange Valley(FieldRange range, double bottom, Func<double, double> function)
    {
        FieldRange ends = FieldRange.Covering(function(range.Low), function(range.High));

        return range.Contains(bottom)
            ? new FieldRange(function(bottom), ends.High)
            : ends;
    }

    /// <summary>
    /// A wave is bounded by its ends together with any crest or trough the range takes in.  A range
    /// wider than one turn takes in both whatever else is true of it.
    /// </summary>
    private static FieldRange Wave(
        FieldRange range, Func<double, double> function, double crest, double trough)
    {
        if (range.Width >= 2 * Math.PI)
            return new FieldRange(-1, 1);

        FieldRange ends = FieldRange.Covering(function(range.Low), function(range.High));
        double low = TakesIn(range, trough) ? -1 : ends.Low;
        double high = TakesIn(range, crest) ? 1 : ends.High;

        return new FieldRange(low, high);
    }

    /// <summary>
    /// This method reports whether the given range takes in the given angle, at any turn.
    /// </summary>
    private static bool TakesIn(FieldRange range, double angle)
    {
        double turns = Math.Ceiling((range.Low - angle) / (2 * Math.PI));

        return angle + turns * 2 * Math.PI <= range.High;
    }

    /// <summary>
    /// This method reports whether the given range takes in one of the places a tangent runs off to
    /// infinity, which are a half turn apart.
    /// </summary>
    private static bool CrossesAPole(FieldRange range)
    {
        if (range.Width >= Math.PI)
            return true;

        double poles = Math.Ceiling((range.Low - Math.PI / 2) / Math.PI);

        return Math.PI / 2 + poles * Math.PI <= range.High;
    }

    /// <summary>
    /// This method reports whether the given range lies inside the given ends.
    /// </summary>
    private static bool Within(FieldRange range, double low, double high)
    {
        return range.Low >= low && range.High <= high;
    }

    /// <summary>
    /// The range of a power.  Only a fixed exponent is worked out here, since that is what every use
    /// of it has been and since a range raised to a range is a good deal of case analysis for no known
    /// gain.  An even whole power is a valley with its bottom at nought; an odd one only ever climbs;
    /// anything else is left alone unless what is being raised is known to be positive.
    /// </summary>
    private static FieldRange Power(FieldRange[] arguments)
    {
        FieldRange value = arguments[0];
        FieldRange exponent = arguments[1];

        if (!exponent.IsExact)
            return FieldRange.Anywhere;

        double power = exponent.Low;

        if (power == Math.Floor(power) && power >= 0)
        {
            return power % 2 == 0
                ? Valley(value, 0, number => Math.Pow(number, power))
                : Climbing(value, number => Math.Pow(number, power));
        }

        // A fractional or negative power of a negative number is not a number, and a negative power of
        // something that could be nought runs away to infinity.
        return value.Low <= 0
            ? FieldRange.Anywhere
            : power > 0
                ? Climbing(value, number => Math.Pow(number, power))
                : Falling(value, number => Math.Pow(number, power));
    }
}
