namespace RayTracer.Instructions.Transforms;

/// <summary>
/// This enumeration notes which axis a transform should apply to.
/// </summary>
public enum TransformAxis
{
    /// <summary>
    /// This entry notes that a transform is not limited to a specific axis.  This is not
    /// actually used for anything other than an initial value.
    /// </summary>
    None,

    /// <summary>
    /// This entry notes that a transform is to be performed along the X axis.
    /// </summary>
    X,

    /// <summary>
    /// This entry notes that a transform is to be performed along the Y axis.
    /// </summary>
    Y,

    /// <summary>
    /// This entry notes that a transform is to be performed along the Z axis.
    /// </summary>
    Z,

    /// <summary>
    /// This entry notes that a transform is to be performed along all axes.
    /// </summary>
    All
}
