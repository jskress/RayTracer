using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.Instructions;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to parse a function a scene writes for itself.
    /// <para>
    /// There are two quite different things called functions here and it is worth keeping them apart.
    /// The one an isosurface or a density is given is arithmetic over a point in space, compiled down
    /// to something that can be asked about a place millions of times over.  This one is a scene's
    /// own: named, taking values, and worked out wherever an expression may stand.  A leading word
    /// tells them apart, which is why this is written the way it is rather than hung off the
    /// assignment a scene already has.
    /// </para>
    /// </summary>
    /// <param name="clause">The clause that opens the declaration.</param>
    private void HandleStartFunctionClause(Clause clause)
    {
        _context.InstructionContext.AddInstruction(new DeclareFunctionInstruction
        {
            Function = ParseFunctionDeclaration(clause)
        });
    }

    /// <summary>
    /// This method reads a whole function declaration, which is the same work whether it stands at the
    /// top of a file or inside another function.
    /// </summary>
    /// <param name="clause">The clause that opens the declaration.</param>
    /// <returns>The function, as yet belonging to no scope.</returns>
    private UserFunction ParseFunctionDeclaration(Clause clause)
    {
        string name = clause.Tokens[1].Text;
        (List<string> parameterNames, List<Term> defaults) = ParseFunctionParameters();
        Clause kind = ParseClause("functionKindClause");

        if (kind is null)
            throw CreateUnexpectedInputException("Expecting \"->\" and a kind here.");

        FunctionBody body = ParseFunctionBody(name);

        return new UserFunction(name, parameterNames, defaults, kind.Tokens[1].Text, body);
    }

    /// <summary>
    /// This method reads the values a function takes and what each falls back to.
    /// <para>
    /// A value with something to fall back to may not be followed by one without, since a call
    /// leaves values off the end: allowing it would let a scene write a function that could not be
    /// called in the way it appears to promise.
    /// </para>
    /// </summary>
    /// <returns>The names of the values and their fallbacks.</returns>
    private (List<string>, List<Term>) ParseFunctionParameters()
    {
        List<string> names = [];
        List<Term> defaults = [];

        while (true)
        {
            Clause clause = ParseClause("functionParameterClause");

            if (clause is null)
                break;

            string name = clause.Tokens[0].Text;

            if (names.Contains(name))
            {
                throw new TokenException($"The function already takes a value named '{name}'.")
                {
                    Token = clause.Tokens[0]
                };
            }

            Term fallback = clause.Expressions.Count > 0 ? clause.Term() : null;

            if (fallback is null && defaults.Count > 0 && defaults[^1] is not null)
            {
                throw new TokenException(
                    $"'{name}' has nothing to fall back to, but follows one that has; a call leaves " +
                    "values off the end, so those with fallbacks must come last.")
                {
                    Token = clause.Tokens[0]
                };
            }

            names.Add(name);
            defaults.Add(fallback);
        }

        CurrentParser.MatchToken(
            true, () => "Expecting a close parenthesis to end the parameter list.",
            BounderToken.RightParen);

        return (names, defaults);
    }

    /// <summary>
    /// This method reads what a function works out on its way to its answer, and the answer itself.
    /// <para>
    /// Things worked out along the way are what make a function readable and, sometimes, correct: a
    /// figure a body needs in three places should be arrived at once rather than three times, or the
    /// three will drift apart the first time one of them is edited.
    /// </para>
    /// </summary>
    /// <param name="name">The function's name, for complaining with.</param>
    /// <returns>The body, as far as its answer.</returns>
    private FunctionBody ParseFunctionBody(string name)
    {
        FunctionBody body = EndingOf(
            ParseBodySteps(), () => ParseFunctionBody(name),
            steps => new FunctionBody
            {
                Steps = steps, Answer = ParseClause("functionReturnClause").Term()
            });

        CloseTheBody($"'{name}'");

        return body;
    }

    /// <summary>
    /// This method reads the things a body works out on its way to its answer: values it names, and
    /// smaller functions or primitives of its own.
    /// </summary>
    /// <returns>The steps, in the order they were written.</returns>
    private List<FunctionBodyStep> ParseBodySteps()
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
                return steps;

            steps.Add(new FunctionBodyStep(local.Tokens[0].Text, local.Term()));
        }
    }

    /// <summary>
    /// This method reads however a body ends: a choice, a selection, or the plain answer the caller
    /// knows how to read.
    /// </summary>
    /// <param name="steps">What was worked out before the ending.</param>
    /// <param name="readBody">How to read a body of the kind being read.</param>
    /// <param name="readAnswer">How to read a plain answer of the kind being read.</param>
    /// <returns>The body, as far as its answer.</returns>
    private FunctionBody EndingOf(
        List<FunctionBodyStep> steps, Func<FunctionBody> readBody,
        Func<List<FunctionBodyStep>, FunctionBody> readAnswer)
    {
        if (ParseClause("startIfClause") is { } choice)
            return ChoiceOf(choice, steps, readBody);

        if (ParseClause("startSwitchClause") is { } selection)
            return SelectionOf(selection, steps, readBody);

        return readAnswer(steps);
    }

    /// <summary>
    /// This method reads the two ways out of a choice, each being a body in its own right.
    /// <para>
    /// An <c>else</c> followed by another <c>if</c> is read as a choice standing where the second body
    /// would have been, rather than as a body containing one.  The two mean the same thing and the
    /// tree that comes out is the same either way; what it saves the writer is a pair of braces and a
    /// step of indenting per case, which is the difference between a run of cases that can be read
    /// down the page and one that walks off the right of it.
    /// </para>
    /// </summary>
    /// <param name="choice">The clause that opened the choice.</param>
    /// <param name="steps">What was worked out before it.</param>
    /// <param name="readBody">How to read a body of the kind being read.</param>
    /// <returns>The body, ending in that choice.</returns>
    private FunctionBody ChoiceOf(
        Clause choice, List<FunctionBodyStep> steps, Func<FunctionBody> readBody)
    {
        FunctionBody whenTrue = readBody();

        // Insisted on by the clause itself, which is what gives the complaint its wording; there is
        // nothing to test here, since it never comes back without having matched.
        ParseClause("startElseClause");

        Clause chained = ParseClause("startIfClause");
        FunctionBody whenFalse;

        if (chained is null)
        {
            CurrentParser.MatchToken(
                true, () => "Expecting an open brace, or another \"if\", to follow \"else\" here.",
                BounderToken.OpenBrace);

            whenFalse = readBody();
        }
        else
        {
            // The chained one opened its own brace and will close it, so nothing is owed here: this
            // choice stands where a body would, and has no braces to call its own.
            whenFalse = ChoiceOf(chained, [], readBody);
        }

        return new FunctionBody
        {
            Steps = steps,
            Condition = choice.Term(),
            ErrorToken = choice.Tokens[0],
            WhenTrue = whenTrue,
            WhenFalse = whenFalse
        };
    }

    /// <summary>
    /// This method reads a selection: a value, a run of cases held against it, and the default that
    /// catches whatever none of them did.
    /// <para>
    /// What comes back is the run of choices the selection stands for, folded up from the bottom.  A
    /// selection really is the <c>else if</c> chain it looks like -- the value is compared with each
    /// case in turn and the first that matches wins -- so building it that way means nothing new
    /// happens when a scene is rendered, and everything already true of a choice is true of this.
    /// What it buys the writer is that the value is named once instead of once per case, which is both
    /// shorter to write and harder to get wrong.
    /// </para>
    /// </summary>
    /// <param name="selection">The clause that opened the selection.</param>
    /// <param name="steps">What was worked out before it.</param>
    /// <param name="readBody">How to read a body of the kind being read.</param>
    /// <returns>The body, ending in that selection.</returns>
    private FunctionBody SelectionOf(
        Clause selection, List<FunctionBodyStep> steps, Func<FunctionBody> readBody)
    {
        Term subject = selection.Term();
        List<(Term Matches, Token Token, FunctionBody Body)> arms = [];

        while (ParseClause("startCaseClause") is { } arm)
        {
            arms.Add((MatchOf(subject, arm), arm.Tokens[0], readBody()));

            HandleIncludeEnd();
        }

        if (arms.Count == 0)
        {
            throw new TokenException(
                "A selection needs at least one \"case\"; with only a default there is nothing being " +
                "selected, and the body may simply say what it gives back.")
            {
                Token = selection.Tokens[0]
            };
        }

        // Insisted on by the clause itself, so there is nothing to test for here.
        ParseClause("startDefaultClause");

        FunctionBody body = readBody();

        CurrentParser.MatchToken(
            true, () => "Expecting a close brace to end the selection; nothing may follow its " +
                        "\"default\".", BounderToken.CloseBrace);

        // Folded from the bottom up, so that the first case written is the first one asked.
        for (int index = arms.Count - 1; index > 0; index--)
        {
            body = new FunctionBody
            {
                Condition = arms[index].Matches,
                ErrorToken = arms[index].Token,
                WhenTrue = arms[index].Body,
                WhenFalse = body
            };
        }

        return new FunctionBody
        {
            Steps = steps,
            Condition = arms[0].Matches,
            ErrorToken = arms[0].Token,
            WhenTrue = arms[0].Body,
            WhenFalse = body
        };
    }

    /// <summary>
    /// This method builds what one case asks: whether the value equals this, or that, or the other.
    /// </summary>
    /// <param name="subject">The value being selected on.</param>
    /// <param name="arm">The clause that opened the case.</param>
    /// <returns>The question the case asks.</returns>
    private static Term MatchOf(Term subject, Clause arm)
    {
        Term matches = null;

        for (int index = 0; index < arm.Expressions.Count; index++)
        {
            Term one = new ComparisonOperation(subject, arm.Term(index), Comparison.Equal);

            matches = matches is null ? one : new LogicalOperation(matches, one, false);
        }

        return matches;
    }

    /// <summary>
    /// This method matches the brace that ends a body.
    /// </summary>
    /// <param name="what">What is being read, for complaining with.</param>
    private void CloseTheBody(string what)
    {
        CurrentParser.MatchToken(
            true, () => $"Expecting a close brace to end {what}; nothing may follow the answer it " +
                        "gives.", BounderToken.CloseBrace);
    }
}
