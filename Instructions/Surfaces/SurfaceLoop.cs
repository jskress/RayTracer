using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class holds a run of surfaces made over and over: a range to count through, a name to call the
/// count by, and the things to make each time round.
/// <para>
/// It stands in a group's list of surfaces without being one, which is the whole difficulty of it: what
/// it puts there is not one surface but any number, and which number is not known until the range is
/// worked out, that being an expression like any other.  So it answers <see cref="ResolveToSurface"/>
/// with a refusal and is asked <see cref="AddSurfacesTo"/> instead, by the one place that walks such a
/// list.
/// </para>
/// <para>
/// The things it makes are put straight where the loop was written rather than into a group of the
/// loop's own.  That is deliberate: a loop is a way of writing, not a thing in the scene, and a reader
/// who writes twelve cubes by hand and a reader who writes a loop that makes twelve should get the same
/// twelve cubes in the same place in the tree.
/// </para>
/// </summary>
public class SurfaceLoop : ISurfaceResolver
{
    /// <summary>
    /// This property holds the name the count is known by inside the loop, or <c>null</c> when the
    /// loop wanted no name for it and merely wanted the repetition.
    /// </summary>
    public string CounterName { get; init; }

    /// <summary>
    /// This property holds where the count starts.
    /// </summary>
    public Term Start { get; init; }

    /// <summary>
    /// This property holds where it ends.
    /// </summary>
    public Term End { get; init; }

    /// <summary>
    /// This property holds how far it moves each time, or <c>null</c> for one.
    /// </summary>
    public Term Step { get; init; }

    /// <summary>
    /// This property notes whether the start was written open, which leaves it out of the count.
    /// </summary>
    public bool StartIsOpen { get; init; }

    /// <summary>
    /// This property notes whether the end was.
    /// </summary>
    public bool EndIsOpen { get; init; }

    /// <summary>
    /// This property holds what the loop makes each time round, in the order it was written.  A loop
    /// may stand among them, which is how one loop is written inside another.
    /// </summary>
    public List<ISurfaceResolver> SurfaceResolvers { get; set; } = [];

    /// <summary>
    /// This method makes everything the loop describes and adds it to the given group.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="add">What to do with each surface it makes.</param>
    public void AddSurfacesTo(RenderContext context, Variables variables, Action<Surface> add)
    {
        Interval interval = new Interval
            {
                Start = Start.GetValue<double>(variables),
                End = End.GetValue<double>(variables),
                IsStartOpen = StartIsOpen,
                IsEndOpen = EndIsOpen
            }
            .Reset(Step?.GetValue<double>(variables) ?? 1);

        while (!interval.IsAtEnd)
        {
            double index = interval.Next();

            // Each turn gets a scope of its own, so that whatever a turn works out belongs to that turn
            // and the count itself is not left lying about after the loop is finished with.  Names from
            // further out are still seen, a scope handing on what it does not hold itself, and two
            // loops nested one inside the other may use the same name without treading on each other.
            Variables scope = new (variables);

            if (CounterName is not null)
                scope.SetValue(CounterName, index);

            AddAllTo(context, scope, SurfaceResolvers, add);
        }
    }

    /// <summary>
    /// This method walks a list of things standing in a group and adds what each makes to it, which is
    /// one surface for nearly all of them and any number for a loop.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="entries">The things standing there.</param>
    /// <param name="add">What to do with each surface they make.</param>
    public static void AddAllTo(
        RenderContext context, Variables variables, List<ISurfaceResolver> entries,
        Action<Surface> add)
    {
        foreach (ISurfaceResolver entry in entries)
        {
            if (entry is SurfaceLoop loop)
                loop.AddSurfacesTo(context, variables, add);
            else
                add(entry.ResolveToSurface(context, variables));
        }
    }

    /// <summary>
    /// This method is never called; a loop is not one surface and cannot answer as one.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>Nothing; this always throws.</returns>
    public Surface ResolveToSurface(RenderContext context, Variables variables)
    {
        throw new NotSupportedException("A loop makes a run of surfaces rather than one.");
    }

    /// <summary>
    /// This method is never called; a loop makes things rather than laying anything over one.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="surface">The surface already made.</param>
    public void ApplyToSurface(RenderContext context, Variables variables, Surface surface)
    {
        throw new NotSupportedException("A loop cannot be laid over a surface.");
    }

    /// <summary>
    /// This method is never called; a loop is not one surface and cannot answer as one.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>Nothing; this always throws.</returns>
    public object ResolveToObject(RenderContext context, Variables variables)
    {
        return ResolveToSurface(context, variables);
    }

    /// <summary>
    /// This method returns a copy of this loop, with a list of its own so that two copies cannot tread
    /// on each other.
    /// </summary>
    /// <returns>A copy of this loop.</returns>
    public object Clone()
    {
        SurfaceLoop loop = (SurfaceLoop) MemberwiseClone();

        loop.SurfaceResolvers = [..loop.SurfaceResolvers];

        return loop;
    }
}
