using Lex.Clauses;
using Lex.Tokens;
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

        HandleEntryClause(resolver, clause, clause =>
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
                case "egg":
                    resolver.SurfaceResolvers.Add(ParseEggClause(clause));
                    break;
                case "superellipsoid":
                    resolver.SurfaceResolvers.Add(ParseSuperellipsoidClause(clause));
                    break;
                case "extrusion":
                    resolver.SurfaceResolvers.Add(ParseExtrusionClause(clause));
                    break;
                case "lathe":
                    resolver.SurfaceResolvers.Add(ParseLatheClause(clause));
                    break;
                case "blob":
                    resolver.SurfaceResolvers.Add(ParseBlobClause(clause));
                    break;
                case "tube":
                    resolver.SurfaceResolvers.Add(ParseTubeClause(clause));
                    break;
                case "sweep":
                    resolver.SurfaceResolvers.Add(ParseSweepClause(clause));
                    break;
                case "text":
                    resolver.SurfaceResolvers.Add(ParseTextClause(clause));
                    break;
                // ReSharper disable once StringLiteralTypo
                case "lsystem":
                    resolver.SurfaceResolvers.Add(ParseLSystemClause(clause));
                    break;
                case "heightField":
                    resolver.SurfaceResolvers.Add(ParseHeightFieldClause(clause));
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
                case "disc":
                    resolver.SurfaceResolvers.Add(ParseDiscClause(clause));
                    break;
                case "genericShape":
                    resolver.SurfaceResolvers.Add(ParseGenericShapeClause(clause));
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
                case "surface":
                    HandleSurfaceClause(clause, resolver, "group");
                    break;
                default:
                    throw new Exception($"Internal error: unknown {clause.Tag} property found on a group object.");
            }
        });
    }

    /// <summary>
    /// This method is used to parse the given clause into a group interval.
    /// </summary>
    /// <param name="clause">The clause to parse.</param>
    /// <returns>The group interval.</returns>
    private static GroupInterval CreateGroupInterval(Clause clause)
    {
        ClauseReader reader = clause.Reader();
        string variableName = null;
        Token token = reader.PeekToken();

        if (!BounderToken.LeftParen.Matches(token) && !BounderToken.OpenBracket.Matches(token))
        {
            variableName = reader.NextText();

            reader.NextToken(); // The assignment operator.
        }

        Token startToken = reader.NextToken();
        bool startIsOpen = BounderToken.LeftParen.Matches(startToken);
        Term startTerm = (Term) reader.NextExpression();

        reader.NextToken(); // The comma.

        Term endTerm = (Term) reader.NextExpression();
        Token endToken = reader.NextToken();
        bool endIsOpen = BounderToken.RightParen.Matches(endToken);
        Term stepTerm = null;

        if (reader.HasMoreTokens)
        {
            reader.NextToken(); // The "by" keyword.

            stepTerm = (Term) reader.NextExpression();
        }

        return new GroupInterval(variableName, startTerm, endTerm, stepTerm, startIsOpen, endIsOpen);
    }
}
