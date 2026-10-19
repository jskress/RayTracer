using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace Tests;

/// <summary>
/// These tests cover the photon map: where photons are kept, and how the light of those near a place is
/// gathered back.
/// </summary>
[TestClass]
public class TestPhotonMap
{
    private static readonly Vector Up = new (0, 1, 0);
    private static readonly Vector Down = new (0, -1, 0);

    // The share of a photon's light Jensen's cone filter keeps, averaged over the area it gathers over.
    private static readonly double ConeNormal = 1 - 2 / (3 * 1.1);

    /// <summary>
    /// This method lays photons on a square grid of the given spacing over the floor, each carrying the
    /// given light, heading straight down onto it.
    /// </summary>
    private static IEnumerable<PhotonMap.Photon> Grid(double spacing, double light, Vector facing, double height = 0)
    {
        int count = (int) Math.Round(2 / spacing);

        for (int x = 0; x < count; x++)
        {
            for (int z = 0; z < count; z++)
            {
                yield return PhotonMap.Photon.At(
                    new Point(-1 + (x + 0.5) * spacing, height, -1 + (z + 0.5) * spacing),
                    new Color(light, light, light), Down, facing);
            }
        }
    }

    /// <summary>
    /// This tests that photons lying evenly over a floor are gathered back as the light they bring: so much
    /// light to a photon, so many photons to the unit of area.
    /// </summary>
    [TestMethod]
    public void TestEvenPhotonsGatherToTheirDensity()
    {
        PhotonMap map = new (Grid(0.01, 0.002, Up), 80, 1);

        foreach (Point place in new Point[] { new (0.003, 0, 0.007), new (-0.4, 0, 0.25), new (0.61, 0, -0.33) })
            Assert.AreEqual(0.002 / (0.01 * 0.01), map.Gather(place, Up, false, _ => 1).Red, 0.02 * 20, $"at {place}");
    }

    /// <summary>
    /// This tests that only the photons that landed on the surface a place is on count there: not those on a
    /// wall standing at it, nor those that landed on the floor's underside, nor those on something lying
    /// above it, unless the surface is a sheet, which is lit from either side.
    /// </summary>
    [TestMethod]
    public void TestOnlyPhotonsOnTheSameSurfaceCount()
    {
        double alone = new PhotonMap(Grid(0.01, 0.002, Up), 80, 1).Gather(new Point(0.003, 0, 0.007), Up, false, _ => 1).Red;
        PhotonMap crowded = new (
            Grid(0.01, 0.002, Up)
                .Concat(Grid(0.01, 1, new Vector(1, 0, 0)))
                .Concat(Grid(0.01, 1, Down))
                .Concat(Grid(0.01, 1, Up, 0.05)), 80, 1);

        Assert.AreEqual(alone, crowded.Gather(new Point(0.003, 0, 0.007), Up, false, _ => 1).Red, 1e-9);

        PhotonMap underneath = new (Grid(0.01, 0.002, Down), 80, 1);

        Assert.AreEqual(0, underneath.Gather(new Point(0.003, 0, 0.007), Up, false, _ => 1).Red, 1e-9);
        Assert.AreEqual(alone, underneath.Gather(new Point(0.003, 0, 0.007), Up, true, _ => 1).Red, 1e-9);
    }

    /// <summary>
    /// This tests a perfect focus: every photon in one point, which the nearest of them would spread over no
    /// area at all.  Gathered over a least radius -- a pixel's width -- the place takes all of their light
    /// over that area, as a pixel's worth of the focus.
    /// </summary>
    [TestMethod]
    public void TestAFocusIsGatheredOverTheLeastArea()
    {
        PhotonMap map = new (
            Enumerable.Range(0, 10_000).Select(_ => PhotonMap.Photon.At(Point.Zero, new Color(1, 1, 1), Down, Up)), 80, 1);

        Assert.AreEqual(
            10_000 / (ConeNormal * Math.PI * 0.1 * 0.1), map.Gather(Point.Zero, Up, false, _ => 1, 0.1).Red, 1e-6);
    }

    /// <summary>
    /// This tests that the surface's say over light arriving at a slant is asked of each photon at its own
    /// slant: photons arriving at 60 degrees, taken at half, give half.
    /// </summary>
    [TestMethod]
    public void TestEachPhotonIsTakenAtItsOwnSlant()
    {
        Vector slanted = new Vector(Math.Sin(Math.PI / 3), -Math.Cos(Math.PI / 3), 0);
        PhotonMap map = new (
            Grid(0.01, 0.002, Up).Select(photon => photon with
            {
                Dx = (float) slanted.X, Dy = (float) slanted.Y, Dz = (float) slanted.Z
            }), 80, 1);
        double square = map.Gather(new Point(0.003, 0, 0.007), Up, false, _ => 1).Red;
        double taken = map.Gather(new Point(0.003, 0, 0.007), Up, false, cosine => cosine).Red;

        Assert.AreEqual(square * 0.5, taken, 1e-6 * square);
    }

    /// <summary>
    /// This tests that the photons a place takes are the nearest that count there, however the tree passes
    /// over branches that can hold none: photons on a rippled floor, on its underside, on both faces of a wall
    /// standing on it, on a slope and on a second floor beyond reach are gathered at places on the floor, the
    /// wall and the slope, one-sided and as sheets, and over a least radius, and each takes exactly what
    /// working through every photon gives it.
    /// </summary>
    [TestMethod]
    public void TestTheNearestAreFoundHoweverTheTreeIsPassed()
    {
        Random random = new (17);
        List<PhotonMap.Photon> photons = [];
        double Across() => random.NextDouble() * 2 - 1;
        double sine = Math.Sin(Math.PI * 60 / 180), cosine = Math.Cos(Math.PI * 60 / 180);
        Vector slope = new (-sine, cosine, 0);
        Point OnTheFloor() => new (Across(), 0, Across());
        Point OnTheWall() => new (0.3, random.NextDouble(), Across());
        Point OnTheSlope()
        {
            double up = random.NextDouble();

            return new Point(-0.5 + up * cosine, up * sine, Across());
        }

        // A floor rippled across X, its facing tilting as far as 32 degrees either way and back every fifth of
        // a unit: a photon on it counts at a place facing straight up if it tilts less than about 25.8 degrees,
        // and the tree's small branches each face about one way, some astride that edge.
        Vector Rippled(Point place, Vector facing)
        {
            double tilt = Math.PI * 32 / 180 * Math.Sin(2 * Math.PI * place.X / 0.2);

            return new Vector(Math.Sin(tilt), Math.Cos(tilt) * facing.Y, 0);
        }

        void Lay(int count, Func<Point> where, Vector facing)
        {
            for (int i = 0; i < count; i++)
            {
                Point place = where();
                Vector turned = facing.X == 0 && facing.Z == 0 ? Rippled(place, facing) : facing;
                Vector heading = (new Vector(Across(), Across(), Across()) * 0.5 - turned).Unit;
                Color light = new (random.NextDouble(), random.NextDouble(), random.NextDouble());

                photons.Add(PhotonMap.Photon.At(place, light, heading, turned));
            }
        }

        Lay(60_000, OnTheFloor, Up);
        Lay(5_000, OnTheFloor, Down);
        Lay(10_000, OnTheWall, new Vector(-1, 0, 0));
        Lay(10_000, OnTheWall, new Vector(1, 0, 0));
        Lay(10_000, OnTheSlope, slope);
        Lay(10_000, () => new Point(Across(), 1.5, Across()), Up);

        PhotonMap map = new (photons, 200, 0.5);
        PhotonMap few = new (photons, 20, 0.5);
        List<(Point Place, Vector Facing)> places = [];

        for (int i = 0; i < 60; i++)
        {
            places.Add((OnTheFloor(), Up));
            places.Add((OnTheWall(), new Vector(-1, 0, 0)));
            places.Add((OnTheSlope(), slope));
        }

        foreach ((Point place, Vector facing) in places)
        {
            foreach (bool bothSides in new[] { false, true })
            {
                AssertClose(
                    Expected(map, place, facing, bothSides, 0), map.Gather(place, facing, bothSides, Math.Abs, 0));
                AssertClose(
                    Expected(few, place, facing, bothSides, 0.15), few.Gather(place, facing, bothSides, Math.Abs, 0.15));
            }
        }
    }

    /// <summary>
    /// This method gathers the light of the photons nearest a place the long way, through every photon of a
    /// map, for photons that lie either on the place's own surface or nowhere near it.
    /// </summary>
    private static Color Expected(PhotonMap map, Point place, Vector facing, bool bothSides, double least)
    {
        (PhotonMap.Photon Photon, double Distance)[] counting = map.Photons
            .Select(photon => (Photon: photon, Distance: DistanceOf(photon, place)))
            .Where(found => Counts(found.Photon, facing, bothSides) && found.Distance < map.Farthest * map.Farthest)
            .OrderBy(found => found.Distance)
            .Take(map.Nearest)
            .ToArray();

        if (counting.Length == 0)
            return Colors.Black;

        double radius = counting.Length < map.Nearest ? map.Farthest : Math.Sqrt(counting[^1].Distance);

        if (radius < least)
        {
            radius = least;
            counting = map.Photons
                .Select(photon => (Photon: photon, Distance: DistanceOf(photon, place)))
                .Where(found => Counts(found.Photon, facing, bothSides) && found.Distance < least * least)
                .ToArray();
        }

        double red = 0, green = 0, blue = 0;

        foreach ((PhotonMap.Photon photon, double distance) in counting)
        {
            double slant = Math.Abs(photon.Dx * facing.X + photon.Dy * facing.Y + photon.Dz * facing.Z);
            double share = (1 - Math.Sqrt(distance) / (1.1 * radius)) * slant;

            red += photon.Red * share;
            green += photon.Green * share;
            blue += photon.Blue * share;
        }

        double area = ConeNormal * Math.PI * radius * radius;

        return new Color(red / area, green / area, blue / area);
    }

    // The same photons summed in another order differ in their last places.
    private static void AssertClose(Color expected, Color actual)
    {
        Assert.AreEqual(expected.Red, actual.Red, 1e-9 * (1 + expected.Red), $"{expected} against {actual}");
        Assert.AreEqual(expected.Green, actual.Green, 1e-9 * (1 + expected.Green), $"{expected} against {actual}");
        Assert.AreEqual(expected.Blue, actual.Blue, 1e-9 * (1 + expected.Blue), $"{expected} against {actual}");
    }

    private static double DistanceOf(PhotonMap.Photon photon, Point place)
    {
        double dx = photon.X - place.X, dy = photon.Y - place.Y, dz = photon.Z - place.Z;

        return dx * dx + dy * dy + dz * dz;
    }

    private static bool Counts(PhotonMap.Photon photon, Vector facing, bool bothSides)
    {
        double facingBy = photon.Nx * facing.X + photon.Ny * facing.Y + photon.Nz * facing.Z;

        return (bothSides ? Math.Abs(facingBy) : facingBy) >= 0.9;
    }
}
