namespace RayTracer.Geometry;

/// <summary>
/// This part of a ground being worn sheds scree from its crags.
/// </summary>
internal sealed partial class ErodingGround
{
    // Each point's way back from a neighbour: the way that neighbour lies from it, turned round.
    private static readonly int[] Opposite = [1, 0, 3, 2, 7, 6, 5, 4];

    // How much of what bare rock stands too steep by it gives up as loose rock in one round of weathering.
    private const double WeatheringRate = 0.05;

    // How much of the most that loose rock stands too steep by slides off it in one round: under a half, so that
    // a point and the neighbour it slides to never swap which is the higher.
    private const double SlideShare = 0.45;

    /// <summary>
    /// This method sheds scree: rock standing steeper than loose rock can rest weathers, where it is bare, into
    /// loose rock, and loose rock on any slope steeper than that slides down it until it rests -- piling at the foot
    /// of every crag in an apron at its angle of repose, which buries the crag's foot and keeps the weather from it
    /// there, so the crag stands on above it, falling back.
    /// <para>
    /// It is worked out in rounds, every point at once from where every point stood: in each round of weathering a
    /// bare crag gives up a twentieth of how far it stands too steep, and the loose rock on every point slides to its
    /// lower neighbours, shared by how far each stands too steep below it.  It weathers for as many rounds as the
    /// time says, the time in widths of the grid -- one is as many rounds as the grid has points across, so a finer
    /// grid, crossed in more rounds, weathers as far -- and the loose rock then slides until it lies still: until
    /// nothing moves as much as a hundredth of what a slope at rest rises over a step of the grid, or for as many
    /// rounds again as the grid has points across.  Its last creep down a long apron dies away as slowly as heat
    /// along a bar, and asked to stop at a millionth of the land's height, it never did.
    /// </para>
    /// </summary>
    /// <param name="time">How long the crags have been shedding, in rounds per point across the grid.</param>
    /// <param name="repose">The steepest angle loose rock rests at.</param>
    internal void ShedScree(double time, double repose)
    {
        double tan = Math.Tan(repose);
        double across = Math.Sqrt((double) _columns * _rows);
        int weathering = Math.Max(1, (int) Math.Round(time * across));
        int most = weathering + (int) across;
        double cover = Math.Min(_dx, _dz) * tan;
        double still = Math.Min(_dx, _dz) * tan * 0.01;
        double[] loose = new double[_count];
        double[] slides = new double[_count * 8];
        double[] sent = new double[_count];
        double[] fastest = new double[_rows];

        for (int round = 0; round < most; round++)
        {
            bool weathers = round < weathering;

            // Everything every point sends each way, from where everything stands.
            Parallel.For(0, _rows, row =>
            {
                for (int column = 0; column < _columns; column++)
                {
                    int point = row * _columns + column;
                    double total = 0;
                    double steepest = 0;

                    for (int way = 0; way < 8; way++)
                    {
                        int neighbor = NeighborOf(point, way);
                        double over = neighbor < 0 ? 0 : _ground[point] - _ground[neighbor] - _run[way] * tan;

                        slides[point * 8 + way] = Math.Max(over, 0);
                        total += slides[point * 8 + way];
                        steepest = Math.Max(steepest, over);
                    }

                    // Bare rock too steep gives up some of itself, as loose rock on itself; a cover of loose rock as
                    // thick as a grid step stands at its rest keeps the weather from it.
                    if (weathers && steepest > 0 && loose[point] < cover)
                        loose[point] += WeatheringRate * steepest * (1 - loose[point] / cover);

                    double moved = Math.Min(loose[point], SlideShare * steepest);

                    sent[point] = total > 0 ? moved : 0;

                    for (int way = 0; way < 8; way++)
                        slides[point * 8 + way] = total > 0 ? moved * slides[point * 8 + way] / total : 0;
                }
            });

            // Everything every point is sent, from every neighbour, less what it sent.
            Parallel.For(0, _rows, row =>
            {
                double fast = 0;

                for (int column = 0; column < _columns; column++)
                {
                    int point = row * _columns + column;
                    double change = -sent[point];

                    for (int way = 0; way < 8; way++)
                    {
                        int neighbor = NeighborOf(point, way);

                        if (neighbor >= 0)
                            change += slides[neighbor * 8 + Opposite[way]];
                    }

                    _ground[point] += change;
                    loose[point] += change;
                    fast = Math.Max(fast, sent[point]);
                }

                fastest[row] = fast;
            });

            if (!weathers && fastest.Max() < still)
                break;
        }
    }
}
