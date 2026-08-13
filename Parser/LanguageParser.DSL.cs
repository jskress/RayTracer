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
        // The mathematical symbols.  These exist so that a formula may be pasted in as it was
        // written rather than translated into function calls first; each is sugar for a function
        // the catalog already holds, so there is one implementation per operation.  Several
        // operations arrive as more than one code point, depending on where the formula was copied
        // from, so the confusable spellings are declared alongside the canonical one and mean
        // exactly the same thing.
        squared: _operator("\u00b2")
        cubed: _operator("\u00b3")
        toZeroPower: _operator("\u2070")
        toFirstPower: _operator("\u00b9")
        toFourthPower: _operator("\u2074")
        toFifthPower: _operator("\u2075")
        toSixthPower: _operator("\u2076")
        toSeventhPower: _operator("\u2077")
        toEighthPower: _operator("\u2078")
        toNinthPower: _operator("\u2079")
        squareRoot: _operator("\u221a")
        cubeRoot: _operator("\u221b")
        degreeSign: _operator("\u00b0")
        dotProduct: _operator("\u22c5")
        middleDot: _operator("\u00b7")
        bulletOperator: _operator("\u2219")
        bullet: _operator("\u2022")
        crossProduct: _operator("\u00d7")
        vectorProduct: _operator("\u2a2f")
        divisionSign: _operator("\u00f7")
        divisionSlash: _operator("\u2215")
        fractionSlash: _operator("\u2044")
        minusSign: _operator("\u2212")
        enDash: _operator("\u2013")
        asteriskOperator: _operator("\u2217")
        starOperator: _operator("\u22c6")
        lessOrEqual: _operator("\u2264")
        greaterOrEqual: _operator("\u2265")
        notEqualTo: _operator("\u2260")
        conjunction: _operator("\u2227")
        disjunction: _operator("\u2228")
        negation: _operator("\u00ac")
        // Each of the three logical operations may be written as a word or as a symbol, and the two
        // are the same operator rather than two that behave alike.  That means "and", "or" and "not"
        // are keywords of ours, and a keyword takes over its name in this specification from the
        // predefined operator that had it -- so the symbols have to be reached for under names of
        // their own.  Naming them here rather than relying on the predefined names is what keeps
        // both spellings working.  The conditional is here for the same reason and is worth calling
        // out, since the collision is not one anybody would foresee: the predefined name for "?" is
        // "if", so making "if" a keyword of ours takes that name away from it.
        arrow: _operator("->")
        logicalAnd: _operator("&&")
        logicalOr: _operator("||")
        logicalNot: _operator("!")
        conditional: _operator("?")

        _keywords: 'absorption', 'accuracy', 'agate', 'alignment', 'ambient', 'amplitude', 'and',
            'angle', 'angles', 'aperture', 'apply',
            'anisotropy', 'are', 'area', 'at', 'author', 'axiom', 'axisU', 'axisV', 'azimuth', 'background', 'banded',
            'baseline', 'black', 'blend', 'blob', 'blur', 'bold', 'bottom', 'bouncing',
            'bounces', 'bounded', 'boxed', 'bozo', 'brick', 'brightness', 'brilliance',
            'by', 'camera', 'case', 'center', 'checker', 'clarity', 'clip', 'close', 'color',
            'columns', 'commands', 'comment', 'completeBranch', 'conic', 'context', 'controls',
            'copyright', 'crackle', 'csg', 'cube', 'cubic', 'curve', 'cylinder', 'cylindrical',
            'default', 'degrees', 'density', 'dents', 'depth', 'description', 'diameter', 'difference', 'diffuse', 'direction', 'disc',
            'disclaimer', 'discontinuous', 'distance', 'distant', 'elevation', 'drawLine', 'east', 'egg', 'else', 'emission', 'environment', 'extrusion', 'factor', 'fade', 'falloff', 'false', 'field', 'file',
            'fainter', 'filter', 'finer', 'fisheye', 'flatness', 'focal', 'font', 'for', 'frequency', 'from', 'function', 'gamma', 'gap', 'generations', 'generic', 'gradient', 'granite',
            'grain', 'group', 'height', 'heightfield', 'hexagon', 'horizontal',
            'icon', 'if', 'ignore', 'image', 'import', 'include', 'index', 'info', 'inherited', 'inner', 'interior', 'intersection',
            'in', 'ior', 'isosurface', 'italic', 'jitter', 'kern', 'kerning', 'lathe', 'layer', 'layout', 'leaf', 'left', 'length',
            'leopard', 'light', 'line', 'linear', 'location', 'look', 'lsystem',
            'marble', 'material', 'materials', 'matrix', 'max', 'medium', 'metallic', 'min', 'mortar',
            'motion', 'mottled', 'move', 'named', 'no', 'noise', 'number', 'octaves', 'normal', 'normals', 'north', 'not', 'null', 'object', 'of', 'once',
            'open', 'or', 'orthographic', 'over', 'panoramic', 'parallel', 'parallelogram', 'patch', 'path', 'perspective', 'phase', 'physical', 'pigment', 'pipes', 'primitive',
            'pitchDown', 'pitchUp', 'pixel', 'planar', 'plane', 'point', 'points', 'poly',
            'position', 'power', 'productions', 'profile', 'quad', 'radial', 'radians', 'radii', 'radius', 'reflective', 'return',
            'refraction', 'regular', 'render', 'right', 'ripples', 'rollLeft', 'rollRight',
            'ramp', 'rayleigh', 'rotate', 'rows', 'samples', 'scale', 'scallop', 'scanner', 'scattering', 'scene', 'seed', 'serial', 'shadow', 'shadows',
            'shape', 'shear', 'shininess', 'shutter', 'sides', 'sine', 'size', 'sky', 'smooth', 'software', 'source',
            'specular', 'sphere', 'spherical', 'spline', 'spot', 'square', 'startBranch', 'steps', 'strength', 'stripes', 'sun',
            'superellipsoid', 'surface', 'surfaces', 'svg', 'sweep', 'switch', 'text', 'thin', 'threshold', 'title', 'to', 'top', 'toroidal', 'torus',
            'toVertical',
            'tightness', 'transform', 'translate', 'transparency', 'triangle', 'triangular', 'true', 'tube', 'tubes',
            'turbidity', 'turbulence', 'turnAround', 'turnLeft', 'turnRight', 'ultraWide', 'uncached', 'union', 'up', 'uSteps',
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
                _identifier leftParen _expression(*, comma) /rightParen => 'call',
                _keyword leftParen _expression(*, comma) /rightParen => 'call',
                _identifier => 'variable',
                _keyword => 'variable'
            ]
            unary: [
                // Postfix binds tighter than prefix, so that -x² is -(x²) and √x² is √(x²), which
                // is what both mean when they are read aloud and what they mean in print.
                : postfixFirst,
                logicalNot*, not*, negation*, minus*, minusSign*, enDash*, dollar*, color*, point*, vector*,
                squareRoot*, cubeRoot*,
                *squared, *cubed, *toZeroPower, *toFirstPower, *toFourthPower, *toFifthPower,
                *toSixthPower, *toSeventhPower, *toEighthPower, *toNinthPower,
                *degreeSign, *degrees, *radians
            ]
            binary: [
                plus, minus, multiply, divide, modulo,
                // The mathematical symbols take the precedence of the plain operators they stand
                // beside, since that is what a reader of the formula will assume of them.
                minusSign: additive, enDash: additive,
                dotProduct: multiplicative, middleDot: multiplicative,
                bulletOperator: multiplicative, bullet: multiplicative,
                crossProduct: multiplicative, vectorProduct: multiplicative,
                asteriskOperator: multiplicative, starOperator: multiplicative,
                divisionSign: multiplicative, divisionSlash: multiplicative,
                fractionSlash: multiplicative,
                // The comparisons and the logic.  The predefined ones carry their own precedence;
                // the symbols standing in for them are given the same.  "&&" is set a step above
                // "||" rather than left level with it, so that a && b || c groups the way anyone
                // who has written C# will read it.
                lessthan, lessthanorequal, greaterthan, greaterthanorequal, equal, notequal,
                logicalAnd: 250, conjunction: 250, and: 250,
                logicalOr: boolean, disjunction: boolean, or: boolean,
                lessOrEqual: comparison, greaterOrEqual: comparison, notEqualTo: equality
            ]
            trinary: [
                (conditional, colon)
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
        // Mottling dims a color by noise rather than pushing points about, so it takes the layers
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
        contextPropertyClause:
        {
            [ width | height | gamma ] ?? 'Expecting a context block item here.' >
            _expression
        }
        // How hard to work at a scattering medium, which belongs with the scanner and the
        // anti-aliasing rather than with the description of what the medium is made of.
        mediumSamplesClause:
        {
            medium > [ samples | bounces ] ?? 'Expecting "samples" or "bounces" to follow "medium" here.' >
            _expression
        }
        contextEntryClause:
        [
            startInfoClause | scannerClause | anglesClause | settingOnClause |
            settingOffClause | mediumSamplesClause | contextPropertyClause
        ] ?? 'Expecting a context property here.'

        // Camera clauses.
        // The word before "camera", if any, names the projection: nothing for the ordinary
        // perspective sort, or one of these for another.  This mirrors how the word before "light"
        // names the sort of light.
        startCameraClause:
        {
            [
                { [ perspective | orthographic | fisheye | ultraWide | panoramic | spherical ] > camera } |
                camera
            ] >
            openBrace ?? 'Expecting an open brace to follow "camera" here.'
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
        // The focus may be given either as how far ahead it lies or as a point to bring into
        // focus; they say the same thing, and which is the easier depends on the scene.
        focalClause:
        {
            focal >
            [ point | distance ] ?? 'Expecting "point" or "distance" to follow "focal" here.' >
            _expression
        }
        blurSamplesClause:
        {
            blur > samples ?? 'Expecting "samples" to follow "blur" here.' > _expression
        }
        cameraEntryClause:
        [
            namedClause | locationClause | lookAtClause | upClause | fieldOfViewClause |
            { aperture > _expression } | focalClause | blurSamplesClause |
            { shutter > _expression } | { seed > _expression }
        ] ?? 'Expecting a camera property here.'

        // Light clauses.  One opener serves all three sorts, told apart by the word before
        // "light": nothing or "point" for a lamp, "distant" for the sun, "spot" for a cone.
        // The word before "light", if any, names the sort.  What follows is either a block describing
        // one or the name of a light already described, which may carry a block of its own to adjust
        // what it found -- the same shape "object <name>" has for a surface.
        startLightClause:
        {
            [ { [ point | distant | spot | area | sky ] > light } | light ] >
            [ openBrace | { [ _identifier | _keyword ] > openBrace{?} } ]
                ?? 'Expecting an open brace, or the name of a light, to follow "light" here.'
        }
        lightColorClause: { color > _expression }
        directionClause: { direction > _expression }
        pointAtClause:
        {
            point > at ?? 'Expecting "at" to follow "point" here.' > _expression
        }
        // How a light thins with the distance it has travelled.  Offered only to lights that stand
        // somewhere: a sun and a sky are infinitely far off, so nothing in a scene is nearer to them
        // than anything else and there is nothing for a distance to mean.
        fadeClause:
        {
            fade > [ distance | power ] ?? 'Expecting "distance" or "power" to follow "fade" here.' >
            _expression
        }
        pointLightEntryClause:
        [
            namedClause | locationClause | lightColorClause | fadeClause
        ] ?? 'Expecting a point light property here.'
        distantLightEntryClause:
        [
            namedClause | directionClause | lightColorClause
        ] ?? 'Expecting a distant light property here.'
        spotLightEntryClause:
        [
            namedClause | locationClause | pointAtClause | lightColorClause |
            { radius > _expression } | { falloff > _expression } | { tightness > _expression } |
            fadeClause
        ] ?? 'Expecting a spotlight property here.'
        // Light from every direction at once, as the sky gives it.  With no pigment of its own it
        // carries the scene's background, so that what lights the scene is what the scene shows.
        skyLightEntryClause:
        [
            namedClause | lightColorClause | pigment | { samples > _expression }
        ] ?? 'Expecting a sky light property here.'
        areaLightEntryClause:
        [
            namedClause | locationClause | lightColorClause |
            { axisU > _expression } | { axisV > _expression } |
            { steps > _expression } | { uSteps > _expression } | { vSteps > _expression } |
            { seed > _expression } |
            { no > jitter ?? 'Expecting "jitter" to follow "no" here.' } | fadeClause
        ] ?? 'Expecting an area light property here.'

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
        // A sky worked out from what the air actually does, rather than painted.  It is a pigment
        // because a background is one: the camera, a reflection and a sky light all read it by
        // direction, so being a pigment is what makes one thing serve all three.
        startPhysicalSkyClause:
        {
            physical > sky ?? 'Expecting "sky" to follow "physical" here.' >
            openBrace ?? 'Expecting an open brace to follow "physical sky" here.'
        }
        physicalSkyEntryClause:
        [
            {
                sun >
                [ elevation | azimuth ] ?? 'Expecting "elevation" or "azimuth" to follow "sun" here.' >
                _expression
            } |
            { turbidity > _expression } | { height > _expression } |
            { brightness > _expression } |
            { rows > _expression } | { columns > _expression } |
            { no > light ?? 'Expecting "light" to follow "no" here.' }
        ]
        pigmentClause:
        [
            blendPigmentClause | mottledPigmentClause | patternPigmentClause |
            startPhysicalSkyClause | imagePigmentClause | { color > _expression } |
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
            materialIorClause | { filter > _expression } | { clarity > _expression } |
            startMediumClause
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
        startMotionClause:
        {
            motion > openBrace ?? 'Expecting an open brace to follow "motion" here.'
        }
        surfaceEntryClause:
        [
            namedClause | startMaterialClause | surfaceTransformClause | noShadowClause |
            boundedByClause | withSeedClause | startMotionClause
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

        // Isosurface clauses.  The function is wrapped in braces even though it is a single
        // expression: it is the whole shape of the surface, often the longest thing in the block, and
        // it reads better fenced off than trailing after a keyword.
        startIsosurfaceClause:
        {
            isosurface > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "isosurface" here.'
        }
        isosurfaceFunctionClause:
        {
            function > openBrace ?? 'Expecting an open brace to follow "function" here.' >
            _expression > closeBrace ?? 'Expecting a close brace to end the function here.'
        }
        isosurfaceEntryClause:
        [
            isosurfaceFunctionClause |
            { threshold > _expression } |
            { accuracy > _expression } |
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
        // 2D transforms applied to a path as a whole -- the 2D counterpart of a surface's
        // transforms.  Translate and scale take a 2D point or an X/Y axis and an amount; rotate
        // takes a single angle, turning the outline in its own plane.
        twoDAxisClause: { [ X | Y ] > _expression }
        twoDTransformByClause: [ twoDAxisClause | _expression ]
        pathTranslateClause: { translate > twoDTransformByClause }
        pathScaleClause: { scale > twoDTransformByClause }
        pathRotateClause: { rotate > _expression }
        extrusionPathClause:
        [
            moveToClause | lineToClause | quadToClause | curveToClause | close |
            { svg > _expression } | { icon > _expression } |
            { text > openBrace ?? 'Expecting an open brace to follow "text" here.' } |
            pathTranslateClause | pathScaleClause | pathRotateClause
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
        splineScaleClause:
        {
            scale > _expression ?? 'Expecting a scale value to follow "scale" here.'
        }
        moveToSplineClause:
        {
            move > to ?? 'Expecting "to" to follow "move" here.' >
            xyzTripleClause > splineScaleClause{?}
        }
        lineToSplineClause:
        {
            line > to ?? 'Expecting "to" to follow "line" here.' >
            xyzTripleClause > splineScaleClause{?}
        }
        quadToSplineClause:
        {
            quad > xyzTripleClause >
            to ?? 'Expecting "to" to follow "quad" control point here.' >
            xyzTripleClause > splineScaleClause{?}
        }
        curveToSplineClause:
        {
            curve > splineControlPointsClause >
            to ?? 'Expecting "to" to follow "curve" control point here.' >
            xyzTripleClause > splineScaleClause{?}
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
            { no > center ?? 'Expecting "center" to follow "no" here.' } |
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
        // A text block used as a path source takes only its own content -- the string, the
        // font and the layout -- and none of a surface's grammar (transforms, material, "open"
        // and such); the shape that carries the path owns all of that.
        pathTextEntryClause:
        [
            { text > _expression } | fontClause |
            { layout > openBrace ?? 'Expecting an open brace after "layout" here.' } |
            kerningClause
        ] ?? 'Expecting a text path property here.'

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
            startIsosurfaceClause     => 'isosurface' |
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
            startCallClause => 'call' |
            startObjectClause => 'object' |
            startCsgClause => 'csg' |
            startGroupClause => 'group' |
            surfaceEntryClause => 'surface'
        ]

        // Group clauses.
        //
        // A loop, which makes what stands in it once for every value in a range.  The name is optional,
        // for when the repetition is wanted and the count is not.  The range is written as an interval
        // like any other -- square brackets take an end in, parentheses leave it out -- so "for
        // [0, 11]" is twelve turns and "for i = (0, 1) by 0.25" is four, at a quarter, a half, three
        // quarters and one.
        startForClause:
        {
            for > [ _identifier | _keyword ] ?? 'Expecting a name for the count to follow "for" here.' >
            in ?? 'Expecting "in" and a range to follow the name here.' > intervalClause >
            { by > _expression }{?} >
            openBrace ?? 'Expecting an open brace to follow the range here.'
        }
        // The same thing with no name for the count, for when the repetition is all that is wanted.
        // It is a word of its own rather than a "for" with a hole in it, so that a reader never has to
        // wonder where the name went.
        startOverClause:
        {
            over > intervalClause > { by > _expression }{?} >
            openBrace ?? 'Expecting an open brace to follow the range here.'
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
            startForClause => 'for' |
            startOverClause => 'over' |
            startIfClause => 'if' |
            startPlaneClause => 'plane' |
            startSphereClause => 'sphere' |
            startCubeClause => 'cube' |
            startCylinderClause => 'cylinder' |
            startConicClause => 'conic' |
            startTorusClause => 'torus' |
            startEggClause => 'egg' |
            startSuperellipsoidClause => 'superellipsoid' |
            startIsosurfaceClause     => 'isosurface' |
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
            startCallClause => 'call' |
            startObjectClause => 'object' |
            startCsgClause => 'csg' |
            startGroupClause => 'group' |
            surfaceEntryClause => 'surface' |
            localClause => 'local'
        ]

        // Scene clauses.
        startSceneClause:
        {
            scene > openBrace ?? 'Expecting an open brace to follow "scene" here.'
        }
        sceneEntryClause:
        [
            startForClause => 'for' |
            startOverClause => 'over' |
            startIfClause => 'if' |
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
            startIsosurfaceClause     => 'isosurface' |
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
            startCallClause => 'call' |
            startObjectClause => 'object' |
            startCsgClause => 'csg' |
            startGroupClause => 'group' |
            background => 'background' |
            startEnvironmentClause => 'environmentBlock' |
            environmentClause => 'environment' |
            localClause => 'local'
        ] ?? 'Unsupported scene property found.'

        // What is true of the space between a scene's objects rather than of any object.  It is
        // written either as a block, which is what anything with more than one thing to say needs,
        // or as the single line the index of refraction arrived as and is kept as a shorthand for.
        startEnvironmentClause:
        {
            environment > openBrace
        }
        environmentEntryClause:
        [
            materialIorClause | startMediumClause
        ] ?? 'Expecting an environment property here.'

        // What fills a piece of space: something a ray passes through rather than strikes.
        startMediumClause:
        {
            medium > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "medium" here.'
        }
        mediumEntryClause:
        [
            { absorption > _expression } |
            // The pigment form first, since "emission pigment ..." would otherwise be tried as
            // an expression, which it is not.
            { emission > pigment } | { emission > _expression } |
            { scattering > _expression } | { anisotropy > _expression } |
            {
                density > function > openBrace ?? 'Expecting an open brace to follow "function" here.' >
                _expression > closeBrace ?? 'Expecting a close brace to end the function here.'
            } |
            {
                density > patternClause >
                openBrace ?? 'Expecting an open brace to follow the pattern here.'
            } |
            { density > _expression } | { samples > _expression } | { bounces > _expression } |
            { phase > rayleigh ?? 'Expecting "rayleigh" to follow "phase" here.' }
        ] ?? 'Expecting a medium property here.'

        environmentClause:
        {
            environment > [ {
                index > of ?? 'Expecting "of" to follow "index" here.' >
                refraction ?? 'Expecting "refraction" to follow "of" here.'
            } | ior ] ?? 'Expecting "ior" or "index of refraction" to follow "environment" here.' >
            _expression
        }

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
                { interior > startThingClause } | { medium > startThingClause } |
                startLightClause |
                startPlaneClause | startSphereClause | startCubeClause | startCylinderClause |
                startConicClause | startTorusClause | startExtrusionClause | startLatheClause |
                startBlobClause | startTubeClause | startSweepClause | startTextClause |
                startLsystemClause | startHeightFieldClause | startTriangleClause |
                startSmoothTriangleClause | startParallelogramClause | startDiscClause |
                startGenericShapeClause | startEggClause | startSuperellipsoidClause |
                startPatchClause | startIsosurfaceClause | startObjectFileClause | startObjectClause |
                startCsgClause | startGroupClause
            ]
        }
        // A function a scene writes for itself.  What follows the parenthesis is read in hand rather
        // than spelled out here, parameters with defaults not being a shape this grammar says well.
        startFunctionClause:
        {
            function > [ _identifier | _keyword ] ?? 'Expecting a name to follow "function" here.' >
            leftParen ?? 'Expecting a parameter list to follow the function name here.'
        }
        functionParameterClause:
        {
            [ _identifier | _keyword ] > { assignment > _expression }{?} > comma{?}
        }
        // A primitive a scene writes for itself.  The kind it gives back is named exactly, rather than
        // merely "a surface", because the block after a call takes that kind's own clauses -- so the
        // parser has to know which those are before it reads them.
        startPrimitiveClause:
        {
            primitive > [ _identifier | _keyword ] ?? 'Expecting a name to follow "primitive" here.' >
            leftParen ?? 'Expecting a parameter list to follow the name here.'
        }
        primitiveKindClause:
        {
            arrow ?? 'Expecting "->" and the kind of thing this gives back here.' >
            [
                group | union | difference | intersection |
                plane | sphere | cube | cylinder | conic | torus | egg | superellipsoid |
                isosurface | patch | lathe | blob | tube | sweep | extrusion | text | lsystem |
                heightfield | parallelogram | disc | triangle |
                { smooth > triangle } | { generic > shape } | { object > file } |
                pigment | material | interior | medium
            ] ?? 'Expecting the kind of surface this gives back here.' >
            openBrace ?? 'Expecting an open brace to follow the kind here.'
        }
        primitiveReturnClause:
        {
            return ?? 'Expecting "return", "if" or "switch" here; a body must say what it gives back.'
        }
        // A call of one, which may carry values and may be followed by a block of that kind's clauses.
        startCallClause:
        {
            object > [ _identifier | _keyword ] ?? 'Expecting a name to follow "object" here.' >
            leftParen
        }
        argumentClause:
        {
            _expression > comma{?}
        }
        functionKindClause:
        {
            arrow ?? 'Expecting "->" and the kind of thing the function gives back here.' >
            [ number | color | vector ] ?? 'A function gives back a number, a color or a vector.' >
            openBrace ?? 'Expecting an open brace to follow the function kind here.'
        }
        localClause:
        {
            [ _identifier | _keyword ] > assignment > _expression
        }
        // An "else" that may or may not be there.  A choice standing among surfaces is allowed to have
        // no second arm, so this one asks rather than insists, and carries no complaint of its own.
        optionalElseClause:
        {
            else
        }
        // A choice.  The same words open both kinds: the one that ends a body, where both ways out give
        // an answer so there is nowhere for a second one to go and nowhere for a missing one to hide,
        // and the one that stands among surfaces, where an arm makes things rather than answering and
        // making nothing is a perfectly good thing for it to do.  Which is being read is settled by
        // where it stands, and the difference is only in what follows the brace.
        startIfClause:
        {
            if > leftParen ?? 'Expecting a condition in parentheses to follow "if" here.' >
            _expression >
            rightParen ?? 'Expecting a close parenthesis after the condition here.' >
            openBrace ?? 'Expecting an open brace to follow the condition here.'
        }
        startElseClause:
        {
            else ?? 'Expecting "else" here; both ways out of a choice must give an answer.'
        }
        // A selection: one value held against a run of cases, the first that matches giving the answer.
        // It ends a body exactly as a choice does and for the same reasons, and the "default" is what
        // makes every path answer, which is why it is demanded rather than merely allowed.
        startSwitchClause:
        {
            switch > leftParen ?? 'Expecting a value in parentheses to follow "switch" here.' >
            _expression >
            rightParen ?? 'Expecting a close parenthesis after the value here.' >
            openBrace ?? 'Expecting an open brace to follow the value here.'
        }
        startCaseClause:
        {
            case > _expression > { comma > _expression }{*} >
            openBrace ?? 'Expecting an open brace to follow the case here.'
        }
        startDefaultClause:
        {
            default ?? 'Expecting "case" or "default" here; a selection must have a last way out.' >
            openBrace ?? 'Expecting an open brace to follow "default" here.'
        }
        functionReturnClause:
        {
            return ?? 'Expecting "return", "if" or "switch" here; a body must say what it gives back.' >
            _expression
        }
        setVariableClause:
        {
            [ _identifier | _keyword ] > assignment > _expression
        }

        // Top-level clause.
        [
            startPrimitiveClause      => 'HandleStartPrimitiveClause' |
            startCallClause           => 'HandleStartCallClause' |
            startFunctionClause       => 'HandleStartFunctionClause' |
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
            startIsosurfaceClause     => 'HandleStartIsosurfaceClause' |
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
            startForClause            => 'HandleStartForClause' |
            startOverClause           => 'HandleStartForClause' |
            startIfClause             => 'HandleStartSurfaceIfClause' |
            background                => 'HandleBackgroundClause' |
            startEnvironmentClause    => 'HandleStartEnvironmentClause' |
            environmentClause         => 'HandleEnvironmentClause' |
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
