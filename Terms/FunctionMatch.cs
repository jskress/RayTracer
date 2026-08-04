namespace RayTracer.Terms;

/// <summary>
/// This class holds the outcome of matching a call written in a scene to a form of a function the
/// DSL knows.  Either it matched -- in which case it carries the form to call and the values to
/// call it with, converted to the types that form takes -- or it did not, in which case it carries
/// the reason why, ready to be reported against the token the call was written at.
/// </summary>
public class FunctionMatch
{
    /// <summary>
    /// This property holds the form of the function the call matched, or <c>null</c> if it matched
    /// none of them.
    /// </summary>
    public FunctionSignature Signature { get; }

    /// <summary>
    /// This property holds the values to call the function with, converted to the types it takes.
    /// </summary>
    public object[] Arguments { get; }

    /// <summary>
    /// This property holds why the call matched nothing, or <c>null</c> if it matched.
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// This property reports whether the call matched a form of the function.
    /// </summary>
    public bool IsMatch => Signature is not null;

    internal FunctionMatch(FunctionSignature signature, object[] arguments)
    {
        Signature = signature;
        Arguments = arguments;
    }

    internal FunctionMatch(string error)
    {
        Error = error;
    }

    /// <summary>
    /// This method is used to call the matched form of the function with the matched values.
    /// </summary>
    /// <returns>The value the function produced.</returns>
    public object Invoke()
    {
        if (!IsMatch)
            throw new InvalidOperationException($"Cannot call a function that did not match: {Error}");

        return Signature.Invoke(Arguments);
    }
}
