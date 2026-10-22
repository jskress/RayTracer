using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Instructions;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Patterns;
using RayTracer.Instructions.Surfaces;
using RayTracer.Instructions.Transforms;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a clause of zero or more transformations.
    /// </summary>
    private MaterialResolver ParseMaterialClause()
    {
        return ParseObjectResolver<MaterialResolver>(
            "materialEntryClause", HandleMaterialEntryClause);
    }

    /// <summary>
    /// This is a helper method for creating the right resolver, either by parsing an
    /// in-place definition or a variable reference.
    /// </summary>
    /// <param name="clause">The clause that tells us how to get the material resolver.</param>
    /// <returns>The proper resolver.</returns>
    private MaterialResolver GetMaterialResolver(Clause clause)
    {
        Token token = clause.Tokens[1];

        // Complete definition.
        if (BounderToken.OpenBrace.Matches(token))
            return ParseMaterialClause();

        // A blend of whole materials by a pattern.
        if (token.Text == "blend" && clause.Tokens.Count > 2)
            return ParseMaterialBlend(clause);

        // Inherited, a surface takes the material handed down to it.  With a block, it takes that and
        // adds decals of its own on top, so a model can carry markings and still be repainted.
        if (token.Text == "inherited")
        {
            MaterialResolver inherited = new () { SetToNull = true };

            if (clause.Tokens.Count > 2)
                ParseObjectResolver("materialEntryClause", HandleMaterialEntryClause, inherited);

            return inherited;
        }

        // A material a scene wrote for itself, called with values.  What follows the call is read as
        // an overlay by the resolver itself, in the caller's names.
        if (CallArgumentsIfAny(token, "material") is { } given)
        {
            MaterialCallResolver call = new ()
            {
                Name = token.Text, Arguments = given, ErrorToken = token
            };

            if (BounderToken.OpenBrace.Matches(CurrentParser.PeekNextToken()))
            {
                CurrentParser.GetNextToken();

                ParseObjectResolver("materialEntryClause", HandleMaterialEntryClause, call, false);
            }

            return call;
        }

        bool extending = clause.Tokens.Count > 2;
        MaterialResolver resolver = GetExtensibleItem<MaterialResolver>(token, extending);

        if (extending)
            ParseObjectResolver("materialEntryClause", HandleMaterialEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to parse a blend of whole materials by a pattern: the pattern and whatever it has to
    /// say of itself, then the materials -- a map of values and materials, or for a pattern that picks among a
    /// fixed number of things, that many materials -- then where the pattern stands.  See
    /// <see cref="MaterialBlend"/>.
    /// </summary>
    /// <param name="clause">The clause that opened the blend: `material blend`, the pattern and a brace.</param>
    /// <returns>The blend's resolver.</returns>
    private MaterialResolver ParseMaterialBlend(Clause clause)
    {
        (IPatternResolver pattern, int discretePigmentsNeeded) = ParsePatternClause(clause, 2);
        MaterialBlendResolver resolver = new () { PatternResolver = pattern };

        ReadWeatheringEntriesInto(pattern);

        if (discretePigmentsNeeded > 0)
        {
            for (int index = 0; index < discretePigmentsNeeded; index++)
            {
                if (index > 0)
                    CurrentParser.MatchToken(true, () => "Expecting a comma here.", OperatorToken.Comma);

                resolver.MaterialResolvers.Add(ParseBlendedMaterial());
            }
        }
        else
        {
            CurrentParser.MatchToken(
                true, () => "Expecting the blend's values and materials, in brackets, here.", BounderToken.OpenBracket);

            while (!CurrentParser.IsNext(BounderToken.CloseBracket))
            {
                Term value = (Term) LanguageDsl.ParseExpression(CurrentParser);

                CurrentParser.MatchToken(true, () => "Expecting a comma here.", OperatorToken.Comma);

                resolver.BreakResolvers.Add(new TermResolver<double> { Term = value });
                resolver.MaterialResolvers.Add(ParseBlendedMaterial());

                if (CurrentParser.IsNext(OperatorToken.Comma))
                    CurrentParser.GetNextToken();
                else if (!CurrentParser.IsNext(BounderToken.CloseBracket))
                {
                    throw new TokenException("Expecting a comma or close bracket here.")
                    {
                        Token = CurrentParser.GetNextToken()
                    };
                }
            }

            Token bracket = CurrentParser.GetNextToken();

            if (resolver.MaterialResolvers.Count < 2)
                throw new TokenException("A blend wants at least two materials.") { Token = bracket };
        }

        ReadWeatheringEntriesInto(pattern);

        resolver.PatternTransformResolver = ParseTransformClause();

        CurrentParser.MatchToken(true, () => "Expecting a close brace here.", BounderToken.CloseBrace);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a weather block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleWeatherEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid weather property here.");

        WeatherResolver resolver = (WeatherResolver) _context.CurrentTarget;
        TermResolver<double> amount = new ()
        {
            Term = clause.Term(),
            Validator = value => value is < 0 or > 1 ? "How much weather there is runs from nothing to one." : null
        };

        switch (ToCmd(clause))
        {
            case "grime":
                resolver.GrimeResolver = amount;

                if (clause.Expressions.Count > 1)
                    resolver.GrimeColorResolver = new TermResolver<Color> { Term = clause.Term(1) };

                break;
            case "dust":
                resolver.DustResolver = amount;

                if (clause.Expressions.Count > 1)
                    resolver.DustColorResolver = new TermResolver<Color> { Term = clause.Term(1) };

                break;
            case "snow":
                resolver.SnowResolver = amount;

                if (clause.Expressions.Count > 1)
                    resolver.SnowColorResolver = new TermResolver<Color> { Term = clause.Term(1) };

                break;
            case "wear":
                resolver.WearResolver = amount;

                // What wear bares follows `to`: a material written out, or named.
                if (clause.Tokens.Count > 1)
                    resolver.WearToResolver = ParseBlendedMaterial();

                break;
            case "radius":
                resolver.RadiusResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value > 0 ? null : "How far weather is looked for must be more than nothing."
                };
                break;
            case "splash":
                resolver.SplashResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value < 0 ? "How high dirt has been kicked up cannot be less than nothing." : null
                };

                if (clause.Expressions.Count > 1)
                    resolver.SplashColorResolver = new TermResolver<Color> { Term = clause.Term(1) };

                break;
            default:
                throw new Exception($"Internal error: unknown weather property found: {ToCmd(clause)}.");
        }
    }

    /// <summary>
    /// This method reads one of a blend's materials: a material written out, or named.
    /// </summary>
    /// <returns>The material's resolver.</returns>
    private Resolver<Material> ParseBlendedMaterial()
    {
        if (CurrentParser.PeekNextToken() is { Text: "material" })
            return GetMaterialResolverForUse(ParseClause("startMaterialClause"));

        Token name = CurrentParser.GetNextToken();

        return IsAPrimitiveParameter(name.Text)
            ? new TermResolver<Material> { Term = new VariableTerm(name) }
            : GetExtensibleItem<MaterialResolver>(name, false);
    }

    /// <summary>
    /// The same, at a place where a material is being *used* rather than declared.
    /// <para>
    /// That is the one place a name may turn out to be a primitive's parameter, which has no value
    /// while the body is being parsed and so cannot be looked up then.  Every other form settles at
    /// parse time and is left to do so: a material written out, an `inherited`, one being extended,
    /// a call of a material primitive, and a name the scene has already written a material for.
    /// Only a name that is none of those is read as a term instead.
    /// </para>
    /// <para>
    /// **Only a parameter takes that road**, and the reason is the import filter.  A scene that
    /// imports two names out of a library has the rest pruned from the parser's own table, which is
    /// what stops it writing one it did not ask for; a name read as a term instead would sail past
    /// that, since the values themselves all go over whole.  So the question asked here is not "is
    /// this name unknown" but "is this name a parameter" -- which a mistyped one is not, so a typo is
    /// still caught while parsing.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause that tells us how to get the material resolver.</param>
    /// <returns>The proper resolver.</returns>
    private Resolver<Material> GetMaterialResolverForUse(Clause clause)
    {
        Token token = clause.Tokens[1];
        Token next = CurrentParser.PeekNextToken();

        if (!BounderToken.OpenBrace.Matches(token) &&
            token.Text != "inherited" &&
            clause.Tokens.Count == 2 &&
            (next is null || !BounderToken.LeftParen.Matches(next)) &&
            IsAPrimitiveParameter(token.Text))
        {
            return new TermResolver<Material> { Term = new VariableTerm(token) };
        }

        return GetMaterialResolver(clause);
    }

    /// <summary>
    /// This method is used to handle an item clause of a material block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleMaterialEntryClause(Clause clause)
    {
        MaterialResolver resolver = (MaterialResolver) _context.CurrentTarget;
        string field = clause.Text();
        Term term = clause.Term();

        switch (field)
        {
            case "pigment":
                resolver.PigmentResolver = ParsePigmentClause();
                break;
            case "ambient":
                resolver.AmbientResolver = new TermResolver<double>() { Term = term };
                break;
            // A number is a white glow of that strength, and a color -- `kelvin(2700) * 3`, say -- a glow
            // of its own color; the material's pigment tints either.
            case "emission":
                resolver.EmissionResolver = new CoefficientResolver
                {
                    Term = term,
                    Validator = value => value.Red < 0 || value.Green < 0 || value.Blue < 0
                        ? "A surface cannot give off less than no light."
                        : null
                };
                break;
            case "diffuse":
                resolver.DiffuseResolver = new TermResolver<double>() { Term = term };
                break;
            case "specular":
                resolver.SpecularResolver = new TermResolver<double>() { Term = term };
                break;
            case "shininess":
                resolver.ShininessResolver = new TermResolver<double>() { Term = term };
                break;
            case "brilliance":
                resolver.BrillianceResolver = new TermResolver<double>() { Term = term };
                break;
            case "grain":
                resolver.GrainResolver = new TermResolver<double>() { Term = term };
                break;
            case "reflective":
                resolver.ReflectiveResolver = new TermResolver<double>() { Term = term };
                break;
            case "roughness":
                resolver.RoughnessResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = value => value is < 0 or > 1
                        ? "A roughness runs from nothing, a mirror, to one, which is matte."
                        : null
                };
                break;
            case "fresnel":
                resolver.FresnelResolver = new LiteralResolver<bool> { Value = true };
                break;
            case "anisotropy":
                resolver.AnisotropyResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = value => value is < 0 or > 1
                        ? "How much a surface is brushed runs from nothing, alike every way, to one."
                        : null
                };

                // Which way the grooves run, if it says: an axis by name, or any direction.
                if (clause.Tokens.Count > 1)
                {
                    Resolver<Vector> direction = clause.Tokens.Count > 2
                        ? new LiteralResolver<Vector>
                        {
                            Value = clause.Text(2) switch
                            {
                                "X" => new Vector(1, 0, 0),
                                "Y" => new Vector(0, 1, 0),
                                _ => new Vector(0, 0, 1)
                            }
                        }
                        : new TermResolver<Vector>
                        {
                            Term = clause.Term(1),
                            Validator = value => value.Magnitude > 0
                                ? null
                                : "The way a surface is brushed must be some way, not nothing."
                        };

                    if (clause.Text(1) == "around")
                    {
                        resolver.BrushedAroundResolver = direction;
                        resolver.BrushedAlongResolver = null;
                    }
                    else
                    {
                        resolver.BrushedAlongResolver = direction;
                        resolver.BrushedAroundResolver = null;
                    }
                }

                break;
            case "melanin":
                resolver.MelaninResolver = new MelaninResolver
                {
                    First = term, Second = clause.Expressions.Count > 1 ? clause.Term(1) : null
                };
                break;
            case "metallic":
                // The amount may be left off, since fully metallic is what is nearly always
                // wanted; naming nothing means naming 1.  When the material's next entry follows
                // straight on, it arrives in the same clause -- whatever amount there is belongs to
                // it -- and is handled as itself.
                bool followed = clause.Tokens.Count > 1;

                resolver.MetallicResolver = term is null || followed
                    ? new LiteralResolver<double> { Value = 1 }
                    : new TermResolver<double> { Term = term };

                if (followed)
                {
                    HandleMaterialEntryClause(new Clause
                    {
                        Tag = clause.Tag, Tokens = clause.Tokens[1..], Expressions = clause.Expressions
                    });
                }

                break;
            case "transparency":
                resolver.TransparencyResolver = new TermResolver<double>() { Term = term };
                break;
            case "normal":
                resolver.SurfaceNormalResolver = ParseNormalClause();
                break;
            case "interior":
                ParseInteriorClause(resolver, clause);
                break;
            case "decal":
                resolver.DecalResolvers.Add(
                    ParseObjectResolver<DecalResolver>("decalEntryClause", HandleDecalEntryClause));
                break;
            case "film":
                resolver.FilmResolver =
                    ParseObjectResolver<ThinFilmResolver>("filmEntryClause", HandleFilmEntryClause);
                break;
            case "subsurface":
                resolver.SubsurfaceResolver =
                    ParseObjectResolver<SubsurfaceResolver>("subsurfaceEntryClause", HandleSubsurfaceEntryClause);
                break;
            case "coat":
                resolver.CoatResolver = ParseObjectResolver<CoatResolver>("coatEntryClause", HandleCoatEntryClause);
                break;
            case "sheen":
                resolver.SheenResolver = ParseObjectResolver<SheenResolver>("sheenEntryClause", HandleSheenEntryClause);
                break;
            default:
                throw new Exception($"Internal error: unknown material property found: {field}.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a sheen block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSheenEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid sheen property here.");

        SheenResolver resolver = (SheenResolver) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            case "color":
                resolver.ColorResolver = new CoefficientResolver
                {
                    Term = clause.Term(),
                    Validator = value => value.Red is >= 0 and <= 1 && value.Green is >= 0 and <= 1 &&
                                         value.Blue is >= 0 and <= 1
                        ? null
                        : "A sheen's color is the share of each color its fibers give back, from nothing to all of it."
                };
                break;
            case "roughness":
                resolver.RoughnessResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value is <= 0 or > 1
                        ? "A sheen's roughness runs from just above nothing, a satin's edge, to one, a felt's glow."
                        : null
                };
                break;
            default:
                throw new Exception($"Internal error: unknown sheen property found: {ToCmd(clause)}.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a coat block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleCoatEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid coat property here.");

        CoatResolver resolver = (CoatResolver) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            case "roughness":
                resolver.RoughnessResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value is < 0 or > 1
                        ? "A coat's roughness runs from nothing, a mirror, to one, which is matte."
                        : null
                };
                break;
            case "index":
            case "ior":
                resolver.IndexOfRefractionResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value >= 1 ? null : "A coat's index of refraction must be at least one."
                };
                break;
            case "tint":
                resolver.TintResolver = new CoefficientResolver
                {
                    Term = clause.Term(),
                    Validator = value => value.Red is >= 0 and <= 1 && value.Green is >= 0 and <= 1 &&
                                         value.Blue is >= 0 and <= 1
                        ? null
                        : "A coat's tint is the share of each color it lets through, from nothing to all of it."
                };
                break;
            default:
                throw new Exception($"Internal error: unknown coat property found: {ToCmd(clause)}.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a subsurface block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleSubsurfaceEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid subsurface property here.");

        SubsurfaceResolver resolver = (SubsurfaceResolver) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            // A number is the same distance for every color, and a color -- [1, 0.4, 0.2], say -- one for each.
            case "radius":
                resolver.RadiusResolver = new CoefficientResolver
                {
                    Term = clause.Term(),
                    Validator = value => value.Red > 0 && value.Green > 0 && value.Blue > 0
                        ? null
                        : "How far light travels under a surface must be more than nothing, in every color."
                };
                break;
            case "thickness":
                resolver.ThicknessResolver = new TermResolver<double?>
                {
                    Term = clause.Term(),
                    Validator = value => value > 0 ? null : "A sheet must be more than nothing thick."
                };
                break;
            default:
                throw new Exception($"Internal error: unknown subsurface property found: {ToCmd(clause)}.");
        }
    }

    /// <summary>
    /// This method is used to handle an item clause of a film block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleFilmEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid film property here.");

        ThinFilmResolver resolver = (ThinFilmResolver) _context.CurrentTarget;

        switch (ToCmd(clause))
        {
            case "thickness":
                resolver.ThicknessResolver = Thickness(clause.Term());
                resolver.ThickestResolver = null;
                resolver.PatternResolver = null;

                // A thickness running from one to another needs a pattern to say where it is which.
                if (clause.Expressions.Count > 1)
                {
                    resolver.ThickestResolver = Thickness(clause.Term(1));
                    resolver.PatternResolver = ParseClause("fieldSizedByClause") is { } by
                        ? ParseDensityShapeClause(by)
                        : throw new TokenException(
                            "Expecting \"by\" and the pattern that sets the film's thickness to follow here.")
                        {
                            Token = CurrentParser.PeekNextToken()
                        };
                }

                break;
            case "index":
            case "ior":
                resolver.IndexOfRefractionResolver = new TermResolver<double>
                {
                    Term = clause.Term(),
                    Validator = value => value > 0 ? null : "A film's index of refraction must be more than nought."
                };
                break;
            default:
                throw new Exception($"Internal error: unknown film property found: {ToCmd(clause)}.");
        }

        return;

        static TermResolver<double> Thickness(Term term) => new ()
        {
            Term = term, Validator = value => value >= 0 ? null : "A film's thickness may not be less than nought."
        };
    }

    /// <summary>
    /// This method checks an angle a decal's fade is given.  It is the angle between the surface and
    /// square on to the decal's projection, so it can only lie between square on and edge on.
    /// </summary>
    /// <param name="angle">The angle, in radians.</param>
    /// <returns>An error message, or <c>null</c>, if all is well.</returns>
    private static string FadeAngle(double angle)
    {
        return angle is < 0 or > Math.PI / 2 + 1e-9
            ? "A decal's fade is measured from square on, so its angles must lie between 0 and 90 degrees."
            : null;
    }

    /// <summary>
    /// This method is used to handle an item clause of a decal block.  Anything that is none of a
    /// decal's own properties is read as a transform, which places the decal as a transform places a
    /// surface.
    /// </summary>
    /// <param name="clause">The clause to process, or <c>null</c>, if it may be a transform.</param>
    private void HandleDecalEntryClause(Clause clause)
    {
        DecalResolver resolver = (DecalResolver) _context.CurrentTarget;

        if (clause == null)
        {
            // Handed back the transforms already gathered, so that a property written between two
            // of them does not throw away the first; and a transform clause matches nothing quite
            // happily, so it is how many there are that says whether it read one.
            int gathered = resolver.TransformResolver?.TransformCreators.Count ?? 0;
            TransformResolver transforms = ParseTransformClause(resolver.TransformResolver);

            if (transforms == null || transforms.TransformCreators.Count == gathered)
                throw CreateUnexpectedInputException("Expecting a decal property here.");

            resolver.TransformResolver = transforms;

            return;
        }

        switch (ToCmd(clause))
        {
            case "path":
                resolver.PathResolver = GetPathResolver(clause);
                break;
            case "color":
                resolver.ColorResolver = new TermResolver<Color> { Term = clause.Term() };
                break;
            case "planar":
                resolver.Projection = DecalProjection.Planar;
                break;
            case "cylindrical":
                resolver.Projection = DecalProjection.Cylindrical;
                break;
            case "spherical":
                resolver.Projection = DecalProjection.Spherical;
                break;
            case "toroidal":
                resolver.Projection = DecalProjection.Toroidal;
                resolver.RingRadiusResolver = new TermResolver<double> { Term = clause.Term() };
                break;
            case "min":
                resolver.MinimumResolver = new TermResolver<double> { Term = clause.Term() };
                resolver.MinimumWord = clause.Text(1);
                break;
            case "max":
                resolver.MaximumResolver = new TermResolver<double> { Term = clause.Term() };
                resolver.MaximumWord = clause.Text(1);
                break;
            case "fade":
                resolver.FadeFromResolver = new AngleResolver { Term = clause.Term(), Validator = FadeAngle };
                resolver.FadeToResolver = new AngleResolver { Term = clause.Term(1), Validator = FadeAngle };
                break;
            case "no.fade":
                resolver.NoFade = true;
                break;
            default:
                throw new Exception($"Internal error: unknown decal property found: {clause.Text()}.");
        }
    }

    /// <summary>
    /// This method is used to parse the interior block of a material: what the surface is made of,
    /// rather than what its skin looks like.
    /// </summary>
    /// <param name="resolver">The material resolver to attach the interior to.</param>
    /// <param name="clause">The clause that tells us how to get the interior resolver.</param>
    private void ParseInteriorClause(MaterialResolver resolver, Clause clause)
    {
        // Naming an interior replaces whatever the material had, since the name brings a whole
        // interior with it; writing one out adds to what is there, since a material may open its
        // interior block more than once.
        if (clause.Tokens.Count > 1 && !BounderToken.OpenBrace.Matches(clause.Tokens[1]))
        {
            resolver.InteriorResolver = GetInteriorResolver(clause);

            return;
        }

        resolver.InteriorResolver ??= new InteriorResolver();

        _ = ParseObjectResolver(
            "interiorEntryClause", HandleInteriorEntryClause, resolver.InteriorResolver);
    }

    /// <summary>
    /// This is a helper method for creating the right interior resolver, either by parsing an
    /// in-place definition or a variable reference.  It works much as the material one does, an
    /// interior being the same sort of thing: something worth declaring once and naming later.
    /// </summary>
    /// <param name="clause">The clause that tells us how to get the interior resolver.</param>
    /// <returns>The proper resolver.</returns>
    private InteriorResolver GetInteriorResolver(Clause clause)
    {
        Token token = clause.Tokens[1];

        // Complete definition.
        if (BounderToken.OpenBrace.Matches(token))
        {
            return ParseObjectResolver<InteriorResolver>(
                "interiorEntryClause", HandleInteriorEntryClause);
        }

        if (CallArgumentsIfAny(token, "interior") is { } given)
        {
            InteriorCallResolver call = new ()
            {
                Name = token.Text, Arguments = given, ErrorToken = token
            };

            if (BounderToken.OpenBrace.Matches(CurrentParser.PeekNextToken()))
            {
                CurrentParser.GetNextToken();

                ParseObjectResolver("interiorEntryClause", HandleInteriorEntryClause, call, false);
            }

            return call;
        }

        bool extending = clause.Tokens.Count > 2;
        InteriorResolver resolver = GetExtensibleItem<InteriorResolver>(token, extending);

        if (extending)
            ParseObjectResolver("interiorEntryClause", HandleInteriorEntryClause, resolver);

        return resolver;
    }

    /// <summary>
    /// This method is used to handle an item clause of a material's interior block.
    /// </summary>
    /// <param name="clause">The clause to process.</param>
    private void HandleInteriorEntryClause(Clause clause)
    {
        if (clause == null)
            throw CreateUnexpectedInputException("Expecting a valid interior property here.");

        InteriorResolver resolver = (InteriorResolver) _context.CurrentTarget;
        Term term = clause.Term();

        switch (ToCmd(clause))
        {
            case "index":
            case "ior":
                resolver.IndexOfRefractionResolver = new TermResolver<double> { Term = term };
                break;
            case "filter":
                resolver.FilterResolver = new TermResolver<double> { Term = term };
                break;
            case "clarity":
                resolver.ClarityResolver = new TermResolver<double> { Term = term };
                break;
            case "dispersion":
                resolver.AbbeNumberResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = value => value <= 0
                        ? "An Abbe number must be more than nothing; the lower it is, the wider the rainbow."
                        : null
                };
                break;
            case "glass":
                resolver.GlassResolver = new TermResolver<string>
                {
                    Term = term,
                    Validator = name => Glasses.IsKnown(name)
                        ? null
                        : $"There is no glass named '{name}'; the glasses are {string.Join(", ", Glasses.Names)}."
                };
                break;
            case "medium":
                // A surface is a far side, so anything a medium may say is allowed here.
                resolver.MediumResolver = GetMediumResolver(clause);
                break;
            default:
                throw new NotSupportedException("Unknown interior property found.");
        }
    }
}
