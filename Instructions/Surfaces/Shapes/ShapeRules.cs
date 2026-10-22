using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces.Shapes;

/// <summary>
/// This class holds a scope: the oriented box a shape grammar's rule works on.  It has a corner, three axes of
/// unit length -- right-handed, X across, Y up, Z into what it stands for -- and a size along each.  A facade's
/// scope stands on the face, its Z pointing into the building, so -Z is outward, as a window from the windows
/// library is authored.
/// </summary>
public readonly record struct ShapeScope(Point Corner, Vector X, Vector Y, Vector Z, double Width, double Height, double Depth)
{
    /// <summary>
    /// This method returns the point at the given distances along the scope's axes from its corner.
    /// </summary>
    public Point At(double across, double up, double into) => Corner + X * across + Y * up + Z * into;

    /// <summary>
    /// This method returns the scope's size along one of its axes: 0 for X, 1 for Y, 2 for Z.
    /// </summary>
    public double SizeAlong(int axis) => axis switch { 0 => Width, 1 => Height, _ => Depth };

    /// <summary>
    /// This method returns the part of the scope from an offset along one of its axes, so long.
    /// </summary>
    public ShapeScope Part(int axis, double offset, double length) => axis switch
    {
        0 => this with { Corner = At(offset, 0, 0), Width = length },
        1 => this with { Corner = At(0, offset, 0), Height = length },
        _ => this with { Corner = At(0, 0, offset), Depth = length }
    };

    /// <summary>
    /// This method returns the matrix that carries a thing's own space into the scope: its origin to the middle of
    /// the scope's near face, its axes to the scope's.
    /// </summary>
    public Matrix FaceFrame()
    {
        Point middle = At(Width / 2, Height / 2, 0);

        return new Matrix([
            X.X, Y.X, Z.X, middle.X,
            X.Y, Y.Y, Z.Y, middle.Y,
            X.Z, Y.Z, Z.Z, middle.Z,
            0, 0, 0, 1
        ]);
    }

    /// <summary>
    /// This method returns the matrix that carries the cube from -1 to 1 onto the scope's box.
    /// </summary>
    public Matrix BoxFrame()
    {
        Point middle = At(Width / 2, Height / 2, Depth / 2);

        return new Matrix([
            X.X * Width / 2, Y.X * Height / 2, Z.X * Depth / 2, middle.X,
            X.Y * Width / 2, Y.Y * Height / 2, Z.Y * Depth / 2, middle.Y,
            X.Z * Width / 2, Y.Z * Height / 2, Z.Z * Depth / 2, middle.Z,
            0, 0, 0, 1
        ]);
    }
}

/// <summary>
/// This class holds one rule of a shape grammar: its name and what it does to the scope it is handed.
/// </summary>
public class ShapeRule
{
    /// <summary>
    /// This property holds the rule's name, by which other rules hand it a scope.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// This property holds what the rule does, in order, each to the same scope.
    /// </summary>
    public List<ShapeAction> Actions { get; init; } = [];
}

/// <summary>
/// This class is the base for everything a rule may do with a scope: divide it, hand it on, or put something in it.
/// </summary>
public abstract class ShapeAction
{
    /// <summary>
    /// This method carries out the action on a scope, with the variables the scope's rule sees.
    /// </summary>
    internal abstract void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth);
}

/// <summary>
/// This class hands the scope to another rule by name.
/// </summary>
public class ShapeSymbol : ShapeAction
{
    public string Name { get; init; }

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        derivation.Derive(Name, scope, variables, depth + 1);
    }
}

/// <summary>
/// This class gives a scope a height and hands it on: a lot raised into a mass.
/// </summary>
public class ShapeExtrude : ShapeAction
{
    public Resolver<double> Height { get; init; }
    public List<ShapeAction> Then { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        derivation.Run(Then, scope with { Height = derivation.Number(Height, variables) }, variables, depth);
    }
}

/// <summary>
/// This class divides a scope along one of its axes into parts, each of a fixed size or a share of what the fixed
/// ones leave.  Fixed sizes that will not fit are all shrunk alike to fit, and the shares then come to nothing.
/// </summary>
public class ShapeSplit : ShapeAction
{
    public int Axis { get; init; }
    public List<(Resolver<double> Size, bool Share, List<ShapeAction> Then)> Parts { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        double length = scope.SizeAlong(Axis);
        double[] sizes = Parts.Select(part => Math.Max(0, derivation.Number(part.Size, variables))).ToArray();
        double fixedTotal = Parts.Select((part, index) => part.Share ? 0 : sizes[index]).Sum();
        double shareTotal = Parts.Select((part, index) => part.Share ? sizes[index] : 0).Sum();
        double scale = fixedTotal > length && fixedTotal > 0 ? length / fixedTotal : 1;
        double left = Math.Max(0, length - fixedTotal * scale);
        double offset = 0;

        for (int index = 0; index < Parts.Count; index++)
        {
            double size = Parts[index].Share
                ? shareTotal > 0 ? left * sizes[index] / shareTotal : 0
                : sizes[index] * scale;

            if (size > 1e-9)
            {
                Variables mine = ShapeDerivation.Counted(variables, index, Parts.Count, null);

                derivation.Run(Parts[index].Then, scope.Part(Axis, offset, size), mine, depth);
            }

            offset += size;
        }
    }
}

/// <summary>
/// This class tiles a scope along one of its axes with as many parts of about a size as fit, stretched to fit
/// exactly, each told its number and how many there are.
/// </summary>
public class ShapeRepeat : ShapeAction
{
    public int Axis { get; init; }
    public Resolver<double> Size { get; init; }
    public string IndexName { get; init; }
    public List<ShapeAction> Then { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        double length = scope.SizeAlong(Axis);
        double size = derivation.Number(Size, variables);

        if (length <= 1e-9 || size <= 0)
            return;

        int count = Math.Max(1, (int) Math.Round(length / size));
        double each = length / count;

        for (int index = 0; index < count; index++)
        {
            Variables mine = ShapeDerivation.Counted(variables, index, count, IndexName);

            derivation.Run(Then, scope.Part(Axis, index * each, each), mine, depth);
        }
    }
}

/// <summary>
/// This class hands each face of a box scope on as a scope of its own, standing on the face with its Z pointing in:
/// front (the near side, -Z), back, left, right, top and bottom, and `sides` for any of the four upright faces not
/// named.  How deep a face's scope reaches into the box is said, or nothing.
/// </summary>
public class ShapeFaces : ShapeAction
{
    public Resolver<double> Depth { get; init; }
    public Dictionary<string, List<ShapeAction>> Faces { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        double deep = Depth is null ? 0 : derivation.Number(Depth, variables);
        double w = scope.Width, h = scope.Height, d = scope.Depth;

        void Face(string name, bool upright, ShapeScope face)
        {
            if (Faces.TryGetValue(name, out List<ShapeAction> actions) || (upright && Faces.TryGetValue("sides", out actions)))
                derivation.Run(actions, face, variables, depth);
        }

        Face("front", true, new ShapeScope(scope.Corner, scope.X, scope.Y, scope.Z, w, h, deep));
        Face("back", true, new ShapeScope(scope.At(w, 0, d), -scope.X, scope.Y, -scope.Z, w, h, deep));
        Face("left", true, new ShapeScope(scope.At(0, 0, d), -scope.Z, scope.Y, scope.X, d, h, deep));
        Face("right", true, new ShapeScope(scope.At(w, 0, 0), scope.Z, scope.Y, -scope.X, d, h, deep));
        Face("top", false, new ShapeScope(scope.At(0, h, 0), scope.X, scope.Z, -scope.Y, w, d, deep));
        Face("bottom", false, new ShapeScope(scope.At(0, 0, d), scope.X, -scope.Z, scope.Y, w, d, deep));
    }
}

/// <summary>
/// This class moves a scope along its own axes and hands it on.
/// </summary>
public class ShapeMove : ShapeAction
{
    public Resolver<Vector> By { get; init; }
    public List<ShapeAction> Then { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        Vector by = By.Resolve(derivation.Context, variables);

        derivation.Run(Then, scope with { Corner = scope.At(by.X, by.Y, by.Z) }, variables, depth);
    }
}

/// <summary>
/// This class sets a scope's size along its axes, its corner where it was, and hands it on.
/// </summary>
public class ShapeResize : ShapeAction
{
    public Resolver<Vector> To { get; init; }
    public List<ShapeAction> Then { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        Vector to = To.Resolve(derivation.Context, variables);

        derivation.Run(Then, scope with { Width = to.X, Height = to.Y, Depth = to.Z }, variables, depth);
    }
}

/// <summary>
/// This class picks one of several ways on at random, each as likely as its weight says.  The pick is the same for
/// the same seed and the same place, however the grammar is walked.
/// </summary>
public class ShapeChoose : ShapeAction
{
    /// <summary>
    /// This property holds the choice's own number, given it as the grammar is read, so that two choices at the
    /// same place draw differently -- and the same every time the scene is rendered.
    /// </summary>
    public int Id { get; init; }

    public List<(Resolver<double> Weight, List<ShapeAction> Then)> Ways { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        double[] weights = Ways.Select(way => Math.Max(0, derivation.Number(way.Weight, variables))).ToArray();
        double total = weights.Sum();

        if (total <= 0)
            return;

        double draw = derivation.DrawFor(this, scope) * total;

        for (int index = 0; index < Ways.Count; index++)
        {
            draw -= weights[index];

            if (draw < 0 || index == Ways.Count - 1)
            {
                derivation.Run(Ways[index].Then, scope, variables, depth);

                return;
            }
        }
    }
}

/// <summary>
/// This class takes one of two ways on, by a condition the scope's variables may say: a bay too narrow for a
/// window is wall.
/// </summary>
public class ShapeIf : ShapeAction
{
    public Resolver<bool> Condition { get; init; }
    public List<ShapeAction> Then { get; init; } = [];
    public List<ShapeAction> Else { get; init; } = [];

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        derivation.Run(Condition.Resolve(derivation.Context, variables) ? Then : Else, scope, variables, depth);
    }
}

/// <summary>
/// This class fills a scope with a solid box: a wall, a ledge, a plinth.  It is a polyhedron in the grammar's own
/// space rather than a cube stretched to fit, so a material's pattern runs on unbroken from one box to the next, a
/// facade's brick from bay to bay.
/// </summary>
public class ShapeFill : ShapeAction
{
    public Resolver<Material> Material { get; init; }

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        if (scope.Width <= 1e-9 || scope.Height <= 1e-9 || scope.Depth <= 1e-9)
            return;

        derivation.AddBox(scope, Material?.Resolve(derivation.Context, variables));
    }
}

/// <summary>
/// This class stands surfaces in a scope: their own origin at the middle of the scope's near face, their axes the
/// scope's, unscaled -- so a window from the windows library, its middle on the face of a wall and -Z outward,
/// goes where it should and is sized by the scope's `width` and `height` in its own arguments.
/// </summary>
public class ShapePlace : ShapeAction
{
    public ISurfaceResolver Surfaces { get; init; }

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        derivation.AddPlaced(scope, Surfaces, variables);
    }
}

/// <summary>
/// This class puts a roof on a box scope, on its top: gabled, its ridge along X or `across` it along Z, with its
/// gable ends in the walls' material; hipped, every side sloping; or flat, a slab.  It may overhang every side, and
/// a gabled one may reach past its gable ends by another amount, its verge -- nothing, for the houses of a terrace,
/// whose roofs meet over the walls between them.
/// </summary>
public class ShapeRoof : ShapeAction
{
    public string Kind { get; init; }
    public Resolver<double> Angle { get; init; }
    public Resolver<double> Overhang { get; init; }
    public Resolver<double> Verge { get; init; }
    public bool Across { get; init; }
    public Resolver<Material> Material { get; init; }
    public Resolver<Material> Gables { get; init; }

    internal override void Apply(ShapeDerivation derivation, ShapeScope scope, Variables variables, int depth)
    {
        derivation.AddRoof(this, scope, variables);
    }
}
