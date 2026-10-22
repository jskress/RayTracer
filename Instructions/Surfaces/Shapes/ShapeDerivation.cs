using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces.Shapes;

/// <summary>
/// This class carries out a shape grammar: from the first scope, each rule is handed its scope and does what it
/// says -- divides it and hands the parts on, or puts something in it -- until nothing is left to hand on, and
/// what was put in the scopes is the building.
/// <para>
/// Every scope's rule sees its scope's size as `width`, `height` and `depth`, and the part of a split or repeat
/// sees its number and how many there are as `index` and `count`, alongside every name the grammar was written
/// among; so a rule can size a window to its bay, or tell the ground floor from the rest.
/// </para>
/// </summary>
public class ShapeDerivation
{
    // How deep rules may hand scopes on before the grammar is taken to be going round in a circle.
    private const int Deepest = 200;

    private readonly Dictionary<string, ShapeRule> _rules;
    private readonly Action<Surface> _add;
    private readonly ulong _seed;

    /// <summary>
    /// This property holds the render context the grammar is carried out in.
    /// </summary>
    public RenderContext Context { get; }

    public ShapeDerivation(IEnumerable<ShapeRule> rules, RenderContext context, int seed, Action<Surface> add)
    {
        _rules = rules.ToDictionary(rule => rule.Name);
        _add = add;
        _seed = (ulong) seed;
        Context = context;
    }

    /// <summary>
    /// This method hands a scope to the rule of the given name.
    /// </summary>
    public void Derive(string name, ShapeScope scope, Variables variables, int depth)
    {
        if (!_rules.TryGetValue(name, out ShapeRule rule))
            throw new Exception($"A shape grammar has no rule named '{name}'.");

        Run(rule.Actions, scope, variables, depth);
    }

    /// <summary>
    /// This method carries out a list of actions, each on the same scope, its size seen as `width`, `height` and
    /// `depth`.
    /// </summary>
    public void Run(List<ShapeAction> actions, ShapeScope scope, Variables variables, int depth)
    {
        if (depth > Deepest)
            throw new Exception($"A shape grammar handed scopes on {Deepest} deep; does a rule hand its scope back to itself?");

        Variables mine = new (variables);

        mine.SetValue("width", scope.Width);
        mine.SetValue("height", scope.Height);
        mine.SetValue("depth", scope.Depth);

        foreach (ShapeAction action in actions)
            action.Apply(this, scope, mine, depth);
    }

    /// <summary>
    /// This method returns a scope of variables naming a part's number and how many parts there are, and the
    /// number again by a name of the scene's own, if one was given.
    /// </summary>
    public static Variables Counted(Variables variables, int index, int count, string name)
    {
        Variables counted = new (variables);

        counted.SetValue("index", (double) index);
        counted.SetValue("count", (double) count);

        if (name is not null)
            counted.SetValue(name, (double) index);

        return counted;
    }

    /// <summary>
    /// This method resolves a number.
    /// </summary>
    public double Number(Resolver<double> resolver, Variables variables) => resolver.Resolve(Context, variables);

    /// <summary>
    /// This method returns a draw from nought to one for a choice at a scope: the same for the same seed, choice and
    /// place, however the grammar was walked to get there.
    /// </summary>
    public double DrawFor(ShapeChoose choice, ShapeScope scope)
    {
        ulong hash = _seed ^ 0x9E3779B97F4A7C15UL;

        foreach (double value in new[]
                 {
                     choice.Id, scope.Corner.X, scope.Corner.Y, scope.Corner.Z,
                     scope.Width, scope.Height, scope.Depth
                 })
        {
            hash = Mix(hash ^ (ulong) BitConverter.DoubleToInt64Bits(Math.Round(value, 6)));
        }

        return (hash >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;

        return value ^ (value >> 31);
    }

    /// <summary>
    /// This method puts a solid box in a scope, in the grammar's own space.
    /// </summary>
    public void AddBox(ShapeScope scope, Material material)
    {
        Polyhedron box = new () { Material = material };

        box.SetFrom(Polytope.Cube(scope.BoxFrame()));
        _add(box);
    }

    /// <summary>
    /// This method stands what a resolver makes in a scope, on the middle of its near face.
    /// </summary>
    public void AddPlaced(ShapeScope scope, ISurfaceResolver resolver, Variables variables)
    {
        Surface surface = resolver.ResolveToSurface(Context, variables);

        surface.Transform = scope.FaceFrame() * surface.Transform;
        _add(surface);
    }

    /// <summary>
    /// This method puts a roof on a box scope.  Each part is a convex polyhedron worked out in the scope's own
    /// axes -- u along the ridge, v across it, up its Y -- and carried out into the grammar's space.
    /// </summary>
    public void AddRoof(ShapeRoof roof, ShapeScope scope, Variables variables)
    {
        double angle = roof.Angle is null ? 0 : roof.Angle.Resolve(Context, variables);
        double over = roof.Overhang is null ? 0 : Number(roof.Overhang, variables);
        double verge = roof.Verge is null ? over : Number(roof.Verge, variables);
        Material material = roof.Material?.Resolve(Context, variables);
        Material gables = roof.Gables?.Resolve(Context, variables);
        bool across = roof.Across;
        double lengthU = across ? scope.Depth : scope.Width;
        double lengthV = across ? scope.Width : scope.Depth;
        double baseY = scope.Height;
        double slope = Math.Tan(angle);
        double thick = Math.Clamp(lengthV * 0.02, 0.04, 0.2);

        // The scope's own axes, with u along the ridge and v across it: a point (u, y, v) is carried to the corner
        // plus that much along each.
        Vector alongU = across ? scope.Z : scope.X;
        Vector alongV = across ? scope.X : scope.Z;
        Matrix frame = new ([
            alongU.X, scope.Y.X, alongV.X, scope.Corner.X,
            alongU.Y, scope.Y.Y, alongV.Y, scope.Corner.Y,
            alongU.Z, scope.Y.Z, alongV.Z, scope.Corner.Z,
            0, 0, 0, 1
        ]);

        // A handedness-keeping frame: when u and v swap, so must the third axis's sense.  The polyhedra would come
        // out the same either way -- the roof is symmetric across its ridge, and a face's normal is carried by the
        // inverse transpose, which keeps it pointing out -- but a mirrored carry would leave each face's corners
        // running clockwise seen from outside, against what a polytope promises.
        if (across)
            frame = new Matrix([
                alongU.X, scope.Y.X, -alongV.X, scope.Corner.X + alongV.X * lengthV,
                alongU.Y, scope.Y.Y, -alongV.Y, scope.Corner.Y + alongV.Y * lengthV,
                alongU.Z, scope.Y.Z, -alongV.Z, scope.Corner.Z + alongV.Z * lengthV,
                0, 0, 0, 1
            ]);

        void Add(Polytope polytope, Material wears)
        {
            if (polytope is null)
                return;

            Polyhedron part = new () { Material = wears };

            part.SetFrom(polytope.Carried(frame));
            _add(part);
        }

        // y - slope v <= c, as a unit normal and offset.
        static (Vector, double) Plane(double y, double v, double offset)
        {
            double length = Math.Sqrt(y * y + v * v);

            return (new Vector(0, y / length, v / length), offset / length);
        }

        Polytope Cut(Polytope polytope, params (Vector Normal, double Offset)[] planes)
        {
            foreach ((Vector normal, double offset) in planes)
            {
                polytope = polytope?.Clip(normal, offset);
            }

            return polytope;
        }

        switch (roof.Kind)
        {
            case "flat":
            {
                Add(Polytope.Box(new Point(-over, baseY, -over), new Point(lengthU + over, baseY + thick, lengthV + over)), material);
                break;
            }
            case "hip":
            {
                double rise = Math.Min(lengthU, lengthV) / 2 * slope;
                Polytope solid = Polytope.Box(
                    new Point(-over, baseY - over * slope, -over),
                    new Point(lengthU + over, baseY + rise, lengthV + over));

                // Under each of the four slopes: y - baseY <= slope times the distance in from that side.
                solid = Cut(solid,
                    Plane(1, -slope, baseY),
                    Plane(1, slope, baseY + slope * lengthV));
                solid = solid?.Clip(new Vector(-slope, 1, 0).Unit, baseY / new Vector(-slope, 1, 0).Magnitude);
                solid = solid?.Clip(new Vector(slope, 1, 0).Unit, (baseY + slope * lengthU) / new Vector(slope, 1, 0).Magnitude);
                Add(solid, material);
                break;
            }
            default:
            {
                // Gabled: the gable ends in the walls, under two slabs of roof meeting at the ridge, reaching past the
                // eaves by the overhang and past the gable ends by the verge.
                double rise = lengthV / 2 * slope;
                double through = thick * Math.Sqrt(1 + slope * slope);

                Add(Cut(Polytope.Box(new Point(0, baseY, 0), new Point(lengthU, baseY + rise, lengthV)),
                        Plane(1, -slope, baseY),
                        Plane(1, slope, baseY + slope * lengthV)),
                    gables);

                Add(Cut(Polytope.Box(new Point(-verge, baseY - over * slope - thick, -over),
                            new Point(lengthU + verge, baseY + rise + through, lengthV / 2)),
                        Plane(-1, slope, -baseY),
                        Plane(1, -slope, baseY + through)),
                    material);
                Add(Cut(Polytope.Box(new Point(-verge, baseY - over * slope - thick, lengthV / 2),
                            new Point(lengthU + verge, baseY + rise + through, lengthV + over)),
                        Plane(-1, -slope, -baseY - slope * lengthV),
                        Plane(1, slope, baseY + slope * lengthV + through)),
                    material);
                break;
            }
        }
    }
}
