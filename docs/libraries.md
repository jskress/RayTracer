## Using Libraries

A **library** is a file of ready-made, named definitions — materials, pigments, interiors and
the colors and numbers they lean on — that a scene may draw from without carrying the
definitions itself.  A scene reaches into one with `import`:

```
import 'golds' { Gold3CMaterial }

sphere { material Gold3CMaterial }
```

Where [`include`](scene-files.md#including-other-files) brings in everything a file has, an
`import` brings in only the names you list and leaves the rest of the library out of scope —
which matters, because a library may hold a hundred materials and a scene will almost never
want more than a few.  The `import` statement itself is covered in
[Scene Files](scene-files.md#importing-from-a-library); this chapter is about the `libraries`
verb, which makes, lists and removes the libraries you import from.

The one you are most likely to want holds the textures POV-Ray ships with, converted and
brought across:

![Materials imported from converted POV-Ray libraries](images/figures/lib-textures.png)

Every material in that scene — the woods, the metals, the glass and the stone — came from a
library and was named, not written.  The whole scene is
`gallery/POVRay/library-textures.igl`.

### Libraries That Come With It

Some libraries ship with the ray tracer.  They are not there until you ask for them:

```bash
dotnet run -- libraries --install
```

That copies them into your library set, and says which it wrote.  It is a thing to ask for rather than
something that happens the first time you render, because writing into your home directory unbidden is
a surprise that is hard to undo — and because a verb can be run again after an update, which a
once-only step at first run cannot.

**A library you already have is left alone.**  If a name is taken, the install keeps yours and says so;
`--overwrite` replaces it.  So a sky you have tuned to your liking survives a new release of the ray
tracer, and the shipped ones are a starting point rather than something imposed.

#### Daylight

The one that ships today is `daylight`, and it exists because the sky in this ray tracer is a real
one — it works out the color of every part of the dome from the way air scatters sunlight.  That is
what makes it look right, and it is also what makes it awkward, because the numbers it wants are the
ones nobody can guess.  Nobody knows what turbidity they want.  Everybody knows what a clear morning
looks like.

```
import 'daylight' { GoldenHour, GoldenHourLight }

background GoldenHour
light GoldenHourLight
```

| | |
| --- | --- |
| `ClearMorning` | Mid-morning, sun well up, air washed clean. |
| `ClearNoon` | Overhead and unforgiving; shadows fall almost straight down. |
| `ClearAfternoon` | Mid to late afternoon, sun well down but nowhere near going. |
| `LateAfternoon` | Later still: shadows long, but an hour short of turning gold. |
| `SoftAfternoon` | The same hour with more in the air, and a softer edge to every shadow. |
| `GoldenHour` | An hour before sunset, long shadows and a warmth in the light. |
| `Sunset` | The sun on the horizon, most of its light gone red on the way. |
| `HazyAfternoon` | Hot and thick, every edge softened and the blue washed out. |
| `WinterSun` | Low and cold, bright without being warm. |
| `Overcast` | Flat and shadowless, made rather than derived — see below. |
| `Dusk` | After the sun has gone, what light there is coming from everywhere. |

**Each is two names, and you want both.**  A sky is what you look at; the light it casts is a separate
thing, and taking one without the other gives a picture that disagrees with itself — a scene with no
sky light quietly keeps the flat `ambient` guess that a sky light exists to replace.  So every
`GoldenHour` has a `GoldenHourLight` beside it.

**They may be adjusted where they are used**, the same as any named thing, so a name is a starting
point and not a straitjacket:

```
light GoldenHourLight { samples 64 }
```

**Two of them are not physical skies at all.**  `Overcast` and `Dusk` are gradients, because a real sky
with the sun taken out of it is not the same thing as a cloudy one: cloud spreads the sun's light
across the whole dome rather than hiding it, so the honest way to get an overcast day is to paint one.

**Every one of these skies has its sun on the `+Z` side.**  That is worth knowing before you place a
camera, because the consequence is invisible until you render: a wall, a row of buildings or a face
of anything that points toward `-Z` is in shade under all six, and a camera sitting out at `-Z`
looking back — which is the usual place to put one — sees the shaded side of everything.  The picture
comes out a silhouette, and nothing in it says why.

The sun's direction is worked out from its `elevation` and `azimuth`, and the azimuth runs the way a
compass does with `-Z` for north:

| Azimuth | The sun lies toward |
| --- | --- |
| `0` | `-Z` |
| `90` | `+X` |
| `180` | `+Z` |
| `270` | `-X` |

The stock azimuths run from `110` to `268`, which is the half of the compass between `+X` and `-X`
passing through `+Z` — morning on one side, evening on the other, and none of them behind you.

**So each sky also comes as a `…From(azimuth)`, and that is the one to reach for.**

```
import 'daylight' { ClearAfternoonFrom, ClearAfternoonLight }

background ClearAfternoonFrom(40)
light ClearAfternoonLight
```

The reason there is a whole second set of names is worth stating, because it is a real distinction and
not a convenience.  Elevation is the hour, turbidity is the air and brightness is the exposure — all
three are properties of the *day*, and belong to a sky's name.  Azimuth is not: it is a property of the
**composition**, set by which way the subject faces and where the camera stands.  Welding it into a name
means the name only serves scenes that happen to face the right way, and since every stock azimuth here
faces `+Z` while the [buildings](#buildings) face `-Z` and the [vehicles](#vehicles) and
[trains](#trains) face `+X`, that was every scene in the gallery.  They all wrote their own skies
instead, which is what this section used to advise — and the library stopped growing as a result.
`ClearAfternoon` and `SoftAfternoon` are two skies that came back out of those hand-written ones.

**Which azimuth?**  Point it at the face you want lit and swing it thirty or forty degrees off, so the
light rakes across the subject rather than flattening it.  For something facing `-Z` that is an azimuth
around `30` to `50`; for something facing `+X`, subtract ninety.  A sun directly behind the camera lights
everything and models nothing.

`Overcast` and `Dusk` have no `…From` because they have no sun in them; there is nothing for an azimuth
to mean.  And writing a sky out by hand is of course still there when you want an hour the library does
not have:

```
MyMorning = pigment physical sky { sun elevation 30  sun azimuth 20  turbidity 2.2  brightness 3 }
```

#### Trees

```
import 'trees' { Elm }

object Elm(9)
object Elm(7, 'autumn')          { translate X 12 }
object Elm(8, 'winter', 4)       { translate X -12 }
```

| | |
| --- | --- |
| `Elm` | Reaching, the limbs sweeping up and out. |
| `Oak` | Heavy and broad, throwing its weight sideways. |
| `Birch` | Slender, pale-barked, dividing into finer twigs than the others. |
| `Fir` | A conifer: one trunk the whole height, with rings of branches coming off it.  Evergreen, so spring, summer and autumn are the same tree -- but ask one for winter and snow gathers along its boughs. |

Three numbers, of which only the first is required: **how tall**, **what time of year**, and **which
tree of that kind**.

**Height is what you would measure** — `Elm(9)` stands about nine units high.  That is worth saying
because the obvious alternative is for the number to mean the trunk, and then nobody can picture it.

**The season is a word**: `'summer'`, `'autumn'` (or `'fall'`), `'winter'`, or anything else for
spring.  A winter tree has no leaves at all and shows the shape they were hanging on.

**The variant is which tree of that kind you want.**  The same numbers always grow the same tree, down
to the last twig — today, next year, and in every frame of an animation.  Change it and you get a
different tree of the same species, which is what a row of them needs, since three identical elms read
as a diagram rather than a hedge:

```
for tree in [0, 5] {
    object Elm(8 + random(tree) * 3, 'summer', tree) { translate X tree * 7 - 17 }
}
```

The library keeps its own workings.  A scene that imports `Elm` does not also inherit the dozen
functions and primitives an elm is built from — see
[Importing from a library](scene-files.md#importing-from-a-library) for the rule, and for the one
exception: the barks are materials, and a material is looked up where it is *used*, so `TreeElmBark`
and its siblings do arrive.  They carry the prefix so they will not collide with anything of yours.

#### Undergrowth

```
import 'undergrowth' { Grass, Boxwood, Lavender }

object Grass(10)
object Boxwood(1.2, 'winter')    { translate X 3 }
object Lavender(0.8, 'summer', 4) { translate X -3 }
```

The `trees` library gives a scene its trees and leaves them standing on a flat green plane.  This one
is the rest of it — the grass underfoot and the shrubs between, which is most of what separates a
picture of some trees from a picture of somewhere.

| | |
| --- | --- |
| `Grass` | An area of it, covered edge to edge.  The first number is how far across, not how tall. |
| `GrassCircle` | The same cut to a circle, the first number being its diameter. |
| `Tuft` | One clump on its own, for putting somewhere in particular. |
| `Boxwood` | A dense clipped dome.  Evergreen, so like the fir it takes snow rather than ignoring winter. |
| `Bramble` | Arching canes with leaves along them; berries in autumn, bare canes in winter. |
| `Lavender` | A mound of fine stems, in flower through the summer and cut back by winter. |

**The first three numbers mean what they mean everywhere else** — how big, what time of year, and
which one of that kind — so a scene that has planted an autumn stand can plant autumn undergrowth
beneath it without learning a second set of habits.  Only the first is ever required.

**A square is the wrong shape surprisingly often**, which is why `GrassCircle` exists.  Anything laid
against a circle — a roundabout's island, a pond, a tree's drip line — has a square patch either
overhanging it at the corners or leaving a ring of bare ground inside it, and no size does neither: a
square's corners are always 1.41 times its half-width out from the middle.

The obvious repair is to intersect a square patch with a cylinder, and that does not work at all.  A
patch of any size is hundreds of separate tufts, and a `CsgSurface` — unlike a `Group` — has no bounded
traversal for a shadow query, so every shadow ray walks every tuft with no distance pruning whatever.
Under a sky light a modest patch had not finished rendering after **ten minutes**.  Testing each tuft
against the circle as it is placed costs nothing, which is what this does.

**What a season does differs by plant**, as it does in a garden.  Grass goes tawny and then to pale
straw, and lies down as well as changing color.  A bramble turns, fruits, and finally stands as bare
canes.  Lavender flowers, fades, and is cut back.  A boxwood is evergreen and does what the fir does:
three of its four seasons look alike, and in winter it takes snow.

**Height is the setting that matters most**, and not for the reason you would guess.  A blade of grass
is about a fortieth of its length across, which is right — and it means ankle-high grass seen from
across a field has blades a third of a pixel wide.  A blade thinner than its pixel does not draw as a
blade; it draws as a speck, and a field of specks draws as wire wool.  Knee-high grass in the same
picture reads immediately, and costs *less*, since taller tufts stand further apart.  If a patch looks
like static, make it taller before you make it denser.

**Grass is the one thing here that can make a scene slow**, and it is worth saying so plainly rather
than leaving it to be discovered.  A blade is two tubes, a tuft is a dozen blades, and an area of it
is a tuft every fifth of a unit in both directions — so `Grass(8)` is on the order of twenty thousand
surfaces.  That is a number this ray tracer handles perfectly well, but it is twenty thousand rather
than twenty, and the reason it is affordable is worth knowing: the tufts are gathered into blocks of
sixty-four and the blocks into one group.  A group that a ray gets inside asks *every* child, so one
flat list of two thousand tufts is two thousand questions per ray; two layers makes it thirty-odd.
Measured on one patch, flattening it is the same picture and **four times the wait**.

A [sky light](materials.md) is the other half of any grass bill.  It works by looking at the dome from
many directions at every point it lights, and every one of those looks has to get out through the
grass, so the sample count is most of the render time in a scene like that.  Turning it down where it
is used costs less than it sounds — grass has no large flat surface for the grain to show on.

`Grass` takes two more numbers after the usual three, and they come *after* rather than among them
precisely so the first three keep their meaning:

```
object Grass(8, 'summer', 1, 0.3, 0.5)     // half as many tufts, a quarter the surfaces
```

Halving the last one quarters the count, since the tufts thin out in both directions at once.  Grass
seen from across a field does not need what grass seen from a foot away needs.

#### Rocks

```
import 'rocks' { Boulder, Scree }

object Boulder(1.2)
object Scree(6, 'winter')        { translate Z 4 }
```

The other two libraries grow things.  This one is what they grow among, and it is the last thing a
piece of ground needs before it stops looking swept.

| | |
| --- | --- |
| `Boulder` | One big weathered stone, lumpy all over.  The expensive one — see below. |
| `Cobble` | A smaller stone with flat faces, knocked off something bigger.  Cheap. |
| `Scree` | An area of cobbles, thrown down thickly.  The first number is how far across. |

**The season does one thing here, and it is winter.**  A rock is not deciduous, so three seasons of
the four are the same stone; in winter snow lies on top of it, as it gathers on the fir and the
boxwood.  That is all the word does, and it is asked for anyway so a scene that sets its season in one
place gets rock agreeing with the grass around it.

**There are two kinds of stone for a reason.**  A rock is a shape problem before it is anything else:
what gives one away instantly is a silhouette that is too regular.  A sphere reads as a ball from
every angle and no amount of coloring the surface repairs it, because the outline is what the eye
checks.

So a `Boulder` is an [isosurface](advanced-surfaces.md#isosurface) — a ball with noise subtracted from
its radius, which gives an outline irregular in the way stone is.  That is the real thing at the real
price: an isosurface is *marched* along the ray rather than solved for, and sixty-four of them
measured about twenty times what sixty-four spheres cost.  A `Cobble` is a sphere with flat faces cut
off it by a couple of turned cubes — all analytic, nothing marched — which reads as broken stone
rather than weathered stone, and measured about four times a sphere rather than twenty.

That is the same division `undergrowth` makes between `Tuft` and `Grass`: the careful expensive one
for the few you look at, the cheap one for the many you do not.  A picture wants some of each.

##### What makes a shape into stone

The shape argument above cuts both ways.  A good outline does not save a bad material: these rocks had
the right silhouette from the start and still read as pale plastic, and it took three separate faults
to fix.

**The color map's stops have to sit where the pattern's values actually fall.**  It is natural to
write a map from nought to one, and quite wrong for `granite`, which is a sum of six octaves of
`|0.5 − noise|`.  Measured over sixty thousand points it runs **0.017 to 0.684, mean 0.232, with 99.2%
of it below 0.5** — so stops at 0.0, 0.5 and 1.0 squeezed every stone into the gap between the first
two colors.  An intended spread of 0.26 to 0.49 arrived as 0.28 to 0.36: a third of the contrast that
was written, and all of it in the pale middle.  Each rock came out a single flat tone, which is most of
what made them look like putty.  The stops now sit at granite's 1st, 25th, 50th, 75th and 95th
percentiles, and the speckle arrives.

**Stone is darker than it seems it should be.**  Dry granite returns about a fifth of the light that
falls on it, and basalt less; the old middle tone of 0.32 was concrete.  Under a warm sky a too-bright
gray does not read as pale stone but as pale *plastic*, and it takes the sun's color with it — which is
where the yellow cast came from.

**A gathered highlight is the strongest plastic cue there is.**  `specular 0.10` at `shininess 22` is a
soft collected sheen, and a collected sheen sliding across a smoothly shaded form says *coated*.  Stone
scatters off a surface rough far below the scale of anything modeled, so what comes back is broad and
dim: `specular 0.03` at `shininess 9`.  The ambient came down with it, from 0.16 to 0.06 — a sixth of
the light arriving from nowhere in particular was lifting the crevices to the same tone as the faces,
flattening the very shading that says *rough*.

#### Fire

```
import 'fire' { Campfire }

object Campfire(1.4) { translate [3, 0, -2] }
```

| | |
| --- | --- |
| `Flame` | One flame on its own.  The first number is how tall it stands. |
| `Campfire` | Logs, coals between them, and flames over the lot.  The number is how far across. |
| `Torch` | A shaft with a flame at the top of it. |
| `Embers` | Coals with no flame left, for a fire going out. |

**These take two numbers, not three.**  Everything else in these libraries takes a season second, and
fire does not, because fire does not have one.  A word taken and ignored is worse than a word not
asked for: it says the library thought about the question when it did not.

##### What makes a pile of tubes into a fire

**A fire is built, and how it is built shows.**  The logs here lie two across the bed with four leaning
in over them, bases out on the ground and tips gathered above the middle.  The first version made a
*starburst*: each log was a vertical tube with its base at the origin, tilted and then swung round, and
rotating about the origin holds the base still while the tip swings out — so six logs came out with
their ends meeting in the middle and their far ends radiating like the spokes of a wheel, lying only
28° off the ground besides.  A leaning log is the other way about, so each is now given its two ends
directly rather than rotated into place: it says what is meant, and it cannot swing the wrong end.

**Bark, not a flat brown.**  A log of one uniform color is a dowel — nothing about its surface says
wood, so the eye reads the shape alone, and the shape of a tube is a tube.  The bark is a
[`crackle`](pigments-and-patterns.md#continuous-patterns) pigment, which is a web of cracks between cells and
therefore exactly the right pattern, with its stops set at crackle's own percentiles rather than spread
over nought to one.  Scale matters more than it looks: at cells a third of the log's diameter it read
as a pineapple, and a log wants twenty or thirty cells round its circumference.

**And the coals have to be above the ground.**  `FireBed` is a slab in the *floor* of its shell — its
density only rises above nought in the bottom sixth — so a shell that is scaled flat and then lifted by
half of that scale puts the whole glowing part underground.  `Embers` did exactly that, and rendered
faithfully as unlit dirt.  Being a slab, it is also thin, and what a medium gives back is emission
times density times the distance a ray travels through it: a coal bed crossed in a tenth of a unit
needs several times the density of a flame crossed in three tenths, or it comes out too faint to find.

**Put a fire in a scene and the scene is lit.**  There is nothing to place beside it and nothing to
keep in step: the stuff a flame is made of gives off light where it is, and that light falls on the
ground, casts shadows, and carries the flame's own color with it — see
[a surface that gives light](surfaces.md#a-surface-that-gives-light).

That was not always so.  Until it was, every fire needed a lamp written beside it and moved with it,
and this library shipped `CampfireLight` and three siblings for the purpose.  **They are gone.**  A
scene that used them should delete the line.

**How bright a fire is, is not a setting.**  It follows from what the flame is made of and how big it
is, falling away as the square of the distance.  If a fire lights too much of a picture, the honest fix
is a smaller fire.

**A flame is a medium, so it is walked along**, and what it costs is how many places along each
crossing the renderer stops to ask.  That is scene-wide and the library cannot set it:

```
context { medium samples 120 }
```

Sixty does for a flame across a room; a hundred and twenty for one filling the frame.  Below about
forty a flame goes banded, and the bands are the steps of the walk showing through.

**Two glowing volumes may overlap; three may not.**  Each shell a ray crosses spends one of the four
refractions the renderer allows, and a ray that runs out comes back black wherever the stuff inside is
thin.  That is why a campfire here is one shell whose density describes the coals and all three flames
together, rather than four shells stacked — which is also cheaper, since a ray crosses one boundary
instead of eight.

#### Buildings

```
import 'buildings' { House, Row, Tower }

object House(4)
object Row(5, 'winter')       { translate X 12 }
object Tower(14, 'summer', 3) { translate X -14 }
```

| | |
| --- | --- |
| `House` | One dwelling: plinth, brick walls, a boarded gable, a roof, a doorway and windows. |
| `Row` | Several houses joined shoulder to shoulder, as a terrace. |
| `Tower` | A taller block, bay after bay, with a parapet rather than a roof. |

The three numbers mean what they mean everywhere else — **how big**, **what time of year**, and **which
one of that kind** — and only the first is required.  For a `Row` the first number is **how many
houses**, since a terrace is counted rather than measured.

**These face `-Z`**, which is the way a camera written the usual way is looking.  The windows, the door
and the sills are all on that face; the other three sides are wall.  Turn a building with `rotate Y` to
put its front where you want it.

**The season puts snow on the roofs**, and does nothing else, which is the whole of what winter does to
a building seen from outside.  A building is not deciduous; it takes the word so that a scene setting
its season once gets roofs that agree with its ground.

##### What makes a box into a building

This library exists because of a scene that failed at it.  `a-block-of-buildings` in the gallery raises
eleven towers out of one loop, and says of itself that they are *boxes with lights on, not
architecture*.  Four things do most of the difference, and all four are cheap:

**A plinth.**  A wall that meets the ground in a line reads as a sheet standing on grass.  A course at
the bottom, a little proud of the wall and a little lighter, is how a building sits down.

**Depth in the window.**  A rectangle of dark paint on a wall is a picture of a window.  What reads is
the *reveal*: glass set back, a frame standing proud around it, and a sill that overhangs and throws a
small shadow.

**An overhang.**  Eaves that reach past the wall lay a shadow line along the top of the facade.
Without it a roof looks glued on; with it the roof is clearly above and the wall clearly below.

**A top that is not a cut.**  A gable, or a parapet standing above the roof line.  A box ended flat at
the top is the strongest single tell that a thing is a box — which is why a `Tower` gets a parapet, and
why in winter its snow lies *across* that parapet rather than down inside it.  Snow inside a parapet is
invisible from any camera standing on the ground, and a season that cannot be seen is a word taken and
ignored.

**A wall made of something, and not all of the same something.**  A house wears brick — one of three, a
yellow stock, a red or a gray, picked by its variant along with everything else — and its gable is
boarded and painted above it.  That is what houses do, and it also puts a change of texture exactly
where the wall stops, which does more for the top of a facade than either material does alone.  A
`Tower` gets neither; a block in render or concrete is what a block is, and it keeps the two kinds of
building apart.

Both of those are patterns on a wall, and patterns on walls have traps in them.

**[A pattern is worked out in the surface's own
space](pigments-and-patterns.md#patterns-live-in-the-surfaces-space).**  A wall is a cube scaled
differently on all three axes, so a pattern laid on plainly comes out stretched by whatever those
numbers were — long flat bricks on the front and short tall ones on the end.  Dividing the scale back
out is what gives bricks of one size, and it is why the library's brick and boarding both have to be
told the wall they are going on.

**In a `union` or an `intersection`, the space that counts is the *leaf* the ray actually met**, not the
shape you wrote the material on.  A gable is an `intersection` of a turned cube with a thin one, and
sizing a pattern for the intersection rather than for the thin cube inside it gave bricks two and a half
times too long, plainly coarser than the wall below.  This is also why the boarding is written on the
thin cube alone and the sloping edges get flat paint: those edges belong to the *turned* cube, so boards
read in its space would run at forty-five degrees.

**`brick` lays joints in all three directions.**  A wall's face has one constant coordinate, so that
face either misses the joints running that way or lands squarely in one — and landing in one turns
*every other course of the whole face* to mortar, because those joints are staggered by course.  Worth
measuring rather than trusting: driving a house wall into its depth joints on purpose took the face from
twenty-two per cent mortar to sixty-one.

What no amount of care will do is turn a corner.  Real brickwork toothes at a quoin, alternate courses
reaching round; a pattern is a lattice cut by the wall's faces, so courses line up in height all the way
round a house and the bond does not interlock where two walls meet.  At the distance a building is seen
from that is invisible, and it is the reason to reach for a pattern here rather than for geometry.

**Frame a brick building near, or expect bands.**  A pattern is sampled at the point a ray hits, with no
filtering over the area a pixel covers, so once a brick course falls below a pixel the lattice beats
against the sample spacing.  Perspective makes that spacing vary smoothly across a wall, so the beat does
too, and a distant terrace comes back covered in broad *curved* bands rather than in brick.  A course is
about three and a half inches; at a distance giving nine pixels to the meter that is 0.8 of a pixel, which
is where it starts.

Antialiasing does not rescue it — the bands are low-frequency, and extra samples within a pixel average
away fine noise rather than a slow beat.  It is also the worst case for the adaptive sampler, which finds
real, unresolvable disagreement over every wall and subdivides all of it: `adaptive:3:0.1` on the night
street ran past a hundred times the un-antialiased cost, against the 22x a normal scene pays.  The gallery's
daylight scenes have no bands because they stand their houses close, which is the whole of the remedy.

For the boarding the useful trick is that `linear Y gradient` is a **sawtooth** — it climbs from nought
to one over each unit and starts again — so one unit is one board and the color map is a cross-section
of it: bright at the proud bottom edge, receding up the face, dark in the last tenth where the board
above laps over.  The drop back to nought *is* the board line, and there is nothing to smooth out.

#### Vehicles

```
import 'vehicles' { Car, Van, Truck }

object Car(4.4)
object Van(5.6, 'winter')      { translate X 9 }
object Truck(8.5, 'summer', 3) { translate X -11 }
```

| | |
| --- | --- |
| `Car` | A saloon: rounded body, a glasshouse under a painted roof, four wheels in cut arches. |
| `Van` | A cab with a box behind it, taller than it is wide, and higher off the ground. |
| `Truck` | A cab and a flat bed with a load on it, on six wheels. |

**The first number is a length, not a height.**  This is the one library that changes what the first
number means, and it changes it because that is how a vehicle is described — a car is four and a half
meters long, not one and a half tall.  The other two numbers mean what they always mean, and only the
length is required.

**These face `+X`**, so a vehicle drives to the right in a camera looking the usual way, and a street
running left to right needs no turning at all.  `rotate Y 180` sends one the other way.

**The season lays snow on whatever is flat and facing up** — roof, bonnet, boot, the top of a van's box
— and does nothing else.  A street whose houses are white-roofed while its cars are not is a street
that has quietly stopped making sense.

##### What makes a shape into something built

The buildings and the vehicles were both made of as few surfaces as would carry the shape, and shape
was never quite the problem: what they lacked was the small stuff that **projects or recesses**.  That
distinction is the whole of this pass.  A detail painted flat onto a wall does nothing at the angles a
street is actually seen from; a detail that stands out a hand's width catches the light on one side and
throws a shadow on the other, and it is those two edges that say a thing was *built* rather than
moulded.

On a house that meant a **chimney** tall enough to stand clear of the ridge with a cap slab on it
(there was one before, whose top cleared the roof by a tenth of its height and read as a wart), a
**ridge cap** along the join of the two slopes, a **gutter** under each eave with a **downpipe** to the
ground, and a **doorway** rather than a door: a surround up both sides, a head across the top, and a
step at the foot.  The downpipe is the cheapest of them and possibly the most valuable — one vertical
line on a face otherwise made of horizontal courses, and the eye finds it at any distance.

On a vehicle it meant **wing mirrors** above all.  Everything else on a car lies flush or recessed —
lamps, plates, a grille all color the face without changing its outline — and a mirror is the one
thing that breaks the line of the body.  A body with an unbroken line reads as a pressed-metal toy
however well it is painted.  Then a **grille** with bars in a sunken recess, since a nose of paint and
two lamps reads as blank, **number plates**, and an **exhaust** off center at the back, because they
never come out of the middle.

##### What makes a box into a car

The same question [the buildings](#buildings) ask, with a different answer:

**Rounded edges, and this one is not optional.**  A shape with corners never reads as a vehicle, at any
size, from any angle.  Every body panel here is a
[superellipsoid](surfaces.md#superellipsoid) held down at about a fifth — a box with its edges taken
off.  This alone does more than the other three together.

**Wheels cut into arches, not bolted to a flat side.**  The arches are cylinders taken out of the body
with a `difference`, so a wheel stands in a hole rather than against a wall.  A wheel touching a flat
slab is a toy, and no amount of tire detail repairs it.

**The glass must be capped with paint.**  A glasshouse whose top is glass reads as a suitcase left on
the roof.  What turns it into a cabin is a painted roof panel a shade *wider* than the glass, so the
glass is only ever seen as a band beneath it.

**Gloss.**  A building is matte and a vehicle is not, and the highlight sliding along a wing is most of
what says *painted metal*.  Drop these to a building's `specular 0.05` and the shape stops reading long
before the color does.

#### Water

```
import 'water' { Water, SeaWater }

object Water(700, 900, 'breezy') { material SeaWater  translate Z 300 }
```

| | |
| --- | --- |
| `Water` | A body of water lying in X/Z with its rest level at `y = 0`, centered on the origin. |
| `SeaWater` | Deep water: dark, blue-green, mostly a mirror. |
| `LakeWater` | Fresh water over a bed near enough to see, letting more through. |
| `PoolWater` | Clear water in a tiled pool, almost entirely a window. |

**The surface comes without a material, and you must give it one.**  What a body of water looks like is
mostly its material, so shipping the surface with one would be shipping the half that matters least.
This is the same bargain [the skies](#daylight) strike: take both halves or the picture disagrees with
itself.

The states run `glassy`, `calm`, `breezy`, `choppy`, `rough`.  Each sets four things — how tall the
swell stands, how far apart the crests run, how sharply they peak, and how far they lean.  **The crests get longer as
well as taller**, which is what makes a rough sea read as *large*: waves that grow taller without
growing longer look like a scale model of themselves.

`Water(across, along, state, variant)` takes its extent in X and Z.  **Make it larger than the picture
needs.**  Enlarging costs almost nothing — an ocean reaching the horizon measured 1.5 seconds against
1.4 for a pond, because a ray finds the surface just as quickly either way — while a box cut too small
ends in a straight edge in mid-water, which reads as a cliff.  `variant` shifts every wave train's
phase, so two bodies of water in one scene are not the same water twice.

##### What makes a surface into water

**The waves are really there.**  This is an [isosurface](advanced-surfaces.md#isosurface), so the
silhouette against the horizon is as rough as the middle of the picture, the shadows are wave-shaped,
and what the water refracts is bent by the slope it actually has.  A
[`normal` block](materials.md#roughening-the-surface) on a flat plane gets a similar look head-on for
far less work, and gives itself away at exactly the place a seascape is looking: at the horizon a flat
plane is still a straight line.

**It wants anti-aliasing, and that is the bill for the waves being real.**  Real detail at a distance
aliases.  At one sample a pixel, water going away from the camera turns to gray mush near the horizon,
because each pixel is averaging some normals that mirror the sky and some that look down into the
dark, and the average of those is neither.  Measured on a horizon-filling ocean at 400×300: **1.5
seconds with a mush band, 31 seconds at `-a adaptive:3` and clean.**  The same bill comes due for any
surface with fine detail in it.

**Give it something to reflect.**  Water is mostly a mirror, and a mirror in an empty room shows an
empty room — a flat background color makes water look like paint whatever else is right about it.  A
gradient will do; one of [the skies](#daylight) does it better.

**The crests are sharpened, and the sharpening had to be pulled back.**  A plain sine is as round on
top as underneath and reads as a swimming pool however tall it is made; real swell is peaked above and
broad below, because the water at a crest is moving forward and bunches there.  Raising a rectified
sine to a power imitates that — but it flattens the trough while it sharpens the crest, necessarily,
since a number near nought raised to a power is much nearer nought over a broad stretch either side of
the bottom.  Pure sharpening therefore paid for its ridges with dead-flat plateaus lying in the
troughs, which at `rough` looked like smooth leaf-shaped patches and read as a fault in the renderer.
Each swell term is now about two thirds sharpened and one third plain, which keeps the crests drawn up
and gives the troughs their curve back.

**The crests lean, and getting there needed no new machinery.**  A true trochoid displaces the
surface *sideways* as well as up, so each crest tilts in the direction it travels: steep in front,
broad behind.  A height written as `y - f(x, z)` looks unable to do that, since every point is
directly above where it started — but an implicit surface need not be a graph.  Shear the field's own
horizontal argument by its height, `f(x - lean·y, z)`, and it leans.  It is the same marched
isosurface as before.

Measured on a single train of crest 1.6 and wavelength 20, by solving the implicit equation directly:
the crest height stays at **1.6000 exactly** while the two faces diverge, the ratio of back slope to
front going 1.00, 0.68, 0.43 as the shear rises.  That is a trochoid's signature — asymmetric, and no
shorter for it.  Each state carries its own lean, given as a fraction of a wavelength so it means the
same thing at every scale.  It costs about 1.6× to march.

**What a lean is not.**  A wave that actually *breaks* — a crest folding over into a tube — is not
this.  Push the shear past about a third of a wavelength and the surface stops leaning and starts
folding into overlapping sheets, which is a mess rather than a breaker.  That wants a different
surface: a Gerstner wave proper, whose parameterisation inverts by Newton in four to six steps, giving
an implicit surface with an exact silhouette and no tessellation.  Worked out, not built.

#### Outdoor Lights

```
import 'outdoor-lights' { StreetLamp, WallLantern, BollardLight }

object StreetLamp(5)                    // dark
object StreetLamp(5, 'summer', 2, 1)    // burning
```

| | |
| --- | --- |
| `StreetLamp` | A column with a lantern on it: a period one, or a modern cobra head. |
| `WallLantern` | A lantern on a bracket, for the wall of a building. |
| `BollardLight` | A knee-high post with a lit band, for a path or a forecourt. |

The first three numbers mean what they mean everywhere — **how tall**, **what time of year**, and **which
one of that kind**.  There is a fourth here, and it is the interesting one.

##### `lit` is off by default, and that is arithmetic

A lamp lights a scene the way [`fire`](#fire)'s flames do: an emissive `medium` inside a shell, marked
`gives light`.  That is the honest way to do it, because the thing you can see and the thing doing the
lighting are then the same object and cannot disagree.

It is also **eleven times the cost of a point light** — one lamp took 2.49 million scene rays against a
point light's 218 thousand on the same scene, and four lamps took 9.65 million.  It scales with how many
are *burning*.  Most scenes with street lamps in them are daylight scenes where the lamps are off, and
those should pay nothing, so an unlit lamp is only geometry.

##### Brightness is free; smoothness is not

Two knobs, and it is worth knowing which does what, because guessing gets it backwards.

**Density is the brightness, and it costs nothing.**  Measured on the ground under a five-meter lamp:

| density | ground (of 255) | render time |
| --- | --- | --- |
| ×2,000 | 61.6 | 12.5s |
| ×10,000 | 128.1 | 12.1s |
| ×50,000 | 244.3 | 12.0s |

There is no ceiling — it climbs until the ground blows out — and the time does not move, because density
is a number in a function.  It scales as roughly the *square root* of the number, so five times the
density buys twice the light; the numbers therefore get large and look alarming, and large numbers are
free.  The library ships ×6000, which lights a house front across a pavement rather than just a pool of
ground under the column.

**`samples` is not a second brightness knob.**  Tripling it from 24 to 72 moved that same measurement
from 61.6 to 59.1 — nothing, within the noise it exists to reduce — and tripled the render time.  It buys
smoothness; only density buys light.

##### What a light cannot do here

**A light cannot live inside a fixture.**  `point light` inside a `group` is a parse error: a light is
only valid at a scene's top level.  So there is no hybrid where a primitive carries a visible globe *and*
a cheap point light doing the real work — for anything self-contained it is emissive or nothing.  If a
scene wants a hard-edged key light it has to place one itself, and then it is that scene's job to keep it
agreeing with the fitting it is pretending to come from.

One consequence worth expecting: an emissive globe is an **area** source, so its shadows are soft.  That
is more truthful than a point light's hard edge, and it does mean a lamp-lit scene looks gentler than a
studio one.

##### Turn the ambient down, and check what it was worth

Every material carries an `ambient` — the fraction of its own color it shows with no light on it, standing
in for bounced light the renderer does not trace.  In a daylight scene that is a small, sensible fudge.  In
a scene lit by these lamps it is competing with the thing you built, so a night scene should say so:

```
context { scale ambient by 0 }
```

That reaches every material at once, including the ones a library named for itself, which a scene has no
other way to touch — see [The Context Block](context.md#ambient).

Then measure what it was actually worth, because the guess is usually wrong.  In
`gallery/Local/functions/a-street-after-dark.igl` the answer is: almost nothing.  Of what lights the house
fronts, the lamps are about 98%, the sky light about 2%, and every material's own ambient a little over
half of one percent.  The reading that sent me looking was the opposite of that, and it came of removing
the sky light and finding the picture got *brighter* — which it does, but for an unrelated reason.  A scene
with no sky light in it gives every material that never mentions ambient 0.1 rather than 0, and that flip
gains more than losing the sky costs.  Change one thing at a time.

##### A lantern has to be mostly glass

The period `StreetLamp` was a single tapered `lathe` first, which is a handsome shape and completely
opaque — the globe sat inside it and the light had nowhere to go, so a *lit* lamp rendered as a dark blob
on a pole.  It is now a base, a cap and four corner posts, with the glass left as the gap between them.
The same trap waits for any fitting built as a solid of revolution.

And an unlit fitting needs pale glass, not dark.  A globe left dark reads as a hole in the lamp, which is
the commonest way an unlit fixture goes wrong.

#### Windows and Doors

```
import 'windows' { Sash, Casement, Shutters }
import 'doors'   { Door, GlazedDoor, DoubleDoor, Fanlight }

object Sash(0.30, 0.46, 0.14)  { translate [-1.9, 1.05, -0.14] }
object Door(0.28, 0.62, 0.14)  { translate [0, 0.64, -0.14] }
```

| | |
| --- | --- |
| `Sash` | Two sashes stacked, with a meeting rail across and glazing bars up each. |
| `Casement` | Side-hung leaves either side of a mullion, with a transom near the head. |
| `Shutters` | A pair of louvred leaves, to flank a window that is already there. |
| `Door` | A panelled leaf, with a surround up both sides, a head across and a step at the foot. |
| `GlazedDoor` | The same, with the upper panels replaced by glass and a bar across it. |
| `DoubleDoor` | Two leaves meeting on a centre stile. |
| `Fanlight` | A light over a doorway, to sit above one already there. |

**These are placed with their middle on the face of the wall**, so a wall built as a cube scaled
`[wide, tall, deep]` takes an opening at `translate [across, up, -deep]` and needs no arithmetic beyond
that.  **All three numbers are half-sizes**, matching that cube — which is not the convention the rest of
these libraries use, and is this way because every part of an opening is placed relative to a wall built
the same way.

**[`buildings`](#buildings) imports both of them**, which is what these libraries are chiefly for: a
`House` and a `Tower` now differ in their *openings* as well as their proportions, and a scene can put the
same sash into a wall it built itself.  That was impossible until recently — a library could not import
another, and the choice was between a copy of every window inside `buildings` and a house that could not
have one.

##### The depths have to stack in order

This is the whole difficulty of an opening, and it went wrong in both directions before it came right.

`-Z` is outward, so the wall's face is `z = 0` and **anything at positive Z is buried inside the wall**.
Working outward from there:

| | span | |
| --- | --- | --- |
| glass | −0.30d … +0.20d | deepest, seen through the opening |
| glazing bars | −0.68d … −0.32d | recessed in the reveal, in front of the glass |
| frame, panels | −0.72d … −0.52d | on the face |
| surround, sill | −1.0d … +1.0d | standing proud of everything |

Put the glass in front of the bars and it hides every one of them — a sash renders as a single blank
sheet.  Put it at positive Z instead and the wall hides the glass, so the window renders as an empty hole.
Both of those look like the geometry is missing rather than misplaced, which is what makes them hard to
diagnose from the picture alone.

**And a glazed door needs a hole, not merely fewer panels.**  Leaving the upper panels off changes
nothing, because the leaf is a solid slab standing in front of wherever the glass goes: the door comes
out looking exactly like a panelled one with a plain top half.  It takes a `difference` to make a real
opening, and the reveal that leaves is what makes the glass read as set *into* the door.

##### What makes a hole in a wall into a window

The [buildings](#buildings) library says an opening needs glass set back, a frame standing proud, and a
sill that overhangs and throws a shadow.  All of that still holds.  What these libraries add is the
fourth thing, which only matters once a camera comes close:

**An opening is divided.**  A single sheet of glass reads as a shop window at best and a mirror at worst;
a flat door leaf reads as a painted rectangle.  What says *window* is the bars across it, and what says
*door* is a raised panel — because a pane and a panel are each about a fixed size in the world, so how
many of them there are tells the eye how big the wall behind them is.  A wall with one enormous pane has
no size at all.

#### Roads

```
import 'roads' { Road, Curb, Pavement }

object Road(80, 7)
object Curb(80)        { translate Z 3.5 }
object Pavement(80, 3) { translate Z 5.2 }
```

| | |
| --- | --- |
| `Road` | A carriageway: cambered, worn where the wheels run, with a line down the middle. |
| `Curb` | A curb run, for the edge of a carriageway. |
| `Pavement` | A footway in slabs, for behind the curb. |
| `Junction` | The mouth where a side road opens off a main one. |
| `CurbCorner` | A quarter circle of curb, for the corner where two roads meet. |
| `Crossing` | A zebra, laid on a carriageway of the same width. |
| `Crossroads` | Where two roads cross, with a stop line across each side arm. |
| `Roundabout` | An island with a carriageway round it. |
| `CurbArc` | A curb along part of a circle, for where a ring has to be broken. |
| `CurbRing` | A whole circle of curb, for a roundabout's island. |
| `ParkingLot` | A rectangle of asphalt with bays marked on it. |

**The first two numbers are a length and a width**, because a road is the one thing in these libraries
that genuinely has two sizes and no sensible ratio between them.  The two after them mean what they
always mean, **what time of year** and **which one of that kind**.

**These run along `X`**, like the [vehicles](#vehicles) and the [trains](#trains).

**The road surface is `y = 0`** and everything else hangs below it, which is exactly where `vehicles`
puts the bottom of its tires — so a car dropped on a road at the same origin stands on it.  It does mean
a `plane` at `y = 0` is the wrong ground for a street: put the verge a little lower, around `-0.12`, and
let the curb stand out of it.

The variant picks the surface, and here that is a bigger choice than a color: `0` is new asphalt, nearly
black; `1` is worn asphalt, grayed and cracked; `2` is concrete, pale and laid in bays.

##### What makes a slab into a road

A road is the flattest thing in any of these scenes, so it has the least to work with.

**Camber.**  A road is crowned so water runs off it, by about one part in forty — far too little to see
directly, and most of what stops a carriageway reading as a sheet of paper, because the crown catches
light differently from the channels.

**Wheel tracks.**  Traffic polishes two bands in each lane and leaves the crown, the lane middles and the
channels matte.  This is the strongest cue that a road is *used*, and it wants to be a **small**
difference over a wide band: giving the tracks three times the surround's specular reads as two stripes
painted down the road.

**Dirt at the edges.**  Nothing sweeps the last half meter against a curb.  A road the same color from
edge to edge reads as newly laid whatever its surface says.

**Asphalt is much darker than it looks.**  New asphalt returns about a twentieth of the light falling on
it, which is darker than almost anyone mixes it — and the [rocks](#rocks) lesson applies exactly.

##### The surface is `parallelogram`s, and that is not a saving

This is the part worth reading if you are going to build anything long and thin.

A pattern is [worked out in the surface's own space](pigments-and-patterns.md#patterns-live-in-the-surfaces-space).
A strip of carriageway built as a cube scaled `[30, 0.06, 0.25]` therefore stretches its granite thirty
times along the road and squashes it to a quarter across — the first version of this rendered long
longitudinal streaks and not one crack anywhere.  A [`parallelogram`](surfaces.md#parallelogram) is given
a corner and two edge vectors and carries **no transform at all**, so its pattern is in world
coordinates.  That also makes the cracks run continuously across the strip seams, which compensating a
cube's scale would not.

**Each strip is tilted rather than laid flat.**  A parallelogram takes edge *vectors*, so giving the
second one a `Y` component puts the strip on the camber's slope and lets consecutive strips meet exactly.
Flat strips leave a vertical step at every seam and neither way of hiding one works: butting them shows
the dark build-up through the step, and overlapping them by a centimeter turns the higher edge into a lip
that casts a thin shadow down the road.

**A joint is a gap, not a line laid on top.**  Concrete is laid in bays, so the wearing course is laid in
bays with a gap between them and the build-up shows through.  A dark cube across the road cannot do it —
a cube is flat and the road is crowned, so it sits *under* the surface across most of the width and pokes
out only near the channels.  Asphalt gets one bay the length of the road and a gap of nothing, so the
same loop serves both.

##### Cracks are the rare end of `crackle`, and where that end is matters twice

`crackle` climbs from nought at its seed points towards the cell walls, so darkening only near the top
paints the walls and leaves a sparse net of lines over an even surface.  Spread the same map evenly over
nought to one and the whole road becomes dark crazy paving.  But the top is lower and narrower than it
looks, and this went wrong in both directions before it came right:

- Measured over sixty thousand points, crackle's 95th percentile is **0.508** and its 99th is **0.666**.
  Dark tones at 0.68 and 0.82 are therefore *past the end of the distribution*, and rendered a road with
  no cracks in it at all.
- Moved to 0.56, they picked out only crackle's **vertices**, where three cells meet, and rendered as
  scattered dots.  Whole walls need a wider band: 0.46 is about the 91st percentile.
- And the cells have to be small.  The top few per cent of crackle is a *ring* around every wall, so at
  55 cm cells those rings render as dark blobs; only at 20 cm do they read as cracking.

##### Joining two roads

**A junction is a piece of main road, not a shape of its own.**  The main road's camber runs straight
through it and the side road ramps up to meet it, which is what a real side turning does — the
alternative is to invent a surface draining both ways at once, and then neither road's channel lines up
with it.  So `Junction` is the same carriageway with the markings left off, because markings stop at a
junction, and a scene lays road either side of it:

```
object Road(30, 7)     { translate X -19 }
object Junction(7, 8)
object Road(30, 7)     { translate X  19 }
```

**`CurbCorner`'s origin is its center of curvature, not the corner.**  This is the one thing here that is
easy to get wrong.  For a pavement corner at `(cx, cz)` with road on its `+X` and `+Z` sides, the center
sits a radius *diagonally inside* the pavement at `(cx - r, cz - r)`, and the arc meets the straight runs
at `(cx - r, cz)` and `(cx, cz - r)` — so those runs stop a radius short of the corner.  Put the origin on
the corner itself and the arc swings away into the pavement, leaving a gap at both ends.

Two straight curbs mitred at a right angle look like a drawing; a radius looks like a street.  It is a
loop of short straight `Curb` runs turned around the arc, and the turn that points each face outward is
`-90 - a`: a `Curb` runs along X with its face looking towards `-Z`, and under `rotate Y θ` that face
lands on `(-sin θ, 0, -cos θ)`, which equals the outward normal `(cos a, 0, sin a)` exactly there.

**A crossing's bars run along the road, not across it**, which surprises people who have not looked
lately — you walk across a zebra *between* its stripes.  That turns out to be the geometry the
carriageway is already built from, so a bar is one strip of it painted, and picks up the camber for free
by being tilted exactly as the strip beneath it.

##### Crossings of two roads, and roundabouts

`Crossroads` is a `Junction` with two arms instead of one, and adds the thing that makes a crossing of
two roads read as a junction rather than as a hole where the markings stop: a **stop line** across each
side arm.  Its deck reaches a corner radius past the side road on each side, so there is carriageway under
the four rounded corners.

Those corners have to be written out rather than looped, and it is worth saying why, because a loop is the
obvious thing to reach for.  For a main road of width `w` crossing a side road of width `v` with radius
`r`, the four centers are at `(±(v/2 + r), ±(w/2 + r))` — and unless the two roads happen to be the same
width those are *not* symmetric under a quarter turn, so rotating one placement by ninety degrees does not
land on the next.

`Roundabout` is the island plus the circulating carriageway.  The carriageway is `disc`s, which is the same
trick the straight road plays with `parallelogram`s: a disc takes a center, a normal and a radius, and an
`inner radius` makes it an annulus — so it carries no transform and its pattern is in world coordinates
rather than stretched round the ring.  Three concentric annuli give the worn band where traffic actually
circulates, and since they are flat and share their radii exactly they abut with no seam at all.

**It is flat, and that is deliberate.**  A real roundabout has a crossfall and there is no honest way to
put one here: a disc is planar by definition, and stacking concentric rings at descending heights brings
back exactly the vertical-step seams the straight road had to be tilted to avoid — and a disc, unlike a
parallelogram, cannot be tilted without becoming an ellipse.  A crossfall over six meters is fifteen
centimeters, which is worth less than a ring of dark seams.

**There is no curb round the outside of it**, because a roundabout's outer curb is broken at every entry
and a full ring lays curb straight across each approach, walling the roads off.  Teaching the primitive
about arms would mean telling it how many, how wide and at what angles, and it would still be wrong for
the roundabout whose arms are not evenly spaced.  So the scene lays `CurbArc`s between its own entries,
the same division of labor `Junction` uses:

```
object Roundabout(4.5, 6)
for arm in [0, 3] {
    object CurbArc(10.5, 21 + arm * 90, 48, 1)
}
```

`CurbArc`'s `facing` is `0` for a face pointing away from the center — an island, with the road outside —
and `1` for a face pointing towards it, with the road inside.  `CurbCorner` is this over ninety degrees
and `CurbRing` over the whole circle, and both are *calls* to it rather than second copies of the loop:
they started as copies, drifted immediately, and a test that renders a corner against a ninety-degree arc
and compares them pixel for pixel caught it on its first run.

One more thing about arcs.  **The segment count goes as the square root of the radius, not the radius.**
The gap a chord leaves is `r · (1 − cos(turn/2))`, so holding that fixed makes the segment angle scale as
one over root `r`.  A rule linear in the radius over-segments a big arc and under-segments a small one —
and the small one is where it shows, since a tight corner is what a camera gets close to.  At four
segments per unit of radius a four-and-a-half meter island came out visibly polygonal: twenty-degree
segments bulge nearly seven centimeters off the circle, which is half a curb's width.

##### Parking

`ParkingLot(width, depth)` is a rectangle of asphalt with two rows of bays against its long edges and an
aisle between them.  Bays are 2.5 m wide and 5 m deep, which is what a bay is, and the count comes from
the width rather than the other way about — so a lot is whatever size the scene needs and the bays divide
it evenly.

One thing to watch: the bay depth is `depth * 0.42` up to a limit of five meters, so a **shallow lot gets
shallow bays** and a car parked in one hangs out the back of it.  Nine meters deep gives 3.8 m bays, which
a 4.4 m car overhangs by more than half a meter; fourteen gives the full five.

##### Winter on a road is not winter on a roof

**The traffic clears the tracks and nothing clears the rest.**  So snow lies along the crown, where no
wheel runs, and banks up in the channels where it is thrown.  A road under snow with a clean unbroken
white surface is a road nobody has driven since it fell.

#### Trains

```
import 'trains' { Locomotive, Tender, Track }

object Track(60)
object Locomotive(11.6)
object Tender(8.05) { translate X -10.35 }
```

| | |
| --- | --- |
| `Locomotive` | A Great Western 4-6-0: coned boiler, copper-capped chimney, three coupled drivers on rods. |
| `Tender` | The six-wheeled tender that runs behind it, coal heaped above the coping. |
| `Track` | A length of line: rails on sleepers on ballast, at standard gauge. |

**The first number is a length**, as it is for the road [vehicles](#vehicles) and for the same reason —
an engine is described by how long it is, not how tall.  For `Track` it is how much line you want.  The
other two numbers mean what they mean everywhere, **what time of year** and **which one of that kind**.

**These face `+X`**, like the road vehicles, so an engine runs to the right in a camera looking the usual
way and a line laid left to right needs no turning.

**Rail top is `y = 0`.**  Everything the engine is made of is measured up from the rail it stands on, and
the sleepers and ballast hang below.  An engine and a length of track therefore drop into a scene at the
same origin and simply fit — but it means a `plane` at `y = 0` is the wrong floor for a railway.  Put the
ground a little lower, around `-0.64`, and let the bed stand out of it.

What it is a model of is 5972 *Olton Hall* in the crimson she was painted for the films, with the
nameplates she wears there.  The dimensions are the prototype's, to the nearest fraction that matters:
six-foot drivers, a coupled wheelbase of fifteen feet six, standard gauge, thirteen feet from rail to
chimney top.  They are written in **meters**, by the same trick the [brick](#buildings) uses with inches
— one line fixes how long a meter is for this engine and everything after it is a real measurement, so
changing the length keeps the proportions without turning every number into a fraction of a fraction.

##### What makes a box into a locomotive

Five things, and the first two are worth more than the rest together.

**Spokes.**  A locomotive wheel is mostly air, and a solid disc reads as a toy at any size.  The rim is a
`difference` of two cylinders and the spokes are a `for` loop of thin bars turned about the axis, twenty
to a driver.  What that buys is not only the wheel: it is the striped *shadow*, and the daylight coming
through the frames under the boiler.

**Rods.**  Spoked wheels alone still read as a cart.  What says *engine* is the coupling rod tying all
three drivers together and the main rod reaching back from the cylinder, because those are the only parts
whose whole purpose is that the thing moves.  They are [`tube`s](advanced-surfaces.md#tube) — two points
and a radius, no angles to work out — flattened along one axis, since a rod is forged flat and a round one
reads as plumbing.

**A boiler that tapers.**  This is the Great Western signature and it is nearly free: one
[`lathe`](advanced-surfaces.md#lathe) turns the barrel as a single revolved profile.  A parallel boiler is
a tube; a coned one is a locomotive.  It has to be the *paint* that is turned, though — sleeving a
straight crimson cylinder over a tapered lathe throws the taper away and leaves a sausage.

**No dome.**  Almost every other engine has a steam dome standing on the barrel and a Great Western taper
boiler does not, because the regulator is in the smokebox.  Leaving it off only works if it is deliberate:
the boiler top runs clean from the chimney to the safety valve, and that clean line is most of what makes
the class recognizable.

**Copper and brass, in two places only.**  The chimney cap and the safety valve bonnet.  Everything else
bright on the engine is lining, and lining is thin.  Put polish anywhere else and it stops looking like a
locomotive and starts looking like a fairground ride.

##### The bed is the railway

At any distance a railway *is* two bright threads on a dark bed, and the bed is worth as much care as the
engine standing on it.

**A rail is two colors.**  The head is wiped bright by every wheel that passes and the web below it rusts.
Painted one color the whole line vanishes into the ballast it sits on.

**The ballast is one extruded trapezoid**, wide at the bottom and narrower at the top, with each top corner
taken off by a `quad`.  It began as two flattened cubes stacked one on the other, which gives a stepped
profile no heap of stones has.  Its top sits far enough below the sleeper tops to leave them about half
proud — at a tenth proud, which is where it started, the bed reads as a slab with planks laid on it rather
than as stone packed around them.

**Gravel wants a pattern with cells in it**, not a noise, and which way round to map it is the whole
question.  [`crackle`](pigments-and-patterns.md#continuous-patterns) is nought at its seed points and
climbs towards the cell walls, so the seeds are the middles of the stones and the walls are the gaps
between them.  Mapped dark-to-light in the obvious direction that paints the *gaps* pale, and a bed of
crushed rock comes out as a white net drawn over dark ground — a mosaic, not gravel.  It wants the other
way about: pale at nought where a stone face catches the light, dark at the top where the shadows are.

And the stops are percentiles, not an even spread.  Measured over sixty thousand points, crackle runs 0 to
1 but its median is **0.158** and only a hundredth of it is above **0.666**, so an even map leaves
practically the whole bed in its darkest two tones.  This is the same trap the [rocks](#rocks) fell into,
and the reason to measure a pattern before mapping it.

##### Winter on an engine is not winter on a house

The season had to be thought about here rather than copied.  **A boiler in steam is hot**, so nothing lies
on the barrel, the smokebox or the chimney.  What carries snow is everything the fire never reaches: the
buffer beam, the front platform, the cab roof and the tender.  An engine caught in snow with a white
boiler is an engine nobody has lit, which is a different picture from the one a winter scene wants.

### Where Libraries Live

Libraries live under your home directory, at `.rayTracer/Libraries`, beside the
[font catalog](fonts.md#the-font-catalog) — the two are the same sort of thing: material the
ray tracer keeps for itself rather than material a scene supplies.  The directory is shared
across every scene you render, so a library need only be added once.

A library is a plain `.igl` file of `Name = definition` assignments, so you can open one and
read it like any other scene file.  A scene may also keep a library of its own right beside
it: a name is looked for next to the scene first and among the shared libraries second, with
or without the `.igl` on the end.  Looking beside the scene first means a scene can carry a
small library of its own, or put one in front of a shared one under the same name.

A library may hold anything that leaves a name behind — a value, a material, a surface, and since
scenes gained a language of their own, a [function or a primitive](scene-files.md#things-of-your-own)
as well.  It keeps its own workings: what a library writes for itself stays in the library, and only
the names a scene asks for cross over.  See
[Importing from a library](scene-files.md#importing-from-a-library) for what that means and where the
line falls.

A hand-written library is nothing more than named definitions in a file:

```
// mine.igl — a small library of my own.
Copper = material {
    pigment color [0.72, 0.45, 0.2]
    specular 0.8  shininess 120  reflective 0.3
}
Jade = material {
    pigment color [0.2, 0.6, 0.45]
    specular 0.4  shininess 60
}
```

A scene sitting beside it imports from it exactly as it would from a shared one:

```
import 'mine' { Copper, Jade }
```

### Seeing What You Have

```bash
RayTracer libraries --list
```

```
/Users/you/.rayTracer/Libraries
  Library   Definitions  Source
  --------  -----------  ----------------------
   finish             8  POV-Ray's finish.inc
   glass            138  POV-Ray's glass.inc
   golds             82  POV-Ray's golds.inc
   metals           125  POV-Ray's metals.inc
   skies              7  POV-Ray's skies.inc
   stars              6  POV-Ray's stars.inc
  stones1            87  POV-Ray's stones1.inc
  stones2            16  POV-Ray's stones2.inc
  textures           97  POV-Ray's textures.inc
   woods             46  POV-Ray's woods.inc
```

The first column is the name a scene imports by, the second is how much the library holds, and
the third is where a converted library came from — a library of your own has nothing there to
say.

### Adding a Library of Your Own

A library of your own — a file like `mine.igl` above — can be installed for every scene to
share, rather than kept beside one scene, by importing it:

```bash
RayTracer libraries --import mine.igl
```

This copies the file into the library directory under its own name, so `mine.igl` becomes the
library `mine`.  Before it copies anything it reads the file through, so a file that will not
parse is turned away here rather than the first time a scene reaches for it.

A library may hold **only definitions** — `Name = …` assignments.  A file that also carries a
surface, a camera, a light or a `render` command is refused, since an import is meant to bring
across named definitions and nothing else; anything else would be dragged into every scene that
imported it.  Use `--dry-run` to check a file and see what it would bring without writing
anything, and `--overwrite` to replace a library of the same name that is already there.

### Bringing POV-Ray's Textures Across

POV-Ray ships a large collection of finishes, metals, glasses, stones and woods in its
`include` directory, and `--import` can convert them all at once when you add `--povray`:

```bash
RayTracer libraries --import /path/to/povray/include --povray
```

`--povray` says the thing being imported is a whole POV-Ray distribution to convert rather than
one `.igl` file to copy, so `--import` is pointed at the `include` directory of a distribution —
the one holding `glass.inc`, `metals.inc` and the rest.  From a stock distribution it writes ten
libraries holding a little over six hundred definitions, and reports what each became:

```
  Library    Materials  Pigments  Interiors  Values
  ---------  ---------  --------  ---------  ------
  woods             34        12          0       0
  golds             55         0          0      27
  metals           105         0          0      20
  glass             17         0         13     108
  stones1           83         0          0       4
  ...
```

The exact counts depend on which distribution you point it at.  A **material** is a full
surface finish; a **pigment** is a color or pattern on its own; an **interior** carries the
index of refraction that makes glass bend light; and the **values** are the named colors and
numbers the rest lean on.

#### Names

POV-Ray marks what a thing is with a prefix, and the converter says it in a word at the end
instead: `T_Gold_3C` comes across as `Gold3CMaterial`, `P_Silver1` as `Silver1Color`,
`I_Glass3` as `Glass3Interior`.  Each definition carries POV-Ray's own name in a comment right
above it, so you can find a thing by either name:

```
// T_Gold_1A
Gold1AMaterial = material {
    // ...
}
```

#### What does not come across

Not everything can be converted — POV-Ray's include files contain macros and constructs with
no simple equivalent here — and the verb says what it had to leave behind, grouped by reason so
that one cause standing for dozens of definitions reads as one line rather than a long list.
Each is reported with its count and an example; the reasons themselves are in plain words, like
these:

```
  This is not something a library can hold.
  A gradient runs between axes, which we cannot express.
  Only the bottom layer's finish came across; the ray tracer has one finish for a surface.
```

`--details` lists every affected definition instead of the counts.  Two of POV-Ray's files,
`colors.inc` and `consts.inc`, are read only for the names they define and are never written
out as libraries of their own.  `ior.inc` is left out on purpose: its indices of refraction are
worth having, but they are already available as
[named indices](materials.md#transparency-and-interiors) — `ior Glass`, `ior Diamond` and the
rest — with no import at all, and a second copy under different names would be a trap rather
than a convenience.

#### Names in more than one library

A few names are declared by more than one library — POV-Ray defines some metal finishes in both
`golds` and `metals`, and defines glass in both `glass` and `textures` — and the verb points
these out at the end, naming each one and where it comes from.  A scene that imports from only
one of the two is unaffected; the warning is there for the scene that reaches into both, since
it gets whichever was read last.

#### Trying it first, and doing it again

`--dry-run` converts and reports without writing anything, so you can see what you would get
before any of it lands on disk:

```bash
RayTracer libraries --import /path/to/povray/include --povray --dry-run
```

Importing will not quietly replace libraries already there; pass `--overwrite` when replacing
them is what you mean:

```bash
RayTracer libraries --import /path/to/povray/include --povray --overwrite
```

One thing to know: a fresh import does not remove libraries it no longer produces.  If a later
version of the ray tracer stops generating one, the old file stays until you take it out
yourself.

### Using an Imported Definition

An imported material is a material like any other, so it can be worn as it is or
[adjusted](materials.md#naming-and-reusing) on the way — named, then followed by a block
saying what to change:

```
import 'golds' { Gold3CMaterial }
import 'glass' { Glass3Material, Glass3Interior }

sphere {
    material Gold3CMaterial { reflective 0.6 }
    translate [-1.5, 1, 0]
}
sphere {
    material Glass3Material { interior Glass3Interior { clarity 7 } }
    translate [1.5, 1, 0]
}
```

Because an import brings in only what you name, the two interiors and dozens of other glasses
that `glass` also holds stay out of the way.

### Removing a Library

```bash
RayTracer libraries --remove golds
```

The name may be given with or without the `.igl`.  This only removes the library file; a scene
that still imports from it will fail to find it the next time it is rendered.

### FontAwesome Icons

The `libraries` verb also keeps the [FontAwesome](https://fontawesome.com) icons a scene can use
as [2D paths](advanced-surfaces.md#icons).  Download a FontAwesome zip and install it once:

```bash
RayTracer libraries --fa-zip fontawesome-free.zip
```

This copies the zip in beside the libraries, as the ray tracer's own, so every scene can read its
icons.  Install it exactly as downloaded — there is no need to unpack it, and the folder the
download wraps everything in does not matter.  The file must be a FontAwesome zip — one holding an
`svgs` folder of icon outlines — and installing a new one replaces the one before it.  A path then
names an icon as `style:name`, or just `name` for the `regular` style; see
[Icons](advanced-surfaces.md#icons).

### A note on the command names

Every one of these has a short form as well — `-i` for `--import`, `-p` for `--povray`, and
`-l`, `-r`, `-o`, `-d`, `-n` for the rest — so converting a POV-Ray distribution is often
written:

```bash
RayTracer libraries -i /path/to/povray/include -p
```
