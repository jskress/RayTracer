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
different questions.

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
`gallery/Local/functions/a-street-after-dark.igl` does exactly that, and records the
measurement in a comment: its lamps turn out to be doing about 98% of the work.

There is a related default worth knowing.  A material that never mentions ambient is given
0.1, unless the scene contains a [sky light](lights.md), in which case it is given 0 — a sky
light delivers the surrounding light for real, so the stand-in for it would be counted twice.
Both are settled before this scaling is applied.

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

