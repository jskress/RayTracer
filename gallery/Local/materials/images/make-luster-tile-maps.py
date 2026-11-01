# Makes the pictures ../copper-on-luster-tiles.igl reads: six by six glazed tiles -- their heights, colors, roughness,
# gold luster and the glaze's fine pitting -- and a hammered copper normal map.  Every picture is made here, from
# nothing but numbers, so it may live in the repository.  They are written beside this script:
# python3 make-luster-tile-maps.py
import os
import numpy as np
from PIL import Image

rng = np.random.default_rng(1977)
TILES = 6

def grid(n):
    # Pixel i stands at i / (n - 1) of the square, as a height field stands its heights, in tiles.
    t = np.linspace(0, TILES, n)
    return np.meshgrid(t, t)          # x across the picture, z down it

def smooth_noise(n, cells, seed):
    # Value noise, bicubic between random values on a coarse lattice, wrapped round.
    r = np.random.default_rng(seed).random((cells + 3, cells + 3))
    r[-3:, :] = r[:3, :]; r[:, -3:] = r[:, :3]
    t = np.linspace(0, cells, n, endpoint=False)
    i = np.floor(t).astype(int); f = t - i
    def cubic(p0, p1, p2, p3, f):
        return p1 + 0.5 * f * (p2 - p0 + f * (2 * p0 - 5 * p1 + 4 * p2 - p3 + f * (3 * (p1 - p2) + p3 - p0)))
    rows = cubic(r[:, (i - 1) % cells], r[:, i], r[:, i + 1], r[:, i + 2], f)
    return cubic(rows[(i - 1) % cells], rows[i], rows[i + 1], rows[i + 2], f[:, None])

def fbm(n, cells, seed, octaves=4):
    total, amp, norm = 0, 1.0, 0
    for o in range(octaves):
        total = total + amp * smooth_noise(n, cells * 2 ** o, seed + o); norm += amp; amp *= 0.5
    return total / norm

GROUT = 0.035      # half the grout's width, in tiles
BEVEL = 0.06       # how far in from its edge a tile's top is rounded over

def tiles(n):
    x, z = grid(n)
    ix = np.clip(np.floor(x), 0, TILES - 1).astype(int); iz = np.clip(np.floor(z), 0, TILES - 1).astype(int)
    fx, fz = x - ix, z - iz
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fz, 1 - fz))    # distance in from the tile's edge
    return x, z, ix, iz, fx, fz, edge

def height(n):
    x, z, ix, iz, fx, fz, edge = tiles(n)
    # Each tile laid a little out of true, as hand-set tiles are: tilted and slightly domed.
    tilt = np.random.default_rng(5).normal(0, 0.035, (TILES, TILES, 2))
    dome = np.random.default_rng(6).uniform(0.0, 0.05, (TILES, TILES))
    top = 0.88 + tilt[iz, ix, 0] * (fx - 0.5) + tilt[iz, ix, 1] * (fz - 0.5) + \
          dome[iz, ix] * (1 - ((fx - 0.5) ** 2 + (fz - 0.5) ** 2) * 2)
    t = np.clip((edge - GROUT) / BEVEL, 0, 1)
    shoulder = np.sqrt(1 - (1 - t) ** 2)                      # a quarter round
    grout = 0.42 + 0.02 * fbm(n, 24, 11)
    return np.where(edge < GROUT, grout, grout + (top - grout) * shoulder)

def srgb(linear):
    linear = np.clip(linear, 0, 1)
    return np.where(linear <= 0.0031308, 12.92 * linear, 1.055 * linear ** (1 / 2.4) - 0.055)

def color(n):
    x, z, ix, iz, fx, fz, edge = tiles(n)
    base = np.array([0.020, 0.115, 0.105])                    # a deep teal glaze, as light
    jitter = np.random.default_rng(7).normal(1, 0.18, (TILES, TILES, 3))
    glaze = base * jitter[iz, ix] * (0.85 + 0.3 * fbm(n, 12, 21)[..., None])
    # Glaze pools darker and richer where it runs off a tile's shoulder.
    pool = np.clip(1 - (edge - GROUT) / (BEVEL * 1.6), 0, 1)[..., None]
    glaze = glaze * (1 - 0.45 * pool)
    grout = np.array([0.33, 0.30, 0.25]) * (0.85 + 0.3 * fbm(n, 40, 31))[..., None]
    out = np.where((edge < GROUT)[..., None], grout, glaze)
    return srgb(out)

def wear(n):
    # Worn dull along a path crossing the tiles, and in patches; least on a tile's shoulders, which feet miss.
    x, z, ix, iz, fx, fz, edge = tiles(n)
    path = np.exp(-((x - 1.3 - 0.45 * z) / 1.6) ** 2)
    patches = np.clip((fbm(n, 10, 41) - 0.45) * 3, 0, 1)
    shoulder = np.clip((edge - GROUT) / (BEVEL * 2), 0, 1)
    return np.clip((0.75 * path + 0.5 * patches) * shoulder, 0, 1)

def roughness(n):
    x, z, ix, iz, fx, fz, edge = tiles(n)
    glaze = 0.03 + 0.04 * fbm(n, 60, 51) + 0.55 * wear(n)
    return np.where(edge < GROUT, 1.0, np.clip(glaze, 0, 1))

def luster(n):
    # Gold luster on every other tile: a ring and four petals, worn away where the glaze is worn.
    x, z, ix, iz, fx, fz, edge = tiles(n)
    u, v = fx - 0.5, fz - 0.5
    r = np.sqrt(u * u + v * v); a = np.arctan2(v, u)
    ring = np.abs(r - 0.30) < 0.018
    petal = np.abs(r - (0.17 + 0.06 * np.cos(4 * a))) < 0.016
    drawn = (ring | petal) & ((ix + iz) % 2 == 0)
    left = np.clip(1 - 0.8 * wear(n) + 0.25 * (fbm(n, 80, 61) - 0.5), 0, 1)
    return np.where(drawn, left, 0.0)

def pits(n):
    # The glaze's own surface: a gentle orange peel and scattered pinholes.
    peel = fbm(n, 90, 71, 3)
    holes = np.zeros((n, n))
    count = int(TILES * TILES * 40)
    pts = rng.random((count, 2)) * n
    yy, xx = np.mgrid[0:n, 0:n]
    for px, pz in pts:
        x0, x1 = int(max(px - 4, 0)), int(min(px + 5, n)); z0, z1 = int(max(pz - 4, 0)), int(min(pz + 5, n))
        d2 = (xx[z0:z1, x0:x1] - px) ** 2 + (yy[z0:z1, x0:x1] - pz) ** 2
        holes[z0:z1, x0:x1] = np.maximum(holes[z0:z1, x0:x1], np.clip(1 - d2 / 4.0, 0, 1))
    return np.clip(0.5 + 0.35 * (peel - 0.5) - 0.4 * holes, 0, 1)

def hammered(width, height, dents, seed):
    # Overlapping round dents, the picture wrapping round both ways; the normal map drawn from them, OpenGL's way.
    g = np.random.default_rng(seed)
    h = np.zeros((height, width))
    for _ in range(dents):
        cx, cy = g.random() * width, g.random() * height
        radius = g.uniform(0.012, 0.022) * width
        reach = int(radius) + 2
        xs = np.arange(int(cx) - reach, int(cx) + reach + 1)
        ys = np.arange(int(cy) - reach, int(cy) + reach + 1)
        dx, dy = np.meshgrid(xs - cx, ys - cy)
        d2 = (dx * dx + dy * dy) / (radius * radius)
        dent = np.where(d2 < 1, -(1 - d2) * radius * g.uniform(0.07, 0.12), 0)
        rows, cols = np.ix_(ys % height, xs % width)
        h[rows, cols] = np.minimum(h[rows, cols], dent)
    dhdx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / 2
    dhdy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) / 2         # down the picture
    nx, ny, nz = -dhdx, dhdy, np.ones_like(h)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([(nx / length + 1) / 2, (ny / length + 1) / 2, (nz / length + 1) / 2], -1)

def save(name, values):
    here = os.path.dirname(os.path.abspath(__file__))
    Image.fromarray(np.round(np.clip(values, 0, 1) * 255).astype(np.uint8)).save(os.path.join(here, name))
    print('wrote', name, values.shape)

save('luster-tiles-height.png', height(577))
save('luster-tiles-color.png', color(1153))
save('luster-tiles-roughness.png', roughness(1153))
save('luster-tiles-gold.png', luster(1153))
save('luster-tiles-glaze.png', pits(1729))
save('hammered-copper-normal.png', hammered(1024, 1024, 3000, 81))
