## Managing Fonts

The `text` surface turns letters into real geometry: each glyph's outline becomes a path,
and the path is extruded into a solid you can light, color and reflect like anything else.
To do that the renderer needs the font's outlines, so fonts are kept in a catalog of the ray
tracer's own rather than read from wherever the operating system might keep them.

A scene names a font by family:

```
text {
    text 'The\nFaçade\nAPI'
    font 'Merriweather'
}
```

You may not have to do anything else at all.  If a named font is not already in the catalog,
the renderer tries to fetch it from [Google Fonts](https://fonts.google.com) the first time a scene asks for it.
It keeps what it finds, so a scene that names a font Google carries just works, and the font
is locally available for next time.  The catalog fills itself as you go.

The `fonts` verb is for the times that is not enough, or not what you want: to add a font that
Google does not have, to pull one down ahead of time rather than mid-render, to look at what a
face contains, to adjust its kerning, or to take one back out.  The rest of this chapter is
about that verb.

### The Font Catalog

The catalog lives under your home directory, at `.rayTracer/Fonts`, with the font files
themselves beside a `fonts.json` describing them.  It is shared across every scene you
render, so a font need only be added once.

To see what is in it:

```bash
RayTracer fonts --list
```

```
Merriweather
  Weight   Style   Glyphs  Source
  -------  ------  ------  ------
  Regular  Normal     967  Google

Tangerine
  Weight   Style   Glyphs  Source
  -------  ------  ------  ------
  Regular  Normal     231  Google
```

A *family* is the typeface as a whole — Merriweather — and a *face* is one particular cut of
it: a weight, and upright or italic.  A scene that names only a family gets the regular
upright face.

#### Naming a face

Anywhere a command wants a face rather than a family, it is written as up to three
colon-separated parts:

```
Merriweather                 the regular upright face
Merriweather:Bold            the bold upright face
Merriweather:Bold:italic     the bold italic face
Merriweather::italic         the regular italic face
```

The weights are `Thin`, `Light`, `Regular`, `Medium`, `Bold` and `Black`.  Numbers may also be
specified, but they must match the standard weight numbering used in fonts, like `700` for bold.
The third part need only begin with `i` — `:i` will do.

### Adding Font Faces

Beyond the automatic fetch a render does for itself, there are two ways to put a face into the
catalog by hand.  Neither will quietly replace one already there; pass `--overwrite` when you
mean to.

#### From Google Fonts

```bash
RayTracer fonts --fetch 'Merriweather'
RayTracer fonts --fetch 'Merriweather:Bold:italic'
```

This fetches the face from Google Fonts and stores it, exactly as a render would, but on
demand.  Doing this ahead of time is worth it when you would rather the fetch not happen in
the middle of a render.  The first render of a scene that names a font not in the catalog
pauses to go and get it, and needs the network to be there when it does.

#### From a file you already have

```bash
RayTracer fonts --import 'Cheltenham:Bold' path/to/cheltenham-bold.ttf
```

The face specification comes from `--import` and the TrueType file is given after it.  Use
this for fonts of your own, or any face Google does not carry.

A face fetched from Google and a face imported from disk are kept distinct: the catalog
remembers where each came from, and will not let an import silently overwrite a fetched face
or the other way about.  Remove the existing one first if that is really what you want.

#### Removing one

```bash
RayTracer fonts --remove 'Merriweather:Bold'
```

### Inspecting a Face

To see what glyphs a face actually carries:

```bash
RayTracer fonts --show-glyphs-for 'Tangerine'
```

```
Tangerine, Regular
  Unicode  Name       Display  Kind  Index
  -------  ---------  -------  ----  -----
  \u0021   (unknown)  '!'      Zero     87
  ...
```

This is the quickest way to find out why a character came out missing or wrong: if it is not
in the list, the face does not have it.  The index is the glyph's position within the font,
which matters mostly when you are chasing something odd.

### Kerning

Kerning is the small adjustment made to the gap between two particular letters — the classic
case being an uppercase A tucked under a V — so that the spacing looks even to the eye rather
than being merely equal.

Well-made fonts carry their own kerning, but not all do, and not always for the pairs you
care about.  The catalog therefore lets you record pairs of your own for any face.

To see what is stored for a face:

```bash
RayTracer fonts --show-kerning-for 'Merriweather'
```

To add or remove a pair:

```bash
RayTracer fonts --add-kerning-for 'Merriweather' --pair 'A:-80:V'
RayTracer fonts --remove-kerning-for 'Merriweather' --pair 'A:-80:V'
```

The pair is written as the left character, the adjustment, and the right character, separated
by colons.  The adjustment is in the font's own design units, and negative values pull the
two letters closer together — which is what kerning is usually for.  Each side must be
exactly one character.

`--pair` is meaningless on its own; it goes with one of the two commands above.

### A note on the command names

Every one of these commands has a short form as well — `-l`, `-f`, `-i`, `-g`, `-k`, `-a`,
`-d`, `-p`, `-r`, `-o` — and the short forms are often quicker to type:

```bash
RayTracer fonts -f 'Merriweather:Bold'
```
