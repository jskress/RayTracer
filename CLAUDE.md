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
include cycle).  Each entry's parser has its OWN Lex `FailureTracker` (`CurrentFailureTracker`), which a message about
unexpected input is built from: shared until 2026-11-02, a library read to its end left its furthest failure standing
for the scene's mistake -- the library's expectations, at a line of the library, reported against the scene (and past
the scene's end, `ShowError` fell over with an IndexOutOfRangeException).  `import 'library' { Names }` reads a library (definitions and imports only, see
`InstructionContext.HoldsOnlyDefinitions`) from `~/.rayTracer/Libraries`.  Values go over whole; functions and
primitives only when asked for.  An imported value is a recipe made among the SCENE's names (so a scene may add to
it), so every call written in a library -- a primitive call of any kind, a function call -- is stamped with the
library's `LibraryScope` (`General/LibraryScope.cs`: thread-static `BeingRead` while the parser reads the library,
its `Variables` filled when the import runs) and finds what it calls there first, then where it is made.  Until
2026-10-26 a value calling a helper the scene was not given failed ("nothing named X is a primitive"), and one the
scene had named alike was called instead.

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
species' height factor measured over five variants.  Given `all over 'name'` in place of a crown, a grown tree CLIMBS
that sibling (`GrownTree.GrowOver`, from `PlacementSettler` beside dropped fields; empty when prepared): points by
`SurfaceSpread` (`points` spread again, more, so the part its limits keep holds that many; or `spacing`), kept by the
field's own limits (`ICoversASurface`, `SurfaceSpread.KeptBy`/`KeptByThePatches`; parsed alike by `HandleCoverClause`
through `ICoverResolver`), grown from the nearest place within `reach` of its origin, every step walked by
`GrownTree.Walker` -- a ray ahead (a corner, so a fin thinner than a step is climbed), a ray down onto the face, and
from just past an edge back onto the face beyond (insets 0.5/0.2/0.05 steps) -- smoothed places set back down on it,
stems held off it by their radius (sinking a little into an inside corner), leaves on every place of wood no thicker
than six twigs, standing OUT of the surface (+Y out, +Z down it within 45 degrees).  `spread` caps the run along the
wood, for any grown tree.  The `climbers` library's `Ivy` and `Creeper` (meters) thin their points from two fifths of
the spread on (`patches spherical` with turbulence) for a ragged edge.  A `Fracture` (`fracture { solid ... }`, a `Group`) breaks a
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
cover).  A `TileGrid` (`tiles { size cell tile { <surface|call|nothing> front 'w' ... sides top bottom turns weight }
edge above below fit 'a' to 'b' tries }`, a Group) is laid when RESOLVED, as a grammar is
(`TileGridResolver.SetProperties`, nothing without tiles): Gumin's simple tiled wave function collapse
(`Geometry/WaveFunctionCollapse.cs`), states = tile x turn (`turns`: sides[(side + turn) % 4], placed by `RotateAroundY(90
turn)`, weight shared among turns), support counting, least entropy first, draws splitmix-hashed from (seed, attempt,
cell, purpose), begun again up to `tries` (100) on a contradiction, refused at once when the outside rules out a cell
before any draw.  Sides fit when equal or either unsaid -- except a word in a `fit` pair, which fits only its partner:
equality alone let a house's `curb` front face another house's, rows facing rows with no road (the town's first try).
Each tile built once, stood by `Instance` where it may be shared, cell middles over the origin on the cell's floor.
`gallery/Local/towns/a-town-that-laid-itself-out.igl`.  A `Field` fills a 2D outline in the X/Z plane on a jittered grid with copies of one or
several things (`of A, B weight 2`), each built once and stood by `Instance` where picked, `turned` and `sized`;
what stands at a place, its turn and its size are hashed from the place's grid cell (`Field.DrawFor`), never drawn
in order, and the jitter keeps its old in-order stream so fields asking for none of it are unchanged.  The
undergrowth and rocks libraries' areas (`Grass`, `Reeds`, `Wheat`, `Scree`) are eight such variants, not a build
per copy (`index in n`).  `over 'name'` drops every copy straight down onto a named SIBLING (`Field.DropOnto`, called
by `PlacementSettler` after placements, at the one moment children are prepared and not yet arranged), upright or
`tilted` (slope from a few extra drops either side, in the shared space), skipping misses, `steepest`, `above`,
`below`; a dropped field is left EMPTY when prepared and fills itself at the drop (`Field.Fill` with a `Dropping`),
building only what lands and then running the group's own preparation over it.  `all over 'name'` covers a named sibling's WHOLE
surface instead, filled at the same moment (`Geometry/SurfaceSpread.cs`): Crofton lines as hair's roots (in the
FIELD's space, so the spacing is the field's), at least 16384 and 5x the wanted crossings, thinned by Yuksel 2015
weighted sample elimination to round(area / spacing^2) (`Crowd`: cell-sorted arrays; places whose normals differ by
more than 120 degrees never crowd, so a plank's two faces are each covered whole), each copy's up the surface normal
(`Field.Leaning`; straight down is half a turn about X) unless `upright`; `above`/`below`/`steepest` (now null =
no limit, up to 180) and hair's `facing [by p]` (its own draw) are the shared space's, patches read at the 3D place,
draws keyed by the place's number among those found, so cutting places leaves the rest put; at most 500,000.  A primitive call hands an empty
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
A point light's `profile 'f.ies' [turned a]` (`Core/LightProfile.cs`, IESNA LM-63 type C; A and B refused; tilt tables
skipped; quarter, half, 90-270 and whole symmetries mirrored; read bilinearly between its angles) is its SHAPE only,
normalized to a peak of one -- brightness stays the light's -- applied by `PointLight.IntensityToward` (so shading,
media, hair and photons all take it) and by an area light's every sample; it hangs straight down, horizontal nought
along +X and 90 along +Z, `point at` aims it (a spotlight's at its cone's aim), and its frame is kept as two points
one unit off the light so `PlaceBy` carries it through groups (a spotlight places its base first).  A primitive's
group holding a light and NO surface was taken for empty and handed back as a new empty group (`SharedAs`), its
light lost, until 2026-11-01.
An area light's samples stand at the middles of its cells, jittered within them, so every one is on its face
(POV-Ray's grid ran edge to edge and its jitter spilled past it); a spotlight's `radius` and `falloff` are angles
read as `angles are` says (degrees unless told), the falloff 30 degrees unless said.
A `group` may hold point, spot and area lights in its own space (`Group.Lights`; a light travels through surface lists
as a `ListedLight`, so loops and choices may set lights out, at a scene's top level too).  `LightPlacer` sets each out
in the world after the scene is arranged -- once for every placement, walking instance chains, and once for every
sample time if the group moves (`Light.At(timeIndex)`, asked by every shading loop) -- and records its `Housing`:
the closed surfaces of its own group round it (odd crossings on all of seven looks), which its shadow rays skip,
keyed with their portals so one lamp's bulb is not the next one's.  Placed lights are ordinary lights to the shading.  A scene with more than sixteen FADING point/spot/area lights
(`LightTree.Fades`: a FadeDistance, power above nought; a light that never fades cannot be ruled out by distance)
holds them in a `LightTree` (`Core/LightTree.cs`, built by `Scene.ArrangeTheLights` after the sky's openings;
`context { light tree [samples n] [split s] }` asks for one of as few as two, `no light tree` refuses): Conty-Kulla
importance (pbrt-v4's `LightBounds::Importance`, each node's fading over the nearest the box can be in place of the
inverse square, one-sided for an opaque solid, guided on a glossy place by its highlight's lobe -- strength = Schlick
F at the eye's slant / (4 pi a^2) / half the diffuse, power 1/(2a^2); a flat boost doubled a floor's grain) with
Estevez-Kulla splitting: from the root a node whose radius is at least `split` (0.25) of its distance is opened, its
leaves taken whole (at most 64 unless split is 0), the rest sampled by `samples` (16) picks spread evenly over their
importance, ONE number stretched down the tree (pbrt's SampleDiscrete), each pick worth 1/(picks x chance), repeats
merged.  Every shading loop asks `Scene.LightsAt(place, normal, first, oneSided, lobe)` for (light, weight, held):
the scene's list at weight one with no tree (x 1.0, so bit-identical), else `Always` (not held), `LightTree.Ambient`
-- one light carrying all held lights' light, its sample worth nothing (cone 0, no shadow ray), so a surface takes
only the AMBIENT every held lamp lends, which no distance thins (left to the picks, 600 bulbs' ambient sparkled) --
and the picks, whose own ambient is off (`ApplyPhong(ambient: false)`).  Places along a bounce, in a medium or a walk
take one pick.  The frosted-glass glow (`GlowsAimedThrough`) still loops every lamp, its MIS assuming each is aimed at.
Every light's housing is one set (`Scene.HousesALight`).  `along <path>` lays copies along each run of a
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
A `Loft` (`loft { section { path P <transforms> [at s] } ... [spline S] [straight] [open] }`, `Geometry/Loft.cs`)
stretches a skin through outlines: each turned into cubics (lines and quads raised, zero-length segments dropped, a
closed run made CCW), outlines of as many segments corresponding segment for segment (a hull's plank edges run its
length), others each cut exactly (de Casteljau) at every outline's join shares by arc length (16-point
Gauss-Legendre, bisection), so a square meets a circle at its eighths.  Across sections the nets run non-uniform
Catmull-Rom spaced by the sections' middles (`straight`: thirds of the straight run), one bicubic patch per segment
per band; along a spline a section stands in its rotation-minimizing frame at share `at`, its own transforms first.
Found exactly: a box tree over the patches, each patch's de Casteljau pieces built ahead (control-point boxes, leaves
0.02 of the loft's diagonal, 14 deep), Newton on (t, u, v) at a leaf to 1e-12, repeats within size x 1e-9 dropped.
Closed outlines are a solid capped flat (plane + `GeneralPath.Contains`), open ones (or `open`) a sheet; which way
is out is voted at the first section.  `gallery/Local/marine/a-boat-at-the-jetty.igl` lofts a clinker hull from
seven stations of three shapes, and oars from a ring into a blade.
Rigging: a `catenary { from to sag | length }` (`Geometry/Catenary.cs`) is a SPLINE value -- cubic Hermite pieces of
y = a cosh((x - x0)/a) + c in the upright plane through its ends, even in arc length, one per 8 degrees of turning
and at least 8 -- named, or written where any spline goes (`spline catenary { }`, read by `SplineNamedBy`), which
is how a primitive hangs one: its body holds only what it gives back.  `rope` (`Geometry/Rope.cs`, a Group) lays
`strands` touching helices, r sin(pi/n)/(1 + sin(pi/n)) each, about a spline in rotation-minimizing frames, 16
straight `TubeSegment`s a `lay` (7 radii unless said); `chain` (`Geometry/Chain.cs`) stands one shared `Tube`
stadium link (`link` long, `wire` link/5.5, `width` 3.5 wires) every link less two wires, along the chord,
alternating the frame's normal and binormal.  A `tube` takes `spline S radius r` and a `strand` `spline S width w`
in place of points (`StrandSet.Add(pieces, width)`, the spline's own curves; a 4 mm telegraph wire as a tube came
out a row of dashes at 20 m, as a metal strand a faint whole line).  Lex keeps no tag on a sequence written in
place among alternatives, so the tube's and strand's spline entries are told apart by their text.  The `rigging`
library's `MooringLine` draws its eye round a bollard's waist (0.74 h up, 0.155 h round) in its own frame, the boat
along +X, and turns it into place.
A closed `HeightField` has walls down to a floor and an underside there (`BaseUnder`): nought, or for a function's
ground below nought, a hundredth of its height under its lowest point.  Until 2026-11-01 it was always nought, a lid
over any ground below it: a lake bed showed through ripples at the water line, and Tatooine's wheel tracks, cut 3 cm
into a disc of sand whose top is 2 mm above nought (the desert dipped 6 cm under it), were 2 mm deep, thin lines.
A `SmoothTriangle` (an OBJ with normals, a `smooth` height field) sets every ray leaving it on the side its normals
face off from about where its round surface would be (`Surface.RiseAt`, added to `Intersection.LitPoint` -- so
`MirrorPoint` too; Hanika 2021, "Hacking the Shadow Terminator": the place carried up onto each corner's tangent plane,
weighed by its barycentric, only ever up, never along a corner's normal facing behind the flat triangle).  Set off from
the flat facet, a shadow ray at a graze went into the mesh and the edge of the light across a coarse ball was a
staircase of facets (`TestACoarseBallIsLitWhereverItFacesTheLight`: 496 of 19548 lit-facing places dark on a 12-around
ball; with the light on the mesh's symmetry no facet straddles the edge and the test passed without the fix).  It is
worked out IN THE WORLD (corner offsets by `DirectionToWorld`, normals by `NormalToWorld`): a stretched height field is
steep in its own space (the dunes 54x), a crest's normal lay nearly flat there, a place risen along it slid under the
slope beside it, and the crests were edged with black.  The rounding is Phong tessellation's: on a ball it lies about
as far outside as the flat facet lies inside.
A smooth triangle is met from the side its FLAT face is met from (`Surface.FacetAt`, the facet facing the way its
corners do together, compared IN THE WORLD -- in a 9 km by 1 m field's own space every facet and normal lies on its
side and the facet said no side; none when they lie near its plane, as the book's example's do): where its shading normal faces
away from the eye on a facet facing it -- an eroded crest seen at a graze -- the normal is laid square to the eye, a
hundredth of the eye's way added (`Intersection.PrepareUsing`).  Taken as met from inside, the normal turned into
the ground and the place was lit as the hill's underside: ambient-only speckles along every smooth crest.
A height field's `erosion { rivers t  scree t  repose a  rain n  sea level h  with seed s }` (`Geometry/Erosion.cs`,
the settings and a cache of the last four grounds worn, keyed by a hash of the heights and every setting, so an
animation wears once; the work in `Geometry/ErodingGround*.cs`) wears the sampled grid in the field's WORLD size (its
X, Y, Z through its transforms and groups, `HeightField.SizeInTheWorld`; not an instance's) before any triangle,
in that order: rivers -- stream power, Braun-Willett implicit, mouths first in Barnes' priority-flood order (a hollow
filled a trillionth of the relief a step, `Rise`), water shared among lower neighbors by slope squared, rates from
areas and lengths in the world (reach x sqrt(A share) x width / run), a lake's bed (under its filled level) left
alone, outlets the edge and the sea; then valley sides fall (`LetValleySidesFall`, a heap from the lowest) to no
steeper than the angle of repose or what the slope was before any river cut (exempting crags from falling at all
raised walls one point wide); scree -- two layers, bare rock steeper than repose weathers a twentieth of its excess
a round into loose rock (covered by a step's worth, no weathering), loose slides by Jacobi rounds (0.45 of the most
excess), time x points-across rounds, settling until a hundredth of a repose step or as many rounds again (it creeps
like heat along a bar, and never reached a millionth of the relief); rain -- Beyer/Lague drops (gravity's sign
fixed), on its OWN grid 512 along the longer side whatever the field's (on the field's grid a finer one wore deeper,
narrower gullies), heights normalized to the relief, scaled from Lague's 256 by fineness, NOTHING worn below
where the drop goes next (unfloored, holes deepened themselves to 50000x the relief), and a drop reaching a river (a
place draining a hundredth of the land) carried off with its grit (laid down, three drops a point buried an alpine
valley hundreds of meters deep); what it wore laid back averaged or interpolated.  Maps kept (`ErodedGround`): `flow` (MFD Holmgren p=4 over the filled level, a lake's every point the
water leaving it, (share of the land)^(1/4)), `deposits` and `wear` (1 - 2^(-depth / mean of those nonzero)),
read by `ErosionPattern` on the field found up from `Pigment.Wearer` (`HeightField.MapAt`, bilinear in its square);
an uneroded field's flow is worked out when first asked.  Rivers, scree and rain each wear a 129-point grid within
a quarter of a 65-point one (`TestErosion`).
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
"inside" and was closed at its box, and shadow rays met the phantom (a Julia set came out peppered).  A march that
STARTS inside (the shape reaching its box there) records the box face as its entry, as one ending inside records its
exit: a Mandelbox reaches its box on every face, and three rays in five toward it had odd crossings.  Both box crossings
face as the box's face does (`AtTheBox`, a `PrecomputedNormalIntersection`): facing as the field slopes, a ball cut
by its `bounded by` was shaded as a cone with a dark line either side of its middle.  Beside `julia`:
`mandelbulb { power iterations detail }` (`Geometry/Mandelbulb.cs`, White-Nylander triplex power, axis Y, ball of
two), `mandelbox { factor fold inner radius outer radius iterations detail }` (`Geometry/Mandelbox.cs`, Lowe's box and
sphere folds; `factor` because `scale` is the transform; reach 2 fold (s+1)/(s-1) above one, 2 fold below minus one,
sampled; escape at max(10, 2 reach)) and `menger { iterations }` (`Geometry/MengerSponge.cs`, Quilez's sponge distance:
true outside the cube, a lower bound in it).  The bulb and the box stand off their sets at `detail` (field = estimate
- detail, unescaped points inside; the box's |z|/|dz| has no sign of its own; their boxes grown by four details, or
the box cut the standing-off surface flat on every face) with normals over half the detail
(`DistanceEstimatedSurface.NormalStep`): drawn at the set's exact edge after N rounds, or with normals over eight
accuracies, both came out a speckled dust, every pixel's facing and shadow at random among crinkles far finer than it.
An isosurface's and
`swells`' interval march (`March`) narrows a span whose ends differ in sign only once it is under 64 accuracies
(`StraddleSpan`), else subdivides it as any other: narrowed at once, a span holding three crossings recorded one, and
a ray skimming a swell's crest went through it (complete over cheap, the user's call 2026-10-24: 4.5-9x on those scenes).
Every surface sets a ray off from itself by `Surface.SelfOffsetAt(time, portals)` (`Intersection.SelfOffset`, the
nudge for over/under/lit points, weathering's looks and subsurface steps): the usual epsilon, or for a surface found to
an accuracy in its OWN units (isosurface, sdf) that accuracy times `StretchInTheWorld` -- scaled up a thousandfold, a
shape found to a ten-thousandth in its own units is a tenth off in the world, and an absolute nudge put rays inside.
Whatever the surface, the nudge is never under 2^-40 of the crossing's largest world coordinate
(`Intersection.FarShare`; it passes the millionth only past 1,100 km, so ordinary scenes are untouched): a place far
off is rounded by a share of how far off it is, and a planet ten times the Moon's distance off came out speckled with
its own shadow.  A `Sphere` is solved without cancelling (Ray Tracing Gems ch. 7: the discriminant from the ray's
nearest approach to the middle, the near root from the product of the roots): the book's b^2 - 4ac lost the square of
the ray's distance in radii, and the Earth seen from the Moon was speckled the same way.  A faulty bound (the smaller sampled value) lit
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
as a color for emissions and pigments. A light's `lamp 'name'` (`Light.SetLamp`, `Core/Lamps.cs`) carries a named
lamp's spectrum the same way: the CIE 015:2018 tables of fluorescents, high pressure sodium and metal halide, and LEDs,
embedded unchanged from `Data/CIE` (CC BY-SA 4.0, see its README; `LogicalName` `Lamps.<file>`), each 5 nm table
joined by straight lines and averaged per band; low pressure sodium and low pressure mercury from NIST's strong-line
tables, each line's intensity spread over its band (NIST's mercury is a low pressure discharge, blue-violet: a mercury
street lamp is not offered).  Everyday names and the CIE's codes (F1-F12 aliases), case-blind; `temperature` beside
`lamp` is refused; `lamp('name')` is the color.  Their CCTs by McCamy land within 2% of the CIE's (`TestLamps`).
`context { white balance T }` (`Graphics/WhiteBalance.cs`, a Bradford adaptation applied to each finished pixel in
`Camera.Render`) makes a glow at T come out white, or a named lamp's light (`white balance 'neutral white LED'`,
`RenderContext.WhiteBalanceLamp`, its color made as bright as white so exposure stays put; `--white-balance` T
replaces it); lamp-lit scenes use it so real bulb temperatures read warm rather than orange.  Every lamp lends ambient
through walls, so bays lit apart need `scale ambient by 0`. A material may say `fresnel`
(its `reflective` is then the square-on share, rising toward a graze) or `roughness` (0-1, GGX facets in
`Core/Microfacets.cs`): a rough surface takes its highlight and its blurred reflection from the same facets,
with Fresnel implied, looking in `reflection samples` directions at the first rough hit of a path
(`Scene.GetRoughReflection`) and one thereafter.  Every opaque rough lobe -- a lamp's highlight (plain, brushed, coat), the reflection rays
(`LookedAtRoughly`, so regularized mirrors too), fine wires, caustic photons -- is multiplied by Turquin's
compensation 1 + F_avg (1/E - 1) for what the facets mirror among themselves (`Microfacets.Compensation`/
`CompensationFor`): E from `Microfacets.Albedo`, a 65x65 table in (sqrt cos, roughness) for THIS model's separable
masking, by visible-normal quadrature crowded toward the disc's edge (even, a narrow lobe wobbled 0.5%), built once
(~0.5 s); brushed, `Facets.AlbedoFor`, a table per (along, across) pair over height and angle from the grooves (the
mean lean kept 0.71 at roughness 1); F_avg `Material.ReflectanceFromAround` (Schlick: F0 + (1 - F0)/21),
`BareMetal.MirrorsFromAround` per band, `GlassReflectanceFromAround` for coats.  In a white furnace a single
mirroring kept 0.69/0.42/0.31 at roughness 0.7/0.9/1.  A smooth coat's point-lamp glint is left alone; an area
light's widened highlight is made up for at the widened lean.  Rough GLASS (reflection fan, refraction fan with its
aimed glow, a lamp's highlight and glow through it, photons) is multiplied by 1/E, `Microfacets.GlassCompensation`:
E = mirrored F G1 + passed (1-F) G1 of the bent way, exactly as `Bend` counts them, tabled 33x33 at steps of 5% in
ln(n2/n1) and read between (only the ratio counts; a table per index pair met hung a spectral render's caustics, every
photon's wavelength making one).  Met once, coming OUT of 1.5 glass kept 0.76/0.42 at roughness 0.7/1, going in
0.97/0.90; a frosted ball in white now 1.00-1.02 (`GetColorFor`'s depth defaults to 4: pass one). Lights a ray can meet (`Light.CanBeSeen`) are not counted
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
in front of it -- unless the container (or an instance it stands in) casts `no shadow`: a shadow ray from OUTSIDE
never charges a container's medium, so charged inside only, ground under a glow's shell came out darker inside it
than out, with an edge (until 2026-10-30); so air over a planet's ground must cast shadows.  Every medium helper finds its container's edges and density where it stands, the ray carried into
its parent's space (`IntoTheSpaceOf`) through its portals (`Intersection.Portals` leaving it,
`Intersection.ContainerPortals` inside it); until 2026-10-19 a medium in a moved group found its edge where the
shape would be unmoved.  No scene's instance holds a medium even so: `Instance.MayBeShared` refuses one, and a glow
must refuse, since a `VolumeLight` is made once per surface, not per placement as `LightPlacer` sets out lamps.  A medium that says no `samples` or `bounces` takes the context's
when the scene is settled (`Medium.TakeTheScenesCounts`), wherever the context is written.  A glow's
power takes its parents' scale (`VolumeLight`: the stretch is |DirectionToWorld(x) . (y x z)|); its smooth (Phong)
highlights take each sample's delivered light (`HighlightsBySample`; until 2026-10-24 the light's own white, unfaded:
a fire put a gray sheen across a polished floor); and a medium lit by
a light of several samples picks the sample per point (`Scene.ShiftFor`, salt 141), not the same few for every place.
`emission by density [map]` (`Medium.EmissionByDensity`, a `PigmentSet`) reads a medium's light from the map at its
shape's RAW value (`ShapeAt`, before `Density` scales it), held under one (a map wraps its values round), the march
handing over the density it has (`EmissionAt(point, density)`, so the shape is not asked twice); refused for an even
density, the last emission said wins.  A thick glow shows emission over extinction, density cancelling: with a
pigment by place a fireball's billows showed only at its thin edges.  The `explosions` library builds on it:
`Explosion`/`Debris`/`Sparks(size, at, where, variant)` read `time` against `at` (a library's scope is a child of the
scene's, so it sees the frame's clock), one box shell per blast (a sphere narrows to nothing at its ends), and a
fireball never `gives light` while it scatters (a scattering glow lit by
itself, one place per march step, comes out grain), so the smoke left is the fire gone out, in the same medium; a blast in space, leaving nothing, is an empty group once burned out (kept, its shell swelled past the camera: 40 s a frame for nothing).  A material's `film { thickness a [to b by <pattern>]  ior n }` (`Core/ThinFilm.cs`) is a skin
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

A material's `glints { size s [roughness r] }` (`Core/Glints.cs`) counts its facets under each pixel rather than
averaging them: in `Light.ApplyPhong`, per lamp sample, the GGX lobe is multiplied by k/lambda, lambda = footprint area
(world, `Intersection.Footprint`) / s^2 x D(h) (n.h) x pi c^2 -- c half the lamp's angular radius, the sun's 0.00465
for a lamp with none (`SpreadOf` gives area lights theirs) -- and k a Poisson draw (summed below 64, normal above)
hashed from a cell of a grid in the material's ANCHOR space (`Material.WorldToAnchor`, the footprint carried too for
the scale) as fine as the footprint, levels log2(side / s) blended, and the half-vector's slope cell (2c wide, in
`Microfacets.FrameAround(normal)`).  Lambda >= 10^4 or no footprint gives 1 (smooth); the average is the smooth lobe
(`TestGlints`).  On a ROUGH material it scales the rough highlight (not brushed: `anisotropy` + glints refused); on
any other it ADDS grains of its own (alpha = r^2, 0.5 unless said; Schlick 0.04), lamps a ray can meet giving none.
Direct light only: reflection rays, bounces and photons stay smooth.  The libraries' eleven snow materials and
`Weather.Snowy` carry `glints { size 0.0008  roughness 0.7 }` in place of a Phong highlight (`Weather.Stripped` drops
glints with coats and sheens), and the vehicles' blue, green, sand and gray are metallic flake paint (rough metallic
base, `glints { size 0.00004 }`, `coat { }`); red and white stay solid.

A material's `metal 'gold'` or `metal <color> [edge <color>]` (`Core/Metal.cs`, `Core/Metals.cs`, `MetalResolver`) is
a conductor: a complex index per band -- ten named, from refractiveindex.info (CC0): Ag Al Au Cu CuZn by way of pbrt-v4,
Cr Ti Johnson-Christy 1974, Ni Pt Rakić 1998, Fe Querry 1985, each the set whose square-on share matches published
values (zinc left out: no set in the database holds up in the visible) -- or Gulbrandsen's from a face and edge color
band by band (pbrt's n = 1 with no edge).  It mirrors by the exact unpolarized conductor Fresnel against the index
outside it (`N1`; under a coat the coat's, at the cosine bent into it, `Coat.Within`), tabled over 1024 cosine steps
per outside index as 32 bands and as RGB (`SpectralColor.ToColor`, clamped: gold's red lies past sRGB).  `metal` sets
`Metallic` 1, which for a material with a `Metal` is how much of the surface is BARE (`BareMetalAt`: Metallic x (1 -
decal coverage), the decals `Painted` over transparent): bare metal shows nothing of its own (`ownShare *= 1 - share`:
ambient, diffuse, emission; the bounce and caustic gathers skip a place with no own share left), and mirrors and
highlights by `BareMetal.Mirrors` = share x F + (1 - share) x the rest's reflectance, per band (a rough highlight worked
out with R = 1 and charged at eye.halfway after; `LookedAtRoughly`'s `tinted`).  POV's tint is skipped wherever there
is a `Metal`.  `MaterialBlend.Mixed` mixes bare shares and weighs the rest's numbers (pigment, ambient, diffuse,
emission, reflective) by how much of each side is NOT metal, so grime over brass is the grime's own; two metals mix as
`Metal.Mixed`.  A film on a metal lies on its index band by band (`ThinFilm.InnerFaceOver`); photons turn at a metal
by its RGB share (spectral photons take it by `Hue.Of`); a metal strand is a wire (`Metallic > 0`).  `transparency`
beside `metal` is refused at parse.  Scenes that never say `metal` are pixel-identical.

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
In a scene with bounces, every surface whose material glows (`emission`) is also ONE light, `EmissionLight`
(`Core/EmissionLight.cs`, made by `Scene.LightTheGlowingSurfaces` after LightPlacer, since bulbs -- light housings --
are left out, and so is anything `no shadow` and strands; walked through instances per placement, dressed by
`MaterialThrough`): a sample picks one of the BALLS round the glowing surfaces by brightness x the solid angle it fills
from the point (normal-free, so a reflection run's odds can be worked out from its origin), a direction even over
that cone (the whole sphere from inside the ball).  The balls are round each of a surface's `Surface.Casings` (its box;
a `Sphere`'s is round, and standing even in the world is its exact ball; a tube segment lays boxes ALONG itself,
`Geometry/Casing.cs`, a curve's eight by de Casteljau) carried into the world and cut along the longest edge into
pieces no longer than twice the next (at most 256: a 3.4 m LED strip capped at 64 lit its wall in grain): one ball round a slanted neon stroke was found by 19 looks in
1024 and the wall behind, inside it, came out grain; 254 now.  A direction's odds are brightness x the number of
the surface's balls holding it / total (a ball's share over its cone is the brightness; `OddsAlong`, `Holds` by the
ray's miss distance, never counting a ball behind).  It finds the surface ALONE along it (`Scene.FindTheGlow`: the outermost
CSG/IAssembled it is part of, its first crossing must be the surface), and carries `GlowSeenAt` there -- the very
reckoning `HitColorOf` gives a ray meeting it (`GlowGiven`, transparency included) -- over pi x the odds; a miss has
Cone 0, which skips its shadow ray (`ReachingFrom`, for spotlights too).  A bounce's straight run finds a held glow
black (`GlowShareOf`), and a rough lobe's run shares it by the balance heuristic with `SampleCount x Odds`
(`LobeShareOf`/`ShareOf`), as a `VolumeLight`'s.  No photons (`BeamsAt` skips it), so a bounce's straight run now
carries on through any glass a shadow ray passes straight, caustics or not (`StopsTheShadowRay`).  Without bounces
there is no such light and a glow lights nothing (lacquer-and-varnish's softbox is "not a light" on purpose).
`context { emission samples N }` (16).

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

A group's tree of boxes (`BoundingVolumeHierarchy`) is cut along the axis its children's middles spread most along IN
THE WORLD: each axis weighed by how long it comes out through the group's transforms and parents
(`Group.StretchOfItsSpace`, null when even).  A height field said in meters up over its unit square, stretched 9 km
each way, spread 3000 of its units up and one across, every box was a slab as wide as the land, and dropping 34
thousand firs onto it took four minutes (3.7 s after; rendering ~20x faster).

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
convolved by `Basics/Fourier.cs` (radix-2, grid twice the picture's sides) as (1-g) + g x pattern.  A perspective
camera's lens character, all off by default (other projections warn and ignore it, `Camera.Unlensed`): `distortion k`
stretches the place each pixel's ray goes through on the unit plane by 1 + k r^2 (r a share of the half-diagonal; the
ray's spread by 1 + 3k r^2), refused at -1/3 or less (the corners fold); `vignetting v` blends cos^4 off-axis into each
finished pixel (`PixelToRayConverter.FalloffAt`) and, with the aperture open, stops lens samples outside a second unit
opening standing off by v x the place / half-diagonal -- the barrel's cat's eye (`Passes`; `PixelRenderer.Trace` skips
them as no light, coverage averaged over those passed); `chromatic aberration c` resamples the finished canvas's red at
1/(1 + c/2) and blue at 1/(1 - c/2) about the middle (`Core/ChromaticAberration.cs`), before the glare.

Denoising (`context { denoise [intel | filter] }`, `--denoise best|intel|filter|off`, `RenderContext.Denoise`, a
`DenoiseMethod`; off by default) smooths the finished canvas's light in `Camera.Render` after the scan and BEFORE
chromatic aberration and glare.  `Denoiser.For` settles it before the first pixel: `best` is Intel's when
`oidnDenoise` is on the PATH (`IntelDenoiser.ToolOn`, as `MovieFile.FfmpegOn`), else the filter, said once a run;
`intel` without the tool is refused.  Both are guided by `Core/Denoising/Guides.cs`, gathered after the scan with the
render's statistics set aside: a pixel's middle without anti-aliasing, a 4x4 grid with, each place taking the lens's
and shutter's samples in turn (`max(16, lens)` looks), each look `Scene.GuideFor` (`Core/Scene.Guides.cs`): the first
hit's settled material (`SettledFor`, no regularizing) -- pigment held to [0, 1], bare metal's square-on mirroring
plus the rest's paint -- normal and place; a SMOOTH surface whose `SharesOf` mirrors + passes reach a half is
followed (mirrored from `MirrorPoint`, bent by `Bent`, total internal reflection mirrored), each way weighed by its
share and tinted (metal's hue, the filter tint), 4 deep, ways under 1/32 dropped; fine strands laid by coverage; the
sky its color held to white, met nowhere.  Nothing in them is drawn by chance, so Intel's is told `--clean_aux`.
`IntelDenoiser` writes light, albedo and normal as little-endian Portable FloatMaps (`ImageIO/PortableFloatMap.cs`,
rows bottom up) to a temp folder, runs `oidnDenoise --hdr`, reads it back keeping alpha; a failing tool is refused
with what it said.  `WaveletFilter` is SVGF's spatial à-trous (5 passes, 5x5 B3 kernel, steps 1-16) on light divided
by albedo (floored 0.05), each tap weighed by facing (cos^128), place off the pixel's tangent plane against 2 x
footprint x pixels apart, albedo apart relative (spread 0.25: a slab and its mortar never mix, or what the paving
mirrors is shared out wrongly), and brightness against 4 sqrt(grain), the grain carried through passes as sum w^2 var /
(sum w)^2.  Grain is measured from the picture, not SVGF's 7x7 moments (which took a shadow's edge for grain): each
pixel against the median of the same-surface 3x3, neighbors taken in mirrored pairs (else a ramp at an edge was
grain), squared, averaged over the same-surface 7x7 with the largest 6% left out / 0.665 (the median kept a
sprinkling of dark specks one pixel in five; the mean took a wet road's mirrored lamps for grain).  Pixels more than
half sky are left alone.  A plain sky light's fixed directions make steps, not grain: no denoiser smooths them.

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
(`since` < 0: already in full flow at nought; only copies within the longest life of the time are looked at) -- or,
`burst n [at t] [over d]` in place of rate and since (refused beside them), copy k of n born at t + d k / n, a flow at
n / d that stops at its last copy, every one at t when d is nought -- its way, speed and life hashed from (seed, k, purpose) -- never drawn in order -- flown from its birth at 1/500 s, to the
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
footprint straddling the horizon averages in the gradient's repeat below it.  `image 'f' [map] [channel c]` is a
PATTERN too (`Patterns/ImagePattern.cs`; wherever a pattern goes -- `normal`, `material blend`, a pigment's map, field
patches, `roughness`/`metallic a to b by`): read RAW (`TextureFor(false)`) as a height field reads it (`Color.AsGray`)
or one channel, held under one (a map wraps one round to its start: white read as black), filtered over the
footprint, laid as an image pigment by the shared `ImageLaying`, where anything whose Parent is a `HeightField`
defaults to `ImageMapType.Field` (texel i's middle at i/(n-1), the field's heights' own places; a planar map put each
half a texel in) and no wearer is planar.  A roughening picture's wearer is the surface it roughens (`Surface.NormalAt`
holds `Pigment.WornBy(this)` round every picture-reading `PerturbAt`): until 2026-11-02 it had none, so a normal map or
picture bump naming no mapping was laid planar round a ball and half a texel off a height field's heights.  Its slope for `normal` is `Pattern.SlopeAt` (null for arithmetic patterns,
which keep the 0.001 gap): a texel either way ALONG THE PICTURE'S OWN AXES (steps from `RatesAt` by the 2x2
inverse), widened to the ray's patch (`Surface.PatchAt`; `FootprintRadius` is set before `NormalAt` for it) -- at
0.001 each texel showed as a facet, and one isotropic widest-texel gap found no slope on a one-texel-high picture.
`normal map 'f' [map] [directx]` (`SurfaceNormal.NormalMap`, `GreenDown`): RGB in the frame of the picture's rates
laid flat (red across, green UP = minus the rows' way, OpenGL), `depth` scaling the tilt.  `roughness a to b by
<pattern>` and `metallic a to b by` (`Material.RoughnessBy`/`MetallicBy`, a `MaterialMap`) are read per hit in
`Scene.SettledFor` and `MaterialBlend.Settled` (`Material.MappedAt`: 256 steps cached per material, forgotten by a
copy; a smooth end Splits as a blend does), so a block laid on later keeps them; beside `metal`, a metalness map is
how much of each place is bare.  `ImagePigment.MayTransmit` is the picture's own (`Texture.IsOpaque`, or `once`),
read when resolved (`Load`): until 2026-10-31 every image said it might, so `MetalApplies`, `CoatApplies`,
`SheenApplies` and `FresnelApplies` were false for a picture-colored material, and a rough one defaulted to
`reflective 1` and was shaded as glass of index one -- mirroring nothing.  An image takes `{ brightness b  <transforms> }`
after it (parsed by hand in `ParseImagePigmentClause`).  Two layings read a picture more than once at a place
(`Pigments/ImageTaps.cs`: up to nine `ImageTap`s, each an affine map from the point to texels -- place, rates across
and down, so footprints, bump slopes and normal-map frames all come from it -- with a projection share and a tile
share), for image pigments, `image` patterns and normal maps alike; every other map keeps its one-place path, so
scenes not saying them are bit-identical.  `triplanar [blend b]` (`ImageMapType.Triplanar`) lays the picture square to
each axis, weights |n|^(1/b) normalized (b 0.25), upright on the sides, unmirrored on the far ones, at the WORLD'S size
along the surface's own axes (the user's call): a `Bearing` (`Pigments/Bearing.cs`, thread-static) holds the surface's
own axes as they stand in the space being read, their lengths in the world and its normal in its own axes -- set by
`Material.GetColorFor` (only when `PigmentSeesTheFacing`), `MaterialBlend.PatternValueAt` and `Surface.NormalAt`, and
turned by every pigment transform in `Pigment.GetTransformedColorFor` and the bump's and blend's own, since a picture is
handed a point already carried through them; the picture's map is A S B (A the axes, B its inverse, S the stretch).
No facing (a strand's root) takes the three alike; a background is refused.  `shuffled [size s] [turned a]` after
`planar` or `triplanar` is Mikkelsen 2022's hex tiling: an equilateral grid of tile middles s apart in pictures (default
1/(2 sqrt 3)), each shifted and turned (uniform within a either way) by `DrawFor(seed, column, row, purpose)` from the
surface's seed, weights (barycentric)^7 normalized, colors leaning to the brighter by lerp(1, luminance, 0.6) per
projection; normal maps and slopes take the geometric weights, so a texture set laid alike on one surface shifts tile
for tile.  A spread normal map tilts per tap in that tap's frame and blends the tilted normals; a spread bump's slope
is the weighted sum of each tap's texel-space slope.  A sky light carrying an image (`SkyLight.IsAPicture`) is
image-based lighting: half its samples are picked by a `SkyMap` (luminance on a grid twice the image's width, at most
4096, read at cell middles) and half evenly, each counted by the balance heuristic over both (`SkyLight.OddsToward`),
and the pattern is turned per point (`Scene.ShiftFor`, salts 107/108) so a cloudy sky is grain, not blotches.  When it
carries the shown background untinted (`Scene.SharedSky`), a rough surface shares it with its reflection, direction
by direction (`HighlightShareOf`, `SkyShareOf` on a `LookingRun` with a `Mirror`).  Pattern skies keep the even
fixed pattern.  A surface marked `portal` (`Surface.OpensToTheSky`; the cube a window is cut from a wall with) is an
opening the sky comes into a room through: `Scene.OpenTheSky` (after LightPlacer and the glowing surfaces) finds every
one per placement, takes the middle of its TRUE box between its two widest sides (refused: empty, endless, sheared,
or no sky either side -- which side is found by 64 shadow-rule looks out of each face) and hands them to every sky
light (`SkyLight.Openings`, `SkyPortal`).  At a point on the room's side of any opening with light beyond it
(`SkyPortals.Faces`), three in four of the sky's samples (`SharesAt`; all of one) go THROUGH the openings
(`SkyPortals.Pick`, salts 151/152): an opening picked by the sky's light through it, then a direction by Bitterli 2015
via PBRT's `PortalImageInfiniteLight` -- the sky re-mapped per wall frame by (atan2(x,z), atan2(y,z)), so an opening
from ANY point is an axis-aligned window of one map (`PortalMap`: a summed-area table read bilinearly, exact for even
cells, halved across then down the column; weight = brightness x pi^2 (1-x^2)(1-y^2)/z; 128 cells for a pattern sky,
each the mean of 16 reads -- read at middles, a 4-degree bright patch came out an eighth dark -- a picture's
clamp(texels, 128, 1024)); openings in one wall share a map.  The rest are looked at as anywhere (a picture half by
its map), every one TURNED per point (fixed, the rare even sample through a window left a 1% window-shaped step),
all counted by the balance heuristic (`MixedOdds`).  PBRT's portal is exclusive (no sky behind it); ours never is,
so an unmarked door, or the roof outside, is still lit rightly.  A point facing no opening is looked at as before,
bit for bit.  A sky with openings is shared with rough reflection as a picture is (`SharedSky`; `OddsToward` takes the
point).  Caustic photons from the sky (`PhotonSample`) ignore openings.  A test room (one window, 24 samples): RMS a tenth of
before, the window-shaped stamps gone.  A smooth highlight from ANY sky light takes each sample's own light (`Light.HighlightsBySample`); until
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
