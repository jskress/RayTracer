using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create pigments.
/// </summary>
public class PigmentInstructionSet : ListInstructionSet<Pigment>
{
    /// <summary>
    /// This method creates a pigment instruction set for a pigment that is a compound of
    /// other pigments.
    /// </summary>
    /// <param name="type">The type of pigment to create.</param>
    /// <param name="transformInstructionSet">The transform instruction set, if any.</param>
    /// <param name="instructionSets">The set of child pigment instruction sets.</param>
    /// <returns>The created pigment instruction set.</returns>
    public static PigmentInstructionSet CompoundPigmentInstructionSet(
        PigmentType type, TransformInstructionSet transformInstructionSet,
        params PigmentInstructionSet[] instructionSets)
    {
        return new PigmentInstructionSet(
            type, instructionSets, null, transformInstructionSet);
    }

    /// <summary>
    /// This method is used to create a pigment instruction set that creates a solid
    /// pigment.
    /// </summary>
    /// <param name="term">The term that represents the solid pigment.</param>
    /// <returns>The created pigment instruction set.</returns>
    public static PigmentInstructionSet SolidPigmentInstructionSet(Term term)
    {
        return new PigmentInstructionSet(
            PigmentType.Color, null, new SolidPigmentInstruction(term),
            null);
    }

    private readonly PigmentType _type;
    private readonly SolidPigmentInstruction _solidPigmentInstruction;
    private readonly TransformInstructionSet _transformInstructionSet;

    private PigmentInstructionSet(
        PigmentType type, PigmentInstructionSet[] pigmentInstructionSets,
        SolidPigmentInstruction solidPigmentInstruction,
        TransformInstructionSet transformInstructionSet)
    {
        _type = type;
        _solidPigmentInstruction = solidPigmentInstruction;
        _transformInstructionSet = transformInstructionSet;

        foreach (PigmentInstructionSet instructionSet in pigmentInstructionSets)
            AddInstruction(instructionSet);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        _solidPigmentInstruction?.Execute(context, variables);
        _transformInstructionSet?.Execute(context, variables);

        base.Execute(context, variables);
    }

    /// <summary>
    /// This method is used to create the target pigment based on the given list of child
    /// pigments.
    /// </summary>
    /// <param name="pigments">The list of child objects.</param>
    protected override void CreateTargetFrom(List<Pigment> pigments)
    {
        CreatedObject = _type switch
        {
            PigmentType.Checker => new CheckerPigment(pigments[0], pigments[1]),
            PigmentType.Ring => new RingPigment(pigments[0], pigments[1]),
            PigmentType.Stripe => new StripePigment(pigments[0], pigments[1]),
            PigmentType.LinearGradient => new LinearGradientPigment(pigments[0], pigments[1]),
            PigmentType.Color => _solidPigmentInstruction.Target,
            _ => throw new Exception($"Internal error: unknown pigment type: {_type}")
        };
    }
}
