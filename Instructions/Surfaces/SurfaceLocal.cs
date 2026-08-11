using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class holds a name worked out part way down a list of surfaces and known to the rest of it.
/// <para>
/// It is the same thing a function's body may do, in the same words and for the same reason: a figure
/// wanted in three places should be arrived at once rather than three times, or the three drift apart
/// the first time one of them is edited.  Inside a loop it is worth more than that, since the figure
/// usually depends on the count and so is a different figure every turn -- a height, an angle, a
/// colour worked out from how far along the run this one is.  Without it, that arithmetic has to be
/// written out again at every place in the turn that needs it.
/// </para>
/// <para>
/// It is the one entry here that makes no surfaces whatever.  What it leaves behind is a name, which
/// the rest of its list can see and nothing outside it can.
/// </para>
/// </summary>
public class SurfaceLocal : SurfaceListEntry
{
    /// <summary>
    /// This property holds the name being given.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds what it is being given to.
    /// </summary>
    public Term Value { get; init; }

    /// <summary>
    /// This property holds what to call this when complaining that it is not a surface.
    /// </summary>
    protected override string Description => "A name";

    /// <summary>
    /// This method works the value out and leaves it under its name for the rest of the list.  It adds
    /// no surfaces, which is the whole of what makes it different from its neighbours here.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The scope of the list this stands in.</param>
    /// <param name="add">What to do with each surface it makes, which is never called.</param>
    public override void AddSurfacesTo(
        RenderContext context, Variables variables, Action<Surface> add)
    {
        variables.SetValue(Name, Value.GetValue(variables));
    }

    /// <summary>
    /// This method returns a copy of this name.
    /// </summary>
    /// <returns>A copy of this name.</returns>
    public override object Clone()
    {
        return MemberwiseClone();
    }
}
