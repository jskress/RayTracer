using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to create triangles.
/// </summary>
public class TriangleInstructionSet : SurfaceInstructionSet<Triangle>
{
    private readonly Term _point1Term;
    private readonly Term _point2Term;
    private readonly Term _point3Term;

    public TriangleInstructionSet(Term point1Term, Term point2Term, Term point3Term)
    {
        _point1Term = point1Term;
        _point2Term = point2Term;
        _point3Term = point3Term;
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

        CreatedObject = new Triangle(point1, point2, point3);
    }
}
