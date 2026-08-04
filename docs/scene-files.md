## Scene Files

A scene file is a plain text file, conventionally given the extension `.igl`.  It is read
from top to bottom, and what it contains is a series of *items* — cameras, some lights, the
surfaces of the world, whole scenes, and whatever settings you care to fix.

### The Shape of a File

Order mostly doesn't matter.  The file is not a program being executed in sequence; it is a
specification of what to render.  The renderer waits until it has read the whole thing
before it draws anything.  A light written after the surfaces lights them just the same as
one written before.

Everything in a scene file can carry a name.  For cameras and scenes, this is important
since you'll have to tell the renderer which camera in which scene to use (but only if you
have more than one of either).  The names of things must not be confused with *variables*,
which are also supported.  Variables do need to be assigned a value before they can be
referenced.  So, they must appear in the file before they are used.  The value can be as
complex an expression as you need, but it won't actually be *evaluated* until render time.
Similarly, a file must be included above the point where you rely on what is in it.  This
is the one way order matters.

Everything else — cameras, lights, surfaces — may appear in any order.  Just remember that
a file with no lights renders black, which is probably not what you want.

### Scenes and Cameras

Most of the time a file describes a single picture, and everything in it — the camera, the
lights, the surfaces — simply sits at the top level, as in every example so far.  That is a
convenience.  What is really being described is a *scene*, and a file may hold more than one
of those.

#### More than one camera

A scene may hold as many cameras as you like.  With just one, it is used without your having
to say so.  With more than one, the renderer cannot guess which you mean, so two things
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

The choice can also be made from the command line, with `--scene` and `--camera`, which take
precedence over anything the file says.  That lets one file be rendered from any of its cameras,
or as any of its scenes, without editing it — and lets a file with several of either be
rendered without a `render` command at all.  See
[Command Line Options](getting-started.md#command-line-options).

### Background

A ray that strikes nothing still has to come back with *some* color.  By default that color is
transparent, so the pixels a scene does not fill come out with no color and no opacity — which
is handy when the image is to be laid over something else later.  `background` sets what is
returned there instead:

```
background Black
background [0.05, 0.06, 0.09]
```

It takes a color — a pigment, strictly, though for an ordinary camera that reads as a single
flat backdrop.  A background is not a surface: nothing lights it and it casts nothing.  But it
is what *any* ray returns on striking nothing, not just those from the camera, so a mirror
aimed at empty sky shows it too.

`background` may sit at the top level, or inside a `scene { }` block, so that two scenes may
carry skies of their own:

```
scene {
    named 'day'
    background [0.5, 0.7, 0.95]
    camera { location [0, 2, -6]  look at [0, 1, 0] }
    // ... lights and surfaces ...
}
```

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

Any value may be given a name, and used afterward wherever that value would go:

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

#### Functions

An expression may call a function, and the arguments are expressions in their own right, so
calls nest and compose with the arithmetic around them:

```
extrusion {
    path { … }
    min Y 0  max Y sqrt(depth² + 1)
}
```

| | |
| --- | --- |
| **Powers and roots** | `sqrt` `cbrt` `pow` `exp` `log` `log10` |
| **Whole numbers** | `floor` `ceil` `round` `trunc` `sign` `abs` `mod` |
| **Ranges and blends** | `min` `max` `clamp` `lerp` `smoothstep` |
| **Angles** | `sin` `cos` `tan` `asin` `acos` `atan` `atan2` `sinh` `cosh` `tanh` `toDegrees` |
| **Vectors** | `length` (or `magnitude`) `dot` `cross` `normalize` `distance` |
| **Noise** | `noise` |

Several take either numbers or vectors, and which they mean follows from what you hand them:
`abs`, `min`, `max`, `clamp` and `lerp` all work on both, and `min` and `max` given a *single*
vector return its smallest or largest component.  Ask for something that does not exist and
the answer says what does — `dot(1, 2)` reports that `dot` takes `(vector, vector)`.

Two of them are worth a note.  `mod` is not the `%` operator: `%` takes its sign from the
number being divided, so `-1 % 4` is -1, while `mod(-1, 4)` is 3.  That is the one that tiles,
since a pattern repeating along an axis wants the same thing either side of the origin.  And
`noise` is a single layer of the same smooth, repeatable field the patterns draw on, between 0
and 1; layering it is something you can now write for yourself, which is the point of having
it as a function:

```
grain = noise(p) + noise(p * 2) / 2 + noise(p * 4) / 4
```

#### Angles are radians

The trigonometric functions work in radians, whichever way `angles are` has been set — that
setting turns the numbers written in *clauses*, and degrees are its default, so `sin(90)` is
emphatically not 1.  Say which you mean with the postfix `degrees` and `°` operators, or reach
for `π`, which is always defined:

```
sin(90°)          // 1
sin(90 degrees)   // the same thing
sin(π / 2)        // and again
1.5 radians       // says out loud what was already true
toDegrees(π)      // 180, for going back the other way
```

Take care not to write `rotate Y 45 degrees`.  A `rotate` clause already reads its angle in
whatever unit `angles are` says, so that would convert to radians and then be read as degrees
all over again.  The postfix operators belong where radians are wanted, which is inside the
functions above.

#### Mathematical symbols

So that a formula can be pasted in as it was written rather than translated first, the
mathematical symbols work as operators:

| Symbol | Means | Also accepted as |
| --- | --- | --- |
| `√` `∛` | Square and cube roots — `√(x² + y²)` | |
| `⁰` `¹` `⁴`…`⁹` | Raises to that power, as `²` and `³` do | |
| `⋅` | Dot product of two vectors, otherwise multiplication | `·` `∙` `•` |
| `×` | Cross product of two vectors, otherwise multiplication | `⨯` |
| `÷` | Division | `∕` `⁄` |
| `−` | Subtraction, or a negative sign | `–` |
| `∗` | Multiplication | `⋆` |
| `°` | The number before it is in degrees | |
| `≤` `≥` `≠` | Comparisons | `<=` `>=` `!=` |
| `∧` `∨` `¬` | And, or, not | `&&` `\|\|` `!`, or `and` `or` `not` |

The right-hand column is not a list of near-misses to be avoided: those spellings mean exactly
the same thing.  One operation reaches a page as several different characters depending on
where the page came from, and telling them apart by eye is hopeless, so they are all accepted.

A root takes only what immediately follows it, so `√4 * 40` is `(√4) * 40`; write `√(4 * 40)`
if you meant the other.  A power written straight against another — `x¹⁰` — is refused rather
than quietly read as `x` to the first and then to the zeroth; use `pow(x, 10)`, or parentheses
if you really did mean a power of a power, as in `(x²)³`.

#### Choosing between values

Comparisons produce true or false, `and`, `or` and `not` combine them, and a conditional picks
between two values:

```
sides = 6
angle = sides > 4 ? 360 / sides : 90
```

The comparisons are `<`, `<=`, `>`, `>=`, `==` and `!=`.  Numbers and text may be compared any
of the six ways; anything else may only be asked whether it is equal, since there is no order
to put two colors in.  Two numbers count as equal when they are near enough to each other,
which is how the rest of the ray tracer treats them — so `0.1 + 0.2 == 0.3` is true here, as it
ought to be and as plain floating point would not have it.

`and` binds tighter than `or`, comparisons bind tighter than either, and only the side that is
needed is evaluated: in `size != 0 && 10 / size < 1` the division is never reached when `size`
is zero.  The same is true of a conditional, which evaluates only the side it chose.

#### What binds tightest

From tightest to loosest:

1. `²` `³` and the other powers, `°`, `degrees`, `radians`
2. `√` `∛`, a negative sign, `not`
3. `*` `/` `%` `⋅` `×`
4. `+` `-`
5. `<` `<=` `>` `>=` `==` `!=`
6. `and`
7. `or`
8. `? :`

Powers binding tighter than a negative sign is what makes `-x²` mean `-(x²)`, as it does in
print, and `√x²` mean `√(x²)`.  Parentheses override all of it.

Strings are also supported and may be delimited by either a single or double quote.  Standard
character escaping is supported.  If you want to interpolate values into a string, precede
the first delimiter with the `$` operator and use the `${_name_}` style of variable notation.
When the expression is evaluated, the current value of the named variable will be substituted
into the string.  Here's an example:

```
context {
    info {
        title $'Chapter ${chapter}, ${title}'
    }
}
```

The Challenge book gallery scenes show how that is all wired together.

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
may be quite large.  Libraries converted from POVRay are large because of all the definitions
that POVRay ships with.  One scene will almost never want everything in a library file.

The libraries themselves are managed with the `libraries` verb, which is covered in
[Using Libraries](libraries.md).
