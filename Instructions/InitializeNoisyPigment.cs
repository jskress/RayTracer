using RayTracer.General;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class provides an instruction that will initialize an instance of the noisy
/// pigment.
/// </summary>
public class InitializeNoisyPigment : Instruction
{
    private readonly TurbulenceInstructionSet _turbulenceInstructionSet;

    public InitializeNoisyPigment(TurbulenceInstructionSet turbulenceInstructionSet)
    {
        _turbulenceInstructionSet = turbulenceInstructionSet;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        _turbulenceInstructionSet.Execute(context, variables);
    }

    /// <summary>
    /// This method is used to apply our current values to the given pigment.
    /// </summary>
    /// <param name="pigment">The pigment to apply our values to.</param>
    internal void ApplyTo(NoisyPigment pigment)
    {
        pigment.Turbulence = _turbulenceInstructionSet.CreatedObject;
    }
}
