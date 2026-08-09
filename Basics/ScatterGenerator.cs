namespace RayTracer.Basics;

/// <summary>
/// This class is the source of the scattered values a scene draws on when it wants things to differ
/// from one another: a number between 0 and 1 for every combination of keys it is handed.
/// <para>
/// It is <see cref="NoiseGenerator"/>'s opposite number and the pair are worth telling apart, since
/// they are asked much the same question and answer it very differently.  Noise is <i>smooth</i>: two
/// nearby points give two nearby values, which is what makes it useful for the grain of marble or the
/// shape of a cloud.  This is <i>scattered</i>: two neighboring keys give two values with nothing to
/// do with each other, which is what makes it useful for a stand of trees where no two are alike.
/// </para>
/// <para>
/// <b>It holds no state and keeps no stream.</b>  Nothing here counts how many values have been handed
/// out, and asking twice for the same keys always gives the same answer.  That is the whole design and
/// it is worth saying why, since a running stream is the obvious way to write this and is the wrong
/// one.  A stream's answers depend on how many questions came before, so adding one tree at the top of
/// a file would rearrange every tree below it; the order things are resolved in would become part of
/// what a scene means; nothing could ever be resolved in parallel; and a frame of an animation would
/// differ from the one before it for no reason the scene could see.  Keys cost the author a number and
/// buy all four of those back.
/// </para>
/// </summary>
public static class ScatterGenerator
{
    /// <summary>
    /// This method returns the value scattered at one key.
    /// </summary>
    /// <param name="first">The key.</param>
    /// <returns>A value between 0 and 1.</returns>
    public static double At(double first)
    {
        return ToFraction(Mix(BitsOf(first)));
    }

    /// <summary>
    /// This method returns the value scattered at two keys, which is how one key may yield several
    /// values that have nothing to do with each other: the second key says which of them is wanted.
    /// </summary>
    /// <param name="first">The first key.</param>
    /// <param name="second">The second.</param>
    /// <returns>A value between 0 and 1.</returns>
    public static double At(double first, double second)
    {
        return ToFraction(Mix(Mix(BitsOf(first)) ^ BitsOf(second)));
    }

    /// <summary>
    /// This method returns the value scattered at three keys.
    /// </summary>
    /// <param name="first">The first key.</param>
    /// <param name="second">The second.</param>
    /// <param name="third">The third.</param>
    /// <returns>A value between 0 and 1.</returns>
    public static double At(double first, double second, double third)
    {
        return ToFraction(Mix(Mix(Mix(BitsOf(first)) ^ BitsOf(second)) ^ BitsOf(third)));
    }

    /// <summary>
    /// This method turns a key into the bits to mix, taking the number exactly as written.
    /// <para>
    /// Negative zero is folded onto zero first.  The two are equal as numbers and a scene that wrote
    /// one meant the other, but their bits differ, and without this <c>random(0)</c> and
    /// <c>random(-0)</c> would be two different values for what a reader sees as one key.
    /// </para>
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The bits of it.</returns>
    private static ulong BitsOf(double key)
    {
        return (ulong) BitConverter.DoubleToInt64Bits(key == 0 ? 0 : key);
    }

    /// <summary>
    /// This method stirs a number thoroughly enough that neighboring inputs give unrelated outputs,
    /// which is the one thing this class has to get right.  It is the finalizer from SplitMix64, whose
    /// business is exactly that.
    /// </summary>
    /// <param name="value">The number to stir.</param>
    /// <returns>The stirred number.</returns>
    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EB;

        return value ^ (value >> 31);
    }

    /// <summary>
    /// This method turns stirred bits into a number between 0 and 1, taking the 53 that a double can
    /// carry exactly so that every value it can give is equally likely.
    /// </summary>
    /// <param name="value">The stirred bits.</param>
    /// <returns>A value between 0 and 1, the 1 not included.</returns>
    private static double ToFraction(ulong value)
    {
        return (value >> 11) * (1.0 / 9007199254740992.0);
    }
}
