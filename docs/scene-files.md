## Scene Files

A scene file is a plain text file, conventionally given the extension `.igl`.  It is read
from top to bottom, and what it contains is a series of *items* — a camera, some lights, the
surfaces of the world, and whatever settings you care to fix.

### The Shape of a File

Order mostly does not matter.  The file is not a program being executed in sequence; it is a
description being gathered up, and the renderer waits until it has read the whole thing
before it draws anything.  A light written after the surfaces lights them just the same as
one written before.

There are two exceptions, and both are the same exception really: a name has to be set
before it is used.  A variable must be assigned above the place it is read, and a file must
be included above the point where you rely on what is in it.  This is the one way in which a
file reads from top to bottom.

Everything else — cameras, lights, surfaces — may appear in any order.  A file with no lights
renders black, which is almost never what was wanted.

### Scenes and Cameras

Most of the time a file describes a single picture, and everything in it — the camera, the
lights, the surfaces — simply sits at the top level, as in every example so far.  That is a
convenience.  What is really being described is a *scene*, and a file may hold more than one.

#### More than one camera

A scene may hold as many cameras as you like.  With just one, it is used without your having
to say so.  With more than one, the renderer cannot guess which you meant, so two things
become necessary: each camera must be given a name, and the file must end with a `render`
command naming the camera to use.

```
camera {
    named 'wide'
    location [0, 3, -9]
    field of view 70
}

camera {
    named 'close'
    location [0, 1.5, -3]
    field of view 40
}

// ... lights and surfaces ...

render with camera 'close'
```

Leave the `render` command off with two cameras present and the render stops with *"No camera
name specified to render, and more than one camera is defined."*  The
[`render` command](#the-render-command) is described below.

#### More than one scene

In the same way, a file may describe several whole scenes, each wrapped in a `scene { }`
block with a name of its own:

```
scene {
    named 'day'
    camera { location [0, 2, -6]  look at [0, 1, 0] }
    point light { location [-8, 10, -8]  color White }
    // ... surfaces ...
}

scene {
    named 'night'
    camera { location [0, 2, -6]  look at [0, 1, 0] }
    point light { location [-8, 10, -8]  color [0.3, 0.3, 0.5] }
    // ... surfaces ...
}

render scene 'night'
```

Not writing a `scene { }` block at all is the convenience you have been using: when nothing
is wrapped in one, the whole file is taken as a single unnamed scene.  As with cameras, one
scene is used automatically, and more than one must be named and chosen with `render`.

The two conveniences do not mix, though.  Once any part of a file is wrapped in a `scene { }`
block, *everything* must be — a surface left at the top level alongside an explicit scene
stops the render with *"... definition found outside a scene definition when explicit scenes
are being used."*

### The `render` Command

When a file holds more than one scene or more than one camera, a `render` command at the end
says which to use.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/scene-files/renderClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/scene-files/renderClause.svg">
  <img alt="The render command" src="images/scene-files/renderClause.svg">
</picture>

Both parts are optional, and either may be given on its own:

```
render                              // only meaningful with one scene and one camera
render scene 'night'                // that scene, its own single camera
render with camera 'close'          // the one scene, this camera
render scene 'night' with camera 'close'
```

The camera named is looked for in the scene being rendered, so two scenes may each have a
camera called `close` without any confusion.

A file needs a `render` command only when there is a genuine choice to make.  With a single
scene and a single camera — which is most files — you may leave it off, and that is why none
of the examples so far have needed one.

### Comments

Comments are written as they are in C, C# or Java:

```
// Everything after two slashes, to the end of the line.

/* Or between these,
   however many lines it runs to. */
```

There is no comment syntax that survives into the rendered image; if you want a title or an
author recorded in the image file itself, that is what the
[`info` block](context.md#image-information) is for.

### Numbers, Points, Vectors and Colors

Numbers are written as you would expect: `1`, `-4`, `0.5`, `1e6`.

Everything with more than one component — a point in space, a direction, a color — is
written as a *tuple*, in square brackets:

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/scene-files/tuple-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/scene-files/tuple.svg">
  <img alt="A tuple" src="images/scene-files/tuple.svg">
</picture>

```
[0, 1, 0]           // a point, or a vector, or a color: it depends where you write it
[1, 0.5, 0.25]      // a color, if used where a color is wanted
[1, 0.5, 0.25, 0.5] // four components: a color with an alpha
```

The same three numbers mean a point in one place and a color in another, and the renderer
tells which from where you wrote it, not from anything in the tuple itself.  Where that would
be genuinely ambiguous you can say outright, with a cast:

```
color [1, 0, 0]
point [0, 1, 0]
vector [0, 1, 0]
```

A great many colors already have names — `White`, `Black`, `Red`, `Gray30`, `Turquoise`,
and hundreds more — and they may be used anywhere a color tuple can.

### Variables

Any value may be given a name, and used afterwards wherever that value would go:

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/scene-files/setVariableClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/scene-files/setVariableClause.svg">
  <img alt="Setting a variable" src="images/scene-files/setVariableClause.svg">
</picture>

```
radius = 1.5
shinyRed = material {
    pigment color Red
    specular 0.9
    shininess 200
}

sphere {
    material shinyRed
    scale radius
    translate [0, radius, 0]
}
```

Materials, pigments, transforms, interiors and whole surfaces can all be named this way and
reused, which is what keeps a large scene from becoming a wall of repetition.

#### A name holds one value *per type*

This is worth understanding early, because it is unusual and it is load-bearing.

A name does not hold one value; it holds one value for each *type* it has been given.  So a
color called `Turquoise` and an index of refraction called `Turquoise` can both exist, and
neither shadows the other.  Which is meant is settled by the context you write it in:

```
sphere {
    material {
        pigment color Turquoise     // the color
        interior { ior Turquoise }  // the index of refraction
    }
}
```

Both of those names come built in, along with many others, and this is how they coexist.
The same applies to names you set yourself.

### Expressions

Anywhere a value is wanted, an expression may be written instead.  The usual arithmetic
works — `+`, `-`, `*`, `/`, `%` — with the usual precedence, and parentheses to override it:

```
count = 5
spacing = 2.5

sphere { translate [(count - 1) * spacing / 2, 1, 0] }
```

Two conveniences are worth knowing: `²` and `³` square and cube what precedes them, and
variables are read *late* — an expression is evaluated when the scene is rendered, not when
the line is parsed.

#### Arithmetic on tuples needs a type first

A bare tuple has no type of its own.  `[1, 0.8, 0.6]` is just three numbers until it is used
somewhere that wants a color or a point, and only then does it become one.  That is what
lets the same tuple serve as either — but it also means arithmetic on it has nothing to work
with, and this fails:

```
base = [1, 0.8, 0.6]
dim  = base * 0.5       // Error: Cannot multiply items of type NumberTuple to those of type Double
```

Say which you mean, and it works:

```
base = color [1, 0.8, 0.6]
dim  = base * 0.5       // fine: a color may be scaled
```

Colors, points, vectors and matrices can all be combined in the ways you would expect —
color by color, color by number, point plus vector, matrix by matrix, and so on.

Giving a tuple a type costs you nothing elsewhere: a point or a vector is still a tuple of
numbers, so either may be used anywhere a bare one can.  All three of these place the sphere
in the same spot:

```
a = [0, 1, 0]
b = point [0, 1, 0]
c = vector [0, 1, 0]

sphere { translate a }
sphere { translate b }
sphere { translate c }
```

So the rule of thumb is simply to say what you mean when it helps — when you need arithmetic,
or when the reader would otherwise have to guess.

### Including Other Files

A scene may be split across files, and one file pulled into another:

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/scene-files/includeClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/scene-files/includeClause.svg">
  <img alt="Including another file" src="images/scene-files/includeClause.svg">
</picture>

```
include 'common-materials.igl'
```

An include behaves as though the contents of the named file had been typed at that point, so
everything it defines is in scope below it.  Includes may nest.

#### Importing from a library

An import is narrower, and reads from the libraries the ray tracer keeps rather than from a
path of your own:

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/scene-files/importClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/scene-files/importClause.svg">
  <img alt="Importing from a library" src="images/scene-files/importClause.svg">
</picture>

```
import 'woods'   { Wood7Material, Wood3Material }
import 'stones1' { Stone10Material }
import 'golds'   { Gold3CMaterial }
```

Where an include brings in everything a file has, an import brings in only the names you
list, and leaves the rest of the library out of scope.  That matters because the libraries
are large — they are converted from POV-Ray's own texture includes — and you rarely want all
of a file.

The libraries themselves are managed with the `libraries` verb, which is covered in
*Importing from POV-Ray*.
