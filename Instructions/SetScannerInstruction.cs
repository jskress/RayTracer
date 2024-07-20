using RayTracer.General;
using RayTracer.Scanners;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting the scanner to use for generating
/// images.
/// </summary>
public class SetScannerInstruction : Instruction
{
    private readonly Func<IScanner> _createScanner;

    public SetScannerInstruction(Func<IScanner> createScanner)
    {
        _createScanner = createScanner;
    }

    /// <summary>
    /// This method is used to execute the instruction to set the copyright statement.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        context.Scanner = _createScanner();
    }
}
