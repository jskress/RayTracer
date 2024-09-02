using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Context;
using RayTracer.Scanners;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a context block.
    /// </summary>
    private void HandleStartContextClause()
    {
        ContextUpdater updater = new();
        
        _context.PushTarget(updater);

        ParseBlock("contextEntryClause", HandleContextClauseEntry);
        
        _context.PopTarget();

        _context.InstructionContext.AddInstruction(updater);
    }

    /// <summary>
    /// This method handles a single entry in the context block.
    /// </summary>
    /// <param name="clause">The clause that represents the entry.</param>
    private void HandleContextClauseEntry(Clause clause)
    {
        ContextUpdater updater = (ContextUpdater) _context.CurrentTarget;
        string field = ToCmd(clause);

        if (field == "info")
        {
            HandleStartImageInfoClause(updater);

            return;
        }

        Term term = clause.Term();

        switch (field)
        {
            case "serial":
            case "parallel":
                CreateScannerResolver(clause, updater);
                break;
            case "angles":
                CreateAnglesAreRadiansResolver(clause, updater);
                break;
            case "apply.gamma":
                updater.ApplyGammaResolver = new LiteralResolver<bool> { Value = true };
                break;
            case "no.gamma":
                updater.ApplyGammaResolver = new LiteralResolver<bool> { Value = false };
                break;
            case "no.shadows":
                updater.SuppressAllShadowsResolver = new LiteralResolver<bool> { Value = true };
                break;
            case "report":
                updater.ReportGammaResolver = new LiteralResolver<bool> { Value = true };
                break;
            case "width":
                updater.WidthResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value is < 1 or > 16384 ? "Width must be between 1 and 16,384." : null
                };
                break;
            case "height":
                updater.HeightResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value is < 1 or > 16384 ? "Height must be between 1 and 16,384." : null
                };
                break;
            case "gamma":
                updater.GammaResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = value => value is < 0 or > 5 ? "Gamma correction must be between 0 and 5." : null
                };
                break;
            default:
                throw new Exception($"Internal error: unknown context property found: {field}.");
        }
    }

    /// <summary>
    /// This method is used to handle the beginning of an image information block.
    /// </summary>
    private void HandleStartImageInfoClause(ContextUpdater contextUpdater)
    {
        contextUpdater.ImageInfoUpdater ??= new ImageInfoUpdater();

        _context.PushTarget(contextUpdater.ImageInfoUpdater);

        ParseBlock("infoEntryClause", HandleInfoEntryClause);

        _context.PopTarget();
    }

    /// <summary>
    /// This method is used to create the appropriate resolver for the scanner clause
    /// of a context block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="contextUpdater">The context updater to add the resolver to.</param>
    private static void CreateScannerResolver(Clause clause, ContextUpdater contextUpdater)
    {
        Token first = clause.Tokens[0];
        Token second = clause.Tokens[1];
        Func<IScanner> creator;

        if (first.Text == "serial")
            creator = () => new SingleThreadScanner();
        else switch (second.Text)
        {
            case "line":
                creator = () => new LineParallelScanner();
                break;
            case "pixel":
                creator = () => new PixelParallelScanner();
                break;
            default:
            {
                string line = string.Join(' ', clause.Tokens.Select(token => token.Text));

                throw new Exception($"Internal error: could not interpret scanner specification: {line}");
            }
        }

        contextUpdater.ScannerResolver = new ScannerResolver { ScannerFactory = creator };
    }

    /// <summary>
    /// This method is used to create the appropriate instruction for the angles clause of
    /// a context block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <param name="contextUpdater">The context updater to add the resolver to.</param>
    private static void CreateAnglesAreRadiansResolver(Clause clause, ContextUpdater contextUpdater)
    {
        contextUpdater.AnglesAreRadiansResolver = new LiteralResolver<bool>
        {
            Value = clause.Text(2) == "radians"
        };
    }

    /// <summary>
    /// This method is used to handle an item clause of an info block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleInfoEntryClause(Clause clause)
    {
        ImageInfoUpdater updater = (ImageInfoUpdater) _context.CurrentTarget;
        string field = clause.Text();
        Term term = clause.Term();

        switch (field)
        {
            case "title":
                updater.TitleResolver = new TermResolver<string> { Term = term };
                break;
            case "author":
                updater.AuthorResolver = new TermResolver<string> { Term = term };
                break;
            case "description":
                updater.DescriptionResolver = new TermResolver<string> { Term = term };
                break;
            case "copyright":
                updater.CopyrightResolver = new CopyrightResolver { Term = term };
                break;
            case "software":
                updater.SoftwareResolver = new TermResolver<string> { Term = term };
                break;
            case "disclaimer":
                updater.DisclaimerResolver = new TermResolver<string> { Term = term };
                break;
            case "warning":
                updater.WarningResolver = new TermResolver<string> { Term = term };
                break;
            case "source":
                updater.SourceResolver = new TermResolver<string> { Term = term };
                break;
            case "comment":
                updater.CommentResolver = new CommentResolver { Term = term };
                break;
            default:
                throw new Exception($"Internal error: unknown info field found: {field}.");
        }
    }
}
