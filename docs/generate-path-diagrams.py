#!/usr/bin/env python3
"""
Draws the 2D paths used by the extrusion and lathe examples, so a reader can see the outline
that produces each rendered solid.

These are hand-drawn SVGs rather than railroad diagrams, so they are not produced by
generate-diagrams.sh.  Run this instead:

    python3 docs/generate-path-diagrams.py

For each diagram it writes docs/images/figures/<name>.svg and <name>-dark.svg, matching the
light/dark pairing the rest of the documentation uses.
"""

import os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "images", "figures")

LIGHT = {
    "axis": "#8a8a8a", "tick": "#b0b0b0", "label": "#555555",
    "outline": "#c25a33", "hole": "#3a6ea8", "fill": "#c25a3322",
    "point": "#c25a33", "control": "#9a9a9a", "note": "#333333",
}
DARK = {
    "axis": "#8a8a8a", "tick": "#5a5a5a", "label": "#aaaaaa",
    "outline": "#e08a5a", "hole": "#6aa6e0", "fill": "#e08a5a22",
    "point": "#e08a5a", "control": "#888888", "note": "#dddddd",
}


class Diagram:
    """Builds one SVG, mapping path coordinates (Y up) onto SVG coordinates (Y down)."""

    def __init__(self, x_range, y_range, scale=70, pad=34, foot=0):
        self.x0, self.x1 = x_range
        self.y0, self.y1 = y_range
        self.scale = scale
        self.pad = pad
        self.foot = foot
        self.width = (self.x1 - self.x0) * scale + pad * 2
        self.height = (self.y1 - self.y0) * scale + pad * 2 + foot

    def px(self, x):
        return self.pad + (x - self.x0) * self.scale

    def py(self, y):
        return self.pad + (self.y1 - y) * self.scale

    def axes(self, c, x_label="X", y_label="Y", step=1.0, y_is_axis_of_revolution=False,
             number_x_ticks=True):
        import math as _m
        out = []

        def ticks(lo, hi):
            first = _m.ceil(lo / step - 1e-9)
            last = _m.floor(hi / step + 1e-9)
            return [i * step for i in range(first, last + 1)]

        # Tick marks, on whole multiples of the step, skipping the origin's own labels.
        for x in ticks(self.x0, self.x1):
            if abs(x) < 1e-9:
                continue
            out.append(f'<line x1="{self.px(x):.1f}" y1="{self.py(0)-4:.1f}" '
                       f'x2="{self.px(x):.1f}" y2="{self.py(0)+4:.1f}" '
                       f'stroke="{c["tick"]}" stroke-width="1"/>')
            if number_x_ticks:
                out.append(f'<text x="{self.px(x):.1f}" y="{self.py(0)+17:.1f}" '
                           f'fill="{c["label"]}" font-family="sans-serif" font-size="10" '
                           f'text-anchor="middle">{x:g}</text>')
        for y in ticks(self.y0, self.y1):
            if abs(y) < 1e-9:
                continue
            out.append(f'<line x1="{self.px(0)-4:.1f}" y1="{self.py(y):.1f}" '
                       f'x2="{self.px(0)+4:.1f}" y2="{self.py(y):.1f}" '
                       f'stroke="{c["tick"]}" stroke-width="1"/>')
            out.append(f'<text x="{self.px(0)-9:.1f}" y="{self.py(y)+3.5:.1f}" fill="{c["label"]}" '
                       f'font-family="sans-serif" font-size="10" text-anchor="end">{y:g}</text>')

        width = 2.2 if y_is_axis_of_revolution else 1.3
        dash = ' stroke-dasharray="6 4"' if y_is_axis_of_revolution else ""
        out.append(f'<line x1="{self.px(self.x0):.1f}" y1="{self.py(0):.1f}" '
                   f'x2="{self.px(self.x1):.1f}" y2="{self.py(0):.1f}" '
                   f'stroke="{c["axis"]}" stroke-width="1.3"/>')
        out.append(f'<line x1="{self.px(0):.1f}" y1="{self.py(self.y0):.1f}" '
                   f'x2="{self.px(0):.1f}" y2="{self.py(self.y1):.1f}" '
                   f'stroke="{c["axis"]}" stroke-width="{width}"{dash}/>')
        out.append(f'<text x="{self.px(self.x1)+6:.1f}" y="{self.py(0)+4:.1f}" fill="{c["label"]}" '
                   f'font-family="sans-serif" font-size="12" font-style="italic">{x_label}</text>')
        out.append(f'<text x="{self.px(0)-4:.1f}" y="{self.py(self.y1)-8:.1f}" fill="{c["label"]}" '
                   f'font-family="sans-serif" font-size="12" font-style="italic" '
                   f'text-anchor="middle">{y_label}</text>')
        return out

    def path(self, commands, stroke, fill="none", width=2.2):
        """commands: ('M'|'L', x, y) | ('Q', cx, cy, x, y) | ('C', ax, ay, bx, by, x, y) | ('Z',)"""
        d = []
        for cmd in commands:
            kind = cmd[0]
            if kind == "Z":
                d.append("Z")
            elif kind in ("M", "L"):
                d.append(f"{kind} {self.px(cmd[1]):.2f} {self.py(cmd[2]):.2f}")
            elif kind == "Q":
                d.append(f"Q {self.px(cmd[1]):.2f} {self.py(cmd[2]):.2f} "
                         f"{self.px(cmd[3]):.2f} {self.py(cmd[4]):.2f}")
            elif kind == "C":
                d.append(f"C {self.px(cmd[1]):.2f} {self.py(cmd[2]):.2f} "
                         f"{self.px(cmd[3]):.2f} {self.py(cmd[4]):.2f} "
                         f"{self.px(cmd[5]):.2f} {self.py(cmd[6]):.2f}")
        return [f'<path d="{" ".join(d)}" fill="{fill}" stroke="{stroke}" '
                f'stroke-width="{width}" stroke-linejoin="round"/>']

    def dot(self, x, y, c, label=None, dx=8, dy=-7, hollow=False, anchor="start"):
        out = []
        if hollow:
            out.append(f'<circle cx="{self.px(x):.1f}" cy="{self.py(y):.1f}" r="3" '
                       f'fill="none" stroke="{c["control"]}" stroke-width="1.4"/>')
        else:
            out.append(f'<circle cx="{self.px(x):.1f}" cy="{self.py(y):.1f}" r="3.6" '
                       f'fill="{c["point"]}"/>')
        if label:
            out.append(f'<text x="{self.px(x)+dx:.1f}" y="{self.py(y)+dy:.1f}" fill="{c["note"]}" '
                       f'font-family="sans-serif" font-size="10.5" '
                       f'text-anchor="{anchor}">{label}</text>')
        return out

    def note(self, x, y, text, c, anchor="start"):
        return [f'<text x="{self.px(x):.1f}" y="{self.py(y):.1f}" fill="{c["note"]}" '
                f'font-family="sans-serif" font-size="11" text-anchor="{anchor}">{text}</text>']

    def arrow(self, x0, y0, x1, y1, c, key, at=0.55, size=9):
        """A small filled arrowhead sitting on the segment (x0,y0)->(x1,y1), showing which way
        the run is drawn -- which is what fixes the facing of the extruded side walls."""
        import math as _m
        ax, ay, bx, by = self.px(x0), self.py(y0), self.px(x1), self.py(y1)
        tipx, tipy = ax + (bx - ax) * at, ay + (by - ay) * at
        back = _m.atan2(by - ay, bx - ax) + _m.pi
        lx, ly = tipx + size * _m.cos(back + 0.45), tipy + size * _m.sin(back + 0.45)
        rx, ry = tipx + size * _m.cos(back - 0.45), tipy + size * _m.sin(back - 0.45)
        return [f'<polygon points="{tipx:.1f},{tipy:.1f} {lx:.1f},{ly:.1f} {rx:.1f},{ry:.1f}" '
                f'fill="{c[key]}"/>']

    def legend(self, entries, c):
        """Lays the key out in the strip below the plot, where nothing can collide with it.

        Each entry is (text, color key, style), where style is "line" for a stroke sample or
        "hollow" for the open circle the control points are drawn with.
        """
        out = []
        top = self.height - self.foot + 12
        for i, (text, key, style) in enumerate(entries):
            y = top + i * 16
            if style == "hollow":
                out.append(f'<circle cx="{self.pad + 9:.1f}" cy="{y - 4:.1f}" r="3" '
                           f'fill="none" stroke="{c[key]}" stroke-width="1.4"/>')
            else:
                dash = ' stroke-dasharray="6 4"' if style == "dash" else ""
                out.append(f'<line x1="{self.pad:.1f}" y1="{y - 4:.1f}" '
                           f'x2="{self.pad + 18:.1f}" y2="{y - 4:.1f}" '
                           f'stroke="{c[key]}" stroke-width="2.4"{dash}/>')
            out.append(f'<text x="{self.pad + 25:.1f}" y="{y:.1f}" fill="{c["note"]}" '
                       f'font-family="sans-serif" font-size="11">{text}</text>')
        return out

    def wrap(self, body):
        return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.width:.0f}" '
                f'height="{self.height:.0f}" viewBox="0 0 {self.width:.0f} {self.height:.0f}">\n'
                + "\n".join("  " + line for line in body) + "\n</svg>\n")


def star_points():
    """The ten points of the star in the extrusion example, drawn counter-clockwise (starting
    at the top and heading left), which is the direction an extrusion wants a run in -- though
    it turns round any run drawn the other way itself."""
    return [(0, 1), (-0.23, 0.31), (-0.95, 0.31), (-0.37, -0.12), (-0.59, -0.81),
            (0, -0.38), (0.59, -0.81), (0.37, -0.12), (0.95, 0.31), (0.23, 0.31)]


def extrusion_diagram(c):
    # The x-range runs wider than the star needs so the direction note in the legend has room.
    d = Diagram((-1.55, 1.55), (-1.15, 1.28), scale=88, foot=46)
    body = d.axes(c)

    pts = star_points()
    outline = [("M", *pts[0])] + [("L", *p) for p in pts[1:]] + [("Z",)]
    body += d.path(outline, c["outline"], fill=c["fill"])

    hole = [("M", 0, 0.42), ("L", 0.25, -0.16), ("L", -0.25, -0.16), ("Z",)]
    body += d.path(hole, c["hole"], fill="none", width=2.0)

    # Arrows showing the drawing direction: the outer run counter-clockwise, the hole clockwise.
    body += d.arrow(pts[0][0], pts[0][1], pts[1][0], pts[1][1], c, "outline")
    body += d.arrow(0, 0.42, 0.25, -0.16, c, "hole")

    for i, (x, y) in enumerate(pts):
        body += d.dot(x, y, c, label="0, 1  (start)" if i == 0 else None, dx=10, dy=-8)
    for x, y in [(0, 0.42), (0.25, -0.16), (-0.25, -0.16)]:
        body += [f'<circle cx="{d.px(x):.1f}" cy="{d.py(y):.1f}" r="3.6" fill="{c["hole"]}"/>']

    body += d.legend([
        ("outer run — drawn counter-clockwise", "outline", "line"),
        ("inner run — drawn clockwise, cutting the hole", "hole", "line"),
    ], c)
    return d.wrap(body)


def lathe_diagram(c):
    d = Diagram((-0.25, 1.15), (-0.2, 2.65), scale=98, pad=38, foot=62)
    # Every X value is named by one of the points below, so the X ticks go unnumbered to keep
    # the crowded bottom-left corner clear.
    body = d.axes(c, x_label="X", y_label="Y", step=0.5, y_is_axis_of_revolution=True,
                  number_x_ticks=False)

    profile = [
        ("M", 0, 0),
        ("L", 0.75, 0),
        ("L", 0.75, 0.1),
        ("C", 0.3, 0.25, 0.2, 0.5, 0.22, 0.9),
        ("C", 0.28, 1.35, 0.8, 1.5, 0.85, 2.1),
        ("Q", 0.86, 2.3, 0.8, 2.45),
        ("L", 0, 2.45),
    ]
    body += d.path(profile, c["outline"])

    # The points the profile actually passes through.  Labels sit to the open side of each
    # point, anchored end when they run leftward so they never cross the axis or its ticks.
    for (x, y, label, dx, dy, anchor) in [
        (0, 0, "0, 0", 7, 16, "start"),
        (0.75, 0, "0.75, 0", 7, 16, "start"),
        (0.75, 0.1, "0.75, 0.1", 9, -6, "start"),
        (0.22, 0.9, "0.22, 0.9", 9, 15, "start"),
        (0.85, 2.1, "0.85, 2.1", 9, 4, "start"),
        (0.8, 2.45, "0.8, 2.45", 9, -7, "start"),
        (0, 2.45, "0, 2.45", 7, -9, "start"),
    ]:
        body += d.dot(x, y, c, label=label, dx=dx, dy=dy, anchor=anchor)

    # The control points that shape the curves, drawn hollow to tell them apart.
    for x, y in [(0.3, 0.25), (0.2, 0.5), (0.28, 1.35), (0.8, 1.5), (0.86, 2.3)]:
        body += d.dot(x, y, c, hollow=True)

    body += d.legend([
        ("the profile — swept around the Y axis", "outline", "line"),
        ("the control points shaping each curve", "control", "hollow"),
        ("the axis of revolution", "axis", "dash"),
    ], c)
    return d.wrap(body)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, build in [("path-extrusion", extrusion_diagram), ("path-lathe", lathe_diagram)]:
        for suffix, colors in [("", LIGHT), ("-dark", DARK)]:
            path = os.path.join(OUT, f"{name}{suffix}.svg")
            with open(path, "w", encoding="utf-8") as handle:
                handle.write(build(colors))
            print(f"Wrote {os.path.relpath(path, os.path.dirname(HERE))}")


if __name__ == "__main__":
    main()
