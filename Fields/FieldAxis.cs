namespace RayTracer.Fields;

/// <summary>
/// This enumeration names the three things a field function is a function of.  A scene writes them
/// as <c>x</c>, <c>y</c> and <c>z</c>, and inside a function they mean the point being asked about
/// rather than any variable of that name the scene may also have set.
/// </summary>
public enum FieldAxis
{
    X,
    Y,
    Z
}
