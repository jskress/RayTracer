using Lex.Clauses;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Graphics;
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
            case "grayscale":
                updater.GrayscaleResolver = new LiteralResolver<bool> { Value = true };
                break;
            case "spectral":
                updater.SpectralResolver = new LiteralResolver<bool> { Value = true };
                break;
            // `white balance <term>`, which arrives as plain "white" for the same reason `scale ambient
            // by` arrives as "scale".
            case "white":
                updater.WhiteBalanceResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = kelvin => kelvin > 0
                        ? null
                        : "A white balance is a temperature in kelvin, and must be above nothing."
                };
                break;
            // `scale ambient by <term>`.  `ToCmd` joins a second word only after `apply`, `no`, `bounded`,
            // `with` or `gives`, so this arrives as plain "scale" -- and `scale` is not a word to add to
            // that list, since it is also the transform keyword and ToCmd serves those too.  The second
            // word is read directly instead, exactly as `medium samples` reads its own.
            case "scale":
                updater.AmbientScaleResolver = new TermResolver<double>
                {
                    Term = term,
                    Validator = value => value < 0
                        ? "Ambient cannot be scaled by less than nothing."
                        : null
                };
                break;
            case "reflection":
                updater.ReflectionSamplesResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value < 1
                        ? "A rough surface must look in at least one direction."
                        : null
                };
                break;
            case "weathering":
                updater.WeatheringSamplesResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value < 1
                        ? "A weathering pattern must look about a place in at least one direction."
                        : null
                };
                break;
            case "subsurface":
                updater.SubsurfaceSamplesResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value < 1
                        ? "A surface whose light comes up from under it must take at least one walk."
                        : null
                };
                break;
            case "film":
                updater.FilmCurveResolver = new LiteralResolver<FilmCurve> { Value = FilmCurve.Named(clause.Text(2)) };
                break;
            case "trace":
                updater.TraceDepthResolver = new TermResolver<int?>
                {
                    Term = term,
                    Validator = value => value < 1
                        ? "A ray from the eye must be followed through at least one surface."
                        : null
                };
                break;
            case "caustics":
                updater.CausticPhotonsResolver = term is null
                    ? new LiteralResolver<int> { Value = General.RenderContext.DefaultCausticPhotons }
                    : new TermResolver<int>
                    {
                        Term = term,
                        Validator = value => value < 1
                            ? "Caustics need at least one photon."
                            : null
                    };
                break;
            case "animation":
                CreateAnimationResolvers(clause, updater);
                break;
            case "gravity":
                updater.GravityResolver = new TermResolver<Vector> { Term = term };
                break;
            case "wind":
                updater.WindResolver = new TermResolver<Vector> { Term = term };
                break;
            case "diffuse":
                if (clause.Text(1) == "cache")
                {
                    updater.DiffuseCacheResolver = term is null
                        ? new LiteralResolver<double> { Value = General.RenderContext.DefaultDiffuseCacheError }
                        : new TermResolver<double>
                        {
                            Term = term,
                            Validator = value => value is <= 0 or >= 1
                                ? "A cache's error is a share of the light, more than nothing and less than one."
                                : null
                        };

                    break;
                }

                if (clause.Text(1) == "bounces")
                {
                    updater.DiffuseBouncesResolver = new TermResolver<int>
                    {
                        Term = term,
                        Validator = value => value < 0
                            ? "Light cannot bounce fewer than no times."
                            : null
                    };

                    break;
                }

                updater.DiffuseSamplesResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value < 1
                        ? "A matte surface must look in at least one direction for the light bounced onto it."
                        : null
                };
                break;
            case "medium":
                if (clause.Text(1) == "bounces")
                {
                    updater.MediumBouncesResolver = new TermResolver<int>
                    {
                        Term = term,
                        Validator = value => value < 0
                            ? "A path cannot be turned fewer than no times."
                            : null
                    };

                    break;
                }

                updater.MediumSamplesResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value < 1
                        ? "A medium must be sampled in at least one place."
                        : null
                };
                break;
            case "color":
                updater.BitsPerChannelResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value is not 8 and not 16
                        ? "A color channel must be either 8 or 16 bits deep."
                        : null
                };
                break;
            case "antialiasing":
                if (clause.Text(1) == "threshold")
                {
                    updater.AntiAliasingThresholdResolver = new TermResolver<double>
                    {
                        Term = term,
                        Validator = value => value is <= 0 or > 1
                            ? "The antialiasing threshold must be more than nought and no more " +
                              "than one."
                            : null
                    };

                    break;
                }

                updater.AntiAliasingDepthResolver = new TermResolver<int>
                {
                    Term = term,
                    Validator = value => value < 0
                        ? "A pixel cannot be looked into fewer than no times."
                        : null
                };
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
                    Validator = value => value is <= 0 or > 5 ? "Gamma correction must be more than 0, and no more than 5." : null
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

    // `animation length <seconds> [rate <r>] [still <frame>]` or `animation frames <count> [rate <r>] [still <frame>]`.
    private static void CreateAnimationResolvers(Clause clause, ContextUpdater contextUpdater)
    {
        bool inFrames = clause.Text(1) == "frames";

        contextUpdater.AnimationRunResolver = new TermResolver<double>
        {
            Term = clause.Term(),
            Validator = value => value > 0
                ? null
                : inFrames ? "An animation must run at least one frame." : "An animation must run longer than nothing."
        };
        contextUpdater.AnimationInFrames = inFrames;

        // The rate and the still are each optional, so which expression is which is told by the words before them.
        int next = 1;

        if (clause.Tokens.Any(token => token.Text == "rate"))
        {
            contextUpdater.AnimationRateResolver = new TermResolver<double>
            {
                Term = clause.Term(next++),
                Validator = value => value > 0 ? null : "An animation's rate must be more than nothing a second."
            };
        }

        if (clause.Tokens.Any(token => token.Text == "still"))
        {
            contextUpdater.AnimationStillResolver = new TermResolver<double>
            {
                Term = clause.Term(next),
                Validator = value => value >= 0 ? null : "An animation's still is one of its frames, from nought."
            };
        }
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
