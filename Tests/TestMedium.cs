using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Fields;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// These tests cover what a medium does to a ray crossing it.  They are arithmetic rather than
/// pictures, because with the density even throughout there is an exact answer to check against:
/// what survives a crossing is <c>exp(-σd)</c>, and what the medium adds along the way is the
/// integral of its own light dimmed by however much medium still lay ahead of it.
/// </summary>
[TestClass]
public class TestMedium
{
    /// <summary>
    /// Integrates what a medium of the given coefficients gives off over the given distance, the
    /// slow and obvious way, as something to hold the closed form to.
    /// </summary>
    private static double EmissionByBruteForce(
        double absorption, double emission, double density, double distance, int steps = 2_000_000)
    {
        double step = distance / steps;
        double total = 0;

        for (int index = 0; index < steps; index++)
        {
            double at = (index + 0.5) * step;

            total += emission * density * Math.Exp(-absorption * density * at) * step;
        }

        return total;
    }

    [TestMethod]
    public void TestWhatSurvivesACrossingFollowsBeersLaw()
    {
        Medium medium = new () { Absorption = new Color(0.1, 0.2, 0.4) };

        Color through = medium.GetTransmittanceOver(3);

        Assert.AreEqual(Math.Exp(-0.1 * 3), through.Red, 1e-12);
        Assert.AreEqual(Math.Exp(-0.2 * 3), through.Green, 1e-12);
        Assert.AreEqual(Math.Exp(-0.4 * 3), through.Blue, 1e-12);
    }

    [TestMethod]
    public void TestDensityMultipliesTheAbsorption()
    {
        // Twice as much of the stuff absorbs as strongly as twice the distance of it does, which is
        // what having the two as separate numbers is worth: how much there is may be tuned without
        // touching what it does.
        Medium thin = new () { Absorption = new Color(0.3, 0.3, 0.3), Density = 1 };
        Medium thick = new () { Absorption = new Color(0.3, 0.3, 0.3), Density = 2 };

        Assert.AreEqual(
            thin.GetTransmittanceOver(4).Red, thick.GetTransmittanceOver(2).Red, 1e-12);
    }

    [TestMethod]
    public void TestWhatTheMediumAddsMatchesTheIntegral()
    {
        // The closed form against the sum it stands for.  A medium's own light is dimmed by the
        // medium still in front of it, so what a long span adds is nothing like its length times
        // its brightness -- the far end of it barely shows.
        const double absorption = 0.35;
        const double emission = 0.8;
        const double density = 1.4;
        const double distance = 6;

        Medium medium = new ()
        {
            Absorption = new Color(absorption, absorption, absorption),
            Emission = new Color(emission, emission, emission),
            Density = density
        };
        Color seen = medium.ApplyOver(Colors.Black, distance);
        double expected = EmissionByBruteForce(absorption, emission, density, distance);

        Assert.AreEqual(expected, seen.Red, 1e-6, $"the medium added {seen.Red}, not {expected}");
    }

    [TestMethod]
    public void TestWhatIsBehindArrivesDimmed()
    {
        Medium medium = new () { Absorption = new Color(0.5, 0.5, 0.5) };
        Color seen = medium.ApplyOver(new Color(1, 1, 1), 2);

        Assert.AreEqual(Math.Exp(-1), seen.Red, 1e-12);
    }

    [TestMethod]
    public void TestAnEndlessCrossingSettlesAtTheMediumsOwnColor()
    {
        // The one that decides what a ray which strikes nothing comes back with.  Let the span run
        // on forever and what lies beyond cannot matter at all, so what is left is the color the
        // medium's own numbers imply: what it gives off, divided by what it takes out.
        Medium medium = new ()
        {
            Absorption = new Color(0.2, 0.4, 0.5),
            Emission = new Color(0.1, 0.3, 0.5)
        };
        Color endless = medium.ApplyOver(new Color(1, 1, 1), double.PositiveInfinity);

        Assert.AreEqual(0.1 / 0.2, endless.Red, 1e-12);
        Assert.AreEqual(0.3 / 0.4, endless.Green, 1e-12);
        Assert.AreEqual(0.5 / 0.5, endless.Blue, 1e-12);

        // And it does not matter in the least what was behind it.
        Color otherSide = medium.ApplyOver(Colors.Black, double.PositiveInfinity);

        Assert.IsTrue(endless.Matches(otherSide), $"{endless} against {otherSide}");
    }

    [TestMethod]
    public void TestAMediumThatAbsorbsNothingStillAddsItsOwnLightEvenly()
    {
        // The general form for what a medium adds is a nought over a nought when it absorbs nothing,
        // so this is the limit rather than the formula: with nothing taking light out, what is put
        // in simply piles up with the distance.
        Medium medium = new () { Emission = new Color(0.25, 0.25, 0.25) };

        Assert.AreEqual(0.5, medium.ApplyOver(Colors.Black, 2).Red, 1e-12);
        Assert.AreEqual(1.0, medium.ApplyOver(Colors.Black, 4).Red, 1e-12);

        // And it must arrive at that limit smoothly rather than leaping to it, so a scene tuning its
        // absorption toward nothing sees no step change.
        Medium nearly = new ()
        {
            Absorption = new Color(1e-9, 1e-9, 1e-9), Emission = new Color(0.25, 0.25, 0.25)
        };

        Assert.AreEqual(0.5, nearly.ApplyOver(Colors.Black, 2).Red, 1e-8);
    }

    [TestMethod]
    public void TestTheTwoFormsAgreeWhereTheyMeet()
    {
        // What a medium adds is worked out two ways -- the plain form, and a series for when the
        // plain form would be all cancellation -- so the seam between them is worth standing on.
        // A visible step here would show up as a band in any scene whose absorption crossed it.
        Medium medium = new () { Emission = new Color(0.7, 0.7, 0.7) };

        foreach (double crossings in new[] { 1e-4, 1e-4 + 1e-12, 1e-4 - 1e-12 })
        {
            medium.Absorption = new Color(crossings / 2, crossings / 2, crossings / 2);

            double added = medium.ApplyOver(Colors.Black, 2).Red;
            double expected = 0.7 / (crossings / 2) * (1 - Math.Exp(-crossings));

            Assert.AreEqual(expected, added, 1e-10, $"at {crossings} crossings");
        }
    }

    [TestMethod]
    public void TestTheSurroundingsDimALampAsWellAsAView()
    {
        // A fog stands between a lamp and what it lights as surely as it stands between the eye and
        // what it looks at, so the light itself must be charged for its trip.  What the medium gives
        // off has no part in this: a shadow ray asks after the lamp's light and nothing else.
        Scene scene = new ()
        {
            Environment = new SceneEnvironment
            {
                Medium = new Medium
                {
                    Absorption = new Color(0.25, 0.25, 0.25),
                    Emission = new Color(0.5, 0.5, 0.5)
                }
            }
        };
        Color reaching = scene.GetLightReaching(
            Point.Zero, new Vector(0, 0, 1), 4);

        Assert.AreEqual(Math.Exp(-1), reaching.Red, 1e-12, $"the lamp arrived at {reaching.Red}");

        // And with nothing in the way at all, the lamp arrives whole.
        Assert.IsTrue(Colors.White.Matches(
            new Scene().GetLightReaching(Point.Zero, new Vector(0, 0, 1), 4)));
    }

    /// <summary>
    /// Averages a phase function over the whole sphere, which is what says whether it is measured
    /// against an even spread: one that is must average to one, however lopsided it may be.
    /// </summary>
    private static double AverageOverTheSphere(Medium medium, int steps = 20_000)
    {
        double total = 0;

        // Even steps in the cosine are even steps in solid angle, which is what makes this a plain
        // average rather than one that needs weighting.
        for (int index = 0; index < steps; index++)
            total += medium.PhaseFor(-1 + 2 * (index + 0.5) / steps);

        return total / steps;
    }

    [TestMethod]
    public void TestAnEvenSpreadIsEvenAndAveragesToOne()
    {
        Medium medium = new () { Scattering = new Color(1, 1, 1) };

        Assert.AreEqual(1, medium.PhaseFor(1), 1e-12);
        Assert.AreEqual(1, medium.PhaseFor(0), 1e-12);
        Assert.AreEqual(1, medium.PhaseFor(-1), 1e-12);
        Assert.AreEqual(1, AverageOverTheSphere(medium), 1e-6);
    }

    [TestMethod]
    public void TestALopsidedSpreadStillAveragesToOne()
    {
        // Anisotropy moves light from one side to the other and must invent none on the way, or a
        // medium would brighten or dim purely for preferring a direction.
        Medium medium = new () { Scattering = new Color(1, 1, 1) };

        foreach (double anisotropy in new[] { 0.3, 0.7, -0.4, -0.85 })
        {
            medium.Anisotropy = anisotropy;

            Assert.AreEqual(1, AverageOverTheSphere(medium), 1e-3,
                $"an anisotropy of {anisotropy} did not conserve what it spread");
        }

        medium.Anisotropy = 0;
        medium.PhaseFunction = PhaseFunction.Rayleigh;

        Assert.AreEqual(1, AverageOverTheSphere(medium), 1e-6, "Rayleigh's did not either");
    }

    [TestMethod]
    public void TestForwardScatteringFavorsTheWayTheLightWasGoing()
    {
        Medium forward = new () { Scattering = new Color(1, 1, 1), Anisotropy = 0.6 };
        Medium backward = new () { Scattering = new Color(1, 1, 1), Anisotropy = -0.6 };

        Assert.IsTrue(forward.PhaseFor(1) > forward.PhaseFor(0),
            "forward scattering should favor straight on over sideways");
        Assert.IsTrue(forward.PhaseFor(0) > forward.PhaseFor(-1),
            "and sideways over straight back");

        // And the other way about is the mirror of it, exactly.
        Assert.AreEqual(forward.PhaseFor(1), backward.PhaseFor(-1), 1e-12);
        Assert.AreEqual(forward.PhaseFor(-1), backward.PhaseFor(1), 1e-12);

        // Rayleigh's sends as much back as on, and least to the sides.
        Medium rayleigh = new ()
        {
            Scattering = new Color(1, 1, 1), PhaseFunction = PhaseFunction.Rayleigh
        };

        Assert.AreEqual(rayleigh.PhaseFor(1), rayleigh.PhaseFor(-1), 1e-12);
        Assert.IsTrue(rayleigh.PhaseFor(0) < rayleigh.PhaseFor(1));
    }

    [TestMethod]
    public void TestTurningLightAsideAlsoTakesItOutOfTheRay()
    {
        // Light turned aside no longer travels along the ray, so a medium that only scatters must dim
        // what lies beyond it exactly as one that only absorbs does.
        Medium scattering = new () { Scattering = new Color(0.3, 0.3, 0.3) };
        Medium absorbing = new () { Absorption = new Color(0.3, 0.3, 0.3) };

        Assert.AreEqual(
            absorbing.GetTransmittanceOver(4).Red, scattering.GetTransmittanceOver(4).Red, 1e-12);

        // And the two together stop light at the sum of their rates.
        Medium both = new ()
        {
            Absorption = new Color(0.1, 0.1, 0.1), Scattering = new Color(0.2, 0.2, 0.2)
        };

        Assert.AreEqual(Math.Exp(-0.3 * 4), both.GetTransmittanceOver(4).Red, 1e-12);
        Assert.AreEqual(0.3, both.MeanExtinction, 1e-12);
    }

    [TestMethod]
    public void TestScatteringSettlesTheMediumsOwnLightToo()
    {
        // What a medium's own light settles at is decided by everything that stops light coming this
        // way, so a medium that turns its glow aside is as limited by that as by swallowing it -- and
        // having a way to be stopped is what lets it fill the endless surroundings at all.
        Medium medium = new ()
        {
            Scattering = new Color(0.4, 0.4, 0.4), Emission = new Color(0.2, 0.2, 0.2)
        };

        Assert.IsFalse(medium.MustBeBounded);
        Assert.AreEqual(0.5, medium.ApplyOver(Colors.Black, double.PositiveInfinity).Red, 1e-12);
    }

    [TestMethod]
    public void TestTheGatheredLightMatchesTheIntegralItStandsFor()
    {
        // The one term with no answer to be written down, held to the sum it stands for.  A lamp sits
        // in an endless fog and the eye looks straight through the lamp's own place: what the eye gets
        // is everything the fog turns toward it along the way, each scrap dimmed twice over -- once on
        // its way from the lamp to where it turned, and again on its way from there to the eye.
        const double scattering = 0.2;

        Scene scene = new ()
        {
            Background = new SolidPigment(Colors.Black),
            Environment = new SceneEnvironment
            {
                Medium = new Medium
                {
                    Scattering = new Color(scattering, scattering, scattering),
                    Samples = 4_000
                }
            }
        };

        scene.Lights.Add(new PointLight { Location = Point.Zero, Color = Colors.White });

        Color seen = scene.GetColorFor(new Ray(new Point(0, 0, -5), Directions.In), 1);

        // The same thing summed the slow and obvious way.  An even spread hands back one in every
        // direction, so the shape drops out and what is left is the two trips through the fog.
        double expected = 0;
        const double step = 0.0005;

        for (double along = step / 2; along < 400; along += step)
        {
            double toTheLamp = Math.Abs(along - 5);

            expected += Math.Exp(-scattering * along) * scattering *
                        Math.Exp(-scattering * toTheLamp) * step;
        }

        Assert.AreEqual(expected, seen.Red, 5e-4,
            $"the gathered light came to {seen.Red}, where the sum says {expected}");
    }

    [TestMethod]
    public void TestMoreSamplesFindTheSameAnswer()
    {
        // The estimate leans on none of its own arrangements: asking in sixteen places and asking in
        // five hundred must find the same light, or the count would be a knob that changes the picture
        // rather than one that settles it.
        Color few = GatheredWith(16);
        Color many = GatheredWith(500);

        Assert.AreEqual(many.Red, few.Red, many.Red * 0.1,
            $"sixteen places gave {few.Red} where five hundred gave {many.Red}");

        // And asking twice gives the same answer twice, however the work was divided up.
        Assert.IsTrue(GatheredWith(16).Matches(few));
    }

    /// <summary>
    /// Looks through a lit fog with the given number of sampling places, and hands back what the eye
    /// gets.
    /// </summary>
    private static Color GatheredWith(int samples)
    {
        Scene scene = new ()
        {
            Background = new SolidPigment(Colors.Black),
            Environment = new SceneEnvironment
            {
                Medium = new Medium
                {
                    Scattering = new Color(0.25, 0.25, 0.25),
                    Anisotropy = 0.4,
                    Samples = samples
                }
            }
        };

        scene.Lights.Add(new PointLight { Location = new Point(2, 1, 0), Color = Colors.White });

        return scene.GetColorFor(new Ray(new Point(0, 0, -5), Directions.In), 1);
    }

    [TestMethod]
    public void TestAFogWithNoLampGathersNothing()
    {
        // Scattering only ever hands on light that came from somewhere, so with nothing shining there
        // is nothing to hand on -- and the medium is then exactly as cheap as one that cannot scatter.
        Scene scene = new ()
        {
            Background = new SolidPigment(Colors.Black),
            Environment = new SceneEnvironment
            {
                Medium = new Medium { Scattering = new Color(0.5, 0.5, 0.5) }
            }
        };

        Assert.IsTrue(Colors.Black.Matches(
            scene.GetColorFor(new Ray(new Point(0, 0, -5), Directions.In), 1)));
    }

    [TestMethod]
    public void TestAShapeSaysHowMuchOfTheStuffIsWhere()
    {
        Medium even = new () { Absorption = new Color(1, 1, 1) };

        Assert.IsFalse(even.HasShape);
        Assert.AreEqual(1, even.DensityAt(Point.Zero));

        // The plain density scales whatever the shape says, so one number thins the whole of it.
        Medium shaped = new ()
        {
            Absorption = new Color(1, 1, 1),
            Density = 0.5,
            DensityField = FieldFunction.Compile(new FieldConstant(3))
        };

        Assert.IsTrue(shaped.HasShape);
        Assert.AreEqual(1.5, shaped.DensityAt(Point.Zero), 1e-12);

        // A shape that would go negative is empty there rather than negative: a density below nothing
        // would have a ray gaining light for crossing the stuff.
        Medium negative = new ()
        {
            Absorption = new Color(1, 1, 1),
            DensityField = FieldFunction.Compile(new FieldConstant(-2))
        };

        Assert.AreEqual(0, negative.DensityAt(Point.Zero));
    }

    [TestMethod]
    public void TestAPatternMayShapeTheDensityToo()
    {
        // The same job as a function, said the other way.  A checker is the plainest thing to hold it
        // to, since where it is on and where it is off can be worked out by hand.
        Medium blocks = new ()
        {
            Absorption = new Color(1, 1, 1),
            Density = 0.5,
            DensityPattern = new DensityShape { Pattern = new CheckerPattern() }
        };

        Assert.IsTrue(blocks.HasShape, "a pattern is a shape, so the crossing must be marched");
        Assert.AreEqual(0, blocks.DensityAt(Point.Zero), "the checker is off at the origin");
        Assert.AreEqual(0.5, blocks.DensityAt(new Point(1.5, 0.5, 0.5)), 1e-12,
            "and on in the next block along, with the medium's own density scaling it");
    }

    [TestMethod]
    public void TestTheTransformPlacesThePattern()
    {
        // Without this the feature would be nearly useless: a pattern sits at the scale of the space
        // it is written in, and the things media fill are a couple of units across, so most of the
        // library would give one block and no more.  The same point, the same pattern, a different
        // footing, and so a different answer.
        Point point = new (1.5, 0.5, 0.5);
        Medium asWritten = new ()
        {
            Absorption = new Color(1, 1, 1),
            DensityPattern = new DensityShape { Pattern = new CheckerPattern() }
        };
        Medium spreadWider = new ()
        {
            Absorption = new Color(1, 1, 1),
            DensityPattern = new DensityShape
            {
                Pattern = new CheckerPattern(), Transform = Transforms.Scale(2)
            }
        };

        Assert.AreEqual(1, asWritten.DensityAt(point));
        Assert.AreEqual(0, spreadWider.DensityAt(point),
            "spread to twice its size, that point falls in the block before");
    }

    [TestMethod]
    public void TestAPatternBuiltForAColorMapIsSpreadBackAcrossTheRange()
    {
        // A pattern built to choose between six pigments hands back a whole number naming which one,
        // not a fraction, so read straight it would quietly mean six times the density.  Every pattern
        // in the library has to land inside the same range to be usable here.
        DensityShape sixWays = new () { Pattern = new TriangularPattern() };
        List<double> seen = [];

        for (int x = 0; x < 12; x++)
        {
            for (int z = 0; z < 12; z++)
            {
                double value = sixWays.ValueAt(new Point(x * 0.31, 0, z * 0.29));

                Assert.IsTrue(value is >= 0 and <= 1,
                    $"a six-way pattern gave {value}, which is outside the range");

                seen.Add(value);
            }
        }

        Assert.IsTrue(seen.Distinct().Count() > 2,
            "the bands should still be told apart after being spread");

        // A two-way pattern is spread by one, so its "on" stays all the way on rather than being
        // halved -- the spreading must not cost a checker its contrast.
        DensityShape twoWays = new () { Pattern = new CheckerPattern() };

        Assert.AreEqual(1, twoWays.ValueAt(new Point(1.5, 0.5, 0.5)));
    }

    [TestMethod]
    public void TestWalkingAnEvenShapeFindsWhatTheExactAnswerSays()
    {
        // The one test that holds the walk to something known.  A shape that is the same everywhere is
        // still walked -- the renderer cannot know the function is constant -- so it must arrive at
        // what the closed form gives for that same even density.  If the walk is wrong in its
        // bookkeeping, this is where it shows, because the right answer is available in one line.
        Color exact = LookedThroughABallOf(null);
        Color walked = LookedThroughABallOf(FieldFunction.Compile(FieldConstant.One));

        Assert.AreEqual(exact.Red, walked.Red, exact.Red * 0.02,
            $"the walk gave {walked.Red} where the exact answer is {exact.Red}");
        Assert.AreEqual(exact.Green, walked.Green, exact.Green * 0.02);
        Assert.AreEqual(exact.Blue, walked.Blue, exact.Blue * 0.02);
    }

    [TestMethod]
    public void TestFinerStepsFindTheAnswerMoreNearly()
    {
        // The walk is first order in its step, so it approaches the exact answer as the steps get
        // finer rather than being right at any particular count.  Its error must therefore fall.
        Color exact = LookedThroughABallOf(null);
        double coarse = Math.Abs(LookedThroughABallOf(
            FieldFunction.Compile(FieldConstant.One), 8).Red - exact.Red);
        double fine = Math.Abs(LookedThroughABallOf(
            FieldFunction.Compile(FieldConstant.One), 256).Red - exact.Red);

        Assert.IsTrue(fine < coarse,
            $"finer steps were no better: {fine} against {coarse}");
    }

    /// <summary>
    /// Looks through a glass ball filled with a lit, glowing medium of the given shape, and hands back
    /// what the eye gets.  A shape of <c>null</c> is an even density, which is answered exactly rather
    /// than walked.
    /// </summary>
    private static Color LookedThroughABallOf(FieldFunction shape, int samples = 64)
    {
        Sphere ball = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White),
                Ambient = 0,
                Diffuse = 0,
                Specular = 0,
                Transparency = 1,
                Interior = new Interior
                {
                    Medium = new Medium
                    {
                        Absorption = new Color(0.3, 0.4, 0.5),
                        Emission = new Color(0.2, 0.15, 0.1),
                        Scattering = new Color(0.4, 0.4, 0.4),
                        DensityField = shape,
                        Samples = samples
                    }
                }
            }
        };
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };

        ball.PrepareForRendering();
        scene.Surfaces.Add(ball);
        scene.Lights.Add(new PointLight { Location = new Point(3, 3, -3), Color = Colors.White });

        return scene.GetColorFor(new Ray(new Point(0, 0, -5), Directions.In), 5);
    }

    [TestMethod]
    public void TestNoBouncesIsExactlyWhatItAlwaysWas()
    {
        // The anchor for the whole of this.  Following no further turns must leave the arithmetic
        // untouched rather than nearly untouched: it is what lets every picture drawn before multiple
        // scattering existed stand unchanged, and what makes the added light plainly the added light.
        Color without = InsideALitCloud(0);

        Assert.IsTrue(without.Matches(InsideALitCloud(0)), "and it must not wander between renders");

        Color with = InsideALitCloud(3);

        Assert.IsFalse(with.Matches(without), "three turns should plainly add light");
    }

    [TestMethod]
    public void TestFollowingThePathFurtherFindsMoreLight()
    {
        // Each turn can only add: light that was turned twice is light that was previously thrown away
        // rather than light taken from somewhere else.  And each turn adds less than the one before,
        // by the share the medium swallows, so the total settles rather than running away.
        double[] found =
        [
            InsideALitCloud(0).Red, InsideALitCloud(1).Red, InsideALitCloud(2).Red,
            InsideALitCloud(4).Red, InsideALitCloud(8).Red
        ];

        for (int index = 1; index < found.Length; index++)
        {
            Assert.IsTrue(found[index] > found[index - 1],
                $"{found[index]} after more turns, against {found[index - 1]} before");
        }

        // What the last four turns added, against what the first one did.  A medium that swallows some
        // of what it stops cannot keep adding at the same rate, so the tail must be the smaller.
        double first = found[1] - found[0];
        double tail = found[4] - found[3];

        Assert.IsTrue(tail < first,
            $"the tail of it added {tail}, where the first turn added {first}");
    }

    [TestMethod]
    public void TestWhatEachTurnIsWorthIsWhatTheMediumPassesOn()
    {
        // The share of stopped light that carried on rather than being swallowed.  It falls out of the
        // coefficients alone: how much stuff is there scales what is stopped and what is passed on
        // alike, so it cancels, and a place in a thin part of a cloud passes on the same share as a
        // place in a thick part.
        Medium medium = new ()
        {
            Absorption = new Color(0.25, 0, 1), Scattering = new Color(0.75, 1, 1)
        };

        Assert.AreEqual(0.75, medium.Albedo.Red, 1e-12);
        Assert.AreEqual(1, medium.Albedo.Green, 1e-12, "nothing absorbed means nothing lost");
        Assert.AreEqual(0.5, medium.Albedo.Blue, 1e-12);

        // A medium that stops nothing at all passes nothing on, rather than dividing by nothing.
        Assert.AreEqual(0, new Medium().Albedo.Red);
    }

    [TestMethod]
    public void TestThePickedDirectionsFollowTheShape()
    {
        // The directions a path is followed back along are picked in proportion to the shape, which is
        // what lets each one be counted at its face value.  Drawn many times, then, they must pile up
        // the way the shape says: mostly forward for a medium that carries light on, mostly backward
        // for one that sends it back.
        Vector heading = new (0, 0, 1);
        Medium forward = new () { Scattering = new Color(1, 1, 1), Anisotropy = 0.7 };
        Medium backward = new () { Scattering = new Color(1, 1, 1), Anisotropy = -0.7 };
        Medium evenly = new () { Scattering = new Color(1, 1, 1) };

        Assert.AreEqual(0.7, AverageCosineOf(forward, heading), 0.02);
        Assert.AreEqual(-0.7, AverageCosineOf(backward, heading), 0.02);
        Assert.AreEqual(0, AverageCosineOf(evenly, heading), 0.02);

        // Rayleigh's is even-handed between forward and back, however lopsided it is toward both.
        Assert.AreEqual(0, AverageCosineOf(
            new Medium { Scattering = new Color(1, 1, 1), PhaseFunction = PhaseFunction.Rayleigh },
            heading), 0.02);
    }

    /// <summary>
    /// Averages the cosine of the angle turned through over many picked directions, which for a shape
    /// picked in proportion to itself is the anisotropy it was built with.
    /// </summary>
    private static double AverageCosineOf(Medium medium, Vector heading, int draws = 20_000)
    {
        double total = 0;

        for (int index = 0; index < draws; index++)
        {
            // Evenly spread draws rather than random ones, since what is being checked is the shape
            // rather than any particular run of numbers.
            Vector picked = medium.SampleDirectionAround(
                heading, (index + 0.5) / draws, index * 0.618033988749895 % 1);

            total += picked.Dot(heading);
        }

        return total / draws;
    }

    /// <summary>
    /// Looks into a lit ball of scattering stuff, following the given number of further turns, and
    /// hands back what the eye gets.
    /// </summary>
    private static Color InsideALitCloud(int bounces)
    {
        Sphere ball = new ()
        {
            Material = new Material
            {
                Pigment = new SolidPigment(Colors.White),
                Ambient = 0,
                Diffuse = 0,
                Specular = 0,
                Transparency = 1,
                Interior = new Interior
                {
                    Medium = new Medium
                    {
                        Scattering = new Color(2.5, 2.5, 2.5),
                        Absorption = new Color(0.1, 0.1, 0.1),
                        Samples = 24,
                        Bounces = bounces
                    }
                }
            }
        };
        Scene scene = new () { Background = new SolidPigment(Colors.Black) };

        ball.PrepareForRendering();
        scene.Surfaces.Add(ball);
        scene.Lights.Add(new PointLight { Location = new Point(-4, 2, -3), Color = Colors.White });

        return scene.GetColorFor(new Ray(new Point(0, 0, -5), Directions.In), 5);
    }

    [TestMethod]
    public void TestAMediumWithNothingToSayChangesNothing()
    {
        Color color = new (0.3, 0.6, 0.9, 0.5);

        Assert.IsTrue(color.Matches(new Medium().ApplyOver(color, 10)));
        Assert.IsTrue(color.Matches(
            new Medium { Absorption = new Color(1, 1, 1), Density = 0 }.ApplyOver(color, 10)));

        // Nor does any medium have anything to say about a crossing of no length at all.
        Assert.IsTrue(color.Matches(
            new Medium { Absorption = new Color(1, 1, 1) }.ApplyOver(color, 0)));
    }

    [TestMethod]
    public void TestAMediumCoversWhatItHides()
    {
        // A fog is a thing that is there, so it stands in front of whatever is behind it -- which
        // for an image rendered over nothing at all means the pixels it fills come out opaque
        // rather than empty.
        Medium medium = new () { Absorption = new Color(0.5, 0.5, 0.5) };
        Color overNothing = medium.ApplyOver(Colors.Transparent, 20);

        Assert.IsTrue(overNothing.Alpha > 0.99, $"the fog covered only {overNothing.Alpha}");

        // A thin one covers only a little.
        Color barely = medium.ApplyOver(Colors.Transparent, 0.01);

        Assert.IsTrue(barely.Alpha < 0.01, $"a whisper of fog covered {barely.Alpha}");
    }

    [TestMethod]
    public void TestAMediumKnowsWhetherItNeedsAFarSide()
    {
        // Light given off where none is absorbed has nothing to settle it, so over an endless span
        // such a medium is infinitely bright.  It is only a fair description of something bounded.
        Assert.IsFalse(new Medium().MustBeBounded);
        Assert.IsFalse(new Medium
        {
            Absorption = new Color(0.1, 0.1, 0.1), Emission = new Color(0.1, 0.1, 0.1)
        }.MustBeBounded);
        Assert.IsTrue(new Medium { Emission = new Color(0.1, 0.1, 0.1) }.MustBeBounded);

        // One color is enough to want a far side, since the others cannot make up for it.
        Assert.IsTrue(new Medium
        {
            Absorption = new Color(0.1, 0.1, 0), Emission = new Color(0.1, 0.1, 0.1)
        }.MustBeBounded);
    }
}
