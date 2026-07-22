namespace RayTracer.PovRay;

/// <summary>
/// This class represents a name in scope while the POV-Ray files are being read, together with
/// where it came from.
/// <para>
/// Where it came from is what lets one generated library lean on another rather than carry a copy
/// of it.  POV-Ray's own files are not much help here: <c>metals.inc</c> says outright that it
/// includes <c>golds.inc</c>, but <c>stones1.inc</c> uses <c>White</c> and <c>Mica</c> without
/// including <c>colors.inc</c> at all, on the understanding that a scene will have included it
/// first.  Noting the source of every name as it is used finds both sorts of dependency the same
/// way, and finds only the ones that are really there.
/// </para>
/// </summary>
public class PovSymbol
{
    /// <summary>
    /// This property holds what the name stands for.
    /// </summary>
    public PovValue Value { get; init; }

    /// <summary>
    /// This property holds the name of the file that declared it.
    /// </summary>
    public string SourceFile { get; init; }

    /// <summary>
    /// This property notes whether that file is becoming a library of its own.  When it is not,
    /// its declarations are folded into whatever used them and there is no dependency to record.
    /// </summary>
    public bool FromSeparateLibrary { get; init; }

    public override string ToString() => $"{Value} (from {SourceFile})";
}
