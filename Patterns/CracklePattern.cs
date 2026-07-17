using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the crackle pattern -- a Voronoi, or cellular, pattern.  Space is cut into
/// unit cells, each holding one feature point, and every position takes its value from how far it
/// lies from the nearest of those points relative to the second nearest.  That difference falls to
/// zero exactly where two feature points are equally close, which is to say along the boundaries
/// between cells, so a colour map running dark at its low end draws a web of cracks through an
/// otherwise smooth surface.
///
/// It is the pattern for anything that dried, cooled or grew into cells: cracked mud, stone,
/// paving, leather, reptile skin, mosaic.
/// </summary>
public class CracklePattern : Pattern, INoiseConsumer
{
    /// <summary>
    /// This property holds the seed for the feature points to use.
    /// If it is not specified, a default one will be used.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        int cellX = (int) Math.Floor(point.X);
        int cellY = (int) Math.Floor(point.Y);
        int cellZ = (int) Math.Floor(point.Z);
        double nearest = double.MaxValue;
        double secondNearest = double.MaxValue;

        // Only the cell holding the point and the twenty-six touching it can own either of the
        // two closest feature points: any point in a cell further out than that is further away
        // than the whole of this cell's own diagonal, so it cannot beat what is found here.
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            Point feature = FeaturePointIn(cellX + dx, cellY + dy, cellZ + dz);
            double distance = new Vector(
                point.X - feature.X, point.Y - feature.Y, point.Z - feature.Z).Magnitude;

            if (distance < nearest)
            {
                secondNearest = nearest;
                nearest = distance;
            }
            else if (distance < secondNearest)
                secondNearest = distance;
        }

        // The gap between the two closest, which is zero on a cell boundary and widest deep
        // inside a cell.  It cannot exceed 1 for feature points scattered one to a unit cell, but
        // clamp anyway rather than hand a colour map something it would quietly wrap.
        double value = secondNearest - nearest;

        return value > 1 ? 1 : value;
    }

    /// <summary>
    /// This method returns the feature point belonging to the given cell.  It has to be a pure
    /// function of the cell's coordinates: neighbouring positions ask about the same cell and
    /// must be told the same thing, or the cells would shift underfoot and the boundaries between
    /// them dissolve.
    /// </summary>
    /// <param name="x">The X coordinate of the cell.</param>
    /// <param name="y">The Y coordinate of the cell.</param>
    /// <param name="z">The Z coordinate of the cell.</param>
    /// <returns>The cell's feature point.</returns>
    private Point FeaturePointIn(int x, int y, int z)
    {
        uint hash = Hash(x, y, z, Seed ?? 0);

        return new Point(
            x + Fraction(hash),
            y + Fraction(hash = Hash((int) hash, y, z, x)),
            z + Fraction(Hash((int) hash, z, x, y)));
    }

    /// <summary>
    /// This method hashes four integers down to one.
    ///
    /// The obvious move here would be to reach for the Perlin noise this project already carries,
    /// and it is a trap: gradient noise is zero at every integer lattice point by construction, so
    /// asking it about cell corners would hand back the same nothing for every cell, and every
    /// feature point would land dead centre.  Sampling cell centres instead dodges that, but
    /// leaves neighbouring samples a single unit apart, close enough to correlate -- and visible
    /// regularity is precisely the flaw a cellular pattern cannot afford.  Hence integer hashing,
    /// which has no lattice to be caught on.
    /// </summary>
    /// <param name="a">The first value to hash.</param>
    /// <param name="b">The second value to hash.</param>
    /// <param name="c">The third value to hash.</param>
    /// <param name="d">The fourth value to hash.</param>
    /// <returns>The hash of the four values.</returns>
    private static uint Hash(int a, int b, int c, int d)
    {
        // A mixing round in the style of Wang and Jenkins: multiply by large odd constants so
        // every input bit reaches the high bits, and xor-shift so the high bits fold back down
        // into the low ones.  Nothing here is subtle; it just has to scatter well and never
        // repeat itself.
        unchecked
        {
            uint hash = (uint) a * 0x8DA6B343u
                      + (uint) b * 0xD8163841u
                      + (uint) c * 0xCB1AB31Fu
                      + (uint) d * 0x165667B1u;

            hash ^= hash >> 15;
            hash *= 0x2C1B3C6Du;
            hash ^= hash >> 12;
            hash *= 0x297A2D39u;
            hash ^= hash >> 15;

            return hash;
        }
    }

    /// <summary>
    /// This method turns a hash into a number in the [0, 1) interval.
    /// </summary>
    /// <param name="hash">The hash to convert.</param>
    /// <returns>The hash, as a number in the [0, 1) interval.</returns>
    private static double Fraction(uint hash)
    {
        return hash / 4294967296.0;
    }
}
