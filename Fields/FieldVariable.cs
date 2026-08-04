using System.Linq.Expressions;

namespace RayTracer.Fields;

/// <summary>
/// This class represents one of the three variables a field function is a function of: the X, Y or Z
/// of the point being asked about.
/// </summary>
public class FieldVariable : FieldExpression
{
    public static readonly FieldVariable X = new (FieldAxis.X);
    public static readonly FieldVariable Y = new (FieldAxis.Y);
    public static readonly FieldVariable Z = new (FieldAxis.Z);

    /// <summary>
    /// This property holds which of the three this is.
    /// </summary>
    public FieldAxis Axis { get; }

    private FieldVariable(FieldAxis axis)
    {
        Axis = axis;
    }

    /// <summary>
    /// This method returns the variable a scene's name refers to, or <c>null</c> if the name is not
    /// one of the three.  A field function's variables are always spelled in lower case: the upper
    /// case X, Y and Z are the DSL's axis keywords, and mean something else entirely.
    /// </summary>
    /// <param name="name">The name to look for.</param>
    /// <returns>The variable of that name, or <c>null</c>.</returns>
    public static FieldVariable For(string name)
    {
        return name switch
        {
            "x" => X,
            "y" => Y,
            "z" => Z,
            _ => null
        };
    }

    /// <summary>
    /// This method is used to emit this variable as a .NET expression: whichever of the compiled
    /// function's three parameters it stands for.
    /// </summary>
    internal override Expression ToDotNet(
        ParameterExpression x, ParameterExpression y, ParameterExpression z)
    {
        return Axis switch
        {
            FieldAxis.X => x,
            FieldAxis.Y => y,
            FieldAxis.Z => z,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override string ToString()
    {
        return Axis.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// A variable changes with itself and with nothing else.
    /// </summary>
    public override FieldExpression Differentiate(FieldAxis axis)
    {
        return axis == Axis ? FieldConstant.One : FieldConstant.Zero;
    }

    /// <summary>
    /// A variable ranges over whatever range was given for it.
    /// </summary>
    public override FieldRange Bound(FieldRange x, FieldRange y, FieldRange z)
    {
        return Axis switch
        {
            FieldAxis.X => x,
            FieldAxis.Y => y,
            FieldAxis.Z => z,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
