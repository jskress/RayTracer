using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a field: an area of the X/Z plane, given by a 2D outline, filled with
/// copies of one surface -- or a line in it, given by a path, laid with them.
/// <para>
/// It is a compound surface, like <see cref="TextSolid"/> and <see cref="LSystem"/> -- it builds its
/// own children when it is prepared, from a description rather than from a list.  What that buys is
/// an area of *any shape*: a patch of grass cut to a verge, a border, an island in a roundabout.
/// Before it there were two shapes to be had, a square and a circle, and a scene wanting anything
/// else had to write a loop and a containment test of its own.
/// </para>
/// <para>
/// **It is not only for grass.**  Balls in a pit, a stand of trees, soldiers drawn up in ranks --
/// anything that is one thing in many places over an area, and none of it needing a loop.  And along
/// a line: fence posts, a ring of standing stones, seats round a curved terrace.
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
    /// This property holds the outline, in the X/Z plane, that the field fills, or <c>null</c> for a
    /// field that follows a path instead.
    /// </summary>
    public GeneralPath Outline { get; set; }

    /// <summary>
    /// This property holds the path, in the X/Z plane, that the field lays its copies along, or
    /// <c>null</c> for a field that fills an outline instead.  Each of the path's runs is laid
    /// separately.
    /// <para>
    /// **Each copy is turned to run with the path where it stands**: its own X runs the way the path
    /// does, so what faces -Z as made faces the path's right as you walk it, and what faces +Z its left.
    /// A bench or a length of railing follows a curve, and a round post or a tree is none the worse.
    /// </para>
    /// <para>
    /// **The spacing is fitted to the length**: a whole number of gaps, each the spacing asked or a
    /// little more, so that an open path has a copy at both ends -- the posts of a fence -- and a closed
    /// one goes round with no two copies at its seam.  A run whose end meets its start is closed, however
    /// it was drawn.
    /// </para>
    /// </summary>
    public GeneralPath Along { get; set; }

    /// <summary>
    /// This property holds how many rows a field following a path lays, side by side.  Each is spaced
    /// along its own length, so the outside of a bend holds more than the inside.
    /// </summary>
    public int Rows { get; set; } = 1;

    /// <summary>
    /// This property holds how far apart a field's rows are, across the path.
    /// </summary>
    public double Apart { get; set; }

    /// <summary>
    /// This property holds how far to the path's left, as it is walked, the middle of a field's rows
    /// lies; to its right, if less than nought.  Nought centers them on the path.
    /// </summary>
    public double Offset { get; set; }

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
        if (Outline is null && Along is null)
            throw new Exception("A field needs an outline to fill or a path to follow.");

        if (Outline is not null && Along is not null)
            throw new Exception("A field fills an outline or follows a path, not both.");

        if (Outline is not null && (Rows != 1 || Offset != 0))
            throw new Exception("A field's rows and offset are for one that follows a path, not one that fills an outline.");

        if (Rows < 1)
            throw new Exception("A field must lay at least one row.");

        if (Rows > 1 && Apart <= 0)
            throw new Exception("A field's rows must be some distance apart.");

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
    /// This method stands a copy at every place of the grid that falls inside the outline, or at every
    /// stop along the path: on the field's own ground, or, given a surface to drop onto, wherever each
    /// lands on it.
    /// </summary>
    /// <param name="ground">The surface to drop the copies onto, or <c>null</c> to stand them on the
    /// field's own ground.</param>
    private void Fill(Surface ground)
    {
        List<FieldChoice> choices = Choices.Count > 0 ? Choices : [new FieldChoice(Prototype)];
        int seed = Seed ?? DefaultSeed;
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

        foreach (Place place in Along is null ? GridPlaces(seed) : PathPlaces(seed))
        {
            // Dropped before anything is made, so that a place standing nothing costs only the drop:
            // most of a wood filling a hill's whole square falls in the lake or above the tree line.
            Matrix landing = null;

            if (dropping is not null && (landing = dropping.LandingAt(place.X, place.Z)) is null)
                continue;

            int pick = choices.Count == 1 ? 0
                : turns is not null ? turns[(place.Row + place.Column) % turns.Length]
                : Picked(choices, totalWeight * DrawFor(seed, place.Row, place.Column, PickDraw));
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
                ? PlacedAt(place, seed) * copy.Transform
                : landing * TurnAndSizeAt(place, seed) * copy.Transform;

            Add(copy);
        }
    }

    /// <summary>
    /// This record holds one place a copy may stand: where, which row and column of the field it is,
    /// for the draws that decide what stands there, and which way the path runs there, in radians about
    /// the up axis, for a field that follows one.
    /// </summary>
    private readonly record struct Place(double X, double Z, int Row, int Column, double? Facing = null);

    /// <summary>
    /// This method returns the places of the grid that fall inside the outline.
    /// </summary>
    /// <param name="seed">The seed the jitter is drawn from.</param>
    /// <returns>The places, row by row.</returns>
    private IEnumerable<Place> GridPlaces(int seed)
    {
        Random random = new Random(seed);

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
                if (Outline.Contains(new TwoDPoint(x, z)))
                    yield return new Place(x, z, row, column);
            }
        }
    }

    /// <summary>
    /// This method returns the stops along the path: along each of its runs, in each row.
    /// <para>
    /// **The jitter is drawn from the place**, along the path and across it, rather than in order as the
    /// grid's is: the grid's has always been drawn in order and fields that ask for nothing else stand
    /// where they always stood, but a path has no such history to keep.
    /// </para>
    /// </summary>
    /// <param name="seed">The seed the draws are made from.</param>
    /// <returns>The places, run by run and row by row.</returns>
    private IEnumerable<Place> PathPlaces(int seed)
    {
        List<SubPath> runs = Along.FindAllSubPaths();

        if (runs.Count == 0)
            throw new Exception("A path to lay a field along has to have something in it.");

        for (int run = 0; run < runs.Count; run++)
        {
            GeneralPath path = runs[run].Path;
            PathGuide guide = new PathGuide(path);
            bool closed = IsClosed(path);

            for (int lane = 0; lane < Rows; lane++)
            {
                double aside = Offset + (lane - (Rows - 1) / 2.0) * Apart;
                int row = run * Rows + lane;
                double[] stops = StopsAlong(guide, aside, closed);

                for (int column = 0; column < stops.Length; column++)
                {
                    double along = stops[column];
                    double across = aside;

                    if (Jitter > 0)
                    {
                        along += (DrawFor(seed, row, column, AlongDraw) - 0.5) * Spacing * Jitter;
                        across += (DrawFor(seed, row, column, AcrossDraw) - 0.5) * Spacing * Jitter;
                    }

                    // Round a closed run rather than off its end.
                    if (closed)
                        along -= Math.Floor(along / guide.Length) * guide.Length;

                    (TwoDPoint point, TwoDVector way) = guide.At(along);

                    // The path's left, as it is walked, is a quarter turn anticlockwise from the way it
                    // runs; and a copy's X is turned onto the way it runs.
                    yield return new Place(
                        point.X - way.Y * across, point.Y + way.X * across, row, column,
                        Math.Atan2(-way.Y, way.X));
                }
            }
        }
    }

    /// <summary>
    /// This method returns whether a run of a path ends where it starts.
    /// </summary>
    private static bool IsClosed(GeneralPath path)
    {
        TwoDPoint start = path.Segments[0].Points[0];
        TwoDPoint end = path.Segments[^1].Points[^1];
        List<TwoDPoint> corners = path.Segments.SelectMany(segment => segment.Points).ToList();
        double size = Math.Max(
            corners.Max(point => point.X) - corners.Min(point => point.X),
            corners.Max(point => point.Y) - corners.Min(point => point.Y));

        return (end - start).Magnitude <= 1e-9 * Math.Max(size, 1);
    }

    /// <summary>
    /// This method returns how far along the path each stop of a row is, for a row standing the given
    /// distance to the path's left.
    /// <para>
    /// **Each row is spaced along its own length**, not the path's: a row on the outside of a bend is
    /// longer than one on the inside, and spaced by the path's own length its copies would stand
    /// further apart than asked there and crowd together on the inside.  So a row beside the path is
    /// measured by walking the path finely and summing how far the row moves, and its stops are carried
    /// back to how far along the path each falls.
    /// </para>
    /// </summary>
    /// <param name="guide">The run of the path, ready to walk.</param>
    /// <param name="aside">How far to the path's left the row stands.</param>
    /// <param name="closed">Whether the run is closed.</param>
    /// <returns>How far along the path each stop is.</returns>
    private double[] StopsAlong(PathGuide guide, double aside, bool closed)
    {
        double[] distances;
        double[] lengths;

        if (aside == 0)
        {
            distances = [0, guide.Length];
            lengths = [0, guide.Length];
        }
        else
        {
            int steps = Math.Max(256, (int) Math.Ceiling(16 * guide.Length / Spacing));

            distances = new double[steps + 1];
            lengths = new double[steps + 1];

            TwoDPoint previous = default;

            for (int step = 0; step <= steps; step++)
            {
                double distance = guide.Length * step / steps;
                (TwoDPoint point, TwoDVector way) = guide.At(distance);
                TwoDPoint beside = new TwoDPoint(point.X - way.Y * aside, point.Y + way.X * aside);

                distances[step] = distance;
                lengths[step] = step == 0 ? 0 : lengths[step - 1] + (beside - previous).Magnitude;
                previous = beside;
            }
        }

        double length = lengths[^1];
        int gaps = Math.Max(1, (int) Math.Floor(length / Spacing + 1e-9));
        int count = closed ? gaps : gaps + 1;
        double[] stops = new double[count];
        int at = 0;

        for (int stop = 0; stop < count; stop++)
        {
            double wanted = length * stop / gaps;

            while (at < lengths.Length - 2 && lengths[at + 1] < wanted)
                at++;

            double span = lengths[at + 1] - lengths[at];
            double fraction = span > 0 ? (wanted - lengths[at]) / span : 0;

            stops[stop] = distances[at] + (distances[at + 1] - distances[at]) * Math.Clamp(fraction, 0, 1);
        }

        return stops;
    }

    // Which of a place's draws is which: each is its own number, so asking for one more of them never
    // changes another.
    private const int PickDraw = 1;
    private const int TurnDraw = 2;
    private const int SizeDraw = 3;
    private const int AlongDraw = 4;
    private const int AcrossDraw = 5;

    /// <summary>
    /// This method returns the transform that stands a copy at a place: turned and sized as the field
    /// says, then moved there.
    /// </summary>
    private Matrix PlacedAt(Place place, int seed)
    {
        Matrix placed = Transforms.Translate(place.X, 0, place.Z);

        if (Turned(place, seed) is { } angle)
            placed *= Transforms.RotateAroundY(angle, true);

        if (SmallestSize != 1 || LargestSize != 1)
            placed *= Transforms.Scale(SizeAt(place, seed));

        return placed;
    }

    /// <summary>
    /// This method returns the turn and size a copy is stood with at a place, without the move there.
    /// </summary>
    private Matrix TurnAndSizeAt(Place place, int seed)
    {
        Matrix shape = Transforms.Scale(1);

        if (Turned(place, seed) is { } angle)
            shape *= Transforms.RotateAroundY(angle, true);

        if (SmallestSize != 1 || LargestSize != 1)
            shape *= Transforms.Scale(SizeAt(place, seed));

        return shape;
    }

    /// <summary>
    /// This method returns how far a copy is turned about the up axis at a place, in radians: the way
    /// the path runs there, if the field follows one, and a share of the field's spread of turns, if it
    /// has one -- or <c>null</c>, if neither, when nothing is turned at all.
    /// </summary>
    private double? Turned(Place place, int seed)
    {
        double? spread = Turn > 0 ? (DrawFor(seed, place.Row, place.Column, TurnDraw) - 0.5) * Turn : null;

        return place.Facing is { } facing ? facing + (spread ?? 0) : spread;
    }

    /// <summary>
    /// This method returns the size a copy is stood at, at a place.
    /// </summary>
    private double SizeAt(Place place, int seed)
    {
        return SmallestSize + (LargestSize - SmallestSize) * DrawFor(seed, place.Row, place.Column, SizeDraw);
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
