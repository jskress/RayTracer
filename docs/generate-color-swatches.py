#!/usr/bin/env python3
"""Generates a small rounded-rectangle SVG swatch for every named color, and the reference's
color table that shows each swatch beside its name.

The colors, and their RGB values, are read straight from Graphics/Colors.cs, so this stays in
step with the source.  Run it from the repository root:

    python3 docs/generate-color-swatches.py

It writes docs/images/swatches/<Name>.svg for each color and prints the table markdown, which
goes into the Colors section of docs/reference.md.  Because each row points at its swatch file,
the "every picture is there" documentation test guarantees a swatch exists for every color.
"""

import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
COLORS = os.path.join(ROOT, "Graphics", "Colors.cs")
SWATCHES = os.path.join(HERE, "images", "swatches")


def read_colors():
    """Returns [(name, '#rrggbb')] in source order, resolving the Grey-style aliases."""
    text = open(COLORS).read()
    rgb = {}
    derived = {}                    # name -> (base, factor), resolved after the literals are in
    order = []
    for name, body in re.findall(
            r'public static readonly Color ([A-Za-z0-9]+) = ([^;]+);', text):
        order.append(name)
        body = body.strip()
        literal = re.match(r'new\s*\(\s*([0-9.]+)\s*,\s*([0-9.]+)\s*,\s*([0-9.]+)', body)
        scaled = re.match(r'([A-Za-z0-9]+)\s*\*\s*([0-9.]+)$', body)     # e.g. White * 0.30
        if literal:
            rgb[name] = tuple(float(x) for x in literal.groups())
        elif scaled:
            derived[name] = (scaled.group(1), float(scaled.group(2)))
        else:                                                           # a plain alias
            derived[name] = (body, 1.0)
    for name, (base, factor) in derived.items():
        rgb[name] = tuple(c * factor for c in rgb[base])
    return [(name, to_hex(rgb[name])) for name in order]


def to_hex(triple):
    return "#" + "".join(f"{max(0, min(255, round(c * 255))):02x}" for c in triple)


def write_swatch(name, hex_color):
    # A subtle, half-transparent gray stroke keeps a white swatch visible on a white page and a
    # black one visible in dark mode, without picking a side.
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" '
           f'viewBox="0 0 13 13">'
           f'<rect x="0.5" y="0.5" width="12" height="12" rx="3" ry="3" '
           f'fill="{hex_color}" stroke="#88888899" stroke-width="1"/></svg>\n')
    with open(os.path.join(SWATCHES, f"{name}.svg"), "w", encoding="utf-8") as handle:
        handle.write(svg)


def table(colors, columns=4):
    cell = ('<img src="images/swatches/{n}.svg" width="13" height="13" alt=""> `{n}`').format
    names = [c[0] for c in colors]
    rows = (len(names) + columns - 1) // columns
    grid = [["" for _ in range(columns)] for _ in range(rows)]
    for i, name in enumerate(names):        # column-major, so it reads down each column
        grid[i % rows][i // rows] = cell(n=name)
    out = ["| | | | |", "| --- | --- | --- | --- |"]
    out += ["| " + " | ".join(r) + " |" for r in grid]
    return "\n".join(out)


def main():
    os.makedirs(SWATCHES, exist_ok=True)
    colors = read_colors()
    for name, hex_color in colors:
        write_swatch(name, hex_color)
    print(f"Wrote {len(colors)} swatches to {os.path.relpath(SWATCHES, ROOT)}", file=sys.stderr)
    print(table(colors))


if __name__ == "__main__":
    main()
