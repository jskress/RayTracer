using RayTracer.Extensions;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class represents the context into which we will place things that we parse from
/// the input source. 
/// </summary>
internal class ParsingContext
{
    /// <summary>
    /// This property holds the instruction context that parsing creates.
    /// </summary>
    internal InstructionContext InstructionContext { get; } = new ();

    /// <summary>
    /// This property holds a dictionary of items that havve been assigned to a variable
    /// name and are extensible where referenced.
    /// </summary>
    internal Dictionary<string, ICopyableInstructionSet> ExtensibleItems { get; } = new ();

    /// <summary>
    /// This property reports whether we have seen any scene definitions.
    /// </summary>
    public bool SeenSceneDefinition { get; private set; }

    /// <summary>
    /// This property reports whether we have any open object definitions being parsed.
    /// </summary>
    public bool IsEmpty => _sets.IsEmpty();

    /// <summary>
    /// This property reports the current instruction set in our stack, if we have one.
    /// </summary>
    internal IInstructionSet CurrentSet => IsEmpty ? null : _sets.Peek();

    /// <summary>
    /// This property reports the parent set of the current instruction set, if we have
    /// one.
    /// </summary>
    internal IInstructionSet ParentSet => _sets.Count < 2
        ? null : _sets.Skip(1).First();

    private readonly Stack<IInstructionSet> _sets = [];

    /// <summary>
    /// This method is used to push a new instruction set onto our set stack.
    /// </summary>
    /// <param name="instructionSet">The new instruction set to start using.</param>
    internal void PushInstructionSet(IInstructionSet instructionSet)
    {
        SeenSceneDefinition |= instructionSet is SceneInstructionSet;

        _sets.Push(instructionSet);
    }

    /// <summary>
    /// This method is used to remove the current instruction set from our set stack.
    /// </summary>
    /// <returns>The instruction set just removed from our stack.</returns>
    internal void PopInstructionSet()
    {
        _sets.Pop();
    }
}
