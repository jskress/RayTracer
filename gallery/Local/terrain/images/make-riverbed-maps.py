# Makes the pictures ../a-dry-riverbed.igl reads: a bed of river pebbles in sand -- its colors and its normal map --
# and weathered rock -- its colors and its normal map.  Every picture wraps round both ways, so it may be laid over a
# whole plane, and every one is made here from nothing but numbers, so it may live in the repository.  They are
# written beside this script:
# python3 make-riverbed-maps.py
import os
import numpy as np
from PIL import Image

N = 1024

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

def fbm(n, cells, seed, octaves=5):
    total, amp, norm = 0, 1.0, 0
    for o in range(octaves):
        total = total + amp * smooth_noise(n, cells * 2 ** o, seed + o); norm += amp; amp *= 0.5
    return total / norm

def srgb(linear):
    linear = np.clip(linear, 0, 1)
    return np.where(linear <= 0.0031308, 12.92 * linear, 1.055 * linear ** (1 / 2.4) - 0.055)

def normals(height, strength):
    # The normal map of a height drawn in texels, OpenGL's way: green up the picture.
    dhdx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) / 2 * strength
    dhdy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) / 2 * strength     # down the picture
    nx, ny, nz = -dhdx, dhdy, np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([(nx / length + 1) / 2, (ny / length + 1) / 2, (nz / length + 1) / 2], -1)

# -- Pebbles ---------------------------------------------------------------------------------------------------------

# What river pebbles are, as light: gray and blue-gray stones, buff and brown sandstones, a rusty one now and then, and
# the odd white quartz.  Dry stone gives back a fifth of the light or so; quartz more.
STONES = np.array([
    [0.20, 0.20, 0.19], [0.15, 0.155, 0.16], [0.26, 0.25, 0.23], [0.30, 0.26, 0.20], [0.22, 0.17, 0.12],
    [0.12, 0.12, 0.12], [0.28, 0.20, 0.14], [0.34, 0.32, 0.29], [0.48, 0.46, 0.43], [0.18, 0.17, 0.15]])
CHANCES = np.array([0.16, 0.13, 0.13, 0.11, 0.09, 0.09, 0.06, 0.09, 0.04, 0.10])
SAND = np.array([0.27, 0.23, 0.18])

def placed(n, tries, g, size=0.026, spread=0.38):
    # Pebbles laid largest first, each refused where it would lie too far into one laid before it: so that stones
    # meet at their edges, and none sits in the middle of another like an eye.
    centers = g.random((tries, 2)) * n
    radii = np.sort(g.lognormal(np.log(n * size), spread, tries))[::-1]
    kept = []
    for i in range(tries):
        if kept:
            others = np.array(kept)
            d = centers[others] - centers[i]
            d = d - n * np.round(d / n)
            if np.any(np.sqrt((d * d).sum(1)) < 0.85 * (radii[others] + radii[i])):
                continue
        kept.append(i)
    return centers[kept], radii[kept]

def pebbles(n, tries, seed):
    g = np.random.default_rng(seed)
    centers, radii = placed(n, tries, g)
    count = len(radii)
    turns = g.random(count) * np.pi
    aspects = g.uniform(1.05, 1.6, count)
    kinds = g.choice(len(STONES), count, p=CHANCES / CHANCES.sum())
    shades = g.normal(1, 0.1, count)
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    best = np.full((n, n), np.inf, np.float32); second = np.full((n, n), np.inf, np.float32)
    owner = np.full((n, n), -1)
    for i in range(count):
        dx = xx - centers[i, 0]; dx = dx - n * np.round(dx / n)
        dy = yy - centers[i, 1]; dy = dy - n * np.round(dy / n)
        c, s, a = np.cos(turns[i]), np.sin(turns[i]), aspects[i]
        u = (dx * c + dy * s) / a; v = (-dx * s + dy * c) * a
        d = np.sqrt(u * u + v * v) / radii[i]                  # one at the pebble's rim
        closer = d < best
        second = np.where(closer, best, np.minimum(second, d))
        owner = np.where(closer, i, owner); best = np.where(closer, d, best)
    # A pebble is where its own rim has not been reached and a neighbor is not too near: domed, flatter on top, and
    # drawn in where two touch so that each stays a stone of its own.
    inside = best < 1
    apart = np.clip((second - best) / 0.18, 0, 1)
    dome = np.sqrt(np.clip(1 - best * best, 0, 1)) * apart ** 0.5
    r = radii[owner]
    pebble_height = np.where(inside, dome * r * 0.55, 0)
    grit = fbm(n, 64, seed + 3, 3)
    sand_height = 0.6 + 1.2 * grit
    height = np.maximum(pebble_height, sand_height)
    on = pebble_height > sand_height
    speckle = 0.82 + 0.36 * fbm(n, 96, seed + 7, 3)
    veins = np.clip(1 - np.abs(smooth_noise(n, 24, seed + 9) - 0.5) * 30, 0, 1)
    color = STONES[kinds[owner]] * shades[owner][..., None] * speckle[..., None]
    color = color * (1 - 0.25 * veins[..., None] * (kinds[owner] < 3)[..., None])
    # Where two stones touch, the narrow gap between them lies in shadow.
    color = color * (0.45 + 0.55 * np.clip((second - best) / 0.12, 0, 1))[..., None]
    # The sand: finer and paler, darker in the shade of a pebble's foot.
    foot = np.clip(1 - (best - 1) / 0.35, 0, 1) * (best >= 1)
    sand = SAND * (0.78 + 0.44 * grit)[..., None] * (1 - 0.35 * foot)[..., None]
    return np.where(on[..., None], color, sand), height

# -- Rock ------------------------------------------------------------------------------------------------------------

def rock(n, seed):
    # Water-worn granite, as the boulders of a river are: gray, gently mottled, peppered with dark and pale grains,
    # crossed here and there by a thin vein of quartz, and wearing only a few small spots of lichen.
    g = np.random.default_rng(seed)
    mottle = fbm(n, 5, seed, 6)
    tone = fbm(n, 3, seed + 5, 3)
    pepper = smooth_noise(n, 256, seed + 10)
    salt = smooth_noise(n, 192, seed + 11)
    dark = np.clip((pepper - 0.72) * 6, 0, 1)
    pale = np.clip((salt - 0.74) * 6, 0, 1)
    vein_field = smooth_noise(n, 4, seed + 20) + 0.35 * smooth_noise(n, 9, seed + 21)
    veins = np.clip(1 - np.abs(vein_field - 0.66) * 70, 0, 1) * np.clip((fbm(n, 6, seed + 22, 3) - 0.45) * 6, 0, 1)
    lichen = np.clip((fbm(n, 14, seed + 30, 4) - 0.66) * 7, 0, 1)
    warm = np.array([0.215, 0.195, 0.17]); cool = np.array([0.17, 0.17, 0.165])
    base = warm * tone[..., None] + cool * (1 - tone[..., None])
    base = base * (0.78 + 0.44 * mottle)[..., None]
    color = base * (1 - 0.55 * dark[..., None]) + np.array([0.42, 0.41, 0.39]) * 0.5 * pale[..., None]
    color = color * (1 - veins[..., None]) + np.array([0.52, 0.51, 0.48]) * veins[..., None]
    color = color * (1 - 0.7 * lichen[..., None]) + np.array([0.33, 0.34, 0.29]) * 0.7 * lichen[..., None]
    height = 5 * mottle + 0.35 * dark - 0.25 * pale + 0.6 * veins + 0.4 * lichen
    return color, height

# -- Earth -----------------------------------------------------------------------------------------------------------

def earth(n, seed):
    # The banks' dry earth: brown and mottled, gritty, with small stones half sunk in it and a few fine cracks.
    g = np.random.default_rng(seed)
    mottle = fbm(n, 6, seed, 6)
    grit = fbm(n, 160, seed + 5, 3)
    base = np.array([0.17, 0.125, 0.085]) * (1 - mottle[..., None]) + np.array([0.26, 0.20, 0.14]) * mottle[..., None]
    color = base * (0.8 + 0.4 * grit)[..., None]
    height = 2 * mottle + 0.8 * grit
    centers, radii = placed(n, 4000, g, 0.006, 0.45)
    kept = g.random(len(radii)) < 0.22
    centers, radii = centers[kept], radii[kept]
    shades = g.choice([0.12, 0.16, 0.20, 0.25], len(radii)) * g.normal(1, 0.1, len(radii))
    speckle = 0.8 + 0.4 * fbm(n, 200, seed + 9, 2)
    for (cx, cy), r, shade in zip(centers, radii, shades):
        reach = int(r) + 2
        xs = np.arange(int(cx) - reach, int(cx) + reach + 1); ys = np.arange(int(cy) - reach, int(cy) + reach + 1)
        dx, dy = np.meshgrid(xs - cx, ys - cy)
        d = np.sqrt(dx * dx + dy * dy) / r
        rows, cols = np.ix_(ys % n, xs % n)
        on = d < 1
        stone = np.sqrt(np.clip(1 - d * d, 0, 1)) * r * 0.5
        lift = np.where(on, stone, 0)
        height[rows, cols] = np.maximum(height[rows, cols], 1.2 + lift)
        patch = color[rows, cols]
        lit = (0.7 + 0.3 * np.sqrt(np.clip(1 - d * d, 0, 1))) * speckle[rows, cols]
        tone = np.array([shade, shade * 0.97, shade * 0.92]) * lit[..., None]
        color[rows, cols] = np.where(on[..., None], tone, patch)
    crack_field = smooth_noise(n, 12, seed + 20) + 0.5 * smooth_noise(n, 25, seed + 21)
    cracks = np.clip(1 - np.abs(crack_field - 0.75) * 60, 0, 1) * np.clip((fbm(n, 8, seed + 22, 3) - 0.5) * 5, 0, 1)
    color = color * (1 - 0.6 * cracks[..., None])
    height = height - 1.2 * cracks
    return color, height

def save(name, values):
    here = os.path.dirname(os.path.abspath(__file__))
    Image.fromarray(np.round(np.clip(values, 0, 1) * 255).astype(np.uint8)).save(os.path.join(here, name))
    print('wrote', name, values.shape)

pebble_color, pebble_height = pebbles(N, 6000, 1984)
save('riverbed-pebbles-color.png', srgb(pebble_color))
save('riverbed-pebbles-normal.png', normals(pebble_height, 1.0))

earth_color, earth_height = earth(N, 1066)
save('riverbed-earth-color.png', srgb(earth_color))
save('riverbed-earth-normal.png', normals(earth_height, 1.0))

rock_color, rock_height = rock(N, 2001)
save('riverbed-rock-color.png', srgb(rock_color))
save('riverbed-rock-normal.png', normals(rock_height, 1.0))
