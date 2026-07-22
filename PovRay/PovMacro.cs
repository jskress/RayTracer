using Lex.Tokens;

namespace RayTracer.PovRay;

/// <summary>
/// This class represents a POV-Ray macro that stands for a single expression.
/// <para>
/// Macros in general are far beyond us -- they may declare things, loop, and write out whole
/// objects -- and one of those is noted as something we cannot handle.  But POV-Ray also uses
/// them as a stand-in for a named function, and those are worth having: <c>ior.inc</c> writes
/// <c>#macro Ior(data) (data.y) #end</c> and then leans on it for every index of refraction it
/// declares, so without this the most useful half of that file, the glasses and the gemstones,
/// does not come across at all.
/// </para>
/// </summary>
public class PovMacro
{
    /// <summary>
    /// This property holds the macro's name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds the names of what it takes, in the order it takes them.
    /// </summary>
    public List<string> Parameters { get; init; } = [];

    /// <summary>
    /// This property holds the tokens of the expression it stands for.
    /// </summary>
    public List<Token> Body { get; init; } = [];

    public override string ToString() => $"{Name}({string.Join(", ", Parameters)})";
}
