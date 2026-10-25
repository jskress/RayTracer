using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Core.Physics;
using RayTracer.General;

namespace RayTracer.Geometry;

/// <summary>
/// This class moves a scene's bodies and swarms to where they stand at the frame's time.  It runs once the scene's
/// surfaces are prepared and placed and before they are sorted for searching: everything else in the scene is
/// what the bodies strike, found by rays, and nothing yet has been decided on the strength of where a body stands.
/// <para>
/// **The world is moved from time nought for every frame**, so a frame is the same however it is asked for --
/// alone, or in a run.  Through the frame it is moved on, as far as the shutter is open, and each body stands where
/// it is at each instant the camera looks at, so a falling thing is blurred along its fall.
/// </para>
/// </summary>
internal static class PhysicsSettler
{
    /// <summary>
    /// This method moves the scene's bodies and swarms.
    /// </summary>
    /// <param name="scene">The scene, its surfaces prepared.</param>
    /// <param name="context">The render context, which holds the frame's time, gravity and the wind.</param>
    /// <param name="sampleTimes">How far through the shutter each of the camera's instants looks.</param>
    internal static void Settle(Scene scene, RenderContext context, double[] sampleTimes)
    {
        RefuseBuriedBodies(scene.Surfaces, true, new HashSet<Surface>(ReferenceEqualityComparer.Instance));

        List<Surface> bodies = scene.Surfaces.Where(surface => surface.Body is not null).ToList();
        List<Swarm> swarms = scene.Surfaces.OfType<Swarm>().ToList();

        if (bodies.Count == 0 && swarms.Count == 0)
            return;

        List<Surface> still = scene.Surfaces
            .Where(surface => surface.Body is null && surface is not Swarm)
            .ToList();
        Func<Point, Vector, double, (double, Vector)?> strike = StrikerFor(still);
        double[] instants = context.FrameInterval > 0 && sampleTimes is { Length: > 1 }
            ? sampleTimes.Distinct().Order().ToArray()
            : [];

        if (bodies.Count > 0)
            MoveTheBodies(bodies, context, instants, strike);

        foreach (Swarm swarm in swarms)
            swarm.Fill(strike, context.Gravity, context.Wind, context.Time, context.FrameInterval, instants);
    }

    // A body moves in the scene's own space, so it must stand there: one inside a group would be moved in the
    // group's space, and a group moved or shared would carry it somewhere the world never put it.  A swarm is only
    // filled at the top, so one buried would stay empty.  A shared shape is looked through once, however many places
    // it stands.
    private static void RefuseBuriedBodies(IEnumerable<Surface> surfaces, bool top, HashSet<Surface> walked)
    {
        foreach (Surface surface in surfaces)
        {
            if (!top && surface.Body is not null)
                throw new InvalidOperationException("A body must stand at the top of the scene, not inside a group.");

            if (!top && surface is Swarm)
                throw new InvalidOperationException("A swarm must stand at the top of the scene, not inside a group.");

            switch (surface)
            {
                case Swarm:
                    break;
                case Group group:
                    RefuseBuriedBodies(group.Surfaces, false, walked);
                    break;
                case CsgSurface csg:
                    RefuseBuriedBodies([csg.Left, csg.Right], false, walked);
                    break;
                case Instance instance when walked.Add(instance.Prototype):
                    RefuseBuriedBodies([instance.Prototype], false, walked);
                    break;
            }
        }
    }

    // How a body finds the scene: the nearest crossing along a ray within a distance, among everything that is
    // not a body, and the normal there turned back toward where the ray set off.
    private static Func<Point, Vector, double, (double, Vector)?> StrikerFor(List<Surface> still)
    {
        ArrangedSurfaces arranged = new ();

        arranged.Arrange(still);

        return (from, direction, within) =>
        {
            Ray ray = new (from, direction);
            List<Intersection> hits = [];

            arranged.IntersectWithin(ray, hits, within, still);

            Intersection nearest = null;

            foreach (Intersection hit in hits)
            {
                if (hit.Distance > 1e-9 && hit.Distance <= within && (nearest is null || hit.Distance < nearest.Distance))
                    nearest = hit;
            }

            if (nearest is null)
                return null;

            hits.Sort();
            nearest.PrepareUsing(ray, hits);

            return (nearest.Distance, nearest.Normal);
        };
    }

    private static void MoveTheBodies(
        List<Surface> bodies, RenderContext context, double[] instants,
        Func<Point, Vector, double, (double, Vector)?> strike)
    {
        World world = new (context.Gravity, context.Wind, strike);
        List<(Surface Surface, Mover Mover, Matrix Start)> moving = [];
        Dictionary<string, Mover> named = [];

        foreach (Surface surface in bodies)
        {
            Matrix start = surface.Transform;
            Point center = start * Point.Zero;
            BoundingBox box = surface.TrueBoundingBox() ?? new BoundingBox().Add(new Point(-1, -1, -1)).Add(new Point(1, 1, 1));
            bool isABox = surface.Body.IsABox ?? surface is Cube;
            Vector[] corners = isABox ? CornersOf(box, start, center) : null;
            double radius = surface.Body.Radius ?? (isABox
                ? corners.Max(corner => corner.Magnitude)
                : RadiusOf(box, start, center));
            Mover mover = world.Add(surface.Body, center, radius, corners);

            moving.Add((surface, mover, start));

            if (surface.Name is { } name && !named.TryAdd(name, mover))
                throw new InvalidOperationException($"Two bodies are named '{name}', so a string tied to it would not know which.");
        }

        world.Tie(named);
        world.RunTo(context.Time);

        Dictionary<Mover, (Point Position, Turn Turn)> now = moving.ToDictionary(
            entry => entry.Mover, entry => (entry.Mover.Position, entry.Mover.Turn));
        List<Dictionary<Mover, (Point, Turn)>> through = [];

        foreach (double instant in instants)
        {
            world.RunTo(context.Time + instant * context.FrameInterval);
            through.Add(moving.ToDictionary(entry => entry.Mover, entry => (entry.Mover.Position, entry.Mover.Turn)));
        }

        foreach ((Surface surface, Mover mover, Matrix start) in moving)
        {
            Matrix StandingAt((Point Position, Turn Turn) pose) =>
                Transforms.Translate(pose.Position.X, pose.Position.Y, pose.Position.Z) * pose.Turn.ToMatrix() *
                Transforms.Translate(-mover.Start.X, -mover.Start.Y, -mover.Start.Z) * start;

            Matrix here = StandingAt(now[mover]);
            Func<double, Matrix> motion = null;

            if (instants.Length > 0)
            {
                Matrix undo = here.Invert();
                Matrix[] there = through.Select(poses => StandingAt(poses[mover]) * undo).ToArray();

                motion = fraction => there[Nearest(instants, fraction)];
            }

            surface.StandAt(here, motion);
        }
    }

    private static int Nearest(double[] instants, double fraction)
    {
        int best = 0;

        for (int index = 1; index < instants.Length; index++)
        {
            if (Math.Abs(instants[index] - fraction) < Math.Abs(instants[best] - fraction))
                best = index;
        }

        return best;
    }

    // A box's corners from its middle, in the scene's space, in the order a box's are: the first differs from the
    // second in X, the third in Y and the fifth in Z.
    private static Vector[] CornersOf(BoundingBox box, Matrix start, Point center)
    {
        Vector[] corners = new Vector[8];

        for (int corner = 0; corner < 8; corner++)
        {
            Point at = new (
                (corner & 1) == 0 ? box.Minimum.X : box.Maximum.X,
                (corner & 2) == 0 ? box.Minimum.Y : box.Maximum.Y,
                (corner & 4) == 0 ? box.Minimum.Z : box.Maximum.Z);

            corners[corner] = start * at - center;
        }

        return corners;
    }

    // The ball round a thing: as far as its box reaches from its middle along each of its own axes, the farthest.
    private static double RadiusOf(BoundingBox box, Matrix start, Point center)
    {
        Point middle = start * new Point(
            (box.Minimum.X + box.Maximum.X) / 2, (box.Minimum.Y + box.Maximum.Y) / 2, (box.Minimum.Z + box.Maximum.Z) / 2);
        Vector half = (box.Maximum - box.Minimum) / 2;
        double reach = new[]
        {
            (start * new Vector(half.X, 0, 0)).Magnitude, (start * new Vector(0, half.Y, 0)).Magnitude,
            (start * new Vector(0, 0, half.Z)).Magnitude
        }.Max();

        return reach + (middle - center).Magnitude;
    }
}
