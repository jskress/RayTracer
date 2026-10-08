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
`Geometry/LSystems/`). `Basics/` has the math primitives (`NumberTuple`, `Point`, `Vector`,
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
`ReflectionShareAt`), which is what keeps a lantern's streak on a wet road from glittering.

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
`Instructions/Pigments/`.

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
- `TestResults/` at the repo root accumulates a very large number of timestamped run
  directories from previous `dotnet test` invocations; they are not gitignored but are also
  not source — ignore them when exploring the repo.
