using Lex.Clauses;
using RayTracer.Extensions;
using RayTracer.Fonts;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces.Extrusions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a text block.
    /// </summary>
    /// <param name="clause">The clause that starts the text block.</param>
    private void HandleStartTextClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Text");

        TextSolidResolver resolver = ParseTextClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create a text resolver from a text block.
    /// </summary>
    /// <param name="clause">The clause that starts the text.</param>
    private TextSolidResolver ParseTextClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<TextSolidResolver>(
                "textEntryClause", HandleTextEntryClause),
            "textEntryClause", HandleTextEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a text block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTextEntryClause(Clause clause)
    {
        TextSolidResolver resolver = (TextSolidResolver) _context.CurrentTarget;

        if (clause == null) // We must have hit a transform property...
            resolver.TransformResolver = ParseTransformClause();
        else
        {
            switch (ToCmd(clause))
            {
                case "text":
                    resolver.TextResolver = new TermResolver<string> { Term = clause.Term() };
                    break;
                case "font":
                    ParseTextFontClause(resolver, clause);
                    break;
                case "layout":
                    resolver.LayoutSettingsResolver = ParseTextLayoutSettingsClause();
                    break;
                case "kerning":
                    resolver.KerningResolver = ParseKerningClause(clause);
                    break;
                case "open":
                    resolver.ClosedResolver = new LiteralResolver<bool> { Value = false };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "extrusion");
                    break;
            }
        }
    }

    /// <summary>
    /// This method is used to parse a font specification clause.
    /// </summary>
    /// <param name="resolver">The resolver to update.</param>
    /// <param name="clause">The font specification clause to parse.</param>
    private static void ParseTextFontClause(TextSolidResolver resolver, Clause clause)
    {
        string text = clause.Text(1);

        resolver.FontFamilyNameResolver = new TermResolver<string> { Term = clause.Term() };

        if (text != string.Empty && text != "italic")
        {
            resolver.FontWeightResolver = new LiteralResolver<FontWeight>
            {
                Value = Enum.Parse<FontWeight>(text, true)
            };

            text = clause.Text(2);
        }

        if (text == "italic")
            resolver.IsItalicResolver = new LiteralResolver<bool> { Value = true };
    }

    /// <summary>
    /// This method is used to create the resolver for a text layout settings block.
    /// </summary>
    private TextLayoutSettingsResolver ParseTextLayoutSettingsClause()
    {
        TextLayoutSettingsResolver resolver = new TextLayoutSettingsResolver();
        
        ParseObjectResolver("textLayoutEntryClause", HandleTextLayoutClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a text layout block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTextLayoutClause(Clause clause)
    {
        TextLayoutSettingsResolver resolver = (TextLayoutSettingsResolver) _context.CurrentTarget;
        string field = clause.Text();

        switch (field)
        {
            case "text":
                resolver.TextAlignmentResolver = GetTextAlignmentResolver(clause);
                break;
            case "horizontal":
                resolver.HorizontalPositionResolver = GetHorizontalPositionResolver(clause);
                break;
            case "vertical":
                resolver.VerticalPositionResolver = GetVerticalPositionResolver(clause);
                break;
            case "line":
                resolver.LineGapResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            default:
                throw new Exception($"Internal error: unknown text layout property found: {field}.");
        }
    }

    /// <summary>
    /// This method is used to create the appropriate text alignment resolver for the
    /// given clause.
    /// </summary>
    /// <param name="clause">The text alignment clause to interpret.</param>
    /// <returns>The appropriate text alignment resolver.</returns>
    private static Resolver<TextAlignment> GetTextAlignmentResolver(Clause clause)
    {
        Term term = clause.Term();

        if (term != null)
            return new TermResolver<TextAlignment> { Term = term };

        TextAlignment alignment = Enum.Parse<TextAlignment>(clause.Text(2), true);

        return new LiteralResolver<TextAlignment> { Value = alignment };
    }

    /// <summary>
    /// This method is used to create the appropriate horizontal position resolver for the
    /// given clause.
    /// </summary>
    /// <param name="clause">The horizontal position clause to interpret.</param>
    /// <returns>The appropriate horizontal position resolver.</returns>
    private static Resolver<HorizontalPosition> GetHorizontalPositionResolver(Clause clause)
    {
        Term term = clause.Term();

        if (term != null)
            return new TermResolver<HorizontalPosition> { Term = term };

        HorizontalPosition position = Enum.Parse<HorizontalPosition>(clause.Text(2), true);

        return new LiteralResolver<HorizontalPosition> { Value = position };
    }

    /// <summary>
    /// This method is used to create the appropriate vertical position resolver for the
    /// given clause.
    /// </summary>
    /// <param name="clause">The vertical position clause to interpret.</param>
    /// <returns>The appropriate vertical position resolver.</returns>
    private static Resolver<VerticalPosition> GetVerticalPositionResolver(Clause clause)
    {
        Term term = clause.Term();

        if (term != null)
            return new TermResolver<VerticalPosition> { Term = term };

        VerticalPosition position = Enum.Parse<VerticalPosition>(clause.Text(2), true);

        return new LiteralResolver<VerticalPosition> { Value = position };
    }

    /// <summary>
    /// This method is used to parse the given kerning clause into a kerning resolver.
    /// </summary>
    /// <param name="clause">The clause to parse.</param>
    /// <returns>The resulting kerning resolver.</returns>
    private static KerningResolver ParseKerningClause(Clause clause)
    {
        KerningResolver resolver = new KerningResolver();

        // The terms will be in sets of three, each representing a single pair.
        for (int index = 0; index < clause.Expressions.Count; index += 2)
        {
            Term left = clause.Term(index);
            Term right = clause.Term(index + 1);
            Term kern = clause.Term(index + 2);
            
            resolver.PairResolvers.Add(new KerningPairResolver
            {
                LeftCharacterResolver = new TermResolver<string>{ Term = left },
                RightCharacterResolver = new TermResolver<string>{ Term = right },
                KernCharacterResolver = new TermResolver<short>{ Term = kern }
            });
        }

        return resolver;
    }
}
