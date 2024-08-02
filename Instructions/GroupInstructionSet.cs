using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create groups.
/// </summary>
public class GroupInstructionSet : SurfaceInstructionSet<Group>
{
    private readonly List<IInstructionSet> _instructions = [];
    private readonly string _variableName;
    private readonly Term _startTerm;
    private readonly Term _endTerm;
    private readonly Term _stepTerm;
    private readonly bool _startIsOpen;
    private readonly bool _endIsOpen;

    public GroupInstructionSet(
        string variableName, Term startTerm, Term endTerm, Term stepTerm,
        bool startIsOpen, bool endIsOpen)
    {
        if ((startTerm == null && endTerm != null) ||
            (startTerm != null && endTerm == null))
            throw new Exception("Internal error: wrong number of terms given.");

        _variableName = variableName;
        _startTerm = startTerm;
        _endTerm = endTerm;
        _stepTerm = stepTerm;
        _startIsOpen = startIsOpen;
        _endIsOpen = endIsOpen;
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
        Interval interval = GetInterval(variables);

        CreatedObject = new Group();

        while (!interval.IsAtEnd)
        {
            double index = interval.Next();

            if (_variableName != null)
                variables.SetValue(_variableName, index);

            CreateChildSurfaces(context, variables);
        }

        ApplyInstructions(context, variables);
    }

    /// <summary>
    /// This method is used to return an interval based on the terms we were (or weren't)
    /// given upon construction that will drive our execution.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The appropriate interval for controlling our execution.</returns>
    private Interval GetInterval(Variables variables)
    {
        double start, end;

        if (_startTerm is null)
            start = end = 1;
        else
        {
            start = _startTerm.GetValue<double>(variables);
            end = _endTerm.GetValue<double>(variables);
        }

        double step = _stepTerm?.GetValue<double>(variables) ?? 1;

        return new Interval
        {
            Start = start,
            End = end,
            IsStartOpen = _startIsOpen,
            IsEndOpen = _endIsOpen
        }
        .Reset(step);
    }

    /// <summary>
    /// This method will iterate over our object creation instruction sets and add the
    /// created surfaces to our group.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    private void CreateChildSurfaces(RenderContext context, Variables variables)
    {
        foreach (Surface surface in _instructions
                     .Select(instruction => CreateSurface(context, variables, instruction)))
        {
            CreatedObject.Add(surface);
        }
    }
}
