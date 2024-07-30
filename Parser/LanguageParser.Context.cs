using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Instructions;
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
        ParseBlock("contextEntryClause", HandleContextClauseEntry);
    }

    /// <summary>
    /// This method handles a single entry in the context block.
    /// </summary>
    /// <param name="clause">The clause that represents the entry.</param>
    private void HandleContextClauseEntry(Clause clause)
    {
        string field = ToCmd(clause);

        if (field == "info")
        {
            ParseBlock("infoEntryClause", HandleInfoEntryClause);

            return;
        }

        Term term = (Term) clause.Expressions.FirstOrDefault();

        Instruction instruction = field switch
        {
            "serial" => CreateScannerClauseInstruction(clause),
            "parallel" => CreateScannerClauseInstruction(clause),
            "angles" => CreateAnglesClauseInstruction(clause),
            "apply.gamma" => new SetContextPropertyInstruction<bool>(
                target => target.ApplyGamma, true),
            "no.gamma" => new SetContextPropertyInstruction<bool>(
                target => target.ApplyGamma, false),
            "no.shadows" => new SetContextPropertyInstruction<bool>(
                target => target.SuppressAllShadows, true),
            "report" => new SetContextPropertyInstruction<bool>(
                target => target.ReportGamma, true),
            "width" => new SetContextPropertyInstruction<int>(
                target => target.Width, term,
                value => value is < 1 or > 16384
                    ? "Width must be between 1 and 16,384."
                    : null
            ),
            "height" => new SetContextPropertyInstruction<int>(
                target => target.Height, term,
                value => value is < 1 or > 16384
                    ? "Height must be between 1 and 16,384."
                    : null
            ),
            "gamma" => new SetContextPropertyInstruction<double>(
                target => target.Gamma, term,
                value => value is < 0 or > 5
                    ? "Gamma correction must be between 0 and 5."
                    : null
            ),
            _ => throw new Exception($"Internal error: unknown context property found: {field}.")
        };

        _context.InstructionContext.AddInstruction(instruction);
    }

    /// <summary>
    /// This method is used to create the appropriate instruction for the scanner clause
    /// of a context block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <returns>The created instruction.</returns>
    private static Instruction CreateScannerClauseInstruction(Clause clause)
    {
        Token first = clause.Tokens[0];
        Token second = clause.Tokens[1];
        Instruction instruction;

        if (first.Text == "serial")
            instruction = new SetScannerInstruction(() => new SingleThreadScanner());
        else switch (second.Text)
        {
            case "line":
                instruction = new SetScannerInstruction(() => new LineParallelScanner());
                break;
            case "pixel":
                instruction = new SetScannerInstruction(() => new PixelParallelScanner());
                break;
            default:
            {
                string line = string.Join(' ', clause.Tokens.Select(token => token.Text));

                throw new Exception($"Internal error: could not interpret scanner specification: {line}");
            }
        }

        return instruction;
    }

    /// <summary>
    /// This method is used to create the appropriate instruction for the angles clause of
    /// a context block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    /// <returns>The created instruction.</returns>
    private static Instruction CreateAnglesClauseInstruction(Clause clause)
    {
        bool isRadians = clause.Tokens[2].Text == "radians";

        return new SetContextPropertyInstruction<bool>(
            context => context.AnglesAreRadians, isRadians);
    }

    /// <summary>
    /// This method is used to handle an item clause of an info block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleInfoEntryClause(Clause clause)
    {
        string field = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions.First();

        Instruction instruction = field switch
        {
            "title" => new SetInfoPropertyInstruction<string>(
                target => target.Title, term),
            "author" => new SetInfoPropertyInstruction<string>(
                target => target.Author, term),
            "description" => new SetInfoPropertyInstruction<string>(
                target => target.Description, term),
            "copyright" => new SetCopyrightInstruction(term),
            "software" => new SetInfoPropertyInstruction<string>(
                target => target.Software, term),
            "disclaimer" => new SetInfoPropertyInstruction<string>(
                target => target.Disclaimer, term),
            "warning" => new SetInfoPropertyInstruction<string>(
                target => target.Warning, term),
            "source" => new SetInfoPropertyInstruction<string>(
                target => target.Source, term),
            "comment" => new AppendInfoStringPropertyInstruction(
                target => target.Comment, term),
            _ => throw new Exception($"Internal error: unknown info field found: {field}.")
        };

        _context.InstructionContext.AddInstruction(instruction);
    }
}
