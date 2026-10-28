using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover the light of the glowing surfaces: what a surface that glows of its own casts, looked at
/// as a light in a scene that follows bounced light.  Each is held to an answer worked out here, or to what
/// bounced light alone finds, since the light stands in for that.
/// </summary>
[TestClass]
public class TestEmissionLight
{
    private static Material Matte(Color color, double diffuse) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = diffuse, Specular = 0
    };

    private static Material Glowing(Color glow) => new ()
    {
        Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = glow, Diffuse = 0, Specular = 0
    };

    private static Color Shade(Scene scene, Ray ray)
    {
        List<Intersection> hits = scene.Intersect(ray);
        Intersection hit = hits.Hit();

        Assert.IsNotNull(hit, "the ray met nothing");

        hit.PrepareUsing(ray, hits);

        return scene.GetHitColor(hit, 4);
    }

    /// <summary>
    /// This method makes a ball of radius a half, glowing at the given strength, standing two above an endless
    /// white floor that takes all the light falling on it.
    /// </summary>
    private static Scene ABallOverAFloor(double glow, int bounces, int looks, Material floor = null)
    {
        Sphere ball = new ()
        {
            Material = Glowing(new Color(glow, glow, glow)),
            Transform = Transforms.Translate(0, 2, 0) * Transforms.Scale(0.5)
        };
        Plane ground = new () { Material = floor ?? Matte(Colors.White, 1) };
        Scene scene = new () { DiffuseBounces = bounces, DiffuseSamples = looks };

        ball.PrepareForRendering();
        ground.PrepareForRendering();
        scene.Surfaces.Add(ball);
        scene.Surfaces.Add(ground);

        return scene;
    }

    // The floor under the ball, seen from off to one side.
    private static readonly Ray UnderTheBall = new (new Point(-3, 1, 0), new Vector(3, -1, 0).Unit);

    /// <summary>
    /// This tests a ball's light on the floor straight under it against the exact answer.  A ball of radius r
    /// glowing at L, its middle d above a point on a floor facing it, lights the point with π L (r / d)²; a
    /// white floor taking all of it gives back that over π, so L (r / d)² -- here a sixteenth of the glow.  The
    /// floor looks for bounced light too, in as many directions as the light is looked at from, and the light
    /// must not be counted twice: a search running straight into the ball finds it black.
    /// </summary>
    [TestMethod]
    public void TestABallLightsTheFloorUnderItAsTheExactAnswerSays()
    {
        Scene scene = ABallOverAFloor(2, 1, 4096);

        scene.LightTheGlowingSurfaces(4096, 1);

        Assert.IsNotNull(scene.GlowingSurfaces, "the glowing ball should have been made a light");

        Color seen = Shade(scene, UnderTheBall);

        Assert.AreEqual(2.0 / 16, seen.Red, 0.002, $"the floor under the ball showed {seen.Red}");
    }

    /// <summary>
    /// This tests that the light finds what bounced light alone finds, on average: a floor beside the ball, lit
    /// at a slant, comes to the same with the light as with only its search for bounced light, looking in many
    /// directions -- and the light's few samples get there as surely as the many.
    /// </summary>
    [TestMethod]
    public void TestTheLightFindsWhatBouncedLightAloneFinds()
    {
        Ray beside = new (new Point(-3, 1, 0.5), new Vector(4.5, -1, -0.5).Unit);
        Scene alone = ABallOverAFloor(2, 1, 262_144);
        Color found = Shade(alone, beside);
        Scene lit = ABallOverAFloor(2, 1, 64);

        lit.LightTheGlowingSurfaces(64, 1);

        Color seen = Shade(lit, beside);

        Assert.IsTrue(found.Red > 0.01, $"the floor beside the ball found only {found.Red} by bounced light");
        Assert.AreEqual(found.Red, seen.Red, found.Red * 0.02,
            $"the light gave the floor beside the ball {seen.Red}; bounced light alone found {found.Red}");
    }

    /// <summary>
    /// This tests that a rough mirror shares a glowing ball between its highlight and its reflection, the two
    /// coming to what its reflection alone finds: counted by both in full, the ball's image in it would be
    /// twice as bright; by the highlight alone, the reflection would find it black and it would lose its due.
    /// </summary>
    [TestMethod]
    public void TestARoughMirrorSharesAGlowBetweenItsHighlightAndItsReflection()
    {
        Material Rough(int looks) => new ()
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1,
            Metallic = 1, Roughness = 0.4, ReflectionSamples = looks
        };

        // Seen from where the floor mirrors the ball's middle into the eye.
        Ray towardTheImage = new (new Point(-3, 1, 0), new Vector(1, -1, 0).Unit);
        Scene alone = ABallOverAFloor(2, 1, 1, Rough(65_536));
        Color found = Shade(alone, towardTheImage);
        Scene lit = ABallOverAFloor(2, 1, 1, Rough(1024));

        lit.LightTheGlowingSurfaces(1024, 1);

        Color seen = Shade(lit, towardTheImage);

        Assert.IsTrue(found.Red > 0.05, $"the rough mirror showed only {found.Red} of the ball by reflection");
        Assert.AreEqual(found.Red, seen.Red, found.Red * 0.02,
            $"shared, the rough mirror showed {seen.Red}; its reflection alone found {found.Red}");
    }

    /// <summary>
    /// This tests a place inside the ball round a glowing surface, which looks over the whole sphere: under a
    /// glowing slab ten across, a tenth above the floor, the floor takes the slab's glow times the share of the
    /// floor's view it fills -- worked out for each quarter of the slab as a rectangle with a corner straight
    /// overhead.
    /// </summary>
    [TestMethod]
    public void TestAPlaceRightUnderABigGlowIsLitByItAsTheExactAnswerSays()
    {
        Cube slab = new ()
        {
            Material = Glowing(new Color(1, 1, 1)),
            Transform = Transforms.Translate(0, 0.11, 0) * Transforms.Scale(5, 0.01, 5)
        };
        Plane ground = new () { Material = Matte(Colors.White, 1) };
        Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = 1 };

        slab.PrepareForRendering();
        ground.PrepareForRendering();
        scene.Surfaces.Add(slab);
        scene.Surfaces.Add(ground);
        scene.LightTheGlowingSurfaces(16_384, 1);

        // A rectangle a by b, parallel to a place and h above it, with a corner straight over it, fills this much
        // of its view.
        double side = 5 / 0.1;
        double across = side / Math.Sqrt(1 + side * side);
        double quarter = (across * Math.Atan(across) + across * Math.Atan(across)) / (2 * Math.PI);
        Color seen = Shade(scene, new Ray(new Point(-6, 0.05, 0), new Vector(6, -0.05, 0).Unit));

        Assert.AreEqual(4 * quarter, seen.Red, 0.005, $"the floor under the slab showed {seen.Red}");
    }

    /// <summary>
    /// This tests that a scene says how many places its glowing surfaces are looked at from, in its context,
    /// and that it lights a floor as the exact answer says; and that it cannot say none.
    /// </summary>
    [TestMethod]
    public void TestASceneSaysHowManyPlacesItsGlowsAreLookedAtFrom()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"emission-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            Canvas Rendered(int samples)
            {
                File.WriteAllText(path,
                    "context { width 9  height 9  no gamma  diffuse bounces 1  diffuse samples 1  " +
                    $"emission samples {samples} }}\n" +
                    "camera { location [-3, 1, 0]  look at [0, 0, 0] }\n" +
                    "plane { material { pigment White  diffuse 1  specular 0 } }\n" +
                    "sphere { material { pigment White  emission 2  diffuse 0  specular 0 }  scale 0.5  translate Y 2 }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                renderer.Render(new RenderOptions { OutputFileName = output });

                return new ImageFile(output).Load()[0];
            }

            Assert.AreEqual(2.0 / 16, Rendered(4096).GetPixel(4, 4).Red, 0.003,
                "a glowing ball should light the floor under it as the exact answer says");
            Assert.ThrowsExactly<TokenException>(() => Rendered(0), "a glow looked at from no place at all");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// This tests that a scene that does not follow bounced light has no light of its glowing surfaces: there
    /// a glow is only seen, and lights nothing, as it always has.
    /// </summary>
    [TestMethod]
    public void TestNoLightIsMadeWithoutBouncedLight()
    {
        Scene scene = ABallOverAFloor(2, 0, 1);

        scene.LightTheGlowingSurfaces(16, 1);

        Assert.IsNull(scene.GlowingSurfaces, "a scene without bounced light should have no light of its glows");
        Assert.AreEqual(0, scene.Lights.Count);
    }

    /// <summary>
    /// This tests that what stands for a light already is no part of the light: something that casts no shadow,
    /// which bounced light passes as a shadow ray does.
    /// </summary>
    [TestMethod]
    public void TestWhatCastsNoShadowIsNoPartOfTheLight()
    {
        Scene scene = ABallOverAFloor(2, 1, 1);

        scene.Surfaces[0].NoShadow = true;
        scene.LightTheGlowingSurfaces(16, 1);

        Assert.IsNull(scene.GlowingSurfaces, "a glow that casts no shadow should be no part of the light");
    }

    /// <summary>
    /// This tests that a glowing shape shared between two places glows as a light in both: the floor under each
    /// is lit alike.
    /// </summary>
    [TestMethod]
    public void TestASharedGlowLightsFromEveryPlaceItStands()
    {
        Sphere ball = new () { Material = Glowing(new Color(2, 2, 2)), Transform = Transforms.Scale(0.5) };
        Instance left = new () { Prototype = ball, Transform = Transforms.Translate(-5, 2, 0) };
        Instance right = new () { Prototype = ball, Transform = Transforms.Translate(5, 2, 0) };
        Plane ground = new () { Material = Matte(Colors.White, 1) };
        Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = 1 };

        left.PrepareForRendering();
        right.PrepareForRendering();
        ground.PrepareForRendering();
        scene.Surfaces.Add(left);
        scene.Surfaces.Add(right);
        scene.Surfaces.Add(ground);
        scene.LightTheGlowingSurfaces(4096, 1);

        Assert.AreEqual(2, scene.GlowingSurfaces.Count, "the ball should glow as a light in both its places");

        Color underLeft = Shade(scene, new Ray(new Point(-8, 1, 0), new Vector(3, -1, 0).Unit));
        Color underRight = Shade(scene, new Ray(new Point(2, 1, 0), new Vector(3, -1, 0).Unit));

        // Each also lights the floor under the other a little, from ten across: (r / d)² with d the distance.
        double far = 2 * 0.25 / 104 * (2 / Math.Sqrt(104));

        Assert.AreEqual(2.0 / 16 + far, underLeft.Red, 0.003, $"the floor under the left ball showed {underLeft.Red}");
        Assert.AreEqual(2.0 / 16 + far, underRight.Red, 0.003, $"the floor under the right ball showed {underRight.Red}");
    }

    /// <summary>
    /// This method makes a tube glowing at the given strength over an endless white floor that takes all the
    /// light falling on it: a straight stroke at a slant, then a curve.
    /// </summary>
    private static Scene ATubeOverAFloor(double radius, int looks)
    {
        TubeControlPoint At(double x, double y, double z) => new () { Center = new Point(x, y, z), Radius = radius };

        Tube tube = new () { Material = Glowing(new Color(2, 2, 2)), Start = At(-1, 0.3, -0.5), Discontinuous = true };
        Plane ground = new () { Material = Matte(Colors.White, 1) };
        Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = looks };

        tube.Segments.Add(new TubeSegmentSpec { End = At(0.6, 0.5, 0.3) });
        tube.Segments.Add(new TubeSegmentSpec
        {
            Control1 = At(1.2, 0.8, 0.6), Control2 = At(1.4, 0.2, -0.4), End = At(0.8, 0.4, -0.9)
        });
        tube.PrepareForRendering();
        ground.PrepareForRendering();
        scene.Surfaces.Add(tube);
        scene.Surfaces.Add(ground);

        return scene;
    }

    /// <summary>
    /// This tests that a tube's light is what bounced light alone finds, on average: the tube has a row of balls
    /// round each of its parts, overlapping, and a direction must be as likely as all the balls holding it make
    /// it -- counted by one, the floor under the tube came out too bright.
    /// </summary>
    [TestMethod]
    public void TestATubesLightIsWhatBouncedLightAloneFinds()
    {
        Ray under = new (new Point(-1.5, 1, 1), new Vector(1.6, -1, -1.1).Unit);
        Color found = Shade(ATubeOverAFloor(0.05, 262_144), under);
        Scene lit = ATubeOverAFloor(0.05, 1);

        lit.LightTheGlowingSurfaces(16_384, 1);

        Color seen = Shade(lit, under);

        Assert.IsTrue(found.Red > 0.05, $"the floor under the tube found only {found.Red} by bounced light");
        Assert.AreEqual(found.Red, seen.Red, found.Red * 0.02,
            $"the light gave the floor under the tube {seen.Red}; bounced light alone found {found.Red}");
    }

    /// <summary>
    /// This tests a place with the same glowing curve on both sides of it -- a post standing inside a tube bent
    /// into a U -- which bounced light alone finds lit as the light does: a ball of the curve behind the place
    /// holds no direction ahead of it, and counted as holding one, the post came out too dark.
    /// </summary>
    [TestMethod]
    public void TestAPlaceInsideABentTubeIsLitAsBouncedLightAloneFinds()
    {
        Scene Made(int looks)
        {
            TubeControlPoint At(double x, double z) => new () { Center = new Point(x, 0.3, z), Radius = 0.05 };

            Tube tube = new () { Material = Glowing(new Color(2, 2, 2)), Start = At(-0.5, 0) };
            Cylinder post = new ()
            {
                Material = Matte(Colors.White, 1), Transform = Transforms.Translate(0, 0, 0.5) * Transforms.Scale(0.1, 1, 0.1)
            };
            Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = looks };

            tube.Segments.Add(new TubeSegmentSpec { Control1 = At(-0.5, 1.2), Control2 = At(0.5, 1.2), End = At(0.5, 0) });
            tube.PrepareForRendering();
            post.PrepareForRendering();
            scene.Surfaces.Add(tube);
            scene.Surfaces.Add(post);

            return scene;
        }

        // The side of the post facing the U's left arm, with its right arm straight behind.
        Ray toThePost = new (new Point(-0.3, 0.6, 0.5), new Vector(0.2, -0.3, 0).Unit);
        Color found = Shade(Made(262_144), toThePost);
        Scene lit = Made(1);

        lit.LightTheGlowingSurfaces(16_384, 1);

        Color seen = Shade(lit, toThePost);

        Assert.IsTrue(found.Red > 0.05, $"the post found only {found.Red} by bounced light");
        Assert.AreEqual(found.Red, seen.Red, found.Red * 0.02,
            $"the light gave the post {seen.Red}; bounced light alone found {found.Red}");
    }

    /// <summary>
    /// This tests that a thin tube is found by most of the light's looks at it, from a wall a hand's breadth
    /// behind it: one ball round a stroke at a slant is almost all empty, and the wall stands inside it, so
    /// the light found the tube one look in twenty-five and came out as grain.
    /// </summary>
    [TestMethod]
    public void TestAThinTubeIsFoundByMostOfTheLooksAtIt()
    {
        Scene scene = ATubeOverAFloor(0.006, 1);

        scene.LightTheGlowingSurfaces(1024, 1);

        Point wall = new Point(-0.2, 0.4, -0.1) + new Vector(0.4, -1.6, 1.6).Unit * 0.1;
        int found = Enumerable.Range(0, 1024).Count(index => scene.GlowingSurfaces.SampleToward(wall, index).Cone > 0);

        Assert.IsTrue(found > 1024 / 5, $"the tube was found by {found} of 1024 looks at it");
    }

    /// <summary>
    /// This tests that every one of the light's looks at a glowing ball finds it: the ball round it is the ball
    /// itself, where the ball round its box fills three times as much of the view.
    /// </summary>
    [TestMethod]
    public void TestEveryLookAtAGlowingBallFindsIt()
    {
        Scene scene = ABallOverAFloor(2, 1, 1);

        scene.LightTheGlowingSurfaces(1024, 1);

        Point floor = new (1, 0, 0.5);
        int found = Enumerable.Range(0, 1024).Count(index => scene.GlowingSurfaces.SampleToward(floor, index).Cone > 0);

        Assert.AreEqual(1024, found, "every look at the ball should find it");
    }

    /// <summary>
    /// This tests that a curved run of tube lies within the boxes laid along it: every sphere along its middle,
    /// as wide as the tube is at its widest, inside one of them.
    /// </summary>
    [TestMethod]
    public void TestACurvedTubeLiesWithinTheBoxesLaidAlongIt()
    {
        Point[] controls = [new (0, 0, 0), new (2, 3, 0), new (3, -1, 1), new (5, 1, 2)];
        const double radius = 0.1;
        Casing[] casings = Casing.AlongATube(controls, radius, 8).ToArray();

        for (int step = 0; step <= 1000; step++)
        {
            double t = step / 1000.0;
            double u = 1 - t;
            Point middle = Point.Zero +
                           ((controls[0] - Point.Zero) * (u * u * u) + (controls[1] - Point.Zero) * (3 * u * u * t) +
                            (controls[2] - Point.Zero) * (3 * u * t * t) + (controls[3] - Point.Zero) * (t * t * t));

            bool Within(Casing casing) => new[] { casing.First, casing.Second, casing.Third }.All(edge =>
            {
                double length = edge.Magnitude;
                double reach = (middle - casing.Corner).Dot(edge) / length;

                return reach >= radius - 1e-9 && reach <= length - radius + 1e-9;
            });

            Assert.IsTrue(casings.Any(Within), $"the tube's middle at {t} is outside every box laid along it");
        }
    }

    /// <summary>
    /// This tests that carried band by band, the light gives a white floor what it gives it carried as a color,
    /// under a white glow.
    /// </summary>
    [TestMethod]
    public void TestCarriedBandByBandTheLightGivesWhatItGivesAsAColor()
    {
        Scene scene = ABallOverAFloor(2, 1, 1);

        scene.LightTheGlowingSurfaces(4096, 1);

        List<Intersection> hits = scene.Intersect(UnderTheBall);
        Intersection hit = hits.Hit();

        hit.PrepareUsing(UnderTheBall, hits, 1, BandSpectrum.Wavelength);

        Color seen = scene.GetHitColor<BandSpectrum>(hit, 4).ToColor();

        Assert.AreEqual(2.0 / 16, seen.Green, 0.003, $"carried band by band, the floor under the ball showed {seen}");
    }
}
