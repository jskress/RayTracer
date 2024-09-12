using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a group block.
    /// </summary>
    private void HandleStartGroupClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Group");

        GroupResolver resolver = ParseGroupClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a group block.
    /// </summary>
    /// <param name="clause">The clause that started the group.</param>
    private GroupResolver ParseGroupClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<GroupResolver>(
                "groupEntryClause", HandleGroupEntryClause),
            "groupEntryClause", HandleGroupEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a group block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleGroupEntryClause(Clause clause)
    {
        GroupResolver resolver = (GroupResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
        {
            switch (clause.Tag)
            {
                case "interval":
                    resolver.GroupInterval = CreateGroupInterval(clause);
                    break;
                case "plane":
                    resolver.SurfaceResolvers.Add(ParsePlaneClause(clause));
                    break;
                case "sphere":
                    resolver.SurfaceResolvers.Add(ParseSphereClause(clause));
                    break;
                case "cube":
                    resolver.SurfaceResolvers.Add(ParseCubeClause(clause));
                    break;
                case "cylinder":
                    resolver.SurfaceResolvers.Add(ParseCylinderClause(clause));
                    break;
                case "conic":
                    resolver.SurfaceResolvers.Add(ParseConicClause(clause));
                    break;
                case "torus":
                    resolver.SurfaceResolvers.Add(ParseTorusClause(clause));
                    break;
                case "triangle":
                    resolver.SurfaceResolvers.Add(ParseTriangleClause(clause));
                    break;
                case "smoothTriangle":
                    resolver.SurfaceResolvers.Add(ParseSmoothTriangleClause(clause));
                    break;
                case "parallelogram":
                    resolver.SurfaceResolvers.Add(ParseParallelogramClause(clause));
                    break;
                case "objectFile":
                    resolver.SurfaceResolvers.Add(ParseObjectFileClause(clause));
                    break;
                case "object":
                    resolver.SurfaceResolvers.Add(GetSurfaceResolver(clause));
                    break;
                case "csg":
                    resolver.SurfaceResolvers.Add(ParseCsgClause(clause));
                    break;
                case "group":
                    resolver.SurfaceResolvers.Add(ParseGroupClause(clause));
                    break;
                case "boundingBox":
                    resolver.BoundingBoxResolver = new BoundingBoxResolver
                    {
                        FirstPointResolver = new TermResolver<Point>() { Term = clause.Term() },
                        SecondPointResolver = new TermResolver<Point>() { Term = clause.Term(1) }
                    };
                    break;
                case "surface":
                    HandleSurfaceClause(clause, resolver, "CSG object");
                    break;
                default:
                    throw new Exception($"Internal error: unknown {clause.Tag} property found on a group object.");
            }
        }
    }

    /// <summary>
    /// This method is used to parse the given clause into a group interval.
    /// </summary>
    /// <param name="clause">The clause to parse.</param>
    /// <returns>The group interval.</returns>
    private GroupInterval CreateGroupInterval(Clause clause)
    {
        string variableName = clause.Text(1) == "="
            ? clause.Text()
            : null;
        Term stepTerm = null;

        if (variableName != null)
            clause.Tokens.RemoveRange(0, 2);
        
        Token token = clause.Tokens.First();
        Term startTerm = clause.Term();
        Term endTerm = clause.Term(1);
        bool startIsOpen = BounderToken.LeftParen.Matches(token);
        bool endIsOpen = BounderToken.LeftParen.Matches(clause.Tokens[2]);

        clause.Expressions.RemoveRange(0, 2);
        clause.Tokens.RemoveRange(0, 3);

        if (clause.Text() == "by")
        {
            stepTerm = clause.Term();

            clause.Expressions.RemoveFirst();
            clause.Tokens.RemoveFirst();
        }

        return new GroupInterval(variableName, startTerm, endTerm, stepTerm, startIsOpen, endIsOpen);
    }
}
