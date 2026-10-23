using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class settles what joints are asked to do and what spans stretch between, once the scene stands where it
/// will -- placed, settled atop one another, its bodies moved -- and before anything is sorted for searching.
/// <para>
/// **What they aim at may stand anywhere**, which is why it waits until now: a lamp set atop a desk learns where it
/// stands only as the desk's group is prepared, and its head must turn toward the page from there.  Joints are
/// settled outer before inner, since turning an outer one carries the inner ones round, and spans after them all,
/// since what they are stretched between may be on anything a joint has turned.
/// </para>
/// <para>
/// **At every instant the camera looks at.**  What a joint aims at may move while the shutter is open, and so may
/// whatever holds the joint, so each is settled at each instant and stood moving through them, as a body is.  Then
/// everything holding something that moved is arranged again for searching, innermost first.
/// </para>
/// </summary>
public static class KinematicsSettler
{
    // How many times a reaching chain is turned through, joint by joint, toward what it reaches for.
    private const int Passes = 64;

    // How near, as a share of the chain's reach, is near enough to stop turning it.
    private const double Near = 1e-6;

    /// <summary>
    /// This method settles every joint that aims or reaches and every span in the scene.
    /// </summary>
    /// <param name="scene">The scene, prepared, placed and its bodies moved.</param>
    /// <param name="sampleTimes">How far through the shutter each of the camera's instants looks.</param>
    public static void Settle(Scene scene, double[] sampleTimes)
    {
        List<Joint> joints = [];
        List<Surface> spans = [];
        Dictionary<string, List<Surface>> named = [];

        Find(scene.Surfaces, joints, spans, named, false);

        if (joints.Count == 0 && spans.Count == 0)
            return;

        double[] fractions = sampleTimes is { Length: > 1 } ? sampleTimes : [0];
        Dictionary<Surface, Matrix>[] settled = new Dictionary<Surface, Matrix>[fractions.Length];

        for (int instant = 0; instant < fractions.Length; instant++)
        {
            Settling now = new (instant, named);

            foreach (Joint joint in joints)
            {
                if (joint.Aim is not null)
                    now.Aim(joint);

                if (joint.Reach is not null)
                    now.Reach(joint);
            }

            foreach (Surface span in spans)
                now.Stretch(span);

            settled[instant] = now.Local;
        }

        foreach (Surface surface in settled[0].Keys)
        {
            Matrix here = settled[0][surface];
            Func<double, Matrix> motion = null;

            if (fractions.Length > 1)
            {
                Matrix undo = here.Invert();
                Matrix[] there = settled.Select(local => local[surface] * undo).ToArray();

                motion = fraction => there[Nearest(fractions, fraction)];
            }

            surface.StandAt(here, motion);
        }

        Resettle(settled[0].Keys);
    }

    // Everything the settling asks about, at one instant: where each surface stands in its parent's space -- what
    // has been settled so far laid over what was resolved -- and where the named things are.
    private sealed class Settling(int instant, Dictionary<string, List<Surface>> named)
    {
        internal Dictionary<Surface, Matrix> Local { get; } = new (ReferenceEqualityComparer.Instance);

        // How far each joint has been turned, all told, at this instant.
        private readonly Dictionary<Joint, double> _angles = new (ReferenceEqualityComparer.Instance);

        private Matrix LocalOf(Surface surface) =>
            Local.TryGetValue(surface, out Matrix matrix) ? matrix : surface.TransformAt(instant);

        private Matrix WorldOf(Surface surface) =>
            surface is null ? Matrix.Identity : WorldOf(surface.Parent) * LocalOf(surface);

        private Point WhereIs(Target target, Surface asking) =>
            target.At ?? WorldOf(Named(target.Name, asking)) * Point.Zero;

        private double AngleOf(Joint joint) => _angles.TryGetValue(joint, out double angle) ? angle : joint.Angle;

        // Turns a joint toward what it aims at: about its axis by the least turn that brings its facing round to
        // face it, within its limits, or, with no axis, by the least turn that points its facing at it.
        internal void Aim(Joint joint)
        {
            Matrix local = LocalOf(joint);
            Matrix toParent = WorldOf(joint.Parent).Invert();
            Point pivot = local * joint.Pivot;
            Vector facing = local * joint.Facing;
            Vector toward = toParent * WhereIs(joint.Aim, joint) - pivot;

            Turn(joint, local, pivot, facing, toward);
        }

        // Turns a chain so that what reaches comes as near what it reaches for as the joints' limits allow: each
        // joint from the tip inward turned about its axis to bring the tip round toward it, and the chain turned
        // through so again and again until the tip stops coming nearer.
        internal void Reach(Joint joint)
        {
            Surface tip = Named(joint.Effector, joint);
            List<Joint> chain = [];

            for (Surface surface = tip.Parent; surface is not null; surface = surface.Parent)
            {
                if (surface is Joint link)
                    chain.Add(link);

                if (ReferenceEquals(surface, joint))
                    break;
            }

            if (!chain.Contains(joint))
                throw new InvalidOperationException($"A joint reaches with '{joint.Effector}', which is not inside it.");

            Point target = WhereIs(joint.Reach, joint);
            double reach = Math.Max((WorldOf(tip) * Point.Zero - WorldOf(joint) * joint.Pivot).Magnitude, 1e-12);

            for (int pass = 0; pass < Passes; pass++)
            {
                foreach (Joint link in chain)
                {
                    Matrix local = LocalOf(link);
                    Matrix toParent = WorldOf(link.Parent).Invert();
                    Point pivot = local * link.Pivot;
                    Vector reaching = toParent * (WorldOf(tip) * Point.Zero) - pivot;
                    Vector toward = toParent * target - pivot;

                    Turn(link, local, pivot, reaching, toward);
                }

                if ((WorldOf(tip) * Point.Zero - target).Magnitude < reach * Near)
                    break;
            }
        }

        // Turns a joint about its pivot, in its parent's space, so that one way it has comes round toward another.
        private void Turn(Joint joint, Matrix local, Point pivot, Vector from, Vector to)
        {
            if (from.Magnitude < 1e-12 || to.Magnitude < 1e-12)
                return;

            Matrix turn;

            if (joint.Axis is { } axisOf)
            {
                Vector axis = (local * axisOf).Unit;
                Vector across = from - axis * from.Dot(axis), wanted = to - axis * to.Dot(axis);

                if (across.Magnitude < 1e-12 || wanted.Magnitude < 1e-12)
                    return;

                double angle = Math.Atan2(axis.Dot(across.Cross(wanted)), across.Dot(wanted));
                double was = AngleOf(joint);
                double now = joint.Limited(was + angle);

                _angles[joint] = now;
                turn = Core.Physics.Turn.About(axis, now - was).ToMatrix();
            }
            else
                turn = Core.Physics.Turn.Between(from.Unit, to.Unit).ToMatrix();

            Local[joint] = Transforms.Translate(pivot.X, pivot.Y, pivot.Z) * turn *
                           Transforms.Translate(-pivot.X, -pivot.Y, -pivot.Z) * local;
        }

        // Stands a spanned surface with its origin at one named place and its +X running toward the other, its +Y as
        // near its parent's +Y as that allows, stretched along X by the distance between them if it says so.
        internal void Stretch(Surface surface)
        {
            Matrix toParent = WorldOf(surface.Parent).Invert();
            Point from = toParent * (WorldOf(Named(surface.Span.From, surface)) * Point.Zero);
            Point to = toParent * (WorldOf(Named(surface.Span.To, surface)) * Point.Zero);
            Vector along = to - from;
            double length = along.Magnitude;

            if (length < 1e-12)
                return;

            Vector x = along / length;
            Vector up = Math.Abs(x.Y) < 0.999 ? new Vector(0, 1, 0) : new Vector(0, 0, -1);
            Vector z = x.Cross(up).Unit;
            Vector y = z.Cross(x);
            Matrix frame = new ([
                x.X, y.X, z.X, from.X,
                x.Y, y.Y, z.Y, from.Y,
                x.Z, y.Z, z.Z, from.Z,
                0, 0, 0, 1
            ]);

            if (surface.Span.Stretched)
                frame *= Transforms.Scale(length, 1, 1);

            Local[surface] = frame * LocalOf(surface);
        }

        // The thing of a name nearest the one asking: the one that shares the most of its enclosing groups with it,
        // so that each of two engines' crank pins is its own engine's.
        private Surface Named(string name, Surface asking)
        {
            if (!named.TryGetValue(name, out List<Surface> candidates))
                throw new InvalidOperationException($"Nothing is named '{name}'.");

            if (candidates.Count == 1)
                return candidates[0];

            List<Surface> around = [];

            for (Surface surface = asking; surface is not null; surface = surface.Parent)
                around.Add(surface);

            Surface nearest = null;
            int deepest = int.MaxValue;
            bool tied = false;

            foreach (Surface candidate in candidates)
            {
                int shared = int.MaxValue;

                for (Surface surface = candidate; surface is not null; surface = surface.Parent)
                {
                    int index = around.IndexOf(surface);

                    if (index >= 0)
                    {
                        shared = index;
                        break;
                    }
                }

                if (shared < deepest)
                {
                    (nearest, deepest, tied) = (candidate, shared, false);
                }
                else if (shared == deepest)
                    tied = true;
            }

            if (tied)
                throw new InvalidOperationException($"More than one thing is named '{name}', none nearer than another.");

            return nearest;
        }
    }

    // Finds every joint that aims or reaches, every span and every named thing, outer before inner; a shared shape
    // holding either has one place for many, and is refused.
    private static void Find(
        IEnumerable<Surface> surfaces, List<Joint> joints, List<Surface> spans, Dictionary<string, List<Surface>> named,
        bool shared)
    {
        foreach (Surface surface in surfaces)
        {
            if (surface is Joint { IsSolved: true } || surface.Span is not null)
            {
                if (shared)
                {
                    throw new InvalidOperationException(
                        "A joint that aims or reaches, or a span, cannot stand in a shape shared between places.");
                }

                if (surface is Joint { IsSolved: true } joint)
                    joints.Add(joint);

                if (surface.Span is not null)
                    spans.Add(surface);
            }

            if (!shared && surface.Name is { } name)
            {
                if (!named.TryGetValue(name, out List<Surface> list))
                    named[name] = list = [];

                list.Add(surface);
            }

            switch (surface)
            {
                case Group group:
                    Find(group.Surfaces, joints, spans, named, shared);
                    break;
                case CsgSurface csg:
                    Find([csg.Left, csg.Right], joints, spans, named, shared);
                    break;
                case Instance instance:
                    Find([instance.Prototype], joints, spans, named, true);
                    break;
            }
        }
    }

    // Works out again everything holding something that moved, innermost first.
    private static void Resettle(IEnumerable<Surface> moved)
    {
        Dictionary<Surface, int> holders = new (ReferenceEqualityComparer.Instance);

        foreach (Surface surface in moved)
        {
            for (Surface holder = surface.Parent; holder is not null; holder = holder.Parent)
                holders[holder] = DepthOf(holder);
        }

        foreach (Surface holder in holders.OrderByDescending(entry => entry.Value).Select(entry => entry.Key))
            holder.Resettle();
    }

    private static int DepthOf(Surface surface)
    {
        int depth = 0;

        for (Surface parent = surface.Parent; parent is not null; parent = parent.Parent)
            depth++;

        return depth;
    }

    private static int Nearest(double[] fractions, double fraction)
    {
        int best = 0;

        for (int index = 1; index < fractions.Length; index++)
        {
            if (Math.Abs(fractions[index] - fraction) < Math.Abs(fractions[best] - fraction))
                best = index;
        }

        return best;
    }
}
