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
            triple quoted strings
            numbers
            bounders
            dsl operators
            whitespace
            """
        _operators: predefined
        squared: _operator("\u00b2")
        cubed: _operator("\u00b3")
        _keywords: 'ambient', 'angles', 'apply', 'at', 'are', 'author', 'background',
            'bits', 'blend', 'bouncing', 'bounded', 'by', 'camera', 'channel', 'checker',
            'closed', 'color', 'comment', 'conic', 'context', 'copyright', 'csg', 'cube',
            'cylinder', 'degrees', 'description', 'difference', 'diffuse', 'disclaimer',
            'false', 'field', 'file', 'from', 'gamma', 'gradient', 'grayscale', 'group',
            'height', 'include', 'index', 'info', 'inherited', 'intersection', 'ior',
            'light', 'line', 'linear', 'location', 'look', 'material', 'matrix', 'max',
            'maximum', 'min', 'minimum', 'named', 'no', 'normals', 'null', 'object', 'of',
            'open', 'parallel', 'per', 'pigment', 'pixel', 'plane', 'point', 'radial',
            'radians', 'reflective', 'refraction', 'render', 'report', 'ring', 'rotate',
            'scale', 'scanner', 'scene', 'serial', 'shadow', 'shadows', 'shear', 'shininess',
            'smooth', 'software', 'source', 'specular', 'sphere', 'stripe', 'title', 'to',
            'transform', 'translate', 'transparency', 'triangle', 'true', 'union', 'up',
            'vector', 'view', 'warning', 'width', 'with', 'X', 'Y', 'Z'

        _expressions:
        {
            term: [
                true, false, null,
                openBracket _expression(3..4, comma) /closeBracket => 'tuple',
                _number => 'number',
                _string => 'string',
                _identifier => 'variable',
                _keyword => 'variable'
            ]
            unary: [
                not*, minus*, *squared, *cubed, dollar*, color*, point*, vector*
            ]
            binary: [
                plus, minus, multiply, divide, modulo
            ]
        }

        includeClause: { include > _string ?? 'Expecting a string to follow "include" here.' }
        namedClause: { named > _expression }
        intervalClause:
        {
            [ leftParen | openBracket ] > _expression >
            comma ?? 'Expecting a comma here.' > _expression >
            [ closeBracket | rightParen ] ?? 'Expecting a close bracket or right parenthesis here.'
        }

        // Info clauses.
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

        // Context clauses.
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
        settingOnClause:
        {
            apply > gamma ?? 'Expecting "gamma" to follow "apply" here.'
        }
        settingOffClause:
        {
            no > [ gamma | shadows ] ?? 'Expecting "gamma" or "shadows" to follow "no" here.'
        }
        reportGammaClause: { report > gamma ?? 'Expecting "gamma" to follow "report" here.' }
        contextPropertyClause:
        {
            [ width | height | gamma ] ?? 'Expecting a context block item here.' >
            _expression
        }
        contextEntryClause:
        [
            startInfoClause | scannerClause | anglesClause | settingOnClause |
            settingOffClause | contextPropertyClause
        ] ?? 'Expecting a context property here.'

        // Camera clauses.
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

        // Point light clauses.
        startPointLightClause:
        {
            [ { point > light } | light ] > openBrace ?? 'Expecting an open brace to follow "light" here.'
        }
        lightColorClause: { color > _expression }
        pointLightEntryClause:
        [
            namedClause | locationClause | lightColorClause
        ] ?? 'Expecting a point light property here.'

        // Transform clauses.
        axisClause: { [ X | Y | Z ] > _expression }
        transformByClause: [ axisClause | _expression ]
        translateClause: { translate > transformByClause }
        scaleClause: { scale > transformByClause }
        rotateClause: { rotate > axisClause ?? 'Expecting "X", "Y" or "Z" after "rotate" here.' }
        shearClause:
        {
            shear > openBracket ?? 'Expecting an open bracket to follow "shear" here.' >
            { _expression > comma ?? 'Expecting a comma here.' }{5..5} >
            _expression > closeBracket ?? 'Expecting a close bracket here.'
        }
        matrixClause:
        {
            matrix > openBracket ?? 'Expecting an open bracket to follow "matrix" here.' >
            { _expression > comma ?? 'Expecting a comma here.' }{15..15} >
            _expression > closeBracket ?? 'Expecting a close bracket here.'
        }
        transformClause:
        [
            translateClause | scaleClause | rotateClause | shearClause | matrixClause
        ]{*}

        // Pigment clauses.
        linearGradientClause:
        {
            bouncing{?} > linear > gradient ?? 'Expecting "gradient" to follow "linear" here.'
        }
        radialGradientClause:
        {
            bouncing{?} > radial > gradient ?? 'Expecting "gradient" to follow "radial" here.'
        }
        startPigmentPairClause:
        {
            [ checker | ring | stripe | blend | linearGradientClause | radialGradientClause ] >
            openBrace ?? 'Expecting an open brace to follow pigment type here.'
        }
        pigmentClause:
        [
            startPigmentPairClause |
            { color > _expression } |
            _expression
        ] ?? 'Expecting a pigment definition here.'

        // Material clauses.
        startMaterialClause:
        {
            material > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "material" here.'
        }
        materialValueClause:
        {
            [ ambient | diffuse | specular | shininess | reflective | transparency ] >
            _expression
        }
        materialIorClause:
        {
            [ {
                index > of ?? 'Expecting "of" to follow "index" here.' >
                refraction ?? 'Expecting "refraction" to follow "of" here.'
            } | ior ] > _expression
        }
        materialEntryClause:
        [
            pigment | materialValueClause | materialIorClause
        ] ?? 'Expecting a material property here.'

        // Common surface clauses.
        noShadowClause:
        {
            no > shadow ?? 'Expecting "shadow" to follow "no" here.'
        }
        surfaceTransformClause:
        {
            transform > _identifier ?? 'Expecting an identifier to follow "transform" here.' >
            openBrace{?}
        }
        surfaceEntryClause:
        [
            namedClause | startMaterialClause | surfaceTransformClause | noShadowClause
        ]
        
        // Plane clause.
        startPlaneClause:
        {
            plane > [ _identifier | _keyword ]{?} >
            openBrace ?? 'Expecting an open brace to follow "plane" here.'
        }
        
        // Sphere clause.
        startSphereClause:
        {
            sphere > [ _identifier | _keyword ]{?} >
            openBrace ?? 'Expecting an open brace to follow "sphere" here.'
        }

        // Cube clause.
        startCubeClause:
        {
            cube > [ _identifier | _keyword ]{?} >
            openBrace ?? 'Expecting an open brace to follow "cube" here.'
        }
        
        // Circular surface clauses.
        startCircularSurfaceClause:
        {
            open{?} > [ cylinder | conic ] > [ _identifier | _keyword ]{?} >
            openBrace ?? 'Expecting an open brace here.'
        }
        circularSurfaceEntryClause:
        [
            { [ min | max ] > Y ?? 'Expecting "X" or "Y" to follow "max" here.' > _expression } |
            surfaceEntryClause
        ]

        // Triangle clauses.
        tripleTupleClause:
        {
            leftParen > _expression > comma ?? 'Expecting a comma here.' > _expression >
            comma ?? 'Expecting a comma here.' > _expression >
            rightParen ?? 'Expecting a right parenthesis here.'
        }
        startTriangleClause:
        {
            triangle > [
                { [ _identifier | _keyword ] > openBrace{?} } |
                { tripleTupleClause > openBrace ?? 'Expecting an open brece here.' }
            ] ?? 'Expectinbg a triangle specification or variable reference here.'
        }
        startSmoothTriangleClause:
        {
            smooth > triangle > [
               { [ _identifier | _keyword ] > openBrace{?} } |
               { tripleTupleClause > normals ?? 'Expecting "normals" here.' >
                 tripleTupleClause > openBrace ?? 'Expecting an open brece here.' }
            ] ?? 'Expectinbg a smooth triangle specification or variable reference here.'
        }

        // Object file clause.
        startObjectFileClause:
        {
            object > file > _expression > openBrace ?? 'Expecting an open brece here.'
        }

        // Object clause.
        startObjectClause:
        {
            object >
            [ _identifier | _keyword ] ?? 'Expecting an identifier or keyword after "object" here.' >
            openBrace{?}
        }

        // CSG clauses.
        startCsgClause:
        [
            { [ union | difference | intersection ] > openBrace ?? 'Expecting an open brece here.' } |
            {
                csg >
                [ _identifier | _keyword ] ?? 'Expecting an identifier or keyword after "csg" here.' >
                openBrace{?}
            }
        ]
        csgEntryClause:
        [
            startPlaneClause => 'plane' |
            startSphereClause => 'sphere' |
            startCubeClause => 'cube' |
            startCircularSurfaceClause => 'circularSurface' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startObjectFileClause => 'objectFile' |
            startObjectClause => 'object' |
            startCsgClause => 'csg' |
            startGroupClause => 'group' |
            surfaceEntryClause => 'surface'
        ]
        
        // Group clauses.
        groupIntervalClause:
        {
            { [ _identifier | _keyword ] > assignment }{?} > intervalClause >
            { by > _expression }{?}
        }
        groupBoundedByClause:
        {
            bounded > by ?? 'Expecting "by" to follow "bounded" here.' > _expression >
            comma ?? 'Expecting a comma here.' > _expression
        }
        startGroupClause:
        {
            group > [
                { groupIntervalClause{?} > openBrace ?? 'Expecting an open brace here.' } |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting a group or variable reference.'
        }
        groupEntryClause:
        [
            startPlaneClause => 'plane' |
            startSphereClause => 'sphere' |
            startCubeClause => 'cube' |
            startCircularSurfaceClause => 'circularSurface' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startObjectFileClause => 'objectFile' |
            startObjectClause => 'object' |
            startCsgClause => 'csg' |
            startGroupClause => 'group' |
            groupBoundedByClause => 'boundingBox' |
            surfaceEntryClause => 'surface'
        ]

        // Scene clauses.
        startSceneClause:
        {
            scene > openBrace ?? 'Expecting an open brace to follow "scene" here.'
        }
        sceneEntryClause:
        [
            namedClause | startCameraClause | startPointLightClause | startPlaneClause |
            startSphereClause | startCubeClause | startCircularSurfaceClause |
            startTriangleClause | startSmoothTriangleClause | startObjectFileClause |
            startObjectClause | startCsgClause | startGroupClause | background
        ] ?? 'Unsupported scene property found.'

        renderClause:
        {
            render > { scene > _expression }{?} > {
                with > camera ?? 'Expecting "camera" to follow "with" here.' > 
                _expression
            }{?}
        }

        // Variable clauses.
        startThingClause:
        [
            { [ _identifier | _keyword ] > openBrace{?} } |
            openBrace ?? 'Expecting an open brace here'
        ]
        setThingToVariable:
        {
            [ _identifier | _keyword ] > assignment >
            [
                pigment | material | transform | startPlaneClause | startPlaneClause |
                startSphereClause | startCubeClause | startCircularSurfaceClause |
                startTriangleClause | startSmoothTriangleClause | startObjectFileClause |
                startObjectClause | startCsgClause | startGroupClause
            ]
        }
        setVariableClause:
        {
            [ _identifier | _keyword ] > assignment > _expression
        }

        // Top-level clause.
        [
            startContextClause         => 'HandleStartContextClause' |
            startSceneClause           => 'HandleStartSceneClause' |
            startCameraClause          => 'HandleStartCameraClause' |
            startPointLightClause      => 'HandleStartPointLightClause' |
            startPlaneClause           => 'HandleStartPlaneClause' |
            startSphereClause          => 'HandleStartSphereClause' |
            startCubeClause            => 'HandleStartCubeClause' |
            startCircularSurfaceClause => 'HandleStartCircularSurfaceClause' |
            startTriangleClause        => 'HandleStartTriangleClause' |
            startSmoothTriangleClause  => 'HandleStartSmoothTriangleClause' |
            startObjectFileClause      => 'HandleStartObjectFileClause' |
            startObjectClause          => 'HandleStartObjectClause' |
            startCsgClause             => 'HandleStartCsgClause' |
            startGroupClause           => 'HandleStartGroupClause' |
            background                 => 'HandleBackgroundClause' |
            renderClause               => 'HandleRenderClause' |
            setThingToVariable         => 'HandleSetThingToVariableClause' |
            setVariableClause          => 'HandleSetVariableClause'
        ] ?? 'Unsupported object type found.'
        """";

    private static readonly Dsl LanguageDsl = LexicalDslFactory.CreateFrom(LanguageDslSpecification);

    static LanguageParser()
    {
        LanguageDsl.ExpressionParser.TreeBuilder = new ExpressionTreeBuilder();
    }
}
