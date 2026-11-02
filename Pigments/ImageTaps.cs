using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This struct holds one reading of a picture that makes up part of what is seen at a place: where in the picture it
/// is read, in texels; how fast that place moves across and down the picture as the point moves, in texels a unit
/// along each axis of the space being read (from which a footprint's reach across the picture, a bump's slope and a
/// normal map's frame are all found); and how much of the whole it counts for.
/// </summary>
internal readonly struct ImageTap
{
    internal double U { get; init; }
    internal double V { get; init; }
    internal double AcrossX { get; init; }
    internal double AcrossY { get; init; }
    internal double AcrossZ { get; init; }
    internal double DownX { get; init; }
    internal double DownY { get; init; }
    internal double DownZ { get; init; }

    /// <summary>
    /// This property holds how much the projection this reading belongs to counts for, out of one.
    /// </summary>
    internal double Projection { get; init; }

    /// <summary>
    /// This property holds how much this reading counts for among the others of its projection, out of one.
    /// </summary>
    internal double Tile { get; init; }

    /// <summary>
    /// This property notes which projection the reading belongs to; the readings of one stand together.
    /// </summary>
    internal int Group { get; init; }

    /// <summary>
    /// This property holds how far across the picture, in texels, a unit step along each axis moves the reading.
    /// </summary>
    internal Vector Across => new (AcrossX, AcrossY, AcrossZ);

    /// <summary>
    /// This property holds how far down the picture, in texels, a unit step along each axis moves the reading.
    /// </summary>
    internal Vector Down => new (DownX, DownY, DownZ);

    /// <summary>
    /// This method returns the picture's color at this reading, averaged over the patch a footprint covers, or read
    /// between its four nearest texels where there is none.  These pictures are laid over a whole plane, and repeat
    /// both ways.
    /// </summary>
    /// <param name="texture">The picture.</param>
    /// <param name="footprint">The patch the ray covers, in the space being read, if any.</param>
    /// <param name="u">How far across the picture to move the reading first, in texels.</param>
    /// <param name="v">How far down the picture to move the reading first, in texels.</param>
    /// <returns>The color there.</returns>
    internal Color ColorIn(Texture texture, Footprint footprint, double u = 0, double v = 0)
    {
        if (footprint is null || footprint.IsEmpty)
            return texture.Bilinear(U + u, V + v, TextureWrap.Repeat, TextureWrap.Repeat);

        Vector across = footprint.Across, along = footprint.Along;

        return texture.Filtered(
            U + u, V + v,
            AcrossX * across.X + AcrossY * across.Y + AcrossZ * across.Z,
            DownX * across.X + DownY * across.Y + DownZ * across.Z,
            AcrossX * along.X + AcrossY * along.Y + AcrossZ * along.Z,
            DownX * along.X + DownY * along.Y + DownZ * along.Z,
            TextureWrap.Repeat, TextureWrap.Repeat);
    }

    /// <summary>
    /// This method returns how far, in texels, a footprint reaches across and down the picture at this reading, at
    /// its farthest, or a texel each way where it reaches less or there is none.
    /// </summary>
    /// <param name="footprint">The patch the ray covers, in the space being read, if any.</param>
    /// <returns>How far it reaches across and down.</returns>
    internal (double Across, double Down) ReachOf(Footprint footprint)
    {
        if (footprint is null || footprint.IsEmpty)
            return (1, 1);

        Vector across = Across, down = Down;

        return (
            Math.Max(1, Math.Max(Math.Abs(across.Dot(footprint.Across)), Math.Abs(across.Dot(footprint.Along))) / 2),
            Math.Max(1, Math.Max(Math.Abs(down.Dot(footprint.Across)), Math.Abs(down.Dot(footprint.Along))) / 2));
    }
}

/// <summary>
/// This class works out the readings a picture laid over a whole plane is made of at a place, for the two ways of
/// laying one that read it more than once: triplanar, and shuffled.
/// <para>
/// **Triplanar** lays the picture flat three times, once square to each of the axes of the space it is read in, and
/// each place takes the three by how squarely its surface faces each axis.  A cliff takes the picture laid on its
/// side, a ledge the one laid flat, and the turn between them blends rather than smears -- so a boulder, a trunk or
/// a hillside wears a picture with no seam anywhere and no stretch, which no map round a shape can give a shape it was
/// not made for.  It is laid at the world's size (see <see cref="Bearing"/>): along the space's own axes, so it
/// turns and moves with the thing, but a unit across it is a unit in the world, however the thing is stretched.  On
/// a face square to an axis it stands upright, its rows running down, and is not mirrored on the far side.
/// </para>
/// <para>
/// **Shuffled** lays a picture that does not repeat (Mikkelsen 2022, "Practical Real-Time Hex-Tiling", after Heitz
/// and Neyret).  The plane is cut into hexagons, each taking the picture shifted by an amount of its own -- turned
/// too, if asked -- and the three about any place are blended by how near the place stands to each, sharpened so
/// that most of the plane is one tile's alone, and leaning toward whichever is brighter there, so that where two
/// pebbles meet one lies over the other rather than both showing through.  Every place shows an unbroken patch of
/// the real picture, and the grid a repeated picture draws across a floor is gone.
/// </para>
/// </summary>
internal static class ImageTaps
{
    /// <summary>
    /// This is the most readings a place can need: three projections of three tiles.
    /// </summary>
    internal const int Most = 9;

    /// <summary>
    /// This is how far a shuffled picture's blending leans toward the brighter tile: Mikkelsen's choice.
    /// </summary>
    internal const double Contrast = 0.6;

    /// <summary>
    /// This is how wide a triplanar picture's turns blend unless told: its weights go as the fourth power of how
    /// squarely the surface faces each axis.
    /// </summary>
    internal const double DefaultBlend = 0.25;

    // How sharply a shuffled picture's tiles give way to one another: their weights go as this power of how near the
    // place stands to each.  Mikkelsen's choice.
    private const double Sharpening = 7;

    // A share too small to be worth a reading, of a projection or of a tile among its fellows.
    private const double Negligible = 1e-3;

    private static readonly double RootThree = Math.Sqrt(3);

    /// <summary>
    /// This method gathers the readings a picture is made of at a point.
    /// </summary>
    /// <param name="taps">Where to put them; it must hold <see cref="Most"/>.</param>
    /// <param name="point">The point, in the space being read.</param>
    /// <param name="triplanar">Whether the picture is laid triplanar, rather than planar.</param>
    /// <param name="blend">How wide a triplanar picture's turns blend, from more than nought to one.</param>
    /// <param name="shuffle">How the picture is shuffled, or <c>null</c> if it is not.</param>
    /// <param name="seed">The seed its shuffling is drawn from.</param>
    /// <param name="width">How many texels the picture has across.</param>
    /// <param name="height">How many texels the picture has down.</param>
    /// <returns>How many readings there are.</returns>
    internal static int Gather(
        Span<ImageTap> taps, Point point, bool triplanar, double blend, Shuffle shuffle, int seed, int width,
        int height)
    {
        if (!triplanar)
        {
            // Planar: the picture across X and down Z, a unit square to the whole of it.
            return AddTiles(
                taps, 0, point.X, point.Z, new Vector(1, 0, 0), new Vector(0, 0, 1), 1, 0, shuffle, seed, width,
                height);
        }

        Measure measure = Measure.From(Bearing.Current);
        (double qx, double qy, double qz) = measure.Apply(point);
        Vector facing = measure.Facing;
        double fx = 1, fy = 1, fz = 1;

        // Read with no facing to go by -- a strand's root, a shadow ray through a picture's clear parts -- the three
        // count alike.
        if (facing is not null)
        {
            double power = 1 / blend;

            fx = Math.Pow(Math.Abs(facing.X), power);
            fy = Math.Pow(Math.Abs(facing.Y), power);
            fz = Math.Pow(Math.Abs(facing.Z), power);
        }

        double total = fx + fy + fz;

        if (!(total > 0))
            (fx, fy, fz, total) = (1, 1, 1, 3);

        (fx, fy, fz) = (fx / total, fy / total, fz / total);

        // Shares too small to be worth reading are dropped, and the rest made to add to one again.
        double kept = (fx < Negligible ? 0 : fx) + (fy < Negligible ? 0 : fy) + (fz < Negligible ? 0 : fz);
        double sx = facing is null || facing.X >= 0 ? 1 : -1;
        double sy = facing is null || facing.Y >= 0 ? 1 : -1;
        double sz = facing is null || facing.Z >= 0 ? 1 : -1;
        int count = 0;

        // Square to X: across the picture runs along Z -- the far way on the face toward +X, so that it is not
        // mirrored -- and down it runs down Y.
        if (fx >= Negligible)
        {
            count = AddTiles(
                taps, count, -sx * qz, -qy, measure.Back(new Vector(0, 0, -sx)), measure.Back(new Vector(0, -1, 0)),
                fx / kept, 0, shuffle, seed, width, height);
        }

        // Square to Y: across X and down Z, as a planar picture lies on a floor.
        if (fy >= Negligible)
        {
            count = AddTiles(
                taps, count, sy * qx, qz, measure.Back(new Vector(sy, 0, 0)), measure.Back(new Vector(0, 0, 1)),
                fy / kept, 1, shuffle, seed, width, height);
        }

        // Square to Z: across X -- the far way on the face toward -Z -- and down Y.
        if (fz >= Negligible)
        {
            count = AddTiles(
                taps, count, sz * qx, -qy, measure.Back(new Vector(sz, 0, 0)), measure.Back(new Vector(0, -1, 0)),
                fz / kept, 2, shuffle, seed, width, height);
        }

        return count;
    }

    /// <summary>
    /// This method adds the readings of one projection: one, for a picture that is not shuffled, or the tiles about
    /// the place for one that is.
    /// </summary>
    /// <param name="taps">Where to put them.</param>
    /// <param name="count">How many are there already.</param>
    /// <param name="s">How far across the picture the place lies, in pictures.</param>
    /// <param name="t">How far down the picture the place lies, in pictures.</param>
    /// <param name="across">How fast <paramref name="s"/> grows along each axis of the space being read.</param>
    /// <param name="down">How fast <paramref name="t"/> grows along each axis.</param>
    /// <param name="share">How much the projection counts for.</param>
    /// <param name="group">Which projection it is.</param>
    /// <param name="shuffle">How the picture is shuffled, or <c>null</c>.</param>
    /// <param name="seed">The seed its shuffling is drawn from.</param>
    /// <param name="width">How many texels the picture has across.</param>
    /// <param name="height">How many texels the picture has down.</param>
    /// <returns>How many readings there are now.</returns>
    private static int AddTiles(
        Span<ImageTap> taps, int count, double s, double t, Vector across, Vector down, double share, int group,
        Shuffle shuffle, int seed, int width, int height)
    {
        if (shuffle is null)
        {
            taps[count] = TapAt(s, t, across, down, share, 1, group, width, height);

            return count + 1;
        }

        // The tiles' middles stand on a grid of equilateral triangles, and a place lies in one triangle, among the
        // three middles at its corners: skewed, the grid is the whole numbers, and how near the place is to each
        // corner is read off the fractions.
        double x = s / shuffle.Size, y = t / shuffle.Size;
        double kx = x - y / RootThree, ky = 2 * y / RootThree;
        double bx = Math.Floor(kx), by = Math.Floor(ky);
        double ax = kx - bx, ay = ky - by, az = 1 - ax - ay;
        Span<(double X, double Y, double Near)> corners = stackalloc (double, double, double)[3];

        if (az > 0)
        {
            corners[0] = (bx, by, az);
            corners[1] = (bx, by + 1, ay);
            corners[2] = (bx + 1, by, ax);
        }
        else
        {
            corners[0] = (bx + 1, by + 1, -az);
            corners[1] = (bx + 1, by, 1 - ay);
            corners[2] = (bx, by + 1, 1 - ax);
        }

        Span<double> pulls = stackalloc double[3];
        double strongest = 0, sum = 0;

        for (int index = 0; index < 3; index++)
        {
            pulls[index] = Math.Pow(Math.Max(0, corners[index].Near), Sharpening);
            strongest = Math.Max(strongest, pulls[index]);
        }

        for (int index = 0; index < 3; index++)
        {
            if (pulls[index] < strongest * Negligible)
                pulls[index] = 0;

            sum += pulls[index];
        }

        for (int index = 0; index < 3; index++)
        {
            if (pulls[index] == 0)
                continue;

            (double cornerX, double cornerY, _) = corners[index];
            long column = (long) cornerX, row = (long) cornerY;

            // Each tile shows the picture shifted by an amount of its own, turned about its middle if asked: drawn from
            // the tile, never in order, so the same tile shows the same patch whoever asks.
            double middleS = shuffle.Size * (cornerX + cornerY / 2);
            double middleT = shuffle.Size * (cornerY * RootThree / 2);
            double angle = shuffle.Turn == 0 ? 0 : shuffle.Turn * (2 * DrawFor(seed, column, row, group * 3 + 2) - 1);
            double cos = Math.Cos(angle), sin = Math.Sin(angle);
            double ds = s - middleS, dt = t - middleT;
            double turnedS = cos * ds - sin * dt + middleS + DrawFor(seed, column, row, group * 3);
            double turnedT = sin * ds + cos * dt + middleT + DrawFor(seed, column, row, group * 3 + 1);

            taps[count++] = TapAt(
                turnedS, turnedT, across * cos - down * sin, across * sin + down * cos, share, pulls[index] / sum,
                group, width, height);
        }

        return count;
    }

    /// <summary>
    /// This method returns the reading at a place in the picture, given in pictures, and how fast it moves.
    /// </summary>
    private static ImageTap TapAt(
        double s, double t, Vector across, Vector down, double share, double tile, int group, int width, int height)
    {
        return new ImageTap
        {
            U = s * width, V = t * height,
            AcrossX = across.X * width, AcrossY = across.Y * width, AcrossZ = across.Z * width,
            DownX = down.X * height, DownY = down.Y * height, DownZ = down.Z * height,
            Projection = share, Tile = tile, Group = group
        };
    }

    /// <summary>
    /// This method returns the picture's color made up of its readings: each projection's tiles weighed as they
    /// count, leaning toward the brighter by the contrast given, and the projections weighed as they count.
    /// </summary>
    /// <param name="texture">The picture.</param>
    /// <param name="taps">The readings.</param>
    /// <param name="footprint">The patch the ray covers, in the space being read, if any.</param>
    /// <param name="contrast">How far the tiles lean toward the brighter, from nought to one.</param>
    /// <returns>The color.</returns>
    internal static Color Read(Texture texture, ReadOnlySpan<ImageTap> taps, Footprint footprint, double contrast)
    {
        double red = 0, green = 0, blue = 0, alpha = 0;
        int index = 0;

        while (index < taps.Length)
        {
            int group = taps[index].Group;
            double share = taps[index].Projection;
            double r = 0, g = 0, b = 0, a = 0, total = 0;

            for (; index < taps.Length && taps[index].Group == group; index++)
            {
                Color color = taps[index].ColorIn(texture, footprint);
                double weight = taps[index].Tile;

                if (contrast != 0)
                {
                    weight *= 1 - contrast +
                              contrast * (0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue);
                }

                r += weight * color.Red;
                g += weight * color.Green;
                b += weight * color.Blue;
                a += weight * color.Alpha;
                total += weight;
            }

            if (total > 0)
            {
                red += share * r / total;
                green += share * g / total;
                blue += share * b / total;
                alpha += share * a / total;
            }
        }

        return new Color(red, green, blue, alpha);
    }

    /// <summary>
    /// This method returns a number from nought to one for one tile of a shuffled picture, as good as random and the
    /// same every time.
    /// </summary>
    private static double DrawFor(int seed, long column, long row, int purpose)
    {
        ulong bits = (ulong) (uint) seed * 0x9E3779B97F4A7C15UL;

        bits ^= (ulong) column * 0xC2B2AE3D27D4EB4FUL + (ulong) row * 0x165667B19E3779F9UL +
                (ulong) purpose * 0xD6E8FEB86659FD93UL;
        bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
        bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;
        bits ^= bits >> 31;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// This struct holds how a triplanar picture measures the space it is read in: the map from a point there to the
    /// same point at the world's size along that space's own axes, and which way the surface faces at that size.
    /// <para>
    /// The surface's own axes, carried into the space being read, are the columns of A, so the space's own axes in
    /// the surface's are the rows of A's inverse, B; stretching the surface's axes to their lengths in the world and
    /// carrying the point back, the map is A S B.  A normal goes by the transposed inverse of the map a point goes
    /// by, which comes to the sum of B's rows, each by how much the surface faces along that axis over its length.
    /// </para>
    /// </summary>
    private readonly struct Measure
    {
        private readonly double _xx, _xy, _xz, _yx, _yy, _yz, _zx, _zy, _zz;

        /// <summary>
        /// This property holds which way the surface faces, in the measured space, or <c>null</c> where that is not
        /// known.
        /// </summary>
        internal Vector Facing { get; }

        private Measure(Vector x, Vector y, Vector z, Vector facing)
        {
            (_xx, _xy, _xz) = (x.X, x.Y, x.Z);
            (_yx, _yy, _yz) = (y.X, y.Y, y.Z);
            (_zx, _zy, _zz) = (z.X, z.Y, z.Z);
            Facing = facing;
        }

        /// <summary>
        /// This method returns how the given bearing measures the space it stands in: unmeasured, for none.
        /// </summary>
        internal static Measure From(Bearing bearing)
        {
            if (bearing is null)
                return new Measure(new Vector(1, 0, 0), new Vector(0, 1, 0), new Vector(0, 0, 1), null);

            Vector a = bearing.AlongX, b = bearing.AlongY, c = bearing.AlongZ;
            Vector bc = b.Cross(c), ca = c.Cross(a), ab = a.Cross(b);
            double determinant = a.Dot(bc);
            Vector stretch = bearing.Stretch;
            double sx = stretch.X > 0 ? stretch.X : 1, sy = stretch.Y > 0 ? stretch.Y : 1, sz = stretch.Z > 0 ? stretch.Z : 1;

            if (Math.Abs(determinant) < 1e-12)
                return new Measure(new Vector(1, 0, 0), new Vector(0, 1, 0), new Vector(0, 0, 1), null);

            // The rows of B.
            Vector rx = bc / determinant, ry = ca / determinant, rz = ab / determinant;

            // Row j of A S B is the sum over the surface's axes of A's jth entry, by the stretch, by B's row.
            Vector row0 = rx * (a.X * sx) + ry * (b.X * sy) + rz * (c.X * sz);
            Vector row1 = rx * (a.Y * sx) + ry * (b.Y * sy) + rz * (c.Y * sz);
            Vector row2 = rx * (a.Z * sx) + ry * (b.Z * sy) + rz * (c.Z * sz);
            Vector facing = bearing.Facing;
            Vector turned = rx * (facing.X / sx) + ry * (facing.Y / sy) + rz * (facing.Z / sz);

            return new Measure(row0, row1, row2, turned.Magnitude > 0 ? turned.Unit : null);
        }

        /// <summary>
        /// This method returns the point at the world's size.
        /// </summary>
        internal (double X, double Y, double Z) Apply(Point point) => (
            _xx * point.X + _xy * point.Y + _xz * point.Z,
            _yx * point.X + _yy * point.Y + _yz * point.Z,
            _zx * point.X + _zy * point.Y + _zz * point.Z);

        /// <summary>
        /// This method returns how fast a measure along the given direction at the world's size grows as the point
        /// moves along each axis of the space being read: the map's transpose.
        /// </summary>
        internal Vector Back(Vector direction) => new (
            _xx * direction.X + _yx * direction.Y + _zx * direction.Z,
            _xy * direction.X + _yy * direction.Y + _zy * direction.Z,
            _xz * direction.X + _yz * direction.Y + _zz * direction.Z);
    }
}
