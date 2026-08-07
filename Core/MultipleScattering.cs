using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds the light that has been turned aside more than once before reaching the eye.
/// <para>
/// Following only the first turn leaves a sky about a third as bright as the real one, which is not a
/// rounding but the largest single error in the whole model.  It is easy to see why once said aloud:
/// a beam turned aside by the air has not stopped -- it is still light, still travelling, and the air
/// it meets next will turn some of it again.  Nothing about the first turn is special.  What is
/// followed here is all the turns after the first, added together.
/// </para>
/// <para>
/// Two things make that tractable rather than ruinous.  The first is that after a turn or two the
/// light has forgotten which way it came from, so it may be treated as arriving evenly from every
/// direction -- which drops the whole business from a question about directions to a question about
/// two numbers, how high you are and how high the sun is.  The second is that the orders form a
/// geometric series: if a place gets back some fraction of what it sends out, then the third turn is
/// that fraction of the second, the fourth that fraction of the third, and the sum of all of them
/// forever is <c>1/(1-f)</c>.  So every remaining order is had for the price of the second.
/// </para>
/// <para>
/// What it costs in honesty: the evenness is an approximation, and it is worst for the second turn,
/// which really does still remember the sun.  It tells most near the horizon and near the sun itself.
/// It is a far smaller error than leaving the light out altogether.
/// </para>
/// </summary>
public class MultipleScattering
{
    private readonly double[][] _entries;
    private readonly int _heights;
    private readonly int _sunAngles;
    private readonly double _tallest;

    /// <summary>
    /// This constructor works out, for every height and every height of sun, how much light arrives
    /// having been turned aside more than once.
    /// </summary>
    /// <param name="turbidity">How hazy the air is.</param>
    /// <param name="heights">How many heights to work out.</param>
    /// <param name="sunAngles">How many heights of sun to work out.</param>
    /// <param name="directions">How many directions to gather from at each.</param>
    public MultipleScattering(
        double turbidity, int heights = 32, int sunAngles = 32, int directions = 32)
    {
        _heights = heights;
        _sunAngles = sunAngles;
        _tallest = Atmosphere.TopRadius - Atmosphere.GroundRadius;
        _entries = new double[heights * sunAngles][];

        Parallel.For(0, heights * sunAngles, entry =>
        {
            int height = entry / sunAngles;
            int sunAngle = entry % sunAngles;

            _entries[entry] = WorkOutOne(
                turbidity, HeightAt(height), SunCosineAt(sunAngle), directions);
        });
    }

    /// <summary>
    /// This method returns how much light arrives at a place having been turned aside more than once.
    /// </summary>
    /// <param name="height">How high the place is, in metres.</param>
    /// <param name="sunCosine">The cosine of the sun's angle from straight up there.</param>
    /// <returns>How much arrives, band by band.</returns>
    public double[] At(double height, double sunCosine)
    {
        double alongHeight = Math.Clamp(
            Math.Sqrt(Math.Clamp(height, 0, _tallest) / _tallest), 0, 1) * (_heights - 1);
        double alongSun = Math.Clamp((sunCosine + 1) / 2, 0, 1) * (_sunAngles - 1);
        int lowHeight = Math.Clamp((int) alongHeight, 0, _heights - 1);
        int highHeight = Math.Min(lowHeight + 1, _heights - 1);
        int lowSun = Math.Clamp((int) alongSun, 0, _sunAngles - 1);
        int highSun = Math.Min(lowSun + 1, _sunAngles - 1);
        double downHeight = alongHeight - lowHeight;
        double acrossSun = alongSun - lowSun;
        double[] found = new double[SpectralColor.Bands];

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            double top = Mix(
                _entries[lowHeight * _sunAngles + lowSun][band],
                _entries[lowHeight * _sunAngles + highSun][band], acrossSun);
            double bottom = Mix(
                _entries[highHeight * _sunAngles + lowSun][band],
                _entries[highHeight * _sunAngles + highSun][band], acrossSun);

            found[band] = Mix(top, bottom, downHeight);
        }

        return found;
    }

    /// <summary>
    /// This method works out one entry of the table.
    /// </summary>
    /// <param name="turbidity">How hazy the air is.</param>
    /// <param name="height">How high the place is, in metres.</param>
    /// <param name="sunCosine">The cosine of the sun's angle from straight up.</param>
    /// <param name="directions">How many directions to gather from.</param>
    /// <returns>How much arrives having been turned more than once, band by band.</returns>
    private static double[] WorkOutOne(
        double turbidity, double height, double sunCosine, int directions)
    {
        // Coarser than the sky itself is worked out at, and rightly so: this is a smooth correction
        // spread over the whole sky rather than anything with an edge in it, and it is being asked for
        // at every place in a table that is itself being asked for at every place.
        Atmosphere air = new ()
        {
            Turbidity = turbidity, Steps = 24, StepsTowardSun = 8
        };
        Vector towardSun = new Vector(
            Math.Sqrt(Math.Max(0, 1 - sunCosine * sunCosine)), sunCosine, 0).Unit;
        double[] secondTurn = new double[SpectralColor.Bands];
        double[] comesBack = new double[SpectralColor.Bands];

        for (int index = 0; index < directions; index++)
        {
            Vector view = EvenlyOverTheSphere(index, directions);
            double[] arriving = air.RadianceToward(view, towardSun, height);
            double[] returned = air.TurnedBackAlong(view, height);

            for (int band = 0; band < SpectralColor.Bands; band++)
            {
                secondTurn[band] += arriving[band];
                comesBack[band] += returned[band];
            }
        }

        double[] total = new double[SpectralColor.Bands];

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            double second = secondTurn[band] / directions;
            double fraction = comesBack[band] / directions;

            // Every turn after the second is that same fraction of the one before, so all of them
            // together come to the second divided by one less the fraction.  The fraction is well
            // under one for any real air; the guard is against a sum running away if it ever were not.
            total[band] = second / (1 - Math.Min(fraction, 0.95));
        }

        return total;
    }

    /// <summary>
    /// This method picks one of a set of directions spread evenly over the whole sphere.
    /// </summary>
    /// <param name="index">Which direction is wanted.</param>
    /// <param name="count">How many there are in all.</param>
    /// <returns>The direction picked.</returns>
    private static Vector EvenlyOverTheSphere(int index, int count)
    {
        // Even steps in the cosine are even steps in solid angle, and the golden ratio spreads the way
        // round so that no two land in line.
        double cosine = 1 - 2 * (index + 0.5) / count;
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * (index * 0.7548776662466927 % 1);

        return new Vector(sine * Math.Cos(around), cosine, sine * Math.Sin(around)).Unit;
    }

    /// <summary>
    /// This method returns the height a given row of the table stands for.  They are spread by the
    /// square of the row so that most of them sit low down, where nearly all of the air is.
    /// </summary>
    /// <param name="row">The row in question.</param>
    /// <returns>The height there, in metres.</returns>
    private double HeightAt(int row)
    {
        double along = _heights == 1 ? 0 : (double) row / (_heights - 1);

        return along * along * _tallest;
    }

    /// <summary>
    /// This method returns the height of sun a given column of the table stands for.
    /// </summary>
    /// <param name="column">The column in question.</param>
    /// <returns>The cosine of the sun's angle from straight up there.</returns>
    private double SunCosineAt(int column)
    {
        double along = _sunAngles == 1 ? 0.5 : (double) column / (_sunAngles - 1);

        return along * 2 - 1;
    }

    /// <summary>
    /// This method mixes two numbers in the given proportion.
    /// </summary>
    /// <param name="first">The number at nothing.</param>
    /// <param name="second">The number at one.</param>
    /// <param name="howFar">How far between them to go.</param>
    /// <returns>The mixed number.</returns>
    private static double Mix(double first, double second, double howFar)
    {
        return first + (second - first) * howFar;
    }
}
