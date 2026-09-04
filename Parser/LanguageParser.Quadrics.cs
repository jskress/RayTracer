using Lex.Clauses;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a paraboloid block.
    /// </summary>
    private void HandleStartParaboloidClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Paraboloid");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseParaboloidClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a paraboloid block.
    /// </summary>
    private ParaboloidResolver ParseParaboloidClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<ParaboloidResolver>(
                "extrudedSurfaceEntryClause", HandleParaboloidEntryClause),
            "extrudedSurfaceEntryClause", HandleParaboloidEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a paraboloid block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleParaboloidEntryClause(Clause clause)
    {
        ParaboloidResolver resolver = (ParaboloidResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause,
            clause => HandleExtrudedSurfaceClause(clause, resolver, "paraboloid"));
    }

    /// <summary>
    /// This method is used to handle the beginning of a hyperboloid block.
    /// </summary>
    private void HandleStartHyperboloidClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Hyperboloid");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseHyperboloidClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a hyperboloid block.
    /// </summary>
    private HyperboloidResolver ParseHyperboloidClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<HyperboloidResolver>(
                "extrudedSurfaceEntryClause", HandleHyperboloidEntryClause),
            "extrudedSurfaceEntryClause", HandleHyperboloidEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a hyperboloid block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleHyperboloidEntryClause(Clause clause)
    {
        HyperboloidResolver resolver = (HyperboloidResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause,
            clause => HandleExtrudedSurfaceClause(clause, resolver, "hyperboloid"));
    }

    /// <summary>
    /// This method is used to handle the beginning of a saddle block.
    /// </summary>
    private void HandleStartSaddleClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Saddle");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseSaddleClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a saddle block.
    /// </summary>
    private SaddleResolver ParseSaddleClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<SaddleResolver>(
                "saddleEntryClause", HandleSaddleEntryClause),
            "saddleEntryClause", HandleSaddleEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a saddle block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSaddleEntryClause(Clause clause)
    {
        SaddleResolver resolver = (SaddleResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "width":
                    resolver.WidthResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                case "depth":
                    resolver.DepthResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "saddle");
                    break;
            }
        });
    }

    /// <summary>
    /// This method is used to handle the beginning of a quadric block.
    /// </summary>
    private void HandleStartQuadricClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Quadric");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseQuadricClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a quadric block.
    /// </summary>
    private QuadricResolver ParseQuadricClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<QuadricResolver>(
                "quadricEntryClause", HandleQuadricEntryClause),
            "quadricEntryClause", HandleQuadricEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a quadric block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleQuadricEntryClause(Clause clause)
    {
        QuadricResolver resolver = (QuadricResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "squares":
                    resolver.SquaresResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "cross":
                    resolver.CrossResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "linear":
                    resolver.LinearResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "constant":
                    resolver.ConstantResolver = new TermResolver<double> { Term = clause.Term() };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "quadric");
                    break;
            }
        });
    }
}
