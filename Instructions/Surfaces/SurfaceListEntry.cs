using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is the parent of the things that may stand in a list of surfaces without being one.
/// <para>
/// Nearly everything written in a group is one surface, and the list is walked by asking each entry
/// for it.  These are the exceptions, and they are exceptions in two different ways.  A loop or a
/// choice makes <i>any number</i> of surfaces, which is not known until the scene is rendered because
/// the range or the condition is an expression; a name makes <i>none at all</i> and merely leaves
/// something behind for the rest of the list to use.  What they have in common is that the count is
/// not one, so none of them can answer <see cref="ResolveToSurface"/>, and all of them are asked
/// <see cref="AddSurfacesTo"/> instead by the one place that walks such a list.
/// </para>
/// <para>
/// What they make is put straight where they were written rather than into a group of their own.
/// That is deliberate: these are ways of writing, not things in the scene, and a reader who writes
/// twelve cubes by hand and a reader who writes a loop that makes twelve should get the same twelve
/// cubes in the same place in the tree.
/// </para>
/// </summary>
public abstract class SurfaceListEntry : ISurfaceResolver
{
    /// <summary>
    /// This property holds what to call this when complaining that it is not a surface.
    /// </summary>
    protected abstract string Description { get; }

    /// <summary>
    /// This method makes whatever this entry describes and hands each one over.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The scope of the list this stands in.</param>
    /// <param name="add">What to do with each surface it makes.</param>
    public abstract void AddSurfacesTo(
        RenderContext context, Variables variables, Action<Surface> add);

    /// <summary>
    /// This method walks a list of things standing in a group and adds what each makes to it, which is
    /// one surface for nearly all of them and any other number for the entries here.
    /// <para>
    /// The list is walked in a scope of its own.  That is what lets a name stand part way down a list
    /// and be known to the rest of it: it is written into this scope rather than the one around, so it
    /// reaches everything below it, including the insides of the surfaces there, and nothing above or
    /// outside.  A group that works out a spacing for its own use does not hand that spacing to the
    /// group that holds it.
    /// </para>
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The scope the list was written in.</param>
    /// <param name="entries">The things standing there.</param>
    /// <param name="add">What to do with each surface they make.</param>
    public static void AddAllTo(
        RenderContext context, Variables variables, List<ISurfaceResolver> entries,
        Action<Surface> add)
    {
        Variables scope = new (variables);

        foreach (ISurfaceResolver entry in entries)
        {
            if (entry is SurfaceListEntry standing)
                standing.AddSurfacesTo(context, scope, add);
            else
                add(entry.ResolveToSurface(context, scope));
        }
    }

    /// <summary>
    /// This method is never called; these are not one surface and cannot answer as one.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>Nothing; this always throws.</returns>
    public Surface ResolveToSurface(RenderContext context, Variables variables)
    {
        throw new NotSupportedException(
            $"{Description} stands in a list of surfaces without being one.");
    }

    /// <summary>
    /// This method is never called; these make things rather than laying anything over one.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="surface">The surface already made.</param>
    public void ApplyToSurface(RenderContext context, Variables variables, Surface surface)
    {
        throw new NotSupportedException($"{Description} cannot be laid over a surface.");
    }

    /// <summary>
    /// This method is never called; these are not one surface and cannot answer as one.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>Nothing; this always throws.</returns>
    public object ResolveToObject(RenderContext context, Variables variables)
    {
        return ResolveToSurface(context, variables);
    }

    /// <summary>
    /// This method returns a copy of this entry.
    /// </summary>
    /// <returns>A copy of this entry.</returns>
    public abstract object Clone();
}
