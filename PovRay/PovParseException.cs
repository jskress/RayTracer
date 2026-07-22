namespace RayTracer.PovRay;

/// <summary>
/// This class represents something in a POV-Ray file that we cannot make sense of.
/// <para>
/// It is thrown far more often than a failure would suggest, and that is by design.  We read only
/// as much of POV-Ray's language as the library files need, so meeting a macro call or a function
/// pattern is an ordinary event rather than a bug.  The reader catches this, notes the declaration
/// it was in the middle of as one it could not convert, and carries on with the next.
/// </para>
/// </summary>
public class PovParseException : Exception
{
    /// <summary>
    /// This property holds the line the trouble was found on.
    /// </summary>
    public int Line { get; }

    public PovParseException(string message, int line) : base(message)
    {
        Line = line;
    }
}
