## Animation

An animation is a scene rendered again and again, a moment later each time.  A scene says how long it runs,
and everything in it may be worked out from **`time`** — the moment of the frame being rendered, in seconds —
or from **`frame`**, its number.  The whole scene is carried out afresh for every frame, so whatever reads
the time changes: where a thing stands, how it is turned, its color, a lamp's brightness, the sun's height,
the camera.

![A Newton's cradle, swinging](../gallery/Local/animation/newtons-cradle.webp)

```
context { animation length 2  rate 30 }

Hop = keys loop {
    0: 0
    0.5: 1.5 ease out
    1: 0 ease in
}

sphere { translate [time * 3 - 3, Hop(time) + 0.5, 0] }
```

That ball crosses six units in two seconds, hopping once a second.  `gallery/Local/animation/newtons-cradle.igl`
is a whole scene built this way.

### Saying How Long

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/animation/animationClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/animation/animationClause.svg">
  <img alt="An animation" src="images/animation/animationClause.svg">
</picture>

A context's **`animation`** says how long the scene runs: **`length`** in seconds, or **`frames`** outright,
at a **`rate`** of so many frames a second — 30 unless it says, or unless the command line's `-r` does.
**`still`** names the frame that stands for the animation as a single picture, the first unless it says.
Frame *n*'s time is *n* over the rate, so the first frame is at time nought and the last a frame short of the
length: a loop lasting exactly the length plays round without a doubled frame.

The length and the rate are worked out before anything else in the scene, since every frame's time hangs on
them, so they are numbers — or arithmetic on the renderer's own names — and not names the scene sets.

Rendered as it stands, an animation writes **every frame, each to a file of its own** carrying its number:
asked for `cradle.png`, it writes `cradle-0000000.png`, `cradle-0000001.png` and so on.  Asked for a movie, it
writes **one file** of all of them instead, each frame developed exactly as a still would be:

| | |
| --- | --- |
| **`.webp`** | The one to reach for.  Every color kept, frames timed to the thousandth of a second, looping for ever, and small: lossy, at a high quality. |
| **`.gif`** | Opens anywhere, looping for ever, but in 256 colors a frame, dithered so that gradients do not band, and timed in hundredths of a second, so 30 frames a second plays at 33. |
| **`.mp4`** | A video, H.264, for anything that plays video rather than images.  It is written by **ffmpeg**, which must be on the path: without it the render is refused at once, before a frame is rendered, saying so.  A scene that is no animation makes a video of its one frame. |

A WebP's or GIF's frames are kept until the last is done, so a long one is better made small: 400 by 300 is
plenty to see a thing move.  An MP4's go to ffmpeg as they are rendered, so it may be as long and as large as
you like.

The command line chooses frames, too:

| Option | What it renders |
| --- | --- |
| `--frames 0-143` | That run of frames, each numbered. |
| `-m 48`, `--frame 48` | Frame 48 alone, as a still at its time, written where it was asked to be. |
| `--still` | The frame the animation names as its still, as a still.  A scene that is no animation renders as it always does, so a gallery of stills is rendered with `--still` whatever each scene is. |
| `-r 60`, `--frame-rate 60` | Every frame at that rate rather than the scene's. |

A scene that is no animation can still be asked for a frame: `time` is then the frame over the rate, and
nought without one.

### Time in the Scene

`time` and `frame` are names like any other, so anything an expression can say can follow them:

```
cube { rotate Y time * 90 }                         // a quarter turn a second
point light { location [0, 4, 0]  color White * (0.5 + 0.5 * sin(time * π)) }
camera { location [8 * cos(time / 3), 3, 8 * sin(time / 3)]  look at [0, 1, 0] }
background ClearMorningFrom(time * 6)               // the sun coming round
```

They reach everywhere a scene's own names do — into the scene's functions and primitives, and into a library's,
since a library is carried out among the names of the scene importing it.  A scene that names something `time`
or `frame` for itself is free to; its own name wins.

(`sin` and `cos` take radians, whatever the context says of angles.)

**Whatever is drawn at random stays put.**  `random` is [keyed](scene-files.md#making-things-differ-from-one-another), and a field picks
what stands where by the place, so a frame agrees with the one before it and nothing scattered crawls.

### Keys

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/animation/keysClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/animation/keysClause.svg">
  <img alt="Keys" src="images/animation/keysClause.svg">
</picture>

Arithmetic on `time` is fine for a steady turn.  For a thing that goes somewhere, waits, and comes back, it is
easier to say where it is when, and let the renderer work out the way between.  **`keys`** does that: each key
a time, a colon and a value, and the whole named and called with the time, as a function is:

```
Lift = keys {
    0: [0, 0, 0]
    1.5: [0, 2, 0] ease in out
    4: [3, 2, 0]
    5: [3, 2, 0]
    6: [0, 0, 0] hold
}

cube { translate Lift(time) }
```

Before the first key the value is the first's, and after the last the last's.  A value may be a number, a
tuple, a point or vector, or a `color`, and every key's must be the same kind as the first's; each part goes
its own way between them.

How a value comes to a key from the key before is said after it:

| | |
| --- | --- |
| *(nothing)*, `linear` | At an even pace. |
| `ease in` | Slowly at first, faster and faster. |
| `ease out` | Slowing as it arrives. |
| `ease`, `ease in out` | Slowly at both ends. |
| `hold` | Not at all until the key, and then all at once. |

**`keys smooth`** passes through every key with no stop at any of them, its pace carried on from one to the next
(a Catmull-Rom curve, measured in time so that keys unevenly spaced still pass evenly); its keys take no
easing, being eased already.  **`keys loop`** begins again once the last key is reached, so a cycle written
once runs for ever: make the last key's value the first's, and it runs round without a jump.  Both together
are `keys smooth loop`.

A swing is two keys — out, slowing to a stop at the top, and back faster and faster:

```
Swing = keys loop { 0: 0  0.375: 32 ease out  0.75: 0 ease in }
```

### Along a Spline

![Darts along an oval: upright on the left, banked at nine meters a second on the right](images/figures/anim-along.png)

A thing that goes somewhere along a way is put **`along`** a spline.  A spline is
[written as a sweep's is](advanced-surfaces.md#sweep), and named so that what runs along it can say so:

```
Oval = spline {
    move to -6, 0, -6
    line to 6, 0, -6
    curve 9.314, 0, -6, 12, 0, -3.314 to 12, 0, 0
    ...
}

object Car(4.4) { along Oval by time * 12 }
object LightPlane(8) { along Oval at time / 10 banked  translate Y 30 }
```

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/animation/alongClause-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="images/animation/alongClause.svg">
  <img alt="Along a spline" src="images/animation/alongClause.svg">
</picture>

`along` is a transform, written among the others and applied in its turn: it stands the thing's origin on the
spline and turns its **+X the way the spline runs** — the way the vehicles, the trains and the aircraft are all
built to face — its +Y up and its +Z to its left.  **`at`** says how far along as a share of the spline's length,
**`by`** as a distance.  A spline is followed by distance, not by how its curves were drawn, so a thing going
`by time * 12` goes twelve units a second, on the straights and round the bends alike.  **A spline that ends where
it began is a loop**, and a distance past its end begins round it again, as does one before its start; any other
spline holds a thing at its ends.

Which way is up:

- **`upright`**, unless said: the world's up, as near as the spline lets it be.  A thing climbing a slope pitches
  with it but never rolls — a car, a train, someone walking.
- **`banked`**: the way a rider would feel to be up, leaning into a bend as a turning aircraft does or a banked
  track is laid — by the arctangent of the speed squared over the radius and gravity, which is to say not at all
  on a straight and further the faster and tighter the bend.  The speed is how fast the place along is changing
  with the time, so a thing standing still does not lean.  Gravity is taken to be 9.81, so a banked scene is
  written in meters and seconds, as the libraries are.

**`wheelbase w`** stands a thing on two places `w` apart rather than one: a rigid vehicle rides on its end axles,
so round a bend both ends are on the line, its middle a little inside it and its heading from one end to the other.
Stood by its middle, an engine on a tight curve has its ends swung off the rails.  `a-model-railway` in the gallery
runs an engine and its tender round an oval this way, each `by` its own distance along the one spline.

Three functions ask a spline the same questions, for what is not put along it — most often a camera riding the
spline or watching something that does:

```
camera {
    location pointAlong(Oval, time / 10)
    look at pointAlong(Oval, time / 10 + 0.02)
}
```

| | |
| --- | --- |
| `length(spline)` | How long it is. |
| `pointAlong(spline, share)` | The place that share of its length along it. |
| `headingAlong(spline, share)` | The way it runs there, of unit length. |

A sweep is carried along a spline by its name, too: `spline Oval` in place of one written out.  The trains
library's `TrackAlong(spline)` lays rails, sleepers and ballast along one so that a train has something to run on.

A field and a piece of text have an `along` of their own, which lays their copies or letters along a path; to move
either along a spline, put it in a group and move the group.

### Motion Blur from Time

A camera whose [shutter](cameras.md#motion-blur) is open catches what moves while it is: **a placement that
follows the time is blurred along its own way through the frame**, as far as the shutter is open — at `shutter
1` across the whole frame, at `0.5` half of it, as a film camera's half-turned shutter does.  The way need not
be straight: a ball on a swing smears along its arc, fastest at the bottom and sharp at the top where it stops.
Nothing in the scene says so but the shutter, and a placement that does not follow the time is left sharp.

A placement follows the time when its transforms are written in terms of it — directly, or by handing it to a
function or to keys, as `translate Lift(time)` does.  A value worked out from the time beforehand and named,
`x = time * 2` and then `translate X x`, is taken at the frame's own instant, and is not blurred.  Only
placement is blurred: a shape that changes with the time is taken at the frame's instant, and a camera that
moves is not blurred by its own motion.  A [`motion { }`](cameras.md#motion-blur) said outright works as it always
did, and is laid over the timed one.

### What It Costs

Every frame is the whole scene, carried out and prepared again from the start — grown trees grown, grammars
derived, the scene's acceleration built — and then rendered, so an animation costs its frames times a still,
near enough.  The cradle above is about fifteen seconds a frame at 400 by 300 and eighteen frames to its
cycle, five minutes in all.  A WebP's or GIF's frames are kept in memory until it is written, which is the other
reason to make one small; an MP4's are not.
