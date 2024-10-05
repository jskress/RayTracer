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

        _keywords: 'agate', 'ambient', 'angles', 'apply', 'at', 'are', 'author',
            'background', 'banded', 'bits', 'blend', 'bouncing', 'bounded', 'boxed',
            'brick', 'by', 'camera', 'channel', 'checker', 'close', 'color', 'comment',
            'conic', 'context', 'copyright', 'csg', 'cube', 'cubic', 'curve', 'cylinder',
            'cylindrical', 'degrees', 'dents', 'description', 'difference', 'diffuse',
            'disclaimer', 'extrusion', 'false', 'field', 'file', 'from', 'gamma',
            'gradient', 'granite', 'grayscale', 'group', 'height', 'hexagon', 'include',
            'index', 'info', 'inherited', 'intersection', 'ior', 'layer', 'leopard',
            'light', 'line', 'linear', 'location', 'look', 'material', 'matrix', 'max',
            'maximum', 'min', 'minimum', 'mortar', 'move', 'named', 'no', 'noisy',
            'normals', 'null', 'object', 'of', 'open', 'parallel', 'parallelogram',
            'path', 'phased', 'pigment', 'pixel', 'planar', 'plane', 'point', 'points',
            'quad', 'radians', 'radii', 'reflective', 'refraction', 'render', 'report',
            'rotate', 'scale', 'scanner', 'scene', 'serial', 'shadow', 'shadows', 'shear',
            'shininess', 'sides', 'size', 'smooth', 'software', 'source', 'specular',
            'sphere', 'spherical', 'square', 'stripes', 'svg', 'title', 'to', 'torus',
            'transform', 'translate', 'transparency', 'triangle', 'triangular', 'true',
            'turbulence', 'union', 'up', 'vector', 'view', 'warning', 'width', 'with',
            'wrinkles', 'X', 'Y', 'Z'

        _expressions:
        {
            term: [
                true, false, null,
                openBracket _expression(2..4, comma) /closeBracket => 'tuple',
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
        turbulenceClause:
        {
            turbulence > _expression >
            { phased > _expression > { comma > _expression }{?} }{?}
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
        
        // Pattern clauses.
        patternClause:
        [
            agate | boxed | brick | checker | cubic | dents | granite | hexagon | leopard |
            planar | square | triangular | wrinkles |
            {
                [ { linear > [ X | Y | Z ]{?} } | cylindrical | spherical ] >
                [ stripes | { bouncing{?} > gradient } ] ?? 'Expecting "stripes" or "gradient" here.'
            }
        ]
        brickSizeClause:
        {
            [ brick | mortar ] > size > _expression
        }

        // Pigment clauses.
        pigmentMapClause:
        {
            banded{?} > openBracket ?? 'Expecting a pigment map here.'
        }
        blendPigmentClause:
        {
            [ blend | layer ] > openBrace ?? 'Expecting an open brace to follow "blend" or "layer" here.'
        }
        noisyPigmentClause:
        {
            noisy > openBrace ?? 'Expecting an open brace to follow "noisy" here.'
        }
        patternPigmentClause:
        {
            patternClause > openBrace ?? 'Expecting an open brace to follow "noisy" here.'
        }
        pigmentClause:
        [
            blendPigmentClause | noisyPigmentClause | patternPigmentClause |
            { color > _expression } |
            _expression
        ] ?? 'Expecting a pigment definition here.'

        // Material clauses.
        startMaterialClause:
        {
            material > [
                openBrace | inherited |
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
            transform > [ _identifier | _keyword ] ?? 'Expecting an identifier to follow "transform" here.' >
            openBrace{?}
        }
        boundedByClause:
        {
            bounded > by ?? 'Expecting "by" to follow "bounded" here.' > _expression >
            comma ?? 'Expecting a comma here.' > _expression
        }
        surfaceEntryClause:
        [
            namedClause | startMaterialClause | surfaceTransformClause | noShadowClause |
            boundedByClause
        ]
        
        // Plane clause.
        startPlaneClause:
        {
            plane > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "plane" here.'
        }
        
        // Sphere clause.
        startSphereClause:
        {
            sphere > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "sphere" here.'
        }

        // Cube clause.
        startCubeClause:
        {
            cube > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "cube" here.'
        }

        // Extruded surface clauses.
        startCylinderClause:
        {
            cylinder > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "cylinder" here.'
        }
        startConicClause:
        {
            conic > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "conic" here.'
        }
        extrudedSurfaceEntryClause:
        [
            { [ min | max ] > Y ?? 'Expecting "X" or "Y" to follow "max" here.' > _expression } |
            open | surfaceEntryClause
        ]

        // Torus clauses.
        startTorusClause:
        {
            torus > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "torus" here.'
        }
        torusEntryClause:
        [
            { radii > _expression > comma ?? 'Expecting a comma here.' > _expression } |
            surfaceEntryClause
        ]

        // Extrusion clauses.
        xyPairClause:
        {
            _expression > comma ?? 'Expecting a comma here.' > _expression
        }
        controlPointsClause:
        {
            xyPairClause > comma ?? 'Expecting a comma here.' > xyPairClause
        }
        moveToClause:
        {
            move > to ?? 'Expecting "to" to follow "move" here.' > xyPairClause
        }
        lineToClause:
        {
            line > to ?? 'Expecting "to" to follow "line" here.' > xyPairClause
        }
        quadToClause:
        {
            quad > xyPairClause > to ?? 'Expecting "to" to follow "quad" control point here.' > xyPairClause
        }
        curveToClause:
        {
            curve > controlPointsClause > to ?? 'Expecting "to" to follow "curve" control point here.' > xyPairClause
        }
        extrusionPathClause:
        [
            moveToClause | lineToClause | quadToClause | curveToClause | close |
            { svg > _expression }
        ] ?? 'Expecting a path command here.'
        startExtrusionClause:
        {
            extrusion > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "extrusion" here.'
        }
        extrusionEntryClause:
        [
            { path > openBrace ?? 'Expecting an open brace after "path" here.' } |
            extrudedSurfaceEntryClause
        ]

        // Triangle clauses.
        startTriangleClause:
        {
            triangle > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "triangle" here.'
        }
        triangleEntryClause:
        [
            {
                points > _expression > comma ?? 'Expecting a comma here.' > _expression >
                comma ?? 'Expecting a comma here.' > _expression
            } |
            surfaceEntryClause
        ]
        startSmoothTriangleClause:
        {
            smooth > triangle > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "triangle" here.'
        }
        smoothTriangleEntryClause:
        [
            {
                normals > _expression > comma ?? 'Expecting a comma here.' > _expression >
                comma ?? 'Expecting a comma here.' > _expression
            } |
            triangleEntryClause
        ]
        
        // Parallelogram clauses.
        startParallelogramClause:
        {
            parallelogram > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "parallelogram" here.'
        }
        parallelogramEntryClause:
        [
            { at > _expression } |
            { sides > _expression > comma ?? 'Expecting a comma here.' > _expression } |
            surfaceEntryClause
        ]
        
        // Object file clauses.
        startObjectFileClause:
        {
           object > file > openBrace ?? 'Expecting an open brece here.'
        }
        objectFileEntryClause:
        [
            { source > _expression } | surfaceEntryClause
        ]
        
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
            {
                [ union | difference | intersection ] > [
                    openBrace |
                    { [ _identifier | _keyword ] > openBrace{?} }
                ] ?? 'Expecting an identifier or open brace here.'
            } |
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
            startCylinderClause => 'cylinder' |
            startConicClause => 'conic' |
            startTorusClause => 'torus' |
            startExtrusionClause => 'extrusion' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startParallelogramClause => 'parallelogram' |
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
        startGroupClause:
        {
            group > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "group" here.'
        }
        groupEntryClause:
        [
            groupIntervalClause => 'interval' |
            startPlaneClause => 'plane' |
            startSphereClause => 'sphere' |
            startCubeClause => 'cube' |
            startCylinderClause => 'cylinder' |
            startConicClause => 'conic' |
            startTorusClause => 'torus' |
            startExtrusionClause => 'extrusion' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startParallelogramClause => 'parallelogram' |
            startObjectFileClause => 'objectFile' |
            startObjectClause => 'object' |
            startCsgClause => 'csg' |
            startGroupClause => 'group' |
            surfaceEntryClause => 'surface'
        ]

        // Scene clauses.
        startSceneClause:
        {
            scene > openBrace ?? 'Expecting an open brace to follow "scene" here.'
        }
        sceneEntryClause:
        [
            namedClause => 'name' |
            startCameraClause => 'camera' |
            startPointLightClause => 'pointLight' |
            startPlaneClause => 'plane' |
            startSphereClause => 'sphere' |
            startCubeClause => 'cube' |
            startCylinderClause => 'cylinder' |
            startConicClause => 'conic' |
            startTorusClause => 'torus' |
            startExtrusionClause => 'extrusion' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startParallelogramClause => 'parallelogram' |
            startObjectFileClause => 'objectFile' |
            startObjectClause => 'object' |
            startCsgClause => 'csg' |
            startGroupClause => 'group' |
            background => 'background'
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
                pigment |
                { material > startThingClause } | { transform > startthingClause } |
                startPlaneClause | startSphereClause | startCubeClause | startCylinderClause |
                startConicClause | startTorusClause | startExtrusionClause | startTriangleClause |
                startSmoothTriangleClause | startParallelogramClause | startObjectFileClause |
                startObjectClause | startCsgClause | startGroupClause
            ]
        }
        setVariableClause:
        {
            [ _identifier | _keyword ] > assignment > _expression
        }

        // Top-level clause.
        [
            startContextClause        => 'HandleStartContextClause' |
            startSceneClause          => 'HandleStartSceneClause' |
            startCameraClause         => 'HandleStartCameraClause' |
            startPointLightClause     => 'HandleStartPointLightClause' |
            startPlaneClause          => 'HandleStartPlaneClause' |
            startSphereClause         => 'HandleStartSphereClause' |
            startCubeClause           => 'HandleStartCubeClause' |
            startCylinderClause       => 'HandleStartCylinderClause' |
            startConicClause          => 'HandleStartConicClause' |
            startTorusClause          => 'HandleStartTorusClause' |
            startExtrusionClause      => 'HandleStartExtrusionClause' |
            startTriangleClause       => 'HandleStartTriangleClause' |
            startSmoothTriangleClause => 'HandleStartSmoothTriangleClause' |
            startParallelogramClause  => 'HandleStartParallelogramClause' |
            startObjectFileClause     => 'HandleStartObjectFileClause' |
            startObjectClause         => 'HandleStartObjectClause' |
            startCsgClause            => 'HandleStartCsgClause' |
            startGroupClause          => 'HandleStartGroupClause' |
            background                => 'HandleBackgroundClause' |
            renderClause              => 'HandleRenderClause' |
            setThingToVariable        => 'HandleSetThingToVariableClause' |
            setVariableClause         => 'HandleSetVariableClause'
        ] ?? 'Unsupported object type found.'
        """";

    private static readonly Dsl LanguageDsl = LexicalDslFactory.CreateFrom(LanguageDslSpecification);

    static LanguageParser()
    {
        LanguageDsl.ExpressionParser.TreeBuilder = new ExpressionTreeBuilder();
    }
}
