using System.Runtime.CompilerServices;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Fields;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace Tests;

/// <summary>
/// The shading carries light in a type of its own rather than in colors, so that it can be carried in
/// more bands than three.  Two things are held here: that three bands are exactly the colors they
/// replaced, operator for operator and bit for bit, and that nothing in the shading quietly counts on
/// there being three.
/// </summary>
[TestClass]
public class TestRgbSpectrum
{
    /// <summary>
    /// Every operator does what a color's does, to the last bit, since the gallery rendering unchanged
    /// rests on it.
    /// </summary>
    [TestMethod]
    public void TestThreeBandsAreColorArithmeticToTheBit()
    {
        Random random = new (17);

        for (int trial = 0; trial < 1000; trial++)
        {
            Color left = RandomColor(random);
            Color right = RandomColor(random);
            double number = random.NextDouble() * 4 - 2;
            RgbSpectrum one = RgbSpectrum.From(left);
            RgbSpectrum other = RgbSpectrum.From(right);

            AssertSameBits(left + right, one + other);
            AssertSameBits(left - right, one - other);
            AssertSameBits(left * right, one * other);
            AssertSameBits(left * number, one * number);
            AssertSameBits(number * left, number * one);
            AssertSameBits(left / number, one / number);
            AssertSameBits(
                Colors.White + (left - Colors.White) * number,
                RgbSpectrum.White + (one - RgbSpectrum.White) * number);
        }
    }

    /// <summary>
    /// The fourth number follows a color's rule: a color turned into light keeps it, every operator
    /// leaves it at one, and only asking for it changes it.
    /// </summary>
    [TestMethod]
    public void TestCoverageFollowsTheColorRule()
    {
        Color half = new (0.2, 0.4, 0.6, 0.5);
        RgbSpectrum light = RgbSpectrum.From(half);

        AssertSameBits(half, light);
        AssertSameBits(half, RgbSpectrum.FromReflectance(half));
        AssertSameBits(half, RgbSpectrum.FromIlluminant(half));
        AssertSameBits(half, RgbSpectrum.FromUnbounded(half));
        Assert.AreEqual(1, (light * 1).Alpha);
        Assert.AreEqual(1, (light + RgbSpectrum.Black).Alpha);
        Assert.AreEqual(0.25, light.WithAlpha(0.25).Alpha);
        Assert.AreEqual(0.6, light.WithAlpha(0.25).Blue);
        Assert.AreEqual(1, RgbSpectrum.Black.Alpha);
        Assert.AreEqual(1, RgbSpectrum.White.Alpha);
    }

    /// <summary>
    /// Being no light at all is what matching black always meant, tolerance and coverage both.
    /// </summary>
    [TestMethod]
    public void TestBeingBlackIsMatchingBlack()
    {
        Color[] colors =
        [
            Colors.Black, Colors.White, Colors.Transparent, new (0, 0, 0, 0.5),
            new (DoubleExtensions.Epsilon / 2, 0, 0), new (DoubleExtensions.Epsilon * 2, 0, 0),
            new (0, -DoubleExtensions.Epsilon / 2, 0), new (0, 0, -DoubleExtensions.Epsilon * 2)
        ];

        foreach (Color color in colors)
        {
            Assert.AreEqual(
                color.Matches(Colors.Black), RgbSpectrum.From(color).IsBlack, color.ToString());
        }
    }

    /// <summary>
    /// A band can be read and written by number, red, green and blue in that order, and the bands add
    /// up and average as the three channels always did.
    /// </summary>
    [TestMethod]
    public void TestBandsByNumber()
    {
        RgbSpectrum light = new (0.1, 0.2, 0.3);

        Assert.AreEqual(3, RgbSpectrum.Count);
        Assert.AreEqual(0.1, light[0]);
        Assert.AreEqual(0.2, light[1]);
        Assert.AreEqual(0.3, light[2]);
        Assert.AreEqual(0.1 + 0.2 + 0.3, light.Sum);
        Assert.AreEqual((0.1 + 0.2 + 0.3) / 3, light.Average);

        light[1] = 0.7;

        Assert.AreEqual(0.7, light.Green);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => light[3]);
    }

    /// <summary>
    /// The real test of the shading being written over any kind of light: carry it in six bands, each
    /// channel held twice over, and every path a ray can take -- lamps of every sort, shadows through
    /// glass, a metal, a fog round everything and a glowing cloud in the middle of it -- must come out
    /// as it does in three.  Anything that took the bands to be red, green and blue, or to be three of
    /// them, would come out differently.
    /// </summary>
    [TestMethod]
    public void TestTheShadingDoesNotCountOnThreeBands()
    {
        Scene scene = EverythingScene(out List<Surface> subjects);
        HashSet<Surface> seen = [];
        Point eye = new (0, 1.5, -7);

        for (int row = 0; row < 12; row++)
        {
            for (int column = 0; column < 16; column++)
            {
                Point target = new (-4 + column * 8.0 / 15, 2.5 - row * 4.5 / 11, 0);
                Ray ray = new (eye, (target - eye).Unit);
                Color three = scene.GetColorFor<RgbSpectrum>(ray, 5).ToColor();
                Color six = scene.GetColorFor<DoubledBands>(ray, 5).ToColor();

                Assert.AreEqual(three.Red, six.Red, 1e-9, $"red at {column}, {row}");
                Assert.AreEqual(three.Green, six.Green, 1e-9, $"green at {column}, {row}");
                Assert.AreEqual(three.Blue, six.Blue, 1e-9, $"blue at {column}, {row}");
                Assert.AreEqual(three.Alpha, six.Alpha, 1e-9, $"coverage at {column}, {row}");

                if (scene.Intersect(ray).Hit() is { } hit)
                    seen.Add(hit.Surface);
            }
        }

        // Without this, a scene the rays missed would pass for nothing.
        foreach (Surface subject in subjects)
            Assert.IsTrue(seen.Contains(subject), $"no ray met the {subject.Name}");
    }

    /// <summary>
    /// This method builds a scene with something in it for every path through the shading.
    /// </summary>
    private static Scene EverythingScene(out List<Surface> subjects)
    {
        PigmentSet checks = new ();

        checks.AddEntry(new SolidPigment(new Color(0.85, 0.30, 0.25)));
        checks.AddEntry(new SolidPigment(new Color(0.20, 0.35, 0.70)));

        Surface floor = new Plane
        {
            Name = "floor",
            Transform = Transforms.Translate(0, -1, 0),
            Material = new Material
            {
                Pigment = new PatternPigment { Pattern = new CheckerPattern(), PigmentSet = checks },
                Reflective = 0.1
            }
        };
        Surface matte = Ball("matte ball", -3, new Material
        {
            Pigment = new SolidPigment(new Color(0.9, 0.5, 0.2)), Specular = 0.6, Shininess = 40
        });
        Surface metal = Ball("metal ball", -1, new Material
        {
            Pigment = new SolidPigment(new Color(0.95, 0.75, 0.3)), Reflective = 0.6, Metallic = 1
        });
        Surface glass = Ball("glass ball", 1, new Material
        {
            Pigment = new SolidPigment(new Color(0.3, 0.8, 0.4)), Transparency = 0.9, Reflective = 0.1,
            Interior = new Interior { IndexOfRefraction = 1.5, Filter = 0.6 }
        });
        Surface cloud = Ball("cloud", 3, new Material
        {
            Pigment = new SolidPigment(Colors.White), Ambient = 0, Diffuse = 0, Specular = 0,
            Transparency = 1,
            Interior = new Interior
            {
                Medium = new Medium
                {
                    Absorption = new Color(0.2, 0.3, 0.1),
                    Scattering = new Color(1.5, 1.2, 0.9),
                    Emission = new Color(0.1, 0.05, 0.2),
                    DensityField = FieldFunction.Compile(new FieldConstant(0.8)),
                    Samples = 6,
                    Bounces = 2
                }
            }
        });
        Scene scene = new ()
        {
            Background = new SolidPigment(new Color(0.2, 0.3, 0.5, 0.5)),
            Environment = new SceneEnvironment
            {
                Medium = new Medium
                {
                    Absorption = new Color(0.02, 0.03, 0.04),
                    Scattering = new Color(0.03, 0.02, 0.01),
                    Emission = new Color(0.01, 0, 0.02),
                    Samples = 3
                }
            }
        };

        subjects = [floor, matte, metal, glass, cloud];

        foreach (Surface surface in subjects)
        {
            surface.PrepareForRendering();
            scene.Surfaces.Add(surface);
        }

        scene.Lights.Add(new PointLight
        {
            Location = new Point(-5, 6, -5), Color = new Color(1, 0.9, 0.7), FadeDistance = 4
        });
        scene.Lights.Add(new AreaLight
        {
            Location = new Point(4, 5, -3), Color = new Color(0.5, 0.6, 0.8), USteps = 2, VSteps = 2
        });
        scene.Lights.Add(new Spotlight
        {
            Location = new Point(0, 6, -2), PointAt = new Point(1, 0, 0), Radius = 15, Falloff = 25,
            Color = new Color(0.6, 0.6, 0.5)
        });
        scene.Lights.Add(new SkyLight
        {
            Pigment = new SolidPigment(new Color(0.4, 0.5, 0.7)), Samples = 4
        });

        return scene;
    }

    /// <summary>
    /// This method makes a unit ball resting on the floor at the given distance across.
    /// </summary>
    private static Sphere Ball(string name, double across, Material material)
    {
        return new Sphere
        {
            Name = name, Transform = Transforms.Translate(across, 0, 0), Material = material
        };
    }

    /// <summary>
    /// This method makes a color of channels that may be negative or well over one, and a coverage
    /// anywhere from none to all, since the arithmetic must agree everywhere and not just over the
    /// colors a scene usually holds.
    /// </summary>
    private static Color RandomColor(Random random)
    {
        return new Color(
            random.NextDouble() * 6 - 3, random.NextDouble() * 6 - 3, random.NextDouble() * 6 - 3,
            random.NextDouble());
    }

    /// <summary>
    /// This method asserts that light in three bands holds exactly the numbers a color does.
    /// </summary>
    private static void AssertSameBits(Color expected, RgbSpectrum actual)
    {
        Assert.AreEqual(Bits(expected.Red), Bits(actual.Red), "red");
        Assert.AreEqual(Bits(expected.Green), Bits(actual.Green), "green");
        Assert.AreEqual(Bits(expected.Blue), Bits(actual.Blue), "blue");
        Assert.AreEqual(Bits(expected.Alpha), Bits(actual.Alpha), "coverage");
    }

    /// <summary>
    /// This method returns the bits of a number, so that two numbers are compared exactly.
    /// </summary>
    private static long Bits(double value)
    {
        return BitConverter.DoubleToInt64Bits(value);
    }

    /// <summary>
    /// Six bands holding each of red, green and blue twice over: the same light as three, carried in
    /// a way that only shading written over any kind of light can carry without noticing.
    /// </summary>
    private struct DoubledBands : ISpectrum<DoubledBands>
    {
        private SixBands _bands;

        public double Alpha { get; private set; }

        public static int Count => 6;
        public static bool IsSpectral => false;
        public static DoubledBands Black => Of(0, 0, 0, 1);
        public static DoubledBands White => Of(1, 1, 1, 1);
        public static DoubledBands FromReflectance(Color color) => Of(color);
        public static DoubledBands FromIlluminant(Color color) => Of(color);
        public static DoubledBands FromUnbounded(Color color) => Of(color);
        public static DoubledBands FromSampled(ReadOnlySpan<double> perBand) =>
            Of(SpectralColor.ToColor(perBand));

        public double this[int band]
        {
            readonly get => _bands[band];
            set => _bands[band] = value;
        }

        public readonly DoubledBands WithAlpha(double alpha)
        {
            DoubledBands copy = this;

            copy.Alpha = alpha;

            return copy;
        }

        public readonly bool IsBlack
        {
            get
            {
                for (int band = 0; band < Count; band++)
                {
                    if (!_bands[band].Near(0))
                        return false;
                }

                return Alpha.Near(1);
            }
        }

        public readonly double Sum
        {
            get
            {
                double sum = 0;

                for (int band = 0; band < Count; band++)
                    sum += _bands[band];

                return sum;
            }
        }

        public readonly double Average => Sum / Count;

        public readonly Color ToColor() => new (
            (_bands[0] + _bands[1]) / 2, (_bands[2] + _bands[3]) / 2, (_bands[4] + _bands[5]) / 2,
            Alpha);

        public static DoubledBands operator +(DoubledBands left, DoubledBands right) =>
            Each(left, right, (one, other) => one + other);

        public static DoubledBands operator -(DoubledBands left, DoubledBands right) =>
            Each(left, right, (one, other) => one - other);

        public static DoubledBands operator *(DoubledBands left, DoubledBands right) =>
            Each(left, right, (one, other) => one * other);

        public static DoubledBands operator *(DoubledBands left, double right) =>
            Each(left, left, (one, _) => one * right);

        public static DoubledBands operator *(double left, DoubledBands right) =>
            Each(right, right, (one, _) => left * one);

        public static DoubledBands operator /(DoubledBands left, double right) =>
            Each(left, left, (one, _) => one / right);

        private static DoubledBands Of(Color color) =>
            Of(color.Red, color.Green, color.Blue, color.Alpha);

        private static DoubledBands Of(double red, double green, double blue, double alpha)
        {
            DoubledBands light = new () { Alpha = alpha };

            light._bands[0] = light._bands[1] = red;
            light._bands[2] = light._bands[3] = green;
            light._bands[4] = light._bands[5] = blue;

            return light;
        }

        private static DoubledBands Each(
            DoubledBands left, DoubledBands right, Func<double, double, double> operation)
        {
            DoubledBands result = new () { Alpha = 1 };

            for (int band = 0; band < Count; band++)
                result._bands[band] = operation(left._bands[band], right._bands[band]);

            return result;
        }
    }

    [InlineArray(6)]
    private struct SixBands
    {
        private double _band;
    }
}
