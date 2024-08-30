using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

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
    /// This property holds a dictionary of items that have been assigned to a variable
    /// name and are extensible where referenced.
    /// </summary>
    internal Dictionary<string, ICloneable> ExtensibleItems { get; } = new ();

    /// <summary>
    /// This property reports whether we have seen any scene definitions.
    /// </summary>
    public bool SeenSceneDefinition { get; private set; }

    /// <summary>
    /// This property reports whether we have any open object definitions being parsed.
    /// </summary>
    public bool IsEmpty => _targets.IsEmpty();

    /// <summary>
    /// This property reports the current target into which parsing should inject
    /// instructions and/or resolvers, if we have one.
    /// </summary>
    internal object CurrentTarget => IsEmpty ? null : _targets.Peek();

    private readonly Stack<object> _targets = [];

    /// <summary>
    /// This method is used to push a new target onto our target stack.
    /// </summary>
    /// <param name="target">The new target to start using.</param>
    internal void PushTarget(object target)
    {
        SeenSceneDefinition |= target is SceneResolver;

        _targets.Push(target);
    }

    /// <summary>
    /// This method is used to remove the current target from our target stack.
    /// </summary>
    internal void PopTarget()
    {
        _targets.Pop();
    }
}
