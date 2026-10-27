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
by a `ClauseDispatcher` built in `LanguageParser.Dispatch.cs` (`CreateDispatcher()`, one
`.On(nameof(HandleXxxClause), ...)` per handler, keyed by the `=> 'HandleXxxClause'` tags in the
grammar spec). When adding a new DSL construct: extend the grammar spec, add a `Handle...Clause`
method in the relevant `LanguageParser.<Topic>.cs` partial file, and register it in
`CreateDispatcher()`.

`include '<file>'` is handled by pushing/popping a stack of `(fileName, LexicalParser)`
entries, so multiple `.igl` files can be composed; a file already being read is refused (an
include cycle).  `import 'library' { Names }` reads a library (definitions and imports only, see
`InstructionContext.HoldsOnlyDefinitions`) from `~/.rayTracer/Libraries`.

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
seeding), and calls `camera.Render(...)`.  A file with several `render` commands carries itself out afresh, up
to each later render, into a fresh context (`InstructionContext.Execute`, `RenderContext.Afresh`): settling the
first render's objects again scaled named materials' ambient twice and hung a physical sky's sun again.

### Geometry and rendering (`Geometry/`, `Core/`, `Basics/`)

`Geometry/` holds all `Surface` subclasses (`Sphere`, `Plane`, `Cube`, `Cylinder`, `Conic`,
`Torus`, `Triangle`/`SmoothTriangle`, `Parallelogram`, `Group`, `CsgSurface`, `HeightField`,
`Extrusion`/`ExtrudedSurface`, `Lathe`, `TextSolid`, and L-system-driven shapes under
`Geometry/LSystems/`).  A `GrownTree` (`grown { crown ... }`, a `Group`) grows itself when prepared, by space
colonization (Runions 2007): `points` scattered through the crown by rejection (inside = inside any part of a group
or instance, else odd crossings on most of three looks), a trunk straight up +Y from the origin until one is within
`reach` (refused once it climbs past them all), then rounds in which each point pulls its nearest node (a grid of
reach-sized cells) and each pulled node grows one `step` toward the sum, points dying within `kill`.  A new node is
skipped where one already lies within a quarter step: without that, a node pulled equally from two sides regrew the
same twig every round and the pipe model (r^n = sum of children's, from `twig`, `joining` 2.5) swelled the limbs
into sausages.  One-child nodes are smoothed twice, wood is straight `TubeSegment`s,
and leaves (+Y along the twig -- the trees library's convention, not an L-system's +Z -- face +Z turned to the sky
within 45 degrees, so snow on a shoot's +Z lies on top) are instances at each tip and up to two nodes back of
`leaf A, B, ...`, each made once and picked at random per place (one shared cluster turned a whole tree one flat
orange in autumn); a leaf that is an empty plain group keeps its share by standing nowhere, and `hanging` stands
leaves upright in the tree's space instead (willow strands, fruit).  Every tree in the trees library but `Palm` (it
never branches) is grown so, and the undergrowth's `Boxwood`, in crowns of lobes placed by the variant (one ball
grows a lollipop) whose LOWEST part is a middle lobe (a side lobe reaching the trunk first dog-legged it), each
species' height factor measured over five variants.  A `Fracture` (`fracture { solid ... }`, a `Group`) breaks a
solid into Voronoi cells when prepared: seeds by rejection (or, `toward` a point, rings and nine spokes held from ring to
ring in the plane of a thin solid's two longest sides, else density falling off), each cell clipped from a start
`Polytope` (`Geometry/Polytope.cs`: faces as plane + CCW polygon, Sutherland-Hodgman clipping, `Shrunk`, `Centroid`) by
the bisector to each neighbor nearest first (in `stretched` space; `flat` drops Y), stopping once a neighbor lies past
twice the cell's farthest corner, each plane drawn in by `gap`/2.  A box -- `Cube`, CSG intersection of them, or a
group of ONE (a solid written in braces; missed, a pane went through the CSG path and speckled) -- is the start
itself (`ConvexOf`); anything else starts from its box grown 1% (coincident faces in the CSG speckled glass) and each
piece is `cell ∩ Instance(solid)`.  `whole` keeps cells whose seeds are inside, uncut, with ghost seeds round it.
Pieces are `Polyhedron` (slab test over planes, `PrecomputedNormalIntersection`) or, `rounded`, `RoundedPolyhedron`:
the cell shrunk by r grown back by a ball = face slabs, edge cylinders, corner balls; convex, so entry = nearest of
their entries, exit = farthest exit, exact.  `loose` builds each piece in a randomly shifted space (pattern grain of
its own); `apart`, `tumble`, `hole`, `materials A, B` per piece -- with more than one, a solid that is not a box is
resolved once per material, nothing in it shared (`Fracture.Solids`, `RenderContext.Unshared`), since a material
handed into one solid every piece stands as an instance went to whichever piece handed it first.  Uses: rocks `DryStoneWall`/`Rubble`, roads
`CrazyPaving`, windows `Sash`/`Casement`'s trailing `broken` count (unbroken is the old surface list, pixel-identical).
A `ShapeGrammar` (`shapes { axiom A  size v  rules { A -> ... } }`, a `Group`; CGA-style split grammar) is carried
out when RESOLVED (`ShapeGrammarResolver.SetProperties`; a resolver with no rules -- the block on a primitive call --
builds nothing): `Instructions/Surfaces/Shapes/` holds `ShapeScope` (oriented box: corner, unit X/Y/Z, sizes; Z INTO
what it stands for, so a facade scope's -Z is outward as windows are built), the actions (`extrude`, `split` fixed /
`~` shares, `repeat` round(L/s) stretched, `faces [depth d]` front/back/left/right/top/bottom/`sides` each standing on
its face Z in, `move`/`size` in the scope's axes, `choose` -- drawn by a splitmix hash of (seed, the choice's parse
id, the scope's corner and sizes), never `GetHashCode` -- `if`, `fill`, `place`, `roof gable|hip|flat [across]
[overhang] [verge] [gables M]`, `nothing`, a rule name) and `ShapeDerivation` (rules see `width`/`height`/`depth`,
parts `index`/`count` and `index in name`; 200 deep is refused as a loop).  A `fill` is a `Polyhedron` of the scope
in the GRAMMAR's space (no transform), so patterns run on unbroken across tiles -- and a face of a scope lies wherever
the rules put it, so the buildings library's grammar brick is 100 m deep with its joints 50 m off, and turned a
quarter (`BuildBrickFlank`) on walls facing X: no face can land in a joint plane.  `place` stands surfaces on the
middle of the scope's near face in its axes, unscaled.  A roof is convex polytopes in (u along ridge, y, v) carried
by a handedness-keeping frame.  Uses: buildings `Terrace`/`Apartments`/`Chapel` (meters), winter as
`weather { snow }` (`Weather.Snow`: facing-up blend, whole above the edge band, unlike `dust`'s noise-broken
cover).  A `Field` fills a 2D outline in the X/Z plane on a jittered grid with copies of one or
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
`Material.PortalTo` hands an anchor only the portals beyond it.  So a tree holding shared leaves is shared itself.
A shared shape is DRESSED PER PLACEMENT: a group's or CSG's hand-down stops at an instance (`SurfaceIterator(...,
intoShared: false)`), settling marks a part left the default (`Surface.WearsTheDefault`, with any decals-only material
it had in `OwnBeforeTheDefault`), and at a hit `Surface.MaterialThrough(portals)` -- `Intersection.SurfaceMaterial`,
cached and cleared when an outer instance stamps the hit -- gives it the material of the innermost instance in the chain
that was dressed (`DressedThrough`: an instance adding only decals -- `material inherited` -- lays them over whatever
dresses it from further out, or over its settled default; the part's own decals laid over by `Material.HandedDownTo`,
kept per material; `Surface.Wearing` lets a blob put its components' mixture on it).  Interiors (`FindContainers` returns both sides' portals, `InteriorOf(surface, portals)`),
caustic targets (walked into instances with their portals), wires and fibers ask through it; hair wearing its skin
takes the skin's mark (`Hair.WearsWhatItsSkinWears`); and an instance's material is readied for the first undressed
part inside it (`FirstUndressedIn`, found once per shared shape: an image picks its mapping from the shape).
A walk through the scene before rendering goes into each shared shape ONCE (`walked`/`shared` sets: kinematics'
`Find`, physics' `RefuseBuriedBodies`, shadow and weather settling) or prunes by a per-shape memo (`LightPlacer`'s
`HoldsALight`, caustics' `MayTurnLight` -- a part left to be dressed may turn light): walked per placement, a-great-
wood's nested twigs and leaves (2 billion placements) took 3.5 minutes to look through for joints that were not there.  A blob dresses its own copy of a
material handed to it (`Blob.Wearing`): wrapped in place, the group's one material put the first blob's mixture on
every sibling, the second blob included.
Handed into the one shape at settling, the first holder's material went to every place.  A call whose own block sets a
material is shared too (`PrimitiveCallResolver`), unless the block `GivesLight` (decided before building) or its
material holds a medium (found once the block is laid on; the call is then built apart and the block laid on again).  `MayBeShared` asks a self-filling group (Field, GrownTree, LSystem, Fracture) what it
WILL hold (`MayShareWhatItHolds`: its candidates or leaves made once and kept for the fill), being empty when asked.
An area light's samples stand at the middles of its cells, jittered within them, so every one is on its face
(POV-Ray's grid ran edge to edge and its jitter spilled past it); a spotlight's `radius` and `falloff` are angles
read as `angles are` says (degrees unless told), the falloff 30 degrees unless said.
A `group` may hold point, spot and area lights in its own space (`Group.Lights`; a light travels through surface lists
as a `ListedLight`, so loops and choices may set lights out, at a scene's top level too).  `LightPlacer` sets each out
in the world after the scene is arranged -- once for every placement, walking instance chains, and once for every
sample time if the group moves (`Light.At(timeIndex)`, asked by every shading loop) -- and records its `Housing`:
the closed surfaces of its own group round it (odd crossings on all of seven looks), which its shadow rays skip,
keyed with their portals so one lamp's bulb is not the next one's.  Placed lights are ordinary lights to the shading.  `along <path>` lays copies along each run of a
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
toward the light, and the sky sampled over the whole sphere (`Light.SampleAround`, weight 4).  With `diffuse
bounces` a strand also gathers bounced light (`Scene.BouncedOntoAFiber`): directions even over the sphere in its frame,
weighed by `Fiber.Scattered` x 4pi / count, as many as `RaysOf` gives its share (salts 135-137).  Along such a gather a
place within the gathering hair's `HairLight` box (`_hairDomain`, `WithinTheHair`: a non-wire strand or an opaque
surface, what the hair light's photons scatter off) gives only what came from beyond (`ShadedAt(fromBeyond)`: no lamps,
no hair light, no walks); a place outside clears the domain for everything after it.  Without the rule the light among
strands was counted twice (2.3x on a fur ball); without the gather, fur under bounces had no fill at all.  Color is matched by
Chiang's inversion from the pigment -- read at each strand's ROOT in the skin's space when the hair wears its skin's
material -- or set by a material's `melanin`.  A strand's shadow rays treat other strands as OPAQUE; light bounced
among strands comes from a `HairLight` gathered per (hair, instance) before the first pixel
(`Scene.GatherTheLightAmongHair`; behind an instance, when first met) (`Core/Scene.HairLight.cs`: photons from each
light sample as a beam over the hair, scattered by `Fiber.Sample` and bounced off the skin or anything else inside
the grid by its diffuse color, recorded after their first scattering as fluence AND net flow in a grid of cells a
sixth of a strand's length IN THE WORLD, at most 2^18 cells, eight paths for each cell the strands occupy, each light
sample's beam carrying its share of the light, irradiance over the sample count (until 2026-10-23 each carried the
whole light, every fur scene lit as many times over as its lamps had samples); a path's
power is tested against exact nought, never `IsBlack`, since it shrinks with the number of paths; each lobe reads
the light arriving from its center direction, `HairLight.Here.From`).  Isotropic light was 1.5-2x too bright: the
flow is what makes it match a brute-force path tracer (`TestHairLight`) within ~15%.  A non-hair surface's shadow
ray keeps each crossed strand's forward share (`Fiber.SharesOf`).  A strand whose OWN material is `metallic` is a WIRE
(`HairIntersection.IsAWire`; not hair wearing its skin): never a fiber -- opaque to shadow rays and photons, not a
photon target, kept out of the bounce cache -- and shaded as a surface: at its true round normal when it covers the
ray (brushed with no direction = grooves ROUND it, a wound string, `Facets.At`'s strand), and when finer than the ray
(`IsAFineWire`, coverage < 1) by `Scene.ShadeWire` (`Core/Scene.Wires.cs`), the EXACT average over its round:
diffuse in closed form (cos(axial)(sin psi + (pi - psi) cos psi)/4), each shadow ray from the side of the round facing
its lamp, reflections at places spread evenly across its width, and each lamp's GGX highlight as pi/(8 cos eps) F
times the integral round the wire of D G -- D stretched (lengthwise / round alphas), the lobe flattened by
tan t = sqrt(P/R) tan v and summed by 16-point Gauss-Legendre, cut where the eye, the halfway and the lamp stop
seeing the round (Phong's averaged by quadrature).  Measured against 4000 rays across a resolved wire, within 0.2%
lit from front, side, behind or along it, seen square on (1% in a highlight's far tail seen at a slant).  Its mirror and
bounce rays go by `Scene.RaysOf`: a wire whose Share x count is under one sends ONE ray with that chance, weighted 1/chance
(salts 133/134) -- unweighted by share, a ray meeting a piano's 234 strings sent 16 mirror rays from each, which met them
all again (the documented example took 52 min at 40x30; 1 s after).  Shading the place each sampled ray met made polished strings glitter, and
a stretched-GGX stand-in (alpha 1 round the wire) was 4/pi too bright and lost backlit glints.  Spectral renders take the absorption by
`FromUnbounded` and work the hair's own color out from it band by band.  A strand finer than a ray's cone is
WIDENED to it and carries `HairIntersection.Coverage` (true width / widened); `Scene.GetColorFor` composites partly
covering strands front to back (stopping at a thousandth left), each taking lights' samples in proportion to its
`Share` of the pixel (by chance below one sample, for lights of eight samples or more).  A shadow ray from a point
the eye saw carries that point's footprint (`Intersection.FootprintRadius`, only in scenes with strands) as a cone
narrowing to nothing at the light (`Ray.NarrowsAt`), and a `StrandTally` that stops its traversal once next to
nothing gets past the strands.  For this `Matrix.Transform` keeps a ray's cone in a surface's space (scaled), and a
`BoundingBox` holding strands is marked `Widens` and grown by the ray's width at its widest side -- other boxes and
surfaces are untouched, so other scenes stay bit-identical. `Basics/` has the math primitives (`NumberTuple`, `Point`, `Vector`,
`Matrix`, `Ray`, `Interval`, `Transforms`, `NoiseGenerator`, `RootFinding`). `Core/` has scene-
graph level types: `Scene`, `Camera`, `Material`, `PointLight`, `Intersection` and its
precomputed variants. `Graphics/` holds the 2D path/curve primitives (`GeneralPath`,
`Line`, `QuadCurve`, `CubicCurve`, `TwoDPoint`/`TwoDVector`) used by extrusions, lathes and
text layout to describe outlines in 2D before they're revolved/extruded into 3D surfaces.  An extrusion's wall
segment skips a ray whose 2D line has every one of the segment's control points strictly to one side
(`ExtrusionPathSurface.AllToOneSide`, the hull property: same crossings, 45% less time on a 13-segment body filling the picture).
A lathe's segment is solved only for a ray that, while within the segment's heights, comes within its radii of the
axis (`LathePathSurface.MayCross`: its control points' bands, padded far past rounding and the hair a crossing may
fall beyond an end; a segment drawn across the axis reaches it): pixel-identical, an Eagle command module close-up
with four windows cut from 21-segment lathes 26.8 s -> 4.6 s.
A CSG of many things written as one (`union { a b c ... }`) is a chain of links (`CsgSurface.IsChainLink`), each
holding all before it on its left; the top link lists the whole chain ONCE (`ListTheChain`: which thing written each
surface stands in, shared by every link, a link's left being the things before its place), where per-link side
lists came to the square of what was written (97 swept grooves, each hundreds of facets: 12 s before the first
ray, 2.5 s after; pixel-identical).
A CONVEX `Superellipsoid` (both exponents <= 2, so its function is a norm less one) is searched along a ray by golden
section for a place inside, each crossing bracketed from it, stopping at once when the function rises where the ray
sets off (every ray leaving its surface) or when chords carried past their ends bound it above nought
(`AddConvexIntersections`): a die filling the picture 7.9 s -> 4.0 s, a-bed-made-up 48 s -> 28 s, pictures alike to
a few pixels; a pinched one keeps the nine-plane, 32-step search.  A distance-estimated surface (`sdf`, `julia`,
`DistanceEstimatedSurface.March`) counts an arrival within its accuracy as a crossing only when a look two
accuracies past finds the other side: taken as crossing, a graze turned inside and outside about, the ray ran out
"inside" and was closed at its box, and shadow rays met the phantom (a Julia set came out peppered).  An isosurface's and
`swells`' interval march (`March`) narrows a span whose ends differ in sign only once it is under 64 accuracies
(`StraddleSpan`), else subdivides it as any other: narrowed at once, a span holding three crossings recorded one, and
a ray skimming a swell's crest went through it (complete over cheap, the user's call 2026-10-24: 4.5-9x on those scenes).
Every surface sets a ray off from itself by `Surface.SelfOffsetAt(time, portals)` (`Intersection.SelfOffset`, the
nudge for over/under/lit points, weathering's looks and subsurface steps): the usual epsilon, or for a surface found to
an accuracy in its OWN units (isosurface, sdf) that accuracy times `StretchInTheWorld` -- scaled up a thousandfold, a
shape found to a ten-thousandth in its own units is a tenth off in the world, and an absolute nudge put rays inside.  A faulty bound (the smaller sampled value) lit
grazed shadows, caught only by rays grazing the degenerate sphere.
`extrusion { ... rounded r }` (`Extrusion.Rounded`, an `IAssembled` like `Tube`: the five places that walk into a
tube's union walk into any `IAssembled`) is the CSG union of the outline drawn in by r (`Graphics/PathOffset.cs`:
lines exact, cubics fitted by least squares with Schneider re-parameterizing and halved until within 1e-4 of the
outline's size, measured geometrically; corners trimmed turning toward the side moved to, joined by arcs turning
away), capped; the outline itself with its outward corners rounded by arcs (`PathOffset.RoundedCorners`) from
min Y + r to max Y - r; and a `Tube` of radius r along each drawn-in run at both heights.  Refused: r over half the
thickness, a curve tighter than r (`d kappa >= 1`), a drawn-in outline that crosses itself (`EnsureUncrossed`: an
outline narrower than 2r; un-refused it overflowed the stack in path normalization), tapering.  Exact tubes cost:
a rounded guitar body filling the frame ~4-5x a sharp one.

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
`ReflectionShareAt`), which is what keeps a lantern's streak on a wet road from glittering; a glow's weight is
`VolumeLight.WeightOf` (count x odds x d^2 x `Size`, the cube root of its box's volume in the world), so the share
does not hang on the scene's unit (it did, a length short). Rough glass
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
in front of it.  Every medium helper finds its container's edges and density where it stands, the ray carried into
its parent's space (`IntoTheSpaceOf`) through its portals (`Intersection.Portals` leaving it,
`Intersection.ContainerPortals` inside it); until 2026-10-19 a medium in a moved group found its edge where the
shape would be unmoved.  No scene's instance holds a medium even so: `Instance.MayBeShared` refuses one, and a glow
must refuse, since a `VolumeLight` is made once per surface, not per placement as `LightPlacer` sets out lamps.  A medium that says no `samples` or `bounces` takes the context's
when the scene is settled (`Medium.TakeTheScenesCounts`), wherever the context is written.  A glow's
power takes its parents' scale (`VolumeLight`: the stretch is |DirectionToWorld(x) . (y x z)|); its smooth (Phong)
highlights take each sample's delivered light (`HighlightsBySample`; until 2026-10-24 the light's own white, unfaded:
a fire put a gray sheen across a polished floor); and a medium lit by
a light of several samples picks the sample per point (`Scene.ShiftFor`, salt 141), not the same few for every place.  A material's `film { thickness a [to b by <pattern>]  ior n }` (`Core/ThinFilm.cs`) is a skin
thinner than light, a soap bubble's or oil's: Airy's sum over both faces, s and p averaged, complex cosines past
the critical angle, worked out over the 32 bands in every render (`FilmShares<TS>`: Mirrored, Passed, Highlight).
It takes the place of `reflective`/`fresnel` (`GetReflectionColor` returns a film's mirror image whole; `GetHitColor`
charges Mirrored to it and Passed to what goes through), Passed scales ambient and diffuse, the highlight is spared
from transparency as rough glass's is and tinted by R over its thickness-average, a light that `CanBeSeen` gives a
film no highlight, and a shadow ray charges each face as met from the surroundings (it is not bent, so measuring
from inside would fake total internal reflection).  The substrate is the material's interior index.

A rough material's `anisotropy a [along <X|Y|Z|vector> | around <axis>]` (`Material.Anisotropy`, `BrushedAlong`,
`BrushedAround`) brushes it: `Core/Facets.cs` is GGX stretched, leaning alpha x sqrt(1-0.9a) along the grooves and
alpha / that across (so the roughness stays the average), with Heitz's VNDF and per-direction Smith masking, in a
frame (Along, Across, Normal) from `Facets.At`: the direction said in the HIT surface's own space (`around` = axis x
the local point, circles about the space's middle), carried out by `Surface.DirectionToWorld` (the transforms
without their moves, through parents and portals) and laid flat on the surface.  `IsAnisotropic` needs rough and
opaque; glass frosts alike every way.  A material that is not brushed gets `null` facets, and every site falls back
to `Microfacets` untouched: `ApplyPhong(facets:)`, `LobeShareOf`, `LookedAtRoughly` (frame, sampler, weight,
`LookingRun` odds) and the photons' `Turned`.

A material's `coat { roughness r  ior n  tint c }` (`Core/Coat.cs`) is clear lacquer over the rest of the material,
which is what lies BENEATH it (metal-flake paint = `metallic reflective 0.6 roughness 0.35 coat { }`).  `Material.CoatApplies`
needs it opaque and filmless (film + coat is refused at parse), and `Scene.CoatOn` only from outside (not `Inside`).
The coat mirrors by Schlick at its index against `Intersection.N1`; everything beneath is charged `Coat.Through`
((1-F(eye))(1-F(light)) x tint^((1/mu'eye + 1/mu'light)/2), mu' the cosine bent into the coat) per lamp in
`Light.ApplyPhong` (a `coat:` tuple: coat, outside index, highlight share), `ThroughFromAround` (the in-way averaged
over the cosine-weighted hemisphere) for ambient, bounces, caustics gathered and subsurface walks, and `ThroughOut`
for emission.  Under a coat the base's PHONG highlight is dropped (the coat is the skin now); a rough base keeps
its GGX highlight, charged.  The coat's highlight is GGX at `Coat.HighlightAlpha` -- its own alpha, or for a smooth
coat `LampRoughness` 0.05 for lamps no ray can meet (those that `CanBeSeen` give a smooth coat none: its mirror
finds them); a rough coat shares seen lights by `LobeShareOf` with its own looks.  EVERY rough highlight (a rough
surface's, a brushed one's, a coat's) from an area light is widened per sample by `Light.SpreadOf` (the light's cell
over the distance) as `Microfacets.WidenedBy`: alpha = sqrt(a^2 + spread^2/4), so a window in satin or lacquer is
one soft patch, not a grid of dots -- GGX's tail leaves its middle about half a true mirror's, measured; narrower
rippled.  Point, spot and distant lights spread nothing.  The glow THROUGH frosted glass is not widened.
`GetCoatedReflection`: a smooth coat over a smooth mirror is ONE ray weighted F + R x Through(v,v); a rough lobe
looks by `LookedAtRoughly` (GetRoughReflection's loop, for any lobe).  `SharesOf` (bailout) counts the coat;
`RegularizedFor` roughens a smooth coat (`Material.RoughAs(r, beneath)`, the base only if it would be).  Photons:
`TurnsLight` includes coats; `Turned` merges a smooth coat with a smooth mirror beneath, else picks one by
chance (`third`, free on opaque surfaces) in proportion to their shares.

A material's `sheen { color c  roughness r }` (`Core/Sheen.cs`) is cloth: Filament's cloth lobe, Estevez-Kulla
"Charlie" D (sin^(1/alpha), alpha = r^2, floored 1e-3) with Neubelt-Pettineo V = 1/4(nl + nv - nl nv), no Fresnel,
in the sheen's color, added per lamp in `ApplyPhong` (not on bounce paths: `highlights: false`).  It peaks with the
eye and lamp both low on the SAME side (halfway vector grazing); lit from the far side the halfway is square-on and
the sheen faint.  The weave beneath is scaled by `Beneath`: 1 - max(color) x E, E the sheen's directional albedo
(`ShareAt`, a 32-step table per alpha, shared statically), min over the eye's and lamp's per lamp (glTF), the eye's
for ambient, bounces, caustics, walks and the base's mirror.  `SheenApplies` needs opaque, no film, no coat (sheen +
coat or film is refused at parse).

Weathering: three patterns LOOK ABOUT a place rather than at a point (`Pattern.SeesTheSurroundings`, handed a
thread-static `Surroundings` -- scene, surface, world point, normal, portals, time index; `Scene.Shading` and
`ShadingTime` are set by `GetHitColor` and by the photon and hair-light passes -- by `Material.GetColorFor` only for a
pigment that `SeesTheSurroundings`, and by `MaterialBlend`): `crevices { radius r }` and `edges { radius r }`
(`Core/Weathering.cs`: `weathering samples` (64) golden-angle, cosine-spread rays at the hit's instant, an eighth of
them off a first hit from the eye, outward or from just under the skin into the solid, through
`Scene.NearestCrossingWithin` -- shadow-ray rules, so `no shadow` stand-ins and strands do not count -- each hit
counted 1 - d/reach, and an inward ray also by the face it leaves through, clamp(1 + n_exit.n, 0, 1), so a sheet's
far side counts nothing (R2's 0.06-inch dome panels wore through whole without it); the reach in the units of the
space the surface STANDS IN (parent's or instance's, never its own scale); memo of 4 per thread keyed (pattern,
scene, surface, time, point)) and `facing { toward d }` ((1 + n.d)/2, WORLD space).  `material blend <pattern> { [v, M, ...] }` (`Core/MaterialBlend.cs`; or N materials
for a discrete pattern) is settled per hit in `Scene.RegularizedFor` -> `intersection.Material`: scalars and pigment
(`Pigments/MixedPigment.cs`) mixed, skins (coat, sheen, film, interior, decals) the nearer's, mixtures cached at 256
steps; a blend's own properties are its FIRST material's for whatever asks without a hit (shadows, photons); a
mixture of a smooth and a rough material carries `Material.Split` and `GetHitColor` shades the hit as EACH, each
regularized for a spread ray (`Regularized`), and mixes the answers (`HitColorOf`), each lamp sample's light reaching
the place found once for both (`Scene.Reached<TS>`, keyed by intersection, light and sample; not through rough glass)
-- still ~1.6x on a weathered R2 at 200x150 (Tatooine), the smooth half's own mirror ray being real work: one mixed material changed path at the first trace of roughness, a seam in the gloss;
components are settled in `RenderInstruction` (ambient default and scale, pigment prep).  `weather { grime g [color
c]  dust d [color c]  wear w [to M]  splash h [color c]  radius r }` on any surface (`Core/Weather.cs`, settled down
groups by `Geometry/WeatherSettler.cs`, nearest wins, and at the hit through the portal chain, so a shared shape is
weathered per instance) makes each part's material a cached layered blend: crevices -> grime, edges -> wear, ground
height in the OWNER's space (`Weather.Owner`, `Material.PortalTo`) -> splash, facing up -> dust, each with a bozo-noise
broken edge; an amount of 0 adds nothing (bit-identical).  `R2D2(legs, dome, weathered)` uses it.

A material's `subsurface { radius r  [thickness t] }` (`Core/Subsurface.cs`, `Core/Scene.Subsurface.cs`) is light
that goes in, wanders and comes out elsewhere: wax, marble, skin, leaves.  `GetHitColor` shades such a hit with
`Material.WithoutDiffuse()` (swapped onto `Intersection.Material` for the light loop) and adds the walk's light; the
pigment x diffuse stays the color shown, turned into a single-scattering albedo per band by `Subsurface.AlbedoFor`, a
64-entry table of sqrt(1 - albedo) simulated for EXACTLY this walk (a million walks in a semi-infinite slab; within
0.16% of Chiang-Kutz-Burley 2016 for colors of 0.3 and up, 0.5% at 0.2, and up to 2.8% lower below that).  The walk: enter cosine-weighted at the under point,
extinction 1/radius per band, each step's band picked in proportion to throughput x albedo with balance-heuristic
weights over all bands (picked evenly, red carried 10x further than blue came out 15% dark: weights went heavy-tailed),
isotropic turns, Russian roulette under a quarter, at most 1024 turns, out at the first crossing of the SAME surface
(`FirstCrossingOf`, the walk ray carried into the surface's parent space through `IntoTheSpaceOf` -- a surface
applies only its own transform -- with the hit's portals stamped on the crossing), lit there as white matte with one
sample per light.  `subsurface samples` (64) walks at the first hit from the eye, one after.  A SHEET walks through
a virtual slab of its `thickness` (default the mean radius) tracking depth only (`SharedOutBy`), giving reflected and
passed shares, lit from the front and from behind (`LitAsMatte`, every light sample).  Bounced light and caustics on
a subsurface surface are still taken at the entry as matte.  Surfaces seen from inside, or transparent, are as before.

Bounced light is path continuation, off by default: `context { diffuse bounces N  diffuse samples K }`
(`Core/Scene.Bounces.cs`).  A matte surface sends K cosine-weighted rays at the first bounce (one after, with
`_spreading` set), each bringing back `GetColorFor`'s whole answer; their average times pigment and diffuse joins
the surface's own color in `GetHitColor`, under its Fresnel share and film.  On a bounce's straight run the sky
counts nothing and a glow that `gives light` lends no glow (`SeenAlongABounce`, `MarchedThrough`'s `withoutGlow`), as
the sky light and volume lights count those directly -- and with no caustics the run carries on through smooth glass
(`GetRefractedColor` moves `_bouncing` onto the bent ray), since a shadow ray passes glass straight then: bent off its
run by a pane, a bounce found the sky again and a floor under a window came out 2.2x; all along a bounce path (`_bounced > 0`) what a shadow ray
passes -- `no shadow` stand-ins, light housings -- is passed (`PassedByBouncedLight`), and surfaces show no highlight
(`ApplyPhong(highlights: false)`), glossy-to-matte light being the caustics'.  A lamp at a bounce point gives one sample
picked per point (`Illuminate`); bounces do not spend the mirrors' `remaining`.  Asking for bounces scales EVERY
ambient to 0, a library's named ones included; what glows of its own says `emission` (`Material.Emission`, a number or
color tinted by the pigment, the same under any lamps, unlike ambient which each lamp lends; it leaves through the
skin as the rest of the surface's own light does -- its Fresnel share, a film's Passed, a coat, a sheen).  An opaque closed surface
hit from inside is lit from the eye's side (`Intersection.LitPoint`); glass met from within keeps the outside over
point, so its inner face takes the light falling on its outer one.  `diffuse cache [error e]` (default 0.15) is Ward's
irradiance cache (`Core/IrradianceCache.cs`) for the FIRST matte hit from the eye only (`_bounced == 0`, not spreading,
not a single-band ray): records of the arriving light (before pigment, so texture stays sharp) with a reach = the
harmonic mean of the K rays' first-hit distances, clamped to 3-64 pixel footprints over the error, blended by Ward's
weight less 1/error (zero at the edge), refusing records in front of the point.  A record keeps only FAR light: the
average of its rays whose first hit lies beyond near = 2 x error x reach (`NearOf`), and that share (`Open`, which
weighs its say).  A covered pixel gathers max(16, K/16) rays itself (`_nearRay`/`_nearLimit`): `GetColorFor` stops
that ray (only it, by reference) at its first hit beyond near, or the sky (`BeyondTheNear`), sets `_escaped`, and
the gather adds the blended far light for it -- so contact shadows under a rug's edge are the pixel's own (a whole-
light blend leaked 6x the light there, `TestThePlaceUnderAnEdgeSeesTheEdgeItself`).  Pretrace returns the blend
for covered places and never gathers near.  `Camera.FillTheBounceCache` pretraces
pixel grids at strides 32 down to 1 before rendering; each pass reads only earlier passes, its offers merged in key
order (`EndPass`), so a cached render is deterministic however threads share the work.  With a cache K is per record,
so it can be thousands; too few shows as blotches, not grain.

Caustics are a photon map, off by default: `context { caustics [photons N] }` (2M by default; `Core/Scene.Caustics.cs`,
`Core/PhotonMap.cs`).  Before the first pixel (before hair light and the bounce cache), photons go from EVERY light at the world
bounding balls of everything whose material
`TurnsLight` (reflective, film, or transmitting with an index; rough or smooth; at most 64 targets, clustered by a
grid; unbounded ones skipped), split by each (light, target) beam's power.  Each photon sets off from its OWN place on the light,
picked continuously (`Light.PhotonSample`: anywhere on an area light's face, any sky direction by map and
evenly, any place in a glow) -- from the light's fixed samples a lens made a speck per sample.  A photon's
power is set at the first thing it does not
pass straight by -- the light there (pi x ColorFor(sample there) x Cone x fade, so a glow's 1/d^2 cancels, a lamp's
absence of falloff does not) times its share of the beam's cross-section -- and it is followed exactly as the eye's
rays shade (`Turned`: film shares; a VNDF facet each way for rough, weight F x G1(out); else ReflectanceAt, metallic
tint, Fresnel split via `Reflectance`, `Bent`, filter tint), and charged for each run as an eye ray is: the insides
it leaves, else those of what the place stands inside (`ContainerBefore`, fade and medium -- until 2026-10-19 a pool's
floor took the sun unfaded by the water over it), else the surroundings; a medium whose density varies is walked in
16 steps, as a shadow ray walks it.  At a fork the greater way always goes
on and the lesser by chance lesser/greater, so all photons carry alike; none is killed for losing light; choices
come from digit-scrambled Halton sequences (`Pick`, `Bases`) at the first surfaces, hashes after; power is tested
against exact nought (IsBlack's allowance ate a glow's photons).  Kept where a TURNED photon meets a diffuse
surface; one that meets matte first is the light's own, and passing something that does not bend (a bubble, a
sheet, `no caustics`) is not a turn.  With caustics on, a shadow ray stops at glass that bends light
(`StopsTheShadowRay`), and a bounce path finds no sky light's sky and no light-giving glow anywhere along it
(`CountedElsewhere`; a backdrop with no sky light still counts).  `no caustics` (settled down groups by
`ShadowSettler`, through instances by `Portals.MakesNoCaustics`) restores the straight line for flat panes.  A
diffuse hit adds pigment x Sigma(photon x cone filter x max(0, cos^brilliance x diffuse - grain)/cos) / (pi r^2) / pi
from the 200 nearest photons on the same surface (80 left a frosted block's light grainy: photons set off by more
than two numbers land as if at random, so a gather wobbles by ~1/sqrt(k); tests sum photons over squares instead) (normals within 0.9, within a quarter of the radius off its plane),
never over less than the ray's footprint radius (a sun focuses to a point).  Every branch of the tree of 8 or more
photons carries a `Bound` (its box and a cone round its photons' facings, widened by the 0.9; numbered heap-fashion,
not by array place) and a search passes by any branch that can hold nothing counting -- finding exactly what it did:
the tree halves along the widest spread, so it never splits a floor by height, and the water above a pool's floor
looked over a square two meters across of floor photons out of its reach at every place (4.5 billion photons looked at for
200 thousand gathers; a 400x250 preview took 46 minutes).  A ray mirrored inside glass sets off
from `Intersection.MirrorPoint` (the under point), not the over point outside it -- for smooth reflection and all
photons; the eye's ROUGH reflection still uses `LitPoint`, since inside, internally reflected rays meet the far face
from random directions and read a lamp's narrow frosted glow there by chance (glitter).  A photon counts only if the first thing
it TURNS at is its own target's -- the first target, in order, whose ball holds the place (`OwnerOf`) -- or
beams at overlapping targets counted the light through both twice.  In a spectral render (`CausticsSpectral`,
from context.Spectral) a photon of every color taking the bent way through glass that `Disperses` is split
(`SplitThrough`): a band picked evenly over the photons, a wavelength jittered within it, 32x that band's
share of its light (`BandSpectrum.FromIlluminant`), re-bent at that wavelength; thereafter every share is taken in
its band (`Hue.Of`), and it is kept with its band (`Photon.Band`); a spectral gather adds band photons band by
band (`FromSampled`) to the full ones (`FromIlluminant`).  RGB renders never split.

`context { trace depth N }` sets how many surfaces a camera ray may be mirrored or bent at (`Scene.TraceDepth`, 4
by default, as it always was): a tumbler of water looked through is eight deep, and at four it came out black.
Asking for a depth also turns on adaptive bailout (`Scene.Bailout` = `RenderContext.Bailout`, 1/1024): a way a
ray goes is followed only while what it is worth of its pixel (`_fallenShort`, thread-static, worked out by
`SharesOf` as `GetHitColor` will weigh it) is at least that.  Without a depth, nothing changes, bit for bit.

Rendering itself is dispatched through `Scanners/` (`SingleThreadScanner`,
`LineParallelScanner`, `PixelParallelScanner` — implementations of `IScanner`, selected via
the `context { scanner ... }` DSL block or command line) and `Pixels/` (anti-aliasing
strategies: `NoAntiAliasingPixelRenderer`, `AdaptiveSuperSamplingPixelRenderer`, controlled by
`AliasingOption`; a subdivided quarter hands its corner, already traced, down as the quarter's own, rather than
tracing it again -- a fifth of the subdivided rays). Output goes through `ImageIO/` (`ImageFile`, `ImageCache`,
`ImageReference`) which wraps SkiaSharp/Magick.NET for reading/writing image formats.

The camera's response, from light to picture: a camera's `exposure` (stops; `Camera.Exposure`, or `--exposure` as
`RenderContext.Exposure` over every camera) scales each finished pixel in `Camera.Render`, after white balance, so the
canvas holds exposed LIGHT, premultiplied: a pixel half covered against the transparent background holds half its
color.  A picture file is written with STRAIGHT alpha (`ImageFile.Straight`, before developing: a soft edge kept its
premultiplied gray and fringed whatever the picture was laid on); a file of light stays premultiplied, as OpenEXR's
convention has it.  A picture file is developed only when written: `Color.ToChannelValues` takes
`RenderContext.Shown` -- the `FilmCurve` (`Graphics/FilmCurve.cs`: `soft` by default, each channel identity below
0.8 and eased above it by Khronos PBR Neutral's shoulder, so bright orange runs through yellow to white as film's;
`neutral`, that shoulder on the brightest channel with the hue kept and paled, WITHOUT Khronos's toe -- the default
for a day, it turned flames peach and Tatooine's sky beige; `agx`; `none` clamps) -- then encodes with
`RenderContext.OutputCurve`: the sRGB curve unless a `gamma` was SAID (`GammaIsSaid`, set only by the scene or the
command line; then a plain power, declared by a gAMA chunk, `ImageIO/PngGamma.cs`, since Magick.NET's gamma is read
only).  `no gamma` is raw: no film curve, no curve, clamped.  Until 2026-10-19 output was a plain 2.2 power that every
viewer decoded as sRGB (a thousandth of white shown as 0.0034) and clipped at white.  `.exr`, `.hdr` and `.pfm` are
written by `ImageFile.SaveLight` as exposed linear light, undeveloped and unclamped, the image tagged linear first
(left sRGB, Magick.NET decoded it on the way into Radiance: 3 became 12.8); Magick.NET cannot read back an OpenEXR
one pixel high or wide.  When a picture is not the light as it stands, the adaptive sampler compares samples as
shown (`PixelRenderer.Seen`: exposed, then developed), so an exposed-up dark scene subdivides as a bright one would.
A camera's `blades n [turned a]` make its opening a polygon: `CameraSampler.ToPolygon` maps the lens samples into n
equal triangles (sqrt warp outward), so bokeh takes the shape, and `glare g` spreads the finished canvas (after
exposure, before any film curve) by the opening's diffraction pattern (`Core/Diffraction.cs`): |FFT|^2 of the
opening on a 1024 grid (opening 0.24 of it, edges supersampled), summed into a table and box-integrated over each
pixel for each of the 32 bands (pattern size by wavelength, first dark ring 1.5 px at 550 nm in an 800-wide picture,
the same share of any width; band weights the clipped unbalanced CIE->sRGB answer, each channel's adding to one),
convolved by `Basics/Fourier.cs` (radix-2, grid twice the picture's sides) as (1-g) + g x pattern.

Animation (`docs/animation.md`): `context { animation length L | frames N [rate R] [still S] }` makes a scene an
animation; `ImageRenderer.Render` reads it first (`InstructionContext.AnimationOf`, ContextUpdaters' animation
resolvers only, in the renderer's own scope -- so no scene names) and then carries the WHOLE scene out again per
frame (`_objects` cleared each time), its scene scope a child of one holding `time` (n / rate, seconds) and `frame`
(`FrameScope`; a still has both nought).  Frames go to `name-0000000.png`, or into one movie (`ImageIO/MovieFile.cs`,
frames developed by `ImageFile.Developed`): `.webp` (the gallery's; lossy q90, delays in ms) or `.gif` (quantized,
dithered, optimized; hundredths) looping, held until written; `.mp4` streamed as rgb24 to ffmpeg (libx264, crf 16,
yuv420p, padded even) -- refused before any frame when `MovieFile.FfmpegOn(PATH)` finds none, and a non-animation
makes a one-frame video; `-m n` is frame n
alone, written as asked, `--frames a-b` a run (numbered however short: `--frames 7` writes `name-0000007.png`),
`--still` the named still (a non-animation renders as ever; a still that is not one of the frames is refused); each
frame counts its own statistics.  An
animation renders through ONE render command.  `Name = keys [smooth] [loop] { t: v [linear|ease [in|out|in out]|hold]
... }` (`General/Keyframes.cs`, `Instructions/Core/KeysResolver.cs`) is called as `Name(time)` (`FunctionCallTerm`
looks a `Keyframes` up by name); numbers, tuples, points, vectors, colors; smooth = Catmull-Rom tangents in time.
Blur from time: in a frame (`RenderContext.FrameInterval` > 0) a surface whose `TransformResolver` gives another
matrix a frame later gets `MotionAt(f) = M(time + f x interval) M(time)^-1`, re-resolving in a child scope with
`time` shifted (`SurfaceResolver.TimedMotionFor`, only while `time` is the frame's: a scene naming its own is
left alone), laid under any `motion { }`; values named from `time` beforehand, shapes and the camera are not blurred.
A motion recipe (`motion { }` or the timed one) reads its names AS THEY STAND where it is written
(`Variables.AsTheyStand`, `RenderContext.AsItStands`, snapshots taken at resolve): read lazily at render, a name
reassigned later in the same scope moved the earlier surface by the later value.
A primitive's body's motion is in the primitive's frame, carried out through the call's transform beneath the call's
own motion (`SurfaceResolver.ApplyToSurface`).  Pigments, decals and films are read where the thing stands at the
instant being shaded (`Scene.ShadingTime`, a hit's `TimeIndex`), so a moving pattern blurs with its outline.  A
placement moves a moving surface through `StandAt`, its motion carried round the move, measuring all it sweeps.  Splines are values (`Name = [discontinuous] spline { ... }`; a sweep takes
`spline Name`, a TermResolver so a primitive's parameter works), measured by `Spline.Measure` (`Geometry/SplineMeasure.cs`:
64 chord steps a curve, distance found by binary search then on its curve; a spline ending at its start LOOPS, any
other clamps).  The transform `along S at share | by distance [upright | banked] [wheelbase w]`
(`Instructions/Transforms/AlongCreator.cs`, refused inside `motion { }` at parse) stands +X along the heading, +Y up,
+Z = X x Y (the thing's left): upright = world Y orthogonalized; banked = BendAt (kappa N) x speed^2 + g (9.81, so
meters), the speed the amount's time derivative over 1 ms; a wheelbase puts both ends on the spline (chord middle,
chord heading).  `length(spline)`, `pointAlong`, `headingAlong` (`Terms/SplineFunctions.cs`).  Trains `TrackAlong(S)`
sweeps the rail and ballast sections along S, sleepers stood `along` it.

Forces: a surface's `body { mass bounce friction drag radius velocity spin  shape sphere|box  tether to <pin | 'name'>
[length l] }` (`Core/Physics/Body.cs`; meters, kg, s; mass unless said = volume x 1000) is moved by
`Geometry/PhysicsSettler.cs`, run after PlacementSettler and before ArrangeSurfaces: TOP-LEVEL surfaces only (one in a
group or CSG, or a swarm there, is refused -- a swarm would stay empty), the rest of the scene the colliders, found
by rays through their own `ArrangedSurfaces` and taken to stand where they are at the frame's time.  `Core/Physics/World.cs`
re-simulates from time nought EVERY frame (so a frame is the same alone or in a run; a late frame costs more: the
cradle's 5 balls about 1.1 s at t = 4, 0.4 s with one round): dt 1 ms, push (context `gravity`, default
(0, -9.81, 0), and drag 0.5 rho Cd A |u| u against `wind`, Cd 0.47 ball / 1.05 box), then 10 rounds of strings (pull
only: position projection, then the outward speed removed), body pairs AS BALLS (sequential impulses at the lever,
bounce the mean, friction sqrt(mu1 mu2); a body turned by its ties spends no impulse turning, `Mover.Turning` 0), and
the scene (a ball looks 14 ways by rays reaching 1.25 radii -- the widest gap between the looks is 36 degrees -- its
depth measured square across the face met, the deepest a round: by its radius along the look, a ball on a 30-degree
slope stood sunk an eighth of its radius; a box casts to its 8 corners and is held at EVERY corner within 0.5% of its reach -- `Press`: pushed
out once by the deepest, each corner's push ACCUMULATED over the step's rounds and clamped at nought, its friction
capped by mu x that accumulated push, the bounce targets of all corners newly met settled BEFORE any is pushed
(settled as each was met, a flat drop came away at 0.43 of its bounce and spun 6 rad/s); met afresh each round, the
first corner took all of gravity and a die pivoted about it, creeping 4 mm/s; under 0.05 m/s into it keeps no
bounce).  A body under 5 mm/s at its fastest point for 0.2 s falls ASLEEP (Push and Strike skip it; Part and a
stretched string wake it): gravity sinks a resting corner g dt^2 = 1e-5 m a step whatever the size, so a die never
quite settled.  A hanging ball is stretched every step, so never sleeps (the cradle was bit-identical).  Ten rounds
measured no different from one on a cradle; they are kept for several contacts at once.  A tied body is turned by its ties
(`TurnWithItsTies`: the least turn carrying the first string's way at time nought onto its way now, then about it
for the second), others integrate spin.  **A body moves about its own origin**: a thing must be drawn about its
middle and placed by its transforms (the cradle's primitive drew each ball off-center and every body stood at the
world origin).  The settler records poses at the shutter's instants and `Surface.StandAt(here, motion)` rebuilds the
moving transforms, so bodies blur.  A `swarm { of T  from toward spread speed a to b  rate  since  life a to b
bounce friction drag radius }` (`Geometry/Swarm.cs`, a Group) is filled by the settler: copy k born at since + k / rate
(`since` < 0: already in full flow at nought; only copies within the longest life of the time are looked at), its
way, speed and life hashed from (seed, k, purpose) -- never drawn in order -- flown from its birth at 1/500 s, to the
frame's EXACT time (stopping within half a step left each copy up to 1 ms behind, by its birth's phase), with swept
rays against the colliders, an `Instance` of T at world scale with a `MotionAt` for blur; copies strike neither each
other nor bodies.  A swarm that MOVES (`MotionAt`) is refused: its copies would be carried along in flight rather
than left behind.  A swarm needs an `of`, and T must be shareable (`Instance.MayBeShared`); a swarm, or a field
dropped `over` something, cannot be placed `on`/`under`/... (both fill themselves after placements).  `within S` (a surface value, in the swarm's space) starts each copy at its own place inside S (box
draws by (seed, k, 100 + 3 x try), `Containment.Holds`, 256 tries, refused if 1024 probes find no inside), and a copy
PASSES what is inside S until it leaves it (else leaves let go in a crown came to rest on its leaves); `falls at a to b`
sets drag = g / v^2 per copy; `spin a to b` tumbles a copy from a Shoemake turn about a random way, and a tumbling copy
RESTS once a contact leaves it under 0.05 m/s -- no more flight -- settling over 0.15 s (smoothstep, slerp) onto its
thinnest box axis against the contact normal, lowered by a ray to half its thickness (at least 1% of its radius).  A
copy's size is `BoxAround(Thing, true)`, its own transforms included: until 2026-10-23 it was the thing's own-space box,
so a drop scaled to 7 mm was a 1 m ball, met the pool a meter early (the fountain's jet ended in an umbrella) and felt
no drag.  Swarms that do not spin are bit-identical to before but for that size.  A default radius, body or copy,
is half the box's longest side (the ball round the box held a drop up off the ground).  No stacking, cloth, ropes or
fluids.

Kinematics: a `joint { pivot  axis X|Y|Z|v  angle  limits a to b  aim at <place|'name'> [facing v]  reach
<place|'name'> with 'name' ... }` (`Geometry/Joint.cs`, a Group) is turned by a `JointCreator` the parser puts FIRST
among its transforms (so a timed angle blurs as any timed placement; no axis or no angle = identity).  `mark 'n' at p`
(`Geometry/Mark.cs`, an empty-boxed surface hitting nothing, among group entries only) names a place; `span from 'a'
to 'b' [stretched]` on any surface (`Surface.Span`) stands its origin at a, +X toward b, +Y as near its parent's +Y as
allowed, X scaled by the distance if stretched, laid over its own transforms.  `Geometry/KinematicsSettler.cs` runs
after PhysicsSettler, before shadow settling and arrangement: per shutter instant (`Surface.TransformAt`, overrides
laid over resolved matrices) solved joints outer-first -- aim (least turn about the axis, limited; free = `Turn.Between`),
reach (CCD tip-inward over every joint between the named effector and the joint, 64 passes, each limited) -- then
spans, then `StandAt(here, motion)` and every holder `Resettle`d deepest-first (`Group.Resettle` re-arranges, a box
rebuilt only if it was derived).  A name is the NEAREST of that name (most shared enclosing groups; ties and nothing
named refused), so library marks need no unique names.  `Instance.MayBeShared` refuses a solved joint, a mark and a
span, and one inside an instance anyway (a swarm's copy) throws.  The joint's entries are `groupEntryClause`
alternatives tagged 'jointProperty' (a nested alternative list loses its tags) placed AFTER `localClause`, or
`reach = ...` in a library body parses as a joint's reach.  Trains: `Locomotive`/`Tender(..., travelled)` turn wheels
as joints by distance / radius, rods `span` crank pins and the crosshead (placed by the main rod's length); the
distance is a value, so spokes are not blurred.  Indoor lights: `Anglepoise(target, height, variant, lit)`, turret
aims, two arms reach the wrist mark to height over the target, head aims, springs span stretched.

### Pigments and patterns (`Pigments/`, `Patterns/`)

Pigments (`Pigments/`) define how color is derived at a surface point (solid, pattern-based,
image-mapped, blended/layered, noisy). Patterns (`Patterns/`) define the underlying spatial
functions (stripes, checker, gradient, agate, wood, granite, brick, hexagon, etc.) that
pigments sample. Each has a corresponding DSL resolver under `Instructions/Patterns/` and
`Instructions/Pigments/`.  What a pigment says nothing of for itself is the surface's being read (`Pigment.Wearer`, a
thread-static set by `Pigment.GetColorFor(surface, ...)` and `MaterialBlend.ValueAt`): noise given no seed takes
`Pigment.WearersSeed` as it is read, and an image given no map is laid by the wearer's shape (`ImagePigment.Laid`,
holders never asked).  Settling put both ON the pigment, so a pigment worn by many surfaces -- named, or a group's --
was the first wearer's everywhere; settling seeds nothing now.  A pattern picking among three or more pigments is filtered by each one's share of the
patch (`Pattern.SharesOver`, `PigmentSet.GetMixedColorFor`): averaging its index made a hexagon's border with a
third color down it; two-pigment patterns keep the averaged index, which is the same thing -- except across a value
that wraps (`Pattern.MayWrap`: a repeating, unbounced `gradient`), where a patch straddling the jump from 1 to 0
averaged to the middle color; there `PatternPigment.FromItsSamples` averages the COLORS at its samples
(`Pattern.SamplesOver`, which also gives the patch's value from the same pass -- sampled twice, a turbulent desert
took twice its time), for patterns averaged over the base grid only (a noise pattern filters itself).  Marble's wave is a
triangle by default (POV-Ray's), and it and wood take turbulence's amplitude as POV-Ray's do (marble x + A.X t, wood
cycloidal((x + t) A) per axis); agate takes only its turbulence's octaves.  An image pigment reads a `Texture` (`Graphics/Texture.cs`, cached per file in
`ImageCache.GetTexture`): the file turned into light by the curve it declares (`TransferFunction`: sRGB for PNG/JPEG,
linear for `.hdr`/`.exr`, which keep light above white), premultiplied by alpha, with a pyramid of halved copies, and
read by EWA over the footprint's two edges carried into texels (`ImageMapType.AxesFor`), the ellipse's half-axes 0.78
of a pixel's side so its spread matches the pixel's box; bilinear with no footprint.  A `no gamma` scene reads an image's values as they stand (its colors are file values),
and a height field reads the raw values too.  An image background is read with a footprint too (`Scene.SkyFootprintOf`); a pattern sky is not, since a
footprint straddling the horizon averages in the gradient's repeat below it.  An image takes `{ brightness b  <transforms> }`
after it (parsed by hand in `ParseImagePigmentClause`).  A sky light carrying an image (`SkyLight.IsAPicture`) is
image-based lighting: half its samples are picked by a `SkyMap` (luminance on a grid twice the image's width, at most
4096, read at cell middles) and half evenly, each counted by the balance heuristic over both (`SkyLight.OddsToward`),
and the pattern is turned per point (`Scene.ShiftFor`, salts 107/108) so a cloudy sky is grain, not blotches.  When it
carries the shown background untinted (`Scene.PictureSky`), a rough surface shares it with its reflection, direction
by direction (`HighlightShareOf`, `SkyShareOf` on a `LookingRun` with a `Mirror`).  Pattern skies keep the even
fixed pattern.  A smooth highlight from ANY sky light takes each sample's own light (`Light.HighlightsBySample`); until
2026-10-17 it took the light's white color, a white sheen under a blue sky.  One pattern
depends on direction rather than place: `flop` (`FlopPattern`), the angle
between the eye and the light's mirror direction (`Basics/Viewing.cs`, `Viewing.Aspecular`), for color-shifting
paint.  A `Viewing` (eye, normal, light) is handed down only to a pigment whose `SeesTheViewer` (cached per pigment in
`Material`), so every other pigment is asked exactly as before; it is worked out per light in `Light.ApplyPhong`, and
with no particular light (transparency, refraction, metallic tint, shadow tint) the light is taken to be at the eye.
`NoiseGenerator` takes a point's lattice cell in 64 bits masked to its 256: in 32 it threw past two billion cells, which
a ground plane met near the horizon reaches in a fine gravel's noise (a fountain's movie died after half an hour).

### Expression language (`Terms/`)

DSL expressions (numbers, colors, vectors, tuples, variables, arithmetic, unary color/point/
vector casts) compile down to a `Term` tree (`Term`, `LiteralTerm`, `VariableTerm`,
`BinaryOperation`/`UnaryOperation` subclasses, `TupleTerm`). `ExpressionTreeBuilder`
(`Parser/ExpressionTreeBuilder.cs`) is plugged into the `Lex` expression parser as the
`TreeBuilder` to build these from parsed tokens. Terms are evaluated against `Variables`
(`General/Variables.cs`) at instruction-execution time, so DSL variables are late-bound.  A name holds a value
per type; read where no type is asked (`Shade = Coral`) a name holding several is refused, naming them
(`VariableTerm`, `Variables.ValuesOf`).  Numbers compare near (`ComparisonOperation`: `==` by `Near`, and the
orderings agree, so exactly one of less, equal, greater holds).  A `function`'s answer is held to its declared kind,
coerced as a value written there would be (`UserFunction.AsPromised`); a built-in's `ArgumentException` or
`InvalidOperationException` is rethrown carrying the call's token (`FunctionCallTerm`).  A loop's range open at its
end leaves out only the end itself (`Interval.IsAtEnd`).

### Fonts and text (`Fonts/`)

TrueType font loading/layout (via `Typography.OpenFont.NetCore`) for the `text { }` surface
type — glyph outlines are converted into `GeneralPath`s and extruded like any other 2D path.

### Entry point and CLI (`Program.cs`, `Commands/`, `Options/`)

`Program.cs` uses `CommandLineParser` to dispatch to verbs: `render` (default,
`RenderOptions`/`RenderCommand`), `fonts` (`FontsOptions`/`FontsCommand`, for managing the
font catalog; `--overwrite` replaces a face through `FontManager.ReplaceFontFace`, keeping its
kerning) and `libraries` (`LibrariesOptions`/`LibrariesCommand`: list, check and `--install` the
shipped libraries into `~/.rayTracer/Libraries`, read from the build output). `RenderCommand.Render` builds a `LanguageParser`, parses the input file into an
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
