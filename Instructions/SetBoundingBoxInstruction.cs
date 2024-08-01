using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to set the bounding box attribute on a group.
/// </summary>
public class SetBoundingBoxInstruction : ObjectInstruction<Group>
{
    private readonly Term _firstTerm;
    private readonly Term _secondTerm;

    public SetBoundingBoxInstruction(Term firstTerm, Term secondTerm)
    {
        _firstTerm = firstTerm;
        _secondTerm = secondTerm;
    }

    /// <summary>
    /// This method is used to execute the instruction.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override void Execute(RenderContext context, Variables variables)
    {
        Point point1 = (Point) _firstTerm.GetValue(variables, typeof(Point));
        Point point2 = (Point) _secondTerm.GetValue(variables, typeof(Point));

        if (point1 == null)
        {
            throw new TokenException("Could not resolve this to a point.")
            {
                Token = _firstTerm.ErrorToken
            };
        }

        if (point2 == null)
        {
            throw new TokenException("Could not resolve this to a point.")
            {
                Token = _secondTerm.ErrorToken
            };
        }

        Target.BoundingBox = new BoundingBox(point1, point2);
    }
}
