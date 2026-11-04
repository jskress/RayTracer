namespace RayTracer.Geometry;

/// <summary>
/// This class holds how a height field's ground has been worn by the weather before it is drawn: the rivers that
/// have cut their valleys into it, the crags that have shed scree at their feet and the rain that has run down it,
/// carrying grit from its slopes to its hollows.  Each happens only when it is asked for, and they happen in that
/// order, the largest first.
/// <para>
/// Everything is worked out on the field's grid of heights, before a triangle is made of it, and in the field's
/// own size in the world: an angle of repose is the same angle however the field is stretched.  What it did is kept
/// as three maps a material may read (<see cref="ErosionMap"/>): where the water runs, where grit was laid down,
/// and where ground was carried away.
/// </para>
/// </summary>
public class Erosion
{
    /// <summary>
    /// This property holds how far the rivers have cut back into the land, as shares of its width: nought for none.
    /// </summary>
    public double Rivers { get; set; }

    /// <summary>
    /// This property holds how long the crags have been shedding scree: nought for none.
    /// </summary>
    public double Scree { get; set; }

    /// <summary>
    /// This property holds the steepest angle, in radians, that loose rock comes to rest at, and the sides of a
    /// valley a river cuts fall back to.
    /// </summary>
    public double Repose { get; set; } = 35 * Math.PI / 180;

    /// <summary>
    /// This property holds how many drops of rain run down the ground for each point of its grid: nought for none.
    /// </summary>
    public double Rain { get; set; }

    /// <summary>
    /// This property holds the level, in the field's own heights, below which the ground is under the sea: it does
    /// not wear, and rivers and rain end there.  Unsaid, there is no sea, and water leaves only by the field's edges.
    /// </summary>
    public double? SeaLevel { get; set; }

    /// <summary>
    /// This property holds the seed the rain falls by, or <c>null</c> to take the field's own.
    /// </summary>
    public int? Seed { get; set; }

    // The grounds already worn, kept so that a scene carried out again -- an animation, frame after frame -- does
    // not wear the same land over again.  Keyed by everything the wearing depends on, the heights included.
    private static readonly List<(ErosionKey Key, ErodedGround Ground)> Worn = [];
    private const int Kept = 4;

    /// <summary>
    /// This method wears the given ground as this erosion says.
    /// </summary>
    /// <param name="heights">The heights of the field's grid, in its own units, indexed by column and row.</param>
    /// <param name="width">How wide the field is in the world, along its X axis.</param>
    /// <param name="height">How tall one of its own units of height is in the world.</param>
    /// <param name="depth">How deep the field is in the world, along its Z axis.</param>
    /// <param name="seed">The seed to fall back on, when the erosion says none of its own.</param>
    /// <returns>The ground, worn, and its maps.</returns>
    internal ErodedGround Erode(double[,] heights, double width, double height, double depth, int seed)
    {
        ErosionKey key = new (
            ErosionKey.HashOf(heights), heights.GetLength(0), heights.GetLength(1), width, height, depth,
            Rivers, Scree, Repose, Rain, SeaLevel, Seed ?? seed);

        lock (Worn)
        {
            foreach ((ErosionKey Key, ErodedGround Ground) entry in Worn)
            {
                if (entry.Key == key)
                    return entry.Ground;
            }
        }

        ErodingGround ground = new (heights, width, height, depth, SeaLevel);

        if (Rivers > 0)
            ground.CutRivers(Rivers, Repose);

        if (Scree > 0)
            ground.ShedScree(Scree, Repose);

        if (Rain > 0)
            ground.FallAsRain(Rain, Seed ?? seed);

        ErodedGround eroded = ground.Finished();

        lock (Worn)
        {
            Worn.Insert(0, (key, eroded));

            if (Worn.Count > Kept)
                Worn.RemoveAt(Kept);
        }

        return eroded;
    }

    /// <summary>
    /// This record is what a worn ground is known by: its heights, its size and everything said of its wearing.
    /// </summary>
    private readonly record struct ErosionKey(
        ulong Heights, int Columns, int Rows, double Width, double Height, double Depth,
        double Rivers, double Scree, double Repose, double Rain, double? SeaLevel, int Seed)
    {
        /// <summary>
        /// This method returns a hash of every height of a grid, bit for bit.
        /// </summary>
        /// <param name="heights">The heights to hash.</param>
        /// <returns>Their hash.</returns>
        internal static ulong HashOf(double[,] heights)
        {
            ulong hash = 0xCBF29CE484222325UL;

            foreach (double value in heights)
            {
                hash ^= (ulong) BitConverter.DoubleToInt64Bits(value);
                hash *= 0x100000001B3UL;
                hash ^= hash >> 29;
            }

            return hash;
        }
    }
}

/// <summary>
/// This enumeration names the maps a height field keeps of what its erosion did.
/// </summary>
public enum ErosionMap
{
    /// <summary>
    /// How much water runs over a place: nought on a ridge, rising along a gully to toward one in the largest river.
    /// </summary>
    Flow,

    /// <summary>
    /// How much grit was laid down at a place: nought where none was, a half where about as much as anywhere that
    /// any was, rising toward one where the most was.
    /// </summary>
    Deposits,

    /// <summary>
    /// How much ground was carried away from a place, by the same measure as deposits.
    /// </summary>
    Wear
}

/// <summary>
/// This class holds a ground once worn: its heights, in the field's own units, and the maps of what was done to it,
/// one value for each point of the grid, row by row.  A map not made is <c>null</c>, and reads as nought.
/// </summary>
internal sealed class ErodedGround
{
    /// <summary>
    /// This property holds the heights of the grid, indexed by column and row, in the field's own units.
    /// </summary>
    internal double[,] Heights { get; init; }

    /// <summary>
    /// This property holds the flow map.
    /// </summary>
    internal float[] Flow { get; init; }

    /// <summary>
    /// This property holds the deposits map.
    /// </summary>
    internal float[] Deposits { get; init; }

    /// <summary>
    /// This property holds the wear map.
    /// </summary>
    internal float[] Wear { get; init; }

    /// <summary>
    /// This method returns the given map.
    /// </summary>
    /// <param name="map">The map wanted.</param>
    /// <returns>The map, or <c>null</c> if there is none.</returns>
    internal float[] MapOf(ErosionMap map) => map switch
    {
        ErosionMap.Flow => Flow,
        ErosionMap.Deposits => Deposits,
        _ => Wear
    };
}
