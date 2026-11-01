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

        _keywords: 'absorption', 'accuracy', 'agate', 'agx', 'alignment', 'along', 'ambient', 'amplitude', 'and',
            'above', 'angle', 'angles', 'aperture', 'apply',
            'anisotropy', 'antialiasing', 'apart', 'are', 'around', 'area', 'at', 'author', 'axiom', 'axisU', 'axisV', 'azimuth', 'background', 'banded',
            'align', 'back', 'baseline', 'behind', 'below', 'bilinear', 'centered', 'black', 'blades', 'blend', 'blob', 'blur', 'bold', 'bottom', 'bouncing',
            'balance', 'bounces', 'bounded', 'boxed', 'bozo', 'brick', 'brightness', 'brilliance',
            'by', 'cache', 'camera', 'case', 'caustics', 'center', 'checker', 'clarity', 'clip', 'close', 'clump', 'coat', 'color', 'crevices', 'crown',
            'columns', 'comb', 'commands', 'comment', 'completeBranch', 'conic', 'context', 'controls',
            'copyright', 'count', 'crackle', 'csg', 'cube', 'cubic', 'curl', 'curve', 'cycle', 'cylinder', 'cylindrical',
            'decal', 'default', 'degrees', 'density', 'dents', 'depth', 'description', 'diameter', 'difference', 'diffuse', 'direction', 'disc',
            'disclaimer', 'discontinuous', 'dispersion', 'distance', 'distant', 'elevation', 'drawLine', 'droop', 'dust', 'east', 'edge', 'edges', 'egg', 'else', 'emission', 'environment', 'error', 'exposure', 'extrusion', 'factor', 'facing', 'fade', 'falloff', 'false', 'field', 'file', 'flat', 'fracture',
            'fainter', 'film', 'filter', 'finer', 'fisheye', 'flatness', 'flop', 'focal', 'font', 'for', 'frequency', 'fresnel', 'frizz', 'front', 'from', 'function', 'gamma', 'gap', 'generations', 'generic', 'glare', 'gradient', 'granite', 'grime',
            'grain', 'grayscale', 'group', 'hair', 'hanging', 'height', 'heightfield', 'hexagon', 'hole', 'horizontal',
            'icon', 'if', 'ignore', 'image', 'import', 'include', 'index', 'info', 'inherited', 'inner', 'interior', 'intersection',
            'in', 'ior', 'isosurface', 'italic', 'jitter', 'joining', 'kern', 'kerning', 'kill', 'lathe', 'layer', 'layout', 'leaf', 'lean', 'left', 'length',
            'leopard', 'light', 'line', 'linear', 'location', 'look', 'loose', 'lsystem',
            'marble', 'material', 'materials', 'matrix', 'max', 'medium', 'melanin', 'metal', 'metallic', 'min', 'mortar',
            'motion', 'mottled', 'move', 'named', 'neutral', 'no', 'noise', 'none', 'number', 'octaves', 'normal', 'normals', 'north', 'not', 'null', 'object', 'of', 'offset', 'on', 'once',
            'open', 'or', 'orthographic', 'paraboloid', 'over', 'panoramic', 'parallel', 'parallelogram', 'hyperboloid', 'quadric', 'cross', 'constant',
            'sdf', 'julia', 'iterations', 'c', 'parametric', 'patch', 'path', 'patches', 'perspective', 'phase', 'photons', 'physical', 'picked', 'pieces', 'pigment', 'pipes', 'primitive',
            'pitchDown', 'pitchUp', 'pixel', 'planar', 'plane', 'point', 'points', 'poly',
            'portal', 'position', 'power', 'productions', 'profile', 'quad', 'radial', 'radians', 'radii', 'radius', 'reach', 'reflection', 'reflective', 'return',
            'refraction', 'regular', 'render', 'ribbon', 'right', 'ripples', 'rollLeft', 'rollRight',
            'gives', 'glass', 'grown', 'ramp', 'rayleigh', 'rotate', 'roughness', 'rounded', 'rows', 'samples', 'scale', 'scallop', 'scanner', 'scattering', 'scene', 'seed', 'serial', 'shadow', 'shadows', 'sheen',
            'shape', 'shear', 'shininess', 'shutter', 'sides', 'sine', 'sink', 'size', 'sized', 'sky', 'smooth', 'soft', 'software', 'solid', 'source',
            'specular', 'spectral', 'spacing', 'sphere', 'spherical', 'splash', 'squares', 'spline', 'spot', 'square', 'startBranch', 'steps', 'steepest', 'steepness', 'strand', 'step', 'stretched', 'strength', 'stripes', 'subsurface', 'sun',
            'superellipsoid', 'surface', 'surfaces', 'saddle', 'susceptibility', 'svg', 'sweep', 'swells', 'switch', 'taper', 'tapered', 'text', 'thin', 'threshold', 'tint', 'title', 'to', 'toward', 'top', 'toroidal', 'torus',
            'toVertical',
            'temperature', 'thickness', 'tightness', 'tilted', 'trace', 'transform', 'translate', 'transparency', 'triangle', 'triangular', 'tropism', 'true', 'tube', 'tubes', 'tumble', 'twist',
            'u', 'v', 'turbidity', 'turbulence', 'turnAround', 'turned', 'turnLeft', 'turnRight', 'twig', 'ultraWide', 'uncached', 'under', 'union', 'up', 'uSteps',
            'vary', 'vector', 'vertical', 'view', 'vSteps', 'warning', 'wave', 'wavelength', 'waves', 'wear', 'weather', 'weathering', 'weight', 'white', 'whole', 'width', 'with', 'within', 'wood',
            'wrinkles', 'shapes', 'rules', 'extrude', 'split', 'repeat', 'faces', 'roof', 'gable', 'hip', 'fill', 'place', 'choose', 'nothing', 'overhang', 'verge', 'across', 'gables', 'snow', 'animation', 'frames', 'rate', 'keys', 'ease', 'out', 'hold', 'loop', 'still', 'upright', 'banked', 'wheelbase', 'body', 'mass', 'velocity', 'spin', 'bounce', 'friction', 'drag', 'tether', 'box', 'gravity', 'wind', 'swarm', 'spread', 'speed', 'life', 'since', 'joint', 'pivot', 'axis', 'limits', 'aim', 'mark', 'span', 'falls', 'burst',
            'channel', 'red', 'green', 'blue', 'alpha', 'map', 'directx',
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
        // The range a loop counts through.  This is a rule apart from `intervalClause`, which `over`
        // uses, and the difference is that **this one carries no hard errors of its own**.
        //
        // A "for" may be given a range or a list, and the two can begin alike: a range says whether
        // each end is in or out the way mathematics does, so `(0, 5]` opens with a parenthesis, and
        // so does a parenthesised expression such as `(balls)`.  Telling them apart therefore means
        // trying the range and, when the comma never arrives, letting the expression be tried
        // instead -- and a rule that stops with an error of its own can never be backtracked out of.
        // Falling out quietly is the whole job of this rule; the message for what fits neither is on
        // the alternation above.
        loopRangeClause:
        [
            // A square bracket is unambiguous -- nothing else a loop accepts begins with one, since a
            // list is never written in brackets -- so this form can say plainly what is wrong with a
            // range that is malformed, rather than falling through and being complained about as a
            // tuple.
            {
                openBracket > _expression >
                comma ?? 'Expecting a comma here.' > _expression >
                [ closeBracket | rightParen ] ?? 'Expecting a close bracket or right parenthesis here.'
            } |
            // A left parenthesis is not unambiguous, and this is the form that must therefore fall
            // out quietly: `(0, 5]` is a range and `(balls)` is a list, and which one it is is not
            // known until the comma either arrives or does not.
            {
                leftParen > _expression >
                comma > _expression >
                [ closeBracket | rightParen ]
            }
        ]
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
            // Not insisting on "wave": `cubic` begins a pattern too, and a pigment may be named `sine`.
            [ ramp | sine | triangle | scallop | cubic |
              { poly > _expression } ] >
            wave
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
                scanner ?? 'Expecting "scanner" to follow "line" or "pixel" here.'
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
        // How many times light is followed from one matte surface to the next, how many directions a matte
        // surface looks in for it the first time a ray meets one, and whether the light so found is cached
        // and blended between places -- with, optionally, how far the blending may stray.  That is said
        // with its own word: a bare number left optional after `cache` reads whatever comes next as one,
        // and a `white balance` on the next line is a color.
        diffuseLightClause:
        {
            diffuse > [
                { [ bounces | samples ] > _expression } |
                { cache > { error > _expression }{?} }
            ] ?? 'Expecting "bounces", "samples" or "cache" to follow "diffuse" here.'
        }
        // How many surfaces a ray from the eye may be mirrored or bent at before it is followed no further.
        traceDepthClause:
        {
            trace > depth ?? 'Expecting "depth" to follow "trace" here.' > _expression
        }
        // Whether light is followed forward from the lamps through mirrors and glass to where it lands, and
        // with how many photons.
        causticsClause:
        {
            caustics > { photons > _expression }{?}
        }
        // How the light a render ends with is developed into a picture: eased toward white, by a film's
        // curve, or cut off at white.
        filmCurveClause:
        {
            film > curve ?? 'Expecting "curve" to follow "film" here.' >
            [ soft | neutral | agx | none ] ?? 'Expecting "soft", "neutral", "agx" or "none" to follow "film curve" here.'
        }
        // How many walks a surface whose light comes up from under it takes, the first time a ray meets one.
        subsurfaceSamplesClause:
        {
            subsurface > samples ?? 'Expecting "samples" to follow "subsurface" here.' > _expression
        }
        // How many places the surfaces that glow of their own are looked at from, as a light, in a scene that
        // follows bounced light.
        emissionSamplesClause:
        {
            emission > samples ?? 'Expecting "samples" to follow "emission" here.' > _expression
        }
        // How many directions the weathering patterns look about in at each place they are asked about.
        weatheringSamplesClause:
        {
            weathering > samples ?? 'Expecting "samples" to follow "weathering" here.' > _expression
        }
        // How many directions a rough surface looks in for what it mirrors, the first time a ray meets
        // one.
        reflectionSamplesClause:
        {
            reflection > samples ?? 'Expecting "samples" to follow "reflection" here.' > _expression
        }
        // Ambient stands in for light this renderer does not trace, and every material either names its
        // own or takes what the scene settles on it.  This turns the whole lot up or down at once, which
        // is the only way to reach the ones that named their own.
        scaleAmbientClause:
        {
            scale > ambient ?? 'Expecting "ambient" to follow "scale" here.' >
            by ?? 'Expecting "by" to follow "scale ambient" here.' >
            _expression
        }
        contextEntryClause:
        [
            startInfoClause | scannerClause | anglesClause | settingOnClause |
            settingOffClause | mediumSamplesClause | diffuseLightClause | causticsClause | traceDepthClause |
            filmCurveClause | subsurfaceSamplesClause | weatheringSamplesClause | emissionSamplesClause |
            reflectionSamplesClause | antiAliasingClause |
            scaleAmbientClause |
            colorDepthClause | grayscale | spectral | whiteBalanceClause | animationClause | physicsContextClause |
            contextPropertyClause
        ] ?? 'Expecting a context property here.'

        // How gravity pulls and the air moves, for the scene's bodies and swarms.
        physicsContextClause:
        {
            [ gravity | wind ] > _expression
        }

        // How long the scene runs as an animation, in seconds or in frames, at what rate, and which frame stands
        // for it as a still.
        animationClause:
        {
            animation > [ length | frames ] ?? 'Expecting "length" or "frames" to follow "animation" here.' >
            _expression > { rate > _expression }{?} > { still > _expression }{?}
        }

        // How much of each channel reaches the file, and whether any of it is color at all.  These
        // decide what the image on disk can hold, so a scene that wants sixteen bits -- or wants no
        // color -- can say so rather than depending on being rendered with the right flags.
        colorDepthClause:
        {
            color > depth ?? 'Expecting "depth" to follow "color" here.' > _expression
        }
        // What the camera is balanced for: the temperature of the glow that should come out white.
        whiteBalanceClause:
        {
            white > balance ?? 'Expecting "balance" to follow "white" here.' > _expression
        }

        // How hard to work at the edges within a pixel.  This belongs with the scanner and the
        // medium samples rather than with anything a scene is made of, and a scene that needs it
        // needs to be able to say so: said only on the command line, it is lost the moment anything
        // renders the scene without repeating it.
        antiAliasingClause:
        {
            antialiasing >
            [ depth | threshold ] ?? 'Expecting "depth" or "threshold" to follow "antialiasing" here.' >
            _expression
        }

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
        // What the camera's opening is made of -- how many blades, and how far they are turned -- which
        // shapes what is out of focus and the rays bright points throw out.
        bladesClause:
        {
            blades > _expression > { turned > _expression }{?}
        }
        cameraEntryClause:
        [
            namedClause | locationClause | lookAtClause | upClause | fieldOfViewClause |
            { aperture > _expression } | focalClause | blurSamplesClause |
            { shutter > _expression } | { seed > _expression } | { exposure > _expression } |
            bladesClause | { glare > _expression }
        ] ?? 'Expecting a camera property here.'

        // Light clauses.  One opener serves every sort, told apart by the word before "light":
        // nothing or "point" for a lamp, "distant" for the sun, "spot" for a cone, "area" for a
        // glowing panel, "sky" for the light of the whole sky.  What follows is either a block describing
        // one or the name of a light already described, which may carry a block of its own to adjust
        // what it found -- the same shape "object <name>" has for a surface.
        startLightClause:
        {
            [ { [ point | distant | spot | area | sky ] > light } | light ] >
            [ openBrace | { [ _identifier | _keyword ] > openBrace{?} } ]
                ?? 'Expecting an open brace, or the name of a light, to follow "light" here.'
        }
        // What light a light gives: a color, or the temperature of something glowing, and either way
        // how bright.
        lightColorClause: { [ color | temperature | brightness ] > _expression }
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
        // Where a thing goes, said in terms of the things beside it rather than in numbers.  Each
        // relation settles one direction by contact; see Geometry/Placement.cs for what that means,
        // and Geometry/PlacementSettler.cs for why it cannot be worked out until rendering starts.
        placementClause:
        {
            [
                on | under | behind |
                { centered > on ?? 'Expecting "on" to follow "centered" here.' } |
                { [ left | right | front ] > of ?? 'Expecting "of" to follow here.' } |
                {
                    align >
                    [ left | right | top | bottom | front | back ]
                        ?? 'Expecting a side to line up here.' >
                    with ?? 'Expecting "with" to follow the side here.'
                }
            ] >
            _expression ?? 'Expecting the name of the thing to place this against here.' >
            { by > _expression ?? 'Expecting how far past it to go here.' }{?}
        }
        // A place along a spline: a share of its length, or a distance, the thing turned to run with it.
        alongClause:
        {
            along > _expression > [ at | by ] ?? 'Expecting "at" or "by" to follow the spline here.' >
            _expression > [ upright | banked ]{?} > { wheelbase > _expression }{?}
        }
        transformClause:
        [
            translateClause | scaleClause | rotateClause | shearClause | matrixClause | alongClause
        ]{*}
        
        // Pattern clauses.
        patternClause:
        [
            imagePatternClause | agate | boxed | brick | checker | crevices | cubic | edges | facing | flop | hexagon | leopard | planar |
            radial | ripples | square | triangular | waves |
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
        // A picture read as a pattern, as a height field reads one: its gray, or one channel of it, laid as an image
        // pigment is laid.
        imagePatternClause:
        {
            imageClause > imageMapTypeClause{?} > imageChannelClause{?}
        }
        imageChannelClause:
        {
            channel > [ red | green | blue | alpha ] ?? 'Expecting "red", "green", "blue" or "alpha" to follow "channel" here.'
        }
        brickSizeClause:
        {
            [ brick | mortar ] > size > _expression
        }
        // How far a crevice or an edge is looked for about a place, and which way a surface is asked to face.
        weatheringEntryClause:
        [
            { radius > _expression } | { toward > [ X | Y | Z | _expression ] }
        ]

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
        // A channel makes the picture a pattern, whose map of pigments follows in braces.
        imagePigmentClause:
        {
            imageClause > imageMapTypeClause{?} > imageChannelClause{?}
        }
        // What may be said of an image in braces after it, besides its transforms: how bright it is, which
        // a photograph of the sky needs, being in whatever units the camera that took it left it in.
        imageEntryClause: { brightness > _expression }
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
            blendPigmentClause | mottledPigmentClause | imagePigmentClause | patternPigmentClause |
            startPhysicalSkyClause | { color > _expression } |
            _expression
        ] ?? 'Expecting a pigment definition here.'

        // Material clauses.
        startMaterialClause:
        {
            material > [
                openBrace | { inherited > openBrace{?} } |
                { blend > patternClause > openBrace ?? 'Expecting an open brace to follow the pattern here.' } |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "material" here.'
        }
        // A roughness, or how metallic a surface is, may run from one value to another by a pattern -- a texture
        // set's roughness or metalness map -- which follows as a field's sizes do.
        materialValueClause:
        {
            [ ambient | emission | diffuse | specular | shininess | reflective | roughness | transparency |
              brilliance | grain ] >
            _expression > { to > _expression }{?}
        }
        materialIorClause:
        {
            [ {
                index > of ?? 'Expecting "of" to follow "index" here.' >
                refraction ?? 'Expecting "refraction" to follow "of" here.'
            } | ior ] > _expression
        }
        // The amount may be left off, and then what follows may be the material's next entry rather
        // than an amount.  The entries are tried before an expression, since an expression would
        // take the next entry's word for the name of a variable -- a keyword may name one -- and the
        // entry would then be lost: `metallic reflective 0.3` read as metallic by `reflective`.  An amount may run
        // to another by a pattern, a metalness map, as a roughness may.
        materialMetallicClause:
        {
            metallic > [
                pigment | normal | fresnel | materialValueClause | materialAnisotropyClause | materialMelaninClause |
                materialMetalClause | startInteriorClause | startDecalClause | startFilmClause |
                startSubsurfaceClause | startCoatClause | startSheenClause | _expression{?}
            ] > { to > _expression }{?}
        }
        // A real metal: by name, measured, or by the color it mirrors square-on and, if said, an edge color for how
        // each color fares at a slant.
        materialMetalClause:
        {
            metal > _expression > { edge > _expression }{?}
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
            { dispersion > _expression } | { glass > _expression } | startMediumClause
        ] ?? 'Expecting an interior property here.'
        // How a surface's skin is roughened: a pattern whose slope tilts the normal from point to
        // point.  It is written as the pigment's sibling because that is what it is -- another
        // pattern over the same surface -- and it is kept apart from the pigment because the two
        // are rarely the same field.  A marble's veins and the roughness of its surface have
        // nothing to do with one another, and each wants its own scale and its own footing.
        startNormalClause:
        {
            [ patternClause | normalMapClause ] > openBrace ?? 'Expecting an open brace to follow the pattern here.'
        }
        // A picture whose colors are the normals themselves, as a texture set ships one: drawn OpenGL's way, its
        // green pointing up the picture, unless it says it was drawn DirectX's.
        normalMapClause:
        {
            map > _expression > imageMapTypeClause{?} > directx{?}
        }
        normalEntryClause:
        {
            depth > _expression
        }
        // A marking painted over the pigment: inside an outline one color, outside the surface's
        // own.  It is written on the material rather than as a pigment because a decal is read in
        // the space of the surface the material was written on, and a pigment inside another one is
        // handed points already in the space of whatever part of a group the ray met.
        startDecalClause:
        {
            decal > openBrace ?? 'Expecting an open brace to follow "decal" here.'
        }
        // Anything that matches none of these is tried as a transform, the way a surface's own
        // block does, so the list carries no complaint of its own.
        decalEntryClause:
        [
            {
                path > [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting an outline, or the name of one, to follow "path" here.'
            } |
            { color > _expression } |
            planar | cylindrical | spherical |
            {
                toroidal >
                radius ?? 'Expecting "radius" and the radius of the ring to follow "toroidal" here.' >
                _expression
            } |
            {
                [ min | max ] > [ Y | radius ] ?? 'Expecting "Y" or "radius" to follow here.' >
                _expression
            } |
            {
                fade > from ?? 'Expecting "from" and the angle the fade starts at to follow "fade" here.' >
                _expression >
                to ?? 'Expecting "to" and the angle the decal is gone by here.' > _expression
            } |
            { no > fade ?? 'Expecting "fade" to follow "no" here.' }
        ]
        // How much of each melanin hair wearing the material holds: a named shade, or an amount of
        // eumelanin with, after a comma, one of pheomelanin.
        materialMelaninClause:
        {
            melanin > _expression > { comma > _expression }{?}
        }
        // A film on the surface, thinner than light's own wavelength -- a soap bubble's skin, oil on a
        // puddle -- which colors what the surface mirrors.  How thick it is is in nanometers: the same
        // everywhere, or running from one thickness to another by a pattern, which follows as a field's
        // sizes do.
        startFilmClause:
        {
            film > openBrace ?? 'Expecting an open brace to follow "film" here.'
        }
        filmEntryClause:
        [
            { thickness > _expression > { to > _expression }{?} } | materialIorClause
        ] ?? 'Expecting a film property here.'
        // Light that goes into the surface, wanders about under it and comes out somewhere else -- wax, marble,
        // skin -- and how far it travels inside, one distance or one for each of red, green and blue.
        startSubsurfaceClause:
        {
            subsurface > openBrace ?? 'Expecting an open brace to follow "subsurface" here.'
        }
        subsurfaceEntryClause:
        [
            { radius > _expression } | { thickness > _expression }
        ] ?? 'Expecting a subsurface property here.'
        // A clear coat over the surface -- lacquer, varnish, a glaze -- which mirrors by its own index over
        // whatever the rest of the material says lies beneath it: how rough its own surface is, its index,
        // and the color it gives what is beneath, seen square-on.
        startCoatClause:
        {
            coat > openBrace ?? 'Expecting an open brace to follow "coat" here.'
        }
        coatEntryClause:
        [
            { roughness > _expression } | materialIorClause | { tint > _expression }
        ] ?? 'Expecting a coat property here.'
        // How much a rough surface is brushed, its facets leaning further across its grooves than along them,
        // and which way the grooves run in the surface's own space: along a direction, or round an axis.
        materialAnisotropyClause:
        {
            anisotropy > _expression > {
                [ along | around ] > [ X | Y | Z | _expression ]
                    ?? 'Expecting "X", "Y", "Z" or a direction to follow here.'
            }{?}
        }
        // The sheen of cloth -- velvet, satin, felt -- a soft shine coming up toward a graze: the color its
        // fibers give back, and how rough it is, a satin's narrow edge to a felt's broad glow.
        startSheenClause:
        {
            sheen > openBrace ?? 'Expecting an open brace to follow "sheen" here.'
        }
        sheenEntryClause:
        [
            { color > _expression } | { roughness > _expression }
        ] ?? 'Expecting a sheen property here.'
        materialEntryClause:
        [
            pigment | normal | fresnel | materialValueClause | materialAnisotropyClause | materialMetallicClause |
            materialMetalClause | materialMelaninClause | startInteriorClause | startDecalClause | startFilmClause |
            startSubsurfaceClause | startCoatClause | startSheenClause
        ] ?? 'Expecting a material property here.'

        // Common surface clauses.
        noShadowClause:
        {
            no > [ shadow | caustics ] ?? 'Expecting "shadow" or "caustics" to follow "no" here.'
        }
        // A surface the sky comes into a room through: a window, a door, a skylight.
        portalClause: { portal }
        // A surface saying that the stuff inside it lights what is around, and not merely itself.
        givesLightClause:
        {
            gives > light ?? 'Expecting "light" to follow "gives" here.' >
            { samples > _expression }{?}
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
        // Hair clauses: what grows on a surface, written inside the surface it grows on.
        startHairClause:
        {
            hair > openBrace ?? 'Expecting an open brace to follow "hair" here.'
        }
        hairEntryClause:
        [
            { density > _expression } | { count > _expression } | { length > _expression } |
            { vary > _expression } | { thickness > _expression } | { lean > _expression } |
            { comb > _expression } | { curl > _expression > { radius > _expression }{?} } |
            { frizz > _expression } | { clump > _expression > { size > _expression }{?} } |
            { droop > _expression } | startMaterialClause | withSeedClause |
            {
                patches > patternClause >
                openBrace ?? 'Expecting an open brace to follow the pattern here.'
            } |
            { facing > _expression > { by > _expression }{?} }
        ] ?? 'Expecting a hair property here.'
        // The weather a group or object has been out in: grime in its crevices, dust and snow on top, wear on
        // its edges -- baring a material, if one is named -- and how far about each place to look.
        startWeatherClause:
        {
            weather > openBrace ?? 'Expecting an open brace to follow "weather" here.'
        }
        weatherEntryClause:
        [
            { [ grime | dust | splash | snow ] > _expression > { color > _expression }{?} } |
            { wear > _expression > to{?} } |
            { radius > _expression }
        ] ?? 'Expecting a weather property here.'
        // A body: a surface moved by gravity, the air, what it strikes and what it is tied to.
        startBodyClause:
        {
            body > openBrace ?? 'Expecting an open brace to follow "body" here.'
        }
        bodyEntryClause:
        [
            { [ mass | bounce | friction | drag | radius | velocity | spin ] > _expression } |
            { shape > [ sphere | box ] ?? 'Expecting "sphere" or "box" to follow "shape" here.' } |
            {
                tether > to ?? 'Expecting "to" to follow "tether" here.' > _expression >
                { length > _expression }{?}
            }
        ] ?? 'Expecting a body property here.'
        surfaceEntryClause:
        [
            namedClause | startMaterialClause | surfaceTransformClause | noShadowClause | portalClause |
            givesLightClause | boundedByClause | withSeedClause | startMotionClause |
            startHairClause | placementClause | startWeatherClause | startBodyClause | spanClause
        ]
        // A surface stretched between two named places: its origin at the first, its +X running toward the second,
        // and, stretched, its X scaled by the distance between them.
        spanClause:
        {
            span > from ?? 'Expecting "from" to follow "span" here.' > _expression >
            to ?? 'Expecting "to" and the name of the other place here.' > _expression > stretched{?}
        }
        
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
            { [ min | max ] > Y ?? 'Expecting "Y" to follow "min" or "max" here.' > _expression } |
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
        // -- The named quadrics.  Each is the unit form of its family: scaling covers every shape
        // -- there is, so none of them asks for a number the way a torus asks for its two radii.
        startParaboloidClause:
        {
            paraboloid > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "paraboloid" here.'
        }
        startHyperboloidClause:
        {
            hyperboloid > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "hyperboloid" here.'
        }
        startSaddleClause:
        {
            saddle > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "saddle" here.'
        }
        saddleEntryClause:
        [
            { width > _expression } |
            { depth > _expression } |
            surfaceEntryClause
        ]
        startQuadricClause:
        {
            quadric > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "quadric" here.'
        }
        // The ten coefficients, in the four groups they fall into naturally rather than as one run of
        // numbers: the squared terms, the cross terms, the linear terms and the constant.
        quadricEntryClause:
        [
            { squares > _expression } |
            { cross > _expression } |
            { linear > _expression } |
            { constant > _expression } |
            surfaceEntryClause
        ]
        // -- Shapes found by sphere tracing ----------------------------------------------------
        // Both are walked by stepping along the ray by however much room the shape reports, so both
        // ask that the answer really is a distance.  See `isosurface` for the marcher that takes any
        // function at all and cannot miss a crossing, at the price of being slower.
        startSdfClause:
        {
            sdf > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "sdf" here.'
        }
        sdfEntryClause:
        [
            fieldFunctionClause | { accuracy > _expression } | surfaceEntryClause
        ]
        startJuliaClause:
        {
            julia > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "julia" here.'
        }
        juliaEntryClause:
        [
            { c > _expression } | { iterations > _expression } |
            { accuracy > _expression } | surfaceEntryClause
        ]
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
        fieldFunctionClause:
        {
            function > openBrace ?? 'Expecting an open brace to follow "function" here.' >
            _expression > closeBrace ?? 'Expecting a close brace to end the function here.'
        }
        isosurfaceEntryClause:
        [
            fieldFunctionClause |
            { threshold > _expression } |
            { accuracy > _expression } |
            surfaceEntryClause
        ]

        // Parametric surface clauses.  The three coordinates are each fenced in braces for the
        // same reason an isosurface's function is: each is a whole shape's worth of arithmetic and
        // reads better set apart than trailing after a word.
        //
        // **The coordinates are the upper-case axis words the language already has**, and that is
        // worth more than a tidier look: inside these expressions the *lower-case* x, y and z are a
        // field's own names for what it is a function of, so `X { ... }` and `x` being different
        // things is exactly the distinction a reader needs.  Spelling the clauses in lower case would
        // have needed a second set of keywords differing from the first only in case.
        startParametricClause:
        {
            parametric > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "parametric" here.'
        }
        parametricPartClause:
        {
            [ X | Y | Z ] >
            openBrace ?? 'Expecting an open brace to follow the coordinate here.' >
            _expression > closeBrace ?? 'Expecting a close brace to end the coordinate here.'
        }
        parametricEntryClause:
        [
            parametricPartClause |
            { [ u | v ] > intervalClause } |
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
        startBilinearPatchClause:
        {
            bilinear > patch > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "patch" here.'
        }
        bilinearPatchEntryClause:
        [
            {
                points > _expression > comma ?? 'Expecting a comma here.' > _expression >
                comma ?? 'Expecting a comma here.' > _expression >
                comma ?? 'Expecting a comma here.' > _expression
            } |
            {
                normals > _expression > comma ?? 'Expecting a comma here.' > _expression >
                comma ?? 'Expecting a comma here.' > _expression >
                comma ?? 'Expecting a comma here.' > _expression
            } |
            surfaceEntryClause
        ]
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
            {
                path > [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting an outline, or the name of one, to follow "path" here.'
            } |
            { rounded > _expression } |
            extrudedSurfaceEntryClause
        ]
        startTaperedExtrusionClause:
        {
            tapered > extrusion > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "extrusion" here.'
        }
        taperedExtrusionEntryClause:
        [
            { taper > _expression } | extrusionEntryClause
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
            {
                path > [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting an outline, or the name of one, to follow "path" here.'
            } |
            surfaceEntryClause
        ]

        // Blob clauses.
        blobSphereEntryClause:
        [
            { center > _expression } |
            { radius > _expression } |
            { strength > _expression } |
            pigment
        ]
        blobCylinderEntryClause:
        [
            { from > _expression } |
            { to > _expression } |
            { radius > _expression } |
            { strength > _expression } |
            pigment
        ]
        blobPlaneEntryClause:
        [
            { at > _expression } |
            { normal > _expression } |
            { radius > _expression } |
            { strength > _expression } |
            pigment
        ]
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
            { plane > openBrace ?? 'Expecting an open brace after "plane" here.' } |
            surfaceEntryClause
        ]

        // Swell clauses.
        swellTrainEntryClause:
        [
            { amplitude > _expression } |
            { steepness > _expression } |
            { wavelength > _expression } |
            { direction > _expression } |
            { phase > _expression }
        ] ?? 'Expecting a wave train property here.'
        startSwellsClause:
        {
            swells > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "swells" here.'
        }
        swellsEntryClause:
        [
            { width > _expression } |
            { depth > _expression } |
            { accuracy > _expression } |
            { wave > openBrace ?? 'Expecting an open brace after "wave" here.' } |
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

        // Field clauses.
        startPathClause:
        {
            path > openBrace ?? 'Expecting an open brace to follow "path" here.'
        }
        // A value that changes over time, given at keys; read by hand past the open brace.
        startKeysClause:
        {
            keys > smooth{?} > loop{?} > openBrace ?? 'Expecting an open brace to follow "keys" here.'
        }
        startFieldClause:
        {
            field > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "field" here.'
        }
        fieldEntryClause:
        [
            {
                within > [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting an outline, or the name of one, to follow "within" here.'
            } |
            {
                along > [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting a path, or the name of one, to follow "along" here.'
            } |
            { rows > _expression > apart ?? 'Expecting "apart" to follow the number of rows here.' > _expression } |
            { offset > _expression } |
            {
                patches > patternClause >
                openBrace ?? 'Expecting an open brace to follow the pattern here.'
            } |
            {
                picked > by ?? 'Expecting "by" to follow "picked" here.' > patternClause >
                openBrace ?? 'Expecting an open brace to follow the pattern here.'
            } |
            {
                of > [ _identifier | _keyword ]
                    ?? 'Expecting a surface name, or a call of a primitive, to follow "of" here.' >
                leftParen{?}
            } |
            { spacing > _expression } | { jitter > _expression } |
            { index > in ?? 'Expecting "in" to follow "index" here.' >
                [ _identifier | _keyword ]
                ?? 'Expecting a name for the copy number to follow "in" here.' } |
            cycle | { turned > _expression } |
            { sized > _expression > { to > _expression }{?} } |
            { over > _expression ?? 'Expecting the name of the ground to drop onto here.' } |
            tilted | { above > _expression } | { below > _expression } |
            { steepest > _expression } | { sink > _expression } |
            surfaceEntryClause
        ]
        // What may follow one of the things a field is filled with: how often it is picked, and then,
        // after a comma, the next of them.
        fieldWeightClause: { weight > _expression }
        // What may follow a field's patches: where their edge begins and ends.  And what may follow its
        // sizes: a pattern that sets them.
        fieldEdgeClause: { from > _expression > to ?? 'Expecting "to" to follow where the edge begins here.' > _expression }
        fieldSizedByClause:
        {
            by > patternClause > openBrace ?? 'Expecting an open brace to follow the pattern here.'
        }
        fieldChoiceClause:
        {
            comma > [ _identifier | _keyword ]
                ?? 'Expecting a surface name, or a call of a primitive, to follow the comma here.' >
            leftParen{?}
        }

        // Ribbon clauses.
        startRibbonClause:
        {
            ribbon > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "ribbon" here.'
        }
        ribbonPointClause:
        {
            width > _expression >
            at ?? 'Expecting "at" to follow a ribbon point width here.' >
            _expression
        }
        ribbonEntryClause:
        [
            ribbonPointClause => 'width' |
            { twist > _expression } |
            surfaceEntryClause
        ]

        // Strand clauses: the points a single fiber runs through, each with its width there.
        startStrandClause:
        {
            strand > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "strand" here.'
        }
        strandEntryClause:
        [
            ribbonPointClause => 'width' |
            surfaceEntryClause
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
            {
                profile > [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting an outline, or the name of one, to follow "profile" here.'
            } |
            {
                discontinuous{?} > spline >
                [ openBrace | _identifier | _keyword ] ?? 'Expecting a spline, or the name of one, after "spline" here.'
            } |
            { steps > _expression } |
            // No insisting on "center": `no shadow` and `no caustics` belong to every surface, and are read below.
            { no > center } |
            open | surfaceEntryClause
        ]

        // Grown tree clauses: a tree grown into the space it is given.
        startGrownClause:
        {
            grown > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "grown" here.'
        }
        grownEntryClause:
        [
            {
                [ crown | leaf ] > [ openBrace | { [ _identifier | _keyword ] > leftParen{?} } ]
                    ?? 'Expecting surfaces in braces, a surface name or a call of a primitive here.'
            } |
            { points > _expression } | { reach > _expression } | { kill > _expression } |
            { step > _expression } | { tropism > _expression } | { susceptibility > _expression } |
            { twig > _expression } | { joining > _expression } |
            surfaceEntryClause
        ]
        // What may follow a grown tree's leaf: another, after a comma, and then whether they hang.
        grownLeafChoiceClause:
        {
            comma > [ openBrace | { [ _identifier | _keyword ] > leftParen{?} } ]
                ?? 'Expecting surfaces in braces, a surface name or a call of a primitive to follow the comma here.'
        }
        grownHangingClause: { hanging }

        // Fracture clauses: a solid broken into pieces.
        startFractureClause:
        {
            fracture > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "fracture" here.'
        }
        fractureEntryClause:
        [
            {
                solid > [ openBrace | { [ _identifier | _keyword ] > leftParen{?} } ]
                    ?? 'Expecting surfaces in braces, a surface name or a call of a primitive here.'
            } |
            { pieces > _expression } | { gap > _expression } | { rounded > _expression } |
            flat | { stretched > _expression } | { toward > _expression } | { hole > _expression } |
            { apart > _expression } | { tumble > _expression } | loose | whole | materials |
            surfaceEntryClause
        ]

        // Joint clauses: a group turned about a pivot by an angle about an axis, within limits; or turned to aim at
        // something, or with the joints inside it to reach for it.
        startJointClause:
        {
            joint > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "joint" here.'
        }
        // A joint's own properties.  They stand among a group's entries, since a joint holds whatever a group may and
        // a group's entries keep their tags only when read as a group's; a plain group refuses them.
        jointPropertyClause:
        [
            { [ pivot | angle | facing ] > _expression } |
            { axis > [ X | Y | Z | _expression ] ?? 'Expecting "X", "Y", "Z" or a direction to follow "axis" here.' } |
            { limits > _expression > to ?? 'Expecting "to" and the most it may turn here.' > _expression } |
            { aim > at ?? 'Expecting "at" to follow "aim" here.' > _expression } |
            {
                reach > _expression >
                with ?? 'Expecting "with" and the name of what reaches here.' > _expression
            }
        ]
        // A named place on a thing, carried wherever it goes.
        markClause:
        {
            mark > _expression > at ?? 'Expecting "at" and where the mark stands here.' > _expression
        }

        // Swarm clauses: copies of one thing thrown from a place, falling and bouncing until their life is up.
        startSwarmClause:
        {
            swarm > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "swarm" here.'
        }
        swarmEntryClause:
        [
            {
                of > [ _identifier | _keyword ]
                    ?? 'Expecting a surface name, or a call of a primitive, to follow "of" here.' >
                leftParen{?}
            } |
            {
                within > [ _identifier | _keyword ]
                    ?? 'Expecting a surface name, or a call of a primitive, to follow "within" here.' >
                leftParen{?}
            } |
            { [ from | toward ] > _expression } |
            { burst > _expression > { at > _expression }{?} > { over > _expression }{?} } |
            { [ spread | rate | since | bounce | friction | drag | radius ] > _expression } |
            { [ speed | life | spin ] > _expression > { to > _expression }{?} } |
            { falls > at ?? 'Expecting "at" and how fast a copy falls here.' > _expression > { to > _expression }{?} } |
            surfaceEntryClause
        ]

        // Shape grammar clauses: rules that divide a box into a building.  The rules themselves are read by
        // hand, past the open brace after "rules".
        startShapesClause:
        {
            shapes > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "shapes" here.'
        }
        shapesEntryClause:
        [
            { axiom > [ _identifier | _keyword ] ?? 'Expecting the name of the first rule here.' } |
            { size > _expression } |
            { rules > openBrace ?? 'Expecting an open brace to follow "rules" here.' } |
            surfaceEntryClause
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
            {
                path > [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting an outline, or the name of one, to follow "path" here.'
            } |
            surfaceEntryClause
        ]

        // Text clauses.
        fontWeightClause:
        [
            thin | light | regular | medium | bold | black
        ]
        // One font or a list of them, separated by commas as CSS separates them: the first that can be
        // had is the one used, so a face not everyone can have may be named with one anyone can.
        fontClause:
        {
            font > _expression > { comma > _expression }{*} > fontWeightClause{?} > italic{?}
        }
        textLayoutEntryClause:
        [
            { text > alignment ?? 'Expecting "alignment" to follow "text" here.' >
                [ left | center | right | _expression ] } |
            { horizontal > position ?? 'Expecting "position" to follow "horizontal" here.' >
                [ left | center | right | _expression ] } |
            { vertical > position ?? 'Expecting "position" to follow "vertical" here.' >
                [ top | baseline | center | bottom | _expression ] } |
            { line > gap ?? 'Expecting "gap" to follow "line" here.' > _expression } |
            {
                along > path ?? 'Expecting "path" to follow "along" here.' >
                [ openBrace | [ _identifier | _keyword ] ]
                    ?? 'Expecting a path, or the name of one, to follow "along path" here.'
            }
        ] ?? 'Expecting a text layout property here.'
        kerningPairClause:
        {
            kern > _expression > comma ?? 'Expecting a comma here.' > _expression >
            comma ?? 'Expecting a comma here.' > _expression
        }
        kerningClause:
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
        startTaperedTextClause:
        {
            tapered > text > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "text" here.'
        }
        taperedTextEntryClause:
        [
            { taper > _expression } | textEntryClause
        ]
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
            { diameter > _expression } | { factor > _expression } |
            { tropism > _expression } | { susceptibility > _expression }
        ]

        // Height field clauses.
        startHeightFieldClause:
        {
            heightfield > [
                openBrace |
                { [ _identifier | _keyword ] > openBrace{?} }
            ] ?? 'Expecting an identifier or open brace to follow "heightfield" here.'
        }
        heightFieldEntryClause:
        [
            imageClause | fieldFunctionClause | { samples > _expression } |
            { clip > _expression } | open | smooth | surfaceEntryClause
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
           object > file > openBrace ?? 'Expecting an open brace here.'
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
            startParaboloidClause     => 'paraboloid' |
            startSdfClause            => 'sdf' |
            startJuliaClause          => 'julia' |
            startHyperboloidClause    => 'hyperboloid' |
            startSaddleClause         => 'saddle' |
            startQuadricClause        => 'quadric' |
            startIsosurfaceClause     => 'isosurface' |
            startParametricClause     => 'parametric' |
            startPatchClause => 'patch' |
            startBilinearPatchClause => 'bilinearPatch' |
            startExtrusionClause => 'extrusion' |
            startTaperedExtrusionClause => 'taperedExtrusion' |
            startLatheClause => 'lathe' |
            startBlobClause => 'blob' |
            startSwellsClause => 'swells' |
            startTubeClause => 'tube' |
            startRibbonClause => 'ribbon' |
            startStrandClause => 'strand' |
            startFieldClause => 'field' |
            startSweepClause => 'sweep' |
            startGrownClause => 'grown' |
            startFractureClause => 'fracture' |
            startShapesClause => 'shapes' |
            startSwarmClause => 'swarm' |
            startJointClause => 'joint' |
            startTextClause => 'text' |
            startTaperedTextClause => 'taperedText' |
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
            in ?? 'Expecting "in" and a range or a list to follow the name here.' >
            [ loopRangeClause | _expression ]
                ?? 'Expecting a range such as [0, 5], or a list, to follow "in" here.' >
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
            // A local name first: `left`, `body`, `reach` and the rest are names a scene may keep, and any clause
            // beginning with that word read first insists on its own next word and fails on the "=".  A local name
            // gives way quietly to anything not followed by "=".
            localClause => 'local' |
            startForClause => 'for' |
            startOverClause => 'over' |
            startIfClause => 'if' |
            startLightClause => 'light' |
            startPlaneClause => 'plane' |
            startSphereClause => 'sphere' |
            startCubeClause => 'cube' |
            startCylinderClause => 'cylinder' |
            startConicClause => 'conic' |
            startTorusClause => 'torus' |
            startEggClause => 'egg' |
            startSuperellipsoidClause => 'superellipsoid' |
            startParaboloidClause     => 'paraboloid' |
            startSdfClause            => 'sdf' |
            startJuliaClause          => 'julia' |
            startHyperboloidClause    => 'hyperboloid' |
            startSaddleClause         => 'saddle' |
            startQuadricClause        => 'quadric' |
            startIsosurfaceClause     => 'isosurface' |
            startParametricClause     => 'parametric' |
            startPatchClause => 'patch' |
            startBilinearPatchClause => 'bilinearPatch' |
            startExtrusionClause => 'extrusion' |
            startTaperedExtrusionClause => 'taperedExtrusion' |
            startLatheClause => 'lathe' |
            startBlobClause => 'blob' |
            startSwellsClause => 'swells' |
            startTubeClause => 'tube' |
            startRibbonClause => 'ribbon' |
            startStrandClause => 'strand' |
            startFieldClause => 'field' |
            startSweepClause => 'sweep' |
            startGrownClause => 'grown' |
            startFractureClause => 'fracture' |
            startShapesClause => 'shapes' |
            startSwarmClause => 'swarm' |
            startJointClause => 'joint' |
            markClause => 'mark' |
            startTextClause => 'text' |
            startTaperedTextClause => 'taperedText' |
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
            jointPropertyClause => 'jointProperty'
        ]

        // Scene clauses.
        startSceneClause:
        {
            scene > openBrace ?? 'Expecting an open brace to follow "scene" here.'
        }
        sceneEntryClause:
        [
            localClause => 'local' |
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
            startParaboloidClause     => 'paraboloid' |
            startSdfClause            => 'sdf' |
            startJuliaClause          => 'julia' |
            startHyperboloidClause    => 'hyperboloid' |
            startSaddleClause         => 'saddle' |
            startQuadricClause        => 'quadric' |
            startIsosurfaceClause     => 'isosurface' |
            startParametricClause     => 'parametric' |
            startPatchClause => 'patch' |
            startBilinearPatchClause => 'bilinearPatch' |
            startExtrusionClause => 'extrusion' |
            startTaperedExtrusionClause => 'taperedExtrusion' |
            startLatheClause => 'lathe' |
            startBlobClause => 'blob' |
            startSwellsClause => 'swells' |
            startTubeClause => 'tube' |
            startRibbonClause => 'ribbon' |
            startStrandClause => 'strand' |
            startFieldClause => 'field' |
            startSweepClause => 'sweep' |
            startGrownClause => 'grown' |
            startFractureClause => 'fracture' |
            startShapesClause => 'shapes' |
            startSwarmClause => 'swarm' |
            startJointClause => 'joint' |
            startTextClause => 'text' |
            startTaperedTextClause => 'taperedText' |
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
            environmentClause => 'environment'
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
            // an expression, which it is not; and the map read by the density, which is read past
            // its words by hand, for the same reason.
            { emission > pigment } |
            { emission > by > density ?? 'Expecting "density" to follow "emission by" here.' } |
            { emission > _expression } |
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
                pigment | startPathClause | startKeysClause |
                { discontinuous{?} > spline > openBrace ?? 'Expecting an open brace to follow "spline" here.' } |
                startMaterialClause | { transform > startThingClause } |
                { interior > startThingClause } | { medium > startThingClause } |
                startLightClause |
                startPlaneClause | startSphereClause | startCubeClause | startCylinderClause |
                startConicClause | startTorusClause | startExtrusionClause |
                startTaperedExtrusionClause | startLatheClause |
                startBlobClause | startSwellsClause | startTubeClause | startRibbonClause | startStrandClause | startFieldClause |
                startSweepClause | startGrownClause | startFractureClause | startShapesClause | startSwarmClause | startJointClause | startTextClause |
                startTaperedTextClause | startLsystemClause | startHeightFieldClause |
                startTriangleClause |
                startSmoothTriangleClause | startParallelogramClause | startDiscClause |
                startGenericShapeClause | startEggClause | startSuperellipsoidClause |
                startPatchClause | startBilinearPatchClause | startIsosurfaceClause |
                startParametricClause |
                startParaboloidClause | startHyperboloidClause | startSaddleClause |
                startQuadricClause | startSdfClause | startJuliaClause |
                startObjectFileClause | startObjectClause |
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
                isosurface | parametric | paraboloid | hyperboloid | saddle | quadric | sdf | julia |
                patch | lathe | blob | swells | tube | ribbon | strand | field | sweep | grown | fracture | shapes | swarm | joint | extrusion | text | lsystem |
                heightfield | parallelogram | disc | triangle |
                { smooth > triangle } | { tapered > extrusion } | { tapered > text } |
                { bilinear > patch } |
                { generic > shape } | { object > file } |
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
            startParaboloidClause     => 'HandleStartParaboloidClause' |
            startSdfClause            => 'HandleStartSdfClause' |
            startJuliaClause          => 'HandleStartJuliaClause' |
            startHyperboloidClause    => 'HandleStartHyperboloidClause' |
            startSaddleClause         => 'HandleStartSaddleClause' |
            startQuadricClause        => 'HandleStartQuadricClause' |
            startIsosurfaceClause     => 'HandleStartIsosurfaceClause' |
            startParametricClause     => 'HandleStartParametricClause' |
            startPatchClause          => 'HandleStartPatchClause' |
            startBilinearPatchClause => 'HandleStartBilinearPatchClause' |
            startExtrusionClause      => 'HandleStartExtrusionClause' |
            startTaperedExtrusionClause => 'HandleStartTaperedExtrusionClause' |
            startLatheClause          => 'HandleStartLatheClause' |
            startBlobClause           => 'HandleStartBlobClause' |
            startSwellsClause         => 'HandleStartSwellsClause' |
            startTubeClause           => 'HandleStartTubeClause' |
            startRibbonClause => 'HandleStartRibbonClause' |
            startStrandClause => 'HandleStartStrandClause' |
            startFieldClause => 'HandleStartFieldClause' |
            startSweepClause          => 'HandleStartSweepClause' |
            startGrownClause          => 'HandleStartGrownClause' |
            startFractureClause       => 'HandleStartFractureClause' |
            startShapesClause         => 'HandleStartShapesClause' |
            startSwarmClause          => 'HandleStartSwarmClause' |
            startJointClause          => 'HandleStartJointClause' |
            startTextClause           => 'HandleStartTextClause' |
            startTaperedTextClause => 'HandleStartTaperedTextClause' |
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
