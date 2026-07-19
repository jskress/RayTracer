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

        _keywords: 'agate', 'alignment', 'ambient', 'and', 'angle', 'angles', 'apply',
            'are', 'at', 'author', 'axiom', 'background', 'banded', 'baseline', 'bits',
            'black', 'blend', 'blob', 'bold', 'bottom', 'bouncing', 'bounded', 'boxed', 'bozo', 'brick',
            'by', 'camera', 'center', 'channel', 'checker', 'clip', 'close', 'color',
            'commands', 'comment', 'completeBranch', 'conic', 'context', 'controls',
            'copyright', 'crackle', 'csg', 'cube', 'cubic', 'curve', 'cylinder', 'cylindrical',
            'degrees', 'dents', 'description', 'diameter', 'difference', 'diffuse', 'disc',
            'disclaimer', 'discontinuous', 'drawLine', 'east', 'egg', 'extrusion', 'factor', 'false', 'field', 'file',
            'filter', 'flatness', 'font', 'from', 'gamma', 'gap', 'generations', 'generic', 'gradient', 'granite',
            'grayscale', 'group', 'height', 'heightfield', 'hexagon', 'horizontal',
            'ignore', 'image', 'include', 'index', 'info', 'inherited', 'inner', 'interior', 'intersection',
            'ior', 'italic', 'kern', 'kerning', 'lathe', 'layer', 'layout', 'leaf', 'left', 'length',
            'leopard', 'light', 'line', 'linear', 'location', 'look', 'lsystem',
            'marble', 'material', 'matrix', 'max', 'maximum', 'medium', 'metallic', 'min', 'minimum', 'mortar',
            'move', 'named', 'no', 'noisy', 'normal', 'normals', 'north', 'null', 'object', 'of', 'once',
            'open', 'parallel', 'parallelogram', 'patch', 'path', 'phased', 'pigment', 'pipes',
            'pitchDown', 'pitchUp', 'pixel', 'planar', 'plane', 'point', 'points',
            'position', 'productions', 'profile', 'quad', 'radial', 'radians', 'radii', 'radius', 'reflective',
            'refraction', 'regular', 'render', 'report', 'right', 'rollLeft', 'rollRight',
            'rotate', 'scale', 'scanner', 'scene', 'seed', 'serial', 'shadow', 'shadows',
            'shape', 'shear', 'shininess', 'sides', 'size', 'smooth', 'software', 'source',
            'specular', 'sphere', 'spherical', 'spline', 'square', 'startBranch', 'steps', 'strength', 'stripes',
            'superellipsoid', 'surfaces', 'svg', 'sweep', 'text', 'thin', 'threshold', 'title', 'to', 'top', 'toroidal', 'torus',
            'toVertical',
            'transform', 'translate', 'transparency', 'triangle', 'triangular', 'true', 'tube', 'tubes',
            'turbulence', 'turnAround', 'turnLeft', 'turnRight', 'uncached', 'union', 'up', 'uSteps',
            'vector', 'vertical', 'view', 'vSteps', 'warning', 'width', 'with', 'wood', 'wrinkles',
            'X', 'Y', 'Z'

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
        withSeedClause:
        {
            with > seed ?? 'Expecting "seed" to follow "with" here.' > _expression
        }
        turbulenceClause:
        {
            turbulence > _expression >
            { phased > _expression > { comma > _expression }{?} }{?} >
            withSeedClause{?}
        }
        imageClause: { uncached{?} > image > _expression }
        
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
            agate | boxed | brick | checker | cubic | hexagon | leopard | planar | radial |
            square | triangular |
            {
                [ bozo | crackle | dents | granite | wrinkles ] > withSeedClause{?}
            } | marble | wood |
            {
                linear > [ X | Y | Z ]{?} >
                [ stripes | { bouncing{?} > gradient } ] ?? 'Expecting "stripes" or "gradient" here.'
            } |
            {
                [ cylindrical | spherical ] > [ stripes | { bouncing{?} > gradient } ]{?}
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
        imageMapTypeClause:
        [
            { planar > once{?} } | spherical |
            { cylindrical > once{?} } | toroidal
        ]
        imagePigmentClause:
        {
            imageClause > imageMapTypeClause{?}
        }
        patternPigmentClause:
        {
            patternClause > openBrace ?? 'Expecting an open brace to follow "noisy" here.'
        }
        pigmentClause:
        [
            blendPigmentClause | noisyPigmentClause | patternPigmentClause |
            imagePigmentClause | { color > _expression } |
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
        materialMetallicClause:
        {
            metallic > _expression{?}
        }
        startInteriorClause:
        {
            interior > openBrace ?? 'Expecting an open brace to follow "interior" here.'
        }
        interiorEntryClause:
        [
            materialIorClause | { filter > _expression }
        ] ?? 'Expecting an interior property here.'
        materialEntryClause:
        [
            pigment | materialValueClause | materialMetallicClause | startInteriorClause
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
            boundedByClause | withSeedClause
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

        // Egg clauses.
        startEggClause:
        {
            egg > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "egg" here.'
        }
        eggEntryClause:
        [
            { radii > _expression > comma ?? 'Expecting a comma here.' > _expression } |
            surfaceEntryClause
        ]

        // Superellipsoid clauses.
        startSuperellipsoidClause:
        {
            superellipsoid > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "superellipsoid" here.'
        }
        superellipsoidEntryClause:
        [
            { east > _expression } |
            { north > _expression } |
            surfaceEntryClause
        ]

        // Patch clauses.
        startPatchClause:
        {
            patch > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "patch" here.'
        }
        patchEntryClause:
        [
            {
                points > _expression > comma > _expression > comma > _expression > comma > _expression > comma >
                _expression > comma > _expression > comma > _expression > comma > _expression > comma >
                _expression > comma > _expression > comma > _expression > comma > _expression > comma >
                _expression > comma > _expression > comma > _expression > comma > _expression
            } |
            { uSteps > _expression } |
            { vSteps > _expression } |
            { flatness > _expression } |
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

        // Spline clauses.  These mirror the path clauses above, except a spline's points
        // are full 3D triples rather than 2D pairs.
        xyzTripleClause:
        {
            _expression > comma ?? 'Expecting a comma here.' >
            _expression > comma ?? 'Expecting a comma here.' >
            _expression
        }
        splineControlPointsClause:
        {
            xyzTripleClause > comma ?? 'Expecting a comma here.' > xyzTripleClause
        }
        moveToSplineClause:
        {
            move > to ?? 'Expecting "to" to follow "move" here.' > xyzTripleClause
        }
        lineToSplineClause:
        {
            line > to ?? 'Expecting "to" to follow "line" here.' > xyzTripleClause
        }
        quadToSplineClause:
        {
            quad > xyzTripleClause >
            to ?? 'Expecting "to" to follow "quad" control point here.' > xyzTripleClause
        }
        curveToSplineClause:
        {
            curve > splineControlPointsClause >
            to ?? 'Expecting "to" to follow "curve" control point here.' > xyzTripleClause
        }
        splineEntryClause:
        [
            moveToSplineClause | lineToSplineClause | quadToSplineClause | curveToSplineClause
        ] ?? 'Expecting a spline command here.'

        // Lathe clauses.
        startLatheClause:
        {
            lathe > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "lathe" here.'
        }
        latheEntryClause:
        [
            { path > openBrace ?? 'Expecting an open brace after "path" here.' } |
            surfaceEntryClause
        ]

        // Blob clauses.
        blobSphereEntryClause:
        [
            { center > _expression } |
            { radius > _expression } |
            { strength > _expression }
        ] ?? 'Expecting a sphere component property here.'
        blobCylinderEntryClause:
        [
            { from > _expression } |
            { to > _expression } |
            { radius > _expression } |
            { strength > _expression }
        ] ?? 'Expecting a cylinder component property here.'
        startBlobClause:
        {
            blob > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "blob" here.'
        }
        blobEntryClause:
        [
            { threshold > _expression } |
            { sphere > openBrace ?? 'Expecting an open brace after "sphere" here.' } |
            { cylinder > openBrace ?? 'Expecting an open brace after "cylinder" here.' } |
            surfaceEntryClause
        ]

        // Tube clauses.
        tubePointClause:
        {
            radius > _expression >
            at ?? 'Expecting "at" to follow a tube point radius here.' >
            _expression
        }
        tubeQuadClause:
        {
            quad >
            radius ?? 'Expecting "radius" to follow "quad" here.' > _expression >
            at ?? 'Expecting "at" to follow a tube quad control radius here.' > _expression >
            radius ?? 'Expecting "radius" to follow a tube quad control point here.' > _expression >
            at ?? 'Expecting "at" to follow a tube quad end radius here.' > _expression
        }
        tubeCubicClause:
        {
            curve >
            radius ?? 'Expecting "radius" to follow "curve" here.' > _expression >
            at ?? 'Expecting "at" to follow a tube curve control radius here.' > _expression >
            radius ?? 'Expecting "radius" to follow a tube curve first control point here.' > _expression >
            at ?? 'Expecting "at" to follow a tube curve control radius here.' > _expression >
            radius ?? 'Expecting "radius" to follow a tube curve second control point here.' > _expression >
            at ?? 'Expecting "at" to follow a tube curve end radius here.' > _expression
        }
        startTubeClause:
        {
            tube > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "tube" here.'
        }
        tubeEntryClause:
        [
            tubePointClause => 'point' |
            tubeQuadClause => 'quad' |
            tubeCubicClause => 'curve' |
            discontinuous | surfaceEntryClause
        ]

        // Sweep clauses.
        startSweepClause:
        {
            sweep > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "sweep" here.'
        }
        sweepEntryClause:
        [
            { profile > openBrace ?? 'Expecting an open brace after "profile" here.' } |
            {
                discontinuous{?} > spline >
                openBrace ?? 'Expecting an open brace after "spline" here.'
            } |
            { steps > _expression } |
            open | surfaceEntryClause
        ]

        // Generic shape clauses.
        startGenericShapeClause:
        {
            generic > shape > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "shape" here.'
        }
        genericShapeEntryClause:
        [
            { path > openBrace ?? 'Expecting an open brace after "path" here.' } |
            surfaceEntryClause
        ]

        // Text clauses.
        fontWeightClause:
        [
            thin | light | regular | medium | bold | black
        ]
        fontClause:
        {
            font > _expression > fontWeightClause{?} > italic{?}
        }
        textLayoutEntryClause:
        [
            { text > alignment ?? 'Expecting "alignment" to follow "text" here.' >
                [ left | center | right | _expression ] } |
            { horizontal > position ?? 'Expecting "position" to follow "horizontal" here.' >
                [ left | center | right | _expression ] } |
            { vertical > position ?? 'Expecting "position" to follow "vertical" here.' >
                [ top | baseline | center | bottom | _expression ] } |
            { line > gap ?? 'Expecting "gap" to follow "line" here.' > _expression }
        ] ?? 'Expecting a text layout property here.'
        kerningPairClause:
        {
            kern > _expression > comma ?? 'Expecting a comma here.' > _expression >
            comma ?? 'Expecting a comma here.' > _expression
        }
        KerningClause:
        {
            kerning > openBrace ?? 'Expecting an open brace to follow "kerning" here.' >
            kerningPairClause{*} > closeBrace ?? 'Expecting a close brace here.'
        }
        startTextClause:
        {
            text > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "text" here.'
        }
        textEntryClause:
        [
            { text > _expression } | fontClause |
            { layout > openBrace ?? 'Expecting an open brace after "layout" here.' } |
            kerningClause | open | surfaceEntryClause
        ]

        // L-system clauses.
        startLsystemClause:
        {
            lsystem > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "lsystem" here.'
        }
        lsystemCommandClause:
        {
            _string > arrow ?? 'Expecting an arrow to follow the command character here.' >
            [
                move | drawLine | turnLeft | turnRight | pitchUp | pitchDown | rollLeft |
                rollRight | turnAround | ToVertical | startBranch | completeBranch
            ] ?? 'Expecting a turtle command to follow the arrow here.'
        }
        lsystemCommandsClause:
        {
            commands > openBrace ?? 'Expecting an open brace to follow "commands" here.' >
            lsystemCommandClause{*} > closeBrace ?? 'Expecting a close brace here.'
        }
        lsystemSurfaceClause:
        {
            _string > arrow ?? 'Expecting an arrow to follow the surface character here.' >
            [ _identifier | _keyword ] ?? 'Expecting a surface name to follow the arrow here.'
        }
        lsystemSurfacesClause:
        {
            surfaces > openBrace ?? 'Expecting an open brace to follow "surfaces" here.' >
            lsystemSurfaceClause{*} > closeBrace ?? 'Expecting a close brace here.'
        }
        lsystemProductionProbabilityClause:
        {
            leftParen > _expression > modulo{?} >
            rightParen ?? 'Expecting a right parenthesis here.'
        }
        lsystemProductionClause:
        {
            _string > lsystemProductionProbabilityClause{?} >
            arrow ?? 'Expecting an arrow to follow the rule variable here.' >
            _expression
        }
        lsystemProductionsClause:
        {
            productions > openBrace ?? 'Expecting an open brace to follow "productions" here.' >
            lsystemProductionClause{*} > closeBrace ?? 'Expecting a close brace here.'
        }
        lsystemIgnoreClause:
        {
            ignore > [ { commands > and > _string } | commands | _string ]
                ?? 'Expecting "commands" or a string to follow "ignore" here.'
        }
        lsystemEntryClause:
        [
            { axiom > _expression } | { generations > _expression } |
            { controls > openBrace ?? 'Expecting an open brace to follow "controls" here.' } |
            { leaf > [ _identifier | _keyword ] ?? 'Expecting a surface name to follow "leaf" here.' } |
            lsystemCommandsClause | lsystemProductionsClause | lsystemIgnoreClause |
            lsystemSurfacesClause | surfaceEntryClause
        ]
        lsystemRenderingControlsEntryClause:
        [
            extrusion | pipes | tubes | { angle > _expression } | { length > _expression } |
            { diameter > _expression } | { factor > _expression }
        ]

        // Height field clauses.
        startHeightFieldClause:
        {
            heightfield > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "field" here.'
        }
        heightFieldEntryClause:
        [
            imageClause | { clip > _expression } | open | surfaceEntryClause
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

        // Disc clauses.
        startDiscClause:
        {
            disc > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "disc" here.'
        }
        discEntryClause:
        [
            { center > _expression } |
            { normal > _expression } |
            { radius > _expression } |
            { inner > radius ?? 'Expecting "radius" to follow "inner" here.' > _expression } |
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
            startEggClause => 'egg' |
            startSuperellipsoidClause => 'superellipsoid' |
            startPatchClause => 'patch' |
            startExtrusionClause => 'extrusion' |
            startLatheClause => 'lathe' |
            startBlobClause => 'blob' |
            startTubeClause => 'tube' |
            startSweepClause => 'sweep' |
            startTextClause => 'text' |
            startLsystemClause => 'lsystem' |
            startHeightFieldClause => 'heightField' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startParallelogramClause => 'parallelogram' |
            startDiscClause => 'disc' |
            startGenericShapeClause => 'genericShape' |
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
            startEggClause => 'egg' |
            startSuperellipsoidClause => 'superellipsoid' |
            startPatchClause => 'patch' |
            startExtrusionClause => 'extrusion' |
            startLatheClause => 'lathe' |
            startBlobClause => 'blob' |
            startTubeClause => 'tube' |
            startSweepClause => 'sweep' |
            startTextClause => 'text' |
            startLsystemClause => 'lsystem' |
            startHeightFieldClause => 'heightField' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startParallelogramClause => 'parallelogram' |
            startDiscClause => 'disc' |
            startGenericShapeClause => 'genericShape' |
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
            startEggClause => 'egg' |
            startSuperellipsoidClause => 'superellipsoid' |
            startPatchClause => 'patch' |
            startExtrusionClause => 'extrusion' |
            startLatheClause => 'lathe' |
            startBlobClause => 'blob' |
            startTubeClause => 'tube' |
            startSweepClause => 'sweep' |
            startTextClause => 'text' |
            startLsystemClause => 'lsystem' |
            startHeightFieldClause => 'heightField' |
            startTriangleClause => 'triangle' |
            startSmoothTriangleClause => 'smoothTriangle' |
            startParallelogramClause => 'parallelogram' |
            startDiscClause => 'disc' |
            startGenericShapeClause => 'genericShape' |
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
                startConicClause | startTorusClause | startExtrusionClause | startLatheClause |
                startBlobClause | startTubeClause | startSweepClause | startTextClause |
                startLsystemClause | startHeightFieldClause | startTriangleClause |
                startSmoothTriangleClause | startParallelogramClause | startDiscClause |
                startGenericShapeClause | startEggClause | startSuperellipsoidClause |
                startPatchClause | startObjectFileClause | startObjectClause | startCsgClause | startGroupClause
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
            startEggClause            => 'HandleStartEggClause' |
            startSuperellipsoidClause => 'HandleStartSuperellipsoidClause' |
            startPatchClause          => 'HandleStartPatchClause' |
            startExtrusionClause      => 'HandleStartExtrusionClause' |
            startLatheClause          => 'HandleStartLatheClause' |
            startBlobClause           => 'HandleStartBlobClause' |
            startTubeClause           => 'HandleStartTubeClause' |
            startSweepClause          => 'HandleStartSweepClause' |
            startTextClause           => 'HandleStartTextClause' |
            startLsystemClause        => 'HandleStartLSystemClause' |
            startHeightFieldClause    => 'HandleStartHeightFieldClause' |
            startTriangleClause       => 'HandleStartTriangleClause' |
            startSmoothTriangleClause => 'HandleStartSmoothTriangleClause' |
            startParallelogramClause  => 'HandleStartParallelogramClause' |
            startDiscClause           => 'HandleStartDiscClause' |
            startGenericShapeClause   => 'HandleStartGenericShapeClause' |
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
