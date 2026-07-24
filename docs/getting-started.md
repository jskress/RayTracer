## Getting Started

### Installing and Building

The ray tracer is a .NET program and needs the .NET 10 SDK or later, which you can get from
[Microsoft](https://dotnet.microsoft.com/download).  Nothing else is required.  The geometry,
the lighting and the image itself are all worked out on your own machine — with one kind of
exception: when a scene points at something that lives elsewhere, the renderer goes and gets
it.  Two things can do that, and they differ in how long what they fetch is kept.

A font not already in the [font catalog](fonts.md) is pulled from Google Fonts and **stored**,
so it is fetched once and is local from then on.  An image given as a web address rather than
a file — the source of an [image pigment](pigments-and-patterns.md) or a
[height field](advanced-surfaces.md), say — is **not** stored: it is held in memory for the
run that needs it, and downloaded again the next time you render.  Naming the same address
twice in one scene costs one download, but rendering that scene ten times costs ten.  If that
matters, fetch the image yourself and point the scene at the local copy.

Point a scene only at local files and fonts you already have, and it renders entirely offline.

Clone the repository and build it:

```bash
dotnet build
```

That produces both the renderer and its test suite.  To satisfy yourself that the build is
sound:

```bash
dotnet test
```

### Rendering a Scene

Rendering is what the program does unless told otherwise, so the `render` verb may be left
off.  These two are the same:

```bash
RayTracer -i scene.igl
RayTracer render -i scene.igl
```

> **A note on the commands in this documentation.**  Every example is written as `RayTracer`,
> as though the program had been published and put on your path.  If you are working from a
> clone instead, run it through the SDK and put `--` before the ray tracer's own arguments,
> to keep them from being read as arguments to `dotnet run`:
>
> ```bash
> dotnet run -- -i scene.igl
> ```
>
> The two are interchangeable everywhere in these chapters.

The input file is the only thing you must supply.  Said nothing else, the renderer writes a
PNG beside the scene file, named after it, at 800 by 600 — or at whatever size the scene
asked for, as the next section explains.

There are scenes to try under `gallery/`, which is where the pictures in this documentation
come from:

```bash
RayTracer -i gallery/challenge-book/chapter-16/csg.igl
```

### Command Line Options

#### How a setting is decided

Several settings can be given in two places — here on the command line, or in the scene's own
[context block](context.md).  Where both could apply, the same rule settles it every time:

1. what the command line says, if it says anything;
2. otherwise what the scene's context block asked for;
3. otherwise the built-in default.

So a scene can fix how it wants to be rendered and still be overridden for a quick look at
something, and passing no options at all gets you the defaults.  The settings that work this
way are the image's `width` and `height`, the `gamma` value, and the switches for gamma
correction and shadows.

Two things do not follow the rule, simply because they exist in only one of the two places.
Anti-aliasing, grayscale, bits per channel, the output paths, the chattiness and the animation
options are command line only; the scanner, the angle units and the `info` block are scene
only.

The on/off switches have one further wrinkle worth knowing: they only ever turn things *on*.
`--no-gamma`, `--no-shadows` and `--grayscale` force their setting when passed and leave the
scene's own choice alone when not, but there is no opposite flag — nothing on the command line
will put shadows back into a scene that wrote `no shadows`.

#### Where the image goes

| Option | What it does |
| --- | --- |
| `-i`, `--input-file` | The scene to render.  The only required option. |
| `-o`, `--output-file` | The file to write.  The format is taken from the extension. |
| `-d`, `--output-dir` | The directory to write into, keeping the scene's own name. |
| `-e`, `--output-extension` | Keep the scene's name but write this format instead. |

`--output-file` and `--output-extension` are two ways of saying the same thing, so give one
or the other and not both.

#### How big, and how good

| Option | Default | What it does |
| --- | --- | --- |
| `-w`, `--width` | 800 | The image width, in pixels. |
| `-h`, `--height` | 600 | The image height, in pixels. |
| `-a`, `--antialias` | off | Anti-aliasing; see below. |
| `-c`, `--bits-per-channel` | | How many bits each color channel gets in the output file. |

Anti-aliasing is written either as `off`, or as `adaptive` with a depth: `adaptive:5`, or
just `5`, which means the same.  Bare `-a` with nothing after it means `adaptive:5`.

The adaptive sampler fires five rays per pixel in the pattern of the five side of a die, and
where the corners disagree with the center by enough to notice, it splits that corner and
looks closer, down to the depth you allow it.  A pixel in the middle of a flat expanse costs
five rays; one on the edge of something costs more, which is where the extra rays are worth
paying for.

#### Color and light

| Option | What it does |
| --- | --- |
| `-g`, `--gamma` | The gamma correction to apply. |
| `--no-gamma` | Apply no gamma correction at all. |
| `--grayscale` | Render in shades of gray. |
| `--no-shadows` | Let nothing cast a shadow. |

#### Animation

| Option | Default | What it does |
| --- | --- | --- |
| `-r`, `--frame-rate` | 24 | Frames per second, when rendering a series. |
| `-m`, `--frame` | | Render one particular frame of an animation. |

#### Chattiness

`-l`, `--output-level` takes `quiet`, `normal`, `chatty` or `verbose` — or just their first
letters, in any case you like.  `normal` draws a progress bar; `quiet` says nothing unless
something is wrong.

### Your First Scene

A scene needs three things: something to look with, something to look at, and something to
light it by.  Leave out the light and you get a black picture, which is the most common
first surprise.

Put this in `first.igl`:

```
// Where we are looking from, and at.
camera {
    location [0, 1.5, -5]
    look at [0, 1, 0]
}

// A lamp, up and to the left.  Leave this out and the picture comes back black.
point light {
    location [-10, 10, -10]
    color White
}

// The ground.  A plane with no transform lies flat at the origin.
plane {
    material {
        pigment checker { White, Gray30 }
    }
}

// And something to look at.  A sphere is written as though it sat at the origin with a
// radius of one, then moved where it is wanted.
sphere {
    material {
        pigment color Red
        specular 0.9
        shininess 200
    }
    translate [0, 1, 0]
}
```

Then:

```bash
RayTracer -i first.igl
```

![The first scene](images/figures/first-scene.png)

It is also kept as
[`docs/examples/getting-started/first.igl`](examples/getting-started/first.igl), if you would
rather render it than retype it.

Several things there are worth pointing out, and each has a chapter of its own later.

The sphere is written as though it were at the origin with a radius of one, and then moved
where it is wanted.  Every surface works this way: it has a natural size and place, and
[transforms](transforms.md) carry it from there to wherever it belongs.  `translate` is one
of those; there are others, and they may be stacked.

`White`, `Gray30` and `Red` are named colors the renderer knows already, and there are a
great many of them.  A name may hold a different value for each type it is used as, so
`Turquoise` can be both a color and an index of refraction without the two colliding; which
one is meant is settled by where you write it.  [Scene Files](scene-files.md) covers this.

`specular` and `shininess` say how the sphere takes the light: the size and strength of its
highlight.  They belong to its [material](materials.md), along with everything else about
how it is colored and lit.

The floor's color comes from a `checker` pattern rather than a single color.  Patterns are
functions of position, and [pigments](pigments-and-patterns.md) are how a surface samples
them.

Nothing in this scene says how large the image should be, what anti-aliasing to use, or
whether to correct gamma.  Those can be given on the command line, as above, or written into
the scene itself so it always renders the same way — that is what the
[context block](context.md) is for.
