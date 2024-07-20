namespace RayTracer.Instructions;

/// <summary>
/// This is the base class for all instructions that act on an object.
/// </summary>
public abstract class ObjectInstruction<TObject> : Instruction
    where TObject : class
{
    /// <summary>
    /// This property holds a reference to the object that is the target of this instruction.
    /// </summary>
    internal TObject Target { get; set; }
}
