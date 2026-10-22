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
its own); `apart`, `tumble`, `hole`, `materials A, B` per piece.  Uses: rocks `DryStoneWall`/`Rubble`, roads
`CrazyPaving`, windows `Sash`/`Casement`'s trailing `broken` count (unbroken is the old surface list, pixel-identical).  A `Field` fills a 2D outline in the X/Z plane on a jittered grid with copies of one or
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
`Matrix`, `Ray`, `Interval`, `Transforms`, `NoiseGenerator`, `Polynomials`). `Core/` has scene-
graph level types: `Scene`, `Camera`, `Material`, `PointLight`, `Intersection` and its
precomputed variants. `Graphics/` holds the 2D path/curve primitives (`GeneralPath`,
`Line`, `QuadCurve`, `CubicCurve`, `TwoDPoint`/`TwoDVector`) used by extrusions, lathes and
text layout to describe outlines in 2D before they're revolved/extruded into 3D surfaces.  An extrusion's wall
segment skips a ray whose 2D line has every one of the segment's control points strictly to one side
(`ExtrusionPathSurface.AllToOneSide`, the hull property: same crossings, 45% less time on a 13-segment body filling the picture).
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
in front of it.  Every medium helper finds its container's edges and density where it stands, the ray carried into
its parent's space (`IntoTheSpaceOf`) through its portals (`Intersection.Portals` leaving it,
`Intersection.ContainerPortals` inside it); until 2026-10-19 a medium in a moved group found its edge where the
shape would be unmoved.  No scene's instance holds a medium even so: `Instance.MayBeShared` refuses one, and a glow
must refuse, since a `VolumeLight` is made once per surface, not per placement as `LightPlacer` sets out lamps.  A material's `film { thickness a [to b by <pattern>]  ior n }` (`Core/ThinFilm.cs`) is a skin
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
steps; a blend's own properties are its FIRST material's for whatever asks without a hit (shadows, photons);
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
the sky light and volume lights count those directly; all along a bounce path (`_bounced > 0`) what a shadow ray
passes -- `no shadow` stand-ins, light housings -- is passed (`PassedByBouncedLight`), and surfaces show no highlight
(`ApplyPhong(highlights: false)`), glossy-to-matte light being the caustics'.  A lamp at a bounce point gives one sample
picked per point (`Illuminate`); bounces do not spend the mirrors' `remaining`.  Asking for bounces scales EVERY
ambient to 0, a library's named ones included; what glows of its own says `emission` (`Material.Emission`, a number or
color tinted by the pigment, the same under any lamps, unlike ambient which each lamp lends).  An opaque closed surface
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
floor took the sun unfaded by the water over it), else the surroundings.  At a fork the greater way always goes
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
`AliasingOption`). Output goes through `ImageIO/` (`ImageFile`, `ImageCache`,
`ImageReference`) which wraps SkiaSharp/Magick.NET for reading/writing image formats.

The camera's response, from light to picture: a camera's `exposure` (stops; `Camera.Exposure`, or `--exposure` as
`RenderContext.Exposure` over every camera) scales each finished pixel in `Camera.Render`, after white balance, so the
canvas holds exposed LIGHT.  A picture file is developed only when written: `Color.ToChannelValues` takes
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

### Pigments and patterns (`Pigments/`, `Patterns/`)

Pigments (`Pigments/`) define how color is derived at a surface point (solid, pattern-based,
image-mapped, blended/layered, noisy). Patterns (`Patterns/`) define the underlying spatial
functions (stripes, checker, gradient, agate, wood, granite, brick, hexagon, etc.) that
pigments sample. Each has a corresponding DSL resolver under `Instructions/Patterns/` and
`Instructions/Pigments/`.  An image pigment reads a `Texture` (`Graphics/Texture.cs`, cached per file in
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
