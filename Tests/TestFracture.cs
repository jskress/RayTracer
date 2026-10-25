using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover a solid broken into pieces: the convex cells it is cut into, the polyhedra they become, sharp
/// or worn round, and the fracture that scatters the seeds, cuts the cells and sets the pieces where they fall.
/// </summary>
[TestClass]
public class TestFracture
{
    private static Fracture Broken(Action<Fracture> set = null, Surface solid = null)
    {
        Fracture fracture = new ()
        {
            Solid = solid ?? new Cube { Transform = Transforms.Scale(2, 1, 1.5) },
            Pieces = 30,
            Seed = 5
        };

        set?.Invoke(fracture);
        fracture.PrepareForRendering();

        return fracture;
    }

    // Whether a place is inside a piece, in the fracture's own space: the piece asked through its own transform.
    private static bool IsIn(Surface piece, Point place) => Containment.Holds(piece, place);

    private static List<Point> PlacesIn(Point low, Point high, int count, int seed)
    {
        Random random = new (seed);

        return Enumerable.Range(0, count)
            .Select(_ => new Point(
                low.X + random.NextDouble() * (high.X - low.X),
                low.Y + random.NextDouble() * (high.Y - low.Y),
                low.Z + random.NextDouble() * (high.Z - low.Z)))
            .ToList();
    }

    // A group that counts how often it is readied, as a text's letters or an L-system's parts are built.
    private sealed class Counted : Group
    {
        public int Readied { get; private set; }

        protected override void PrepareSurfaceForRendering()
        {
            Readied++;
            base.PrepareSurfaceForRendering();
        }
    }

    /// <summary>
    /// A solid broken into pieces is readied once, however many pieces stand it: readied twice, a solid that builds
    /// itself when readied -- a text, an L-system -- built itself twice, coincident, and its pieces came out hollow.
    /// </summary>
    [TestMethod]
    public void TestASolidIsReadiedOnce()
    {
        Counted solid = new ();

        solid.Add(new Sphere());

        Broken(fracture => fracture.Pieces = 12, solid);

        Assert.AreEqual(1, solid.Readied);
    }

    /// <summary>
    /// A polytope keeps what lies inside a plane, and its volume is what geometry says: a cube of side two cut
    /// through its middle keeps four; with a corner cut off -- the plane through the midpoints of the corner's three
    /// edges -- it keeps all but the corner's pyramid, a sixth, and gains a triangle; drawn in by a quarter it is a
    /// cube of side one and a half.
    /// </summary>
    [TestMethod]
    public void TestAPolytopeIsCutByPlanes()
    {
        Polytope cube = Polytope.Cube(Matrix.Identity);

        Assert.AreEqual(8, cube.Volume(), 1e-9);
        Assert.AreEqual(4, cube.Clip(new Vector(1, 0, 0), 0).Volume(), 1e-9);
        Assert.AreEqual(8 - 1.0 / 6, cube.Clip(new Vector(1, 1, 1).Unit, 2 / Math.Sqrt(3)).Volume(), 1e-9);
        Assert.AreEqual(1.5 * 1.5 * 1.5, cube.Shrunk(0.25).Volume(), 1e-9);
        Assert.AreSame(cube, cube.Clip(new Vector(0, 1, 0), 2));
        Assert.IsNull(cube.Clip(new Vector(0, 1, 0), -2));

        Polytope corner = cube.Clip(new Vector(1, 1, 1).Unit, 2 / Math.Sqrt(3));

        Assert.AreEqual(7, corner.Faces.Count);
        Assert.AreEqual(10, corner.Vertices().Count);
        Assert.AreEqual(15, corner.Edges().Count);
    }

    /// <summary>
    /// A polyhedron of a box meets every ray where the box does, normals and all, behind the ray's origin as well as
    /// ahead of it.
    /// </summary>
    [TestMethod]
    public void TestAPolyhedronOfABoxIsTheBox()
    {
        Matrix carried = Transforms.Translate(0.3, -0.2, 0.5) * Transforms.RotateAroundY(30) * Transforms.Scale(1.5, 0.4, 0.8);
        Cube cube = new () { Transform = carried };
        Polyhedron polyhedron = new ();

        polyhedron.SetFrom(Polytope.Cube(carried));
        cube.PrepareForRendering();
        polyhedron.PrepareForRendering();

        Random random = new (11);
        int met = 0;

        for (int index = 0; index < 500; index++)
        {
            Point origin = new (random.NextDouble() * 6 - 3, random.NextDouble() * 6 - 3, random.NextDouble() * 6 - 3);
            Vector toward = new Vector(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Unit;
            Ray ray = new (origin, toward);
            List<Intersection> boxHits = [];
            List<Intersection> hits = [];

            cube.Intersect(ray, boxHits);
            polyhedron.Intersect(ray, hits);

            Assert.AreEqual(boxHits.Count, hits.Count, $"ray {index}");

            for (int hit = 0; hit < hits.Count; hit++)
            {
                Assert.AreEqual(boxHits[hit].Distance, hits[hit].Distance, 1e-9);

                Point at = ray.At(hits[hit].Distance);

                Assert.IsTrue((cube.NormalAt(at, boxHits[hit]) - polyhedron.NormalAt(at, hits[hit])).Magnitude < 1e-9);
            }

            met += hits.Count;
        }

        Assert.IsTrue(met > 100, $"only {met} crossings were tried");
    }

    /// <summary>
    /// A rounded polyhedron is every point within its radius of the inner one: wherever a ray meets it, the place is
    /// exactly that far from the inner polyhedron, and the normal points straight away from the nearest part of it.
    /// For a box inner, the nearest part is the box's point clamped from the place.
    /// </summary>
    [TestMethod]
    public void TestARoundedPolyhedronIsTheInnerOneGrownByABall()
    {
        const double Radius = 0.3;
        Point low = new (-1, -0.5, -0.7);
        Point high = new (1, 0.5, 0.7);
        RoundedPolyhedron rounded = new ();

        rounded.SetFrom(Polytope.Box(low, high), Radius);
        rounded.PrepareForRendering();

        Point Nearest(Point at) => new (
            Math.Clamp(at.X, low.X, high.X), Math.Clamp(at.Y, low.Y, high.Y), Math.Clamp(at.Z, low.Z, high.Z));

        Random random = new (13);
        int met = 0;

        for (int index = 0; index < 800; index++)
        {
            Point origin = new (random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4);
            Vector toward = ((Point.Zero + new Vector(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5) * 3) - origin).Unit;
            Ray ray = new (origin, toward);
            List<Intersection> hits = [];

            rounded.Intersect(ray, hits);

            foreach (Intersection hit in hits)
            {
                Point at = ray.At(hit.Distance);
                Vector away = at - Nearest(at);

                Assert.AreEqual(Radius, away.Magnitude, 1e-9, $"ray {index} met it at {at}");
                Assert.IsTrue((rounded.NormalAt(at, hit) - away.Unit).Magnitude < 1e-6, $"ray {index}'s normal at {at}");
            }

            met += hits.Count;
        }

        Assert.IsTrue(met > 300, $"only {met} crossings were tried");
    }

    /// <summary>
    /// With no gap, the pieces fill the solid exactly, without overlapping: every place inside a box is inside one
    /// piece and only one, and every piece is a convex polyhedron cut by the box's own planes.  With a gap, no place
    /// is in two, and the places in none lie within half the gap of a crack.
    /// </summary>
    [TestMethod]
    public void TestThePiecesFillTheSolidWithoutOverlapping()
    {
        Fracture whole = Broken();
        Fracture cracked = Broken(fracture => fracture.Gap = 0.05);
        List<Point> places = PlacesIn(new Point(-1.98, -0.98, -1.48), new Point(1.98, 0.98, 1.48), 600, 7);

        Assert.IsTrue(whole.Surfaces.Count >= 25 && whole.Surfaces.All(piece => piece is Polyhedron));

        foreach (Point place in places)
            Assert.AreEqual(1, whole.Surfaces.Count(piece => IsIn(piece, place)), $"{place} is not in exactly one piece");

        int lost = 0;

        foreach (Point place in places)
        {
            int count = cracked.Surfaces.Count(piece => IsIn(piece, place));

            Assert.IsTrue(count <= 1, $"{place} is in {count} pieces");

            if (count == 0)
                lost++;
        }

        // The cracks are a sliver of the volume: some places fall in them, most do not.
        Assert.IsTrue(lost > 0 && lost < places.Count / 4, $"{lost} of {places.Count} places fell in the cracks");
    }

    /// <summary>
    /// The seed decides the break: the same one breaks the solid the same way, another another way.
    /// </summary>
    [TestMethod]
    public void TestTheSameSeedBreaksTheSameWay()
    {
        static List<double> Volumes(Fracture fracture) => fracture.Surfaces
            .Select(piece => piece.BoundingBox)
            .Select(box => (box.Maximum - box.Minimum).Magnitude)
            .ToList();

        CollectionAssert.AreEqual(Volumes(Broken()), Volumes(Broken()));
        CollectionAssert.AreNotEqual(Volumes(Broken()), Volumes(Broken(fracture => fracture.Seed = 6)));
    }

    /// <summary>
    /// Broken flat, every crack runs straight up and down: each piece reaches the solid's top and bottom, its own
    /// faces all standing upright.
    /// </summary>
    [TestMethod]
    public void TestAFlatBreakRunsStraightDown()
    {
        Fracture paving = Broken(fracture => fracture.Flat = true, new Cube { Transform = Transforms.Scale(2, 0.1, 1.5) });

        foreach (Surface piece in paving.Surfaces)
        {
            BoundingBox box = piece.BoundingBox;

            Assert.AreEqual(-0.1, box.Minimum.Y, 1e-3);
            Assert.AreEqual(0.1, box.Maximum.Y, 1e-3);
        }

        // A ray straight down through any place on the slab meets exactly one piece's top and bottom.
        foreach (Point place in PlacesIn(new Point(-1.9, 1, -1.4), new Point(1.9, 1, 1.4), 200, 3))
        {
            Ray down = new (place, new Vector(0, -1, 0));

            Assert.AreEqual(1, paving.Surfaces.Count(piece =>
            {
                List<Intersection> hits = [];

                piece.Intersect(down, hits);

                return hits.Count == 2 && Math.Abs(hits[1].Distance - hits[0].Distance - 0.2) < 1e-9;
            }), $"straight down at {place}");
        }
    }

    /// <summary>
    /// Stretched, pieces come out longer along the axis stretched: courses of stone rather than lumps.
    /// </summary>
    [TestMethod]
    public void TestAStretchedBreakMakesLongPieces()
    {
        static double Ratio(Fracture fracture) => fracture.Surfaces.Average(piece =>
        {
            Vector side = piece.BoundingBox.Maximum - piece.BoundingBox.Minimum;

            return side.X / side.Y;
        });

        Cube block = new () { Transform = Transforms.Scale(2, 2, 0.2) };
        double even = Ratio(Broken(fracture => fracture.Pieces = 60, block));
        double stretched = Ratio(Broken(fracture => { fracture.Pieces = 60; fracture.Stretched = new Vector(3, 1, 1); },
            new Cube { Transform = Transforms.Scale(2, 2, 0.2) }));

        Assert.IsTrue(stretched > even * 2, $"stretched three times along X, pieces came out {stretched} long for {even}");
    }

    /// <summary>
    /// Struck at a point, a pane breaks finer toward it -- rings and spokes, the pieces nearest the point the
    /// smallest -- and with a hole, no piece is left within it.
    /// </summary>
    [TestMethod]
    public void TestAStruckPaneBreaksRoundThePoint()
    {
        Point struck = new (0.3, 0.2, 0);
        Fracture pane = Broken(fracture =>
        {
            fracture.Pieces = 90;
            fracture.Toward = struck;
            fracture.Hole = 0.15;
        }, new Cube { Transform = Transforms.Scale(1.2, 1.2, 0.01) });

        List<(double Distance, double Size)> pieces = pane.Surfaces
            .Select(piece =>
            {
                BoundingBox box = piece.BoundingBox;
                Point middle = box.Minimum + (box.Maximum - box.Minimum) * 0.5;

                return ((middle - struck).Magnitude, (box.Maximum - box.Minimum).Magnitude);
            })
            .ToList();

        Assert.IsTrue(pieces.Count > 50, $"only {pieces.Count} pieces");
        Assert.IsTrue(pieces.All(piece => piece.Distance > 0.1), "a piece was left in the hole");

        double nearSize = pieces.Where(piece => piece.Distance < 0.5).Average(piece => piece.Size);
        double farSize = pieces.Where(piece => piece.Distance > 0.9).Average(piece => piece.Size);

        Assert.IsTrue(farSize > nearSize * 2, $"pieces far off are {farSize} across, near ones {nearSize}");
    }

    /// <summary>
    /// Pieces moved apart are carried out from the middle by the distance asked, and tumbled ones turned; loose
    /// ones stand exactly where they would but each in a place of its own in its pattern; and given materials, the
    /// pieces wear them, more than one of them.
    /// </summary>
    [TestMethod]
    public void TestPiecesAreMovedTurnedLooseAndDressed()
    {
        static Point Middle(Surface piece)
        {
            BoundingBox box = BoxOf(piece);

            return box.Minimum + (box.Maximum - box.Minimum) * 0.5;
        }

        Fracture still = Broken();
        Fracture apart = Broken(fracture => fracture.Apart = 0.5);
        Fracture tumbled = Broken(fracture => fracture.Tumble = 0.5);
        Fracture loose = Broken(fracture => fracture.Loose = true);
        Material[] materials = [new Material(), new Material(), new Material()];
        Fracture dressed = Broken(fracture => fracture.PieceMaterials = [..materials]);

        for (int index = 0; index < still.Surfaces.Count; index++)
        {
            Vector moved = Middle(apart.Surfaces[index]) - Middle(still.Surfaces[index]);
            Vector outward = Middle(still.Surfaces[index]) - Point.Zero;

            Assert.AreEqual(0.5, moved.Magnitude, 0.05, $"piece {index} moved {moved}");
            Assert.IsTrue(moved.Dot(outward) > 0, $"piece {index} moved inward");

            // Loose, a piece stands where it did: its world box is the same, though its own space is not.
            BoundingBox here = BoxOf(still.Surfaces[index]);
            BoundingBox there = BoxOf(loose.Surfaces[index]);

            Assert.IsTrue((here.Minimum - there.Minimum).Magnitude < 1e-6 && (here.Maximum - there.Maximum).Magnitude < 1e-6);
        }

        Assert.IsTrue(tumbled.Surfaces.Any(piece => Math.Abs(piece.Transform.Entry(0, 1)) > 0.01), "nothing was turned");
        Assert.IsTrue(loose.Surfaces.All(piece => (piece.Transform * Point.Zero - Point.Zero).Magnitude > 1),
            "a loose piece kept the solid's own space");
        Assert.IsTrue(dressed.Surfaces.All(piece => materials.Contains(piece.Material)));
        Assert.IsTrue(dressed.Surfaces.Select(piece => piece.Material).Distinct().Count() > 1);
    }

    private static BoundingBox BoxOf(Surface piece) => piece.BoundingBox.TransformedBy(piece.Transform);

    /// <summary>
    /// Worn round, the pieces are rounded polyhedra whose faces still stand where the cell's did: the solid's own
    /// faces are not drawn in, so a slab broken flat is its full thickness through the middle of every piece.
    /// </summary>
    [TestMethod]
    public void TestWornPiecesKeepTheirFaces()
    {
        Fracture worn = Broken(fracture => { fracture.Rounded = 0.05; fracture.Gap = 0.02; fracture.Flat = true; },
            new Cube { Transform = Transforms.Scale(2, 0.3, 2) });

        Assert.IsTrue(worn.Surfaces.All(piece => piece is RoundedPolyhedron));

        // Straight down through the middle of a piece, it is the slab's full thickness.
        foreach (Surface piece in worn.Surfaces)
        {
            BoundingBox box = piece.BoundingBox;
            Point above = new ((box.Minimum.X + box.Maximum.X) / 2, 2, (box.Minimum.Z + box.Maximum.Z) / 2);
            List<Intersection> hits = [];

            piece.Intersect(new Ray(above, new Vector(0, -1, 0)), hits);

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(0.6, hits[1].Distance - hits[0].Distance, 1e-6);
        }
    }

    /// <summary>
    /// A solid that is not a box is broken by cutting each piece from it: inside the solid, every place is in one
    /// piece; outside it, in none -- though the cells themselves reach past it.  And a box written in braces, which
    /// is a group of one, is still broken exactly: cut from, its faces met the cells' own and a pane of glass came
    /// out speckled.
    /// </summary>
    [TestMethod]
    public void TestAnyClosedSolidMayBeBroken()
    {
        Fracture ball = Broken(fracture => fracture.Pieces = 20, new Sphere());

        Assert.IsTrue(ball.Surfaces.All(piece => piece is CsgSurface));

        foreach (Point place in PlacesIn(new Point(-1, -1, -1), new Point(1, 1, 1), 400, 9))
        {
            int count = ball.Surfaces.Count(piece => IsIn(piece, place));

            Assert.AreEqual((place - Point.Zero).Magnitude < 1 ? 1 : 0, count, $"{place} is in {count} pieces");
        }

        Group written = new ();

        written.Add(new Cube { Transform = Transforms.Scale(1.2, 1.2, 0.01) });

        Assert.IsTrue(Broken(solid: written).Surfaces.All(piece => piece is Polyhedron));
    }

    /// <summary>
    /// Whole pieces are never cut by the solid: each is a whole cell, kept where its seed lies inside the solid, so
    /// the outline is made of whole stones -- some reaching past the solid, none cut to its skin -- and the seeds
    /// scattered round it keep the outer pieces as small as the inner ones.
    /// </summary>
    [TestMethod]
    public void TestWholePiecesAreKeptNotCut()
    {
        Fracture heap = Broken(fracture => { fracture.Whole = true; fracture.Pieces = 40; },
            new Sphere { Transform = Transforms.Scale(1, 0.5, 1) });
        List<double> sizes = heap.Surfaces
            .Select(piece => (piece.BoundingBox.Maximum - piece.BoundingBox.Minimum).Magnitude)
            .ToList();

        Assert.IsTrue(heap.Surfaces.All(piece => piece is Polyhedron), "a whole piece was cut");
        Assert.AreEqual(40, heap.Surfaces.Count, 2);
        Assert.IsTrue(heap.Surfaces.Any(piece => piece.BoundingBox.Maximum.Y > 0.5), "no stone reached past the mound");
        Assert.IsTrue(sizes.Max() < sizes.Average() * 3, $"the largest piece is {sizes.Max()} across, the average {sizes.Average()}");
    }

    /// <summary>
    /// What cannot be broken is refused, with the reason.
    /// </summary>
    [TestMethod]
    public void TestWhatCannotBeBrokenIsRefused()
    {
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() => Broken(solid: new Plane())).Message, "must end somewhere");
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() => Broken(fracture => fracture.Gap = -1)).Message,
            "no less than nought");
        StringAssert.Contains(Assert.ThrowsExactly<Exception>(() =>
            Broken(fracture => fracture.Stretched = new Vector(1, 0, 1))).Message, "more than nought");
    }

    /// <summary>
    /// A solid that is not a box, broken into pieces of several materials, shows each piece's material on its skin as
    /// well as on its broken faces.  (Every piece stood the one solid, and the material handed into it was the first
    /// piece's, so a boulder broken into four stones came out one stone's color all over.)
    /// </summary>
    [TestMethod]
    public void TestEachPieceWearsItsOwnMaterialOnItsSkin()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fracture-tests-{Guid.NewGuid():N}");
        string scene = Path.Combine(directory, "skins.igl");
        string output = Path.Combine(directory, "skins.png");

        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(scene, """
                context { no gamma }
                camera { location [0, 0, -4]  look at [0, 0, 0] }
                point light { location [-5, 8, -6] }
                Red = material { pigment [1, 0, 0]  ambient 1  diffuse 0  specular 0 }
                Blue = material { pigment [0, 0, 1]  ambient 1  diffuse 0  specular 0 }
                fracture { solid { sphere { } }  pieces 12  materials Red, Blue  with seed 5 }
                """);
            new LanguageParser(scene).Parse().Render(new RenderOptions { OutputFileName = output, Width = 40, Height = 40 });

            Canvas picture = new ImageFile(output).Load()[0];
            int red = 0, blue = 0;

            for (int x = 0; x < 40; x++)
            {
                for (int y = 0; y < 40; y++)
                {
                    Color color = picture.GetPixel(x, y);

                    if (color.Red > 0.8 && color.Blue < 0.2)
                        red++;
                    else if (color.Blue > 0.8 && color.Red < 0.2)
                        blue++;
                }
            }

            Assert.IsTrue(red > 100 && blue > 100, $"{red} red pixels and {blue} blue");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// The scene language: every property of a fracture, written out; a solid named and a material list; and a
    /// fracture with no solid refused as it is read.
    /// </summary>
    [TestMethod]
    public void TestTheSceneLanguageBreaksASolid()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fracture-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string Run(string body)
            {
                string scene = Path.Combine(directory, "scene.igl");
                StringWriter captured = new ();
                TextWriter was = Console.Out;

                File.WriteAllText(scene,
                    "camera { location [0, 2, -6]  look at [0, 0.5, 0] }\n" +
                    "point light { location [-5, 8, -6] }\n" + body);
                Console.SetOut(captured);

                try
                {
                    new LanguageParser(scene).Parse()?.Render(new RenderOptions
                    {
                        OutputFileName = Path.Combine(directory, "scene.png"), Width = 8, Height = 6
                    });
                }
                catch (Exception exception)
                {
                    return exception.Message;
                }
                finally
                {
                    Console.SetOut(was);
                }

                return captured.ToString().Contains("Error") ? captured.ToString() : null;
            }

            Assert.IsNull(Run("""
                Grey = material { pigment [0.5, 0.5, 0.5] }
                Brown = material { pigment [0.5, 0.4, 0.3] }
                fracture {
                    solid { cube { scale [2, 1, 1]  translate [0, 1, 0] } }
                    pieces 25  gap 0.02  rounded 0.03  stretched [2, 1, 1]  apart 0.1  tumble 10  loose
                    materials Grey, Brown, material { pigment [0.6, 0.6, 0.5] }
                    with seed 3
                }
                fracture {
                    solid { cube { scale [1, 1, 0.01] } }
                    pieces 40  flat  toward [0.2, 0.3, 0]  hole 0.1
                    translate [0, 1, 2]
                }
                """));
            Assert.IsNull(Run("""
                Ball = sphere { translate [0, 1, 0] }
                fracture { solid Ball  pieces 10  gap 0.05 }
                """));
            StringAssert.Contains(Run("fracture { pieces 10 }"), "needs a solid");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
