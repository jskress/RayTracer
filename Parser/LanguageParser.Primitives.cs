using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Surfaces.Extrusions;
using RayTracer.Instructions.Surfaces.LSystems;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This field holds what each primitive a scene has declared gives back, so that a call may be
    /// read.  It is kept here, beside the parsing, rather than with the scene's other names, because
    /// a call has to be <i>read</i> before anything is built and the shape of what follows it depends
    /// on the answer.  The primitive itself is bound into the scene's names as well, at render time,
    /// which is where its scope comes from.
    /// </summary>
    private readonly Dictionary<string, UserPrimitive> _primitives = new ();

    /// <summary>
    /// This method is used to parse a primitive a scene writes for itself.
    /// </summary>
    /// <param name="clause">The clause that opens the declaration.</param>
    private void HandleStartPrimitiveClause(Clause clause)
    {
        UserPrimitive primitive = ParsePrimitiveDeclaration(clause);

        _primitives[primitive.Name] = primitive;

        _context.InstructionContext.AddInstruction(new DeclarePrimitiveInstruction
        {
            Primitive = primitive
        });
    }

    /// <summary>
    /// This method reads a whole primitive declaration, which is the same work whether it stands at
    /// the top of a file or inside another primitive.
    /// </summary>
    /// <param name="clause">The clause that opens the declaration.</param>
    /// <returns>The primitive, as yet belonging to no scope.</returns>
    private UserPrimitive ParsePrimitiveDeclaration(Clause clause)
    {
        string name = clause.Tokens[1].Text;
        (List<string> parameterNames, List<Term> defaults) = ParseFunctionParameters();
        Clause kind = ParseClause("primitiveKindClause");

        if (kind is null)
            throw CreateUnexpectedInputException("Expecting \"->\" and a kind here.");

        string kindName = string.Join(' ', kind.Tokens[1..^1].Select(token => token.Text));

        // What was known before this body was read, so that anything the body declares for itself can
        // be forgotten again afterward.  A smaller primitive is its parent's business and nobody
        // else's, and the parser is where that has to be enforced, a call being *read* rather than
        // looked up when the time comes.
        Dictionary<string, UserPrimitive> before = new (_primitives);

        // Known by its own name while its body is read, so that a body may stand on itself.
        _primitives[name] = new UserPrimitive(name, parameterNames, defaults, kindName, [], null);

        (List<FunctionBodyStep> steps, ISurfaceResolver body) = ParsePrimitiveBody(name, kindName);

        _primitives.Clear();

        foreach ((string had, UserPrimitive was) in before)
            _primitives[had] = was;

        return new UserPrimitive(name, parameterNames, defaults, kindName, steps, body);
    }

    /// <summary>
    /// This method reads what a primitive works out on its way to its answer, and the answer itself.
    /// <para>
    /// The answer is a surface rather than a sum, so what follows the <c>return</c> is read as one --
    /// and read as the very kind that was declared, so that saying one thing and giving back another
    /// is caught here rather than left to be discovered when something looks wrong in a picture.
    /// </para>
    /// </summary>
    /// <param name="name">The primitive's name, for complaining with.</param>
    /// <param name="kind">The kind of surface it says it gives back.</param>
    /// <returns>The steps on the way, and the recipe for the answer.</returns>
    private (List<FunctionBodyStep>, ISurfaceResolver) ParsePrimitiveBody(string name, string kind)
    {
        List<FunctionBodyStep> steps = [];

        while (true)
        {
            // A smaller primitive of its own, which is how a complicated thing is made out of simpler
            // ones without the simpler ones becoming everybody's business.
            Clause inner = ParseClause("startPrimitiveClause");

            if (inner != null)
            {
                UserPrimitive smaller = ParsePrimitiveDeclaration(inner);

                // Known for the rest of this body, and dropped again with it.
                _primitives[smaller.Name] = smaller;

                steps.Add(new FunctionBodyStep(smaller.Name, smaller));

                continue;
            }

            Clause nested = ParseClause("startFunctionClause");

            if (nested != null)
            {
                UserFunction function = ParseFunctionDeclaration(nested);

                steps.Add(new FunctionBodyStep(function.Name, function));

                continue;
            }

            Clause local = ParseClause("functionLocalClause");

            if (local is null)
                break;

            steps.Add(new FunctionBodyStep(local.Tokens[0].Text, local.Term()));
        }

        if (ParseClause("primitiveReturnClause") is null)
        {
            throw CreateUnexpectedInputException(
                $"The primitive '{name}' never says what it gives back; it needs a \"return\".");
        }

        ISurfaceResolver body = ParseSurfaceOfKind(kind, name);

        CurrentParser.MatchToken(
            true, () => $"Expecting a close brace to end the primitive '{name}'; nothing may follow " +
                        "its \"return\".", BounderToken.CloseBrace);

        return (steps, body);
    }

    /// <summary>
    /// This method is used to handle a call of a primitive a scene has written.
    /// </summary>
    /// <param name="clause">The clause that opens the call.</param>
    private void HandleStartCallClause(Clause clause)
    {
        VerifyDefaultSceneUsage(clause, "Object");

        _context.InstructionContext.AddInstruction(new TopLevelObjectCreator
        {
            Context = _context.InstructionContext,
            Resolver = ParseCall(clause)
        });
    }

    /// <summary>
    /// This method reads one call of a primitive: the values it is given, and whatever the call adds
    /// in a block of its own.
    /// <para>
    /// The block is the kind's own, not some general one, because that is what a scene's author will
    /// expect: reusing a named group has always taken group clauses, and a call giving back a group
    /// should be no different.  The copy is what makes that safe -- what a call adds belongs to that
    /// call, and the recipe everyone else uses is left as it was written.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause that opens the call.</param>
    /// <returns>The resolver for this call.</returns>
    private ISurfaceResolver ParseCall(Clause clause)
    {
        Token nameToken = clause.Tokens[1];
        string name = nameToken.Text;

        if (!_primitives.TryGetValue(name, out UserPrimitive primitive))
        {
            throw new TokenException($"Nothing named '{name}' is a primitive this scene has declared.")
            {
                Token = nameToken
            };
        }

        List<Term> arguments = ParseCallArguments();
        string wrong = primitive.CheckCall(arguments.Count);

        if (wrong != null)
            throw new TokenException(wrong) { Token = nameToken };

        return new PrimitiveCallResolver
        {
            Name = name,
            Arguments = arguments,
            Body = primitive.Body,
            Extras = ParseCallBlock(primitive.Kind),
            ErrorToken = nameToken
        };
    }

    /// <summary>
    /// This method reads the surface a primitive gives back, as the very kind it said it would.
    /// </summary>
    /// <param name="kind">The kind declared.</param>
    /// <param name="name">The primitive's name, for complaining with.</param>
    /// <returns>The recipe for the surface.</returns>
    private ISurfaceResolver ParseSurfaceOfKind(string kind, string name)
    {
        Clause clause = ParseClause(StartClauseFor(kind));

        if (clause is null)
        {
            throw CreateUnexpectedInputException(
                $"The primitive '{name}' says it gives back a {kind}, so a {kind} must follow its " +
                "\"return\".");
        }

        return kind switch
        {
            "group" => ParseGroupClause(clause),
            "union" or "difference" or "intersection" => ParseCsgClause(clause),
            "plane" => ParsePlaneClause(clause),
            "sphere" => ParseSphereClause(clause),
            "cube" => ParseCubeClause(clause),
            "cylinder" => ParseCylinderClause(clause),
            "conic" => ParseConicClause(clause),
            "torus" => ParseTorusClause(clause),
            "egg" => ParseEggClause(clause),
            "superellipsoid" => ParseSuperellipsoidClause(clause),
            "isosurface" => ParseIsosurfaceClause(clause),
            "patch" => ParsePatchClause(clause),
            "lathe" => ParseLatheClause(clause),
            "blob" => ParseBlobClause(clause),
            "tube" => ParseTubeClause(clause),
            "sweep" => ParseSweepClause(clause),
            "extrusion" => ParseExtrusionClause(clause),
            "text" => ParseTextClause(clause),
            "lsystem" => ParseLSystemClause(clause),
            "heightfield" => ParseHeightFieldClause(clause),
            "parallelogram" => ParseParallelogramClause(clause),
            "disc" => ParseDiscClause(clause),
            "triangle" => ParseTriangleClause(clause),
            "smooth triangle" => ParseSmoothTriangleClause(clause),
            "generic shape" => ParseGenericShapeClause(clause),
            "object file" => ParseObjectFileClause(clause),
            _ => throw CreateUnexpectedInputException($"A primitive cannot give back a {kind}.")
        };
    }

    /// <summary>
    /// This method reads whatever a call adds in a block of its own.
    /// <para>
    /// The block is read as the declared kind's own clauses, so a call giving back a cylinder takes
    /// <c>max Y</c> exactly as reusing a named cylinder does.  It is kept apart from the primitive's
    /// body rather than folded into it, because the two belong to different sets of names: the body
    /// to where the primitive was written, this to where the call was.
    /// </para>
    /// <para>
    /// The table below is the twin of the one <c>object</c> keeps for reusing a named surface, and
    /// the two have to say the same thing about every kind.  There is no deriving one from the other:
    /// which clauses a kind takes is a fact about the grammar, and several kinds share a set while
    /// others have their own.
    /// </para>
    /// </summary>
    /// <param name="kind">The kind declared.</param>
    /// <returns>What the call added, or <c>null</c> if it added nothing.</returns>
    private ISurfaceResolver ParseCallBlock(string kind)
    {
        Token next = CurrentParser.PeekNextToken();

        if (next is null || !BounderToken.OpenBrace.Matches(next))
            return null;

        CurrentParser.GetNextToken();

        return kind switch
        {
            "group" => ParseObjectResolver<GroupResolver>(
                "groupEntryClause", HandleGroupEntryClause, validate: false),
            "union" or "difference" or "intersection" => ParseObjectResolver<CsgSurfaceResolver>(
                "csgEntryClause", HandleCsgEntryClause, validate: false),
            "plane" => ParseObjectResolver<PlaneResolver>(
                "surfaceEntryClause", HandlePlaneEntryClause, validate: false),
            "sphere" => ParseObjectResolver<SphereResolver>(
                "surfaceEntryClause", HandleSphereEntryClause, validate: false),
            "cube" => ParseObjectResolver<CubeResolver>(
                "surfaceEntryClause", HandleCubeEntryClause, validate: false),
            "cylinder" => ParseObjectResolver<CylinderResolver>(
                "extrudedSurfaceEntryClause", HandleCylinderEntryClause, validate: false),
            "conic" => ParseObjectResolver<ConicResolver>(
                "extrudedSurfaceEntryClause", HandleConicEntryClause, validate: false),
            "torus" => ParseObjectResolver<TorusResolver>(
                "torusEntryClause", HandleTorusEntryClause, validate: false),
            "egg" => ParseObjectResolver<EggResolver>(
                "eggEntryClause", HandleEggEntryClause, validate: false),
            "superellipsoid" => ParseObjectResolver<SuperellipsoidResolver>(
                "superellipsoidEntryClause", HandleSuperellipsoidEntryClause, validate: false),
            "isosurface" => ParseObjectResolver<IsosurfaceResolver>(
                "isosurfaceEntryClause", HandleIsosurfaceEntryClause, validate: false),
            "patch" => ParseObjectResolver<BicubicPatchResolver>(
                "patchEntryClause", HandlePatchEntryClause, validate: false),
            "lathe" => ParseObjectResolver<LatheResolver>(
                "latheEntryClause", HandleLatheEntryClause, validate: false),
            "blob" => ParseObjectResolver<BlobResolver>(
                "blobEntryClause", HandleBlobEntryClause, validate: false),
            "tube" => ParseObjectResolver<TubeResolver>(
                "tubeEntryClause", HandleTubeEntryClause, validate: false),
            "sweep" => ParseObjectResolver<SweepResolver>(
                "sweepEntryClause", HandleSweepEntryClause, validate: false),
            "extrusion" => ParseObjectResolver<ExtrusionResolver>(
                "extrusionEntryClause", HandleExtrusionEntryClause, validate: false),
            "text" => ParseObjectResolver<TextSolidResolver>(
                "textEntryClause", HandleTextEntryClause, validate: false),
            "lsystem" => ParseObjectResolver<LSystemResolver>(
                "lsystemEntryClause", HandleLSystemEntryClause, validate: false),
            "heightfield" => ParseObjectResolver<HeightFieldResolver>(
                "heightFieldEntryClause", HandleHeightFieldEntryClause, validate: false),
            "parallelogram" => ParseObjectResolver<ParallelogramResolver>(
                "parallelogramEntryClause", HandleParallelogramEntryClause, validate: false),
            "disc" => ParseObjectResolver<DiscResolver>(
                "discEntryClause", HandleDiscEntryClause, validate: false),
            "triangle" => ParseObjectResolver<TriangleResolver>(
                "triangleEntryClause", HandleTriangleEntryClause, validate: false),
            "smooth triangle" => ParseObjectResolver<SmoothTriangleResolver>(
                "smoothTriangleEntryClause", HandleSmoothTriangleEntryClause, validate: false),
            "generic shape" => ParseObjectResolver<GenericShapeResolver>(
                "genericShapeEntryClause", HandleGenericShapeEntryClause, validate: false),
            "object file" => ParseObjectResolver<ObjectFileResolver>(
                "objectFileEntryClause", HandleObjectFileEntryClause, validate: false),
            _ => throw CreateUnexpectedInputException($"A {kind} takes no block here.")
        };
    }

    /// <summary>
    /// This method returns the grammar rule that opens a surface of the given kind.
    /// </summary>
    /// <param name="kind">The kind in question.</param>
    /// <returns>The name of the rule that opens it.</returns>
    private static string StartClauseFor(string kind)
    {
        return kind switch
        {
            "group" => "startGroupClause",
            "union" or "difference" or "intersection" => "startCsgClause",
            "plane" => "startPlaneClause",
            "sphere" => "startSphereClause",
            "cube" => "startCubeClause",
            "cylinder" => "startCylinderClause",
            "conic" => "startConicClause",
            "torus" => "startTorusClause",
            "egg" => "startEggClause",
            "superellipsoid" => "startSuperellipsoidClause",
            "isosurface" => "startIsosurfaceClause",
            "patch" => "startPatchClause",
            "lathe" => "startLatheClause",
            "blob" => "startBlobClause",
            "tube" => "startTubeClause",
            "sweep" => "startSweepClause",
            "extrusion" => "startExtrusionClause",
            "text" => "startTextClause",
            "lsystem" => "startLsystemClause",
            "heightfield" => "startHeightFieldClause",
            "parallelogram" => "startParallelogramClause",
            "disc" => "startDiscClause",
            "triangle" => "startTriangleClause",
            "smooth triangle" => "startSmoothTriangleClause",
            "generic shape" => "startGenericShapeClause",
            "object file" => "startObjectFileClause",
            _ => throw new ArgumentException($"There is no grammar rule for a {kind}.")
        };
    }

    /// <summary>
    /// This method reads the values a call supplies.
    /// </summary>
    /// <returns>The values, in the order they were written.</returns>
    private List<Term> ParseCallArguments()
    {
        List<Term> arguments = [];

        // The close is looked for before each value rather than after, since an expression clause
        // complains rather than declining when there is no expression there -- so a call with no
        // values at all, or a trailing comma, would otherwise be met with a complaint about a missing
        // term instead of being read as what it plainly is.
        while (!NextIsRightParen())
        {
            Clause clause = ParseClause("argumentClause");

            if (clause is null)
                break;

            arguments.Add(clause.Term());
        }

        CurrentParser.MatchToken(
            true, () => "Expecting a close parenthesis to end the values here.",
            BounderToken.RightParen);

        return arguments;
    }

    /// <summary>
    /// This method reports whether the next thing to be read closes a parenthesis.
    /// </summary>
    /// <returns><c>true</c>, if a close parenthesis is next.</returns>
    private bool NextIsRightParen()
    {
        Token next = CurrentParser.PeekNextToken();

        return next is not null && BounderToken.RightParen.Matches(next);
    }
}
