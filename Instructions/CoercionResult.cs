namespace RayTracer.Instructions;

/// <summary>
/// This enumeration notes the possible result of trying to coerce a value to a type.
/// </summary>
public enum CoercionResult
{
    /// <summary>
    /// This entry notes that the value was either already of requested type or was
    /// successfully coerced to it.
    /// </summary>
    OfProperType,

    /// <summary>
    /// This entry notes that coercion was attempted without a target type.
    /// </summary>
    NotCoerced,

    /// <summary>
    /// This entry notes that the value could not be coerced to the requested type.
    /// </summary>
    CouldNotCoerce
}
