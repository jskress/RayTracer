using Lex.Clauses;
using Lex.Parser;
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
    /// This method is used to handle a loop written at the top of a file, where what it makes goes
    /// straight into the scene the file describes.
    /// </summary>
    /// <param name="clause">The clause that opened the loop.</param>
    private void HandleStartForClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Loop");

        _context.InstructionContext.AddInstruction(new TopLevelSurfacesCreator
        {
            Context = _context.InstructionContext,
            Entry = ParseForClause(clause)
        });
    }

    /// <summary>
    /// This method is used to handle a choice written at the top of a file, where what it makes goes
    /// straight into the scene the file describes.
    /// </summary>
    /// <param name="clause">The clause that opened the choice.</param>
    private void HandleStartSurfaceIfClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Choice");

        _context.InstructionContext.AddInstruction(new TopLevelSurfacesCreator
        {
            Context = _context.InstructionContext,
            Entry = ParseSurfaceIfClause(clause)
        });
    }

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
                case "for":
                case "over":
                    resolver.SurfaceResolvers.Add(ParseForClause(clause));
                    break;
                case "if":
                    resolver.SurfaceResolvers.Add(ParseSurfaceIfClause(clause));
                    break;
                case "local":
                    resolver.SurfaceResolvers.Add(ParseLocalClause(clause));
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
                case "isosurface":
                    resolver.SurfaceResolvers.Add(ParseIsosurfaceClause(clause));
                    break;
                case "patch":
                    resolver.SurfaceResolvers.Add(ParsePatchClause(clause));
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
                case "call":
                    resolver.SurfaceResolvers.Add(ParseCall(clause));
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
    /// This method reads a loop: the range it counts through and the things it makes each time round.
    /// <para>
    /// The body is read as though it were a group's, which is exactly what it is -- the same surfaces
    /// may stand in it, and a loop may stand in it too, that being how one loop is written inside
    /// another.  What may not stand in it is anything belonging to a group rather than to the things
    /// in it: a transform, a material, a name.  A loop is not a thing in the scene and has nothing for
    /// those to be about, so they are refused here rather than read and quietly dropped.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause that opened the loop, whether "for" or "over".</param>
    /// <returns>The loop.</returns>
    private SurfaceLoop ParseForClause(Clause clause)
    {
        ClauseReader reader = clause.Reader();
        string counterName = null;

        // "for i in ..." or "over ...", the second being the first with no name for the count.
        if (reader.NextToken().Text == "for")
        {
            counterName = reader.NextText();

            reader.NextToken(); // The "in" keyword.
        }

        Token startToken = reader.NextToken();
        bool startIsOpen = BounderToken.LeftParen.Matches(startToken);
        Term start = (Term) reader.NextExpression();

        reader.NextToken(); // The comma.

        Term end = (Term) reader.NextExpression();
        Token endToken = reader.NextToken();
        bool endIsOpen = BounderToken.RightParen.Matches(endToken);
        Term step = null;

        // What is left is the "by" and its expression, the open brace having been taken by the clause.
        if (reader.HasMoreTokens && !BounderToken.OpenBrace.Matches(reader.PeekToken()))
        {
            reader.NextToken(); // The "by" keyword.

            step = (Term) reader.NextExpression();
        }

        SurfaceLoop loop = new ()
        {
            ErrorToken = clause.Tokens[0],
            CounterName = counterName,
            Start = start,
            End = end,
            Step = step,
            StartIsOpen = startIsOpen,
            EndIsOpen = endIsOpen
        };
        loop.SurfaceResolvers = ParseSurfaceList("a \"for\"");

        return loop;
    }

    /// <summary>
    /// This method reads a choice standing among surfaces: the question, the things to make when the
    /// answer is yes, and the things to make when it is not.
    /// <para>
    /// The <c>else</c> is optional, which is the one way this differs in the reading from the choice
    /// that ends a function's body.  There, both ways out must give an answer.  Here, an arm makes
    /// things, and making nothing is a perfectly good thing to do -- so a choice written with no
    /// <c>else</c> is complete as it stands rather than half-written.
    /// </para>
    /// <para>
    /// An <c>else</c> followed by another <c>if</c> is read as a choice standing alone where the second
    /// arm would have been, exactly as a body's is, and for the same reason: it saves the writer a pair
    /// of braces and a step of indenting per case, which is the difference between a run of cases that
    /// reads down the page and one that walks off the right of it.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause that opened the choice.</param>
    /// <returns>The choice.</returns>
    private SurfaceChoice ParseSurfaceIfClause(Clause clause)
    {
        SurfaceChoice choice = new ()
        {
            Condition = clause.Term(),
            ErrorToken = clause.Tokens[0],
            WhenTrue = ParseSurfaceList("an \"if\"")
        };

        Clause otherwise = ParseClause("optionalElseClause");

        if (otherwise is null)
            return choice;

        Clause chained = ParseClause("startIfClause");

        if (chained is null)
        {
            // Complained about here rather than by matching, since an "else" may be the last thing in
            // a file and there would then be no parser left to do the matching or the complaining.
            if (CurrentParser is null)
            {
                throw new TokenException(
                    "Expecting an open brace, or another \"if\", to follow \"else\" here.")
                {
                    Token = otherwise.Tokens[0]
                };
            }

            CurrentParser.MatchToken(
                true, () => "Expecting an open brace, or another \"if\", to follow \"else\" here.",
                BounderToken.OpenBrace);

            choice.WhenFalse = ParseSurfaceList("an \"else\"");
        }
        else
        {
            // The chained one opened its own brace and will close it, so nothing is owed here: this
            // choice stands where a list would, and has no braces to call its own.
            choice.WhenFalse = [ParseSurfaceIfClause(chained)];
        }

        return choice;
    }

    /// <summary>
    /// This method reads a name worked out part way down a list of surfaces.
    /// </summary>
    /// <param name="clause">The clause that gave the name.</param>
    /// <returns>The name and what it stands for.</returns>
    private static SurfaceLocal ParseLocalClause(Clause clause)
    {
        return new SurfaceLocal
        {
            Name = clause.Tokens[0].Text,
            Value = clause.Term()
        };
    }

    /// <summary>
    /// This method reads a run of surfaces up to its closing brace, the opening one having been taken
    /// already by whatever is being read.
    /// </summary>
    /// <param name="what">What is being read, for complaining with.</param>
    /// <returns>The things standing there, in the order they were written.</returns>
    private List<ISurfaceResolver> ParseSurfaceList(string what)
    {
        return ParseObjectResolver<GroupResolver>(
                "groupEntryClause", clause => HandleNestedEntryClause(clause, what), validate: false)
            .SurfaceResolvers;
    }

    /// <summary>
    /// This method is used to handle an item clause inside a loop or a choice, which is a group's item
    /// clause with the things that belong to a group taken away.
    /// <para>
    /// What is taken away is what would belong to the thing around rather than to the things inside.
    /// Neither a loop nor a choice is a thing in the scene, so neither has anything for a transform, a
    /// material or a name to be about.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause to process, or <c>null</c> for a transform.</param>
    /// <param name="what">What is being read, for complaining with.</param>
    private void HandleNestedEntryClause(Clause clause, string what)
    {
        if (clause is null || clause.Tag == "surface")
        {
            // Thrown rather than handed to the usual complaint, which would report whatever the
            // grammar happened to be trying last and bury the one thing worth saying.
            throw new TokenException(
                $"Only surfaces may stand inside {what}.  A transform, a material or a name belongs " +
                "either to the group around it or to one of the things inside it.")
            {
                Token = clause is null ? CurrentParser.PeekNextToken() : clause.Tokens[0]
            };
        }

        HandleGroupEntryClause(clause);
    }
}
