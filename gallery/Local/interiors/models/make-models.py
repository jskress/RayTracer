#!/usr/bin/env python3
"""
Makes the models in this folder: Wavefront .obj files, the .mtl libraries they name, and the pictures those lay on
them.  Nothing here was downloaded; every model is written out below, in the right-handed space a modeling tool
works in, with its front toward +Z, as such a tool would write it.  The ray tracer turns each the right way about
as it reads it.

    python3 make-models.py

needs Pillow, and the Arial Bold and Impact faces macOS ships, for the lettering on the crate.  It draws with fixed
seeds, so it writes the same files every time.

    crate   a packing crate: planks with a bump map of their joints, and its front stenciled.  Two materials.
    die     a die: one picture holding all six faces, each face laid on its own part of it.
    barrel  a barrel: bulging staves wrapped round it, iron hoops in a metal material, and a lid of boards.
    plant   a potted plant: a terracotta pot, and leaves whose outline is their picture's alpha.
"""

import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = '/System/Library/Fonts/Supplemental'


class Mesh:
    """A model being written: vertices, texture coordinates, normals and faces, each counted from one."""

    def __init__(self):
        self.lines = []
        self.vertices = self.textures = self.normals = 0

    def vertex(self, x, y, z):
        self.lines.append(f'v {x:.5f} {y:.5f} {z:.5f}')
        self.vertices += 1
        return self.vertices

    def texture(self, u, v):
        self.lines.append(f'vt {u:.5f} {v:.5f}')
        self.textures += 1
        return self.textures

    def normal(self, x, y, z):
        length = math.sqrt(x * x + y * y + z * z) or 1
        self.lines.append(f'vn {x / length:.5f} {y / length:.5f} {z / length:.5f}')
        self.normals += 1
        return self.normals

    def face(self, corners):
        self.lines.append('f ' + ' '.join(f'{v}/{t}/{n}' for v, t, n in corners))

    def use(self, material):
        self.lines.append(f'usemtl {material}')

    def group(self, name):
        self.lines.append(f'g {name}')

    def write(self, name, library, remark):
        with open(os.path.join(HERE, f'{name}.obj'), 'w') as file:
            file.write(f'# {remark}\n# Written by make-models.py.\nmtllib {library}\n')
            file.write('\n'.join(self.lines) + '\n')


def quad(mesh, origin, across, up, uv=((0, 0), (1, 0), (1, 1), (0, 1))):
    """A square face from a corner and two edges, facing along across x up, its picture laid by uv."""
    ox, oy, oz = origin
    ax, ay, az = across
    ux, uy, uz = up
    nx, ny, nz = ay * uz - az * uy, az * ux - ax * uz, ax * uy - ay * ux
    n = mesh.normal(nx, ny, nz)
    places = [(ox, oy, oz), (ox + ax, oy + ay, oz + az), (ox + ax + ux, oy + ay + uy, oz + az + uz),
              (ox + ux, oy + uy, oz + uz)]
    mesh.face([(mesh.vertex(*p), mesh.texture(*t), n) for p, t in zip(places, uv)])


def cube_faces(h):
    """The six faces of a cube from -h to h, each as (name, corner, across, up), all facing out."""
    s = 2 * h
    return [
        ('front', (-h, -h, h), (s, 0, 0), (0, s, 0)),
        ('back', (h, -h, -h), (-s, 0, 0), (0, s, 0)),
        ('right', (h, -h, h), (0, 0, -s), (0, s, 0)),
        ('left', (-h, -h, -h), (0, 0, s), (0, s, 0)),
        ('top', (-h, h, h), (s, 0, 0), (0, 0, -s)),
        ('bottom', (-h, -h, -h), (s, 0, 0), (0, 0, s)),
    ]


def save(image, name):
    image.save(os.path.join(HERE, name))


def library(name, text):
    with open(os.path.join(HERE, name), 'w') as file:
        file.write('# Written by make-models.py.\n' + text.strip() + '\n')


# -- The crate --------------------------------------------------------------------------------------

def wood(size, planks, seed, base=(150, 108, 66), gap=(52, 36, 22), frame=True):
    """A face of boards: planks across with dark joints, grain along them, a frame round the edge."""
    random.seed(seed)
    image = Image.new('RGB', (size, size), base)
    heights = Image.new('L', (size, size), 255)
    draw, bump = ImageDraw.Draw(image), ImageDraw.Draw(heights)
    width = size / planks

    for plank in range(planks):
        top = int(plank * width)
        shade = random.uniform(-18, 18)
        tone = tuple(int(c + shade) for c in base)
        draw.rectangle((0, top, size, int(top + width)), fill=tone)

        # Grain: long faint streaks along the plank, darker and lighter.
        for _ in range(int(width * 1.5)):
            y = top + random.uniform(2, width - 2)
            x0 = random.uniform(-size * 0.2, size)
            length = random.uniform(size * 0.2, size * 0.7)
            d = random.uniform(-24, 14)
            draw.line((x0, y, x0 + length, y + random.uniform(-1.5, 1.5)),
                      fill=tuple(int(c + shade + d) for c in base), width=1)

        # The joint below each plank.
        draw.rectangle((0, int(top + width) - 3, size, int(top + width) + 1), fill=gap)
        bump.rectangle((0, int(top + width) - 4, size, int(top + width) + 2), fill=40)

    if frame:
        edge = int(size * 0.09)
        frame_tone = tuple(int(c * 0.8) for c in base)
        for box in ((0, 0, size, edge), (0, size - edge, size, size), (0, 0, edge, size), (size - edge, 0, size, size)):
            draw.rectangle(box, fill=frame_tone)
            bump.rectangle(box, fill=255)
        for box in ((edge - 3, edge - 3, size - edge + 3, edge), (edge - 3, size - edge, size - edge + 3, size - edge + 3),
                    (edge - 3, edge, edge, size - edge), (size - edge, edge, size - edge + 3, size - edge)):
            draw.rectangle(box, fill=gap)
            bump.rectangle(box, fill=60)
        # Nails at the frame's corners.
        for cx in (edge / 2, size - edge / 2):
            for cy in (edge / 2, size - edge / 2):
                draw.ellipse((cx - 4, cy - 4, cx + 4, cy + 4), fill=(70, 66, 60))

    return image.filter(ImageFilter.GaussianBlur(0.6)), heights.filter(ImageFilter.GaussianBlur(1.2))


def crate():
    size = 512
    planks, heights = wood(size, 5, seed=3)
    save(planks, 'crate-wood.png')
    save(heights, 'crate-bump.png')

    # The front, stenciled: the same boards, lettered in worn red and black.
    label = planks.copy()
    ink = Image.new('L', (size, size), 0)
    draw = ImageDraw.Draw(ink)
    big = ImageFont.truetype(os.path.join(FONTS, 'Impact.ttf'), 92)
    small = ImageFont.truetype(os.path.join(FONTS, 'Arial Bold.ttf'), 34)
    draw.text((size / 2, size * 0.40), 'FRAGILE', font=big, fill=255, anchor='mm')
    draw.text((size / 2, size * 0.62), 'THIS WAY UP', font=small, fill=255, anchor='mm')
    # An arrow pointing up.
    draw.polygon([(size / 2, size * 0.69), (size / 2 - 26, size * 0.76), (size / 2 + 26, size * 0.76)], fill=255)
    draw.rectangle((size / 2 - 8, size * 0.76, size / 2 + 8, size * 0.82), fill=255)

    # Worn: the ink broken up by speckle, as a stencil sprayed on boards wears.
    random.seed(11)
    wear = Image.new('L', (size, size), 255)
    wear_draw = ImageDraw.Draw(wear)
    for _ in range(2600):
        x, y, r = random.uniform(0, size), random.uniform(0, size), random.uniform(1, 4)
        wear_draw.ellipse((x - r, y - r, x + r, y + r), fill=0)
    wear = wear.filter(ImageFilter.GaussianBlur(0.8)).point(lambda value: 255 if value > 128 else 0)
    ink = Image.composite(ink, Image.new('L', (size, size), 0), wear)
    red = Image.new('RGB', (size, size), (150, 30, 24))
    label.paste(red, (0, 0), ink)
    save(label, 'crate-label.png')

    mesh = Mesh()
    mesh.group('Crate')
    for name, corner, across, up in cube_faces(0.5):
        mesh.use('CrateLabel' if name == 'front' else 'Crate')
        quad(mesh, corner, across, up)
    mesh.write('crate', 'crate.mtl', 'A packing crate, a unit cube about its middle, its stenciled front toward +Z.')

    library('crate.mtl', '''
newmtl Crate
Kd 1 1 1
Ks 0.04 0.04 0.04
Ns 8
map_Kd crate-wood.png
bump -bm 0.5 crate-bump.png

newmtl CrateLabel
Kd 1 1 1
Ks 0.04 0.04 0.04
Ns 8
map_Kd crate-label.png
bump -bm 0.5 crate-bump.png
''')


# -- The die ----------------------------------------------------------------------------------------

def die():
    cell = 256
    image = Image.new('RGB', (cell * 3, cell * 2), (244, 241, 233))
    draw = ImageDraw.Draw(image)
    spots = {
        1: [(0, 0)], 2: [(-1, -1), (1, 1)], 3: [(-1, -1), (0, 0), (1, 1)],
        4: [(-1, -1), (1, -1), (-1, 1), (1, 1)], 5: [(-1, -1), (1, -1), (0, 0), (-1, 1), (1, 1)],
        6: [(-1, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (1, 1)],
    }
    # Which cell of the picture each face is laid on, by its count: (column, row), row nought at the top.
    cells = {1: (0, 0), 2: (1, 0), 3: (2, 0), 4: (0, 1), 5: (1, 1), 6: (2, 1)}

    for count, (column, row) in cells.items():
        x0, y0 = column * cell, row * cell
        # A faint shadow toward the cell's edge, as a die's rounded edge darkens.
        for inset in range(0, 14):
            tone = 244 - (14 - inset) * 3
            draw.rectangle((x0 + inset, y0 + inset, x0 + cell - inset, y0 + cell - inset),
                           outline=(tone, tone - 2, tone - 8))
        for sx, sy in spots[count]:
            cx, cy = x0 + cell / 2 + sx * cell * 0.26, y0 + cell / 2 + sy * cell * 0.26
            radius = cell * (0.1 if count == 1 else 0.075)
            draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius),
                         fill=(170, 24, 30) if count == 1 else (24, 24, 28))

    save(image.filter(ImageFilter.GaussianBlur(0.7)), 'die.png')

    # Opposite faces add to seven: one at the front, six behind; two on top, five beneath; three right, four left.
    counts = {'front': 1, 'back': 6, 'top': 2, 'bottom': 5, 'right': 3, 'left': 4}
    mesh = Mesh()
    mesh.group('Die')
    mesh.use('Die')
    for name, corner, across, up in cube_faces(0.5):
        column, row = cells[counts[name]]
        u0, u1 = column / 3, (column + 1) / 3
        v0, v1 = 1 - (row + 1) / 2, 1 - row / 2
        quad(mesh, corner, across, up, ((u0, v0), (u1, v0), (u1, v1), (u0, v1)))
    mesh.write('die', 'die.mtl', 'A die, a unit cube about its middle, its one toward +Z and its two on top.')

    library('die.mtl', '''
newmtl Die
Kd 1 1 1
Ks 0.5 0.5 0.5
Ns 140
map_Kd die.png
''')


# -- The barrel -------------------------------------------------------------------------------------

def barrel():
    random.seed(5)
    width, height = 1024, 512
    staves, seams = 24, Image.new('L', (width, height), 255)
    image = Image.new('RGB', (width, height))
    draw, bump = ImageDraw.Draw(image), ImageDraw.Draw(seams)

    for stave in range(staves):
        x0, x1 = stave * width / staves, (stave + 1) * width / staves
        tone = (int(118 + random.uniform(-16, 16)), int(80 + random.uniform(-12, 12)), int(46 + random.uniform(-8, 8)))
        draw.rectangle((x0, 0, x1, height), fill=tone)
        for _ in range(60):
            x = random.uniform(x0 + 2, x1 - 2)
            d = random.uniform(-22, 12)
            draw.line((x, random.uniform(-50, height), x + random.uniform(-2, 2), random.uniform(0, height + 50)),
                      fill=tuple(int(c + d) for c in tone))
        draw.rectangle((x1 - 2, 0, x1 + 1, height), fill=(48, 32, 18))
        bump.rectangle((x1 - 3, 0, x1 + 2, height), fill=50)

    save(image.filter(ImageFilter.GaussianBlur(0.6)), 'barrel-staves.png')
    save(seams.filter(ImageFilter.GaussianBlur(1.5)), 'barrel-bump.png')

    lid = Image.new('RGB', (512, 512), (112, 78, 46))
    lid_draw = ImageDraw.Draw(lid)
    for board in range(1, 5):
        lid_draw.line((0, board * 512 / 5, 512, board * 512 / 5), fill=(50, 34, 20), width=4)
    for _ in range(300):
        y = random.uniform(0, 512)
        lid_draw.line((random.uniform(-100, 512), y, random.uniform(0, 600), y + random.uniform(-2, 2)),
                      fill=(int(112 + random.uniform(-20, 10)), int(78 + random.uniform(-14, 8)), 46))
    save(lid.filter(ImageFilter.GaussianBlur(0.6)), 'barrel-lid.png')

    tall, sides, rows = 1.1, 32, 10

    def radius(y):
        return 0.40 + 0.07 * (1 - (2 * y / tall - 1) ** 2)

    def slope(y):
        return 0.07 * -2 * (2 * y / tall - 1) * 2 / tall

    def band(mesh, y0, y1, out, rows_of, textured):
        """A surface of revolution between two heights, its staves' picture wrapped once round it."""
        heights = [y0 + (y1 - y0) * j / rows_of for j in range(rows_of + 1)]
        ring = {}
        for j, y in enumerate(heights):
            for i in range(sides + 1):
                angle = 2 * math.pi * i / sides
                r = radius(y) + out
                # Round toward -Z as the angle grows, so that each face is wound to face out.
                c, s = math.cos(angle), -math.sin(angle)
                ring[(i, j)] = (mesh.vertex(r * c, y, r * s),
                                mesh.texture(i / sides, (y - y0) / (y1 - y0) if textured else 0),
                                mesh.normal(c, -slope(y), s))
        for j in range(rows_of):
            for i in range(sides):
                mesh.face([ring[(i, j)], ring[(i + 1, j)], ring[(i + 1, j + 1)], ring[(i, j + 1)]])

    mesh = Mesh()
    mesh.group('Staves')
    mesh.use('Staves')
    band(mesh, 0, tall, 0, rows, True)
    mesh.group('Hoops')
    mesh.use('Iron')
    for y in (0.1, 0.32, 0.74, 0.96):
        band(mesh, y, y + 0.05, 0.008, 1, False)

    mesh.group('Lids')
    mesh.use('Lid')
    for y, up in ((tall - 0.03, 1), (0.03, -1)):
        r = radius(y) - 0.012
        middle = (mesh.vertex(0, y, 0), mesh.texture(0.5, 0.5), mesh.normal(0, up, 0))
        rim = []
        for i in range(sides + 1):
            angle = 2 * math.pi * i / sides
            c, s = math.cos(angle), -math.sin(angle)
            rim.append((mesh.vertex(r * c, y, r * s), mesh.texture(0.5 + 0.5 * c, 0.5 - 0.5 * s), middle[2]))
        for i in range(sides):
            mesh.face([middle, rim[i], rim[i + 1]] if up > 0 else [middle, rim[i + 1], rim[i]])
    mesh.write('barrel', 'barrel.mtl', 'A barrel 1.1 tall, standing on the origin.')

    library('barrel.mtl', '''
newmtl Staves
Kd 1 1 1
Ks 0.05 0.05 0.05
Ns 10
map_Kd barrel-staves.png
bump -bm 0.6 barrel-bump.png

newmtl Iron
Kd 0.13 0.12 0.11
Ks 0.35 0.35 0.35
Ns 40
Pm 1
Pr 0.45

newmtl Lid
Kd 1 1 1
Ks 0.04 0.04 0.04
Ns 8
map_Kd barrel-lid.png
''')


# -- The plant --------------------------------------------------------------------------------------

def plant():
    random.seed(9)
    leaf = Image.new('RGBA', (128, 512), (0, 0, 0, 0))
    draw = ImageDraw.Draw(leaf)

    # A long pointed leaf: its outline the alpha, deeper green along the middle, a pale midrib down it.
    outline = []
    for k in range(65):
        t = k / 64
        half = 60 * math.sin(math.pi * t) ** 0.8 * (1 - 0.35 * t)
        outline.append((64 - half, 512 - t * 512))
    outline += [(128 - x, y) for x, y in reversed(outline)]
    draw.polygon(outline, fill=(52, 104, 40, 255))
    for k in range(40):
        t = k / 40
        y = 512 - t * 512
        draw.line((64, y, 64 + 50 * math.sin(math.pi * t) * (1 - 0.35 * t), y - 30), fill=(70, 128, 52, 255), width=2)
        draw.line((64, y, 64 - 50 * math.sin(math.pi * t) * (1 - 0.35 * t), y - 30), fill=(70, 128, 52, 255), width=2)
    draw.line((64, 512, 64, 0), fill=(150, 180, 110, 255), width=4)
    save(leaf.filter(ImageFilter.GaussianBlur(0.8)), 'plant-leaf.png')

    pot = Image.new('RGB', (512, 256), (160, 84, 52))
    pot_draw = ImageDraw.Draw(pot)
    for _ in range(4000):
        x, y = random.uniform(0, 512), random.uniform(0, 256)
        d = random.uniform(-26, 18)
        pot_draw.point((x, y), fill=(int(160 + d), int(84 + d * 0.6), int(52 + d * 0.4)))
    save(pot.filter(ImageFilter.GaussianBlur(1.1)), 'plant-pot.png')

    mesh = Mesh()
    sides = 32

    def cone(y0, r0, y1, r1, flip=False):
        for i in range(sides):
            corners = []
            for a, y, r, v in ((i, y0, r0, 0), (i + 1, y0, r0, 0), (i + 1, y1, r1, 1), (i, y1, r1, 1)):
                angle = 2 * math.pi * a / sides
                c, s = math.cos(angle), -math.sin(angle)
                n = (c, (r0 - r1) / (y1 - y0), s)
                if flip:
                    n = tuple(-value for value in n)
                corners.append((mesh.vertex(r * c, y, r * s), mesh.texture(a / sides, v), mesh.normal(*n)))
            mesh.face(list(reversed(corners)) if flip else corners)

    mesh.group('Pot')
    mesh.use('Pot')
    cone(0, 0.2, 0.33, 0.27)
    cone(0.33, 0.3, 0.4, 0.31)
    cone(0.31, 0.25, 0.4, 0.28, flip=True)
    middle = (mesh.vertex(0, 0.33, 0), mesh.texture(0.5, 0.5), mesh.normal(0, 1, 0))
    mesh.use('Soil')
    rim = []
    for i in range(sides + 1):
        angle = 2 * math.pi * i / sides
        rim.append((mesh.vertex(0.26 * math.cos(angle), 0.33, -0.26 * math.sin(angle)), mesh.texture(0.5, 0.5),
                    middle[2]))
    for i in range(sides):
        mesh.face([middle, rim[i], rim[i + 1]])

    mesh.group('Leaves')
    mesh.use('Leaf')
    leaves = 17
    for k in range(leaves):
        turn = 2 * math.pi * (k / leaves) + random.uniform(-0.15, 0.15)
        rise = math.radians(random.uniform(48, 74))
        length, wide = random.uniform(0.55, 0.75), random.uniform(0.15, 0.2)
        droop = random.uniform(0.5, 0.8)
        way = (math.cos(turn), -math.sin(turn))
        side = (math.sin(turn), math.cos(turn))
        steps = 6
        previous = None
        for j in range(steps + 1):
            t = j / steps
            reach = length * t * math.cos(rise)
            up = length * (t * math.sin(rise) - droop * t * t * math.sin(rise))
            x, z = way[0] * reach, way[1] * reach
            y = 0.34 + up
            left = (mesh.vertex(x - side[0] * wide / 2, y, z - side[1] * wide / 2), mesh.texture(0, t), mesh.normal(0, 1, 0))
            right = (mesh.vertex(x + side[0] * wide / 2, y, z + side[1] * wide / 2), mesh.texture(1, t), left[2])
            if previous is not None:
                mesh.face([previous[0], previous[1], right, left])
            previous = (left, right)
    mesh.write('plant', 'plant.mtl', 'A potted plant 0.4 tall to the pot\'s rim, standing on the origin.')

    library('plant.mtl', '''
newmtl Pot
Kd 1 1 1
Ks 0.05 0.05 0.05
Ns 8
map_Kd plant-pot.png

newmtl Soil
Kd 0.13 0.09 0.06
Ks 0 0 0

newmtl Leaf
Kd 1 1 1
Ks 0.25 0.25 0.25
Ns 30
map_Kd plant-leaf.png
''')


if __name__ == '__main__':
    crate()
    die()
    barrel()
    plant()
