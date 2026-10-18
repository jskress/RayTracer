using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds the light bounced onto a scene's matte surfaces, worked out with care at scattered
/// places and blended between them everywhere else (Ward's irradiance cache, as POV-Ray's radiosity
/// keeps one).
/// <para>
/// **Bounced light changes slowly across a surface**, even where the light falling straight on it does
/// not: the pool under a lamp has a hard edge, but the light that pool throws onto the wall beside it
/// is a soft wash.  So it need not be worked out afresh at every point.  Worked out at one place with
/// many directions, it holds for the places around it -- for as far as the surroundings it was gathered
/// from look the same, which is about as far as they are away.  A place among nearby things is good for
/// a little way round it; a place in the open, for a long way.  Each record keeps how far: the harmonic
/// mean of how far its directions went before meeting something, which is ruled by the nearest things.
/// </para>
/// <para>
/// **A place is read from the records near it that face its way**, each weighed by how near it is for
/// its reach and how nearly it faces the same way, and the weights falling to nothing at the edge of a
/// record's reach so that no seam shows where one record gives way to the next.  What it gives is light
/// arriving; the surface's own color still multiplies it at each place, so a wood's grain and a
/// pattern's detail are as sharp as ever.
/// </para>
/// <para>
/// **But only the light from far away.**  How far round it a record holds good is ruled by the things it
/// saw, on the whole -- and a thing that fills little of one place's view may fill most of the view of a
/// place a finger's width away: the floor beside a rug sees the rug's edge low on its horizon, while the
/// floor just under the edge sees nothing but rug.  Blended across, the one lends the other its light,
/// and the dark line where the rug meets the floor is lost.  So a record keeps only the light that came
/// from beyond its <see cref="NearOf">near distance</see> -- twice as far as it holds good for -- as the
/// average of the directions that went that far, and how many of them did.  A place reading the cache
/// looks round itself for whatever is nearer than that, which it alone can see rightly, and takes the
/// blended far light for every direction that finds nothing so near.
/// </para>
/// <para>
/// **It is filled in passes, and read only from what earlier passes filled**, so that a picture comes out
/// the same however the work is shared among threads: places looked at in one pass that no record yet
/// covers each work out a record of their own, all of which join the cache together, in a fixed order,
/// once the pass is over.
/// </para>
/// </summary>
/// <typeparam name="TS">The kind of light the records hold.</typeparam>
public sealed class IrradianceCache<TS>
    where TS : struct, ISpectrum<TS>
{
    /// <summary>
    /// This record holds the light arriving at one place from far away: where it is, which way the
    /// surface there faces, the average light along the directions that met nothing nearer than its near
    /// distance, the share of its directions that did, and how far round it the light holds good.
    /// </summary>
    public readonly record struct Record(Point Place, Vector Facing, TS Far, double Open, double Reach);

    /// <summary>
    /// This record holds what the cache gives a place: the far light along a direction meeting nothing
    /// nearer than <see cref="Near"/>, if any record covering the place had a direction that did, and
    /// how near is near there.
    /// </summary>
    public readonly record struct Blend(TS Far, bool HasFar, double Near);

    // How far a place looks round itself for what is near, in the distances its records hold good for.
    private const double NearReaches = 2;

    /// <summary>
    /// This property holds how far the cache's guesses may stray, as a share of the light: smaller holds
    /// each record good for less far round it, and so makes more of them.
    /// </summary>
    public double Error { get; }

    /// <summary>
    /// This property holds how many records the cache holds.
    /// </summary>
    public int Count => _records.Count;

    private readonly List<Record> _records = [];
    private readonly Dictionary<(int Level, long X, long Y, long Z), List<int>> _cells = new ();
    private readonly SortedSet<int> _levels = [];
    private readonly ConcurrentBag<(long Key, Record Record)> _proposed = [];

    public IrradianceCache(double error)
    {
        Error = error;
    }

    /// <summary>
    /// This method returns how near is near for a record that holds good as far as the given reach
    /// allows: twice as far as it holds good for.
    /// </summary>
    /// <param name="reach">The record's reach.</param>
    /// <returns>The distance within which a place looks for itself.</returns>
    public double NearOf(double reach)
    {
        return NearReaches * Error * reach;
    }

    /// <summary>
    /// This method blends the far light arriving at a place from the records that cover it.  Each counts
    /// for its weight and for the share of its directions that went far, so that a record tucked into a
    /// corner, its far light seen through a chink, says little about it.
    /// </summary>
    /// <param name="place">The place.</param>
    /// <param name="facing">Which way the surface faces there, a unit vector.</param>
    /// <param name="blend">The far light, over π, and how near is near, if any record covers the place.</param>
    /// <returns><c>true</c>, if any did.</returns>
    public bool TryBlend(Point place, Vector facing, out Blend blend)
    {
        TS far = TS.Black;
        double total = 0;
        double open = 0;
        double near = 0;

        foreach (int level in _levels)
        {
            double size = Math.Pow(2, level);
            long cx = (long) Math.Floor(place.X / size);
            long cy = (long) Math.Floor(place.Y / size);
            long cz = (long) Math.Floor(place.Z / size);

            for (long x = cx - 1; x <= cx + 1; x++)
            for (long y = cy - 1; y <= cy + 1; y++)
            for (long z = cz - 1; z <= cz + 1; z++)
            {
                if (!_cells.TryGetValue((level, x, y, z), out List<int> here))
                    continue;

                foreach (int index in here)
                {
                    Record record = _records[index];
                    double weight = WeightOf(record, place, facing);

                    if (weight > 0)
                    {
                        far += record.Far * (weight * record.Open);
                        open += weight * record.Open;
                        near += NearOf(record.Reach) * weight;
                        total += weight;
                    }
                }
            }
        }

        blend = total > 0
            ? new Blend(open > 0 ? far * (1 / open) : TS.Black, open > 0, near / total)
            : default;

        return total > 0;
    }

    /// <summary>
    /// This method weighs one record for a place: Ward's weight -- one over how far off it is for its
    /// reach plus how far it is turned -- less the least weight a record must have to count, so that it
    /// falls smoothly to nothing at the edge of the record's reach.  A record standing in front of the
    /// place, above the surface there, saw the place's surroundings from somewhere the place cannot, and
    /// counts for nothing.
    /// </summary>
    private double WeightOf(Record record, Point place, Vector facing)
    {
        Vector offset = place - record.Place;
        double distance = offset.Magnitude;

        if (distance >= Error * record.Reach)
            return 0;

        double turned = Math.Sqrt(Math.Max(0, 1 - facing.Dot(record.Facing)));
        double apart = distance / record.Reach + turned;

        if (apart >= Error)
            return 0;

        // In front: the record is above the tangent plane at the place, by more than a sliver of its reach.
        if (-offset.Dot((facing + record.Facing) * 0.5) > 0.05 * Error * record.Reach)
            return 0;

        return 1 / Math.Max(apart, 1e-9) - 1 / Error;
    }

    /// <summary>
    /// This method offers a record to join the cache once the pass now running is over.  Records offered
    /// in a pass join in the order of their keys, so the cache is the same however the pass was shared
    /// among threads.
    /// </summary>
    /// <param name="key">Where the record falls in that order.</param>
    /// <param name="record">The record.</param>
    public void Offer(long key, Record record)
    {
        _proposed.Add((key, record));
    }

    /// <summary>
    /// This method ends a pass, setting the records offered during it into the cache in their keys' order.
    /// </summary>
    /// <returns>How many records joined.</returns>
    public int EndPass()
    {
        List<(long Key, Record Record)> offered = [.. _proposed];

        _proposed.Clear();
        offered.Sort((one, other) => one.Key.CompareTo(other.Key));

        foreach ((_, Record record) in offered)
        {
            int index = _records.Count;
            double reach = Error * record.Reach;
            int level = (int) Math.Ceiling(Math.Log2(Math.Max(reach, 1e-12)));
            double size = Math.Pow(2, level);
            (int, long, long, long) cell = (
                level, (long) Math.Floor(record.Place.X / size), (long) Math.Floor(record.Place.Y / size),
                (long) Math.Floor(record.Place.Z / size));

            _records.Add(record);
            _levels.Add(level);

            if (!_cells.TryGetValue(cell, out List<int> here))
                _cells[cell] = here = [];

            here.Add(index);
        }

        return offered.Count;
    }
}
