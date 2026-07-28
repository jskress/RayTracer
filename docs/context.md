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

Antialiasing is set from the command line rather than from the context block, with
`-a`/`--antialias`; see
[Command Line Options](getting-started.md#how-big-and-how-good).

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

