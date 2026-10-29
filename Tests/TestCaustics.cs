using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Patterns;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover caustics: light followed from the lamps through mirrors and glass to where it lands.
/// Each is held to an answer worked out here where there is one, since a caustic can be wrong by any amount
/// and still look like light.
/// </summary>
[TestClass]
public class TestCaustics
{
    private static Material Matte() => new ()
    {
        Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0
    };

    private static Material Glass(double reflective = 1) => new ()
    {
        Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
        Transparency = 1, Reflective = reflective, Interior = new Interior { IndexOfRefraction = 1.5 }
    };

    private static Scene SceneOf(int photons, Light light, params Surface[] surfaces)
    {
        Scene scene = new () { CausticPhotons = photons };

        foreach (Surface surface in surfaces)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(light);
        scene.ShootCausticPhotons();

        return scene;
    }

    private static double Shade(Scene scene, Point above) => ShadeColor(scene, above).Red;

    // Five places on the floor, about the middle: a gather from the nearest photons is a hundredth or so off
    // either way at any one place, the photons lying where they land.
    private static readonly Point[] Middle =
        [new (0.05, 0.5, 0.1), new (-0.4, 0.5, 0.3), new (0.3, 0.5, -0.35), new (-0.25, 0.5, -0.2), new (0.45, 0.5, 0.4)];

    /// <summary>
    /// This method returns the light the photons that landed in a square of the floor bring it, over the
    /// square's area, as a color is: their light summed, which is exact however they lie, where a gather from
    /// the nearest few hundred to one place is a few hundredths off for photons that land as if at random --
    /// those a sky, an area light, a glow or a rough mirror sends.
    /// </summary>
    private static Color LandedOver(Scene scene, double x, double z, double half)
    {
        double red = 0, green = 0, blue = 0;

        foreach (PhotonMap.Photon photon in scene.Caustics.Photons)
        {
            if (Math.Abs(photon.Y) < 1e-6 && Math.Abs(photon.X - x) < half && Math.Abs(photon.Z - z) < half)
            {
                red += photon.Red;
                green += photon.Green;
                blue += photon.Blue;
            }
        }

        double area = 4 * half * half * Math.PI;

        return new Color(red / area, green / area, blue / area);
    }

    /// <summary>
    /// This method returns the floor's color averaged over a square of it, looked at from above.
    /// </summary>
    private static Color ShadedOver(Scene scene, double x, double z, double half)
    {
        List<Point> places = [];

        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 9; j++)
                places.Add(new Point(x - half + (i + 0.5) * half / 4.5, 0.5, z - half + (j + 0.5) * half / 4.5));
        }

        return AveragedOver(scene, places);
    }

    private static Color AveragedOver(Scene scene, IEnumerable<Point> places)
    {
        Point[] all = places.ToArray();

        return all.Aggregate(Colors.Black, (sum, place) => sum + ShadeColor(scene, place)) * (1.0 / all.Length);
    }

    private static Color ShadeColor(Scene scene, Point above)
    {
        Ray ray = new (above, Directions.Down);
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 4);
    }

    /// <summary>
    /// With caustics on, glass that bends light stops shadow rays, the photons bringing what comes through it --
    /// but only glass the photons could be aimed at.  An endless sea, a plane, has no edges to aim at and is sent
    /// none, so the floor under it is lit through it as it would be without caustics, not left in the dark.
    /// </summary>
    [TestMethod]
    public void TestTheFloorUnderAnEndlessSeaIsLit()
    {
        Plane sea = new () { Material = Glass(0), Transform = Transforms.Translate(0, 1, 0) };
        Plane floor = new () { Material = Matte() };
        Scene scene = SceneOf(20000, new DistantLight { Direction = new Vector(0, -1, 0) }, floor, sea);

        Assert.IsTrue(Shade(scene, new Point(0, 0.5, 0)) > 0.5, "the sun should reach the floor through the water");
    }

    /// <summary>
    /// This method makes a square mirror a unit on a side, centered at the given place and tipped so that
    /// a sun shining along +X is thrown straight down: its face looks back along -X and down.
    /// </summary>
    private static Parallelogram MirrorAt(Point center, double reflective)
    {
        Vector across = new (0, 0, 1);
        Vector down = new Vector(1, -1, 0).Unit;

        return new Parallelogram
        {
            Point = center - across * 0.5 - down * 0.5, Side1 = across, Side2 = down,
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
                Reflective = reflective
            }
        };
    }

    /// <summary>
    /// This tests the plainest caustic there is: a mirror throwing a sun onto a floor.  The sun shines level,
    /// so it gives the floor nothing of itself, and a mirror tipped at 45 degrees throws it straight down;
    /// the floor under the mirror is lit square-on by the sun's whole light, less what the mirror keeps.
    /// </summary>
    [TestMethod]
    public void TestAMirrorThrowsTheSunOntoTheFloor()
    {
        foreach (double reflective in new[] { 1, 0.6 })
        {
            Scene scene = SceneOf(
                200_000, new DistantLight { Direction = new Vector(1, 0, 0) },
                new Plane { Material = Matte() }, MirrorAt(new Point(0, 2, 0), reflective));

            Assert.IsNotNull(scene.Caustics);
            Assert.IsGreaterThan(0, scene.Caustics.Count, "photons should have landed");
            Assert.AreEqual(reflective, Shade(scene, new Point(0.05, 1, 0.1)), 0.03 * reflective,
                $"the floor under a mirror of {reflective}");
            Assert.AreEqual(0, Shade(scene, new Point(2, 1, 0)), 1e-9, "the floor beyond the mirror's reach");
        }

        // A sheet that casts no shadow, hung in the sun's way, stands for a light rather than being lit: the
        // photons pass it as the sun's shadow rays would.
        Parallelogram standIn = new ()
        {
            Point = new Point(-1, 1, -1), Side1 = new Vector(0, 0, 2), Side2 = new Vector(0, 2, 0),
            Material = Matte(), NoShadow = true
        };
        Scene past = SceneOf(
            200_000, new DistantLight { Direction = new Vector(1, 0, 0) },
            new Plane { Material = Matte() }, MirrorAt(new Point(0, 2, 0), 1), standIn);

        Assert.AreEqual(1, Shade(past, new Point(0.05, 1, 0.1)), 0.03, "past something that casts no shadow");
    }

    /// <summary>
    /// This tests a metal throwing the sun: a mirror of gold, tipped at 45 degrees, throws onto the floor
    /// under it the sun's light by gold's own share of each color at that angle -- photons are sent at it with
    /// nothing said of its reflectance, and it is not lit as matte.
    /// </summary>
    [TestMethod]
    public void TestAMetalThrowsTheSunInItsOwnColor()
    {
        Parallelogram gold = MirrorAt(new Point(0, 2, 0), 0);

        gold.Material.Metal = Metal.Named("gold");
        gold.Material.Metallic = 1;

        Scene scene = SceneOf(
            200_000, new DistantLight { Direction = new Vector(1, 0, 0) }, new Plane { Material = Matte() }, gold);
        RgbSpectrum share = Metal.Named("gold").ShareAt<RgbSpectrum>(Math.Sqrt(0.5), 1);
        Color seen = ShadeColor(scene, new Point(0.05, 1, 0.1));

        Assert.AreEqual(share.Red, seen.Red, 0.03 * share.Red, "red");
        Assert.AreEqual(share.Green, seen.Green, 0.03 * share.Green, "green");
        Assert.AreEqual(share.Blue, seen.Blue, 0.03 * share.Blue, "blue");
    }

    /// <summary>
    /// This tests a mirror throwing a lamp rather than a sun.  This renderer's lamps do not fall off with
    /// distance unless they fade, so the light the mirror gets is the lamp's own; but what it throws spreads
    /// from the lamp's image behind it, ten units off, and has spread over twelve by the time it reaches the
    /// floor under the mirror: (10/12)^2 of the lamp.  A spotlight throws that times its cone's strength
    /// toward the mirror.
    /// </summary>
    [TestMethod]
    public void TestAMirrorThrowsALampAsIfFromBehindIt()
    {
        PointLight lamp = new () { Location = new Point(-10, 2, 0) };
        Spotlight spot = new ()
        {
            Location = new Point(-10, 2, 0), PointAt = new Point(0, 2.6, 0), Radius = 2 * Math.PI / 180, Falloff = 6 * Math.PI / 180
        };

        foreach (PointLight light in new[] { lamp, spot })
        {
            double ThrownBy(int photons) => Shade(
                SceneOf(photons, light, new Plane { Material = Matte() }, MirrorAt(new Point(0, 2, 0), 1)),
                new Point(0, 1, 0));

            double aimed = light.IntensityToward(new Point(0, 2, 0));
            double thrown = ThrownBy(400_000) - ThrownBy(0);

            if (light is Spotlight)
                Assert.IsLessThan(0.9, aimed, "the mirror should stand in the soft edge of the spot's cone");
            Assert.AreEqual(100.0 / 144 * aimed, thrown, 0.03 * aimed, $"thrown by the {light.GetType().Name}");
        }

        // A dim lamp's photons each carry less than any allowance for "next to nothing" would let count, and
        // must count all the same: what each carries shrinks with the number of photons, too.
        PointLight dim = new () { Location = new Point(-10, 2, 0), Color = new Color(1e-4, 1e-4, 1e-4) };
        double dimly = Shade(SceneOf(400_000, dim, new Plane { Material = Matte() }, MirrorAt(new Point(0, 2, 0), 1)),
                           new Point(0, 1, 0)) -
                       Shade(SceneOf(0, dim, new Plane { Material = Matte() }, MirrorAt(new Point(0, 2, 0), 1)),
                           new Point(0, 1, 0));

        Assert.AreEqual(1e-4 * 100.0 / 144, dimly, 3e-6, "thrown by a dim lamp");

        // In a clear globe the lamp throws the same: what it passes straight through only dims it, and its
        // light is still the lamp's where it meets the mirror, not where it met the globe a step away.
        Sphere globe = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Transparency = 1
            },
            Transform = Transforms.Translate(-10, 2, 0) * Transforms.Scale(0.5, 0.5, 0.5)
        };
        double ThrownInAGlobe(int photons) => Shade(
            SceneOf(photons, new PointLight { Location = new Point(-10, 2, 0) }, new Plane { Material = Matte() },
                MirrorAt(new Point(0, 2, 0), 1), globe),
            new Point(0, 1, 0));

        Assert.AreEqual(100.0 / 144, ThrownInAGlobe(400_000) - ThrownInAGlobe(0), 0.03, "a lamp in a clear globe");
    }

    /// <summary>
    /// This tests a pane turned 75 degrees from the sun, at which each face mirrors a quarter of what meets it
    /// (Schlick's share, as the eye's rays have it), so that what goes back and forth between the faces is no
    /// longer a trifle: (1 - R)^2 / (1 - R^2) of the sun gets through, and lands beneath parallel to how it
    /// came, the pane's faces being parallel.  The pane is long, so that the light its ends pipe along it
    /// lands well away from where it is measured.  This is the test that found a ray mirrored inside glass
    /// setting off from outside it (see Intersection.MirrorPoint): what went back and forth between the faces
    /// came out of the pane unbent and landed in a band of its own.
    /// </summary>
    [TestMethod]
    public void TestAPaneTurnedFromTheSunMirrorsMoreOfIt()
    {
        double slant = Math.Pow(1 - Math.Cos(75 * Math.PI / 180), 5);
        double reflectance = 0.04 + 0.96 * slant;
        Scene scene = SceneOf(
            400_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() },
            new Cube
            {
                Material = Glass(),
                Transform = Transforms.Translate(0, 3.5, 0) * Transforms.RotateAroundZ(75 * Math.PI / 180, true) *
                            Transforms.Scale(3, 0.05, 1)
            });

        // Within a few hundredths: the light that went back and forth arrives as fewer photons than what came
        // straight through, so how many of them are among the nearest to a place varies a little.
        Assert.AreEqual((1 - reflectance) / (1 + reflectance), Shade(scene, new Point(-0.3, 0.3, 0.1)), 0.025);
        Assert.AreEqual((1 - reflectance) / (1 + reflectance), Shade(scene, new Point(0.3, 0.3, -0.2)), 0.025);
    }

    /// <summary>
    /// This tests a photon losing light on its way through glass as a ray from the eye does: faded over the
    /// way through by the glass's clarity, (0.96 x 0.96 x f) / (1 - 0.04 x 0.04 x f x f) of the sun getting
    /// through where f is the fade across the pane; and tinted by the glass's filter at each face it crosses,
    /// a red glass letting through no green.
    /// </summary>
    [TestMethod]
    public void TestAPaneFadesAndTintsWhatItLetsThrough()
    {
        Material faded = Glass();
        Material red = Glass();

        faded.Interior.Clarity = 0.1;
        red.Pigment = new SolidPigment(new Color(1, 0, 0));
        red.Interior.Filter = 1;

        Scene Through(Material material) => SceneOf(
            200_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() },
            new Cube { Material = material, Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(1, 0.05, 1) });

        double fade = Math.Exp(-1);

        Assert.AreEqual(
            0.96 * 0.96 * fade / (1 - 0.04 * 0.04 * fade * fade), AveragedOver(Through(faded), Middle).Red,
            0.01 * fade);

        Color tinted = AveragedOver(Through(red), Middle);

        Assert.AreEqual(0.96 * 0.96 / (1 - 0.04 * 0.04), tinted.Red, 0.01);
        Assert.AreEqual(0, tinted.Green, 1e-9);
    }

    /// <summary>
    /// This tests that a floor standing inside something that fades light, or that is filled with a medium,
    /// is lit through it, as it is seen through it: an overhead sun goes into water, losing 0.04 at its face,
    /// and what reaches the floor a unit below is faded over that unit -- e^-1 of it for a clarity of one, and
    /// e^-0.5 of the red alone in a medium taking half of the red a unit.  Only what left the water used to be
    /// charged for it, and the floor of a pool took the sun whole.
    /// </summary>
    [TestMethod]
    public void TestAFloorInsideWaterIsLitThroughIt()
    {
        Scene Under(Interior interior) => SceneOf(
            200_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() },
            new Cube
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
                    Transparency = 1, Reflective = 1, Interior = interior
                },
                Transform = Transforms.Scale(3, 1, 3)
            });

        Color faded = LandedOver(Under(new Interior { IndexOfRefraction = 1.5, Clarity = 1 }), 0, 0, 1);

        Assert.AreEqual(0.96 * Math.Exp(-1), faded.Red, 0.01);

        Color absorbed = LandedOver(
            Under(new Interior { IndexOfRefraction = 1.5, Medium = new Medium { Absorption = new Color(0.5, 0, 0) } }),
            0, 0, 1);

        Assert.AreEqual(0.96 * Math.Exp(-0.5), absorbed.Red, 0.01);
        Assert.AreEqual(0.96, absorbed.Green, 0.01);
    }

    /// <summary>
    /// This tests that photons cross a medium whose density varies as the eye's rays do, walking it: in water filled
    /// with a checker of murk, a square of floor under a block of the checker that is clear takes the sun as clear
    /// water lets it through.  Charged at the murk's even density everywhere, it lost e^-0.5 of its red there.
    /// </summary>
    [TestMethod]
    public void TestPhotonsWalkAMediumWhoseDensityVaries()
    {
        Scene scene = SceneOf(
            200_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() },
            new Cube
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
                    Transparency = 1, Reflective = 1,
                    Interior = new Interior
                    {
                        IndexOfRefraction = 1.5,
                        Medium = new Medium
                        {
                            Absorption = new Color(0.5, 0, 0), Density = 1,
                            DensityPattern = new DensityShape { Pattern = new CheckerPattern() }
                        }
                    }
                },
                Transform = Transforms.Scale(3, 1, 3)
            });

        // Over x and z from 0.3 to 0.9, the cube's own space runs from 0.1 to 0.3 across and 0 to 1 up: one block
        // of the checker, and a clear one.
        Color clear = LandedOver(scene, 0.6, 0.6, 0.3);

        Assert.AreEqual(0.96, clear.Red, 0.02, "the sun through the clear block of the murk");
    }

    /// <summary>
    /// This tests that light reaching a floor through two things photons are aimed at is counted once.  Two
    /// panes, one over the other, are two targets, and the beam at each goes through both; each pane lets
    /// through T = (0.96 x 0.96) / (1 - 0.04 x 0.04) of the sun and mirrors the rest, so together, with what
    /// goes back and forth between them, they let through T x T / (1 - (1 - T)^2).  Counted by both beams,
    /// it came to twice that.
    /// </summary>
    [TestMethod]
    public void TestLightThroughTwoTargetsIsCountedOnce()
    {
        Cube PaneAt(double height) => new ()
        {
            Material = Glass(), Transform = Transforms.Translate(0, height, 0) * Transforms.Scale(1, 0.05, 1)
        };

        Scene scene = SceneOf(
            400_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() }, PaneAt(1),
            PaneAt(1.4));
        double pane = 0.96 * 0.96 / (1 - 0.04 * 0.04);

        Assert.AreEqual(pane * pane / (1 - (1 - pane) * (1 - pane)), AveragedOver(scene, Middle).Red, 0.015);
    }

    /// <summary>
    /// This tests light through a pane of glass under an overhead sun.  Each face mirrors 4% of what meets
    /// it square-on, and what is mirrored inside goes back and forth between the faces, letting a little
    /// more out each time: (0.96 x 0.96) / (1 - 0.04 x 0.04) of the sun gets through.  The shadow ray, which
    /// would have let 0.96 x 0.96 straight through, is stopped by the glass; asked to turn no photons, the
    /// pane lets the shadow ray through as before, and sends no photons.
    /// </summary>
    [TestMethod]
    public void TestAPaneLetsTheSunThroughBothFaces()
    {
        Cube Pane(bool noCaustics) => new ()
        {
            Material = Glass(), NoCaustics = noCaustics,
            Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(1, 0.05, 1)
        };

        Scene turning = SceneOf(
            200_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() }, Pane(false));

        Assert.AreEqual(0.96 * 0.96 / (1 - 0.04 * 0.04), AveragedOver(turning, Middle).Red, 0.01);

        Scene straight = SceneOf(
            200_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() }, Pane(true));

        Assert.AreEqual(0, straight.Caustics.Count, "a pane that turns no photons should be sent none");
        Assert.AreEqual(0.96 * 0.96, Shade(straight, new Point(0.05, 0.5, 0.1)), 1e-9);
    }

    /// <summary>
    /// This tests the caustic the shadow ray cannot find: a glass ball bringing a sun to a focus on the floor.
    /// A ball of index 1.5 focuses level light at three quarters of its diameter from its middle, which is
    /// where the floor is put; the middle of its shadow is many times brighter than open floor, and the rest of
    /// its shadow darker, the light having been gathered out of it.  The open floor round it takes the sun,
    /// and the little the ball's surface mirrors onto it.
    /// </summary>
    [TestMethod]
    public void TestAGlassBallFocusesTheSun()
    {
        Sphere ball = new () { Material = Glass(), Transform = Transforms.Translate(0, 1.5, 0) };
        Scene scene = SceneOf(
            400_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() }, ball);

        Assert.IsGreaterThan(5, Shade(scene, new Point(0, 0.2, 0)), "the focus");
        Assert.IsLessThan(0.5, Shade(scene, new Point(0.7, 0.2, 0)), "the rest of the shadow");
        Assert.AreEqual(1, Shade(scene, new Point(3, 0.2, 0)), 0.01, "open floor, and the little the ball mirrors onto it");
    }

    /// <summary>
    /// This tests that what a lamp lights directly is not counted again: a block that both mirrors and is
    /// matte, as glossy paint is, is aimed at, but the photons that meet it first are light its shadow rays
    /// already count.  Under an overhead sun its top shows the sun's light once.
    /// </summary>
    [TestMethod]
    public void TestWhatALampLightsDirectlyIsNotCountedAgain()
    {
        Material glossy = Matte();

        glossy.Reflective = 0.5;

        Scene scene = SceneOf(
            200_000, new DistantLight { Direction = new Vector(0, -1, 0) },
            new Cube { Material = glossy, Transform = Transforms.Translate(0, 1, 0) });

        Assert.AreEqual(1, Shade(scene, new Point(0.1, 3, 0.2)), 1e-9);
    }

    /// <summary>
    /// This tests a rough mirror throwing the sun.  Barely rough, it throws it as a smooth one does.  Rough,
    /// it spreads it, and what lands on the floor is what its facets send downward: the mirror's share of
    /// the sun, over each facet a photon can see, of what that facet's neighbors leave unhidden on the way
    /// out -- worked out here over a fine grid of facets with the same facets the renderer picks.
    /// </summary>
    [TestMethod]
    public void TestARoughMirrorSpreadsWhatItThrows()
    {
        Parallelogram Rough(double roughness)
        {
            Parallelogram mirror = MirrorAt(new Point(0, 2, 0), 1);

            mirror.Material.Roughness = roughness;

            return mirror;
        }

        Scene barely = SceneOf(
            200_000, new DistantLight { Direction = new Vector(1, 0, 0) }, new Plane { Material = Matte() }, Rough(0.02));

        Assert.AreEqual(1, LandedOver(barely, 0, 0, 0.2).Red, 0.02, "a mirror barely rough");

        const double roughness = 0.7;
        double alpha = roughness * roughness;
        Vector normal = new Vector(-1, -1, 0).Unit;
        (Vector across, Vector along) = Microfacets.FrameAround(normal);
        Vector toward = new (-1, 0, 0);
        Vector eye = new (toward.Dot(across), toward.Dot(along), toward.Dot(normal));
        double down = 0;
        const int steps = 400;

        for (int i = 0; i < steps; i++)
        {
            for (int j = 0; j < steps; j++)
            {
                Vector facet = Microfacets.SampleVisibleNormal(eye, alpha, (i + 0.5) / steps, (j + 0.5) / steps);
                Vector away = facet * (2 * eye.Dot(facet)) - eye;
                Vector world = across * away.X + along * away.Y + normal * away.Z;

                if (away.Z > 0 && world.Y < 0)
                    down += Microfacets.Masking(away.Z, alpha);
            }
        }

        Scene rough = SceneOf(
            400_000, new DistantLight { Direction = new Vector(1, 0, 0) }, new Plane { Material = Matte() }, Rough(roughness));
        double landed = rough.Caustics.Photons.Sum(photon => (double) photon.Red);

        Assert.AreEqual(Math.PI * Math.Sqrt(0.5) * down / (steps * steps), landed, 0.01 * landed);
    }

    /// <summary>
    /// This tests a frosted pane under an overhead sun: barely frosted, it lets the sun through as a clear
    /// one does; asked to turn no photons, it lets a shadow ray through as before.
    /// </summary>
    [TestMethod]
    public void TestAFrostedPaneLetsTheSunThrough()
    {
        Scene Under(double roughness, bool noCaustics)
        {
            Material frosted = Glass();

            frosted.Roughness = roughness;

            return SceneOf(
                200_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() },
                new Cube
                {
                    Material = frosted, NoCaustics = noCaustics,
                    Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(1, 0.05, 1)
                });
        }

        Assert.AreEqual(0.96 * 0.96 / (1 - 0.04 * 0.04), Shade(Under(0.02, false), new Point(0.05, 0.5, 0.1)), 0.03);

        Scene straight = Under(0.3, true);

        Assert.AreEqual(0, straight.Caustics.Count);
        Assert.AreEqual(0.96 * 0.96, Shade(straight, new Point(0.05, 0.5, 0.1)), 1e-9);
    }

    /// <summary>
    /// This tests a soap film.  Tipped to a level sun at 45 degrees it throws the floor its own colors --
    /// what it mirrors of each, at that angle.  Laid flat under an overhead sun, what it lets through goes
    /// straight on, as a shadow ray does, and is the shadow ray's alone: nothing more lands on the floor.
    /// </summary>
    [TestMethod]
    public void TestAFilmThrowsItsOwnColors()
    {
        Material Filmed() => new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Transparency = 1,
            Film = new ThinFilm { Thickness = 350 }
        };

        Parallelogram tipped = MirrorAt(new Point(0, 2, 0), 0);

        tipped.Material = Filmed();

        Scene thrown = SceneOf(
            1_000_000, new DistantLight { Direction = new Vector(1, 0, 0) }, new Plane { Material = Matte() }, tipped);
        Color expected = tipped.Material.Film.SharesAt<RgbSpectrum>(350, Math.Sqrt(0.5), 1, 1).Mirrored.ToColor();

        // Summed over the middle of the patch: a film mirrors a twentieth of the sun, so only a twentieth of
        // the photons come this way, chosen by chance.
        Color seen = LandedOver(thrown, 0, 0, 0.25);

        Assert.AreEqual(expected.Red, seen.Red, 0.03 * expected.Red, "red");
        Assert.AreEqual(expected.Green, seen.Green, 0.03 * expected.Green, "green");
        Assert.AreEqual(expected.Blue, seen.Blue, 0.03 * expected.Blue, "blue");

        Parallelogram flat = new ()
        {
            Point = new Point(-1, 1, -1), Side1 = new Vector(2, 0, 0), Side2 = new Vector(0, 0, 2), Material = Filmed()
        };
        Scene through = SceneOf(
            200_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() }, flat);
        Color passed = flat.Material.Film.SharesAt<RgbSpectrum>(350, 1, 1, 1).Passed.ToColor();
        Color under = ShadeColor(through, new Point(0.05, 0.5, 0.1));

        Assert.AreEqual(passed.Red, under.Red, 1e-9);
        Assert.AreEqual(passed.Blue, under.Blue, 1e-9);
    }

    /// <summary>
    /// This tests the sky sending photons: under a sky and nothing else, a glass ball gathers light into
    /// the middle of its shadow, where none comes straight.
    /// </summary>
    [TestMethod]
    public void TestTheSkySendsPhotons()
    {
        Scene scene = new () { CausticPhotons = 400_000, Background = new SolidPigment(Colors.White) };
        Plane floor = new () { Material = Matte() };
        Sphere ball = new () { Material = Glass(), Transform = Transforms.Translate(0, 1.5, 0) };

        floor.PrepareForRendering();
        ball.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ball);
        scene.Lights.Add(new SkyLight { Pigment = scene.Background, Samples = 256 });
        scene.ShootCausticPhotons();

        Assert.IsGreaterThan(0, scene.Caustics.Count);
        Assert.IsGreaterThan(
            0.05, scene.Caustics.Gather(Point.Zero, new Vector(0, 1, 0), false, _ => 1).Red / Math.PI,
            "light through the ball, under it");
    }

    /// <summary>
    /// This tests a glowing volume sending photons, from each of its places as a lamp would: a glow above
    /// and to one side of a glass ball lights the floor in the ball's shadow, where the line from the glow
    /// through the ball lands, though no light reaches there straight.
    /// </summary>
    [TestMethod]
    public void TestAGlowSendsPhotons()
    {
        Sphere glow = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Transparency = 1,
                Interior = new Interior { Medium = new Medium { Emission = new Color(4, 4, 4) } }
            },
            Transform = Transforms.Translate(-2, 4, 0) * Transforms.Scale(0.3, 0.3, 0.3)
        };
        Scene scene = new () { CausticPhotons = 400_000 };
        Plane floor = new () { Material = Matte() };
        Sphere ball = new () { Material = Glass(), Transform = Transforms.Translate(0, 1.5, 0) };

        foreach (Surface surface in new Surface[] { glow, floor, ball })
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(new VolumeLight(glow, glow.Material.Interior.Medium) { Samples = 16 });
        scene.ShootCausticPhotons();

        Point beyond = new (1.2, 0, 0);

        Assert.IsGreaterThan(0, scene.Caustics.Gather(beyond, new Vector(0, 1, 0), false, _ => 1).Red);
        Assert.IsTrue(
            Colors.Black.Matches(scene.GetLightReaching(beyond, (new Point(-2, 4, 0) - beyond).Unit, 4)),
            "no light should reach there straight, through the glass");
    }

    /// <summary>
    /// This tests the sky's photons for how much light they carry.  Under an even sky, each direction above
    /// the horizon lights the floor beneath a wide pane with what the pane lets through at its slant --
    /// (1 - R)/(1 + R) by Schlick's share at each face -- times the cosine it meets the floor at; over the
    /// whole sky, 2 times the integral of that over the cosine.  Summed over the middle of the floor, the
    /// photons' light comes to that, less the little that reaches there at a graze from beyond the pane's
    /// edge.
    /// </summary>
    [TestMethod]
    public void TestTheSkysPhotonsCarryItsLight()
    {
        SkyLight sky = new () { Pigment = new SolidPigment(Colors.White), Samples = 16 };
        Scene scene = new () { CausticPhotons = 1_000_000, Background = new SolidPigment(Colors.White) };
        Plane floor = new () { Material = Matte() };
        Cube pane = new () { Material = Glass(), Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(6, 0.05, 6) };

        floor.PrepareForRendering();
        pane.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(pane);
        scene.Lights.Add(sky);
        scene.ShootCausticPhotons();

        double expected = 0;
        const int steps = 10_000;

        for (int step = 0; step < steps; step++)
        {
            double cosine = (step + 0.5) / steps;
            double reflectance = 0.04 + 0.96 * Math.Pow(1 - cosine, 5);

            expected += 2 * (1 - reflectance) / (1 + reflectance) * cosine / steps;
        }

        double landed = scene.Caustics.Photons
            .Where(photon => Math.Abs(photon.X) < 2 && Math.Abs(photon.Z) < 2)
            .Sum(photon => (double) photon.Red);

        Assert.AreEqual(expected, landed / 16 / Math.PI, 0.02 * expected);
    }

    /// <summary>
    /// This tests a glowing volume's photons for how much light they carry.  Seen in a mirror tipped at 45
    /// degrees, a glow lights the floor beneath as its mirror image would, standing where the mirror shows
    /// it -- which, unlike a lamp, gives less the further off it is.  So the light its photons bring the floor
    /// is what the same glow, moved to its image's place, gives the floor straight.
    /// </summary>
    [TestMethod]
    public void TestAGlowsPhotonsCarryItsLight()
    {
        Sphere Glow(Point place) => new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Transparency = 1,
                Interior = new Interior { Medium = new Medium { Emission = new Color(40, 40, 40) } }
            },
            Transform = Transforms.Translate(place.X, place.Y, place.Z) * Transforms.Scale(0.2, 0.2, 0.2)
        };

        Scene Lit(Sphere glow, int photons, params Surface[] more)
        {
            Scene scene = new () { CausticPhotons = photons };

            foreach (Surface surface in more.Prepend(glow))
            {
                surface.PrepareForRendering();
                scene.Surfaces.Add(surface);
            }

            // Looked at from many places, so that the glow and its image average over the whole of the ball
            // alike, the image's places not being the mirror images of the glow's.
            scene.Lights.Add(new VolumeLight(glow, glow.Material.Interior.Medium) { Samples = 128 });
            scene.ShootCausticPhotons();

            return scene;
        }

        Scene seen = Lit(Glow(new Point(-1.5, 2, 0)), 400_000, new Plane { Material = Matte() }, MirrorAt(new Point(0, 2, 0), 1));
        Scene image = Lit(Glow(new Point(0, 3.5, 0)), 0, new Plane { Material = Matte() });

        // At the middle, and off it, where the place on the mirror is an eighth further from the glow and
        // gives a quarter less -- though still well inside the patch the mirror throws.
        foreach ((double x, double z) in new[] { (0.0, 0.0), (0.3, 0.4) })
        {
            double thrown = LandedOver(seen, x, z, 0.1).Red;
            double straight = ShadedOver(image, x, z, 0.1).Red;

            Assert.AreEqual(straight, thrown, 0.03 * straight, $"at ({x}, {z})");
        }
    }

    /// <summary>
    /// This tests that photons set off from anywhere over a light that is spread out, not from its samples
    /// alone: a lens brings each place a photon sets off from to a point of its own.  An area light focused
    /// by a glass ball makes a picture of itself, flat across the middle; a sky looked at in only four
    /// directions is gathered as a soft glow, not four hard points.  Set off from one place, the area light's
    /// picture was a point; set off from its samples, the sky's glow was four specks.
    /// </summary>
    [TestMethod]
    public void TestASpreadLightIsGatheredAsWideAsItIs()
    {
        Scene Under(Light light)
        {
            Scene scene = new () { CausticPhotons = 1_000_000, Background = new SolidPigment(Colors.White) };

            foreach (Surface surface in new Surface[]
                         { new Plane { Material = Matte() }, new Sphere { Material = Glass(), Transform = Transforms.Translate(0, 1.5, 0) } })
            {
                surface.PrepareForRendering();
                scene.Surfaces.Add(surface);
            }

            scene.Lights.Add(light);
            scene.ShootCausticPhotons();

            return scene;
        }

        double At(Scene scene, double x, double z) =>
            scene.Caustics.Gather(new Point(x, 0, z), new Vector(0, 1, 0), false, _ => 1).Red / Math.PI;

        Scene panel = Under(new AreaLight
        {
            Location = new Point(0, 12, 0), Axis1 = new Vector(4, 0, 0), Axis2 = new Vector(0, 0, 4), USteps = 2, VSteps = 2
        });

        // Nearly as bright a little off the middle as at it -- a ball's picture of a panel falls off a little
        // toward its edge -- where from one place it was a point.
        Assert.IsGreaterThan(
            0.8, LandedOver(panel, 0.15, 0, 0.05).Red / LandedOver(panel, 0, 0, 0.05).Red, "the panel's picture, wide");

        Scene sky = Under(new SkyLight { Pigment = new SolidPigment(Colors.White), Samples = 4 });
        List<double> glow = [];

        for (int i = 0; i <= 20; i++)
        {
            for (int j = 0; j <= 20; j++)
                glow.Add(At(sky, -1 + i * 0.1, -1 + j * 0.1));
        }

        Assert.IsLessThan(3, glow.Max() / glow.Average(), "the sky's glow under the ball, with no hard points");
    }

    /// <summary>
    /// This tests that bounced light does not find what the photons carry.  A floor before an endless
    /// mirror under an even sky sees the sky over half its view and the sky's mirror image over the other
    /// half, which bounced light finds by way of the mirror.  With caustics, the sky's photons carry what
    /// reaches the floor by way of a mirror, so a bounce finds it no longer -- and an endless mirror cannot be
    /// aimed at, so here nothing does: the floor keeps only the half it sees straight.  A backdrop that is no
    /// light is found as before.
    /// </summary>
    [TestMethod]
    public void TestABounceFindsNothingThePhotonsCarry()
    {
        double Lit(int photons, bool skyLight = true)
        {
            Scene scene = new ()
            {
                CausticPhotons = photons, Background = new SolidPigment(Colors.White), DiffuseBounces = 1,
                DiffuseSamples = 1024
            };
            Plane floor = new () { Material = Matte() };
            Plane mirror = new ()
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1
                },
                Transform = Transforms.RotateAroundZ(-Math.PI / 2, true)
            };

            floor.PrepareForRendering();
            mirror.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Surfaces.Add(mirror);
            if (skyLight)
                scene.Lights.Add(new SkyLight { Pigment = scene.Background, Samples = 1024 });

            scene.ShootCausticPhotons();

            return Shade(scene, new Point(1, 1, 0));
        }

        double without = Lit(0);
        double with = Lit(100_000);

        Assert.AreEqual(1, without, 0.03, "without caustics, the sky and its mirror image");
        Assert.AreEqual(0.5, with, 0.03, "with them, the sky alone");

        // A sky with no sky light is a backdrop, which sends no photons and lights nothing straight; what a
        // bounce finds of it in the mirror is found with caustics as without.
        Assert.AreEqual(Lit(0, false), Lit(100_000, false), 1e-9, "a backdrop's mirror image");
        Assert.AreEqual(0.5, Lit(100_000, false), 0.03, "a backdrop's mirror image, the half of the view it fills");
    }

    /// <summary>
    /// This method makes a block of heavy flint glass, which spreads colors widely, standing round the floor
    /// from a little below it to a unit above, lit by a sun 60 degrees from overhead: light comes in through
    /// its top and lands on the floor inside it.
    /// </summary>
    private static Scene InFlint(bool spectral)
    {
        Material flint = Glass();

        flint.Interior = new Interior { Glass = "SF10" };

        Scene scene = new () { CausticPhotons = 400_000, CausticsSpectral = spectral };

        foreach (Surface surface in new Surface[]
                 {
                     new Plane { Material = Matte() },
                     new Cube { Material = flint, Transform = Transforms.Translate(0, 0.25, 0) * Transforms.Scale(2, 0.75, 2) }
                 })
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(new DistantLight { Direction = new Vector(Math.Sin(Math.PI / 3), -Math.Cos(Math.PI / 3), 0) });
        scene.ShootCausticPhotons();

        return scene;
    }

    /// <summary>
    /// This tests glass that spreads colors, in a render that carries them band by band.  Every photon that
    /// goes in through the flint's top is split into one band and bent by the flint's index at a wavelength
    /// within it -- so the way it is going when it lands, read back through Snell's law, gives an index
    /// within that band's range of the flint's.  Every band has its photons.  In a render in colors, none
    /// is split.
    /// </summary>
    [TestMethod]
    public void TestGlassThatSpreadsColorsSplitsPhotons()
    {
        Interior flint = new () { Glass = "SF10" };
        double width = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / SpectralColor.Bands;
        List<PhotonMap.Photon> split = InFlint(true).Caustics.Photons
            .Where(photon => photon.Band != PhotonMap.Photon.EveryBand && photon.Dy < -0.7)
            .ToList();

        Assert.HasCount(SpectralColor.Bands, split.Select(photon => photon.Band).Distinct(), "every band");

        foreach (IGrouping<byte, PhotonMap.Photon> band in split.GroupBy(photon => photon.Band))
        {
            double shortest = SpectralColor.WavelengthOf(band.Key) - width / 2;
            double most = flint.IndexOfRefractionAt(shortest);
            double least = flint.IndexOfRefractionAt(shortest + width);
            List<double> indices = band
                .Select(photon => Math.Sin(Math.PI / 3) / Math.Sqrt(photon.Dx * photon.Dx + photon.Dz * photon.Dz))
                .ToList();

            Assert.IsTrue(
                indices.All(index => index >= least - 1e-4 && index <= most + 1e-4),
                $"band {band.Key} bent as if by indices from {indices.Min()} to {indices.Max()}");

            // Bent at wavelengths across the band, not at its middle alone: bands bent at one wavelength each
            // laid a rainbow down as so many stripes.
            Assert.IsGreaterThan(0.8 * (most - least), indices.Max() - indices.Min(), $"band {band.Key}'s spread");
        }

        Assert.IsTrue(InFlint(false).Caustics.Photons.All(photon => photon.Band == PhotonMap.Photon.EveryBand));
    }

    /// <summary>
    /// This tests that photons split into bands carry the light, band by band, that the photons of a render
    /// in colors carry as a color: summed over the middle of the floor inside the flint and turned back into
    /// a color, the split photons' light under a white sun is what the colored ones' is, to within what the
    /// flint's index varying with the band does to the share each face lets through.
    /// </summary>
    [TestMethod]
    public void TestSplitPhotonsCarryTheirShareOfTheLight()
    {
        double[] bands = new double[SpectralColor.Bands];

        foreach (PhotonMap.Photon photon in InFlint(true).Caustics.Photons)
        {
            if (photon.Band != PhotonMap.Photon.EveryBand && Math.Abs(photon.X) < 0.5 && Math.Abs(photon.Z) < 0.5)
                bands[photon.Band] += photon.Red;
        }

        Color split = SpectralBasis.Shared.ToColor(bands, 1);
        Color colored = LandedOver(InFlint(false), 0, 0, 0.5) * Math.PI;

        Assert.AreEqual(colored.Red, split.Red, 0.03 * colored.Red, "red");
        Assert.AreEqual(colored.Green, split.Green, 0.03 * colored.Green, "green");
        Assert.AreEqual(colored.Blue, split.Blue, 0.03 * colored.Blue, "blue");
    }

    /// <summary>
    /// This tests that a photon split into one band takes the share of each surface it meets after in that
    /// band: split by a pane of clear flint, the bands go on through a pane of red glass below it, which lets
    /// next to none of the blue ones through -- a fifth as much as of the red, turning a red pigment into a
    /// spectrum leaving a little blue in it, where taken as a color the share let every band through alike.
    /// And the floor beneath, shaded band by band, takes that light: red, with next to no blue.
    /// </summary>
    [TestMethod]
    public void TestASplitPhotonIsFilteredInItsOwnBand()
    {
        Material flint = Glass();
        Material red = Glass();

        flint.Interior = new Interior { Glass = "SF10" };
        red.Pigment = new SolidPigment(new Color(1, 0, 0));
        red.Interior = new Interior { IndexOfRefraction = 1.5, Filter = 1 };

        Scene scene = new () { CausticPhotons = 400_000, CausticsSpectral = true };

        foreach (Surface surface in new Surface[]
                 {
                     new Plane { Material = Matte() },
                     new Cube { Material = flint, Transform = Transforms.Translate(0, 1.5, 0) * Transforms.Scale(2, 0.1, 2) },
                     new Cube { Material = red, Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(2, 0.1, 2) }
                 })
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(new DistantLight { Direction = new Vector(0, -1, 0) });
        scene.ShootCausticPhotons();

        double[] bands = new double[SpectralColor.Bands];

        foreach (PhotonMap.Photon photon in scene.Caustics.Photons.Where(photon => photon.Band != PhotonMap.Photon.EveryBand))
            bands[photon.Band] += photon.Red;

        double blue = bands[..8].Sum();
        double reddish = bands[^8..].Sum();

        Assert.IsGreaterThan(0, reddish);
        Assert.IsLessThan(0.4 * reddish, blue, "the blue, filtered by the red glass");

        Ray down = new (new Point(0.1, 0.5, 0.1), Directions.Down);
        List<Intersection> hits = scene.Intersect(down);
        Intersection floor = hits.Hit();

        floor.PrepareUsing(down, hits);

        Color shaded = scene.GetHitColor<BandSpectrum>(floor, 4).ToColor();

        Assert.IsGreaterThan(0.4, shaded.Red, "the floor's red");
        Assert.IsLessThan(0.1 * shaded.Red, shaded.Blue, "the floor's blue");
    }

    /// <summary>
    /// This tests that photons are sent the same way every time, however the work is shared among threads.
    /// </summary>
    [TestMethod]
    public void TestCausticsComeOutTheSameEveryTime()
    {
        Scene Made() => SceneOf(
            100_000, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() },
            new Sphere { Material = Glass(), Transform = Transforms.Translate(0, 1.5, 0) });

        PhotonMap one = Made().Caustics;
        PhotonMap other = Made().Caustics;

        Assert.AreEqual(one.Count, other.Count);

        for (int index = 0; index < one.Count; index++)
            Assert.AreEqual(one.Photons[index], other.Photons[index], $"photon {index}");
    }

    /// <summary>
    /// This tests that nothing changes unless caustics are asked for: no photons, and glass lets a shadow
    /// ray through as it always has.
    /// </summary>
    [TestMethod]
    public void TestNothingChangesUnlessCausticsAreAskedFor()
    {
        Scene scene = SceneOf(
            0, new DistantLight { Direction = new Vector(0, -1, 0) }, new Plane { Material = Matte() },
            new Cube { Material = Glass(), Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(1, 0.05, 1) });

        Assert.IsNull(scene.Caustics);
        Assert.AreEqual(0.96 * 0.96, Shade(scene, new Point(0.05, 0.5, 0.1)), 1e-9);
    }

    /// <summary>
    /// This tests the words a scene says it with: `caustics`, with or without a count, followed by more of
    /// the context block; and `no caustics` on a surface.
    /// </summary>
    [TestMethod]
    public void TestAScenesAskForCaustics()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"caustic-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // Looking straight down at the floor under a glass ball in a sun: the middle of its shadow.
            double Middle(string context, string ball)
            {
                File.WriteAllText(path,
                    $"context {{ no gamma  {context}  angles are degrees }}\n" +
                    "camera { location [0, 0.2, 0]  look at [0, 0, 0]  up [0, 0, 1]  field of view 1 }\n" +
                    "distant light { direction [0, -1, 0] }\n" +
                    "plane { material { pigment White  ambient 0  diffuse 1  specular 0 } }\n" +
                    "sphere { material { pigment White  ambient 0  diffuse 0  specular 0  transparency 1" +
                    $"  reflective 1  interior {{ ior 1.5 }} }}  translate Y 1.5  {ball} }}\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{context}' did not parse");

                renderer.Render(new RenderOptions
                {
                    OutputFileName = output, Width = 4, Height = 4, BitsPerChannel = 16, ProgressStyleText = "none"
                });

                return new ImageFile(output).Load()[0].GetPixel(2, 2).Red;
            }

            double plain = Middle("", "");

            Assert.AreEqual(0.96 * 0.96, plain, 0.01, "no caustics: the shadow ray straight through");
            Assert.IsGreaterThan(0.99, Middle("caustics", ""), "caustics should gather the sun there");
            Assert.IsGreaterThan(0.99, Middle("caustics photons 100000", ""), "and with a count");
            Assert.AreEqual(plain, Middle("caustics photons 100000", "no caustics"), 1e-9,
                "a ball that turns no photons should shadow as before");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
