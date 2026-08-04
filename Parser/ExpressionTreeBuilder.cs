using Lex.Expressions;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class is used in concert with the Lex library for creating expressions.
/// </summary>
public class ExpressionTreeBuilder : IExpressionTreeBuilder
{
    private const string Square = "\u00b2";
    private const string Cube = "\u00b3";
    private const string SquareRoot = "\u221a";
    private const string CubeRoot = "\u221b";
    private const string DegreeSign = "\u00b0";

    /// <summary>
    /// The superscripts that raise what precedes them to a power, and the power each one means.
    /// Two and three are absent because they were here first, as their own operations, and those
    /// also square and cube a color or a matrix, which a power of a number cannot.
    /// </summary>
    private static readonly Dictionary<string, double> Powers = new ()
    {
        { "\u2070", 0 }, { "\u00b9", 1 }, { "\u2074", 4 }, { "\u2075", 5 },
        { "\u2076", 6 }, { "\u2077", 7 }, { "\u2078", 8 }, { "\u2079", 9 }
    };

    // The spellings each operation arrives in.  One operation reaches a scene as several code
    // points, since which one a formula carries depends on where it was copied from, and a reader
    // cannot tell them apart by looking; a minus sign, an en dash and a hyphen all subtract.
    private static readonly HashSet<string> Minuses = ["\u2212", "\u2013"];
    private static readonly HashSet<string> Multiplies = ["\u2217", "\u22c6"];
    private static readonly HashSet<string> Divides = ["\u00f7", "\u2215", "\u2044"];
    private static readonly HashSet<string> DotProducts = ["\u22c5", "\u00b7", "\u2219", "\u2022"];
    private static readonly HashSet<string> CrossProducts = ["\u00d7", "\u2a2f"];
    private static readonly HashSet<string> Ands = ["&&", "\u2227", "and"];
    private static readonly HashSet<string> Ors = ["||", "\u2228", "or"];
    private static readonly HashSet<string> Nots = ["!", "\u00ac", "not"];

    /// <summary>
    /// The ways two values may be compared, and what each is written as.
    /// </summary>
    private static readonly Dictionary<string, Comparison> Comparisons = new ()
    {
        { "<", Comparison.Less },
        { "<=", Comparison.LessOrEqual },
        { "\u2264", Comparison.LessOrEqual },
        { ">", Comparison.Greater },
        { ">=", Comparison.GreaterOrEqual },
        { "\u2265", Comparison.GreaterOrEqual },
        { "==", Comparison.Equal },
        { "!=", Comparison.NotEqual },
        { "\u2260", Comparison.NotEqual }
    };

    /// <summary>
    /// This method is used to create a term in an expression tree.  It is provided the
    /// list of tokens that are not part of a sub-expression or decoration tokens and the
    /// list of any sub-expressions.
    /// </summary>
    /// <param name="tokens">The list of relevant tokens that make up the term.</param>
    /// <param name="expressions">The list of any sub-expression objects.</param>
    /// <param name="tag">The tag that goes with the type of term parsed.</param>
    /// <returns>The created term.</returns>
    public IExpressionTerm CreateTerm(List<Token> tokens, List<IExpressionTerm> expressions, string tag)
    {
        List<Term> terms = expressions.Cast<Term>().ToList();
        Token token = tokens.First();

        return tag switch
        {
            "tuple" => new TupleTerm(token, terms),
            "call" => new FunctionCallTerm(token, terms),
            "number" => LiteralTerm.CreateLiteralTerm(token),
            "string" => LiteralTerm.CreateLiteralTerm(token),
            "variable" => new VariableTerm(token),
            _ => LiteralTerm.CreateLiteralTerm(token)
        };
    }

    /// <summary>
    /// This method is used to create a term that represents a unary operation.
    /// </summary>
    /// <param name="tokens">The list of tokens that define the operator.</param>
    /// <param name="expressionTerm">The expression term the operator should act on.</param>
    /// <param name="isPrefix">A flag that indicates whether the operator preceded the term
    /// or followed it.</param>
    /// <returns>A term that represents a unary operation.</returns>
    public IExpressionTerm CreateUnaryOperation(
        List<Token> tokens, IExpressionTerm expressionTerm, bool isPrefix)
    {
        Token token = tokens[0];
        string text = token.Text;
        Term term = (Term) expressionTerm;

        if (!isPrefix && (text == Square || text == Cube || Powers.ContainsKey(text)))
            RejectAStackOfPowers(token, term);

        if (!isPrefix && Powers.TryGetValue(text, out double power))
        {
            return new FunctionCallTerm(token, "pow",
                [term, LiteralTerm.Of(token, power)]);
        }

        return isPrefix switch
        {
            true when Nots.Contains(text) => new NotOperation(term),
            true when text == OperatorToken.Minus.Text || Minuses.Contains(text) =>
                new UnaryMinusOperation(term),
            true when text == SquareRoot => new FunctionCallTerm(token, "sqrt", [term]),
            true when text == CubeRoot => new FunctionCallTerm(token, "cbrt", [term]),
            false when text == Square => new SquareOperation(term, token),
            false when text == Cube => new CubeOperation(term, token),
            false when text == DegreeSign || text == "degrees" => new AngleOperation(term, true),
            false when text == "radians" => new AngleOperation(term, false),
            true when text == OperatorToken.Dollar.Text => new StringSubstitutionOperation(term),
            true when text == "color" => new UnaryCastOperation<Color>(term),
            true when text == "point" => new UnaryCastOperation<Point>(term),
            true when text == "vector" => new UnaryCastOperation<Vector>(term),
            _ => throw new Exception(
                $"Internal error: cannot interpret unary operator of " +
                $"{string.Join(' ', tokens.Select(token => token.Text))}.")
        };
    }

    /// <summary>
    /// This method is used to turn away a power written directly against another one.  Each
    /// superscript is its own operator, so <c>x¹⁰</c> would read as <c>x</c> to the first and then
    /// that to the zeroth -- which is 1, and is certainly not what was meant by it.
    /// <para>
    /// What is refused is the two superscripts standing side by side, since that is how a power of
    /// more than one digit is written and there is nothing else it could be.  A power raised to a
    /// power in its own right is perfectly writable, with the parentheses that say so:
    /// <c>(x²)³</c> is fine, and so is <c>x² ³</c> if anyone cares to write it that way.  That is
    /// why the powers report against the operator rather than against what it acts on -- where the
    /// two superscripts sit is the only thing that tells these cases apart.
    /// </para>
    /// </summary>
    /// <param name="token">The superscript token to report against.</param>
    /// <param name="term">The term the superscript was written on.</param>
    private static void RejectAStackOfPowers(Token token, Term term)
    {
        bool isAlreadyAPower = term is SquareOperation or CubeOperation ||
                               term is FunctionCallTerm call && call.IsCallTo("pow");
        Token inner = term.ErrorToken;

        if (isAlreadyAPower && inner.Line == token.Line && inner.Column + 1 == token.Column)
        {
            throw new TokenException(
                "One power cannot be written directly against another, since a power of more than " +
                "one digit would read as several powers instead.  Use pow(value, exponent) here, " +
                "or parentheses if a power of a power is really meant.")
            {
                Token = token
            };
        }
    }

    /// <summary>
    /// This method is used to create a term that represents a binary operation.
    /// </summary>
    /// <param name="tokens">The list of tokens that define the operator.</param>
    /// <param name="left">The left-hand term the operator should act on.</param>
    /// <param name="right">The right-hand term the operator should act on.</param>
    /// <returns>A term that represents a binary operation.</returns>
    public IExpressionTerm CreateBinaryOperation(List<Token> tokens, IExpressionTerm left, IExpressionTerm right)
    {
        Token operation = tokens[0];
        Term leftTerm = (Term) left;
        Term rightTerm = (Term) right;

        if (OperatorToken.Plus.Matches(operation))
            return new BinaryPlusOperation(leftTerm, rightTerm);

        if (OperatorToken.Minus.Matches(operation) || Minuses.Contains(operation.Text))
            return new BinaryMinusOperation(leftTerm, rightTerm);

        if (OperatorToken.Multiply.Matches(operation) || Multiplies.Contains(operation.Text))
            return new BinaryMultiplyOperation(leftTerm, rightTerm);

        if (OperatorToken.Divide.Matches(operation) || Divides.Contains(operation.Text))
            return new BinaryDivideOperation(leftTerm, rightTerm);

        if (OperatorToken.Modulo.Matches(operation))
            return new BinaryModuloOperation(leftTerm, rightTerm);

        if (DotProducts.Contains(operation.Text))
            return new VectorProductOperation(leftTerm, rightTerm, false);

        if (CrossProducts.Contains(operation.Text))
            return new VectorProductOperation(leftTerm, rightTerm, true);

        if (Comparisons.TryGetValue(operation.Text, out Comparison comparison))
            return new ComparisonOperation(leftTerm, rightTerm, comparison);

        if (Ands.Contains(operation.Text))
            return new LogicalOperation(leftTerm, rightTerm, true);

        if (Ors.Contains(operation.Text))
            return new LogicalOperation(leftTerm, rightTerm, false);

        throw new Exception(
            $"Internal error: cannot interpret binary operator of " +
            $"{string.Join(' ', tokens.Select(token => token.Text))}.");
    }

    /// <summary>
    /// This method is used to create a term that represents a trinary operation.
    /// </summary>
    /// <param name="leftTokens">The list of tokens that define the left operator.</param>
    /// <param name="rightTokens">The list of tokens that define the right operator.</param>
    /// <param name="left">The left-hand term the operator should act on.</param>
    /// <param name="middle">The middle term the operator should act on.</param>
    /// <param name="right">The right-hand term the operator should act on.</param>
    /// <returns>A term that represents a trinary operation.</returns>
    public IExpressionTerm CreateTrinaryOperation(List<Token> leftTokens, List<Token> rightTokens, IExpressionTerm left, IExpressionTerm middle,
        IExpressionTerm right)
    {
        return new ConditionalOperation(
            leftTokens[0], (Term) left, (Term) middle, (Term) right);
    }
}
