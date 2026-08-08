namespace RayTracer.Terms;

/// <summary>
/// This attribute marks a static method as one of the DSL's functions and gives it the name a
/// scene calls it by.  The method itself carries the rest of the signature -- its parameter types
/// and its return type -- so there is no second copy of any of that to drift out of step with the
/// code that implements it.
/// <para>
/// A method may carry more than one of these, which is how one implementation answers to more
/// than one name.  Overloading is the other way round: several methods carrying the same name,
/// told apart by what they take (see <see cref="FunctionCatalog"/>).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class FunctionAttribute : Attribute
{
    /// <summary>
    /// This property holds the name a scene calls this function by.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// This property notes that the function may not be lowered into a field at all -- not that it
    /// merely lacks a rule for its slope, but that it has no business being asked about a place in
    /// space in the first place.
    /// </summary>
    public bool NotInAField { get; init; }

    public FunctionAttribute(string name)
    {
        Name = name;
    }
}
