using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a field: an area of the X/Z plane, given by a 2D outline, filled with
/// copies of one surface.
/// <para>
/// It is a compound surface, like <see cref="TextSolid"/> and <see cref="LSystem"/> -- it builds its
/// own children when it is prepared, from a description rather than from a list.  What that buys is
/// an area of *any shape*: a patch of grass cut to a verge, a border, an island in a roundabout.
/// Before it there were two shapes to be had, a square and a circle, and a scene wanting anything
/// else had to write a loop and a containment test of its own.
/// </para>
/// <para>
/// **It is not only for grass.**  Balls in a pit, a stand of trees, soldiers drawn up in ranks --
/// anything that is one thing in many places over an area, and none of it needing a loop.
/// </para>
/// <para>
/// **The copies are <see cref="Instance"/>s by default.**  An instance's saving is in the *building*
/// rather than the testing, which is exactly this case: one tuft of grass built once and stood in
/// two thousand places costs one tuft to make.  What it cannot do is let each differ, since every
/// instance is the same geometry; a scene that needs each its own way says so, and pays to build
/// each of them.
/// </para>
/// <para>
/// **A shape that may not be shared is copied whether or not the scene asked for copies**, since
/// that is the shape's to refuse rather than the author's to get right.  See
/// <see cref="Instance.MayBeShared"/> for what refuses and why.
/// </para>
/// </summary>
public class Field : Group
{
    private const int DefaultSeed = 0;

    /// <summary>
    /// This property holds the outline, in the X/Z plane, that the field fills.
    /// </summary>
    public GeneralPath Outline { get; set; }

    /// <summary>
    /// This property holds how to make the thing the field is filled with.  It is a factory rather
    /// than a surface so that a field asked for its copies one by one can have a fresh one each
    /// time, the way an L-system makes its leaves.
    /// <para>
    /// It is given the copy's number, which is what lets each of them differ: the scene names that
    /// number and its own arithmetic does the rest.  Without it, copies would be the same geometry
    /// built over and over -- strictly worse than instances, which build it once.
    /// </para>
    /// </summary>
    public Func<int, Surface> Prototype { get; set; }

    /// <summary>
    /// This property holds how far apart the copies stand, before any jitter.
    /// </summary>
    public double Spacing { get; set; } = 1;

    /// <summary>
    /// This property holds how far a copy may stray from its place on the grid, as a fraction of the
    /// spacing.  It is nought for ranks and files, and about 0.8 for something that should not look
    /// counted.
    /// <para>
    /// **A lattice is very easy to see and impossible to unsee**, which is why anything meant to look
    /// natural wants most of a cell of jitter rather than a little.
    /// </para>
    /// </summary>
    public double Jitter { get; set; }

    /// <summary>
    /// This property notes that each place is to get its own surface, built for it, rather than an
    /// instance of one shared shape.  A scene asks for this by writing `index in <name>`.
    /// </summary>
    public bool Copies { get; set; }

    /// <summary>
    /// This method is called once prior to rendering to fill the outline.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Outline is null)
            throw new Exception("A field needs an outline to fill.");

        if (Prototype is null)
            throw new Exception("A field needs something to fill its outline with.");

        if (Spacing <= 0)
            throw new Exception("A field's spacing must be greater than nought.");

        Random random = new Random(Seed ?? DefaultSeed);
        Surface shared = null;
        Surface first = null;
        int number = 0;

        if (!Copies)
        {
            // Built to be shared -- but not every shape may be, and asking is not fussiness.  A
            // shape holding a light, or a medium, or one that moves has no single place to be; and
            // an instance of a shape that is itself an instance cannot say which of the two a point
            // lies in, which the instance refuses outright rather than getting quietly wrong.
            //
            // **That last is the ordinary case, not an exotic one.**  A primitive that gives back a
            // group hands out a group holding an instance, and most primitives worth calling give
            // back a group.  A field of one used to end the render complaining about shapes holding
            // shapes, which is true of the internals and no help at all to whoever wrote the scene.
            Surface candidate = Prototype(0);

            if (Instance.MayBeShared(candidate))
                shared = candidate;
            else
            {
                // It is made already, so it stands at the first place rather than being thrown away,
                // and the numbering carries on from it.  Nothing much is lost by the refusal: where
                // the shape came from a primitive, that primitive does its own sharing underneath,
                // so what is built again here is the wrapper and not the geometry.
                first = candidate;
                number = 1;
            }
        }

        Surfaces.Clear();

        // The grid runs over the outline's own extent, so an outline drawn anywhere is filled where
        // it actually lies rather than about the origin.
        int columns = (int) Math.Ceiling((Outline.MaxX - Outline.MinX) / Spacing);
        int rows = (int) Math.Ceiling((Outline.MaxY - Outline.MinY) / Spacing);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                // The middle of each cell rather than its corner, which keeps the first row and
                // column off the outline's own edge -- a point lying exactly on the edge is neither
                // in nor out as far as an even-odd test is concerned, and a whole row of them would
                // be decided by rounding.
                double x = Outline.MinX + (column + 0.5) * Spacing +
                           (random.NextDouble() - 0.5) * Spacing * Jitter;
                double z = Outline.MinY + (row + 0.5) * Spacing +
                           (random.NextDouble() - 0.5) * Spacing * Jitter;

                // Tested where it has actually been *moved to*, not where the grid put it.  Asking
                // about the grid point instead would let a copy's jitter carry it outside the very
                // outline it was chosen for, which shows along every edge.
                if (!Outline.Contains(new TwoDPoint(x, z)))
                    continue;

                Surface copy;

                if (shared is not null)
                    copy = new Instance { Prototype = shared };
                else if (first is not null)
                {
                    copy = first;
                    first = null;
                }
                else
                    copy = Prototype(number++);

                copy.Transform = Transforms.Translate(x, 0, z) * copy.Transform;

                Add(copy);
            }
        }

        base.PrepareSurfaceForRendering();
    }
}
