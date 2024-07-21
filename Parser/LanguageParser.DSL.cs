using Lex.Dsl;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL
/// </summary>
public partial class LanguageParser
{
    private const string LanguageDslSpecification = """"
        _parserSpec: """
            standard comments
            dsl keywords
            identifiers starting with defaults, greekLetters containing defaults, greekLetters
            single quoted strings multiChar
            double quoted strings
            tripple quoted strings
            numbers
            bounders
            dsl operators
            whitespace
            """
        _keywords: 'ambient', 'angles', 'at', 'are', 'author', 'bits', 'bounding', 'box',
            'camera', 'channel', 'checker', 'closed', 'color', 'comment', 'conic', 'context',
            'copyright', 'cube', 'cylinder', 'degrees', 'description', 'difference',
            'diffuse', 'disclaimer', 'false', 'field', 'file', 'gamma', 'gradient',
            'grayscale', 'group', 'include', 'index', 'info', 'intersection', 'ior', 'light',
            'line', 'linear', 'location', 'look', 'material', 'max', 'maximum', 'min',
            'minimum', 'named', 'null', 'object', 'of', 'open', 'parallel', 'per',
            'pigmentation', 'pixel', 'plane', 'point', 'radians', 'reflective', 'refraction',
            'ring', 'rotate', 'scale', 'scanner', 'serial', 'shear', 'shininess', 'smooth',
            'software', 'source', 'specular', 'sphere', 'stripe', 'title', 'translate',
            'transparency', 'triangle', 'true', 'union', 'up', 'vector', 'view', 'warning',
            'width', 'X', 'Y', 'Z'
        _operators: predefined
        squared: _operator("\u00b2")
        cubed: _operator("\u00b3")

        _expressions:
        {
            term: [
                true, false, null,
                openBracket _expression(3..4, comma) /closeBracket => 'tuple',
                _number => 'number',
                _string => 'string',
                _identifier => 'variable'
            ]
            unary: [
                not*, *squared, *cubed, dollar*, point*, color*, vector*
            ]
            binary: [
                plus, minus, multiply, divide, modulo
            ]
        }

        includeClause: { include > _string ?? 'Expecting a string to follow "include" here.' }
        namedClause: { named > _expression }

        startInfoClause:
        {
            info > openBrace ?? 'Expecting an open brace to follow "info" here.'
        }
        infoEntryClause:
        {
            [ title | author | description | copyright | software | disclaimer | warning |
              source | comment ] ?? 'Expecting an info property here.' >
            _expression
        }

        startContextClause:
        {
            context > openBrace ?? 'Expecting an open brace to follow "context" here.'
        }
        scannerClause:
        [
            { serial > scanner ?? 'Expecting "scanner" to follow "serial" here.' } |
            { 
                parallel >
                [ line | pixel ] ?? 'Expecting "line" or "pixel" to follow "parallel" here.' >
                scanner ?? 'Expecting "scanner" to follow "serial" here.'
            }
        ]
        anglesClause:
        {
            angles > are ?? 'Expecting "are" to follow "angles" here.' >
            [ degrees | radians ] ?? 'Expecting "degrees" or "radians" to follow "are" here.'
        }
        contextPropertyClause:
        {
            [ width | height | gamma ] ?? 'Expecting a context block item here.` >
            _expression
        }
        contextEntryClause:
        [
            startInfoClause => 'HandleStartInfoClause' |
            scannerClause => 'HandleScannerClause' |
            anglesClause => 'HandleAnglesClause' |
            contextPropertyClause => 'HandleContextPropertyClause'
        ] ?? 'Expecting a context property here.'

        startCameraClause:
        {
            camera > openBrace ?? 'Expecting an open brace to follow "camera" here.'
        }
        locationClause: { location > _expression }
        lookAtClause: { look > at ?? 'Expecting "at" to follow "look" here.' > _expression }
        upClause: { up > _expression }
        fieldOfViewClause:
        {
            field > of ?? 'Expecting "of" to follow "field" here.' >
            view ?? 'Expecting "view" to follow "of" here.' >
            _expression
        }
        cameraEntryClause:
        [
            namedClause | locationClause | lookAtClause | upClause | fieldOfViewClause
        ] ?? 'Expecting a camera property here.'

        startPointLightClause:
        {
            [ { point > light } | light ] > openBrace ?? 'Expecting an open brace to follow "light" here.'
        }
        lightColorClause: { color > _expression }
        pointLightEntryClause:
        [
            namedClause | locationClause | lightColorClause
        ] ?? 'Expecting a point light property here.'
        
        // Top-level clause.
        [
            startContextClause    => 'HandleStartContextClause' |
            startCameraClause     => 'HandleStartCameraClause' |
            startPointLightClause => 'HandleStartPointLightClause'
        ] ?? 'Unsupported object type found.'
        """";

    private static readonly Dsl LanguageDsl = LexicalDslFactory.CreateFrom(LanguageDslSpecification);

    static LanguageParser()
    {
        LanguageDsl.ExpressionParser.TreeBuilder = new ExpressionTreeBuilder();
    }
}
