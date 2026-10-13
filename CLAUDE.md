# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A C# ray tracer originally built by working through Jamis Buck's book, *The Ray Tracer
Challenge*. The book's content is complete; ongoing work extends beyond it (see `README.md`
for the to-do list — motion blur, area lights, spotlights, focal blur, other surface types,
etc.). Scenes are described in a custom DSL (files with the `.igl` extension) rather than
written in C#; the C# code is the engine plus the DSL parser/interpreter.

## Build, run, test

```bash
dotnet build                              # build the whole solution (RayTracer + Tests)
dotnet run -- -i path/to/scene.igl        # render a .igl scene file (see Options/RenderOptions.cs for all flags)
dotnet test                               # run the full MSTest suite (Tests/ project)
dotnet test --filter "FullyQualifiedName~TestLathe"   # run one test class
dotnet test --filter "FullyQualifiedName~TestLathe.TestShapeIntersections"  # run one test method
```

Example scenes live under `gallery/` (`gallery/challenge-book`, `gallery/POVRay`,
`gallery/Local`), useful for manual end-to-end checks of parser/renderer changes, e.g.:
`dotnet run -- -i gallery/challenge-book/chapter-16/csg.igl`.

The `RayTracer.csproj` excludes `Tests/**` from its own compile/resource items; the `Tests`
project references `RayTracer.csproj` directly. Both projects target `net10.0` with
`LangVersion` 14, implicit usings enabled and nullable reference types disabled.
`RayTracer.csproj` sets `Optimize` true in every configuration, so the Debug build (which is what
renders run from) is as fast as Release -- about four times faster than an unoptimized one. Render
times recorded before 2026-10-06 were taken unoptimized. To step through code in a debugger,
rebuild in full both ways -- a plain build sees no file changed and keeps whichever the last compile
made:

```bash
dotnet build --no-incremental -p:Optimize=false   # before debugging
dotnet build --no-incremental                     # afterwards, back to optimized
```

## Architecture

The system has two halves: a **DSL front end** that parses `.igl` scene files into an
in-memory instruction list, and a **rendering engine** that executes those instructions to
produce an image. Data flows roughly:

```
.igl file --[LanguageParser]--> Instructions --[ImageRenderer]--> Scene/Camera --[Scanner]--> Canvas --[ImageIO]--> output file
```

### Parser (`Parser/`)

`LanguageParser` is a `partial class` split across many files (`LanguageParser.<Topic>.cs`,
one per DSL construct: Cameras, Lights, Surfaces, CSG, Groups, Transforms, Pigments, Text,
LSystems, HeightFields, etc.). The actual grammar is a single large embedded DSL spec string
in `LanguageParser.DSL.cs` (`LanguageDslSpecification`), written against the third-party
`Lex` package (`Lex.Dsl`, `Lex.Clauses`, `Lex.Tokens`, `Lex.Parser`). It defines keywords,
expression syntax, and named "clauses" (grammar rules) which are wired to C# handler methods
via string-tag dispatch tables in `LanguageParser.Dispatch.cs` (`_blockStartHandlers` /
`_clauseHandlers`, keyed by the `=>` tag names used in the grammar spec). When adding a new
DSL construct: extend the grammar spec, add a `Handle...Clause` method in the relevant
`LanguageParser.<Topic>.cs` partial file, and register it in `FillDispatchTables()`.

`include '<file>'` is handled by pushing/popping a stack of `(fileName, LexicalParser)`
entries, so multiple `.igl` files can be composed.

### Instructions and resolvers (`Instructions/`)

Parsing a clause produces `Instruction` objects (and `IObjectResolver` "resolvers" that
incrementally build up a target object — surface, pigment, material, transform, etc. — as
nested clauses are parsed). Resolvers are grouped by kind under `Instructions/Surfaces/`,
`Instructions/Patterns/`, `Instructions/Pigments/`, `Instructions/Transforms/`,
`Instructions/Core/`, `Instructions/Context/`. `ParsingContext` (`Parser/ParsingContext.cs`)
tracks the current resolver target stack and variable scope while parsing.

`RenderInstruction` (`Instructions/RenderInstruction.cs`) is the terminal instruction: at
execute time it resolves the `Scene` and `Camera` to use (from an explicit `scene { }` block,
or an implicit scene built from top-level objects), finalizes surfaces (materials, pigment
seeding), and calls `camera.Render(...)`.

### Geometry and rendering (`Geometry/`, `Core/`, `Basics/`)

`Geometry/` holds all `Surface` subclasses (`Sphere`, `Plane`, `Cube`, `Cylinder`, `Conic`,
`Torus`, `Triangle`/`SmoothTriangle`, `Parallelogram`, `Group`, `CsgSurface`, `HeightField`,
`Extrusion`/`ExtrudedSurface`, `Lathe`, `TextSolid`, and L-system-driven shapes under
`Geometry/LSystems/`).  A `Field` fills a 2D outline in the X/Z plane on a jittered grid with copies of one or
several things (`of A, B weight 2`), each built once and stood by `Instance` where picked, `turned` and `sized`;
what stands at a place, its turn and its size are hashed from the place's grid cell (`Field.DrawFor`), never drawn
in order, and the jitter keeps its old in-order stream so fields asking for none of it are unchanged.  The
undergrowth and rocks libraries' areas (`Grass`, `Reeds`, `Wheat`, `Scree`) are eight such variants, not a build
per copy (`index in n`).  `over 'name'` drops every copy straight down onto a named SIBLING (`Field.DropOnto`, called
by `PlacementSettler` after placements, at the one moment children are prepared and not yet arranged), upright or
`tilted` (slope from a few extra drops either side, in the shared space), skipping misses, `steepest`, `above`,
`below`; a dropped field is left EMPTY when prepared and fills itself at the drop (`Field.Fill` with a `Dropping`),
building only what lands and then running the group's own preparation over it.  A primitive call hands an empty
group back bare, never as an instance: there is nothing in it to share (before instances could nest, a summer fir
bough's empty snow stopped the bough being shared and every fir rebuilt its 2000 needle shoots, 140 MB a tree).
Instances NEST: a hit carries `Intersection.Portals` (`Geometry/Portals.cs`: the innermost instance plus an immutable
`PortalChain` of those beyond, appended as outer instances stamp it -- the only allocation, and only for nested
hits), every walk continues at each shared shape's root with the next portal, CSG membership asks the OUTERMOST
stamped so far, `no shadow` holds if any instance in the chain says it, hair light is keyed by the whole chain, and
`Material.PortalTo` hands an anchor only the portals beyond it.  So a tree holding shared leaves is shared itself.  `along <path>` lays copies along each run of a
path instead of filling an outline (`Field.PathPlaces`, walking a `PathGuide` by distance): each turned so its +X runs
with the path (-Z faces the path's right), the spacing fitted to whole gaps (both ends of an open run, no doubled seam
on a closed one), `rows n apart d` each spaced along its OWN length, `offset` to the path's left, jitter drawn from
the place.  Patterns read on the field's plane at (x, 0, z): `patches` keeps a place by the RAW value as a chance
(`from a to b` an edge; reversed, the exact complement on the same grid), while `picked by` and `sized … by` go by
the place's RANK among the kept places (`Field.RanksOf`), so weights are true shares whatever the pattern's
distribution, blurred by `Mingling`.  Fibers are `Strand` and `Hair`, both `StrandSurface`s over a compact
`StrandSet` (floats, Bézier segments, its own BVH) hit as camera-facing ribbons and shaded as rods.
A surface's `hair { }` becomes a `HairGrowth`; the surface grows a `Hair` at the end of
`PrepareForRendering` (roots by random lines through a sphere around it, Cauchy-Crofton, kept by `patches`
-- a pattern's value at the root -- and `facing` -- which way the surface faces there) and the
hair is found through the surface -- `Surface.Intersect` asks it, `BoxAround` boxes it in unless
asked for the true extent, and CSG keeps or drops strand hits without counting them as entered.
Strands are never Phong: `Scene.GetHitColor` hands a `HairIntersection` to `Scene.ShadeFiber` (`Core/Scene.Fibers.cs`),
the Chiang 2016 / PBRT fiber model in `Core/Fiber.cs` (R, TT, TRT and the rest; index 1.55, tilt 2 degrees, the
material's `roughness` or 0.3), lit from every light at the strand's middle with shadow rays setting off a radius
toward the light, and the sky sampled over the whole sphere (`Light.SampleAround`, weight 4).  Color is matched by
Chiang's inversion from the pigment -- read at each strand's ROOT in the skin's space when the hair wears its skin's
material -- or set by a material's `melanin`.  A strand's shadow rays treat other strands as OPAQUE; light bounced
among strands comes from a `HairLight` gathered per (hair, instance) before the first pixel
(`Scene.GatherTheLightAmongHair`; behind an instance, when first met) (`Core/Scene.HairLight.cs`: photons from each
light sample as a beam over the hair, scattered by `Fiber.Sample` and bounced off the skin or anything else inside
the grid by its diffuse color, recorded after their first scattering as fluence AND net flow in a grid of cells a
sixth of a strand's length IN THE WORLD, at most 2^18 cells, eight paths for each cell the strands occupy; a path's
power is tested against exact nought, never `IsBlack`, since it shrinks with the number of paths; each lobe reads
the light arriving from its center direction, `HairLight.Here.From`).  Isotropic light was 1.5-2x too bright: the
flow is what makes it match a brute-force path tracer (`TestHairLight`) within ~15%.  A non-hair surface's shadow
ray keeps each crossed strand's forward share (`Fiber.SharesOf`).  Spectral renders take the absorption by
`FromUnbounded` and work the hair's own color out from it band by band.  A strand finer than a ray's cone is
WIDENED to it and carries `HairIntersection.Coverage` (true width / widened); `Scene.GetColorFor` composites partly
covering strands front to back (stopping at a thousandth left), each taking lights' samples in proportion to its
`Share` of the pixel (by chance below one sample, for lights of eight samples or more).  A shadow ray from a point
the eye saw carries that point's footprint (`Intersection.FootprintRadius`, only in scenes with strands) as a cone
narrowing to nothing at the light (`Ray.NarrowsAt`), and a `StrandTally` that stops its traversal once next to
nothing gets past the strands.  For this `Matrix.Transform` keeps a ray's cone in a surface's space (scaled), and a
`BoundingBox` holding strands is marked `Widens` and grown by the ray's width at its widest side -- other boxes and
surfaces are untouched, so other scenes stay bit-identical. `Basics/` has the math primitives (`NumberTuple`, `Point`, `Vector`,
`Matrix`, `Ray`, `Interval`, `Transforms`, `NoiseGenerator`, `Polynomials`). `Core/` has scene-
graph level types: `Scene`, `Camera`, `Material`, `PointLight`, `Intersection` and its
precomputed variants. `Graphics/` holds the 2D path/curve primitives (`GeneralPath`,
`Line`, `QuadCurve`, `CubicCurve`, `TwoDPoint`/`TwoDVector`) used by extrusions, lathes and
text layout to describe outlines in 2D before they're revolved/extruded into 3D surfaces.

Shading carries light in an `ISpectrum<TSelf>` struct (`Graphics/ISpectrum.cs`), not in `Color`:
every shading method (`Scene`, `Light.ApplyPhong`, `Medium`, `Interior`/`Material` tints,
`PixelRenderer.Trace`) is generic in it, `RgbSpectrum` mirrors `Color`'s arithmetic bit for bit, and
the `Color`-returning methods tests call are one-line wrappers over `<RgbSpectrum>`. Pigments, light
colors and medium coefficients stay `Color` (they are what an author wrote) and are converted where
the shading first meets them -- as a reflectance, an illuminant or an unbounded rate. `context {
spectral }` / `--spectral` carries light in `BandSpectrum` instead: 32 bands over 380-780 nm (the
sky's own, `SpectralColor.Bands`), colors turned into smooth spectra by `RgbToSpectrumTable`
(Jakob-Hanika, ported from PBRT and fitted against our own bands at render start) and back by
`SpectralBasis`. Under white light a spectral render matches RGB; the physical sky and its sun reach
it as the spectra they were worked out as. An interior that disperses (`dispersion`, an Abbe number,
or a named `glass` from `Core/Glasses.cs`) splits a spectral ray crossing it into one `SingleBand` ray
per band (`Scene.SplitThrough`), which never split again; RGB renders ignore dispersion. A light
given a `temperature` (`Light.SetColorTemperature`) carries a Planck spectrum as well as its color,
scaled so its brightest channel is one; `brightness` multiplies either, and `kelvin(T)` is the same glow
as a color for emissions and pigments. `context { white balance T }` (`Graphics/WhiteBalance.cs`, a
Bradford adaptation applied to each finished pixel in `Camera.Render`) makes a glow at T come out white;
lamp-lit scenes use it so real bulb temperatures read warm rather than orange. A material may say `fresnel`
(its `reflective` is then the square-on share, rising toward a graze) or `roughness` (0-1, GGX facets in
`Core/Microfacets.cs`): a rough surface takes its highlight and its blurred reflection from the same facets,
with Fresnel implied, looking in `reflection samples` directions at the first rough hit of a path
(`Scene.GetRoughReflection`) and one thereafter. Lights a ray can meet (`Light.CanBeSeen`) are not counted
twice: the sky reaches a rough surface through its reflection only, and a glowing volume is shared between
its own samples and the reflection by Veach's balance heuristic (`Scene.HighlightShareOf` /
`ReflectionShareAt`), which is what keeps a lantern's streak on a wet road from glittering. Rough glass
(`roughness` on something transparent) frosts: `Scene.GetRoughRefraction` bends a fan of rays through visible
facets, each facet mirroring and passing by the glass's own index, and a lamp behind it glows through it
(`Microfacets.TransmittedHighlight`) -- given only at the LAST of the glass before the lamp
(`GetLightReaching`'s `glass` check), so a pane's near face never counts it, and glowing volumes are shared
there along the straight run out, exactly as for reflection.  A smooth mirror or clear glass met by a ray a rough
surface has already spread is shaded as rough as that surface (`Scene.RegularizedFor`, path regularization:
`Material.RoughAs`, kept on `Intersection.Material`, while identity checks keep `Surface.Material`), so a lantern
seen in a car's paint in a wet road is shared rather than found by chance; the camera's own view of a mirror,
and a shell at the surroundings' index, are untouched.  A container's interior (`clarity` fade, `medium`) is
charged where a ray leaves it (end of `GetHitColor`) and, for anything met inside it, over the run to that
(`Scene.ThroughTheInsideOf` from `Intersection.ContainerBefore`); a surface inside a medium is lit through it
up to the container's edge (`SurvivingTheMedium` in `ReachingFrom`) -- so a log inside a flame keeps the glow
in front of it.

Rendering itself is dispatched through `Scanners/` (`SingleThreadScanner`,
`LineParallelScanner`, `PixelParallelScanner` — implementations of `IScanner`, selected via
the `context { scanner ... }` DSL block or command line) and `Pixels/` (anti-aliasing
strategies: `NoAntiAliasingPixelRenderer`, `AdaptiveSuperSamplingPixelRenderer`, controlled by
`AliasingOption`). Output goes through `ImageIO/` (`ImageFile`, `ImageCache`,
`ImageReference`) which wraps SkiaSharp/Magick.NET for reading/writing image formats.

### Pigments and patterns (`Pigments/`, `Patterns/`)

Pigments (`Pigments/`) define how color is derived at a surface point (solid, pattern-based,
image-mapped, blended/layered, noisy). Patterns (`Patterns/`) define the underlying spatial
functions (stripes, checker, gradient, agate, wood, granite, brick, hexagon, etc.) that
pigments sample. Each has a corresponding DSL resolver under `Instructions/Patterns/` and
`Instructions/Pigments/`.  One pattern depends on direction rather than place: `flop` (`FlopPattern`), the angle
between the eye and the light's mirror direction (`Basics/Viewing.cs`, `Viewing.Aspecular`), for color-shifting
paint.  A `Viewing` (eye, normal, light) is handed down only to a pigment whose `SeesTheViewer` (cached per pigment in
`Material`), so every other pigment is asked exactly as before; it is worked out per light in `Light.ApplyPhong`, and
with no particular light (transparency, refraction, metallic tint, shadow tint) the light is taken to be at the eye.

### Expression language (`Terms/`)

DSL expressions (numbers, colors, vectors, tuples, variables, arithmetic, unary color/point/
vector casts) compile down to a `Term` tree (`Term`, `LiteralTerm`, `VariableTerm`,
`BinaryOperation`/`UnaryOperation` subclasses, `TupleTerm`). `ExpressionTreeBuilder`
(`Parser/ExpressionTreeBuilder.cs`) is plugged into the `Lex` expression parser as the
`TreeBuilder` to build these from parsed tokens. Terms are evaluated against `Variables`
(`General/Variables.cs`) at instruction-execution time, so DSL variables are late-bound.

### Fonts and text (`Fonts/`)

TrueType font loading/layout (via `Typography.OpenFont.NetCore`) for the `text { }` surface
type — glyph outlines are converted into `GeneralPath`s and extruded like any other 2D path.

### Entry point and CLI (`Program.cs`, `Commands/`, `Options/`)

`Program.cs` uses `CommandLineParser` to dispatch to verbs: `render` (default,
`RenderOptions`/`RenderCommand`) and `fonts` (`FontsOptions`/`FontsCommand`, for managing the
font catalog). `RenderCommand.Render` builds a `LanguageParser`, parses the input file into an
`ImageRenderer`, and calls `Render(options)`. Output level (`Terminal.OutputLevel`) is set
from CLI options and consumed globally via the static `Terminal` class (`Terminal.cs`) for
progress/error output.

## Notes

- Tests use MSTest (`[TestClass]`/`[TestMethod]`), one `Test<Thing>.cs` file per subject area,
  living flat in `Tests/` (no subfolders). `Tests/Usings.cs` has the single global using for
  `Microsoft.VisualStudio.TestTools.UnitTesting`.
- `editors/igl/` is a TextMate bundle that gives `.igl` files syntax highlighting in Rider and VS Code.
  Its grammar is generated by `python3 editors/igl/generate-grammar.py` from the engine's own lists
  (the DSL keywords, named colors, indices of refraction, directions, built-in functions and
  renderer globals), and `TestEditorGrammar` fails when it falls behind -- so a change that adds
  any of those needs the generator re-run.
- `Camera.GetTransform` makes its sideways axis of unit length, which the book's view transform does not:
  left as it came, a camera tilted θ saw wider than its field of view by 1/cos θ (41% at 45°, nothing at all
  looking straight down).  Fixed 2026-10-12, which re-framed nearly every gallery image slightly; the book's
  "arbitrary view transformation" test carries the corrected numbers.  A camera looking along its `up` is refused.
- `TestResults/` at the repo root accumulates a very large number of timestamped run
  directories from previous `dotnet test` invocations; they are not gitignored but are also
  not source — ignore them when exploring the repo.
