using Lex.Expressions;
using Lex.Tokens;
using RayTracer.Instructions;

namespace RayTracer.Parser;

/// <summary>
/// This class is used in concert with the Lex library for creating expressions.
/// </summary>
public class ExpressionTreeBuilder : IExpressionTreeBuilder
{
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
    /// <param name="term">The term the operator should act on.</param>
    /// <param name="isPrefix">A flag that indicates whether the operator preceded the term
    /// or followed it.</param>
    /// <returns>A term that represents a unary operation.</returns>
    public IExpressionTerm CreateUnaryOperation(List<Token> tokens, IExpressionTerm term, bool isPrefix)
    {
        if (isPrefix && OperatorToken.Minus.Matches(tokens[0]))
            return new UnaryMinusTerm((Term) term);

        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
