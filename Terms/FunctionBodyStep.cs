using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class holds one thing a function does on its way to its answer: either working something out
/// and giving it a name, or declaring a smaller function of its own.
/// <para>
/// They are kept in one list and in the order they were written, rather than as two lists, because
/// they lean on each other in that order: a thing worked out may call a function declared above it,
/// and a function declared may use a thing worked out above it.  Splitting them would either lose
/// that or invent a rule about which comes first, and neither is worth doing to save a class.
/// </para>
/// </summary>
public class FunctionBodyStep
{
    /// <summary>
    /// This property holds the name the step binds.
    /// </summary>
    public string Name { get; }

    private readonly Term _value;
    private readonly UserFunction _function;
    private readonly UserPrimitive _primitive;

    /// <summary>
    /// This constructor makes a step that works something out and names it.
    /// </summary>
    /// <param name="name">The name to bind.</param>
    /// <param name="value">The expression to work out.</param>
    public FunctionBodyStep(string name, Term value)
    {
        Name = name;
        _value = value;
    }

    /// <summary>
    /// This constructor makes a step that declares a smaller function.
    /// </summary>
    /// <param name="name">The name to bind.</param>
    /// <param name="function">The function, as yet belonging to no scope.</param>
    public FunctionBodyStep(string name, UserFunction function)
    {
        Name = name;
        _function = function;
    }

    /// <summary>
    /// This constructor makes a step that declares a smaller primitive.
    /// </summary>
    /// <param name="name">The name to bind.</param>
    /// <param name="primitive">The primitive, as yet belonging to no scope.</param>
    public FunctionBodyStep(string name, UserPrimitive primitive)
    {
        Name = name;
        _primitive = primitive;
    }

    /// <summary>
    /// This method carries the step out, binding whatever it names into the given scope.
    /// <para>
    /// A function declared here is bound to <i>this</i> scope, which is the call's own -- so a
    /// smaller function may use the values its surrounding function was given, exactly as one written
    /// at the top of a file may use what that file set up.
    /// </para>
    /// </summary>
    /// <param name="scope">The scope being built up.</param>
    public void CarryOut(Variables scope)
    {
        object bound = _primitive is not null
            ? _primitive.BoundTo(scope)
            : _function is not null
                ? _function.BoundTo(scope)
                : _value.GetValue(scope);

        scope.SetValue(Name, bound);
    }
}
