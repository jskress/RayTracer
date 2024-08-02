using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create CSG surfaces.
/// </summary>
public class CsgSurfaceInstructionSet : SurfaceInstructionSet<CsgSurface>
{
    private readonly List<IInstructionSet> _instructions = [];
    private readonly CsgOperation _operation;
    private readonly Token _errorToken;

    public CsgSurfaceInstructionSet(CsgOperation operation, Token errorToken)
    {
        _operation = operation;
        _errorToken = errorToken;
    }

    /// <summary>
    /// This method is used to add a new instruction set to this one set.
    /// </summary>
    /// <param name="instruction">The instruction to add.</param>
    public void AddInstruction(IInstructionSet instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        _instructions.Add(instruction);
    }

    /// <summary>
    /// This method is used to run all our instructions.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        if (_instructions.Count < 2)
        {
            throw new TokenException(
                $"Not enough surfaces provided to perform the {_operation.ToString().ToLower()} operation.")
            {
                Token = _errorToken
            };
        }

        List<Surface> surfaces = _instructions
            .Select(instruction => CreateSurface(context, variables, instruction))
            .ToList();

        do
        {
            surfaces[1] = new CsgSurface(_operation)
            {
                Left = surfaces[0],
                Right = surfaces[1]
            };

            surfaces.RemoveFirst();
        }
        while (surfaces.Count > 1);

        CreatedObject = (CsgSurface) surfaces[0];

        ApplyInstructions(context, variables);

        if (CreatedObject.Material != null)
            CreatedObject.SetMaterial(CreatedObject.Material);
    }
}
