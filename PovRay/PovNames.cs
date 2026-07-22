namespace RayTracer.PovRay;

/// <summary>
/// This class turns a POV-Ray name into one written the way the ray tracer's own libraries are.
/// <para>
/// POV-Ray marks what a thing is with a one-letter prefix, and the ray tracer says it in a word at
/// the end, so <c>T_Ruby</c> becomes <c>RubyMaterial</c>.  What the word is comes from the value
/// itself rather than from the prefix, which is worth the trouble because POV-Ray's prefixes are
/// not to be trusted: <c>M_Wood1A</c> is a color map rather than a material, and <c>P_Gold1</c> is
/// a plain color rather than a pigment.  Reading the prefix would have got both wrong.
/// </para>
/// </summary>
public static class PovNames
{
    /// <summary>
    /// These are the prefixes POV-Ray marks a name with.  They are taken off wherever they are
    /// found, since the word put on the end says the same thing more plainly.
    /// </summary>
    private static readonly string[] Prefixes = ["Col_", "T_", "M_", "F_", "P_", "I_"];

    /// <summary>
    /// This method works out what a POV-Ray declaration should be called here.
    /// </summary>
    /// <param name="povName">The name POV-Ray gave it.</param>
    /// <param name="suffix">The word saying what it is, or <c>null</c> to add none.</param>
    /// <returns>The name to declare it under.</returns>
    public static string From(string povName, string suffix)
    {
        string bare = povName;

        foreach (string prefix in Prefixes)
        {
            if (bare.StartsWith(prefix, StringComparison.Ordinal))
            {
                bare = bare[prefix.Length..];

                break;
            }
        }

        string joined = string.Concat(bare
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(Capitalize));

        // Taking the prefix off can leave nothing at all, for a name that was only a prefix, and
        // can leave something starting with a digit, which would not be a name here.
        if (joined.Length == 0 || char.IsDigit(joined[0]))
            joined = Capitalize(povName.Replace("_", string.Empty));

        return suffix is null ? joined : joined + suffix;
    }

    /// <summary>
    /// This method puts the first letter of a word in upper case and leaves the rest of it alone,
    /// so that the case POV-Ray chose within a word is kept: <c>WoodGrain1A</c> stays as it is.
    /// </summary>
    /// <param name="word">The word to capitalize.</param>
    /// <returns>The word, capitalized.</returns>
    private static string Capitalize(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];
}
