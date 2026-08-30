## Transforms

Every [surface](surfaces.md) is written as though it sat at the origin at its own natural
size.  Transforms are how it gets anywhere else — moved, resized, turned or leaned.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/transforms/transformClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/transforms/transformClause.svg">
  <img alt="Transforms" src="images/transforms/transformClause.svg">
</picture>

They may be written anywhere among a surface's other properties, and as many as you like:

```
cylinder {
    material { pigment color [0.8, 0.75, 0.65] }
    min Y 0
    max Y 1
    scale [0.3, 3, 0.3]
    rotate Z 15
    translate [-2, 0, 1]
}
```

### Order Matters

Transforms take effect **in the order you write them**, top to bottom.  This is the single
thing worth understanding about them, because writing the same two in the opposite order is a
genuinely different instruction.

![The same two transforms, both ways round](images/figures/transform-order.png)

Two identical bars seen from directly above, with the small white ball marking the origin.
Both were given a 90° turn and a move four units along X; only the order differs.

```
// Red: turned where it stands, then carried out along X.
cube {
    scale [1.1, 0.5, 0.4]
    rotate Y 90
    translate X 4
}

// Blue: carried out along X first, so the turn then swings it about the origin.
cube {
    scale [1.1, 0.5, 0.4]
    translate X 4
    rotate Y 90
}
```

The red bar ends up where you would expect: out along X, turned a quarter turn.  The blue
one is turned by the same quarter — you can see both bars lie the same way — but it is
nowhere near where the `translate X 4` put it.  That's because the rotation afterward swung
the whole arrangement about the origin and carried it round to −Z.

The rule to carry away: **a rotation turns things about the origin, not about themselves.**  A
surface still sitting at the origin has no distinction between the two, which is why turning
first and moving afterward does what you usually mean.  Move first and the rotation becomes
an orbit.

The same applies to scaling, which also works from the origin.  If you scale after translating,
you scale the *distance* as well as the thing.

The scene is [`docs/examples/transforms/order.igl`](examples/transforms/order.igl); change one
line and re-render it to see this for yourself.

### Translate

Moves a surface.  Either give all three components at once, or name a single axis:

```
translate [2, 1, -3]
translate X 2
translate Y -1
```

### Scale

Resizes a surface, from the origin.  Three forms:

```
scale 2                 // uniformly, in every direction
scale [1, 3, 1]         // each axis separately
scale Y 3               // one axis, leaving the others alone
```

A uniform scale is usually what you want.  That said, scaling unevenly is how you get other
shapes; an ellipsoid out of a sphere or a slab out of a cube.  It's also how a pattern gets
stretched; a [pigment](pigments-and-patterns.md) is evaluated in the surface's own space, so squashing the
surface squashes what is painted on it.

### Rotate

Turns a surface about one of the three axes, and about the origin.

```
rotate Y 45
rotate X -90
```

Unlike `translate` and `scale`, `rotate` **must** name an axis — there is no `rotate [x, y, z]`
form.  Turn about more than one axis by writing more than one rotation, remembering that they
apply in order:

```
rotate X 30
rotate Y 45
```

The angle is read in whatever unit the [context block](context.md#angles) names; degrees are the default.

Which way a positive angle turns follows the left hand: point your left thumb along the
positive axis, and your fingers curl the way a positive angle goes.  In practice, it is quicker
to write one, look, and flip the sign if it went the wrong way.

### Shear

Leans a surface, so that moving along one axis drags it along another — what turns a rectangle
into a parallelogram.  It takes six numbers, one for each ordered pair of axes:

```
shear [1, 0, 0, 0, 0, 0]
```

The six are, in order: X by Y, X by Z, Y by X, Y by Z, Z by X, Z by Y.  The example above
makes X grow with Y, so an upright cube leans to one side.

### Matrix

The transform of last resort: sixteen numbers, given row by row, used as-is.

```
matrix [1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        1, 0, 0, 1]
```

Almost nothing needs this.  It is here for transforms brought in from elsewhere, and for the
rare cases that none of the others can express.

### Naming a Transform

A transform may be assigned to a variable and used as often as you like, which is worth doing whenever
the same placement recurs:

```
onEdge = transform {
    scale [1, 1, 0.15]
    rotate X 90
}

cylinder { transform onEdge  translate [-3, 1, 0] }
cylinder { transform onEdge  translate [ 0, 1, 0] }
cylinder { transform onEdge  translate [ 3, 1, 0] }
```

Three discs stood on edge in a row, each written once and placed three times.

A variable holding a transform is a value like any other, so it obeys the same ordering rule:
everything inside it happens where you write the `transform`, before anything written after.

### Transforming a Group

A transform on a [group](surfaces.md#groups) applies to everything in it, after each child's
own transforms.  That is what lets you build something at the origin, in convenient
coordinates, and then place the finished assembly:

```
group {
    cube { scale [1, 0.1, 1]  translate [0, 2, 0] }
    cylinder { min Y 0  max Y 2  scale [0.15, 1, 0.15]  translate [-0.8, 0, -0.8] }
    cylinder { min Y 0  max Y 2  scale [0.15, 1, 0.15]  translate [ 0.8, 0, -0.8] }

    rotate Y 30
    translate [0, 0, 4]
}
```

Build first, place second.  Trying to write each leg already in its final position is how
scenes become impossible to adjust.

### Placing One Thing Against Another

Every position so far has been a number, and a number is often the wrong thing to have to know.  Where
a lamp goes depends on how tall the table turned out to be, and if the table is a library primitive
whose height is worked out from its own arguments, nobody wants to be the one holding that arithmetic.

So a surface may be told where to go in terms of another surface instead:

```
cube   { scale [2, 0.5, 1.2]  translate Y 0.5  named 'table' }
sphere { scale 0.4  on 'table' }
```

The ball ends up sitting on the table, touching it, centered over it.  Nothing had to know how tall the
table was, and if the table changes the ball follows.

The thing being placed against must carry a [name](#naming-a-transform); that is what `named` is for
here.

| Relation | Where it puts the thing |
| --- | --- |
| `on 'x'` | On top of it, touching. |
| `under 'x'` | Beneath it, touching. |
| `left of 'x'` | Beside it, on the `-X` side. |
| `right of 'x'` | Beside it, on the `+X` side. |
| `front of 'x'` | In front of it, toward `-Z`. |
| `behind 'x'` | Behind it, toward `+Z`. |

**Contact is not the only thing you may want to say.**  A relation above puts one thing *against*
another; sometimes what is wanted is one edge *level with* another edge, touching or not.  A table leg
goes under the top and lined up with its end — and `left of` will not say that, because `left of` means
beside, which puts the leg off the edge and in mid-air.

```
cube { scale [1.5, 0.1, 1.0]  translate Y 1  named 'top' }
cube { scale [0.1, 0.45, 0.1]  under 'top'  align left with 'top' }
```

| Relation | Where it puts the thing |
| --- | --- |
| `align left with 'x'` | Its `-X` edge level with that one's. |
| `align right with 'x'` | Its `+X` edge level with that one's. |
| `align top with 'x'` | Its `+Y` edge level with that one's. |
| `align bottom with 'x'` | Its `-Y` edge level with that one's. |
| `align front with 'x'` | Its `-Z` edge level with that one's. |
| `align back with 'x'` | Its `+Z` edge level with that one's. |

**Any of them may be given a distance**, and it means the thing you would mean.  After a contact it
is a *gap* — away from what you are against; after an alignment it is an *inset* — toward the middle
of what you lined up with:

```
cube { scale [0.78, 0.02, 0.44]  under 'top'  by 0.25 }        // a shelf slung clear of the top
cube { scale [0.05, 0.4, 0.05]   under 'top'
       align left with 'top'  by 0.08                          // a leg standing in from the corner
       align front with 'top'  by 0.08 }
```

**And what the directions nobody claimed are centered on can be chosen.**  By default they are
centered on whatever was named first, which is right nearly always — a lamp `on 'table'` wants to be
over the middle of the table.  When it is not, `centered on` settles no direction of its own and only
says which thing the rest are measured from:

```
cube { scale [0.4, 0.02, 0.3]  align bottom with 'leg'  centered on 'top' }
```

That shelf takes its height from the legs' feet and its place from the middle of the table.  Being
centered on two things at once is an error, and so is giving it a distance — it points nowhere for a
distance to be along.

The two kinds compose: one says which side of a thing you are on, the other says lined up how.  They
count the same way for everything below — each settles one direction, and telling one direction both
an alignment and a contact is the same contradiction as telling it two contacts.

**Each relation settles one direction, and the rest are centered.**  Saying a lamp goes on a table
says where it stands vertically and nothing whatever about the other two, so the middle of the table is
both the obvious answer and the only one that does not demand a second number.  Give a second relation
and it takes its own direction over, leaving anything still unclaimed centered on the first thing
named:

```
sphere { scale 0.3  on 'table'  named 'lamp' }
cube   { scale 0.25  on 'table'  right of 'lamp' }
```

Two relations for the same direction is an error rather than a race — a surface can be told one thing
per direction.

**A chain is ordinary, and the order you write it in does not matter.**  A lamp on a table on a rug
resolves the rug first, then the table, then the lamp, however they are written down; a circle of
placements is reported as one, naming the ring.

**Placement is between things in the same group.**  Two surfaces in one group share a coordinate
system, so the arithmetic is a subtraction and — this is the part that matters — the answer keeps
meaning the same thing when the group is later turned or moved.  A placement reaching into another
group would be worked out in a space that the group's own transform is about to change.

**The directions are the world's, not the thing's.**  `left of` means `-X`, whatever way the surface
it names happens to be turned.  A thing is placed by the box it occupies, and a box is square with the
axes; there is no such thing here as the left-hand side of a rotated chair.

`gallery/Local/surfaces/two-numbers-and-a-room.igl` is a whole room built this way: two numbers say
how high the table stands and how high the shelf hangs, and everything else — four legs, a stack of
books, a lamp in three pieces, the crate on the floor — follows from those two.

**A surface with no bounds cannot take part.**  An infinite `plane` occupies no region a placement
could be worked out from, at either end of the relation, and says so rather than guessing.

### Setting a Surface Moving

A `motion` block takes the same transforms but means something different by them: not where
the surface is, but where it *goes* (or, *how it moves*) while the camera's shutter is open.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/transforms/motionClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/transforms/motionClause.svg">
  <img alt="Setting a surface moving" src="images/transforms/motionClause.svg">
</picture>

```
sphere {
    translate [-2.4, 0.5, 0]
    motion { translate [0.55, 0, 0] }
}
```

That sphere sits at −2.4 and travels 0.55 units to the right during the exposure, coming out
smeared along that path.  The motion is relative to wherever the surface already is.

Nothing happens unless the camera's shutter is open — see
[Motion Blur](cameras.md#motion-blur).  A scene may leave its motions in place and simply shut
the shutter to get a still picture out.

One thing to know about interpolation.  Each transform is worked part of the way through by
its own reckoning of doing nothing, and for `scale` that is **one**, not zero:

```
motion { scale 2 }      // grows from its own size to twice it
```

Halfway through the exposure that sphere is one and a half times its size — not half of it,
which is what measuring from zero would give and would have the thing begin the exposure as a
speck.
