using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents an instruction for constructing a solid color pigment.
/// </summary>
public class SolidPigmentInstruction : ObjectInstruction<Pigment>
{
    private readonly Term _term;

    public SolidPigmentInstruction(Term term)
    {
        _term = term;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        object result = _term.GetValue(variables, typeof(Color), typeof(Pigment));

        Target = result is Color color
            ? new SolidPigment(color)
            : (Pigment) result;
    }
}
