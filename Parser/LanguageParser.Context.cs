using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Instructions;
using RayTracer.Scanners;

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
        ParseBlock("contextEntryClause");
    }

    /// <summary>
    /// This method is used to handle the scanner clause of a context block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleScannerClause(Clause clause)
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

        _context.InstructionContext.AddInstruction(instruction);
    }

    /// <summary>
    /// This method is used to handle the angles clause of a context block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleAnglesClause(Clause clause)
    {
        bool isRadians = clause.Tokens[2].Text == "radians";
        Instruction instruction = new SetContextPropertyInstruction<bool>(
            context => context.AnglesAreRadians, isRadians);

        _context.InstructionContext.AddInstruction(instruction);
    }

    /// <summary>
    /// This method is used to handle an item clause of a context block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleContextPropertyClause(Clause clause)
    {
        string field = clause.Tokens[0].Text;
        Term term = (Term) clause.Expressions.First();

        Instruction instruction = field switch
        {
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
            _ => throw new Exception($"Internal error: unknown context field found: {field}.")
        };

        _context.InstructionContext.AddInstruction(instruction);
    }

    /// <summary>
    /// This method is used to handle the beginning of an info block.
    /// </summary>
    private void HandleStartInfoClause()
    {
        ParseBlock("infoEntryClause");
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
            "location" => new SetInfoPropertyInstruction<string>(
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
