# Writes the light profiles ../a-gallery-after-hours.igl reads, as IES files (IESNA LM-63-2002, photometric type C):
# two downlights and a wall-washer.  They are made here from numbers, not measured from any maker's fitting, so they
# may live in the repository.  They are written beside this script: python3 make-light-profiles.py
import math
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def smoothstep(edge0, edge1, x):
    t = min(max((x - edge0) / (edge1 - edge0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def write(name, title, vertical, horizontal, candela):
    lines = [
        'IESNA:LM-63-2002',
        f'[TEST] Made by make-light-profiles.py, not measured',
        f'[MANUFAC] J. Stephen Kress',
        f'[LUMINAIRE] {title}',
        'TILT=NONE',
        # Lamps, lumens a lamp (-1: absolute photometry), candela multiplier, vertical and horizontal angle counts,
        # photometric type C, units in meters, width, length, height.
        f'1 -1 1 {len(vertical)} {len(horizontal)} 1 2 0.1 0.1 0.05',
        # Ballast factor, the number kept for the future, input watts.
        '1 1 12',
    ]

    def wrapped(values):
        out, line = [], []
        for value in values:
            line.append(f'{value:g}')
            if len(line) == 10:
                out.append(' '.join(line))
                line = []
        if line:
            out.append(' '.join(line))
        return out

    lines += wrapped(vertical)
    lines += wrapped(horizontal)
    for plane in candela:
        lines += wrapped([round(value, 1) for value in plane])
    with open(os.path.join(HERE, name), 'w') as file:
        file.write('\r\n'.join(lines) + '\r\n')
    print('wrote', name)


vertical = [v * 2.5 for v in range(37)]          # 0 to 90: down only

# A narrow downlight: a 24-degree beam with a hard-ish edge from the baffle, and a faint spill past it.
narrow = [[3200 * (math.cos(math.radians(v)) ** 6) * (1 - smoothstep(16, 26, v)) + 120 * (1 - smoothstep(26, 50, v))
           for v in vertical]]
write('downlight-narrow.ies', 'Narrow recessed downlight', vertical, [0], narrow)

# A wide downlight: a broad 60-degree spread, brightest a little off the axis, cut off by its trim.
wide = [[1400 * (0.8 + 0.2 * math.sin(math.radians(v * 3))) * (1 - smoothstep(30, 42, v)) + 40 * (1 - smoothstep(42, 70, v))
         for v in vertical]]
write('downlight-wide.ies', 'Wide recessed downlight', vertical, [0], wide)

# A wall-washer: most of its light thrown out sideways toward the wall it stands before (horizontal angle 0), at
# 35 to 75 degrees from straight down, little behind it, so that it reaches high and falls off gently down the wall; measured over a half, mirrored about the 0-180 plane.
horizontal = [h * 15.0 for h in range(13)]       # 0 to 180
washer = []
for h in horizontal:
    toward = 0.5 + 0.5 * math.cos(math.radians(h))   # one toward the wall, nought away from it
    plane = []
    for v in vertical:
        kick = math.exp(-((v - 56) / 22) ** 2)
        down = 0.25 * math.cos(math.radians(v)) ** 2
        plane.append(1800 * (down + kick * toward ** 2) * (1 - smoothstep(82, 90, v)))
    washer.append(plane)
write('wall-washer.ies', 'Asymmetric wall-washer', vertical, horizontal, washer)
