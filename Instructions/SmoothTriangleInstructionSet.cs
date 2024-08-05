using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create smooth triangles.
/// </summary>
public class SmoothTriangleInstructionSet : SurfaceInstructionSet<SmoothTriangle>
{
    private readonly Term _point1Term;
    private readonly Term _point2Term;
    private readonly Term _point3Term;
    private readonly Term _normal1Term;
    private readonly Term _normal2Term;
    private readonly Term _normal3Term;

    public SmoothTriangleInstructionSet(
        Term point1Term, Term point2Term, Term point3Term,
        Term normal1Term, Term normal2Term, Term normal3Term)
    {
        _point1Term = point1Term;
        _point2Term = point2Term;
        _point3Term = point3Term;
        _normal1Term = normal1Term;
        _normal2Term = normal2Term;
        _normal3Term = normal3Term;
    }

    /// <summary>
    /// This method may be used by subclasses to create our group.
    /// </summary>
    /// <param name="variables">The current set of scoped variables.</param>
    protected override void CreateObject(Variables variables)
    {
        Point point1 = _point1Term.GetValue<Point>(variables);
        Point point2 = _point2Term.GetValue<Point>(variables);
        Point point3 = _point3Term.GetValue<Point>(variables);
        Vector normal1 = _normal1Term.GetValue<Vector>(variables);
        Vector normal2 = _normal2Term.GetValue<Vector>(variables);
        Vector normal3 = _normal3Term.GetValue<Vector>(variables);

        CreatedObject = new SmoothTriangle(
            point1, point2, point3, normal1, normal2, normal3);
    }

    /// <summary>
    /// This method creates a copy of this instruction set,
    /// </summary>
    /// <returns>The copy of this instruction set.</returns>
    public override object Copy()
    {
        SmoothTriangleInstructionSet instructionSet = new (
            _point1Term, _point2Term, _point3Term, _normal1Term, _normal2Term, _normal3Term);

        CopyInto(instructionSet);

        return instructionSet;
    }
}
