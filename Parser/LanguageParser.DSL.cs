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

        _keywords: 'agate', 'alignment', 'ambient', 'amplitude', 'and', 'angle', 'angles', 'apply',
            'are', 'at', 'author', 'axiom', 'background', 'banded', 'baseline', 'bits',
            'black', 'blend', 'blob', 'bold', 'bottom', 'bouncing', 'bounded', 'boxed', 'bozo', 'brick', 'brilliance',
            'by', 'camera', 'center', 'channel', 'checker', 'clarity', 'clip', 'close', 'color',
            'commands', 'comment', 'completeBranch', 'conic', 'context', 'controls',
            'copyright', 'crackle', 'csg', 'cube', 'cubic', 'curve', 'cylinder', 'cylindrical',
            'degrees', 'dents', 'depth', 'description', 'diameter', 'difference', 'diffuse', 'direction', 'disc',
            'disclaimer', 'discontinuous', 'distant', 'drawLine', 'east', 'egg', 'exponent', 'extrusion', 'factor', 'falloff', 'false', 'field', 'file',
            'fainter', 'filter', 'finer', 'flatness', 'font', 'frequency', 'from', 'gamma', 'gap', 'generations', 'generic', 'gradient', 'granite',
            'grain', 'grayscale', 'group', 'height', 'heightfield', 'hexagon', 'horizontal',
            'ignore', 'image', 'import', 'include', 'index', 'info', 'inherited', 'inner', 'interior', 'intersection',
            'ior', 'italic', 'kern', 'kerning', 'lathe', 'layer', 'layout', 'leaf', 'left', 'length',
            'leopard', 'light', 'line', 'linear', 'location', 'look', 'lsystem',
            'marble', 'material', 'materials', 'matrix', 'max', 'maximum', 'medium', 'metallic', 'min', 'minimum', 'mortar',
            'mottled', 'move', 'named', 'no', 'noise', 'octaves', 'normal', 'normals', 'north', 'null', 'object', 'of', 'once',
            'open', 'parallel', 'parallelogram', 'patch', 'path', 'phase', 'pigment', 'pipes',
            'pitchDown', 'pitchUp', 'pixel', 'planar', 'plane', 'point', 'points', 'poly',
            'position', 'productions', 'profile', 'quad', 'radial', 'radians', 'radii', 'radius', 'reflective',
            'refraction', 'regular', 'render', 'report', 'right', 'ripples', 'rollLeft', 'rollRight',
            'ramp', 'rotate', 'scale', 'scallop', 'scanner', 'scene', 'seed', 'serial', 'shadow', 'shadows',
            'shape', 'shear', 'shininess', 'sides', 'sine', 'size', 'smooth', 'software', 'source',
            'specular', 'sphere', 'spherical', 'spline', 'spot', 'square', 'startBranch', 'steps', 'strength', 'stripes',
            'superellipsoid', 'surfaces', 'svg', 'sweep', 'text', 'thin', 'threshold', 'title', 'to', 'top', 'toroidal', 'torus',
            'toVertical',
            'tightness', 'transform', 'translate', 'transparency', 'triangle', 'triangular', 'true', 'tube', 'tubes',
            'turbulence', 'turnAround', 'turnLeft', 'turnRight', 'uncached', 'union', 'up', 'uSteps',
            'vector', 'vertical', 'view', 'vSteps', 'warning', 'wave', 'waves', 'width', 'with', 'wood',
            'wrinkles',
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
        // An import reads a library the way an include reads a file, but only the definitions it
        // names are left in scope afterward.
        importClause:
        {
            import > _string ?? 'Expecting a file name to follow "import" here.' >
            openBrace ?? 'Expecting an open brace to follow the file name here.' >
            [ _identifier | _keyword ] ?? 'Expecting the name of something to import here.' >
            { comma > [ _identifier | _keyword ] ?? 'Expecting a name to follow the comma here.' }{*} >
            closeBrace ?? 'Expecting a close brace here.'
        }
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
        // Turbulence is written either as a bare amount, which is by far the common case and means
        // the amplitude alone, or as a block when there is more to say.
        turbulenceClause: { turbulence > [ openBrace | _expression ] }
        turbulenceEntryClause:
        [
            { amplitude > _expression } | { octaves > _expression } |
            { finer > _expression } | { fainter > _expression } | withSeedClause
        ] ?? 'Expecting a turbulence property here.'
        // Mottling dims a colour by noise rather than pushing points about, so it takes the layers
        // and nothing else -- an amplitude would have nothing here to move.
        noiseClause: { noise > openBrace ?? 'Expecting an open brace to follow "noise" here.' }
        noiseEntryClause:
        [
            { octaves > _expression } | { finer > _expression } |
            { fainter > _expression } | withSeedClause
        ] ?? 'Expecting a noise property here.'
        // How a pattern's value is shaped once the pattern has produced it: scaled and slid by the
        // frequency and phase, then bent by a wave.  Offered to every pattern, since none of it
        // belongs to any pattern in particular.
        waveClause:
        {
            [ ramp | sine | triangle | scallop | cubic |
              { poly > _expression } ] >
            wave ?? 'Expecting "wave" to follow the wave name here.'
        }
        patternShapingClause:
        [
            { frequency > _expression } | { phase > _expression } | waveClause
        ]
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

        // Light clauses.  One opener serves all three sorts, told apart by the word before
        // "light": nothing or "point" for a lamp, "distant" for the sun, "spot" for a cone.
        startLightClause:
        {
            [ { [ point | distant | spot ] > light } | light ] >
            openBrace ?? 'Expecting an open brace to follow "light" here.'
        }
        lightColorClause: { color > _expression }
        directionClause: { direction > _expression }
        pointAtClause:
        {
            point > at ?? 'Expecting "at" to follow "point" here.' > _expression
        }
        pointLightEntryClause:
        [
            namedClause | locationClause | lightColorClause
        ] ?? 'Expecting a point light property here.'
        distantLightEntryClause:
        [
            namedClause | directionClause | lightColorClause
        ] ?? 'Expecting a distant light property here.'
        spotLightEntryClause:
        [
            namedClause | locationClause | pointAtClause | lightColorClause |
            { radius > _expression } | { falloff > _expression } | { tightness > _expression }
        ] ?? 'Expecting a spotlight property here.'

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
            ripples | square | triangular | waves |
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
        mottledPigmentClause:
        {
            mottled > openBrace ?? 'Expecting an open brace to follow "mottled" here.'
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
            patternClause > openBrace ?? 'Expecting an open brace to follow the pattern here.'
        }
        pigmentClause:
        [
            blendPigmentClause | mottledPigmentClause | patternPigmentClause |
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
            [ ambient | diffuse | specular | shininess | reflective | transparency |
              brilliance | grain ] >
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
        // An interior may be written out in full, named, or named and then added to, much as a
        // material may.  What a surface is made of is worth keeping and reusing on its own terms:
        // POV-Ray's glass library, for one, declares its interiors apart from its textures and
        // pairs them up afterward.  There is no "inherited" here as there is for a material,
        // since that word is about handing a material down to the pieces of a surface that named
        // none of their own, and interiors are not handed down that way.
        startInteriorClause:
        {
            interior > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "interior" here.'
        }
        interiorEntryClause:
        [
            materialIorClause | { filter > _expression } | { clarity > _expression }
        ] ?? 'Expecting an interior property here.'
        // How a surface's skin is roughened: a pattern whose slope tilts the normal from point to
        // point.  It is written as the pigment's sibling because that is what it is -- another
        // pattern over the same surface -- and it is kept apart from the pigment because the two
        // are rarely the same field.  A marble's veins and the roughness of its surface have
        // nothing to do with one another, and each wants its own scale and its own footing.
        startNormalClause:
        {
            patternClause > openBrace ?? 'Expecting an open brace to follow the pattern here.'
        }
        normalEntryClause:
        {
            depth > _expression
        }
        materialEntryClause:
        [
            pigment | normal | materialValueClause | materialMetallicClause | startInteriorClause
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
        lsystemMaterialClause:
        [
            {
                depth > _number ?? 'Expecting a branching depth to follow "depth" here.' >
                arrow ?? 'Expecting an arrow to follow the depth here.' >
                [ _identifier | _keyword ] ?? 'Expecting a material name to follow the arrow here.'
            } |
            {
                _string > arrow ?? 'Expecting an arrow to follow the material character here.' >
                [ _identifier | _keyword ] ?? 'Expecting a material name to follow the arrow here.'
            }
        ]
        lsystemMaterialsClause:
        {
            materials > openBrace ?? 'Expecting an open brace to follow "materials" here.' >
            lsystemMaterialClause{*} > closeBrace ?? 'Expecting a close brace here.'
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
            lsystemSurfacesClause | lsystemMaterialsClause | surfaceEntryClause
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
            startLightClause => 'light' |
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
                { interior > startThingClause } |
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
            startLightClause          => 'HandleStartLightClause' |
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
