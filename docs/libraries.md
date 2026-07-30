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
