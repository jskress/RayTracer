using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Surfaces.Shapes;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    // The words a rule's action may begin with.  Anything else that begins one is the name of a rule.
    private static readonly HashSet<string> ShapeActionWords =
    [
        "extrude", "split", "repeat", "faces", "move", "size", "roof", "fill", "place", "choose", "if", "nothing"
    ];

    private static readonly HashSet<string> ShapeFaceWords = ["front", "back", "left", "right", "sides", "top", "bottom"];

    // How many choices the grammars read so far have held, so each has a number of its own.
    private int _shapeChoices;

    /// <summary>
    /// This method is used to handle the beginning of a shape grammar block.
    /// </summary>
    /// <param name="clause">The clause that starts the grammar.</param>
    private void HandleStartShapesClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Shape grammar");

        ShapeGrammarResolver resolver = ParseShapesClause(clause);

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = resolver
        });
    }

    /// <summary>
    /// This method is used to create the instruction set from a shape grammar block.
    /// </summary>
    /// <param name="clause">The clause that starts the grammar.</param>
    private ShapeGrammarResolver ParseShapesClause(Clause clause)
    {
        return GetSurfaceResolver(
            clause, () => ParseObjectResolver<ShapeGrammarResolver>(
                "shapesEntryClause", HandleShapesEntryClause),
            "shapesEntryClause", HandleShapesEntryClause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a shape grammar block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleShapesEntryClause(Clause clause)
    {
        ShapeGrammarResolver resolver = (ShapeGrammarResolver) _context.CurrentTarget;

        HandleEntryClause(resolver, clause, clause =>
        {
            switch (clause.Text())
            {
                case "axiom":
                    resolver.Axiom = clause.Tokens[1].Text;
                    break;
                case "size":
                    resolver.SizeResolver = new TermResolver<Vector> { Term = clause.Term() };
                    break;
                case "rules":
                    resolver.Rules = ParseShapeRules(clause.Tokens[0]);
                    break;
                default:
                    HandleSurfaceClause(clause, resolver, "shape grammar");
                    break;
            }
        });
    }

    /// <summary>
    /// This method reads a grammar's rules, its open brace already read: each a name, an arrow, and what it does,
    /// up to the next name and arrow or the close brace.  Every rule a rule hands a scope to must be among them.
    /// </summary>
    private List<ShapeRule> ParseShapeRules(Token start)
    {
        List<ShapeRule> rules = [];
        List<Token> named = [];

        while (!CurrentParser.IsNext(BounderToken.CloseBrace))
        {
            Token name = CurrentParser.GetNextToken();

            if (name is not (IdToken or KeywordToken) || !IsArrow(CurrentParser.PeekNextToken()))
                throw new TokenException("Expecting the name of a rule and an arrow here.") { Token = name ?? start };

            CurrentParser.GetNextToken();

            if (rules.Any(rule => rule.Name == name.Text))
                throw new TokenException($"The rule '{name.Text}' is written twice.") { Token = name };

            rules.Add(new ShapeRule { Name = name.Text, Actions = ParseShapeActions(true, named) });
        }

        CurrentParser.GetNextToken();

        foreach (Token token in named.Where(token => rules.All(rule => rule.Name != token.Text)))
            throw new TokenException($"There is no rule named '{token.Text}'.") { Token = token };

        return rules;
    }

    private static bool IsArrow(Token token) => token is OperatorToken { Text: "->" };

    /// <summary>
    /// This method reads actions until a close brace, or, for a rule's own, until the next rule begins.
    /// </summary>
    private List<ShapeAction> ParseShapeActions(bool ownRule, List<Token> named)
    {
        List<ShapeAction> actions = [];

        while (true)
        {
            Token next = CurrentParser.PeekNextToken();

            if (next is null || BounderToken.CloseBrace.Matches(next))
                return actions;

            if (next is not (IdToken or KeywordToken))
                throw new TokenException("Expecting an action or the name of a rule here.") { Token = next };

            if (ownRule && !ShapeActionWords.Contains(next.Text))
            {
                // A name followed by an arrow is the next rule, not a scope handed on.
                Token name = CurrentParser.GetNextToken();
                bool isNextRule = IsArrow(CurrentParser.PeekNextToken());

                CurrentParser.ReturnToken(name);

                if (isNextRule)
                    return actions;
            }

            ShapeAction action = ParseShapeAction(named);

            if (action is not null)
                actions.Add(action);
        }
    }

    // A scope handed on: a rule's name, or a block of actions.
    private List<ShapeAction> ParseShapeTarget(List<Token> named)
    {
        if (CurrentParser.IsNext(BounderToken.OpenBrace))
        {
            CurrentParser.GetNextToken();

            List<ShapeAction> actions = ParseShapeActions(false, named);

            CurrentParser.MatchToken(true, () => "Expecting a close brace here.", BounderToken.CloseBrace);

            return actions;
        }

        Token name = CurrentParser.GetNextToken();

        if (name is not (IdToken or KeywordToken) || ShapeActionWords.Contains(name.Text))
            throw new TokenException("Expecting the name of a rule, or actions in braces, here.") { Token = name };

        named.Add(name);

        return [new ShapeSymbol { Name = name.Text }];
    }

    private ShapeAction ParseShapeAction(List<Token> named)
    {
        Token word = CurrentParser.GetNextToken();

        switch (word.Text)
        {
            case "extrude":
                return new ShapeExtrude { Height = ShapeNumber(), Then = ParseShapeTarget(named) };
            case "split":
            {
                int axis = ShapeAxis();
                List<(Resolver<double>, bool, List<ShapeAction>)> parts = [];

                CurrentParser.MatchToken(true, () => "Expecting an open brace to follow the axis here.", BounderToken.OpenBrace);

                while (!CurrentParser.IsNext(BounderToken.CloseBrace))
                {
                    bool share = CurrentParser.IsNext(OperatorToken.Tilde);

                    if (share)
                        CurrentParser.GetNextToken();

                    parts.Add((ShapeNumber(), share, ParseShapeTarget(named)));
                }

                CurrentParser.GetNextToken();

                return new ShapeSplit { Axis = axis, Parts = parts };
            }
            case "repeat":
            {
                int axis = ShapeAxis();
                Resolver<double> size = ShapeNumber();
                string index = null;

                if (CurrentParser.PeekNextToken() is { Text: "index" })
                {
                    CurrentParser.GetNextToken();
                    CurrentParser.MatchToken(true, () => "Expecting \"in\" to follow \"index\" here.", new KeywordToken("in"));
                    index = CurrentParser.GetNextToken().Text;
                }

                return new ShapeRepeat { Axis = axis, Size = size, IndexName = index, Then = ParseShapeTarget(named) };
            }
            case "faces":
            {
                Resolver<double> deep = null;
                Dictionary<string, List<ShapeAction>> faces = [];

                if (CurrentParser.PeekNextToken() is { Text: "depth" })
                {
                    CurrentParser.GetNextToken();
                    deep = ShapeNumber();
                }

                CurrentParser.MatchToken(true, () => "Expecting an open brace to follow \"faces\" here.", BounderToken.OpenBrace);

                while (!CurrentParser.IsNext(BounderToken.CloseBrace))
                {
                    Token face = CurrentParser.GetNextToken();

                    if (face is null || !ShapeFaceWords.Contains(face.Text))
                        throw new TokenException("Expecting front, back, left, right, sides, top or bottom here.") { Token = face };

                    CurrentParser.MatchToken(true, () => "Expecting a colon to follow the face here.", OperatorToken.Colon);
                    faces[face.Text] = ParseShapeTarget(named);
                }

                CurrentParser.GetNextToken();

                return new ShapeFaces { Depth = deep, Faces = faces };
            }
            case "move":
                return new ShapeMove { By = new TermResolver<Vector> { Term = ShapeTerm() }, Then = ParseShapeTarget(named) };
            case "size":
                return new ShapeResize { To = new TermResolver<Vector> { Term = ShapeTerm() }, Then = ParseShapeTarget(named) };
            case "roof":
            {
                Token kind = CurrentParser.GetNextToken();

                if (kind is null || kind.Text is not ("gable" or "hip" or "flat"))
                    throw new TokenException("Expecting gable, hip or flat to follow \"roof\" here.") { Token = kind ?? word };

                Resolver<double> angle = kind.Text == "flat" ? null : new AngleResolver { Term = ShapeTerm() };
                Resolver<double> overhang = null;
                Resolver<double> verge = null;
                Resolver<Material> material = null;
                Resolver<Material> gables = null;
                bool across = false;

                while (CurrentParser.PeekNextToken() is { Text: "across" or "overhang" or "verge" or "material" or "gables" } modifier)
                {
                    switch (modifier.Text)
                    {
                        case "across":
                            CurrentParser.GetNextToken();
                            across = true;
                            break;
                        case "overhang":
                            CurrentParser.GetNextToken();
                            overhang = ShapeNumber();
                            break;
                        case "verge":
                            CurrentParser.GetNextToken();
                            verge = ShapeNumber();
                            break;
                        case "material":
                            material = ParseBlendedMaterial();
                            break;
                        default:
                            CurrentParser.GetNextToken();
                            gables = ParseBlendedMaterial();
                            break;
                    }
                }

                return new ShapeRoof
                {
                    Kind = kind.Text, Angle = angle, Overhang = overhang, Verge = verge, Across = across, Material = material,
                    Gables = gables
                };
            }
            case "fill":
                return new ShapeFill
                {
                    Material = CurrentParser.PeekNextToken() is { Text: "material" } ? ParseBlendedMaterial() : null
                };
            case "place":
            {
                CurrentParser.MatchToken(true, () => "Expecting an open brace to follow \"place\" here.", BounderToken.OpenBrace);

                return new ShapePlace { Surfaces = ParseObjectResolver<GroupResolver>("groupEntryClause", HandleGroupEntryClause) };
            }
            case "choose":
            {
                List<(Resolver<double>, List<ShapeAction>)> ways = [];

                CurrentParser.MatchToken(true, () => "Expecting an open brace to follow \"choose\" here.", BounderToken.OpenBrace);

                while (!CurrentParser.IsNext(BounderToken.CloseBrace))
                    ways.Add((ShapeNumber(), ParseShapeTarget(named)));

                CurrentParser.GetNextToken();

                return new ShapeChoose { Id = ++_shapeChoices, Ways = ways };
            }
            case "if":
            {
                Resolver<bool> condition = new TermResolver<bool> { Term = ShapeTerm() };
                List<ShapeAction> then = ParseShapeTarget(named);
                List<ShapeAction> otherwise = [];

                if (CurrentParser.PeekNextToken() is { Text: "else" })
                {
                    CurrentParser.GetNextToken();
                    otherwise = ParseShapeTarget(named);
                }

                return new ShapeIf { Condition = condition, Then = then, Else = otherwise };
            }
            case "nothing":
                return null;
            default:
                named.Add(word);

                return new ShapeSymbol { Name = word.Text };
        }
    }

    private Term ShapeTerm() => (Term) LanguageDsl.ParseExpression(CurrentParser);

    private TermResolver<double> ShapeNumber() => new () { Term = ShapeTerm() };

    private int ShapeAxis()
    {
        Token axis = CurrentParser.GetNextToken();

        return axis?.Text switch
        {
            "X" => 0,
            "Y" => 1,
            "Z" => 2,
            _ => throw new TokenException("Expecting X, Y or Z here.") { Token = axis }
        };
    }
}
