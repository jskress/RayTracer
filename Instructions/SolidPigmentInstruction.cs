using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents an instruction for constructing a solid color pigment.
/// </summary>
public class SolidPigmentInstruction : ObjectInstruction<SolidPigment>
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
        Color color = (Color) _term.GetValue(variables, typeof(Color));

        Target = new SolidPigment(color);
    }
}
