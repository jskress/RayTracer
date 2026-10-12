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
    /// This property holds the things the field may stand at a place, when there is more than one: each
    /// built once and stood wherever it is picked.  Empty means the one <see cref="Prototype"/>.
    /// <para>
    /// **A wood is not one tree in a thousand places, nor a thousand different trees.**  Built one by
    /// one, every copy costs a tree to make; built once, every copy is the same tree.  A handful of
    /// trees, each built once and picked among at every place -- and turned and sized as each is stood
    /// there -- reads as a wood for the cost of a handful.
    /// </para>
    /// </summary>
    public List<FieldChoice> Choices { get; } = [];

    /// <summary>
    /// This property notes that the things to stand are taken in turn rather than picked at random: by
    /// row and column together, so that over a grid they run in diagonal stripes.
    /// </summary>
    public bool Cycle { get; set; }

    /// <summary>
    /// This property holds how far each copy may be turned about the up axis, in radians: the whole
    /// spread of turns, centered on the way the thing faces as made.  Nought leaves every copy facing
    /// the same way, as soldiers in ranks do; a whole turn faces each any way at all.
    /// </summary>
    public double Turn { get; set; }

    /// <summary>
    /// This property holds the smallest size a copy may be stood at, as a scale of the thing as made.
    /// </summary>
    public double SmallestSize { get; set; } = 1;

    /// <summary>
    /// This property holds the largest size a copy may be stood at, as a scale of the thing as made.
    /// </summary>
    public double LargestSize { get; set; } = 1;

    /// <summary>
    /// This method is called once prior to rendering to fill the outline.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Outline is null)
            throw new Exception("A field needs an outline to fill.");

        List<FieldChoice> choices = Choices.Count > 0 ? Choices
            : Prototype is null ? [] : [new FieldChoice(Prototype)];

        if (choices.Count == 0)
            throw new Exception("A field needs something to fill its outline with.");

        if (Spacing <= 0)
            throw new Exception("A field's spacing must be greater than nought.");

        if (choices.Any(choice => choice.Weight <= 0))
            throw new Exception("A field's weights must be greater than nought.");

        if (SmallestSize <= 0 || LargestSize < SmallestSize)
            throw new Exception("A field's sizes must be greater than nought, the smaller first.");

        int seed = Seed ?? DefaultSeed;
        Random random = new Random(seed);
        Surface[] shared = new Surface[choices.Count];
        Surface[] first = new Surface[choices.Count];
        int number = 0;

        if (!Copies)
        {
            for (int index = 0; index < choices.Count; index++)
            {
                // Built to be shared -- but not every shape may be, and asking is not fussiness.  A
                // shape holding a light, or a medium, or one that moves has no single place to be;
                // and an instance of a shape that is itself an instance cannot say which of the two a
                // point lies in, which the instance refuses outright rather than getting quietly wrong.
                //
                // **That last is the ordinary case, not an exotic one.**  A primitive that gives back
                // a group hands out a group holding an instance, and most primitives worth calling
                // give back a group.  A field of one used to end the render complaining about shapes
                // holding shapes, which is true of the internals and no help at all to whoever wrote
                // the scene.
                Surface candidate = choices[index].Make(number);

                if (Instance.MayBeShared(candidate))
                    shared[index] = candidate;
                else
                {
                    // It is made already, so it stands at the first place it is picked for rather than
                    // being thrown away, and the numbering carries on from it.  Nothing much is lost
                    // by the refusal: where the shape came from a primitive, that primitive does its
                    // own sharing underneath, so what is built again here is the wrapper and not the
                    // geometry.
                    first[index] = candidate;
                    number++;
                }
            }
        }

        int[] turns = Cycle ? TurnsOf(choices) : null;
        double totalWeight = choices.Sum(choice => choice.Weight);

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

                int pick = choices.Count == 1 ? 0
                    : turns is not null ? turns[(row + column) % turns.Length]
                    : Picked(choices, totalWeight * DrawFor(seed, row, column, PickDraw));
                Surface copy;

                if (shared[pick] is not null)
                    copy = new Instance { Prototype = shared[pick] };
                else if (first[pick] is not null)
                {
                    copy = first[pick];
                    first[pick] = null;
                }
                else
                    copy = choices[pick].Make(number++);

                copy.Transform = PlacedAt(x, z, seed, row, column) * copy.Transform;

                Add(copy);
            }
        }

        base.PrepareSurfaceForRendering();
    }

    // Which of a place's draws is which: each is its own number, so asking for one more of them never
    // changes another.
    private const int PickDraw = 1;
    private const int TurnDraw = 2;
    private const int SizeDraw = 3;

    /// <summary>
    /// This method returns the transform that stands a copy at a place: turned and sized as the field
    /// says, then moved there.
    /// </summary>
    private Matrix PlacedAt(double x, double z, int seed, int row, int column)
    {
        Matrix placed = Transforms.Translate(x, 0, z);

        if (Turn > 0)
            placed *= Transforms.RotateAroundY((DrawFor(seed, row, column, TurnDraw) - 0.5) * Turn, true);

        if (SmallestSize != 1 || LargestSize != 1)
        {
            placed *= Transforms.Scale(
                SmallestSize + (LargestSize - SmallestSize) * DrawFor(seed, row, column, SizeDraw));
        }

        return placed;
    }

    /// <summary>
    /// This method returns which of the things to stand a draw picks, each as often as its weight says.
    /// </summary>
    private static int Picked(List<FieldChoice> choices, double draw)
    {
        for (int index = 0; index < choices.Count - 1; index++)
        {
            draw -= choices[index].Weight;

            if (draw < 0)
                return index;
        }

        return choices.Count - 1;
    }

    /// <summary>
    /// This method returns the order the things to stand are taken in, for a field that takes them in
    /// turn: each as many times running as its weight, rounded to a whole number and at least once.
    /// </summary>
    private static int[] TurnsOf(List<FieldChoice> choices)
    {
        List<int> turns = [];

        for (int index = 0; index < choices.Count; index++)
            turns.AddRange(Enumerable.Repeat(index, Math.Max(1, (int) Math.Round(choices[index].Weight))));

        return turns.ToArray();
    }

    /// <summary>
    /// This method returns a number from nought to one for one place of the grid, as good as random and
    /// the same every time.
    /// <para>
    /// **It comes from the place, not from the order places are filled in.**  Drawn one after another,
    /// what stood at a place would depend on how many places before it fell inside the outline, and a
    /// pond cut out of a wood would turn and swap every tree beyond it.  Drawn from the place, a tree
    /// keeps its kind, its turn and its size whatever is cut away around it.  The jitter is drawn in
    /// order, as it always has been, so a field asked for none of this stands exactly where it did.
    /// </para>
    /// </summary>
    private static double DrawFor(int seed, int row, int column, int purpose)
    {
        ulong bits = (ulong) (uint) seed * 0x9E3779B97F4A7C15UL;

        bits ^= (ulong) (uint) row * 0xC2B2AE3D27D4EB4FUL + (ulong) (uint) column * 0x165667B19E3779F9UL +
                (ulong) purpose * 0xD6E8FEB86659FD93UL;
        bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
        bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;
        bits ^= bits >> 31;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }
}

/// <summary>
/// This record holds one of the things a field may stand at a place: how to make it, given a copy's
/// number, and how often it is picked against the others.
/// </summary>
/// <param name="Make">How to make it.</param>
/// <param name="Weight">How often it is picked, against the others' weights.</param>
public sealed record FieldChoice(Func<int, Surface> Make, double Weight = 1);
