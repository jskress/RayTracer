using Lex.Parser;
using Lex.Tokens;
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
/// It stands in a group's list of surfaces without being one, which is the whole difficulty of it and
/// the reason <see cref="SurfaceListEntry"/> exists: what it puts there is not one surface but any
/// number, and which number is not known until the range is worked out, that being an expression like
/// any other.
/// </para>
/// </summary>
public class SurfaceLoop : SurfaceListEntry
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
    /// This property holds the token to complain about when the range makes no sense.
    /// </summary>
    public Token ErrorToken { get; init; }

    /// <summary>
    /// This property holds what to call this when complaining that it is not a surface.
    /// </summary>
    protected override string Description => "A loop";

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
    public override void AddSurfacesTo(
        RenderContext context, Variables variables, Action<Surface> add)
    {
        double start = Start.GetValue<double>(variables);
        double end = End.GetValue<double>(variables);
        double step = Step?.GetValue<double>(variables) ?? 1;

        // A step of nothing never arrives, and a step pointing away from the end never arrives either.
        // Both are worth saying out loud rather than leaving to be discovered, since what either one
        // does is hang: the range is made of expressions, so a loop that has counted properly for
        // months can be given a step of zero by a value worked out somewhere else entirely.
        if (step == 0 || (end - start) * step < 0)
        {
            throw new TokenException(
                step == 0
                    ? "A loop's step cannot be zero; the count would never reach the end of the range."
                    : $"A loop counting from {start} to {end} cannot step by {step}, which heads the " +
                      "other way and would never reach the end.")
            {
                Token = ErrorToken
            };
        }

        Interval interval = new Interval
            {
                Start = start,
                End = end,
                IsStartOpen = StartIsOpen,
                IsEndOpen = EndIsOpen
            }
            .Reset(step);

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
    /// This method returns a copy of this loop, with a list of its own so that two copies cannot tread
    /// on each other.
    /// </summary>
    /// <returns>A copy of this loop.</returns>
    public override object Clone()
    {
        SurfaceLoop loop = (SurfaceLoop) MemberwiseClone();

        loop.SurfaceResolvers = [..loop.SurfaceResolvers];

        return loop;
    }
}
