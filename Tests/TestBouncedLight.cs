using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;
using RayTracer.Pigments;
using RayTracer.Renderer;

namespace Tests;

/// <summary>
/// These tests cover light bounced from one matte surface to the next.  Each is held to an exact answer
/// worked out here, since bounced light can be wrong by any amount and still look like light.
/// </summary>
[TestClass]
public class TestBouncedLight
{
    private static Material Matte(Color color, double diffuse) => new ()
    {
        Pigment = new SolidPigment(color), Ambient = 0, Diffuse = diffuse, Specular = 0
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
    /// This tests the furnace: a floor and a ceiling, endless, facing each other, each giving off 0.2 of its
    /// own and taking half the light that falls on it.  Each sees nothing but the other, the same
    /// everywhere, so each bounce gives back half the light of the one before: the glow is 0.2, one bounce
    /// adds 0.1, two 0.05, and so on.  Since every direction a bounce looks in finds the same light, the
    /// answer is exact whatever directions are picked.  There is no lamp at all.
    /// </summary>
    [TestMethod]
    public void TestEachBounceBetweenAFloorAndACeilingGivesBackItsShare()
    {
        for (int bounces = 0; bounces <= 3; bounces++)
        {
            Material glowing = new ()
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = new Color(0.2, 0.2, 0.2),
                Diffuse = 0.5, Specular = 0
            };
            Plane floor = new () { Material = glowing };
            Plane ceiling = new ()
            {
                Material = glowing, Transform = Transforms.Translate(0, 1, 0) * Transforms.RotateAroundX(Math.PI, true)
            };
            Scene scene = new () { DiffuseBounces = bounces, DiffuseSamples = 8 };

            floor.PrepareForRendering();
            ceiling.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Surfaces.Add(ceiling);

            double expected = 0.2 * (1 - Math.Pow(0.5, bounces + 1)) / (1 - 0.5);
            Color seen = Shade(scene, new Ray(new Point(0.2, 0.5, -0.1), new Vector(0.3, -1, 0.2).Unit));

            Assert.AreEqual(expected, seen.Red, 1e-9, $"{bounces} bounces gave {seen.Red}, not {expected}");
        }
    }

    /// <summary>
    /// This tests that emission is a surface's own light, tinted by its color, whatever lamps there are --
    /// here none -- where ambient is only ever lent by a lamp.
    /// </summary>
    [TestMethod]
    public void TestEmissionGlowsWithNoLampAtAll()
    {
        Plane floor = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(new Color(0.5, 1, 0.25)), Ambient = 0.3, Emission = new Color(2, 2, 2),
                Diffuse = 0.9, Specular = 0
            }
        };
        Scene scene = new ();

        floor.PrepareForRendering();
        scene.Surfaces.Add(floor);

        Color seen = Shade(scene, new Ray(new Point(0, 3, 0), Directions.Down));

        Assert.IsTrue(new Color(1, 2, 0.5).Matches(seen), $"a floor giving off 2, colored (0.5, 1, 0.25), showed {seen}");
    }

    /// <summary>
    /// This tests that a glow leaves through the skin as the rest of a surface's own light does: one that mirrors by
    /// Fresnel shows its glow only for what it does not mirror, so a twentieth mirrored square on is a twentieth less
    /// glow, and at a graze, where it mirrors nearly everything, next to none.
    /// </summary>
    [TestMethod]
    public void TestAGlowUnderAFresnelSkinShowsWhatTheSkinLetsOut()
    {
        Plane floor = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = new Color(1, 1, 1),
                Diffuse = 0, Specular = 0, Reflective = 0.05, Fresnel = true
            }
        };
        Scene scene = new ();

        floor.PrepareForRendering();
        scene.Surfaces.Add(floor);

        Color square = Shade(scene, new Ray(new Point(0, 3, 0), Directions.Down));
        Color graze = Shade(scene, new Ray(new Point(0, 0.01, 0), new Vector(1, -0.005, 0).Unit));

        Assert.AreEqual(0.95, square.Red, 1e-6, "square on, a twentieth is mirrored");
        Assert.IsTrue(graze.Red < 0.2, $"at a graze nearly everything is mirrored, and {graze.Red} glowed through");
    }

    /// <summary>
    /// This tests that emission lights what is near it by bouncing: under an endless ceiling giving off 0.4,
    /// a floor taking half the light falling on it, with no lamp anywhere, comes back as 0.2 -- the ceiling
    /// fills its whole view.
    /// </summary>
    [TestMethod]
    public void TestEmissionLightsItsNeighborsByBouncing()
    {
        Plane floor = new () { Material = Matte(Colors.White, 0.5) };
        Plane ceiling = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = new Color(0.4, 0.4, 0.4),
                Diffuse = 0, Specular = 0
            },
            Transform = Transforms.Translate(0, 2, 0) * Transforms.RotateAroundX(Math.PI, true)
        };
        Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = 8 };

        floor.PrepareForRendering();
        ceiling.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(ceiling);

        Color seen = Shade(scene, new Ray(new Point(0, 1, 0), Directions.Down));

        Assert.AreEqual(0.2, seen.Red, 1e-9, $"the ceiling's glow should give the floor 0.2, and gave {seen.Red}");
    }

    /// <summary>
    /// This tests a lamp inside a hollow ball whose wall takes half the light falling on it.  The lamp
    /// lights the wall seen from inside -- which, worked out from just outside the wall as glass met from
    /// within is, it never did -- to 0.5; and since every place on the inside of a ball sees the rest of the
    /// inside the same, the wall is everywhere as bright, and each bounce gives back half the one before.
    /// </summary>
    [TestMethod]
    public void TestALampInsideABallLightsItsWallAndEachBounceGivesBackItsShare()
    {
        for (int bounces = 0; bounces <= 3; bounces++)
        {
            Sphere ball = new () { Material = Matte(Colors.White, 0.5) };
            Scene scene = new () { DiffuseBounces = bounces, DiffuseSamples = 8 };

            ball.PrepareForRendering();
            scene.Surfaces.Add(ball);
            scene.Lights.Add(new PointLight { Location = Point.Zero, Color = Colors.White });

            double expected = 0.5 * (1 - Math.Pow(0.5, bounces + 1)) / (1 - 0.5);
            Color seen = Shade(scene, new Ray(Point.Zero, new Vector(0.3, 0.5, 0.8).Unit));

            Assert.AreEqual(expected, seen.Red, 1e-9, $"{bounces} bounces gave {seen.Red}, not {expected}");
        }
    }

    /// <summary>
    /// This tests light bounced off a sunlit wall onto the floor in front of it.  The wall is endless and
    /// upright, so every direction a point on the floor looks in that heads toward the wall meets it --
    /// half the floor's view, weighed as a matte surface weighs it -- and nothing else is there but a
    /// black sky.  One bounce brings the floor half the wall's light, times what the floor takes; a
    /// second brings it half of the light the wall had bounced onto it off the floor in turn.
    /// </summary>
    [TestMethod]
    public void TestAWallBouncesHalfItsLightOntoTheFloorBeforeIt()
    {
        const double FloorTakes = 0.8, WallTakes = 0.6;
        double slant = Math.Sqrt(0.5);
        double floorLit = FloorTakes * slant, wallLit = WallTakes * slant;

        Color OnTheFloor(int bounces)
        {
            Plane floor = new () { Material = Matte(Colors.White, FloorTakes) };
            Plane wall = new ()
            {
                Material = Matte(new Color(1, 0, 0), WallTakes),
                Transform = Transforms.Translate(1, 0, 0) * Transforms.RotateAroundZ(Math.PI / 2, true)
            };
            Scene scene = new () { DiffuseBounces = bounces, DiffuseSamples = 4096 };

            floor.PrepareForRendering();
            wall.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Surfaces.Add(wall);

            // A sun up and to the left of the wall's face, lighting the floor and the wall alike.
            scene.Lights.Add(new PointLight { Location = new Point(-1e6, 1e6, 0), Color = Colors.White });

            return Shade(scene, new Ray(new Point(-0.5, 2, 0.3), new Vector(0.5, -2, -0.3).Unit));
        }

        Color none = OnTheFloor(0);
        Color once = OnTheFloor(1);
        Color twice = OnTheFloor(2);

        Assert.AreEqual(floorLit, none.Red, 1e-6, "the sun alone");

        double expectedOnce = FloorTakes * 0.5 * wallLit;
        double expectedTwice = FloorTakes * 0.5 * (wallLit + WallTakes * 0.5 * floorLit);

        Assert.AreEqual(expectedOnce, once.Red - none.Red, expectedOnce * 0.03, $"one bounce gave {once.Red - none.Red}");
        Assert.AreEqual(expectedTwice, twice.Red - none.Red, expectedTwice * 0.05, $"two bounces gave {twice.Red - none.Red}");

        // The wall is red, so none of its light is green -- and the floor's own green, bounced off the
        // wall, comes back as nothing.
        Assert.AreEqual(none.Green, twice.Green, 1e-9, "a red wall bounced green light");
    }

    /// <summary>
    /// This tests that a matte surface weighs the light arriving from each direction by how squarely it
    /// comes, as a matte surface does: under an endless glowing roof sloped at 45 degrees, a point on the
    /// floor takes (1 + cos 45°) / 2 of the roof's glow, 0.854 of it -- not the 0.75 of the view the roof
    /// fills counted evenly, which is what looking evenly and forgetting the weighing gives.  A wall or a
    /// ceiling cannot tell the two apart, filling half the view or all of it either way.
    /// </summary>
    [TestMethod]
    public void TestASlopingRoofLightsTheFloorByHowSquarelyItFacesIt()
    {
        Vector up = new Vector(Math.Sin(Math.PI / 4), Math.Cos(Math.PI / 4), 0);
        Plane floor = new () { Material = Matte(Colors.White, 0.8) };
        Plane roof = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White), Ambient = 0.2, Diffuse = 0, Specular = 0
            },
            Transform = Transforms.Translate(up.X, up.Y, up.Z) * Transforms.RotateAroundZ(-Math.PI / 4, true)
        };
        Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = 4096 };

        floor.PrepareForRendering();
        roof.PrepareForRendering();
        scene.Surfaces.Add(floor);
        scene.Surfaces.Add(roof);
        scene.Lights.Add(new PointLight { Location = new Point(0, -10, 0), Color = Colors.White });

        double expected = 0.8 * 0.2 * (1 + Math.Cos(Math.PI / 4)) / 2;
        Color seen = Shade(scene, new Ray(new Point(-0.2, 0.3, 0.1), new Vector(0.2, -0.3, -0.1).Unit));

        Assert.AreEqual(expected, seen.Red, expected * 0.01, $"the roof should give the floor {expected}, and gave {seen.Red}");
    }

    /// <summary>
    /// This tests that the sky is not counted twice.  A floor under an open sky sees nothing but the sky,
    /// which its sky light has already counted, so asking for bounced light changes nothing about it.
    /// </summary>
    [TestMethod]
    public void TestAFloorUnderAnOpenSkyBouncesNothing()
    {
        Color Under(int bounces)
        {
            Plane floor = new () { Material = Matte(Colors.White, 0.9) };
            Scene scene = new ()
            {
                Background = new SolidPigment(new Color(0.3, 0.4, 0.6)), DiffuseBounces = bounces
            };

            floor.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Lights.Add(new SkyLight { Pigment = scene.Background, Samples = 64 });

            return Shade(scene, new Ray(new Point(0, 3, 0), Directions.Down));
        }

        Assert.IsTrue(Under(0).Matches(Under(2)), $"without bounces {Under(0)}, with two {Under(2)}");
    }

    /// <summary>
    /// This tests that the sky through glass is not counted twice either.  With no caustics a shadow ray passes glass
    /// straight, so the sky light counts the sky through a pane over the floor; a search for bounced light bent by
    /// the pane's index came out of it beside its straight run, found the sky, and lit the floor twice over.
    /// </summary>
    [TestMethod]
    public void TestAFloorUnderAPaneOfGlassBouncesNothingOfTheSky()
    {
        Color Under(int bounces)
        {
            Plane floor = new () { Material = Matte(Colors.White, 0.9) };
            Cube pane = new ()
            {
                Transform = Transforms.Translate(0, 1, 0) * Transforms.Scale(50, 0.05, 50),
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
                    Transparency = 1, Interior = new Interior { IndexOfRefraction = 1.5 }
                }
            };
            Scene scene = new ()
            {
                Background = new SolidPigment(new Color(0.3, 0.4, 0.6)), DiffuseBounces = bounces
            };

            floor.PrepareForRendering();
            pane.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Surfaces.Add(pane);
            scene.Lights.Add(new SkyLight { Pigment = scene.Background, Samples = 64 });

            return Shade(scene, new Ray(new Point(0, 0.5, 0), Directions.Down));
        }

        Color without = Under(0), with = Under(1);

        Assert.AreEqual(without.Blue, with.Blue, 0.02 * without.Blue, $"without bounces {without}, with one {with}");
    }

    /// <summary>
    /// This tests that a glowing volume is not counted twice: a floor beside a glowing ball, with nothing
    /// else to bounce light off, is no brighter for asking for bounced light, since the ball's glow
    /// reaches the floor through the ball's own light and a search for bounced light that runs through it
    /// leaves its glow out.
    /// </summary>
    [TestMethod]
    public void TestAGlowingVolumeIsNotCountedTwice()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"bounce-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            Canvas Render(int bounces)
            {
                string path = Path.Combine(directory, $"glow-{bounces}.igl");
                string output = Path.Combine(directory, $"glow-{bounces}.png");

                File.WriteAllText(path,
                    $"context {{ width 24  height 16  no gamma  diffuse bounces {bounces}  diffuse samples 16 }}\n" +
                    "camera { location [0, 2, -4]  look at [0, 0.3, 0] }\n" +
                    "plane { material { pigment White  diffuse 0.9  specular 0  ambient 0 } }\n" +
                    "sphere { material { pigment White  transparency 1  diffuse 0  specular 0  ambient 0\n" +
                    "    interior { medium { emission [2, 1.5, 1] } } }  gives light  scale 0.4  translate [0, 0.6, 0] }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, "the scene did not parse");

                renderer.Render(new RenderOptions { OutputFileName = output });

                return new ImageFile(output).Load()[0];
            }

            Canvas without = Render(0), with = Render(1);
            int differing = 0;

            for (int y = 0; y < without.Height; y++)
            {
                for (int x = 0; x < without.Width; x++)
                {
                    if (!without.GetPixel(x, y).Matches(with.GetPixel(x, y)))
                        differing++;
                }
            }

            Assert.AreEqual(0, differing, "asking for bounced light changed what the glow lit");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// This tests that bounced light passes what a shadow ray passes.  A glowing ball set out with no shadow
    /// stands for a light -- a painted sun -- and is not seen by a search for bounced light, so the floor
    /// beside it is no brighter for asking for it; the same ball casting a shadow is a glowing thing like
    /// any other, and lights the floor by bouncing.
    /// </summary>
    [TestMethod]
    public void TestBouncedLightPassesWhatCastsNoShadow()
    {
        Color OnTheFloor(int bounces, bool noShadow)
        {
            Plane floor = new () { Material = Matte(Colors.White, 0.9) };
            Sphere ball = new ()
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = Colors.White, Diffuse = 0,
                    Specular = 0
                },
                Transform = Transforms.Translate(1, 0.5, 0) * Transforms.Scale(0.4),
                NoShadow = noShadow
            };
            Scene scene = new () { DiffuseBounces = bounces, DiffuseSamples = 256 };

            floor.PrepareForRendering();
            ball.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Surfaces.Add(ball);
            scene.Lights.Add(new PointLight { Location = new Point(-3, 5, -2), Color = Colors.White });

            return Shade(scene, new Ray(new Point(0, 3, 0), Directions.Down));
        }

        Assert.IsTrue(OnTheFloor(0, true).Matches(OnTheFloor(1, true)), "a ball with no shadow was seen by bounced light");
        Assert.IsGreaterThan(OnTheFloor(0, false).Red + 0.01, OnTheFloor(1, false).Red, "a glowing ball should light the floor by bouncing");
    }

    /// <summary>
    /// This tests that bounced light carries light from one matte surface to the next and no highlights: a
    /// ball that only shines -- no diffuse at all, a broad highlight -- under a lamp lends the floor beside
    /// it nothing by bouncing.  The light a shiny surface focuses onto a matte one is a caustic.
    /// </summary>
    [TestMethod]
    public void TestBouncedLightTakesNoHighlights()
    {
        Color OnTheFloor(int bounces)
        {
            Plane floor = new () { Material = Matte(Colors.White, 0.9) };
            Sphere ball = new ()
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 1, Shininess = 5
                },
                Transform = Transforms.Translate(0.8, 0.6, 0) * Transforms.Scale(0.4)
            };
            Scene scene = new () { DiffuseBounces = bounces, DiffuseSamples = 256 };

            floor.PrepareForRendering();
            ball.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Surfaces.Add(ball);
            scene.Lights.Add(new PointLight { Location = new Point(-2, 4, -1), Color = Colors.White });

            return Shade(scene, new Ray(new Point(0, 3, 0), Directions.Down));
        }

        Assert.IsTrue(OnTheFloor(0).Matches(OnTheFloor(1)), $"a shining ball lent the floor {OnTheFloor(1)} over {OnTheFloor(0)}");
    }

    /// <summary>
    /// This tests that bounced light passes what a shadow ray passes all along its path, not only on its
    /// first straight run: a glowing ball set out with no shadow, seen from the floor in a mirror, lends
    /// the floor nothing -- the floor is as bright with it as without it.
    /// </summary>
    [TestMethod]
    public void TestBouncedLightPassesAStandInSeenInAMirror()
    {
        Color OnTheFloor(bool withBall)
        {
            Plane floor = new () { Material = Matte(Colors.White, 0.9) };
            Plane mirror = new ()
            {
                Material = new Material
                {
                    Pigment = new SolidPigment(Colors.Black), Ambient = 0, Diffuse = 0, Specular = 0, Reflective = 1
                },
                Transform = Transforms.Translate(1, 0, 0) * Transforms.RotateAroundZ(Math.PI / 2, true)
            };
            Scene scene = new () { DiffuseBounces = 1, DiffuseSamples = 512 };

            floor.PrepareForRendering();
            mirror.PrepareForRendering();
            scene.Surfaces.Add(floor);
            scene.Surfaces.Add(mirror);
            scene.Lights.Add(new PointLight { Location = new Point(-2, 4, -1), Color = Colors.White });

            if (withBall)
            {
                Sphere ball = new ()
                {
                    Material = new Material
                    {
                        Pigment = new SolidPigment(Colors.White), Ambient = 0, Emission = new Color(5, 5, 5),
                        Diffuse = 0, Specular = 0
                    },
                    Transform = Transforms.Translate(-0.6, 0.6, 0) * Transforms.Scale(0.3),
                    NoShadow = true
                };

                ball.PrepareForRendering();
                scene.Surfaces.Add(ball);
            }

            return Shade(scene, new Ray(new Point(0, 3, 0), Directions.Down));
        }

        Assert.IsTrue(OnTheFloor(false).Matches(OnTheFloor(true)), $"a stand-in seen in a mirror lent the floor {OnTheFloor(true)} over {OnTheFloor(false)}");
    }

    /// <summary>
    /// This tests that bounced light passes the bulb round a lamp, whose glow is the lamp's light counted
    /// already: a floor under a lamp in a glowing bulb is no brighter for asking for bounced light, though
    /// the bulb is plain to see.
    /// </summary>
    [TestMethod]
    public void TestBouncedLightPassesTheBulbRoundALamp()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"bounce-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            Canvas Render(int bounces)
            {
                string path = Path.Combine(directory, $"lamp-{bounces}.igl");
                string output = Path.Combine(directory, $"lamp-{bounces}.png");

                File.WriteAllText(path,
                    $"context {{ width 24  height 16  no gamma  diffuse bounces {bounces}  diffuse samples 32 }}\n" +
                    "camera { location [0, 2, -4]  look at [0, 0.3, 0] }\n" +
                    "plane { material { pigment White  diffuse 0.9  specular 0  ambient 0 } }\n" +
                    "group {\n" +
                    "    sphere { material { pigment White  emission 1  diffuse 0  specular 0 }  scale 0.2 }\n" +
                    "    point light { location [0, 0, 0] }\n" +
                    "    translate [0, 0.8, 0]\n" +
                    "}\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, "the scene did not parse");

                renderer.Render(new RenderOptions { OutputFileName = output });

                return new ImageFile(output).Load()[0];
            }

            Canvas without = Render(0), with = Render(1);
            int differing = 0;

            for (int y = 0; y < without.Height; y++)
            {
                for (int x = 0; x < without.Width; x++)
                {
                    if (!without.GetPixel(x, y).Matches(with.GetPixel(x, y)))
                        differing++;
                }
            }

            Assert.AreEqual(0, differing, "asking for bounced light counted the bulb's glow again");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// This tests that a scene can ask for bounced light, and that asking takes the ambient fudge away, as
    /// a sky light does: the fudge stands in for exactly the light now being followed.
    /// </summary>
    [TestMethod]
    public void TestASceneAsksForBouncedLightAndLosesItsAmbient()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"bounce-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            string path = Path.Combine(directory, "scene.igl");
            string output = Path.Combine(directory, "out.png");

            // A ball lit only from behind: what shows of its near side is its ambient alone.
            Canvas Rendered(string context)
            {
                File.WriteAllText(path,
                    $"context {{ width 8  height 8  no gamma  {context} }}\n" +
                    "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
                    "point light { location [0, 0, 10] }\n" +
                    "sphere { material { pigment White } }\n");

                ImageRenderer renderer = new LanguageParser(path).Parse();

                Assert.IsNotNull(renderer, $"the scene with '{context}' did not parse");

                renderer.Render(new RenderOptions { OutputFileName = output });

                return new ImageFile(output).Load()[0];
            }

            Assert.AreEqual(0.1, Rendered("").GetPixel(4, 4).Red, 0.005, "the fudge, unasked");
            Assert.AreEqual(0, Rendered("diffuse bounces 2  diffuse samples 4").GetPixel(4, 4).Red, 1e-9,
                "asking for bounced light should take the fudge away");

            // Even an ambient the material names for itself, as a curated library's do; and emission stays.
            File.WriteAllText(path,
                "context { width 8  height 8  no gamma  diffuse bounces 1  diffuse samples 4 }\n" +
                "camera { location [0, 0, -5]  look at [0, 0, 0] }\n" +
                "point light { location [0, 0, 10] }\n" +
                "sphere { material { pigment White  ambient 0.5 }  translate X -1.2 }\n" +
                "sphere { material { pigment White  ambient 0  emission 0.25 }  translate X 1.2 }\n");

            ImageRenderer both = new LanguageParser(path).Parse();

            both.Render(new RenderOptions { OutputFileName = output });

            Canvas canvas = new ImageFile(output).Load()[0];
            Color named = Enumerable.Range(0, 8).Select(x => canvas.GetPixel(x, 4)).First(pixel => pixel.Alpha > 0.99);
            Color glowing = Enumerable.Range(0, 8).Select(x => canvas.GetPixel(7 - x, 4)).First(pixel => pixel.Alpha > 0.99);

            Assert.AreEqual(0, named.Red, 1e-9, "a named ambient should go when bounced light is asked for");
            Assert.AreEqual(0.25, glowing.Red, 0.005, "emission should stay");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
