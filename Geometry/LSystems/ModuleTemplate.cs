using System.Text;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents one module as it is <em>written</em>, which is to say a letter and the
/// arithmetic that will produce its numbers rather than the numbers themselves.
/// <para>
/// The distinction matters because a production is written once and applied a great many times.
/// <c>F(x * 0.5)</c> in a successor is a letter and one expression; what comes out of it depends on
/// the <c>x</c> that the module being rewritten happened to carry.  So the expressions are compiled
/// to terms when the scene is read, and evaluated per application against a scope holding the
/// formal parameters -- which is the same late binding every other expression in this language
/// gets.
/// </para>
/// </summary>
public class ModuleTemplate
{
    /// <summary>
    /// This property holds the letter this module is written with.
    /// </summary>
    public Rune Letter { get; init; }

    /// <summary>
    /// This property holds the arithmetic that will produce the module's numbers.  It is empty,
    /// never null, for a module written without any.
    /// </summary>
    public Term[] Arguments { get; init; } = [];

    /// <summary>
    /// This method works out this template's numbers against the given scope and hands back the
    /// module they make.
    /// </summary>
    /// <param name="variables">The scope to evaluate the arithmetic against.</param>
    /// <returns>The module this template makes here.</returns>
    public Module Resolve(Variables variables)
    {
        if (Arguments.Length == 0)
            return new Module { Letter = Letter };

        double[] values = new double[Arguments.Length];

        for (int index = 0; index < Arguments.Length; index++)
            values[index] = Arguments[index].GetValue<double>(variables);

        return new Module { Letter = Letter, Parameters = values };
    }

    public override string ToString()
    {
        return Arguments.Length == 0 ? Letter.ToString() : $"{Letter}(...{Arguments.Length})";
    }
}
