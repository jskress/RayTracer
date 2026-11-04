using Lex.Clauses;
using Lex.Parser;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL: here a grid laid with tiles by wave function
/// collapse.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle the beginning of a grid of tiles.
    /// </summary>
    private void HandleStartTilesClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Tiles");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseTilesClause(clause)
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a grid of tiles.
    /// </summary>
    private TileGridResolver ParseTilesClause(Clause clause)
    {
        TileGridResolver resolver = GetSurfaceResolver(
            clause, () => ParseObjectResolver<TileGridResolver>("tilesEntryClause", HandleTilesEntryClause),
            "tilesEntryClause", HandleTilesEntryClause);

        resolver.ErrorToken ??= clause.Tokens[0];

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a grid of tiles.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleTilesEntryClause(Clause clause)
    {
        TileGridResolver resolver = (TileGridResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "size":
                    resolver.SizeResolver = new TermResolver<Basics.Vector> { Term = clause.Term() };
                    break;
                case "cell":
                    resolver.CellResolver = new CellSizeResolver
                    {
                        Term = clause.Term(),
                        Validator = size => size.X > 0 && size.Y > 0 && size.Z > 0
                            ? null
                            : "A cell must be some size every way."
                    };
                    break;
                case "tile":
                    resolver.Tiles.Add(ParseTile(clause));
                    break;
                case "edge":
                    resolver.EdgeResolver = new TermResolver<string> { Term = clause.Term() };
                    break;
                case "above":
                    resolver.AboveResolver = new TermResolver<string> { Term = clause.Term() };
                    break;
                case "below":
                    resolver.BelowResolver = new TermResolver<string> { Term = clause.Term() };
                    break;
                case "fit":
                    resolver.Fits.Add((new TermResolver<string> { Term = clause.Term() },
                                       new TermResolver<string> { Term = clause.Term(1) }));
                    break;
                case "tries":
                    resolver.TriesResolver = new TermResolver<int>
                    {
                        Term = clause.Term(),
                        Validator = tries => tries >= 1 ? null : "A grid of tiles must be tried at least once."
                    };
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "tiles");
                    break;
            }
        });
    }

    /// <summary>
    /// This method reads one tile, its brace already read: what stands in it -- a named surface, a call of a
    /// primitive, or nothing -- what lies on its sides, whether it turns, and how often it is drawn.
    /// </summary>
    private TileSpec ParseTile(Clause opening)
    {
        TileSpec tile = new ();
        bool standing = false;

        Lex.Tokens.Token closing = ParseBlock("tileEntryClause", clause =>
        {
            string text = clause.Tokens[0].Text;
            int side = text switch
            {
                "right" => 0, "back" => 1, "left" => 2, "front" => 3, "top" => 4, "bottom" => 5,
                "sides" => -2, _ => -1
            };

            if (side >= 0 || side == -2)
            {
                TermResolver<string> label = new () { Term = clause.Term() };

                if (side >= 0)
                    tile.Sides[side] = label;
                else
                {
                    for (int around = 0; around < 4; around++)
                        tile.Sides[around] = label;
                }

                return;
            }

            switch (text)
            {
                case "turns":
                    tile.Turns = true;
                    return;
                case "weight":
                    tile.WeightResolver = new TermResolver<double>
                    {
                        Term = clause.Term(),
                        Validator = weight => weight > 0 ? null : "A tile's weight must be more than nought."
                    };
                    return;
            }

            if (standing)
            {
                throw new TokenException("A tile holds one thing; this is a second.") { Token = clause.Tokens[0] };
            }

            standing = true;

            if (text != "nothing")
            {
                tile.Surface = clause.Tokens.Count > 1
                    ? ParseCallOf(clause.Tokens[0])
                    : GetExtensibleItem<ISurfaceResolver>(clause.Tokens[0], false);
            }
        });

        if (!standing)
        {
            throw new TokenException(
                "A tile must say what stands in it: a surface's name, a call of a primitive, or \"nothing\".")
            {
                Token = closing ?? opening.Tokens[0]
            };
        }

        return tile;
    }
}
