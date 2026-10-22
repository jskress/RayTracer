using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a fracture block.
    /// </summary>
    /// <param name="clause">The clause that starts the fracture.</param>
    private void HandleStartFractureClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Fracture");

        FractureResolver resolver = ParseFractureClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a fracture block.
    /// </summary>
    /// <param name="clause">The clause that starts the fracture.</param>
    private FractureResolver ParseFractureClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<FractureResolver>(
                "fractureEntryClause", HandleFractureEntryClause),
            "fractureEntryClause", HandleFractureEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a fracture block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleFractureEntryClause(Clause clause)
    {
        FractureResolver resolver = (FractureResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "solid":
                    resolver.SolidResolver = ParseSurfaceAfterAWord(clause);
                    break;
                case "pieces":
                    resolver.PiecesResolver = new TermResolver<int>
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 1 ? null : "A fracture must break into at least one piece."
                    };
                    break;
                case "gap":
                    resolver.GapResolver = NoLessThanNought(clause, "gap");
                    break;
                case "rounded":
                    resolver.RoundedResolver = NoLessThanNought(clause, "rounding");
                    break;
                case "flat":
                    resolver.FlatResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "stretched":
                    resolver.StretchedResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "toward":
                    resolver.TowardResolver = new TermResolver<Point> { Term = clause.Term() };
                    break;
                case "hole":
                    resolver.HoleResolver = NoLessThanNought(clause, "hole");
                    break;
                case "apart":
                    resolver.ApartResolver = NoLessThanNought(clause, "apart");
                    break;
                case "tumble":
                    // In the scene's own unit of angle, as a rotation is.
                    resolver.TumbleResolver = new AngleResolver
                    {
                        Term = clause.Term(),
                        Validator = value => value >= 0 ? null : "A fracture's tumble may not be less than nought."
                    };
                    break;
                case "loose":
                    resolver.LooseResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "whole":
                    resolver.WholeResolver = new LiteralResolver<bool> { Value = true };
                    break;
                case "materials":
                    // One or more, after commas: each a material written out, a name, or a primitive's parameter.
                    List<Resolver<Material>> materials = [ParseBlendedMaterial()];

                    while (CurrentParser.IsNext(OperatorToken.Comma))
                    {
                        CurrentParser.GetNextToken();
                        materials.Add(ParseBlendedMaterial());
                    }

                    resolver.MaterialResolvers = materials;
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "fracture");
                    break;
            }
        });
    }

    private static TermResolver<double> NoLessThanNought(Clause clause, string what)
    {
        return new TermResolver<double>
        {
            Term = clause.Term(),
            Validator = value => value >= 0 ? null : $"A fracture's {what} may not be less than nought."
        };
    }
}
