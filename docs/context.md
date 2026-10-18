## The Context Block

Most of what a scene says is about the world — where things are, what they are made of, how
they are lit.  The context block is about the *rendering* instead: how the picture is drawn
rather than what is in it.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/context/contextClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/context/contextClause.svg">
  <img alt="The context block" src="images/context/contextClause.svg">
</picture>

Everything in it has a sensible default, so the block is optional and many scenes leave it
out entirely.  Its value is that a scene can fix how it renders rather than depending on
being invoked the right way:

```
context {
    parallel pixel scanner
    angles are degrees
    no gamma
}
```

Several of the settings this block may contain can still be overridden from the command line,
but never the other way about — see [How a setting is decided](getting-started.md#how-a-setting-is-decided).

If a scene contains more than one context block, they are treated as if there was only one.
The values accumulate so if a setting is specified more than once, either in the same block
or across multiple blocks, the last one wins.

### Image Information

The `info` block records who made the picture and what its title is and other types of
metadata for the image.  What it holds is written into the image file's own metadata (if
the image file format supports it), so it travels with the picture rather than living only
in the scene file.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/context/infoClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/context/infoClause.svg">
  <img alt="The info block" src="images/context/infoClause.svg">
</picture>

```
context {
    info {
        title 'A Documented Scene'
        author 'A. Scene Author'
        description 'Showing what the context block can fix.'
        copyright 'Public domain'
    }
}
```

The fields are `title`, `author`, `description`, `copyright`, `software`, `disclaimer`,
`warning`, `source` and `comment`.  Each takes a string.  How they are stored depends on the
image format — PNG keeps them as text chunks, which is why the example above comes back out
of the file with its title and author intact.  The renderer defines a variable called,
`__software__` that provides a suitable value for the `software` metadata field.  You can see
examples of its use in the Challenge book gallery pictures.

The complete example is
[`docs/examples/context/settings.igl`](examples/context/settings.igl).

### Angles

Every angle in a scene — a rotation, a spotlight's cone, a camera's field of view — is read
in whichever unit this setting names:

```
angles are degrees
angles are radians
```

Degrees are the default, so `angles are degrees` says out loud what was already true.  Rather
more than half the gallery scenes write it anyway, and that is a reasonable habit: a scene
that turns things is easier to trust when it states which unit it means.  Write
`angles are radians` only if you would rather work in radians throughout your scene since
the setting applies to the whole scene.

### Gamma

Gamma correction bends the colors on their way out of the renderer to suit how a display
actually behaves.

| Written | What it does |
| --- | --- |
| `apply gamma` | Turn gamma correction on. |
| `no gamma` | Turn it off. |
| `gamma 2.2` | Set the value used when it is on. |

Correction is on by default.  A great many of the gallery scenes write `no gamma`, which is
worth understanding: with correction off, the numbers a scene writes for its colors are the
numbers that reach the file, so a material's color can be reasoned about directly.  With it
on, the picture generally looks better on an ordinary screen.  Neither is wrong; they answer
different questions.  An [image pigment](pigments-and-patterns.md#image-pigments) follows the scene: with correction
on, a picture's values are turned into the light they stand for, and with it off they are taken as
they stand, like every other color in the scene.

### Shadows

| Written | What it does |
| --- | --- |
| `no shadows` | Let nothing in the scene cast a shadow. |

Everything is then lit as though nothing stood between it and the lights.  It covers the whole
scene, wherever in the file the block is written, and `--no-shadows` on the command line does the
same for a scene that does not ask for it.  To spare a single surface its shadow instead, give it
[`no shadow`](surfaces.md#no-shadow).

### Ambient

`ambient` is the fraction of its own color a surface shows regardless of any light reaching
it.  It stands in for bounced light this renderer does not trace: without it, everything an
actual light misses would be pure black, and a shadow would read as a hole rather than as a
shadow.  Each material either names its own or takes what the scene settles on it — see
[Materials](materials.md).

That is a reasonable default for a scene lit like daylight, and quite wrong for one lit by a
candle or a street lamp, where the fudge is the same size as the light.  A scene can turn the
whole lot up or down at once:

```
context { scale ambient by 0.25 }
```

Every material's ambient is multiplied by that number, whether the material named the value
itself or was given the default.  This is the only way to reach the ones that named their
own, which matters most when the materials come from a library: a scene using `buildings`,
`roads` and `vehicles` inherits some eighty explicit ambient values it did not write and
should not have to edit.

`scale ambient by 0` removes the stand-in altogether, leaving only light that was actually
traced.  Sensible for a night scene with its own lamps in it, and a good way to see how much
of a picture the fudge was really carrying — usually less than you would guess.
`gallery/Local/towns/a-street-after-dark.igl` does exactly that, and records the
measurement in a comment: its lamps turn out to be doing about 98% of the work.

There is a related default worth knowing.  A material that never mentions ambient is given
0.1, unless the scene contains a [sky light](lights.md), in which case it is given 0 — a sky light
delivers the surrounding light for real, so the stand-in for it would be counted twice.  Both are
settled before this scaling is applied.  A scene that asks for [bounced light](#bounced-light) goes
further and drops every material's ambient, named or not.

### Scanners

A scanner decides how the work of tracing pixels is handed out.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/context/scannerClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/context/scannerClause.svg">
  <img alt="Choosing a scanner" src="images/context/scannerClause.svg">
</picture>

| Written | What it does |
| --- | --- |
| `serial scanner` | One pixel after another, on a single thread. |
| `parallel line scanner` | Whole rows handed out across threads. |
| `parallel pixel scanner` | Individual pixels handed out across threads. |

The choice affects only how long the render takes; the picture is identical either way.
`parallel pixel scanner` is usually the fastest, and is what the gallery scenes use, because
it keeps every thread busy even when one part of the image is far more expensive than
another — which is exactly what happens when some of the scene is glass and the rest is a
plain floor.  `serial scanner` is mostly useful when you are chasing a bug and want the work
to happen in a predictable order.

### Anti-Aliasing

A scene may say how hard to work at the edges within a pixel:

```
context {
    antialiasing depth 1
    antialiasing threshold 0.5
}
```

| Written | What it does |
| --- | --- |
| `antialiasing depth` | How many times the sampler may look further into a pixel. |
| `antialiasing threshold` | How far two samples must disagree before it does. |

Either one on its own turns the adaptive sampler on, since neither number means anything
without it; `depth` alone leaves the threshold at its usual value, which is the common case.
Nothing said means no antialiasing at all, which is what makes it worth saying.

**A scene that needs antialiasing should say so here rather than rely on being rendered with
the right flag.**  `-a`/`--antialias` on the command line still overrules whatever the scene
asked for — see [Command Line Options](getting-started.md#how-big-and-how-good) — but a scene
that only ever said it on a command line has not really recorded it: the next person to render
it, or a sweep that re-renders the whole gallery, gets a picture with the edges left rough and
no indication that anything was lost.  That is not hypothetical; it is how three gallery images
came to be replaced with worse ones.

### Rough Reflections

A [rough surface](materials.md#roughness) sees what it mirrors as a blur, and finds the blur by
looking in many directions and averaging:

```
context { reflection samples 32 }
```

The usual number is 16.  More makes a large glossy surface smoother and costs more on every pixel
of it; fewer is quicker and grainier, which is often fine for a draft or for surfaces that are
small in the picture.  Antialiasing adds to it, since every sample within a pixel looks in its own
set of directions.

Only the first rough surface a ray meets spreads it this way.  A ray already one of a spread, and
every bounce after it, looks in one direction only, so two rough surfaces facing each other cost
the samples once rather than raised to the power of the bounces.

### Bounced Light

Light does not stop at the first thing it lands on.  A sunlit wall lights the floor in front of it, a
red rug puts a warm cast on the white wall above it, and the sand under a figure lights the shaded side
of everything standing on it.  `ambient` is a flat stand-in for all of that; this follows it instead:

```
context { diffuse bounces 2 }
```

Each matte surface then looks out in a spread of directions -- more of them where it faces, in
proportion to how squarely, exactly as a matte surface weighs the light it takes -- and takes its share
of everything it finds there: other surfaces lit by the lamps, what they mirror, what shines through
them, and their own bounced light in turn, as many times over as `diffuse bounces` says.  Asking for it
takes `ambient` away altogether, even where a material names its own, as a curated library's materials
do: what the fudge stood in for is now being followed.  What glows of its own says
[`emission`](materials.md#emission) instead, and is seen by its neighbors as the light it is, which is
how a lit window or a glowing lampshade lights the room around it.

| Setting | Default | What it does |
| --- | --- | --- |
| `diffuse bounces` | 0 | How many times light is followed from one matte surface to the next. |
| `diffuse samples` | 32 | How many directions the first matte surface along a path looks in -- or, with a [cache](#a-cache-of-bounced-light), each of its records, the pixels looking round themselves in a sixteenth as many. |

**Each bounce is fainter than the last** by the surfaces' own color: a room of white walls taking 0.8
of the light gives back 0.8 of it, then 0.64, then about half, so two or three bounces is most of it in
a bright room and one is most of it outdoors.  **Only the first surface spreads**, as with
[rough reflections](#rough-reflections): every surface after it looks in one direction and asks each
lamp about one of its samples, so a second bounce costs about what the first did rather than its square.
What a modest count costs is grain, which the samples within a pixel average away; more `diffuse
samples` settles it further.

**Nothing is counted twice.**  A direction that runs straight out to the sky finds what a sky light
has already counted, and a sky with no sky light is a backdrop rather than a light, so it brings back
nothing; one that runs straight through a glowing volume that [gives light](surfaces.md#a-surface-that-gives-light)
brings back only what the volume lets through.  Lamps, panels and suns cannot be met by a ray at all.
Anything found another way -- the sky in a mirror, a lamp's pool of light on a wall, a glow that gives
no light of its own -- is light no lamp could have found, and counts.  And bounced light passes what a shadow ray passes:
anything set out with `no shadow` and the bulb round a lamp, which stand for a light rather than being lit
by one.  So a lamp's glowing bulb, or a painted sun's disc, is not counted again as a glowing thing --
and does not turn up, as it would, as bright specks wherever one bounce in thousands happened to find it.

#### A Cache of Bounced Light

Bounced light changes slowly across a surface, even where the light falling straight on it does not:
the pool under a lamp has a hard edge, but the light that pool throws onto the wall beside it is a soft
wash.  So it need not be worked out afresh at every pixel:

```
context {
    diffuse bounces 3
    diffuse samples 2048
    diffuse cache
}
```

With `diffuse cache`, the first surface a ray from the eye meets no longer looks far for its bounced
light itself.  Before the first pixel, the renderer works out the light arriving from far away at
scattered places, each looking in `diffuse samples` directions, and every pixel blends it from the
records near it that face its way.  Each record holds good for about as far as the things it looked out
at are away -- a long way in the open middle of a floor, a little way into a corner, where the light
changes faster -- and gives way smoothly to the next, so no seam shows where it does.  What a record
holds is the light arriving; each pixel's own color still takes its share of it, so a wood's grain or a
pattern's detail is as sharp as ever.

What is near, each pixel still looks for itself, in a sixteenth as many directions as a record (and
never fewer than 16): anything closer than twice the distance its records hold good for.  A record
cannot speak for that.  The floor beside a rug sees the rug's edge low on its horizon, while the floor
just under the edge sees nothing but rug, so a record lending the one's light to the other would lose
the dark line where the rug meets the floor -- and the dark where a chair leg stands on it, and the
red a rug throws on the skirting beside it.  Directions that meet nothing so near take the blended far
light instead of looking further.

| Setting | Default | What it does |
| --- | --- | --- |
| `diffuse cache` | off | Blend the first surface's bounced light from records worked out at scattered places. |
| `diffuse cache error` | 0.15 | How far the blend may stray, as a share of the light, between 0 and 1. |

**It trades grain for smoothness**, and for many more directions at the same cost.  There are thousands
of records rather than hundreds of thousands of pixels, so each can look in far more directions: a
couple of thousand is a usual `diffuse samples` with a cache, where a few hundred is a usual one without.
Too few shows not as grain but as blotches -- a slow wash of lighter and darker patches the size of the
records -- and more samples are the cure.  Blotches come first where a nook is lit mostly by something
small and bright across the room, a glowing lampshade or a lamp's pool on a wall, which only a few of a
record's directions find; the nook under a desk is the usual place to look, and a room lit that way may
want several thousand more.  A tight corner, where much
is near, shows a little grain instead, from the pixel's own look round it.  A smaller `diffuse cache error` holds each record good for
less far round it, which makes more of them and follows fine changes more closely.  The records are
worked out in passes over ever finer grids of the picture, each pass reading only what earlier ones set
down, so a picture with a cache comes out the same every time it is rendered.

### Color Depth and Grayscale

What reaches the image file, as opposed to what was rendered:

```
context {
    color depth 16
    grayscale
}
```

| Written | What it does |
| --- | --- |
| `color depth` | How many bits each channel gets in the file: `8` or `16`. |
| `grayscale` | Write the image without color. |

Eight bits is the default and is what a screen shows.  Sixteen is worth asking for when the
image is going to be worked on afterwards — graded, or stretched in contrast — because that is
where the banding an eight-bit file hides would start to show.  The PNG is written at the depth
asked for, so the file holds exactly the precision it was given rather than padding one into
the other.

A gray image is written in a gray container rather than as three equal channels, which is
smaller and says what it holds.  What is never written is a **palette**: a palette turns the
colors into indices into a table, and an image read back from one is not the image that was
written.

`-c`/`--bits-per-channel` and `--grayscale` on the command line still overrule what a scene
asks for; saying nothing leaves it alone.

### Spectral Light

Light is carried as red, green and blue unless a scene asks for it to be carried wavelength by
wavelength instead:

```
context {
    spectral
}
```

Three numbers are enough to say what color something is, but not what it does to light of
another color.  Two paints can look the same yellow under white light and quite different under a
sodium lamp, because one reflects a narrow band of yellow and the other a broad mix of red and
green that only adds up to yellow.  Carried as red, green and blue, light can only be multiplied
channel by channel, which treats every paint as if it were the broad kind.  Carried wavelength by
wavelength, colored light on colored surfaces, light through colored glass, and light through
colored fog or water come out as they physically would.

Nothing has to be written differently.  Colors are still written as colors, and each is turned
into the smoothest spectrum that is that color — a surface's never reflecting more than all the
light at any wavelength, and a dark one dark at all of them — so a scene under white light looks as
it always did.  The differences are
where light of one color meets a surface of another.  The physical sky is already worked out
wavelength by wavelength, and a spectral render takes it as it was worked out rather than as the
color it comes to.

It costs more: a fraction of a second to start, while the table that turns colors into spectra is
built, and then more per ray, since every ray carries thirty-two numbers rather than three.  Across
the gallery that came to a seventh more time in all; most scenes hardly notice, and those that
march light through thick media take about twice as long, every band needing its own reckoning of
what the medium lets through.

`--spectral` on the command line asks for it too.

### White Balance

A camera has to be told what white is, and so does this renderer.  Left alone, white is light of the
same strength at every wavelength, which is noon sunlight at about 5500 K — how a film balanced for
daylight sees.  Outdoors that is right.  Indoors it is not: a household bulb glows at about 2800 K, and
on daylight film a room lit by one comes out deeply orange.  That is a faithful photograph, and nothing
like what the room looks like to someone standing in it, whose eyes have adjusted to the lamps.

```
context {
    white balance 3400
}
```

**`white balance`** names the [temperature](lights.md#color-temperature) of a glow that should come out
white, and the whole picture is shifted to match, the way a photographer balances a camera for the
light in the room.  Balanced a little above the lamps themselves, as here, a lamp-lit room reads warm
rather than orange; balanced right at them, it reads neutral.  Everything else moves too, as it does in
a photograph: daylight through the windows of that room turns blue, and balanced for candlelight, a
candle's flame comes out white.

| Balance | Use |
| --- | --- |
| 2500 | Candlelight that still looks warm. |
| 3400 | A room lit by household bulbs, warm but not orange. |
| 5500 | Daylight — the same as leaving it out. |

The balance is applied to the finished picture, in red, green and blue and in a
[spectral](#spectral-light) render alike, so it costs nothing to render.  `--white-balance` on the
command line overrules what a scene asks for.

### Image Size

A scene may fix the size of the image it wants:

```
context {
    width 1200
    height 1200
}
```

That is useful when a scene only makes sense at a particular shape — cover art that wants to
be square, say, or a panorama that wants to be wide.  `gallery/challenge-book/cover.igl` asks
for `height 800` for exactly that reason.

Either dimension may be settled on its own, and the other falls back.  As with everything
here that the command line can also set, `-w` and `-h` win when they are given, this block is
consulted next, and 800 by 600 is the last resort — see
[How a setting is decided](getting-started.md#how-a-setting-is-decided).

