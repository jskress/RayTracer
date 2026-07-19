using RayTracer.Basics;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class implements rendering a 3D, L-system generated path as a series of tapering tube
/// segments.  It is the smooth-jointed alternative to <see cref="LSystemsPipesRenderer"/>: where
/// that draws each segment as a cylinder of one fixed radius and caps it with a sphere, this
/// draws each as a <see cref="TubeSegment"/> that tapers from the radius at its start to the
/// radius at its end.  Since a segment's end radius is made to match the next segment's start
/// radius, the shoulder that a diameter change ("!") otherwise leaves at every joint disappears.
/// The cap spheres go too: a tube segment is the union of the spheres interpolated between its
/// ends, so it is already round at both, and several segments meeting at a branch node each carry
/// a sphere of the same radius there, which unions into a smooth crotch on its own.
/// </summary>
public class LSystemTubesRenderer : LSystemShapeRenderer
{
    public LSystemTubesRenderer(string production) : base(production) {}

    private double _initialRadius;
    private TubeSegment _pending;

    /// <summary>
    /// This method is used to tell us that the rendering to a surface is starting.
    /// </summary>
    /// <param name="turtle">The initial turtle.</param>
    protected override void Begin(Turtle turtle)
    {
        _initialRadius = turtle.Diameter / 2;

        // No sphere to open with, unlike the pipes renderer: the first segment we emit is round at
        // its own start already.
        BoundingBox = new BoundingBox()
            .Add(Point.Zero);
    }

    /// <summary>
    /// This method handles the given command.  A drawn segment is not emitted right away: it is
    /// held until we learn the radius it should taper to, which we only know once we see the next
    /// segment drawn (the "!" that changes the diameter comes between segments, not within one).
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    /// <param name="command">The turtle command to handle.</param>
    protected override void Execute(Turtle turtle, TurtleCommand command)
    {
        switch (command)
        {
            // 'G' draws exactly as 'F' does; the two differ only in whether they leave a corner
            // behind for a polygon being traced, which is the base renderer's business, not ours.
            case TurtleCommand.DrawLine:
            case TurtleCommand.DrawLineWithoutVertex:
                double radius = turtle.Diameter / 2;

                // The segment we were holding now knows what to taper to: this one's radius.  The
                // two then agree at the point they share, which is what removes the joint.
                Emit(radius);

                _pending = new TubeSegment
                {
                    Start = turtle.PreviousLocation, StartRadius = radius,
                    End = turtle.Location, EndRadius = radius,
                    Material = null // <-- This is important.
                };

                // The box is null-safe throughout because a leaf that cannot report an extent of
                // its own drops it (see StampLeaf), leaving the whole L-system unbounded.
                BoundingBox?.Add(turtle.Location);
                break;
            case TurtleCommand.Move:
                // A move breaks the chain: the turtle picks up somewhere else, so whatever we were
                // holding ends here rather than tapering into a segment it does not actually touch.
                Emit(null);
                break;
            case TurtleCommand.CompleteBranch:
                // The branch ends here, so whatever it was holding is a tip: nothing follows to
                // taper into, so it keeps its own radius and stays round at the end.
                Emit(null);
                break;
        }
    }

    /// <summary>
    /// This method is used to tell us that the rendering to a surface is ending.
    /// </summary>
    /// <param name="turtle">The current turtle.</param>
    protected override void Complete(Turtle turtle)
    {
        Emit(null);

        BoundingBox?.Expand(_initialRadius);
    }

    /// <summary>
    /// This method emits the segment we have been holding, if any, tapering it to the given
    /// radius -- or leaving it at its own, when there is nothing following it to match.
    /// </summary>
    /// <param name="endRadius">The radius to taper the held segment to, or <c>null</c> to leave
    /// it as it is.</param>
    private void Emit(double? endRadius)
    {
        if (_pending is null)
            return;

        if (endRadius is not null)
            _pending.EndRadius = endRadius.Value;

        Surfaces.Add(_pending);

        _pending = null;
    }
}
