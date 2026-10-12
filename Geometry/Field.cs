using RayTracer.Basics;
using RayTracer.Core;
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
    /// This property holds the name of the surface the copies are dropped onto, standing beside the
    /// field in the same group: each falls straight down onto it, and onto nothing else, and a place
    /// with nothing of it below stands nothing.  Unsaid, the copies stand on the field's own ground.
    /// </summary>
    public string Over { get; set; }

    /// <summary>
    /// This property notes that a dropped copy leans with the ground under it, as grass and stones do,
    /// rather than standing upright, as trees and people do.
    /// </summary>
    public bool Tilted { get; set; }

    /// <summary>
    /// This property holds the steepest ground a dropped copy may stand on, as an angle from level, in
    /// radians.
    /// </summary>
    public double Steepest { get; set; } = Math.PI / 2;

    /// <summary>
    /// This property holds the lowest ground a dropped copy may stand on, in the space the field shares
    /// with the ground, or <c>null</c> for no limit.
    /// </summary>
    public double? Above { get; set; }

    /// <summary>
    /// This property holds the highest ground a dropped copy may stand on, in the space the field shares
    /// with the ground, or <c>null</c> for no limit.
    /// </summary>
    public double? Below { get; set; }

    /// <summary>
    /// This property holds how far a dropped copy is sunk into the ground, along its own up.  A trunk
    /// standing upright on a slope touches the ground only on its uphill side, and this keeps its
    /// downhill side from standing in the air.
    /// </summary>
    public double Sink { get; set; }

    /// <summary>
    /// This method is called once prior to rendering to fill the outline -- or, for a field to be
    /// dropped onto the ground, to leave it empty until it is dropped, since only then is it known
    /// which places stand anything.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Outline is null)
            throw new Exception("A field needs an outline to fill.");

        if (Choices.Count == 0 && Prototype is null)
            throw new Exception("A field needs something to fill its outline with.");

        if (Spacing <= 0)
            throw new Exception("A field's spacing must be greater than nought.");

        if (Choices.Any(choice => choice.Weight <= 0))
            throw new Exception("A field's weights must be greater than nought.");

        if (SmallestSize <= 0 || LargestSize < SmallestSize)
            throw new Exception("A field's sizes must be greater than nought, the smaller first.");

        Surfaces.Clear();

        if (Over is null)
            Fill(null);

        base.PrepareSurfaceForRendering();
    }

    /// <summary>
    /// This method stands a copy at every place of the grid that falls inside the outline: on the
    /// field's own ground, or, given a surface to drop onto, wherever each lands on it.
    /// </summary>
    /// <param name="ground">The surface to drop the copies onto, or <c>null</c> to stand them on the
    /// field's own ground.</param>
    private void Fill(Surface ground)
    {
        List<FieldChoice> choices = Choices.Count > 0 ? Choices : [new FieldChoice(Prototype)];
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
        Dropping dropping = ground is null ? null : new Dropping(this, ground);

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

                // Dropped before anything is made, so that a place standing nothing costs only the
                // drop: most of a wood filling a hill's whole square falls in the lake or above the
                // tree line.
                Matrix landing = null;

                if (dropping is not null && (landing = dropping.LandingAt(x, z)) is null)
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

                copy.Transform = landing is null
                    ? PlacedAt(x, z, seed, row, column) * copy.Transform
                    : landing * TurnAndSizeAt(seed, row, column) * copy.Transform;

                Add(copy);
            }
        }
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
    /// This method returns the turn and size a copy is stood with at a place, without the move there.
    /// </summary>
    private Matrix TurnAndSizeAt(int seed, int row, int column)
    {
        Matrix shape = Transforms.Scale(1);

        if (Turn > 0)
            shape *= Transforms.RotateAroundY((DrawFor(seed, row, column, TurnDraw) - 0.5) * Turn, true);

        if (SmallestSize != 1 || LargestSize != 1)
        {
            shape *= Transforms.Scale(
                SmallestSize + (LargestSize - SmallestSize) * DrawFor(seed, row, column, SizeDraw));
        }

        return shape;
    }

    /// <summary>
    /// This method fills the field by dropping every copy straight down onto the given surface, which
    /// stands beside the field in the space they share: a copy stands where it lands, upright or leaning
    /// with the ground as the field says, and a place where nothing of the surface lies below, or where
    /// the ground is too steep, too low or too high, stands nothing.
    /// <para>
    /// **It runs when relative placement does**, between the field's group preparing its children and
    /// sorting them, for the same reason: the ground has to have been built and moved to where it stands
    /// first, and nothing may yet have been decided on the strength of where the copies are.  The field
    /// was left empty when it was prepared, and is prepared again here now that it holds something, which
    /// gives it its box.
    /// </para>
    /// </summary>
    /// <param name="ground">The surface to drop the copies onto.</param>
    internal void DropOnto(Surface ground)
    {
        Surfaces.Clear();

        Fill(ground);

        base.PrepareSurfaceForRendering();

        BoundingBox = GetDefaultBoundingBox();
        BoundingBox?.Expand();
    }

    /// <summary>
    /// This class drops a field's copies onto the ground, one place at a time.
    /// <para>
    /// **The slope is found by dropping again, a little way either side**, rather than asked of the
    /// surface: what the ground's own normal says is in the world's space and of whatever part of it was
    /// struck, while a few more drops answer in the very space the copies are stood in, whatever the
    /// ground is made of.
    /// </para>
    /// </summary>
    private sealed class Dropping
    {
        private readonly Field _field;
        private readonly Surface _ground;
        private readonly BoundingBox _box;
        private readonly Matrix _shared;
        private readonly Matrix _own;
        private readonly double _reach;

        /// <summary>
        /// This constructor readies the drops of a field onto the ground.
        /// </summary>
        /// <param name="field">The field being dropped.</param>
        /// <param name="ground">The surface it is dropped onto.</param>
        internal Dropping(Field field, Surface ground)
        {
            BoundingBox box = BoxAround(ground, true);

            _field = field;
            _ground = ground;
            _box = box is { IsEmpty: true } ? null : box;
            _shared = field.Transform;
            _own = field.Transform.Invert();
            _reach = ((new Point(field.Spacing, 0, 0) * _shared) - (Point.Zero * _shared)).Magnitude * 0.05;
        }

        /// <summary>
        /// This method returns where a copy at a place of the field's grid lands, as the move that stands
        /// it there in the field's own space -- leaned and sunk as the field says -- or <c>null</c> if it
        /// stands nothing.
        /// </summary>
        /// <param name="x">Where the place is across the field.</param>
        /// <param name="z">Where the place is along the field.</param>
        /// <returns>The move that stands a copy there, or <c>null</c>.</returns>
        internal Matrix LandingAt(double x, double z)
        {
            Point at = new Point(x, 0, z) * _shared;
            double top = Math.Max(_box?.Maximum.Y ?? 0, at.Y) + 1;
            double? height = HeightOf(_ground, at.X, at.Z, top);

            if (height is null || height < _field.Above || height > _field.Below)
                return null;

            Vector up = UpOf(_ground, at.X, at.Z, height.Value, top, _reach);

            if (Math.Acos(Math.Clamp(up.Y, -1, 1)) > _field.Steepest + 1e-12)
                return null;

            Point landed = new Point(at.X, height.Value, at.Z) * _own;
            Matrix placed = Transforms.Translate(landed.X, landed.Y, landed.Z);

            if (_field.Tilted)
                placed *= Leaning((up * _own).Unit);

            if (_field.Sink != 0)
                placed *= Transforms.Translate(0, -_field.Sink, 0);

            return placed;
        }
    }

    /// <summary>
    /// This method returns the height of the first thing of a surface met dropping straight down at a
    /// place, from the given height, or <c>null</c> if nothing of it lies below.  Strands of hair the
    /// surface grows are passed through: a copy stands on the skin, not on the fur.
    /// </summary>
    private static double? HeightOf(Surface ground, double x, double z, double top)
    {
        List<Intersection> hits = [];
        double nearest = double.PositiveInfinity;

        ground.Intersect(new Ray(new Point(x, top, z), new Vector(0, -1, 0)), hits);

        foreach (Intersection hit in hits)
        {
            if (hit is not HairIntersection && hit.Distance > 0 && hit.Distance < nearest)
                nearest = hit.Distance;
        }

        return double.IsPositiveInfinity(nearest) ? null : top - nearest;
    }

    /// <summary>
    /// This method returns which way is up out of the ground at a place, from where the ground lies a
    /// little way either side of it: as evenly as both sides allow, and from the one side where the
    /// other falls off the ground's edge.
    /// </summary>
    private static Vector UpOf(Surface ground, double x, double z, double height, double top, double reach)
    {
        double Rise(double? before, double? after)
        {
            return (before, after) switch
            {
                ({ } low, { } high) => (high - low) / (2 * reach),
                (null, { } high) => (high - height) / reach,
                ({ } low, null) => (height - low) / reach,
                _ => 0
            };
        }

        double acrossX = Rise(HeightOf(ground, x - reach, z, top), HeightOf(ground, x + reach, z, top));
        double acrossZ = Rise(HeightOf(ground, x, z - reach, top), HeightOf(ground, x, z + reach, top));

        return new Vector(-acrossX, 1, -acrossZ).Unit;
    }

    /// <summary>
    /// This method returns the rotation that tips a copy's up onto the given direction, about the line
    /// square to both, by the angle between them.
    /// </summary>
    private static Matrix Leaning(Vector up)
    {
        double cos = Math.Clamp(up.Y, -1, 1);
        double sin = Math.Sqrt(up.X * up.X + up.Z * up.Z);

        if (sin < 1e-12)
            return Transforms.Scale(1);

        // The axis is up crossed with the new up, of unit length: (z, 0, -x) / sin.
        double kx = up.Z / sin, kz = -up.X / sin;
        double turn = 1 - cos;

        return new Matrix()
            .SetEntry(0, 0, cos + kx * kx * turn)
            .SetEntry(0, 1, -kz * sin)
            .SetEntry(0, 2, kx * kz * turn)
            .SetEntry(1, 0, kz * sin)
            .SetEntry(1, 1, cos)
            .SetEntry(1, 2, -kx * sin)
            .SetEntry(2, 0, kx * kz * turn)
            .SetEntry(2, 1, kx * sin)
            .SetEntry(2, 2, cos + kz * kz * turn);
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
