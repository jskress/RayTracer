using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create tori.
/// </summary>
public class TorusInstructionSet : SurfaceInstructionSet<Torus>
{
    private readonly Term _majorTerm;
    private readonly Term _minorTerm;

    public TorusInstructionSet(Term majorTerm, Term minorTerm)
    {
        _majorTerm = majorTerm;
        _minorTerm = minorTerm;
    }

    /// <summary>
    /// This method is used to create our torus.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    protected override void CreateObject(Variables variables)
    {
        double major = _majorTerm.GetValue<double>(variables);
        double minor = _minorTerm.GetValue<double>(variables);

        CreatedObject = new Torus(major, minor);
    }

    /// <summary>
    /// This method creates a copy of this instruction set,
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public override object Copy()
    {
        TorusInstructionSet instructionSet = new (_majorTerm, _minorTerm);

        CopyInto(instructionSet);

        return instructionSet;
    }
}
